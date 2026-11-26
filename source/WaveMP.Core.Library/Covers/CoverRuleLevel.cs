namespace WaveMP.Core.Library.Covers;

/// <summary>
/// The level a cover rule is set on. The more specific level wins.
/// </summary>
public enum CoverRuleLevel
{
    /// <summary>
    /// The library settings; applies to all albums and tracks without a rule of their own.
    /// </summary>
    Global,

    /// <summary>
    /// An album; applies to its tracks without a rule of their own.
    /// </summary>
    Album,

    /// <summary>
    /// A single track.
    /// </summary>
    Track,
}
