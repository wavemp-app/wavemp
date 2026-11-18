using Microsoft.AspNetCore.Cors.Infrastructure;
using WaveMP.Core.Streaming.Signatures;

namespace WaveMP.Service.Signatures;

/// <summary>
/// Marks an endpoint that delivers media and accepts signed media URLs (see
/// <see cref="MediaSignatureAuthenticationHandler"/>) besides the session token. A signature is only valid for the
/// kind of media named here and the track in the route value <c>id</c>. Use it together with
/// <c>[Authorize(Policy = AuthorizationPolicies.Media)]</c>. The endpoint also answers cross-origin requests with the
/// CORS policy <see cref="MediaSignatureDefaults.CorsPolicy"/>, because casting devices load HLS streams and covers
/// from the origin of their receiver.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SignedMediaAttribute(MediaResource resource) : Attribute, IEnableCorsAttribute
{
    public MediaResource Resource { get; } = resource;

    /// <summary>
    /// Always <see cref="MediaSignatureDefaults.CorsPolicy"/>.
    /// </summary>
    public string? PolicyName
    {
        get => MediaSignatureDefaults.CorsPolicy;
        set => throw new NotSupportedException("The CORS policy of signed media endpoints is fixed.");
    }
}
