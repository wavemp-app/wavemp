using WaveMP.Core.Subsonic.Authentication;
using WaveMP.Core.Subsonic.Requests;
using WaveMP.Core.Subsonic.Responses;

namespace WaveMP.Core.Subsonic.Browsing;

/// <summary>
/// The endpoints of the Subsonic API that browse and search the library. The library has one music folder (the media
/// folder); <c>musicFolderId</c> is accepted and needs no filter.
/// </summary>
public interface ISubsonicBrowsingService
{
    SubsonicResponse GetMusicFolders();

    Task<SubsonicResponse> GetIndexesAsync(SubsonicUser user, CancellationToken cancellationToken = default);

    Task<SubsonicResponse> GetArtistsAsync(SubsonicUser user, CancellationToken cancellationToken = default);

    Task<SubsonicResponse> GetArtistAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default);

    Task<SubsonicResponse> GetAlbumAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default);

    Task<SubsonicResponse> GetSongAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The folders of the library follow the tags: an artist holds its albums (and its songs without album), an album
    /// its songs.
    /// </summary>
    Task<SubsonicResponse> GetMusicDirectoryAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default);

    Task<SubsonicResponse> GetAlbumList2Async(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default);

    Task<SubsonicResponse> GetRandomSongsAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches artists, albums and songs, each with its own page. An empty query (also <c>""</c>) lists everything,
    /// which apps use to copy the whole library.
    /// </summary>
    Task<SubsonicResponse> Search3Async(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default);

    Task<SubsonicResponse> GetStarred2Async(SubsonicUser user, CancellationToken cancellationToken = default);
}
