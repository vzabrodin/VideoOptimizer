using System.Globalization;
using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;

namespace VideoOptimizer.FFmpeg;

public static class FFmpegRunner
{
    private static readonly string FFmpegPath = ResolveExecutablePath("ffmpeg");
    private static readonly string FFprobePath = ResolveExecutablePath("ffprobe");

    private static string ResolveExecutablePath(string baseName)
    {
        string extension = OperatingSystem.IsWindows() ? ".exe" : "";
        string fileName = baseName + extension;
        string localPath = Path.Combine(AppContext.BaseDirectory, fileName);
        return File.Exists(localPath) ? localPath : baseName;
    }

    public static async Task<VideoMetadata> GetMetadataAsync(
        string inputPath,
        bool verbose = false,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input video file not found.", inputPath);

        long actualFileSize = new FileInfo(inputPath).Length;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(">>> Running ffprobe (Analyzing video metadata)...");
        Console.ResetColor();

        if (verbose)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(
                $"[Verbose] Running: {FFprobePath} -v error -show_format -show_streams -print_format json \"{inputPath}\"");
            Console.ResetColor();
        }

        BufferedCommandResult result = await Cli.Wrap(FFprobePath)
            .WithArguments(["-v", "error", "-show_format", "-show_streams", "-print_format", "json", inputPath])
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(cancellationToken);

        if (verbose)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[Verbose] ffprobe Exit Code: {result.ExitCode}");
            if (result.ExitCode != 0)
            {
                Console.WriteLine($"[Verbose] ffprobe Stderr: {result.StandardError}");
            }
            else
            {
                Console.WriteLine("[Verbose] ffprobe Stdout (JSON):");
                Console.WriteLine(result.StandardOutput);
            }

            Console.ResetColor();
        }

        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"ffprobe failed to analyze the file (exit code {result.ExitCode}). Error: {result.StandardError}");

        try
        {
            FFprobeResult? probeResult = JsonSerializer.Deserialize(
                result.StandardOutput,
                FFprobeJsonContext.Default.FFprobeResult);
            if (probeResult == null)
                throw new InvalidOperationException("Failed to deserialize ffprobe output.");

            return new VideoMetadata(inputPath, probeResult, actualFileSize);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to parse video metadata from ffprobe output.", ex);
        }
    }

    public static async Task RunTwoPassEncodeAsync(
        string inputPath,
        string outputPath,
        long videoBitrateBps,
        long audioBitrateBps,
        int targetWidth,
        int targetHeight,
        double targetFps,
        int originalWidth,
        int originalHeight,
        double originalFps,
        bool hasAudio,
        bool verbose = false,
        CancellationToken cancellationToken = default)
    {
        string passLogPrefix = Path.Combine(Path.GetTempPath(), $"ffmpeg2pass_{Guid.NewGuid()}");

        try
        {
            // Ensure output directory exists
            string? outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!String.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            string nullDevice = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";

            // --- PASS 1 ---
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(">>> Running Pass 1 of 2 (Analyzing video dynamics)...");
            Console.ResetColor();

            var pass1Args = new List<string>
            {
                "-y",
                "-hide_banner",
                "-i",
                inputPath,
                "-c:v",
                "libx264",
                "-b:v",
                $"{videoBitrateBps}",
                "-pass",
                "1",
                "-passlogfile",
                passLogPrefix
            };

            if (targetHeight != originalHeight || targetWidth != originalWidth)
            {
                pass1Args.Add("-vf");
                pass1Args.Add($"scale={targetWidth}:{targetHeight}");
            }

            if (Math.Abs(targetFps - originalFps) > 0.01)
            {
                pass1Args.Add("-r");
                pass1Args.Add($"{targetFps.ToString(CultureInfo.InvariantCulture)}");
            }

            pass1Args.AddRange(["-an", "-f", "mp4", nullDevice]);

            Command pass1Cmd = Cli.Wrap(FFmpegPath)
                .WithArguments(pass1Args)
                .WithValidation(CommandResultValidation.None);

            if (verbose)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"[Verbose] Running: {FFmpegPath} {String.Join(" ", pass1Args)}");
                Console.ResetColor();
                pass1Cmd = pass1Cmd
                    .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
                    .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()));
            }

            CommandResult pass1Result = await pass1Cmd.ExecuteAsync(cancellationToken);
            if (pass1Result.ExitCode != 0)
                throw new InvalidOperationException($"ffmpeg first pass failed with exit code {pass1Result.ExitCode}.");

            // --- PASS 2 ---
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n>>> Running Pass 2 of 2 (Encoding output video)...");
            Console.ResetColor();

            var pass2Args = new List<string>
            {
                "-y",
                "-hide_banner",
                "-i",
                inputPath,
                "-c:v",
                "libx264",
                "-b:v",
                $"{videoBitrateBps}",
                "-pass",
                "2",
                "-passlogfile",
                passLogPrefix
            };

            if (targetHeight != originalHeight || targetWidth != originalWidth)
            {
                pass2Args.Add("-vf");
                pass2Args.Add($"scale={targetWidth}:{targetHeight}");
            }

            if (Math.Abs(targetFps - originalFps) > 0.01)
            {
                pass2Args.Add("-r");
                pass2Args.Add($"{targetFps.ToString(CultureInfo.InvariantCulture)}");
            }

            if (audioBitrateBps > 0 && hasAudio)
            {
                pass2Args.Add("-c:a");
                pass2Args.Add("aac");
                pass2Args.Add("-b:a");
                pass2Args.Add($"{audioBitrateBps}");
            }
            else
            {
                pass2Args.Add("-an");
            }

            pass2Args.Add(outputPath);

            Command pass2Cmd = Cli.Wrap(FFmpegPath)
                .WithArguments(pass2Args)
                .WithValidation(CommandResultValidation.None);

            if (verbose)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"[Verbose] Running: {FFmpegPath} {String.Join(" ", pass2Args)}");
                Console.ResetColor();
                pass2Cmd = pass2Cmd
                    .WithStandardErrorPipe(PipeTarget.ToStream(Console.OpenStandardError()))
                    .WithStandardOutputPipe(PipeTarget.ToStream(Console.OpenStandardOutput()));
            }

            CommandResult pass2Result = await pass2Cmd.ExecuteAsync(cancellationToken);
            if (pass2Result.ExitCode != 0)
                throw new InvalidOperationException(
                    $"ffmpeg second pass failed with exit code {pass2Result.ExitCode}.");
        }
        finally
        {
            // Clean up ffmpeg log files
            try
            {
                string logFile = $"{passLogPrefix}-0.log";
                string mbtreeFile = $"{passLogPrefix}-0.log.mbtree";
                if (File.Exists(logFile))
                    File.Delete(logFile);
                if (File.Exists(mbtreeFile))
                    File.Delete(mbtreeFile);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: Failed to clean up 2-pass log files: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}
