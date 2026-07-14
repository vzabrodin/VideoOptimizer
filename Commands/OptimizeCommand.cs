using CliFx.Binding;
using CliFx.Infrastructure;
using VideoOptimizer.FFmpeg;
using VideoOptimizer.Optimization;

namespace VideoOptimizer.Commands;

[Command(Description = "Optimizes a video file to meet a target file size limit using two-pass H.264 encoding.")]
public partial class OptimizeCommand : VideoCommandBase
{
    [CommandOption("output", 'o', Description = "Path to save the optimized output video file.")]
    public required string OutputPath { get; set; }

    [CommandOption(
        "force",
        'f',
        Description = "Force encoding even if the input file size is already smaller than the target size.")]
    public bool Force { get; set; }

    public override async ValueTask ExecuteAsync(IConsole console)
    {
        CancellationToken cancellationToken = console.RegisterCancellationHandler();

        if (!TryParseTargetSizeInBytes(out long targetSizeInBytes))
        {
            await console.Error.WriteLineAsync(
                $"Error: Invalid target size format '{TargetSizeString}'. Use values like '25MB', '8MB', '500KB', or a plain number for Megabytes.");
            return;
        }

        if (!TryParseResolutionHeight(out int? forcedHeight))
        {
            await console.Error.WriteLineAsync(
                $"Error: Invalid forced resolution '{ForceResolution}'. Use values like '720', '1080', '720p', etc.");
            return;
        }

        await console.Output.WriteLineAsync($"Analyzing input file: {InputPath}...");

        try
        {
            VideoMetadata metadata = await FFmpegRunner.GetMetadataAsync(InputPath, Verbose, cancellationToken);
            var parameters = new OptimizationParams(metadata, targetSizeInBytes, forcedHeight, ForceFramerate);

            parameters.PrintComparison(console);

            bool proceeded = await OptimizationEngine.OptimizeVideoAsync(parameters, OutputPath, Force, Verbose, cancellationToken);
            if (proceeded)
            {
                await console.Output.WriteLineAsync("Video optimized successfully!");
            }
            else
            {
                await console.Output.WriteLineAsync("Optimization was skipped.");
            }
        }
        catch (Exception ex)
        {
            console.ForegroundColor = ConsoleColor.Red;
            await console.Error.WriteLineAsync($"Error during optimization: {ex.Message}");
            if (ex.InnerException != null)
                await console.Error.WriteLineAsync($"Details: {ex.InnerException.Message}");

            console.ResetColor();
        }
    }
}