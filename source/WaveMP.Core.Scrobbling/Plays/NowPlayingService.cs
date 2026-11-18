using Microsoft.Extensions.Logging;
using WaveMP.Core.Scrobbling.Clients;
using WaveMP.Core.Scrobbling.Credentials;
using WaveMP.Core.Scrobbling.Rules;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scrobbling.Plays;

internal sealed class NowPlayingService(
    ITrackRepository tracks,
    IScrobbleRepository repository,
    IScrobbleSender sender,
    IScrobbleCredentialProtector protector,
    ILogger<NowPlayingService> logger) : INowPlayingService
{
    public async Task<NowPlayingStatus> ReportAsync(
        Guid userId,
        Guid trackId,
        CancellationToken cancellationToken = default)
    {
        TrackRow? track = await tracks.FindAsync(trackId, userId, cancellationToken);
        if (track is null)
        {
            return NowPlayingStatus.TrackNotFound;
        }

        if (!ScrobbleRules.IsEligible(track.Duration) || ScrobbleTracks.From(track) is not { } song)
        {
            return NowPlayingStatus.NotReported;
        }

        IReadOnlyList<ScrobbleConnection> connections =
            await repository.ListConnectionsAsync(userId, cancellationToken);
        bool reported = false;
        foreach (ScrobbleConnection connection in connections
            .Where(connection => connection.Status == ScrobbleConnectionStatus.Active))
        {
            reported |= await ReportAsync(connection, song, cancellationToken);
        }

        return reported ? NowPlayingStatus.Reported : NowPlayingStatus.NotReported;
    }

    private async Task<bool> ReportAsync(
        ScrobbleConnection connection,
        ScrobbleTrack song,
        CancellationToken cancellationToken)
    {
        if (protector.Unprotect(connection.ProtectedCredential) is not { } credential)
        {
            await repository.SetConnectionStatusAsync(
                connection.Id,
                ScrobbleConnectionStatus.AuthorizationFailed,
                cancellationToken);
            return false;
        }

        ScrobbleSendStatus status =
            await sender.SendNowPlayingAsync(connection.Service, credential, song, cancellationToken);
        if (status == ScrobbleSendStatus.Unauthorized)
        {
            logger.LogWarning(
                "{Service} rejected the credentials of the connection {ConnectionId}",
                connection.Service,
                connection.Id);
            await repository.SetConnectionStatusAsync(
                connection.Id,
                ScrobbleConnectionStatus.AuthorizationFailed,
                cancellationToken);
        }

        return status == ScrobbleSendStatus.Accepted;
    }
}
