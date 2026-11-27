namespace WaveMP.Data.Repositories;

/// <summary>
/// The outcome of <see cref="IArtistAliasRepository.AddAsync"/>.
/// </summary>
public enum ArtistAliasAddStatus
{
    Added,

    /// <summary>
    /// The artist does not exist.
    /// </summary>
    ArtistNotFound,

    /// <summary>
    /// The normalized name is the one of the artist or of one of its aliases already.
    /// </summary>
    Duplicate,

    /// <summary>
    /// The name has no letters, digits or symbols.
    /// </summary>
    Empty,
}
