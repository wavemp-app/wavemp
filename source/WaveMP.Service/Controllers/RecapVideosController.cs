using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WaveMP.Core.Recaps.Options;
using WaveMP.Core.Recaps.Videos;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.OpenApiTransformers;

namespace WaveMP.Service.Controllers;

/// <summary>
/// Converts the videos the clients record of a recap into MP4, for browsers that cannot record MP4 themselves.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/me/recap-videos")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class RecapVideosController(IRecapVideoConverter converter, IOptions<RecapOptions> options)
    : ControllerBase
{
    private const string Mp4 = "video/mp4";
    private const string WebM = "video/webm";
    private const string Matroska = "video/x-matroska";
    private const string FileName = "wavemp-recap.mp4";
    private const int BufferSize = 81_920;

    /// <summary>
    /// Converts a recorded video (the raw bytes of a WebM, Matroska or MP4 file, 200 MiB at most by default) into MP4
    /// with H.264 and, if the recording has sound, AAC, and returns it. The server keeps nothing: the uploaded and the
    /// converted file lie in a temporary folder of the data folder only while the request runs, and left-over files
    /// are deleted automatically.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request and the conversion.</param>
    /// <response code="200">The MP4 video.</response>
    /// <response code="413">The video is too large (code <c>video.too_large</c>).</response>
    /// <response code="415">The content is no WebM, Matroska or MP4 video (code
    /// <c>video.unsupported_format</c>).</response>
    /// <response code="422">The video could not be converted, e.g. because it is damaged (code
    /// <c>video.unreadable</c>).</response>
    /// <response code="503">Videos cannot be converted, ffmpeg is missing or the data folder cannot be written (code
    /// <c>service.unavailable</c>).</response>
    [HttpPost(Name = "ConvertMyRecapVideo")]
    [BinaryRequestBody(WebM, Matroska, Mp4)]
    [ProducesResponseType<Stream>(StatusCodes.Status200OK, Mp4)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> ConvertMyRecapVideoAsync(CancellationToken cancellationToken)
    {
        long maxBytes = options.Value.MaxVideoBytes;
        if (Request.ContentLength > maxBytes)
        {
            return this.Problem(ProblemCodeDto.VideoTooLarge, StatusCodes.Status413PayloadTooLarge);
        }

        // The server limit of request bodies must not cut a video that the configured limit allows.
        if (HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature &&
            feature.MaxRequestBodySize < maxBytes + 1)
        {
            feature.MaxRequestBodySize = maxBytes + 1;
        }

        RecapVideoResult result = await converter.ConvertAsync(Request.Body, cancellationToken);
        if (result is { Status: RecapVideoStatus.Converted, Path: { } path })
        {
            FileStream video = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            return File(video, Mp4, FileName);
        }

        return result.Status switch
        {
            RecapVideoStatus.TooLarge => this.Problem(
                ProblemCodeDto.VideoTooLarge,
                StatusCodes.Status413PayloadTooLarge),
            RecapVideoStatus.UnsupportedFormat => this.Problem(
                ProblemCodeDto.VideoUnsupportedFormat,
                StatusCodes.Status415UnsupportedMediaType),
            RecapVideoStatus.Unreadable => this.Problem(
                ProblemCodeDto.VideoUnreadable,
                StatusCodes.Status422UnprocessableEntity),
            _ => this.Problem(ProblemCodeDto.ServiceUnavailable, StatusCodes.Status503ServiceUnavailable),
        };
    }
}
