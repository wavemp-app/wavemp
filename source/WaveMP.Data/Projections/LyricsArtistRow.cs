using WaveMP.Data.Entities;

namespace WaveMP.Data.Projections;

/// <summary>
/// An artist of a track as the lookup of its lyrics needs it.
/// </summary>
/// <param name="Name">Name of the artist.</param>
/// <param name="Role">Main or featured artist.</param>
public sealed record LyricsArtistRow(string Name, ArtistRole Role);
