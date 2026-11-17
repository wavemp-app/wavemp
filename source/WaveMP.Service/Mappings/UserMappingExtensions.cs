using WaveMP.Core.Users.Avatars;
using WaveMP.Core.Users.Pins;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class UserMappingExtensions
{
    public static ProfileDto ToProfileDto(this User user)
    {
        return new ProfileDto
        {
            Id = user.Id,
            Name = user.Name,
            Initials = user.Initials,
            Color = user.Color,
            Role = user.Role.ToDto(),
            IsBuiltIn = user.IsBuiltIn,
            HasPin = user.PinHash is not null,
            Avatar = user.ToAvatarDto(),
        };
    }

    public static UserDto ToUserDto(this User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Name = user.Name,
            Initials = user.Initials,
            Color = user.Color,
            Role = user.Role.ToDto(),
            IsBuiltIn = user.IsBuiltIn,
            HasPin = user.PinHash is not null,
            Avatar = user.ToAvatarDto(),
            CreatedAt = user.CreatedAt,
        };
    }

    public static AvatarDto? ToAvatarDto(this User user)
    {
        return user.AvatarUpdatedAt is { } updatedAt
            ? new AvatarDto { Version = AvatarVersions.Of(updatedAt), PresetId = user.AvatarPresetId }
            : null;
    }

    public static PinStatusDto ToDto(this PinStatus status)
    {
        return new PinStatusDto { IsSet = status.IsSet, IsManagedByEnvironment = status.IsManagedByEnvironment };
    }

    public static RoleDto ToDto(this UserRole role)
    {
        return role switch
        {
            UserRole.Admin => RoleDto.Admin,
            UserRole.User => RoleDto.User,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role."),
        };
    }

    public static UserRole ToEntity(this RoleDto role)
    {
        return role switch
        {
            RoleDto.Admin => UserRole.Admin,
            RoleDto.User => UserRole.User,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role."),
        };
    }
}
