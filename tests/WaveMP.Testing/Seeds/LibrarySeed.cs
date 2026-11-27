using Microsoft.EntityFrameworkCore;
using WaveMP.Data;
using WaveMP.Data.Entities;
using WaveMP.Data.Texts;

namespace WaveMP.Testing.Seeds;

/// <summary>
/// Builds library data (artists, albums, tracks, files and covers) for tests of the data access, with sensible values
/// for everything a test does not care about. Nothing is written until <see cref="SaveAsync"/>.
/// </summary>
public sealed class LibrarySeed
{
    /// <summary>
    /// Time of the entities that set none of their own.
    /// </summary>
    public static readonly DateTimeOffset DefaultTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly List<object> _entities = [];
    private readonly Dictionary<string, Genre> _genres = new(StringComparer.Ordinal);
    private int _counter;

    public Artist Artist(string name, DateTimeOffset? firstSeenAt = null)
    {
        Artist artist = new()
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            NormalizedName = NameKeys.Of(name)!,
            FirstSeenAt = firstSeenAt ?? DefaultTime,
        };
        _entities.Add(artist);
        return artist;
    }

    public Album Album(
        string title,
        Artist? albumArtist = null,
        bool isCompilation = false,
        int? year = null,
        DateTimeOffset? firstSeenAt = null,
        IReadOnlyList<Artist>? moreAlbumArtists = null)
    {
        var albumId = Guid.CreateVersion7();
        Artist[] albumArtists = albumArtist is null ? [] : [albumArtist, .. moreAlbumArtists ?? []];
        Album album = new()
        {
            Id = albumId,
            Title = title,
            NormalizedTitle = NameKeys.Of(title) ?? string.Empty,
            AlbumKey = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            AlbumArtistId = albumArtist?.Id,
            ArtistCredits = [.. albumArtists.Select((artist, position) => new AlbumArtistCredit
            {
                AlbumId = albumId,
                ArtistId = artist.Id,
                Position = position,
            })],
            IsCompilation = isCompilation,
            Year = year,
            FirstSeenAt = firstSeenAt ?? DefaultTime,
            UpdatedAt = firstSeenAt ?? DefaultTime,
        };
        _entities.Add(album);
        return album;
    }

    public Track Track(
        string title,
        Artist? artist = null,
        Album? album = null,
        int? number = null,
        int? disc = null,
        int? year = null,
        string? genre = null,
        TimeSpan? duration = null,
        bool hasAudio = true,
        bool hasVideo = false,
        DateTimeOffset? firstSeenAt = null,
        MediaSource? source = null,
        IReadOnlyList<Artist>? moreArtists = null,
        IReadOnlyList<Artist>? featuring = null)
    {
        string baseName = $"{++_counter:D4} {title}";
        var trackId = Guid.CreateVersion7();
        (Artist Artist, ArtistRole Role)[] credits = artist is null
            ? []
            :
            [
                (artist, ArtistRole.Main),
                .. (moreArtists ?? []).Select(other => (other, ArtistRole.Main)),
                .. (featuring ?? []).Select(other => (other, ArtistRole.Featured)),
            ];
        Track track = new()
        {
            Id = trackId,
            SourceId = source?.Id ?? MediaSource.DefaultId,
            DirectoryPath = album?.Title ?? string.Empty,
            BaseName = baseName,
            NormalizedBaseName = baseName.ToUpperInvariant(),
            Title = title,
            ArtistId = artist?.Id,
            ArtistCredits = [.. credits.Select((credit, position) => new TrackArtistCredit
            {
                TrackId = trackId,
                ArtistId = credit.Artist.Id,
                Position = position,
                Role = credit.Role,
            })],
            AlbumId = album?.Id,
            TrackNumber = number,
            DiscNumber = disc,
            Year = year,
            GenreId = GenreOf(genre)?.Id,
            Duration = duration,
            HasAudio = hasAudio,
            HasVideo = hasVideo,
            MetadataSource = TrackMetadataSource.Tags,
            FirstSeenAt = firstSeenAt ?? DefaultTime,
            UpdatedAt = firstSeenAt ?? DefaultTime,
        };
        _entities.Add(track);
        return track;
    }

    /// <summary>
    /// The genre of a name, one per normalized name (see <see cref="NameKeys"/>), spelled as first named.
    /// </summary>
    public Genre? GenreOf(string? name)
    {
        if (NameKeys.Of(name) is not { } key)
        {
            return null;
        }

        if (!_genres.TryGetValue(key, out Genre? genre))
        {
            genre = new Genre { Id = Guid.CreateVersion7(), Name = name!, NormalizedName = key };
            _genres[key] = genre;
            _entities.Add(genre);
        }

        return genre;
    }

    /// <summary>
    /// A media source besides the first one that the migrations create, with a unique name.
    /// </summary>
    public MediaSource Source(string? name = null, string? path = null, bool isActive = true)
    {
        string unique = name ?? "Source " + Guid.NewGuid().ToString("N")[..12];
        MediaSource source = new()
        {
            Id = Guid.CreateVersion7(),
            Name = unique,
            NormalizedName = unique.ToUpperInvariant(),
            Path = path ?? "/sources/" + Guid.NewGuid().ToString("N"),
            IsActive = isActive,
            CreatedAt = DefaultTime,
            UpdatedAt = DefaultTime,
        };
        _entities.Add(source);
        return source;
    }

    /// <summary>
    /// A file of a media source (the first one if <paramref name="source"/> is <c>null</c>); <paramref name="track"/>
    /// is <c>null</c> for pictures without a track, such as the cover of an album. The file lies in the source of its
    /// track.
    /// </summary>
    public MediaFile File(
        string relativePath,
        MediaFileKind kind,
        Track? track = null,
        bool isPrimary = true,
        long size = 1000,
        bool isLossless = false,
        bool hasEmbeddedPicture = false,
        MediaFileIssue? issue = null,
        MediaSource? source = null)
    {
        int slash = relativePath.LastIndexOf('/');
        string fileName = slash < 0 ? relativePath : relativePath[(slash + 1)..];
        int dot = fileName.LastIndexOf('.');
        string baseName = dot < 0 ? fileName : fileName[..dot];
        MediaFile file = new()
        {
            Id = Guid.CreateVersion7(),
            SourceId = source?.Id ?? track?.SourceId ?? MediaSource.DefaultId,
            RelativePath = relativePath,
            DirectoryPath = slash < 0 ? string.Empty : relativePath[..slash],
            FileName = fileName,
            BaseName = baseName,
            NormalizedBaseName = baseName.ToUpperInvariant(),
            Extension = dot < 0 ? string.Empty : fileName[(dot + 1)..].ToLowerInvariant(),
            Kind = kind,
            Size = size,
            ModifiedAt = DefaultTime,
            TrackId = track?.Id,
            IsPrimary = isPrimary,
            IsLossless = isLossless,
            HasEmbeddedPicture = hasEmbeddedPicture,
            IsWritable = kind == MediaFileKind.Audio,
            Issue = issue,
            MetadataVersion = 1,
            FirstSeenAt = DefaultTime,
            ScannedAt = DefaultTime,
        };
        _entities.Add(file);
        return file;
    }

    public Cover AlbumCover(Album album, MediaFile file, bool isPrimary = true, string? mimeType = "image/jpeg")
    {
        return Add(new Cover
        {
            Id = Guid.CreateVersion7(),
            AlbumId = album.Id,
            Source = CoverSource.File,
            MediaFileId = file.Id,
            MimeType = mimeType,
            IsPrimary = isPrimary,
        });
    }

    public Cover TrackCover(
        Track track,
        MediaFile file,
        CoverSource source = CoverSource.File,
        bool isPrimary = true,
        string? mimeType = "image/jpeg")
    {
        return Add(new Cover
        {
            Id = Guid.CreateVersion7(),
            TrackId = track.Id,
            Source = source,
            MediaFileId = file.Id,
            MimeType = mimeType,
            IsPrimary = isPrimary,
        });
    }

    /// <summary>
    /// A stored picture of an artist (uploaded or online); an online picture names Wikimedia Commons with a credit.
    /// </summary>
    public ArtistPicture ArtistPicture(Artist artist, ArtistPictureKind kind, DateTimeOffset? createdAt = null)
    {
        bool online = kind == ArtistPictureKind.Online;
        ArtistPicture picture = new()
        {
            Id = Guid.CreateVersion7(),
            ArtistId = artist.Id,
            Kind = kind,
            CreatedAt = createdAt ?? DefaultTime,
            Width = 1000,
            Height = 1000,
            Provider = online ? ArtistPictureProvider.Wikimedia : null,
            Author = online ? "Jane Doe" : null,
            License = online ? "CC BY-SA 4.0" : null,
            LicenseUrl = online ? "https://creativecommons.org/licenses/by-sa/4.0/" : null,
            SourceUrl = online ? "https://commons.wikimedia.org/wiki/File:Example.jpg" : null,
        };
        _entities.Add(picture);
        return picture;
    }

    /// <summary>
    /// A profile with a unique name; the tests share the users of a database, so names never repeat.
    /// </summary>
    public User User(string? name = null)
    {
        string unique = name ?? "Listener " + Guid.NewGuid().ToString("N")[..12];
        User user = new()
        {
            Id = Guid.CreateVersion7(),
            Name = unique,
            NormalizedName = unique.ToUpperInvariant(),
            Initials = "L",
            Color = "#336699",
            Role = UserRole.User,
            IsBuiltIn = false,
            CreatedAt = DefaultTime,
        };
        _entities.Add(user);
        return user;
    }

    public Favorite Favorite(User user, Track track, DateTimeOffset? createdAt = null)
    {
        return AddFavorite(user, FavoriteType.Track, createdAt, trackId: track.Id);
    }

    public Favorite Favorite(User user, Album album, DateTimeOffset? createdAt = null)
    {
        return AddFavorite(user, FavoriteType.Album, createdAt, albumId: album.Id);
    }

    public Favorite Favorite(User user, Artist artist, DateTimeOffset? createdAt = null)
    {
        return AddFavorite(user, FavoriteType.Artist, createdAt, artistId: artist.Id);
    }

    public TrackRating Rating(User user, Track track, int rating, DateTimeOffset? updatedAt = null)
    {
        TrackRating trackRating = new()
        {
            UserId = user.Id,
            TrackId = track.Id,
            Rating = rating,
            UpdatedAt = updatedAt ?? DefaultTime,
        };
        _entities.Add(trackRating);
        return trackRating;
    }

    /// <summary>
    /// A play of the track by the user in the play history.
    /// </summary>
    public PlayHistoryEntry Play(User user, Track track, DateTimeOffset playedAt)
    {
        PlayHistoryEntry entry = new()
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TrackId = track.Id,
            PlayedAt = playedAt,
        };
        _entities.Add(entry);
        return entry;
    }

    /// <summary>
    /// A playlist of the user with the tracks in the given order.
    /// </summary>
    public Playlist Playlist(User user, string name, params Track[] tracks)
    {
        Playlist playlist = new()
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            CreatedAt = DefaultTime,
            UpdatedAt = DefaultTime,
        };
        _entities.Add(playlist);
        _entities.AddRange(tracks.Select((track, index) => new PlaylistEntry
        {
            Id = Guid.CreateVersion7(),
            PlaylistId = playlist.Id,
            TrackId = track.Id,
            Position = index,
            AddedAt = DefaultTime,
        }));
        return playlist;
    }

    /// <summary>
    /// An alias of an artist that the search finds it by.
    /// </summary>
    public ArtistAlias Alias(
        Artist artist,
        string name,
        ArtistAliasSource source = ArtistAliasSource.Admin,
        bool isRemoved = false)
    {
        ArtistAlias alias = new()
        {
            Id = Guid.CreateVersion7(),
            ArtistId = artist.Id,
            Name = name,
            NormalizedName = NameKeys.Of(name)!,
            Source = source,
            IsRemoved = isRemoved,
            CreatedAt = DefaultTime,
        };
        _entities.Add(alias);
        return alias;
    }

    /// <summary>
    /// Adds everything built so far to the database. A genre whose normalized name is stored already, e.g. by an
    /// earlier seed, is not added again; the tracks get the stored one.
    /// </summary>
    public async Task SaveAsync(WaveMpDbContext dbContext)
    {
        string[] keys = [.. _entities.OfType<Genre>().Select(genre => genre.NormalizedName)];
        Dictionary<string, Guid> stored = keys.Length == 0
            ? []
            : await dbContext.Genres.AsNoTracking()
                .Where(genre => keys.Contains(genre.NormalizedName))
                .ToDictionaryAsync(genre => genre.NormalizedName, genre => genre.Id, StringComparer.Ordinal);
        foreach (Genre genre in _entities.OfType<Genre>().Where(genre => stored.ContainsKey(genre.NormalizedName)))
        {
            foreach (Track track in _entities.OfType<Track>().Where(track => track.GenreId == genre.Id))
            {
                track.GenreId = stored[genre.NormalizedName];
            }

            genre.Id = stored[genre.NormalizedName];
        }

        _entities.RemoveAll(entity => entity is Genre genre && stored.ContainsKey(genre.NormalizedName));
        dbContext.AddRange(_entities);
        await dbContext.SaveChangesAsync();
        _entities.Clear();
    }

    private Cover Add(Cover cover)
    {
        _entities.Add(cover);
        return cover;
    }

    private Favorite AddFavorite(
        User user,
        FavoriteType type,
        DateTimeOffset? createdAt,
        Guid? trackId = null,
        Guid? albumId = null,
        Guid? artistId = null)
    {
        Favorite favorite = new()
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Type = type,
            TrackId = trackId,
            AlbumId = albumId,
            ArtistId = artistId,
            CreatedAt = createdAt ?? DefaultTime,
        };
        _entities.Add(favorite);
        return favorite;
    }
}
