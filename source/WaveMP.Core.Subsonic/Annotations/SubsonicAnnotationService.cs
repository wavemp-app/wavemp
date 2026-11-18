using System.Globalization;
using WaveMP.Core.Collection.Favorites;
using WaveMP.Core.Collection.Ratings;
using WaveMP.Core.Scrobbling.Plays;
using WaveMP.Core.Streaming.Histories;
using WaveMP.Core.Subsonic.Authentication;
using WaveMP.Core.Subsonic.Elements;
using WaveMP.Core.Subsonic.NowPlaying;
using WaveMP.Core.Subsonic.Requests;
using WaveMP.Core.Subsonic.Responses;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Subsonic.Annotations;

internal sealed class SubsonicAnnotationService(
    IFavoriteService favorites,
    IRatingService ratings,
    IPlayHistoryService history,
    INowPlayingService nowPlaying,
    ISubsonicNowPlayingRegistry registry,
    ITrackRepository tracks,
    ISubsonicRepository repository,
    TimeProvider timeProvider) : ISubsonicAnnotationService
{
    /// <summary>
    /// How long a song of unknown duration counts as playing.
    /// </summary>
    private static readonly TimeSpan _unknownDuration = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How long a song still counts as playing after its end (pauses, slow reports).
    /// </summary>
    private static readonly TimeSpan _grace = TimeSpan.FromMinutes(1);

    public async Task<SubsonicResponse> StarAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseTargets(parameters, out List<Target> targets))
        {
            return Failed(targets);
        }

        bool allFound = true;
        foreach (Target target in targets)
        {
            if (target.Type is { } type)
            {
                allFound &= await favorites.AddAsync(user.Id, type, target.Id, cancellationToken)
                    == FavoriteStatus.Done;
                continue;
            }

            // A plain id is a song, an album or an artist (folder-based apps star albums and artists with it).
            bool found = false;
            foreach (FavoriteType candidate in new[] { FavoriteType.Track, FavoriteType.Album, FavoriteType.Artist })
            {
                if (await favorites.AddAsync(user.Id, candidate, target.Id, cancellationToken) == FavoriteStatus.Done)
                {
                    found = true;
                    break;
                }
            }

            allFound &= found;
        }

        return allFound ? SubsonicResponse.Ok() : SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
    }

    public async Task<SubsonicResponse> UnstarAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseTargets(parameters, out List<Target> targets))
        {
            return Failed(targets);
        }

        foreach (Target target in targets)
        {
            FavoriteType[] types = target.Type is { } type
                ? [type]
                : [FavoriteType.Track, FavoriteType.Album, FavoriteType.Artist];
            foreach (FavoriteType candidate in types)
            {
                await favorites.RemoveAsync(user.Id, candidate, target.Id, cancellationToken);
            }
        }

        return SubsonicResponse.Ok();
    }

    public async Task<SubsonicResponse> SetRatingAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.Has("id") || !parameters.Has("rating"))
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.MissingParameter);
        }

        if (parameters.GetInt("rating") is not { } rating || rating is < 0 or > 5)
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.Generic);
        }

        if (parameters.GetId("id") is not { } trackId)
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
        }

        return await ratings.SetAsync(user.Id, trackId, rating, cancellationToken) switch
        {
            RatingStatus.Done => SubsonicResponse.Ok(),
            RatingStatus.NotFound => SubsonicResponse.Failed(SubsonicErrorCode.NotFound),
            _ => SubsonicResponse.Failed(SubsonicErrorCode.Generic),
        };
    }

    public async Task<SubsonicResponse> ScrobbleAsync(
        SubsonicUser user,
        SubsonicParameters parameters,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> ids = parameters.GetAll("id");
        if (ids.Count == 0)
        {
            return SubsonicResponse.Failed(SubsonicErrorCode.MissingParameter);
        }

        IReadOnlyList<string> times = parameters.GetAll("time");
        bool submission = parameters.GetBool("submission", true);
        bool allFound = true;
        for (int index = 0; index < ids.Count; index++)
        {
            if (SubsonicIds.Parse(ids[index]) is not { } trackId)
            {
                allFound = false;
                continue;
            }

            if (!submission)
            {
                NowPlayingStatus status = await nowPlaying.ReportAsync(user.Id, trackId, cancellationToken);
                if (status == NowPlayingStatus.TrackNotFound)
                {
                    allFound = false;
                    continue;
                }

                registry.Report(user.Id, trackId, parameters.Get("c"));
                continue;
            }

            DateTimeOffset? playedAt = index < times.Count
                && long.TryParse(times[index], NumberStyles.None, CultureInfo.InvariantCulture, out long milliseconds)
                    ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
                    : null;
            PlayHistoryResult result = await history.RecordAsync(user.Id, trackId, playedAt, cancellationToken);

            // A play from too long ago or from the future cannot be recorded; it is dropped, so apps do not send it
            // again and again.
            allFound &= result.Status != PlayHistoryStatus.TrackNotFound;
        }

        return allFound ? SubsonicResponse.Ok() : SubsonicResponse.Failed(SubsonicErrorCode.NotFound);
    }

    public async Task<SubsonicResponse> GetNowPlayingAsync(
        SubsonicUser user,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SubsonicNowPlayingEntry> entries = registry.List();
        IReadOnlyList<TrackRow> rows = await tracks.ListByIdsAsync(
            [.. entries.Select(entry => entry.TrackId)],
            user.Id,
            cancellationToken);
        var byId = rows.ToDictionary(row => row.Id);
        IReadOnlyDictionary<Guid, SubsonicTrackDetails> details = await repository.ListTrackDetailsAsync(
            user.Id,
            byId.Keys,
            cancellationToken);
        IReadOnlyDictionary<Guid, string> names = await repository.ListUserNamesAsync(
            [.. entries.Select(entry => entry.UserId)],
            cancellationToken);

        DateTimeOffset now = timeProvider.GetUtcNow();
        List<SubsonicElement> items = [];
        int playerId = 0;
        foreach (SubsonicNowPlayingEntry entry in entries)
        {
            playerId++;
            if (!byId.TryGetValue(entry.TrackId, out TrackRow? track)
                || !names.TryGetValue(entry.UserId, out string? userName)
                || now > entry.StartedAt + (track.Duration ?? _unknownDuration) + _grace)
            {
                continue;
            }

            items.Add(SubsonicElements.Song(track, details.GetValueOrDefault(track.Id), "entry")
                .Set("username", userName)
                .Set("minutesAgo", (int)Math.Max(0, (now - entry.StartedAt).TotalMinutes))
                .Set("playerId", playerId)
                .Set("playerName", entry.PlayerName));
        }

        return SubsonicResponse.Ok(new SubsonicElement("nowPlaying").AddList("entry", items));
    }

    /// <summary>
    /// The targets of <c>star</c> and <c>unstar</c>; <c>false</c> if there is none (the list is then empty) or one is
    /// no id (the list then has items).
    /// </summary>
    private static bool TryParseTargets(SubsonicParameters parameters, out List<Target> targets)
    {
        targets = [];
        bool valid = true;
        foreach ((string name, FavoriteType? type) in new (string, FavoriteType?)[]
        {
            ("id", null),
            ("albumId", FavoriteType.Album),
            ("artistId", FavoriteType.Artist),
        })
        {
            foreach (string value in parameters.GetAll(name))
            {
                if (SubsonicIds.Parse(value) is { } id)
                {
                    targets.Add(new Target(id, type));
                }
                else
                {
                    valid = false;
                    targets.Add(new Target(Guid.Empty, type));
                }
            }
        }

        return valid && targets.Count > 0;
    }

    private static SubsonicResponse Failed(List<Target> targets)
    {
        return SubsonicResponse.Failed(
            targets.Count == 0 ? SubsonicErrorCode.MissingParameter : SubsonicErrorCode.NotFound);
    }

    /// <param name="Id">Id of the song, album or artist.</param>
    /// <param name="Type">What the id is; <c>null</c> for the parameter <c>id</c>, which can be each of them.</param>
    private sealed record Target(Guid Id, FavoriteType? Type);
}
