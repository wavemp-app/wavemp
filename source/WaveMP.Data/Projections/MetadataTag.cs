namespace WaveMP.Data.Projections;

/// <summary>
/// A field of the tags that the review inbox checks: a track whose file lacks one of them gets a hint.
/// </summary>
public enum MetadataTag
{
    Title,
    Artist,
    Album,
    Year,
    TrackNumber,
    Genre,
}
