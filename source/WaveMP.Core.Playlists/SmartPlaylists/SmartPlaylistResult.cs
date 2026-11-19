using System.Diagnostics.CodeAnalysis;

namespace WaveMP.Core.Playlists.SmartPlaylists;

/// <summary>
/// The smart playlist after a change, or why there is none.
/// </summary>
/// <param name="Status">Whether the change was made.</param>
/// <param name="Item">The smart playlist; set if the change was made.</param>
/// <param name="Errors">The rejected fields of <see cref="SmartPlaylistStatus.Invalid"/>.</param>
public sealed record SmartPlaylistResult(
    SmartPlaylistStatus Status,
    SmartPlaylistItem? Item,
    IReadOnlyList<SmartPlaylistError> Errors)
{
    [MemberNotNullWhen(true, nameof(Item))]
    public bool IsOk => Status == SmartPlaylistStatus.Ok && Item is not null;

    public static SmartPlaylistResult Of(SmartPlaylistStatus status)
    {
        return new SmartPlaylistResult(status, null, []);
    }

    public static SmartPlaylistResult Invalid(IReadOnlyList<SmartPlaylistError> errors)
    {
        return new SmartPlaylistResult(SmartPlaylistStatus.Invalid, null, errors);
    }
}
