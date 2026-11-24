using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Data.Repositories;

internal sealed class OrganizeRepository(WaveMpDbContext dbContext) : IOrganizeRepository
{
    public async Task<IReadOnlyList<TemplateSongRow>> GetTemplateSongsAsync(
        IReadOnlyList<Guid> trackIds,
        CancellationToken cancellationToken = default)
    {
        if (trackIds.Count == 0)
        {
            return [];
        }

        Guid[] ids = [.. trackIds.Distinct()];
        IReadOnlyList<TemplateSongRow> rows = await LoadAsync(
            dbContext.Tracks.AsNoTracking().Where(track => ids.Contains(track.Id)),
            cancellationToken);
        var byId = rows.ToDictionary(row => row.TrackId);
        return [.. ids.Where(byId.ContainsKey).Select(id => byId[id])];
    }

    public async Task<IReadOnlyList<TemplateSongRow>> GetSampleTemplateSongsAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Track> tracks = dbContext.Tracks.AsNoTracking()
            .OrderBy(track => track.AlbumId == null)
            .ThenBy(track => track.Album!.AlbumArtist!.NormalizedName)
            .ThenBy(track => track.Album!.NormalizedTitle)
            .ThenBy(track => track.DiscNumber)
            .ThenBy(track => track.TrackNumber)
            .ThenBy(track => track.Title)
            .ThenBy(track => track.Id)
            .Take(count);
        return await LoadAsync(tracks, cancellationToken);
    }

    private async Task<IReadOnlyList<TemplateSongRow>> LoadAsync(
        IQueryable<Track> tracks,
        CancellationToken cancellationToken)
    {
        var songs = await tracks
            .Select(track => new
            {
                track.Id,
                track.Title,
                ArtistName = track.Artist == null ? null : track.Artist.Name,
                AlbumTitle = track.Album == null ? null : track.Album.Title,
                AlbumArtistName = track.Album == null || track.Album.AlbumArtist == null
                    ? null
                    : track.Album.AlbumArtist.Name,
                IsCompilation = track.Album != null && track.Album.IsCompilation,
                Year = track.Year ?? (track.Album == null ? null : track.Album.Year),
                track.TrackNumber,
                track.DiscNumber,
                track.Genre,
                track.BaseName,
            })
            .ToListAsync(cancellationToken);
        Guid[] ids = [.. songs.Select(song => song.Id)];
        var files = (await dbContext.MediaFiles.AsNoTracking()
                .Where(file => file.TrackId != null && ids.Contains(file.TrackId.Value))
                .Select(file => new { TrackId = file.TrackId!.Value, file.Extension, file.Kind, file.IsPrimary })
                .ToListAsync(cancellationToken))
            .ToLookup(file => file.TrackId);

        return [.. songs.Select(song =>
        {
            var own = files[song.Id].ToList();
            string? primary = own
                .Where(file => file.IsPrimary && file.Kind is MediaFileKind.Audio or MediaFileKind.Video)
                .OrderBy(file => file.Kind)
                .Select(file => file.Extension)
                .FirstOrDefault();
            return new TemplateSongRow(
                song.Id,
                song.Title,
                song.ArtistName,
                song.AlbumTitle,
                song.AlbumArtistName,
                song.IsCompilation,
                song.Year,
                song.TrackNumber,
                song.DiscNumber,
                song.Genre,
                song.BaseName,
                primary,
                [.. own.Select(file => file.Extension).Distinct(StringComparer.Ordinal)]);
        })];
    }
}
