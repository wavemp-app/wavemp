using Microsoft.Extensions.Options;
using WaveMP.Core.Users.Avatars;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Filters;

/// <summary>
/// Limits the request body of an avatar upload to <see cref="AvatarOptions.MaxUploadBytes"/> plus the room of the
/// multipart framing; larger bodies are answered with 413 and <c>avatar.too_large</c> (see
/// <see cref="UploadLimitAttribute"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AvatarUploadLimitAttribute : UploadLimitAttribute
{
    protected override ProblemCodeDto TooLargeCode => ProblemCodeDto.AvatarTooLarge;

    protected override long MaxUploadBytes(IServiceProvider services)
    {
        return services.GetRequiredService<IOptions<AvatarOptions>>().Value.MaxUploadBytes;
    }
}
