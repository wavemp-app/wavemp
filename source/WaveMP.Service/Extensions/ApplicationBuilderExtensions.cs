using Microsoft.Net.Http.Headers;
using WaveMP.Service.Policies;

namespace WaveMP.Service.Extensions;

public static class ApplicationBuilderExtensions
{
    private const string ApiPathPrefix = "/api";

    /// <summary>
    /// Sets the Content Security Policy <see cref="ContentSecurityPolicies.WebApp"/> on every response outside the API
    /// (the web app, its files and the OpenAPI document) that has no policy of its own. Responses of the API are no
    /// documents a browser renders; endpoints that return such files set their own policy.
    /// </summary>
    public static IApplicationBuilder UseWaveMpContentSecurityPolicy(this IApplicationBuilder app)
    {
        return app.Use((context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments(ApiPathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                HttpResponse response = context.Response;
                response.OnStarting(() =>
                {
                    if (!response.Headers.ContainsKey(HeaderNames.ContentSecurityPolicy))
                    {
                        response.Headers.ContentSecurityPolicy = ContentSecurityPolicies.WebApp;
                    }

                    return Task.CompletedTask;
                });
            }

            return next(context);
        });
    }
}
