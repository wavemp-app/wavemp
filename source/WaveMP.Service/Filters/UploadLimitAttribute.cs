using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using WaveMP.Service.Dtos;
using WaveMP.Service.Problems;

namespace WaveMP.Service.Filters;

/// <summary>
/// Limits the request body of a picture upload as multipart form before the form is read: a body whose
/// Content-Length is larger than the largest picture plus the room of the multipart framing is answered with 413 and
/// the code of the upload at once, and the server stops reading longer bodies without Content-Length. The size of the
/// picture itself is checked by the action.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public abstract class UploadLimitAttribute : Attribute, IResourceFilter
{
    /// <summary>
    /// Room for the boundaries, headers and the other fields of the form.
    /// </summary>
    public const long MultipartOverheadBytes = 64 * 1024;

    /// <summary>
    /// The code of the answer 413.
    /// </summary>
    protected abstract ProblemCodeDto TooLargeCode { get; }

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        HttpContext httpContext = context.HttpContext;
        long maxBytes = MaxUploadBytes(httpContext.RequestServices) + MultipartOverheadBytes;
        if (httpContext.Request.ContentLength > maxBytes)
        {
            ProblemDetails problem = httpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>()
                .CreateProblemDetails(httpContext, StatusCodes.Status413PayloadTooLarge);
            problem.Extensions[ProblemCodes.ExtensionName] = TooLargeCode;
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

    /// <summary>
    /// The largest picture in bytes, from the options of the upload.
    /// </summary>
    protected abstract long MaxUploadBytes(IServiceProvider services);
}
