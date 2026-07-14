# Video Optimizer

A high-performance C# CLI utility built to optimize and compress videos to a target file size (e.g., Discord's free 25MB limit or Nitro's 50MB/500MB limits). It analyzes video characteristics using `ffprobe`, calculates optimal video and audio parameters, and runs a precise **two-pass H.264/AAC encoding** using `ffmpeg`.

The project is fully structured, features cancellation token support, and is optimized for **Native AOT (Ahead-of-Time)** compilation for instant startup and zero external dependencies.

---

## Key Features

*   **Two-Pass Encoding**: Uses FFmpeg's two-pass `libx264` rate-control to hit the target file size with maximum possible visual quality.
*   **Smart Parameter Estimation**: 
    *   Automatically calculates target video and audio bitrates based on duration.
    *   Automatically downscales resolution (e.g., 1080p $\rightarrow$ 720p $\rightarrow$ 480p) and framerate if the target bitrate is too low to maintain the original resolution's quality.
    *   Ensures dimensions are compatible with H.264 encoding constraints (even width and height).
*   **Dry-Run Estimation (`analyze` command)**: Outputs a neat before/after parameter comparison and predicted file size without running the actual encoding process.
*   **Native AOT Compatible**: Compiles to a single, standalone native binary with zero runtime reflection and zero startup overhead. Deserializes `ffprobe` JSON outputs using Roslyn source generators.
*   **Local Executable Path Resolution**: Automatically checks the application's base directory for `ffmpeg`/`ffprobe` executables (useful for local self-contained packaging across Windows, Linux, and macOS) before falling back to system `PATH` binaries.
*   **Graceful Cancellation**: Propagates `Ctrl+C` cancellation tokens down to active `ffmpeg` and `ffprobe` processes for clean exits and temporary file cleanup.
*   **Verbose Diagnostics**: Includes a `--verbose` flag to display the raw CLI process commands and redirect live progress streams.

---

## Usage

### 1. Optimize a Video (Default Command)
Encodes the video to fit within the specified target size.

```bash
VideoOptimizer.exe --input "input.mp4" --output "output.mp4" --size 25MB
```

**Options:**
*   `-i` | `--input`: (Required) Path to the input video file.
*   `-o` | `--output`: (Required) Path to save the optimized output video file.
*   `-s` | `--size`: (Required) Target file size. Supports units like `MB`, `KB`, or raw numbers (defaults to MB). E.g., `25MB`, `8MB`, `500KB`.
*   `-r` | `--resolution`: (Optional) Force target resolution height (e.g., `720`, `1080`, `720p`, `1080p`).
*   `--fps`: (Optional) Force target framerate (e.g., `30`, `60`).
*   `-f` | `--force`: (Optional) Force encoding even if the input file size is already smaller than the target size.
*   `--verbose`: (Optional) Print raw commands and pipe full ffprobe/ffmpeg process output logs to the console.

---

### 2. Dry-Run Estimation (`analyze` subcommand)
Calculates target video settings and displays them alongside a predicted file size. Does not write files.

```bash
VideoOptimizer.exe analyze --input "input.mp4" --size 25MB
```

**Options:**
*   `-i` | `--input`, `-s` | `--size`, `-r` | `--resolution`, `--fps`, `--verbose` work exactly as described above.

**Example Comparison Table Output:**
```
================ OPTIMIZATION PARAMETERS COMPARISON ================
Parameter            Before (Original)         After (Target)           
------------------------------------------------------------------------
Resolution           1920x1080                 1280x720                 
Framerate            60.00 fps                 30.00 fps                
Video Bitrate        8500 kbps                 1420 kbps                
Audio Bitrate        320 kbps                  128 kbps                 
Total Bitrate        8820 kbps                 1548 kbps                
File Size            145.20 MB                 24.25 MB (Target: 25.00 MB)
Duration             132.50 seconds            132.50 seconds           
========================================================================
```

---

## Dependencies & Building

### Dependencies
The project uses the following libraries:
*   [CliFx](https://github.com/Tyrrrz/CliFx): Roslyn source-generated command-line arguments processor.
*   [CliWrap](https://github.com/Tyrrrz/CliWrap): Asynchronous process execution pipeline manager.

### Standard Build
```bash
dotnet build
```

### Native AOT Release Publish
To build a highly optimized, single, native executable file containing no dependencies:
```bash
dotnet publish -r win-x64 -c Release
```

The compiled binary will be located in `bin/Release/net10.0/win-x64/publish/VideoOptimizer.exe`.

---

## CI/CD Pipeline (GitHub Actions)

A GitHub Actions build workflow is available in `.github/workflows/build.yml`. It automates packaging the application:
1.  Downloads the latest `FFmpeg` source code.
2.  Sets up MSYS2 on a Windows runner and compiles a **minimal, static build of `ffmpeg.exe` and `ffprobe.exe`** targeting H.264, HEVC, VP9, AV1, AAC, Opus, FLAC, and PCM. This drops the dependency bundle size from **100MB+ to under 12MB**.
3.  Implements **caching** for the compiled `.o` object files and headers, keyed by the latest FFmpeg commit SHA. It automatically performs fast incremental updates when FFmpeg pushes updates, or skips compiling entirely on cache hits.
4.  Publishes the C# application with Native AOT.
5.  Bundles the minimal `ffmpeg.exe` and `ffprobe.exe` in the same directory and uploads it as a single ZIP file (`VideoOptimizer-win-x64`).