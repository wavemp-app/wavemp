using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;
using WaveMP.Core.Users.Avatars;
using WaveMP.Service.Dtos;
using WaveMP.Service.Problems;

namespace WaveMP.Service.Filters;

/// <summary>
/// Limits the request body of an avatar upload before the form is read: a body whose Content-Length is larger than
/// <see cref="AvatarOptions.MaxUploadBytes"/> plus the room of the multipart framing is answered with 413 and
/// <c>avatar.too_large</c> at once, and the server stops reading longer bodies without Content-Length. The size of the
/// picture itself is checked by the action.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AvatarUploadLimitAttribute : Attribute, IResourceFilter
{
    /// <summary>
    /// Room for the boundaries, headers and the other fields of the form.
    /// </summary>
    public const long MultipartOverheadBytes = 64 * 1024;

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        HttpContext httpContext = context.HttpContext;
        long maxBytes = httpContext.RequestServices.GetRequiredService<IOptions<AvatarOptions>>().Value
            .MaxUploadBytes + MultipartOverheadBytes;
        if (httpContext.Request.ContentLength > maxBytes)
        {
            ProblemDetails problem = httpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>()
                .CreateProblemDetails(httpContext, StatusCodes.Status413PayloadTooLarge);
            problem.Extensions[ProblemCodes.ExtensionName] = ProblemCodeDto.AvatarTooLarge;
            context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status413PayloadTooLarge };
            return;
        }

        if (httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature)
        {
            feature.MaxRequestBodySize = maxBytes;
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
