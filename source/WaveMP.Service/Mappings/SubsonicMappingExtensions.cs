using WaveMP.Core.Subsonic.Accounts;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class SubsonicMappingExtensions
{
    public static SubsonicAccessDto ToDto(this SubsonicAccountState state)
    {
        return new SubsonicAccessDto
        {
            Enabled = state.Enabled,
            HasPassword = state.HasPassword,
            PasswordCreatedAt = state.PasswordCreatedAt,
        };
    }

    public static SubsonicPasswordDto ToDto(this SubsonicPassword password)
    {
        return new SubsonicPasswordDto { Password = password.Password, Access = password.State.ToDto() };
    }
}
