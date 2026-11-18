namespace WaveMP.Service.Areas.Subsonic;

/// <summary>
/// Names of the Subsonic API in the web application.
/// </summary>
public static class SubsonicDefaults
{
    /// <summary>
    /// The area of the controller of the Subsonic API.
    /// </summary>
    public const string Area = "Subsonic";

    /// <summary>
    /// The path of the Subsonic API; apps add it to the address of the server themselves.
    /// </summary>
    public const string RoutePrefix = "rest";

    /// <summary>
    /// The CORS policy of the Subsonic API: web-based Subsonic apps call it from their own origin. The apps sign in
    /// with parameters, not with cookies, so every origin may call it.
    /// </summary>
    public const string CorsPolicy = "Subsonic";

    /// <summary>
    /// The suffix older apps add to the names of the endpoints (<c>ping.view</c>).
    /// </summary>
    public const string ViewSuffix = ".view";
}
