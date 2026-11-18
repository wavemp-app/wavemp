namespace WaveMP.Service.Policies;

/// <summary>
/// The Content Security Policies of WaveMP. Themes are CSS and pictures from the server itself; the policy of the web
/// app makes sure that neither a theme nor anything else loads styles, pictures, fonts or scripts from elsewhere.
/// </summary>
public static class ContentSecurityPolicies
{
    /// <summary>
    /// Policy of the web app and every other response outside the API:
    /// <list type="bullet">
    /// <item>Scripts only from the server itself; the Cast sender SDK from www.gstatic.com. <c>'unsafe-eval'</c>,
    /// because the Milkdrop presets of the visualizations are compiled to JavaScript functions.</item>
    /// <item>Styles from the server itself and inline, because components set CSS variables in style attributes
    /// (the theme API).</item>
    /// <item>Pictures from the server, as <c>data:</c> (visualizations) and as <c>blob:</c> (covers and avatars the
    /// app loads with its session token); media from the server and as <c>blob:</c> (HLS through Media Source
    /// Extensions); workers from the server (the service worker of the PWA) and as <c>blob:</c> (HLS).</item>
    /// <item>Connections only to the server itself and to the Cast SDK.</item>
    /// </list>
    /// No <c>frame-ancestors</c>, so WaveMP can still be embedded in a dashboard, and no
    /// <c>upgrade-insecure-requests</c>, so installations without HTTPS in the home network keep working.
    /// </summary>
    public const string WebApp =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-eval' https://www.gstatic.com; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self'; " +
        "media-src 'self' blob:; " +
        "connect-src 'self' https://www.gstatic.com; " +
        "worker-src 'self' blob:; " +
        "frame-src 'self'; " +
        "manifest-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";

    /// <summary>
    /// Policy of a file of a theme opened on its own, e.g. an SVG file in a new tab: it may only style itself and
    /// runs nothing. Within the CSS of the web app, a picture never runs scripts anyway.
    /// </summary>
    public const string ThemeFile = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
}
