using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Recaps.Options;
using WaveMP.Core.Transcoding.Options;
using WaveMP.Core.Transcoding.Processes;

namespace WaveMP.Core.Recaps.Videos;

internal sealed class RecapVideoConverter(
    IFfmpegCommandRunner ffmpeg,
    IOptions<RecapOptions> options,
    IOptions<TranscodingOptions> transcodingOptions,
    ILogger<RecapVideoConverter> logger) : IRecapVideoConverter
{
    private const int BufferSize = 81_920;

    public async Task<RecapVideoResult> ConvertAsync(Stream content, CancellationToken cancellationToken = default)
    {
        RecapOptions settings = options.Value;
        string name = Guid.CreateVersion7().ToString("N");
        string input = Path.Combine(settings.VideoPath, name + ".upload");
        string output = Path.Combine(settings.VideoPath, name + ".mp4");
        try
        {
            try
            {
                Directory.CreateDirectory(settings.VideoPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(
                    exception,
                    "The folder {Folder} of the recap videos cannot be created",
                    settings.VideoPath);
                return new RecapVideoResult(RecapVideoStatus.Unavailable);
            }

            RecapVideoStatus? saved = await SaveAsync(content, input, settings.MaxVideoBytes, cancellationToken);
            if (saved is { } failure)
            {
                return new RecapVideoResult(failure);
            }

            FfmpegCommandResult result = await ffmpeg.RunAsync(
                RecapVideoArguments.Build(input, output, transcodingOptions.Value.X264Preset),
                settings.ConversionTimeout,
                cancellationToken);
            if (!result.Started)
            {
                TryDelete(output);
                return new RecapVideoResult(RecapVideoStatus.Unavailable);
            }

            if (!result.Succeeded || !File.Exists(output))
            {
                logger.LogWarning(
                    "A recap video could not be converted (exit code {ExitCode}): {Error}",
                    result.ExitCode,
                    result.ErrorOutput);
                TryDelete(output);
                return new RecapVideoResult(RecapVideoStatus.Unreadable);
            }

            return new RecapVideoResult(RecapVideoStatus.Converted, output);
        }
        catch
        {
            TryDelete(output);
            throw;
        }
        finally
        {
            TryDelete(input);
        }
    }

    /// <summary>
    /// Writes the content into the file; returns why it was not kept, or <c>null</c>.
    /// </summary>
    private async Task<RecapVideoStatus?> SaveAsync(
        Stream content,
        string path,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        byte[] header = new byte[RecapVideoFormats.HeaderLength];
        long written = 0;
        try
        {
            await using FileStream file = new(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous);
            byte[] buffer = new byte[BufferSize];
            int read;
            while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (written + read > maxBytes)
                {
                    return RecapVideoStatus.TooLarge;
                }

                if (written < header.Length)
                {
                    int count = (int)Math.Min(read, header.Length - written);
                    Array.Copy(buffer, 0, header, written, count);
                }

                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                written += read;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "A recap video cannot be written to {Path}", path);
            return RecapVideoStatus.Unavailable;
        }

        return RecapVideoFormats.IsSupported(header.AsSpan(0, (int)Math.Min(written, header.Length)))
            ? null
            : RecapVideoStatus.UnsupportedFormat;
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "The temporary file {Path} could not be deleted", path);
        }
    }
}
