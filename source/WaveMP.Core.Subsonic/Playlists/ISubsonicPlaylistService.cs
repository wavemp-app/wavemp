using WaveMP.Core.Subsonic.Authentication;
using WaveMP.Core.Subsonic.Requests;
using WaveMP.Core.Subsonic.Responses;

namespace WaveMP.Core.Subsonic.Playlists;

/// <summary>
/// The playlists of the user in the Subsonic API: read, create, change and delete. Playlists of other users are
/// neither listed nor found; comments and public playlists do not exist in WaveMP and are ignored.
/// </summary>
public interface ISubsonicPlaylistService
{
    Task<SubsonicResponse> GetPlaylistsAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default);

    Task<SubsonicResponse> GetPlaylistAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a playlist with <c>name</c> and the songs <c>songId</c>, or with <c>playlistId</c> replaces the songs
    /// of an existing one. Returns the playlist with its songs.
    /// </summary>
    Task<SubsonicResponse> CreatePlaylistAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a playlist (<c>name</c>), removes songs by their index before the change (<c>songIndexToRemove</c>) and
    /// adds songs at the end (<c>songIdToAdd</c>).
    /// </summary>
    Task<SubsonicResponse> UpdatePlaylistAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default);

    Task<SubsonicResponse> DeletePlaylistAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default);
}
