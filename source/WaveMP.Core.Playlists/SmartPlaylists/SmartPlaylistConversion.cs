using System.Diagnostics.CodeAnalysis;
using WaveMP.Core.Playlists.Playlists;

namespace WaveMP.Core.Playlists.SmartPlaylists;

/// <summary>
/// The playlist a smart playlist was converted into, or why it was not converted.
/// </summary>
/// <param name="Status">Whether the smart playlist was converted.</param>
/// <param name="Playlist">The new playlist; set if it was converted.</param>
/// <param name="Max">The largest number of entries of a playlist for <see cref="SmartPlaylistStatus.Full"/>.</param>
public sealed record SmartPlaylistConversion(SmartPlaylistStatus Status, PlaylistItem? Playlist, int? Max = null)
{
    [MemberNotNullWhen(true, nameof(Playlist))]
    public bool IsOk => Status == SmartPlaylistStatus.Ok && Playlist is not null;

    public static SmartPlaylistConversion Of(SmartPlaylistStatus status, int? max = null)
    {
        return new SmartPlaylistConversion(status, null, max);
    }
}
