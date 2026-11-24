namespace WaveMP.Service.Dtos;

/// <summary>
/// Settings of the organization of the media files.
/// </summary>
public sealed class OrganizingSettingsDto
{
    /// <summary>
    /// The layout template that names the folders and files of a song, e.g.
    /// <c>{AlbumArtist}/{Album}/{TrackNumber:00} {Title}</c> (the default) or <c>{Artist} - {Title}</c>. <c>/</c>
    /// separates folders; placeholders are <c>{Artist}</c>, <c>{AlbumArtist}</c>, <c>{Album}</c>, <c>{Year}</c>,
    /// <c>{TrackNumber}</c>, <c>{DiscNumber}</c>, <c>{Title}</c> and <c>{Genre}</c> (numbers take a format of 1 to 4
    /// zeros, e.g. <c>{TrackNumber:00}</c>); <c>[...]</c> is a section that vanishes when one of its placeholders has
    /// no value; doubled brackets and braces stand for the character itself. The file name must contain
    /// <c>{Title}</c>.
    /// </summary>
    public required string LayoutTemplate { get; init; }

    /// <summary>
    /// Whether the template lays the songs out in folders (it contains <c>/</c>). With a file-based template the scan
    /// removes a leading artist name followed by <c>" - "</c> from title tags without regard to case; with a
    /// folder-based one only if it equals the artist tag exactly.
    /// </summary>
    public required bool IsFolderBased { get; init; }
}
