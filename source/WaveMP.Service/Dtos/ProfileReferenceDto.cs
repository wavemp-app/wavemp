namespace WaveMP.Service.Dtos;

/// <summary>
/// A profile referred to by another resource, for example the profile that reviewed a track.
/// </summary>
public sealed class ProfileReferenceDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }
}
