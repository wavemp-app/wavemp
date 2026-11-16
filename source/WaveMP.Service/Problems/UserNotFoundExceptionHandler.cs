using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Data.Repositories;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Problems;

/// <summary>
/// Answers a write for a user that was deleted while the request ran (<see cref="UserNotFoundException"/>), e.g. a
/// play reported by a profile that is deleted at the same time, with 404 and the code <c>user.not_found</c> instead
/// of 500. Requests that start after the deletion are already rejected by the authentication (its session is gone),
/// so this only covers the short time between the authentication and the write. The exception is expected and not
/// logged as an error.
/// </summary>
public sealed class UserNotFoundExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<UserNotFoundExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not UserNotFoundException notFound)
        {
            return false;
        }

        logger.LogDebug(
            "The user {UserId} was deleted while the request ran; answered with user.not_found",
            notFound.UserId);
        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Extensions = { [ProblemCodes.ExtensionName] = ProblemCodeDto.UserNotFound },
            },
        });
    }
}
