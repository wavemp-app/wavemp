namespace WaveMP.Data.Repositories;

/// <summary>
/// An album as the upgrade to normalized names sees it (see <see cref="INameKeyUpgradeRepository"/>): its title, its
/// album artist and the folder of one of its tracks, which identifies an album without artist; an album without tracks
/// has no folder.
/// </summary>
public sealed record NameKeyUpgradeAlbum(
    Guid Id,
    string Title,
    string AlbumKey,
    Guid? AlbumArtistId,
    DateTimeOffset FirstSeenAt,
    Guid? SourceId,
    string? DirectoryPath);
