namespace WaveMP.Data.Repositories;

/// <summary>
/// Everything the upgrade to normalized names works out its changes from (see <see cref="INameKeyUpgradeRepository"/>).
/// </summary>
public sealed record NameKeyUpgradeInput(
    IReadOnlyList<NameKeyUpgradeArtist> Artists,
    IReadOnlyList<NameKeyUpgradeAlbum> Albums,
    IReadOnlyList<NameKeyUpgradeGenre> Genres,
    IReadOnlyList<NameKeyUpgradeSmartPlaylist> SmartPlaylists);
