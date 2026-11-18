using System.Diagnostics.CodeAnalysis;

namespace WaveMP.Core.Subsonic.Responses;

/// <summary>
/// The answer of an endpoint of the Subsonic API: success with an optional content element, or an error code.
/// </summary>
/// <param name="Content">The element below <c>subsonic-response</c>; <c>null</c> for an empty success or an error.
/// </param>
/// <param name="Error">The error; <c>null</c> on success.</param>
public sealed record SubsonicResponse(SubsonicElement? Content, SubsonicErrorCode? Error)
{
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsOk => Error is null;

    /// <summary>
    /// Items directly below <c>subsonic-response</c>; <c>null</c> for the other answers.
    /// </summary>
    public (string ItemName, IReadOnlyList<SubsonicElement> Items)? RootList { get; init; }

    public static SubsonicResponse Ok(SubsonicElement? content = null)
    {
        return new SubsonicResponse(content, null);
    }

    /// <summary>
    /// A success whose items stand directly below <c>subsonic-response</c> (JSON: an array there), like the answer of
    /// <c>getOpenSubsonicExtensions</c>.
    /// </summary>
    public static SubsonicResponse OkList(string itemName, IEnumerable<SubsonicElement> items)
    {
        return new SubsonicResponse(null, null) { RootList = (itemName, [.. items]) };
    }

    public static SubsonicResponse Failed(SubsonicErrorCode error)
    {
        return new SubsonicResponse(null, error);
    }
}
