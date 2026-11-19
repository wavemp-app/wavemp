using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using WaveMP.Service.Queries;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Changes the album tags of the songs of an album. Missing members stay unchanged; null or an empty text removes the
/// value from the tags.
/// </summary>
/// <remarks>
/// Whether a member is present is recorded when it is set: the JSON serializer only sets the members of the request,
/// also when they are null.
/// </remarks>
public sealed class UpdateAlbumTagsDto
{
    /// <summary>
    /// New album.
    /// </summary>
    [StringLength(LibraryLimits.NameMaxLength)]
    public string? Album
    {
        get;
        init
        {
            field = value;
            HasAlbum = true;
        }
    }

    /// <summary>
    /// New album artist; replaces all album artists of the files.
    /// </summary>
    [StringLength(LibraryLimits.NameMaxLength)]
    public string? AlbumArtist
    {
        get;
        init
        {
            field = value;
            HasAlbumArtist = true;
        }
    }

    /// <summary>
    /// New year.
    /// </summary>
    [Range(LibraryLimits.MinYear, LibraryLimits.MaxYear)]
    public int? Year
    {
        get;
        init
        {
            field = value;
            HasYear = true;
        }
    }

    /// <summary>
    /// New genre; replaces all genres of the files.
    /// </summary>
    [StringLength(LibraryLimits.GenreMaxLength)]
    public string? Genre
    {
        get;
        init
        {
            field = value;
            HasGenre = true;
        }
    }

    [JsonIgnore]
    public bool HasAlbum { get; private set; }

    [JsonIgnore]
    public bool HasAlbumArtist { get; private set; }

    [JsonIgnore]
    public bool HasYear { get; private set; }

    [JsonIgnore]
    public bool HasGenre { get; private set; }
}
