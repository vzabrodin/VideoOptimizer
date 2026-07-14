using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using VideoOptimizer.Helpers;

namespace VideoOptimizer.Commands;

public abstract class VideoCommandBase : ICommand
{
    [CommandOption("input", 'i', Description = "Path to the input video file.")]
    public required string InputPath { get; set; }

    [CommandOption(
        "size",
        's',
        Description = "Target file size (e.g. 25MB, 8MB, 50MB, 500KB, or just a number in MB).")]
    public required string TargetSizeString { get; set; }

    [CommandOption(
        "resolution",
        'r',
        Description = "Force target resolution height (720, 1080, 1440, 2160, or 720p, 1080p, etc.).")]
    public string? ForceResolution { get; set; }

    [CommandOption("fps", Description = "Force target framerate (e.g. 30, 60).")]
    public double? ForceFramerate { get; set; }

    [CommandOption("verbose", Description = "Show verbose output, including detailed process logs.")]
    public bool Verbose { get; set; }

    public abstract ValueTask ExecuteAsync(IConsole console);

    protected bool TryParseResolutionHeight(out int? height)
    {
        height = null;
        if (String.IsNullOrWhiteSpace(ForceResolution))
            return true;

        string clean = ForceResolution.Trim().ToLowerInvariant().Replace("p", "");
        if (!Int32.TryParse(clean, out int parsedHeight))
            return false;

        height = parsedHeight;
        return true;
    }

    protected bool TryParseTargetSizeInBytes(out long bytes)
    {
        return SizeParser.TryParse(TargetSizeString, out bytes);
    }
}