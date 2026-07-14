using CliFx.Infrastructure;
using VideoOptimizer.FFmpeg;

namespace VideoOptimizer.Optimization;

public class OptimizationParams
{
    public VideoMetadata Metadata { get; }

    public long TargetSizeInBytes { get; }

    public int TargetWidth { get; }

    public int TargetHeight { get; }

    public double TargetFrameRate { get; }

    public long TargetVideoBitrateBps { get; }

    public long TargetAudioBitrateBps { get; }

    public long PredictedSizeInBytes { get; }

    public OptimizationParams(VideoMetadata metadata, long targetSizeInBytes, int? forceHeight, double? forceFps)
    {
        Metadata = metadata;
        TargetSizeInBytes = targetSizeInBytes;

        // 5% safety margin to ensure we don't go over target size (especially important with muxing overhead)
        double safetyMargin = 0.95;
        double effectiveTargetBytes = targetSizeInBytes * safetyMargin;

        // Total target bitrate
        double totalTargetBitrateBps = (effectiveTargetBytes * 8.0) / metadata.DurationSeconds;

        // Audio bitrate selection
        if (metadata.HasAudio)
        {
            if (totalTargetBitrateBps > 512000)
                TargetAudioBitrateBps = 128000;
            else if (totalTargetBitrateBps > 256000)
                TargetAudioBitrateBps = 96000;
            else if (totalTargetBitrateBps > 128000)
                TargetAudioBitrateBps = 64000;
            else
                TargetAudioBitrateBps = Math.Max(32000, (long) (totalTargetBitrateBps * 0.20));
        }
        else
        {
            TargetAudioBitrateBps = 0;
        }

        // Video bitrate is remainder
        double videoBitrate = totalTargetBitrateBps - TargetAudioBitrateBps;
        if (videoBitrate < 10000)
            throw new InvalidOperationException(
                $"The target size is too small ({targetSizeInBytes / (1024.0 * 1024.0):F2} MB) for the duration of the video ({metadata.DurationSeconds:F2} seconds). The required bitrate is too low to encode.");

        TargetVideoBitrateBps = (long) videoBitrate;

        // Determine Target Resolution
        int height = metadata.Height;
        if (forceHeight.HasValue)
        {
            height = forceHeight.Value;
        }
        else
        {
            // Auto scale down if bitrate is low
            if (TargetVideoBitrateBps < 300000)
                height = Math.Min(height, 240);
            else if (TargetVideoBitrateBps < 600000)
                height = Math.Min(height, 360);
            else if (TargetVideoBitrateBps < 1200000)
                height = Math.Min(height, 480);
            else if (TargetVideoBitrateBps < 2500000)
                height = Math.Min(height, 720);
            else if (TargetVideoBitrateBps < 5000000)
                height = Math.Min(height, 1080);
            else if (TargetVideoBitrateBps < 10000000)
                height = Math.Min(height, 1440);
        }

        // Target heights must be divisible by 2 for H.264
        TargetHeight = (height / 2) * 2;

        if (TargetHeight == metadata.Height)
        {
            TargetWidth = metadata.Width;
        }
        else
        {
            // maintain aspect ratio, width must also be divisible by 2
            TargetWidth = (int) Math.Round((double) metadata.Width * TargetHeight / metadata.Height / 2.0) * 2;
        }

        // Determine Target Framerate
        double fps = metadata.FrameRate;
        if (forceFps.HasValue)
        {
            fps = forceFps.Value;
        }
        else
        {
            // Auto scale down framerate if bitrate is low
            if (TargetVideoBitrateBps < 600000 && metadata.FrameRate > 30)
            {
                fps = 30;
            }
        }

        TargetFrameRate = fps;

        // Predicted size (re-calculate based on actual target bitrates, and add a small 1% container overhead estimation)
        double predictedBytes = ((TargetVideoBitrateBps + TargetAudioBitrateBps) * metadata.DurationSeconds) / 8.0;
        PredictedSizeInBytes = (long) (predictedBytes * 1.01);
    }

    public void PrintComparison(IConsole console)
    {
        console.Output.WriteLine();
        console.ForegroundColor = ConsoleColor.Cyan;
        console.Output.WriteLine("================ OPTIMIZATION PARAMETERS COMPARISON ================");
        console.ResetColor();
        console.Output.WriteLine($"{"Parameter",-20} {"Before (Original)",-25} {"After (Target)",-25}");
        console.Output.WriteLine(new string('-', 72));

        string originalRes = $"{Metadata.Width}x{Metadata.Height}";
        string targetRes = $"{TargetWidth}x{TargetHeight}";
        console.Output.WriteLine($"{"Resolution",-20} {originalRes,-25} {targetRes,-25}");

        console.Output.WriteLine(
            "{0,-20} {1,-25} {2,-25}",
            "Framerate",
            $"{Metadata.FrameRate:F2} fps",
            $"{TargetFrameRate:F2} fps");

        console.Output.WriteLine(
            "{0,-20} {1,-25} {2,-25}",
            "Video Bitrate",
            $"{Metadata.VideoBitrateBps / 1000.0:F0} kbps",
            $"{TargetVideoBitrateBps / 1000.0:F0} kbps");

        string originalAudio = Metadata.HasAudio ? $"{Metadata.AudioBitrateBps / 1000.0:F0} kbps" : "N/A (No Audio)";
        string targetAudio = TargetAudioBitrateBps > 0 ? $"{TargetAudioBitrateBps / 1000.0:F0} kbps" : "N/A (No Audio)";
        console.Output.WriteLine($"{"Audio Bitrate",-20} {originalAudio,-25} {targetAudio,-25}");

        long originalTotalBr = Metadata.VideoBitrateBps + Metadata.AudioBitrateBps;
        long targetTotalBr = TargetVideoBitrateBps + TargetAudioBitrateBps;
        console.Output.WriteLine(
            "{0,-20} {1,-25} {2,-25}",
            "Total Bitrate",
            $"{originalTotalBr / 1000.0:F0} kbps",
            $"{targetTotalBr / 1000.0:F0} kbps");

        console.Output.WriteLine(
            "{0,-20} {1,-25} {2,-25}",
            "File Size",
            $"{Metadata.FileSize / (1024.0 * 1024.0):F2} MB",
            $"{PredictedSizeInBytes / (1024.0 * 1024.0):F2} MB (Target: {TargetSizeInBytes / (1024.0 * 1024.0):F2} MB)");

        console.Output.WriteLine(
            "{0,-20} {1,-25} {2,-25}",
            "Duration",
            $"{Metadata.DurationSeconds:F2} seconds",
            $"{Metadata.DurationSeconds:F2} seconds");

        console.ForegroundColor = ConsoleColor.Cyan;
        console.Output.WriteLine("====================================================================");
        console.ResetColor();
        console.Output.WriteLine();
    }
}
