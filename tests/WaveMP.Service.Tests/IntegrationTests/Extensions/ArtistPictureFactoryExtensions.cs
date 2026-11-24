using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WaveMP.Core.ArtistPictures.Stores;
using WaveMP.Data;
using WaveMP.Data.Entities;

namespace WaveMP.Service.Tests.IntegrationTests.Extensions;

/// <summary>
/// Stores artist pictures next to the running service, through its store and its database, for pictures the API
/// cannot store yet (an online picture is otherwise taken over from the online lookup).
/// </summary>
public static class ArtistPictureFactoryExtensions
{
    /// <summary>
    /// Stores the picture as the online picture of the artist with a credit of Wikimedia Commons and returns its id.
    /// </summary>
    public static async Task<Guid> AddOnlineArtistPictureAsync(
        this WaveMpWebApplicationFactory factory,
        Guid artistId,
        byte[] picture)
    {
        var pictureId = Guid.CreateVersion7();
        IArtistPictureStore store = factory.Services.GetRequiredService<IArtistPictureStore>();
        await using (MemoryStream content = new(picture))
        {
            ArtistPictureSaveResult saved = await store.SaveAsync(pictureId, content);
            saved.Status.Should().Be(ArtistPictureSaveStatus.Saved);
        }

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        WaveMpDbContext db = scope.ServiceProvider.GetRequiredService<WaveMpDbContext>();
        db.ArtistPictures.Add(new ArtistPicture
        {
            Id = pictureId,
            ArtistId = artistId,
            Kind = ArtistPictureKind.Online,
            CreatedAt = DateTimeOffset.UtcNow,
            Width = 300,
            Height = 300,
            Provider = ArtistPictureProvider.Wikimedia,
            MusicBrainzArtistId = Guid.Parse("5b11f4ce-a62d-471e-81fc-a69a8278c7da"),
            Author = "Jane Doe",
            License = "CC BY-SA 4.0",
            LicenseUrl = "https://creativecommons.org/licenses/by-sa/4.0/",
            SourceUrl = "https://commons.wikimedia.org/wiki/File:Example.jpg",
        });
        await db.SaveChangesAsync();
        return pictureId;
    }
}
