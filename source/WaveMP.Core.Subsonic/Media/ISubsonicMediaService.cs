using WaveMP.Core.Subsonic.Authentication;
using WaveMP.Core.Subsonic.Requests;

namespace WaveMP.Core.Subsonic.Media;

/// <summary>
/// The binary endpoints of the Subsonic API: <c>stream</c>, <c>download</c> and <c>getCoverArt</c>.
/// </summary>
public interface ISubsonicMediaService
{
    /// <summary>
    /// The file whose sound the song <c>id</c> plays and whether it is transcoded (see
    /// <see cref="SubsonicStreamPlanner"/>).
    /// </summary>
    Task<SubsonicStreamResult> PrepareStreamAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The original file of the song <c>id</c>, while the library settings allow downloads (otherwise error 50).
    /// </summary>
    Task<SubsonicStreamResult> PrepareDownloadAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The cover <c>id</c> (see <see cref="SubsonicIds"/>) in the <c>size</c>; without a size, as stored.
    /// </summary>
    Task<SubsonicCoverResult> GetCoverAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default);
}
