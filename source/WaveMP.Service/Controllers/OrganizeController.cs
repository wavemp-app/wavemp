using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Organizing.Previews;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// Organization of the media files by the layout template, for admins only.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/organize")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class OrganizeController(ITemplatePreviewService templatePreview) : ControllerBase
{
    /// <summary>
    /// Shows where a layout template would put songs of the library or example songs, without saving the template and
    /// without touching any file. An invalid template is answered with its problems (codes with their position).
    /// </summary>
    /// <param name="request">The template and the songs to show.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The preview: whether the template is valid, its problems and the paths of the
    /// songs.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>, errors by field).</response>
    [HttpPost("template-preview", Name = "PreviewLayoutTemplate")]
    [Consumes("application/json")]
    [ProducesResponseType<LayoutTemplatePreviewDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<LayoutTemplatePreviewDto>> PreviewLayoutTemplateAsync(
        PreviewLayoutTemplateDto request,
        CancellationToken cancellationToken)
    {
        TemplatePreviewResult result = await templatePreview.PreviewAsync(request.ToRequest(), cancellationToken);
        return Ok(result.ToDto());
    }
}
