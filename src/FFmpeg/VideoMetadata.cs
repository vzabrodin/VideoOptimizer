using System.Globalization;
using System.Text.Json.Serialization;

namespace VideoOptimizer.FFmpeg;

public class FFprobeResult
{
    [JsonPropertyName("streams")]
    public List<FFprobeStream>? Streams { get; set; }

    [JsonPropertyName("format")]
    public FFprobeFormat? Format { get; set; }
}

public class FFprobeStream
{
    [JsonPropertyName("codec_type")]
    public string? CodecType { get; set; }

    [JsonPropertyName("codec_name")]
    public string? CodecName { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("r_frame_rate")]
    public string? RFrameRate { get; set; }

    [JsonPropertyName("avg_frame_rate")]
    public string? AvgFrameRate { get; set; }

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("bit_rate")]
    public string? BitRate { get; set; }
}

public class FFprobeFormat
{
    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("size")]
    public string? Size { get; set; }

    [JsonPropertyName("bit_rate")]
    public string? BitRate { get; set; }
}

public class VideoMetadata
{
    public string InputPath { get; }

    public double DurationSeconds { get; }

    public int Width { get; }

    public int Height { get; }

    public double FrameRate { get; }

    public long FileSize { get; }

    public long VideoBitrateBps { get; }

    public long AudioBitrateBps { get; }

    public bool HasAudio { get; }

    public VideoMetadata(string inputPath, FFprobeResult probeResult, long actualFileSize)
    {
        InputPath = inputPath;
        FileSize = actualFileSize;

        // Try to parse duration from format, then stream
        double duration = 0;
        if (probeResult.Format?.Duration != null &&
            Double.TryParse(
                probeResult.Format.Duration,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double formatDuration))
        {
            duration = formatDuration;
        }

        FFprobeStream? videoStream = null;
        FFprobeStream? audioStream = null;

        if (probeResult.Streams != null)
        {
            foreach (FFprobeStream stream in probeResult.Streams)
            {
                switch (stream.CodecType)
                {
                    case "video" when videoStream == null:
                        videoStream = stream;
                        break;
                    case "audio" when audioStream == null:
                        audioStream = stream;
                        break;
                }
            }
        }

        if (videoStream == null)
            throw new InvalidOperationException("No video stream found in the source file.");

        if (duration == 0 && videoStream.Duration != null &&
            Double.TryParse(
                videoStream.Duration,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double videoDuration))
        {
            duration = videoDuration;
        }

        DurationSeconds = duration > 0
            ? duration
            : throw new InvalidOperationException("Could not determine video duration.");
        Width = videoStream.Width ?? throw new InvalidOperationException("Could not determine video width.");
        Height = videoStream.Height ?? throw new InvalidOperationException("Could not determine video height.");

        // Parse FrameRate
        double fps = ParseFrameRate(videoStream.AvgFrameRate) ?? ParseFrameRate(videoStream.RFrameRate) ?? 30.0;
        FrameRate = fps;

        // Parse Bitrates
        long videoBitrate = 0;
        if (videoStream.BitRate != null && Int64.TryParse(videoStream.BitRate, out long vBr))
        {
            videoBitrate = vBr;
        }

        long audioBitrate = 0;
        HasAudio = audioStream != null;
        if (audioStream is { BitRate: not null } && Int64.TryParse(audioStream.BitRate, out long aBr))
        {
            audioBitrate = aBr;
        }

        // If bitrates are 0, estimate from file size and duration
        if (videoBitrate == 0)
        {
            long totalBitrate = (long) ((FileSize * 8.0) / DurationSeconds);
            if (HasAudio)
            {
                audioBitrate = audioBitrate > 0 ? audioBitrate : 128000; // Assume 128kbps if unknown
                videoBitrate = Math.Max(0, totalBitrate - audioBitrate);
            }
            else
            {
                videoBitrate = totalBitrate;
            }
        }
        else if (HasAudio && audioBitrate == 0)
        {
            audioBitrate = 128000;
        }

        VideoBitrateBps = videoBitrate;
        AudioBitrateBps = audioBitrate;
    }

    private static double? ParseFrameRate(string? rFrameRate)
    {
        if (String.IsNullOrWhiteSpace(rFrameRate))
            return null;

        string[] parts = rFrameRate.Split('/');
        if (parts.Length == 1)
        {
            if (Double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                return val;
        }
        else if (parts.Length == 2)
        {
            if (!Double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double num) ||
                !Double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double den))
                return null;

            if (den != 0)
                return num / den;
        }

        return null;
    }
}
