using WaveMP.Core.Subsonic.Authentication;
using WaveMP.Core.Subsonic.Requests;
using WaveMP.Core.Subsonic.Responses;

namespace WaveMP.Core.Subsonic.Annotations;

/// <summary>
/// What a user does with the library in the Subsonic API: favorites (<c>star</c>, <c>unstar</c>), ratings
/// (<c>setRating</c>), plays (<c>scrobble</c>) and <c>getNowPlaying</c>. Favorites, ratings and plays are the same as
/// in the web app; plays are scrobbled to Last.fm and ListenBrainz like there.
/// </summary>
public interface ISubsonicAnnotationService
{
    /// <summary>
    /// Marks songs, albums and artists as favorites: <c>id</c> (a song, album or artist), <c>albumId</c> and
    /// <c>artistId</c>, each repeatable.
    /// </summary>
    Task<SubsonicResponse> StarAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default);

    Task<SubsonicResponse> UnstarAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rates a song with 1 to 5 stars; 0 removes the rating. WaveMP rates songs only.
    /// </summary>
    Task<SubsonicResponse> SetRatingAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records plays of songs (<c>submission=true</c>, the default) with their start in <c>time</c> (milliseconds
    /// since 1970), or reports the song that starts as "Now Playing" (<c>submission=false</c>).
    /// </summary>
    Task<SubsonicResponse> ScrobbleAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The songs the users are playing right now, the latest first.
    /// </summary>
    Task<SubsonicResponse> GetNowPlayingAsync(SubsonicUser user, CancellationToken cancellationToken = default);
}
