using WaveMP.Data.Entities;

namespace WaveMP.Core.Metadata.Artists;

/// <summary>
/// An artist of a song or album as the metadata names it: its name, its role and, if the tags name it, its
/// MusicBrainz artist.
/// </summary>
/// <param name="Name">Name of the artist, normalized (see <see cref="Texts.MetadataText.Normalize"/>).</param>
/// <param name="Role">Main or featured artist; album artists are always main artists.</param>
/// <param name="MusicBrainzArtistId">The MusicBrainz artist of the name, if the tags name it.</param>
public sealed record ArtistName(string Name, ArtistRole Role = ArtistRole.Main, Guid? MusicBrainzArtistId = null);
