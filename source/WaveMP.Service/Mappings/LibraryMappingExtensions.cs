using WaveMP.Core.Library.Searches;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class LibraryMappingExtensions
{
    public static ArtistDto ToDto(this ArtistRow artist)
    {
        return new ArtistDto
        {
            Id = artist.Id,
            Name = artist.Name,
            AlbumCount = artist.AlbumCount,
            TrackCount = artist.TrackCount,
            FirstSeenAt = artist.FirstSeenAt,
            IsFavorite = artist.IsFavorite,
            Picture = artist.Picture.ToPictureDto(),
        };
    }

    public static AlbumDto ToDto(this AlbumRow album)
    {
        return new AlbumDto
        {
            Id = album.Id,
            Title = album.Title,
            Artist = ToReference(album.ArtistId, album.ArtistName),
            Artists = album.AllArtists.ToDtos(),
            IsCompilation = album.IsCompilation,
            Year = album.Year,
            Genre = album.Genre,
            TrackCount = album.TrackCount,
            DurationMilliseconds = (long)Math.Round(album.DurationMilliseconds),
            HasCover = album.HasCover,
            FirstSeenAt = album.FirstSeenAt,
            IsFavorite = album.IsFavorite,
        };
    }

    public static TrackDto ToDto(this TrackRow track)
    {
        return new TrackDto
        {
            Id = track.Id,
            Title = track.Title,
            Artist = ToReference(track.ArtistId, track.ArtistName),
            Artists = track.AllArtists.ToDtos(),
            Album = track.AlbumId is { } albumId && track.AlbumTitle is { } albumTitle
                ? new AlbumReferenceDto
                {
                    Id = albumId,
                    Title = albumTitle,
                    Artist = ToReference(track.AlbumArtistId, track.AlbumArtistName),
                    Artists = track.AllAlbumArtists.ToDtos(),
                    IsCompilation = track.AlbumIsCompilation,
                }
                : null,
            TrackNumber = track.TrackNumber,
            DiscNumber = track.DiscNumber,
            Year = track.Year,
            Genre = track.Genre,
            DurationMilliseconds = ToMilliseconds(track.Duration),
            HasAudio = track.HasAudio,
            HasVideo = track.HasVideo,
            HasCover = track.HasCover,
            FirstSeenAt = track.FirstSeenAt,
            IsFavorite = track.IsFavorite,
            Rating = track.Rating,
        };
    }

    public static MediaFileDto ToDto(this MediaFile file)
    {
        return new MediaFileDto
        {
            Id = file.Id,
            Kind = file.Kind.ToDto(),
            RelativePath = file.RelativePath,
            FileName = file.FileName,
            Extension = file.Extension,
            Size = file.Size,
            ModifiedAt = file.ModifiedAt,
            IsPrimary = file.IsPrimary,
            Format = file.Format,
            Codec = file.Codec,
            AudioCodec = file.AudioCodec,
            PixelFormat = file.PixelFormat,
            Bitrate = file.Bitrate,
            SampleRate = file.SampleRate,
            BitDepth = file.BitDepth,
            Channels = file.Channels,
            IsLossless = file.IsLossless,
            Width = file.Width,
            Height = file.Height,
            DurationMilliseconds = ToMilliseconds(file.Duration),
            HasEmbeddedPicture = file.HasEmbeddedPicture,
            IsWritable = file.IsWritable,
            Issue = file.Issue?.ToDto(),
            FirstSeenAt = file.FirstSeenAt,
            ScannedAt = file.ScannedAt,
        };
    }

    public static GenreDto ToDto(this GenreRow genre)
    {
        return new GenreDto
        {
            Id = genre.Id,
            Name = genre.Name,
            TrackCount = genre.TrackCount,
            AlbumCount = genre.AlbumCount,
        };
    }

    public static YearDto ToDto(this YearRow year)
    {
        return new YearDto { Year = year.Year, TrackCount = year.TrackCount, AlbumCount = year.AlbumCount };
    }

    public static SearchResultDto ToDto(this SearchResult result)
    {
        return new SearchResultDto
        {
            Artists = result.Artists.ToDto(artist => artist.ToDto()),
            Albums = result.Albums.ToDto(album => album.ToDto()),
            Tracks = result.Tracks.ToDto(track => track.ToDto()),
        };
    }

    /// <summary>
    /// Problems of a file use the codes of the scan issues, which the clients already translate.
    /// </summary>
    public static ScanIssueCodeDto ToDto(this MediaFileIssue issue)
    {
        return issue switch
        {
            MediaFileIssue.TagsUnreadable => ScanIssueCodeDto.FileTagsUnreadable,
            MediaFileIssue.ProbeFailed => ScanIssueCodeDto.FileProbeFailed,
            MediaFileIssue.Unreadable => ScanIssueCodeDto.FileUnreadable,
            _ => throw new ArgumentOutOfRangeException(nameof(issue), issue, "Unknown issue."),
        };
    }

    /// <summary>
    /// The artists of a song or album in their order.
    /// </summary>
    public static IReadOnlyList<ArtistCreditDto> ToDtos(this IReadOnlyList<ArtistCreditRow> artists)
    {
        return [.. artists.Select(artist => new ArtistCreditDto
        {
            Id = artist.Id,
            Name = artist.Name,
            Role = artist.Role.ToDto(),
        })];
    }

    public static ArtistRoleDto ToDto(this ArtistRole role)
    {
        return role switch
        {
            ArtistRole.Main => ArtistRoleDto.Main,
            ArtistRole.Featured => ArtistRoleDto.Featured,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role."),
        };
    }

    public static ArtistRole ToEntity(this ArtistRoleDto role)
    {
        return role switch
        {
            ArtistRoleDto.Main => ArtistRole.Main,
            ArtistRoleDto.Featured => ArtistRole.Featured,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role."),
        };
    }

    public static MediaFileKindDto ToDto(this MediaFileKind kind)
    {
        return kind switch
        {
            MediaFileKind.Audio => MediaFileKindDto.Audio,
            MediaFileKind.Video => MediaFileKindDto.Video,
            MediaFileKind.Image => MediaFileKindDto.Image,
            MediaFileKind.Lyrics => MediaFileKindDto.Lyrics,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown kind."),
        };
    }

    public static MediaFileKind ToEntity(this MediaFileKindDto kind)
    {
        return kind switch
        {
            MediaFileKindDto.Audio => MediaFileKind.Audio,
            MediaFileKindDto.Video => MediaFileKind.Video,
            MediaFileKindDto.Image => MediaFileKind.Image,
            MediaFileKindDto.Lyrics => MediaFileKind.Lyrics,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown kind."),
        };
    }

    public static ArtistSortField ToEntity(this ArtistSortDto sort)
    {
        return sort switch
        {
            ArtistSortDto.Name => ArtistSortField.Name,
            ArtistSortDto.FirstSeenAt => ArtistSortField.FirstSeenAt,
            ArtistSortDto.Relevance => ArtistSortField.Relevance,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Unknown sort key."),
        };
    }

    public static AlbumSortField ToEntity(this AlbumSortDto sort)
    {
        return sort switch
        {
            AlbumSortDto.Title => AlbumSortField.Title,
            AlbumSortDto.Artist => AlbumSortField.Artist,
            AlbumSortDto.Year => AlbumSortField.Year,
            AlbumSortDto.FirstSeenAt => AlbumSortField.FirstSeenAt,
            AlbumSortDto.Relevance => AlbumSortField.Relevance,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Unknown sort key."),
        };
    }

    public static TrackSortField ToEntity(this TrackSortDto sort)
    {
        return sort switch
        {
            TrackSortDto.AlbumArtist => TrackSortField.AlbumArtist,
            TrackSortDto.Album => TrackSortField.Album,
            TrackSortDto.Title => TrackSortField.Title,
            TrackSortDto.Artist => TrackSortField.Artist,
            TrackSortDto.Duration => TrackSortField.Duration,
            TrackSortDto.Year => TrackSortField.Year,
            TrackSortDto.Genre => TrackSortField.Genre,
            TrackSortDto.FirstSeenAt => TrackSortField.FirstSeenAt,
            TrackSortDto.Relevance => TrackSortField.Relevance,
            TrackSortDto.Rating => TrackSortField.Rating,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Unknown sort key."),
        };
    }

    public static GenreSortField ToEntity(this GenreSortDto sort)
    {
        return sort switch
        {
            GenreSortDto.Name => GenreSortField.Name,
            GenreSortDto.TrackCount => GenreSortField.TrackCount,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Unknown sort key."),
        };
    }

    public static MediaFileSortField ToEntity(this MediaFileSortDto sort)
    {
        return sort switch
        {
            MediaFileSortDto.Kind => MediaFileSortField.Kind,
            MediaFileSortDto.Name => MediaFileSortField.Name,
            MediaFileSortDto.Size => MediaFileSortField.Size,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Unknown sort key."),
        };
    }

    /// <summary>
    /// A reference only if both id and name are known.
    /// </summary>
    internal static ArtistReferenceDto? ToReference(Guid? id, string? name)
    {
        return id is { } artistId && name is not null ? new ArtistReferenceDto { Id = artistId, Name = name } : null;
    }

    /// <summary>
    /// The list of artists of an entry whose query loads the first artist only.
    /// </summary>
    internal static IReadOnlyList<ArtistCreditDto> ToCredits(Guid? id, string? name)
    {
        return id is { } artistId && name is not null
            ? [new ArtistCreditDto { Id = artistId, Name = name, Role = ArtistRoleDto.Main }]
            : [];
    }

    internal static long? ToMilliseconds(TimeSpan? duration)
    {
        return duration is { } value ? (long)Math.Round(value.TotalMilliseconds) : null;
    }
}
