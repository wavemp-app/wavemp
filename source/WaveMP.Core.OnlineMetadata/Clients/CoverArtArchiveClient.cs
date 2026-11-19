using System.Globalization;
using Microsoft.Extensions.Options;
using WaveMP.Core.OnlineMetadata.Options;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;

namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// Calls <c>GET release/{id}/front-{size}</c> of the Cover Art Archive, which redirects to the picture. The Cover Art
/// Archive has no rate limit of its own, so its requests do not wait in the queue of MusicBrainz.
/// </summary>
internal sealed class CoverArtArchiveClient(
    OnlineRequestSender sender,
    ISettingsSnapshot<OnlineMetadataSettings> settings,
    IOptions<OnlineMetadataOptions> options) : ICoverArtArchiveClient
{
    public Task<OnlineResponse<byte[]>> GetFrontCoverAsync(
        Guid releaseId,
        CoverArtSize size,
        CancellationToken cancellationToken = default)
    {
        Uri url = FrontUrl(settings.Current.CoverArtArchiveUrl, releaseId, size);
        return sender.GetAsync(url, queued: false, options.Value.MaxCoverBytes, cancellationToken);
    }

    /// <summary>
    /// The address of the front cover: <c>{base}/release/{id}/front-{size}</c>.
    /// </summary>
    internal static Uri FrontUrl(string baseUrl, Guid releaseId, CoverArtSize size)
    {
        return new Uri(string.Create(
            CultureInfo.InvariantCulture,
            $"{baseUrl.TrimEnd('/')}/release/{releaseId:D}/front-{(int)size}"));
    }
}
