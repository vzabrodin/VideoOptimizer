using CliFx.Binding;
using CliFx.Infrastructure;
using VideoOptimizer.FFmpeg;
using VideoOptimizer.Optimization;

namespace VideoOptimizer.Commands;

[Command(
    "analyze",
    Description =
        "Calculates and outputs optimal target settings, presenting them in a before/after comparison layout alongside the predicted file size.")]
public partial class AnalyzeCommand : VideoCommandBase
{
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

            using (console.WithForegroundColor(ConsoleColor.Green))
            {
                await console.Output.WriteLineAsync(
                    "Analysis complete. Run the optimization command to encode with these parameters.");
            }
        }
        catch (Exception ex)
        {
            using (console.WithForegroundColor(ConsoleColor.Red))
            {
                await console.Error.WriteLineAsync($"Error during analysis: {ex.Message}");
                if (ex.InnerException != null)
                {
                    await console.Error.WriteLineAsync($"Details: {ex.InnerException.Message}");
                }
            }
        }
    }
}