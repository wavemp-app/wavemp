namespace WaveMP.Data.Repositories;

/// <summary>
/// Thrown when a media source is saved with a name that another source already has (unique index on the normalized
/// name).
/// </summary>
public sealed class MediaSourceNameConflictException(Exception innerException)
    : Exception("The name is already used by another media source.", innerException);
