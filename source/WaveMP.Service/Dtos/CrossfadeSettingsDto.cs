namespace WaveMP.Service.Dtos;

/// <summary>
/// Crossfade between tracks of the current profile.
/// </summary>
public sealed class CrossfadeSettingsDto
{
    /// <summary>
    /// Whether the players fade from one track into the next; false (the default) plays the tracks gapless. Turning it
    /// off keeps the seconds, so turning it on again fades as long as before.
    /// </summary>
    public required bool Enabled { get; init; }

    /// <summary>
    /// Seconds the players fade from one track into the next while the crossfade is enabled, from 0 to 12 (5 by
    /// default); 0 plays the tracks gapless.
    /// </summary>
    public required int Seconds { get; init; }

    /// <summary>
    /// Whether consecutive tracks of the same album play gapless even with a crossfade; true by default.
    /// </summary>
    public required bool AlbumsGapless { get; init; }
}
