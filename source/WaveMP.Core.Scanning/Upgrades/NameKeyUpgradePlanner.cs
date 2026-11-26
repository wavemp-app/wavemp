using WaveMP.Core.Metadata.Resolutions;
using WaveMP.Core.Scanning.Processors;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;
using WaveMP.Data.Texts;

namespace WaveMP.Core.Scanning.Upgrades;

/// <summary>
/// Works out the upgrade of an existing library to the normalized names of <see cref="NameKeys"/>, without touching
/// the database. Artists and genres get their normalized names, albums the keys and normalized titles a scan would
/// give them now (see <see cref="AlbumKeys"/>). Entries whose new names or keys are equal are joined: the artist and
/// the album seen first (then the smallest id) and the genre with the most tracks (then the smallest id) stay and get
/// the songs of the others; the names of the entries that stay do not change. Rules of smart playlists with "is" or
/// "is not" that name an artist, album or genre get the id of the entry with that normalized name; a rule whose name
/// matches no entry, or several albums of that title, keeps its text.
/// </summary>
public static class NameKeyUpgradePlanner
{
    public static NameKeyUpgradePlan Plan(NameKeyUpgradeInput input)
    {
        Dictionary<Guid, string> artistKeys = [];
        Dictionary<Guid, Guid> artistMerges = [];
        Dictionary<string, Guid> artistsByKey = new(StringComparer.Ordinal);
        foreach (IGrouping<string, NameKeyUpgradeArtist> group in input.Artists
            .GroupBy(artist => KeyOf(artist.Name, artist.Id), StringComparer.Ordinal))
        {
            NameKeyUpgradeArtist[] ordered = [.. group
                .OrderBy(artist => artist.FirstSeenAt)
                .ThenBy(artist => artist.Id)];
            artistKeys[ordered[0].Id] = group.Key;
            artistsByKey[group.Key] = ordered[0].Id;
            foreach (NameKeyUpgradeArtist joined in ordered.Skip(1))
            {
                artistMerges[joined.Id] = ordered[0].Id;
            }
        }

        var artistNames = input.Artists.ToDictionary(artist => artist.Id, artist => artist.Name);
        Dictionary<Guid, (string AlbumKey, string NormalizedTitle)> albumKeys = [];
        Dictionary<Guid, Guid> albumMerges = [];
        List<(Guid Id, string TitleKey)> albums = [];
        foreach (IGrouping<string, NameKeyUpgradeAlbum> group in input.Albums
            .Select(album => (Album: album, Key: AlbumKeyOf(album, artistNames)))
            .Where(album => album.Key is not null)
            .GroupBy(album => album.Key!, album => album.Album, StringComparer.Ordinal))
        {
            NameKeyUpgradeAlbum[] ordered = [.. group.OrderBy(album => album.FirstSeenAt).ThenBy(album => album.Id)];
            string titleKey = NameKeys.Of(ordered[0].Title) ?? string.Empty;
            albumKeys[ordered[0].Id] = (group.Key, titleKey);
            albums.Add((ordered[0].Id, titleKey));
            foreach (NameKeyUpgradeAlbum joined in ordered.Skip(1))
            {
                albumMerges[joined.Id] = ordered[0].Id;
            }
        }

        Dictionary<Guid, string> genreKeys = [];
        Dictionary<Guid, Guid> genreMerges = [];
        Dictionary<string, Guid> genresByKey = new(StringComparer.Ordinal);
        foreach (IGrouping<string, NameKeyUpgradeGenre> group in input.Genres
            .GroupBy(genre => KeyOf(genre.Name, genre.Id), StringComparer.Ordinal))
        {
            NameKeyUpgradeGenre[] ordered = [.. group
                .OrderByDescending(genre => genre.TrackCount)
                .ThenBy(genre => genre.Id)];
            genreKeys[ordered[0].Id] = group.Key;
            genresByKey[group.Key] = ordered[0].Id;
            foreach (NameKeyUpgradeGenre joined in ordered.Skip(1))
            {
                genreMerges[joined.Id] = ordered[0].Id;
            }
        }

        ILookup<string, Guid> albumsByTitle = albums.ToLookup(
            album => album.TitleKey,
            album => album.Id,
            StringComparer.Ordinal);
        Dictionary<Guid, IReadOnlyList<SmartPlaylistRule>> rules = [];
        foreach (NameKeyUpgradeSmartPlaylist playlist in input.SmartPlaylists)
        {
            bool changed = false;
            List<SmartPlaylistRule> linked = [];
            foreach (SmartPlaylistRule rule in playlist.Rules)
            {
                Guid? id = rule.CanReference() && rule.ReferenceId is null && NameKeys.Of(rule.Text) is { } key
                    ? rule.Field switch
                    {
                        SmartPlaylistField.Artist => artistsByKey.TryGetValue(key, out Guid artist) ? artist : null,
                        SmartPlaylistField.Album => albumsByTitle[key].ToArray() is [Guid album] ? album : null,
                        _ => genresByKey.TryGetValue(key, out Guid genre) ? genre : null,
                    }
                    : null;
                changed |= id is not null;
                linked.Add(id is null ? rule : rule with { ReferenceId = id });
            }

            if (changed)
            {
                rules[playlist.Id] = linked;
            }
        }

        return new NameKeyUpgradePlan(artistKeys, artistMerges, albumKeys, albumMerges, genreKeys, genreMerges, rules);
    }

    /// <summary>
    /// The normalized name; a name without one (only white space) keeps a placeholder of its own, so it joins nothing.
    /// </summary>
    private static string KeyOf(string name, Guid id)
    {
        return NameKeys.Of(name) ?? "#" + id.ToString("D");
    }

    /// <summary>
    /// The key a scan would give the album now: by its album artist, or by the folder of its tracks for an album
    /// without artist; <c>null</c> for an album without artist and tracks, which the upgrade removes.
    /// </summary>
    private static string? AlbumKeyOf(NameKeyUpgradeAlbum album, Dictionary<Guid, string> artistNames)
    {
        if (album.AlbumArtistId is { } artistId)
        {
            return artistNames.TryGetValue(artistId, out string? artist)
                ? AlbumKeys.Create(album.Title, artist)
                : album.AlbumKey;
        }

        return album is { SourceId: { } sourceId, DirectoryPath: { } directory }
            ? AlbumKeys.Create(album.Title, null, FolderProcessor.AlbumFolderOf(sourceId, directory))
            : null;
    }
}
