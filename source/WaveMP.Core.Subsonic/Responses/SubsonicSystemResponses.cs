using WaveMP.Core.Subsonic.Authentication;

namespace WaveMP.Core.Subsonic.Responses;

/// <summary>
/// The answers of the system endpoints: <c>ping</c>, <c>getLicense</c>, <c>getOpenSubsonicExtensions</c> and
/// <c>tokenInfo</c>.
/// </summary>
public static class SubsonicSystemResponses
{
    /// <summary>
    /// The OpenSubsonic extensions WaveMP supports, with their versions.
    /// </summary>
    public static IReadOnlyList<(string Name, int[] Versions)> Extensions { get; } =
    [
        ("apiKeyAuthentication", [1]),
        ("formPost", [1]),
        ("transcodeOffset", [1]),
    ];

    public static SubsonicResponse Ping()
    {
        return SubsonicResponse.Ok();
    }

    /// <summary>
    /// WaveMP needs no license; it is always valid.
    /// </summary>
    public static SubsonicResponse License()
    {
        return SubsonicResponse.Ok(new SubsonicElement("license").Set("valid", true));
    }

    public static SubsonicResponse OpenSubsonicExtensions()
    {
        return SubsonicResponse.OkList(
            "openSubsonicExtensions",
            Extensions.Select(extension => new SubsonicElement("openSubsonicExtensions")
                .Set("name", extension.Name)
                .AddValues("versions", extension.Versions)));
    }

    /// <summary>
    /// The user an API key belongs to.
    /// </summary>
    public static SubsonicResponse TokenInfo(SubsonicUser user)
    {
        return SubsonicResponse.Ok(new SubsonicElement("tokenInfo").Set("username", user.Name));
    }
}
