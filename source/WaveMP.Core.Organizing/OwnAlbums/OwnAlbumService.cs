using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Organizing.Options;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Organizing.OwnAlbums;

internal sealed class OwnAlbumService(
    IOwnAlbumRepository albums,
    ITrackRepository tracks,
    IOwnAlbumCoverStore covers,
    IOptions<OrganizeOptions> options,
    TimeProvider timeProvider,
    ILogger<OwnAlbumService> logger) : IOwnAlbumService
{
    public async Task<Page<OwnAlbumItem>> ListAsync(
        OwnAlbumListQuery query,
        CancellationToken cancellationToken = default)
    {
        Page<OwnAlbumRow> page = await albums.ListAsync(query, cancellationToken);
        return new Page<OwnAlbumItem>([.. page.Items.Select(ToItem)], page.Offset, page.Limit, page.Total);
    }

    public async Task<OwnAlbumItem?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        OwnAlbumRow? row = await albums.FindAsync(id, cancellationToken);
        return row is null ? null : ToItem(row);
    }

    public async Task<OwnAlbumResult> CreateAsync(
        OwnAlbumValues values,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = Now();
        string title = values.Title.Trim();
        OwnAlbum album = new()
        {
            Id = Guid.CreateVersion7(),
            Title = title,
            NormalizedTitle = title.ToUpperInvariant(),
            AlbumArtist = values.AlbumArtist.Trim(),
            Year = values.Year is > 0 ? values.Year : null,
            Genre = TrimToNull(values.Genre),
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await albums.AddAsync(album, cancellationToken);
        return await ResultOfAsync(album.Id, cancellationToken);
    }

    public async Task<OwnAlbumResult> UpdateAsync(
        Guid id,
        OwnAlbumChanges changes,
        CancellationToken cancellationToken = default)
    {
        OwnAlbumRow? current = await albums.FindAsync(id, cancellationToken);
        if (current is null)
        {
            return OwnAlbumResult.Failed(OwnAlbumResultStatus.NotFound);
        }

        string title = changes.Title?.Trim() ?? current.Title;
        string albumArtist = changes.AlbumArtist?.Trim() ?? current.AlbumArtist;
        int? year = changes.Year switch
        {
            null => current.Year,
            0 => null,
            { } value => value,
        };
        string? genre = changes.Genre is null ? current.Genre : TrimToNull(changes.Genre);
        if (title == current.Title && albumArtist == current.AlbumArtist && year == current.Year
            && genre == current.Genre)
        {
            return OwnAlbumResult.Ok(ToItem(current));
        }

        return await albums.UpdateAsync(id, title, albumArtist, year, genre, Now(), cancellationToken)
            ? await ResultOfAsync(id, cancellationToken)
            : OwnAlbumResult.Failed(OwnAlbumResultStatus.NotFound);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        OwnAlbumRow? current = await albums.FindAsync(id, cancellationToken);
        if (current is null || !await albums.DeleteAsync(id, cancellationToken))
        {
            return false;
        }

        if (current.CoverFileName is { } fileName)
        {
            covers.Delete(fileName);
        }

        return true;
    }

    public async Task<Page<OwnAlbumTrackItem>?> ListTracksAsync(
        Guid id,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (await albums.FindAsync(id, cancellationToken) is null)
        {
            return null;
        }

        IReadOnlyList<OwnAlbumMemberRow> members = await albums.ListMembersAsync(id, cancellationToken);
        OwnAlbumMemberRow[] page = [.. members.Skip(offset).Take(limit)];
        IReadOnlyList<TrackRow> rows = await tracks.ListByIdsAsync([.. page.Select(member => member.TrackId)], null, cancellationToken);
        var byId = rows.ToDictionary(row => row.Id);
        return new Page<OwnAlbumTrackItem>(
            [.. page.Where(member => byId.ContainsKey(member.TrackId))
                .Select(member => new OwnAlbumTrackItem(member, byId[member.TrackId]))],
            offset,
            limit,
            members.Count);
    }

    public async Task<OwnAlbumResult> ReplaceTracksAsync(
        Guid id,
        IReadOnlyList<Guid> trackIds,
        CancellationToken cancellationToken = default)
    {
        OwnAlbumRow? album = await albums.FindAsync(id, cancellationToken);
        if (album is null)
        {
            return OwnAlbumResult.Failed(OwnAlbumResultStatus.NotFound);
        }

        IReadOnlyList<OwnAlbumMemberRow> current = await albums.ListMembersAsync(id, cancellationToken);
        return await SaveAsync(album, current, [.. trackIds.Distinct()], cancellationToken);
    }

    public async Task<OwnAlbumResult> AddTracksAsync(
        Guid id,
        IReadOnlyList<Guid> trackIds,
        CancellationToken cancellationToken = default)
    {
        OwnAlbumRow? album = await albums.FindAsync(id, cancellationToken);
        if (album is null)
        {
            return OwnAlbumResult.Failed(OwnAlbumResultStatus.NotFound);
        }

        IReadOnlyList<OwnAlbumMemberRow> current = await albums.ListMembersAsync(id, cancellationToken);
        Guid[] members = [.. MembersOf(current).Select(member => member.TrackId)];
        return await SaveAsync(album, current, [.. members.Concat(trackIds).Distinct()], cancellationToken);
    }

    public async Task<OwnAlbumResult> RemoveTrackAsync(
        Guid id,
        Guid trackId,
        CancellationToken cancellationToken = default)
    {
        OwnAlbumRow? album = await albums.FindAsync(id, cancellationToken);
        if (album is null)
        {
            return OwnAlbumResult.Failed(OwnAlbumResultStatus.NotFound);
        }

        IReadOnlyList<OwnAlbumMemberRow> current = await albums.ListMembersAsync(id, cancellationToken);
        Guid[] members = [.. MembersOf(current).Select(member => member.TrackId)];
        if (!members.Contains(trackId))
        {
            return OwnAlbumResult.Failed(OwnAlbumResultStatus.TrackNotFound, [trackId]);
        }

        return await SaveAsync(album, current, [.. members.Where(member => member != trackId)], cancellationToken);
    }

    public async Task<OwnAlbumResult> SetCoverAsync(
        Guid id,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        OwnAlbumRow? album = await albums.FindAsync(id, cancellationToken);
        if (album is null)
        {
            return OwnAlbumResult.Failed(OwnAlbumResultStatus.NotFound);
        }

        OwnAlbumCoverSave saved = await covers.SaveAsync(id, content, cancellationToken);
        switch (saved.Status)
        {
            case OwnAlbumCoverSaveStatus.UnsupportedFormat:
                return OwnAlbumResult.Failed(OwnAlbumResultStatus.CoverUnsupportedFormat);
            case OwnAlbumCoverSaveStatus.Unavailable:
                return OwnAlbumResult.Failed(OwnAlbumResultStatus.CoverUnavailable);
        }

        DateTimeOffset now = Now();
        if (!await albums.SetCoverAsync(id, saved.FileName, saved.ContentType, now, now, cancellationToken))
        {
            covers.Delete(saved.FileName!);
            return OwnAlbumResult.Failed(OwnAlbumResultStatus.NotFound);
        }

        return await ResultOfAsync(id, cancellationToken);
    }

    public async Task<OwnAlbumResult> RemoveCoverAsync(Guid id, CancellationToken cancellationToken = default)
    {
        OwnAlbumRow? album = await albums.FindAsync(id, cancellationToken);
        if (album is null)
        {
            return OwnAlbumResult.Failed(OwnAlbumResultStatus.NotFound);
        }

        if (album.CoverFileName is not { } fileName)
        {
            return OwnAlbumResult.Ok(ToItem(album));
        }

        if (!await albums.SetCoverAsync(id, null, null, null, Now(), cancellationToken))
        {
            return OwnAlbumResult.Failed(OwnAlbumResultStatus.NotFound);
        }

        covers.Delete(fileName);
        return await ResultOfAsync(id, cancellationToken);
    }

    public async Task<(bool AlbumExists, OwnAlbumCover? Cover)> OpenCoverAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        OwnAlbumRow? album = await albums.FindAsync(id, cancellationToken);
        if (album is null)
        {
            return (false, null);
        }

        if (album is not { CoverFileName: { } fileName, CoverContentType: { } contentType, CoverUpdatedAt: { } at }
            || covers.FindPath(fileName) is not { } path)
        {
            return (true, null);
        }

        try
        {
            FileStream content = new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 1, true);
            string etag = at.UtcTicks.ToString("x", CultureInfo.InvariantCulture);
            return (true, new OwnAlbumCover(content, contentType, etag, at));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "The cover of the own album {AlbumId} cannot be read", id);
            return (true, null);
        }
    }

    public async Task RemoveOrphanCoversAsync(CancellationToken cancellationToken = default)
    {
        covers.RemoveOthers(await albums.ListCoverFileNamesAsync(cancellationToken));
    }

    /// <summary>
    /// Saves <paramref name="trackIds"/> as the members in this order. New members must exist, have audio and belong
    /// to no other own album. Members left out are removed if the album was applied, otherwise forgotten; songs that
    /// were removed before stay removed unless they come back.
    /// </summary>
    private async Task<OwnAlbumResult> SaveAsync(
        OwnAlbumRow album,
        IReadOnlyList<OwnAlbumMemberRow> current,
        IReadOnlyList<Guid> trackIds,
        CancellationToken cancellationToken)
    {
        if (trackIds.Count > options.Value.MaxAlbumTracks)
        {
            return OwnAlbumResult.Failed(OwnAlbumResultStatus.TooManyTracks);
        }

        var existing = current.ToDictionary(member => member.TrackId);
        Guid[] added = [.. trackIds.Where(trackId =>
            !existing.TryGetValue(trackId, out OwnAlbumMemberRow? member) || member.State != OwnAlbumTrackState.Member)];
        if (added.Length > 0)
        {
            IReadOnlyList<OwnAlbumCandidateRow> candidates = await albums.ListCandidatesAsync(added, cancellationToken);
            var found = candidates.ToDictionary(candidate => candidate.TrackId);
            Guid[] missing = [.. added.Where(trackId => !found.ContainsKey(trackId))];
            if (missing.Length > 0)
            {
                return OwnAlbumResult.Failed(OwnAlbumResultStatus.TrackNotFound, missing);
            }

            Guid[] videoOnly = [.. added.Where(trackId => !found[trackId].HasAudio)];
            if (videoOnly.Length > 0)
            {
                return OwnAlbumResult.Failed(OwnAlbumResultStatus.TrackNoAudio, videoOnly);
            }

            Guid[] taken = [.. added.Where(trackId => found[trackId].MemberOfOwnAlbumId is { } other && other != album.Id)];
            if (taken.Length > 0)
            {
                return OwnAlbumResult.Failed(OwnAlbumResultStatus.TrackInOtherAlbum, taken);
            }
        }

        DateTimeOffset now = Now();
        List<OwnAlbumMemberRow> rows = [.. trackIds.Select((trackId, index) => existing.TryGetValue(trackId, out OwnAlbumMemberRow? member)
            ? member with { Position = index, State = OwnAlbumTrackState.Member }
            : new OwnAlbumMemberRow(trackId, index, OwnAlbumTrackState.Member, null, now))];
        HashSet<Guid> kept = [.. trackIds];
        foreach (OwnAlbumMemberRow member in current.Where(member => !kept.Contains(member.TrackId)))
        {
            if (member.State == OwnAlbumTrackState.Removed || album.AppliedAt is not null)
            {
                rows.Add(member with { State = OwnAlbumTrackState.Removed });
            }
        }

        bool unchanged = rows.Count == current.Count && rows.All(row =>
            existing.TryGetValue(row.TrackId, out OwnAlbumMemberRow? member) && member == row);
        if (unchanged)
        {
            return OwnAlbumResult.Ok(ToItem(album));
        }

        OwnAlbumSaveStatus status = await albums.SaveMembersAsync(album.Id, rows, now, cancellationToken);
        return status switch
        {
            OwnAlbumSaveStatus.Saved => await ResultOfAsync(album.Id, cancellationToken),
            OwnAlbumSaveStatus.TrackTaken => OwnAlbumResult.Failed(OwnAlbumResultStatus.TrackInOtherAlbum, added),
            _ => OwnAlbumResult.Failed(OwnAlbumResultStatus.NotFound),
        };
    }

    private async Task<OwnAlbumResult> ResultOfAsync(Guid id, CancellationToken cancellationToken)
    {
        OwnAlbumRow? row = await albums.FindAsync(id, cancellationToken);
        return row is null
            ? OwnAlbumResult.Failed(OwnAlbumResultStatus.NotFound)
            : OwnAlbumResult.Ok(ToItem(row));
    }

    internal static OwnAlbumItem ToItem(OwnAlbumRow row)
    {
        OwnAlbumStatus status = row.AppliedAt switch
        {
            null => OwnAlbumStatus.Draft,
            { } applied when row.UpdatedAt > applied => OwnAlbumStatus.Changed,
            _ => OwnAlbumStatus.Applied,
        };
        return new OwnAlbumItem(row, status);
    }

    private static IEnumerable<OwnAlbumMemberRow> MembersOf(IEnumerable<OwnAlbumMemberRow> rows)
    {
        return rows.Where(member => member.State == OwnAlbumTrackState.Member).OrderBy(member => member.Position);
    }

    private static string? TrimToNull(string? text)
    {
        string? trimmed = text?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private DateTimeOffset Now()
    {
        DateTimeOffset value = timeProvider.GetUtcNow();
        return new DateTimeOffset(value.Ticks - (value.Ticks % TimeSpan.TicksPerMicrosecond), value.Offset);
    }
}
