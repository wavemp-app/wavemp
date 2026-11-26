using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

/// <summary>
/// The changes of the upgrade to normalized names (see <see cref="INameKeyUpgradeRepository"/>).
/// </summary>
/// <param name="ArtistKeys">The new normalized name of every artist that stays.</param>
/// <param name="ArtistMerges">Artists whose normalized name equals that of another artist, with the artist that gets
/// their songs.</param>
/// <param name="AlbumKeys">The new key and normalized title of every album that stays; albums left out keep theirs.
/// </param>
/// <param name="AlbumMerges">Albums whose new key equals that of another album, with the album that gets their songs.
/// </param>
/// <param name="GenreKeys">The new normalized name of every genre that stays.</param>
/// <param name="GenreMerges">Genres whose normalized name equals that of another genre, with the genre that gets their
/// tracks.</param>
/// <param name="SmartPlaylistRules">The new rules of the smart playlists whose rules change.</param>
public sealed record NameKeyUpgradePlan(
    IReadOnlyDictionary<Guid, string> ArtistKeys,
    IReadOnlyDictionary<Guid, Guid> ArtistMerges,
    IReadOnlyDictionary<Guid, (string AlbumKey, string NormalizedTitle)> AlbumKeys,
    IReadOnlyDictionary<Guid, Guid> AlbumMerges,
    IReadOnlyDictionary<Guid, string> GenreKeys,
    IReadOnlyDictionary<Guid, Guid> GenreMerges,
    IReadOnlyDictionary<Guid, IReadOnlyList<SmartPlaylistRule>> SmartPlaylistRules);
