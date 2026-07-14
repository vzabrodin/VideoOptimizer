using VideoOptimizer.FFmpeg;

namespace VideoOptimizer.Optimization;

public static class OptimizationEngine
{
    public static async Task<bool> OptimizeVideoAsync(
        OptimizationParams parameters,
        string outputPath,
        bool forceOverride,
        bool verbose = false,
        CancellationToken cancellationToken = default)
    {
        // Check if file is already within the target size
        if (parameters.Metadata.FileSize <= parameters.TargetSizeInBytes && !forceOverride)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(
                $"The input file is already optimized! Size: {parameters.Metadata.FileSize / (1024.0 * 1024.0):F2} MB (Target: {parameters.TargetSizeInBytes / (1024.0 * 1024.0):F2} MB)");
            Console.WriteLine("Use the '--force' parameter to re-encode and optimize anyway.");
            Console.ResetColor();
            return false;
        }

        // Delegate process execution to FFmpegRunner
        await FFmpegRunner.RunTwoPassEncodeAsync(
            parameters.Metadata.InputPath,
            outputPath,
            parameters.TargetVideoBitrateBps,
            parameters.TargetAudioBitrateBps,
            parameters.TargetWidth,
            parameters.TargetHeight,
            parameters.TargetFrameRate,
            parameters.Metadata.Width,
            parameters.Metadata.Height,
            parameters.Metadata.FrameRate,
            parameters.Metadata.HasAudio,
            verbose,
            cancellationToken);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\nOptimization complete!");
        if (File.Exists(outputPath))
        {
            var outInfo = new FileInfo(outputPath);
            Console.WriteLine($"Output file size: {outInfo.Length / (1024.0 * 1024.0):F2} MB");
        }

        Console.ResetColor();
        return true;
    }
}