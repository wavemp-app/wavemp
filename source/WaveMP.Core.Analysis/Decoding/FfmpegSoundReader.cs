using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Analysis.Features;
using WaveMP.Core.Analysis.Options;
using WaveMP.Core.Transcoding.Options;

namespace WaveMP.Core.Analysis.Decoding;

/// <summary>
/// Reads the sound with ffmpeg (from <see cref="TranscodingOptions.FfmpegPath"/>, without a shell) in one pass: the
/// first audio track is split, one branch is measured with the EBU R 128 filter (integrated loudness in the summary of
/// the error output), the other one is resampled to mono at <see cref="SoundFeatures.SampleRate"/> and read from the
/// standard output into the <see cref="SoundFeatureBuilder"/>. ffmpeg runs with one thread at the lowest process
/// priority, so playback and transcodes always come first.
/// </summary>
internal sealed partial class FfmpegSoundReader(
    IOptions<TranscodingOptions> transcoding,
    IOptions<AnalysisOptions> options,
    ILogger<FfmpegSoundReader> logger) : ISoundReader
{
    private const int MaxErrorLength = 16_000;

    public async Task<SoundReading> ReadAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        AnalysisOptions analysis = options.Value;
        ProcessStartInfo startInfo = new(transcoding.Value.FfmpegPath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in Arguments(fullPath, analysis.MaxDuration))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };
        StringBuilder error = new();
        try
        {
            if (!process.Start())
            {
                logger.LogWarning("ffmpeg did not start for the song analysis");
                return SoundReading.Failed;
            }
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            logger.LogWarning(exception, "ffmpeg cannot be started for the song analysis");
            return SoundReading.Failed;
        }

        LowerPriority(process);
        process.ErrorDataReceived += (_, line) => Append(error, line.Data);
        process.BeginErrorReadLine();
        process.StandardInput.Close();

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(analysis.ReadTimeout);
        SoundFeatureBuilder builder = new();
        try
        {
            await ReadSamplesAsync(process.StandardOutput.BaseStream, builder, limit.Token);
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogWarning("The song analysis of {Path} took too long", fullPath);
            return SoundReading.Failed;
        }

        // Let the last lines of the error output arrive.
        process.WaitForExit();
        string output;
        lock (error)
        {
            output = error.ToString();
        }

        if (process.ExitCode != 0)
        {
            if (output.Contains("matches no streams", StringComparison.Ordinal))
            {
                return SoundReading.NoSound;
            }

            logger.LogInformation(
                "ffmpeg could not read the sound of {Path} (exit code {ExitCode}): {Error}",
                fullPath,
                process.ExitCode,
                Tail(output));
            return SoundReading.Failed;
        }

        SoundFeatures features = builder.Build();
        return features.FrameCount == 0
            ? SoundReading.NoSound
            : new SoundReading(Data.Entities.SongAnalysisStatus.Analyzed, features, ParseLoudness(output));
    }

    /// <summary>
    /// The arguments of ffmpeg for a file.
    /// </summary>
    internal static IReadOnlyList<string> Arguments(string fullPath, TimeSpan maxDuration)
    {
        string filter = "[0:a:0]asplit=2[pcm][meter];"
            + "[meter]ebur128=framelog=quiet,anullsink;"
            + string.Create(
                CultureInfo.InvariantCulture,
                $"[pcm]aresample={SoundFeatures.SampleRate},aformat=sample_fmts=flt:channel_layouts=mono[out]");
        return
        [
            "-hide_banner", "-nostdin", "-nostats", "-loglevel", "info", "-threads", "1",
            "-t", maxDuration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture),
            "-i", fullPath,
            "-filter_complex", filter,
            "-map", "[out]", "-f", "f32le", "pipe:1",
        ];
    }

    /// <summary>
    /// The integrated loudness from the summary of the EBU R 128 filter; <c>null</c> without one or for silence.
    /// </summary>
    internal static double? ParseLoudness(string errorOutput)
    {
        int summary = errorOutput.LastIndexOf("Summary:", StringComparison.Ordinal);
        if (summary < 0)
        {
            return null;
        }

        Match match = IntegratedLoudness().Match(errorOutput, summary);
        return match.Success
            && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lufs)
            && lufs > -70
                ? lufs
                : null;
    }

    private static async Task ReadSamplesAsync(
        Stream stream,
        SoundFeatureBuilder builder,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[64 * 1024];
        int carried = 0;
        while (true)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(carried), cancellationToken);
            if (read == 0)
            {
                break;
            }

            int available = carried + read;
            int whole = available - (available % sizeof(float));
            builder.Add(MemoryMarshal.Cast<byte, float>(buffer.AsSpan(0, whole)));
            carried = available - whole;
            if (carried > 0)
            {
                Array.Copy(buffer, whole, buffer, 0, carried);
            }
        }
    }

    private static void LowerPriority(Process process)
    {
        try
        {
            process.PriorityClass = ProcessPriorityClass.Idle;
        }
        catch (Exception exception) when (exception is Win32Exception
            or InvalidOperationException
            or PlatformNotSupportedException)
        {
            // Runs at the normal priority; one thread keeps the load low anyway.
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            // Ended in the meantime.
        }
    }

    private static void Append(StringBuilder error, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (error)
        {
            error.AppendLine(line);
            if (error.Length > MaxErrorLength)
            {
                error.Remove(0, error.Length - MaxErrorLength);
            }
        }
    }

    private static string Tail(string output)
    {
        string trimmed = output.Trim();
        return trimmed.Length <= 500 ? trimmed : trimmed[^500..];
    }

    [GeneratedRegex(@"\bI:\s+(-?\d+(?:\.\d+)?) LUFS")]
    private static partial Regex IntegratedLoudness();
}
