using WaveMP.Core.Scanning.Sources;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class MediaSourceMappingExtensions
{
    public static MediaSourceDto ToDto(this MediaSourceOverview source)
    {
        return new MediaSourceDto
        {
            Id = source.Id,
            Name = source.Name,
            Path = source.Path,
            FollowsMediaPath = source.FollowsMediaPath,
            IsActive = source.IsActive,
            IsAvailable = source.IsAvailable,
            IsWatched = source.IsWatched,
            TrackCount = source.TrackCount,
            FileCount = source.FileCount,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
        };
    }
}
