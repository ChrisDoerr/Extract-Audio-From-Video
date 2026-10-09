using System.IO;
using System.Diagnostics;
using System.Globalization;

namespace ExtractAudioFromVideo;

internal sealed class FfmpegAudioExtractor
{
    public async Task<string> ExtractFirstAudioAsM4aAsync(
        string videoPath,
        string outputBaseName,
        IProgress<double?>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);
        ValidateOutputBaseName(outputBaseName);

        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException("The selected video file was not found.", videoPath);
        }

        var outputDirectory = Path.GetDirectoryName(videoPath)
            ?? throw new InvalidOperationException("The folder of the video file could not be determined.");
        var outputPath = GetAvailableOutputPath(outputDirectory, outputBaseName);
        progress?.Report(null);
        var duration = await TryGetDurationAsync(videoPath);
        progress?.Report(duration is null ? null : 0d);

        var startInfo = new ProcessStartInfo
        {
            FileName = GetBundledToolPath("ffmpeg.exe"),
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-nostdin");
        startInfo.ArgumentList.Add("-loglevel");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-progress");
        startInfo.ArgumentList.Add("pipe:1");
        startInfo.ArgumentList.Add("-nostats");
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(videoPath);
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("0:a:0");
        startInfo.ArgumentList.Add("-vn");
        startInfo.ArgumentList.Add("-c:a");
        startInfo.ArgumentList.Add("aac");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("ipod");
        startInfo.ArgumentList.Add(outputPath);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("The ffmpeg process could not be started.");
        }

        var standardOutputTask = ReadProgressAsync(process, duration, progress);
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var errorOutput = await standardErrorTask;
        await standardOutputTask;

        if (process.ExitCode != 0)
        {
            throw new FfmpegExecutionException(errorOutput, process.ExitCode);
        }

        progress?.Report(1d);
        return outputPath;
    }

    private static async Task<double?> TryGetDurationAsync(string videoPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = GetBundledToolPath("ffprobe.exe"),
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-show_entries");
        startInfo.ArgumentList.Add("format=duration");
        startInfo.ArgumentList.Add("-of");
        startInfo.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
        startInfo.ArgumentList.Add(videoPath);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            return null;
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        await errorTask;

        return process.ExitCode == 0
            && double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration)
            && double.IsFinite(duration)
            && duration > 0
                ? duration
                : null;
    }

    private static string GetBundledToolPath(string executableName) =>
        Path.Combine(
            AppContext.BaseDirectory,
            "tools",
            "ffmpeg",
            "win-x64",
            "bin",
            executableName);

    private static async Task ReadProgressAsync(
        Process process,
        double? duration,
        IProgress<double?>? progress)
    {
        while (await process.StandardOutput.ReadLineAsync() is { } line)
        {
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }

            var key = line[..separatorIndex];
            var value = line[(separatorIndex + 1)..];
            if (key is not ("out_time_us" or "out_time_ms" or "out_time"))
            {
                continue;
            }

            if (duration is not > 0
                || !TryParseOutputTime(key, value, out var elapsedSeconds))
            {
                progress?.Report(null);
                continue;
            }

            var fraction = Math.Clamp(elapsedSeconds / duration.Value, 0d, 0.99d);
            progress?.Report(fraction);
        }
    }

    private static bool TryParseOutputTime(string key, string value, out double seconds)
    {
        if (key is "out_time_us" or "out_time_ms"
            && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds))
        {
            seconds = microseconds / 1_000_000d;
            return true;
        }

        if (key == "out_time"
            && TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var time))
        {
            seconds = time.TotalSeconds;
            return true;
        }

        seconds = 0;
        return false;
    }

    private static void ValidateOutputBaseName(string outputBaseName)
    {
        if (string.IsNullOrWhiteSpace(outputBaseName))
        {
            throw new ArgumentException("Please enter an output name.", nameof(outputBaseName));
        }

        if (outputBaseName is "." or ".."
            || outputBaseName.EndsWith(' ')
            || outputBaseName.EndsWith('.')
            || outputBaseName.Any(character =>
                character < 32 || "<>:\"/\\|?*".IndexOf(character) >= 0))
        {
            throw new ArgumentException("The output name contains characters not allowed in a Windows file name.", nameof(outputBaseName));
        }

        var deviceName = outputBaseName.Split('.')[0];
        if (IsReservedDeviceName(deviceName))
        {
            throw new ArgumentException("This output name is reserved by Windows.", nameof(outputBaseName));
        }
    }

    private static bool IsReservedDeviceName(string name)
    {
        if (name.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || name.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || name.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || name.Equals("NUL", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return name.Length == 4
            && (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
            && name[3] is >= '1' and <= '9';
    }

    private static string GetAvailableOutputPath(string directory, string outputBaseName)
    {
        var candidate = Path.Combine(directory, $"{outputBaseName}.m4a");
        if (!File.Exists(candidate) && !Directory.Exists(candidate))
        {
            return candidate;
        }

        for (long suffix = 1; ; suffix++)
        {
            candidate = Path.Combine(
                directory,
                $"{outputBaseName} ({suffix.ToString(CultureInfo.InvariantCulture)}).m4a");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}

internal sealed class FfmpegExecutionException(string errorOutput, int exitCode)
    : Exception($"ffmpeg exited with exit code {exitCode}.")
{
    public string ErrorOutput { get; } = errorOutput;
}
