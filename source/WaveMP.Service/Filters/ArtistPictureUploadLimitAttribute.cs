using Microsoft.Extensions.Options;
using WaveMP.Core.ArtistPictures.Options;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Filters;

/// <summary>
/// Limits the request body of an artist picture upload to <see cref="ArtistPictureOptions.MaxUploadBytes"/> plus the
/// room of the multipart framing; larger bodies are answered with 413 and <c>artist.picture_too_large</c> (see
/// <see cref="UploadLimitAttribute"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ArtistPictureUploadLimitAttribute : UploadLimitAttribute
{
    protected override ProblemCodeDto TooLargeCode => ProblemCodeDto.ArtistPictureTooLarge;

    protected override long MaxUploadBytes(IServiceProvider services)
    {
        return services.GetRequiredService<IOptions<ArtistPictureOptions>>().Value.MaxUploadBytes;
    }
}
