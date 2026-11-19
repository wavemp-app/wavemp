namespace WaveMP.Data.Entities;

/// <summary>
/// How the rules of a smart playlist are combined.
/// </summary>
public enum SmartPlaylistMatch
{
    /// <summary>
    /// A track has to meet every rule.
    /// </summary>
    All,

    /// <summary>
    /// A track has to meet at least one rule.
    /// </summary>
    Any,
}
