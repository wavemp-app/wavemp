using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Transcoding.Options;

namespace WaveMP.Core.Subsonic.Media;

/// <summary>
/// Starts ffmpeg from <see cref="TranscodingOptions.FfmpegPath"/> (the ffmpeg of the HLS transcoding) without a shell
/// and without a window, with the arguments of <see cref="SubsonicTranscodeArguments"/>.
/// </summary>
internal sealed class SubsonicTranscoder(
    IOptions<TranscodingOptions> options,
    ILogger<SubsonicTranscoder> logger) : ISubsonicTranscoder
{
    public ISubsonicTranscode? Start(string inputPath, SubsonicStreamPlan plan)
    {
        ProcessStartInfo startInfo = new(options.Value.FfmpegPath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in SubsonicTranscodeArguments.Build(inputPath, plan))
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process process = new() { StartInfo = startInfo, EnableRaisingEvents = true };
        try
        {
            if (!process.Start())
            {
                process.Dispose();
                logger.LogWarning("ffmpeg ({Ffmpeg}) did not start", startInfo.FileName);
                return null;
            }
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            process.Dispose();
            logger.LogWarning(exception, "ffmpeg ({Ffmpeg}) cannot be started", startInfo.FileName);
            return null;
        }

        return new Transcode(process);
    }

    private sealed class Transcode : ISubsonicTranscode
    {
        private const int MaxErrorLength = 4000;

        private readonly Process _process;
        private readonly StringBuilder _error = new();
        private readonly Lock _lock = new();

        public Transcode(Process process)
        {
            _process = process;
            process.ErrorDataReceived += (_, line) => Append(line.Data);
            process.BeginErrorReadLine();
            try
            {
                process.StandardInput.Close();
            }
            catch (IOException)
            {
                // Already gone.
            }

            Exited = WaitAsync(process);
        }

        public Stream Output => _process.StandardOutput.BaseStream;

        public Task<int> Exited { get; }

        public string ErrorOutput
        {
            get
            {
                lock (_lock)
                {
                    return _error.ToString().Trim();
                }
            }
        }

        public void Dispose()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
            {
                // Ended in the meantime.
            }

            _process.Dispose();
        }

        private static async Task<int> WaitAsync(Process process)
        {
            await process.WaitForExitAsync();
            return process.ExitCode;
        }

        private void Append(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (_lock)
            {
                _error.AppendLine(line);
                if (_error.Length > MaxErrorLength)
                {
                    _error.Remove(0, _error.Length - MaxErrorLength);
                }
            }
        }
    }
}
