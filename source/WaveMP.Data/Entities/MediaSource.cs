namespace WaveMP.Data.Entities;

/// <summary>
/// A folder of the container whose media files the library contains, e.g. one for music and one for videos. Every
/// track and every media file belongs to exactly one source; their paths are relative to the folder of their source.
/// The admin adds, changes, deactivates and removes sources. A deactivated source is neither scanned nor watched, its
/// tracks stay in the library; a removed source takes its tracks and files with it.
/// </summary>
public class MediaSource
{
    /// <summary>
    /// Id of the first source. The update to multiple sources creates it and assigns the existing library to it; its
    /// folder follows <c>MEDIA_PATH</c> until the admin sets a path of its own.
    /// </summary>
    public static readonly Guid DefaultId = new("019a0000-0000-7000-8000-000000000001");

    public Guid Id { get; set; }

    /// <summary>
    /// Display name, unique without regard to case (see <see cref="NormalizedName"/>).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Upper-case form of <see cref="Name"/>; carries the unique index.
    /// </summary>
    public required string NormalizedName { get; set; }

    /// <summary>
    /// Absolute path of the folder in the container, without a separator at the end; <c>null</c> for the first source
    /// as long as it follows <c>MEDIA_PATH</c>.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Whether the source is scanned and watched.
    /// </summary>
    public required bool IsActive { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    public required DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Version of the rules the albums of the tracks of this source were formed with. A scan forms them again from the
    /// stored files of every folder of a source with an older version (without reading the files), so a change of the
    /// rules also applies to folders that did not change.
    /// </summary>
    public int AlbumGroupingVersion { get; set; }
}
