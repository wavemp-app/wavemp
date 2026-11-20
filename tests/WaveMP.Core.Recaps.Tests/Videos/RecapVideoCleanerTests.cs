using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Recaps.Options;
using WaveMP.Core.Recaps.Videos;
using WaveMP.Testing.Files;

namespace WaveMP.Core.Recaps.Tests.Videos;

[TestFixture]
public class RecapVideoCleanerTests
{
    [Test]
    public void CleanUp_Always_DeletesOnlyFilesOlderThanTheMaximumAge()
    {
        using TemporaryDirectory folder = new();
        DateTimeOffset now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        string old = folder.Combine("old.upload");
        string fresh = folder.Combine("fresh.mp4");
        File.WriteAllBytes(old, [1]);
        File.WriteAllBytes(fresh, [1]);
        File.SetLastWriteTimeUtc(old, now.UtcDateTime.AddHours(-2));
        File.SetLastWriteTimeUtc(fresh, now.UtcDateTime.AddMinutes(-10));
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(now);
        RecapVideoCleaner cleaner = new(
            Microsoft.Extensions.Options.Options.Create(new RecapOptions
            {
                VideoPath = folder.Path,
                TemporaryFileMaxAge = TimeSpan.FromHours(1),
            }),
            clock,
            NullLogger<RecapVideoCleaner>.Instance);

        int deleted = cleaner.CleanUp();

        deleted.Should().Be(1);
        File.Exists(old).Should().BeFalse();
        File.Exists(fresh).Should().BeTrue();
    }

    [Test]
    public void CleanUp_WithoutFolder_DeletesNothing()
    {
        RecapVideoCleaner cleaner = new(
            Microsoft.Extensions.Options.Options.Create(new RecapOptions
            {
                VideoPath = Path.Combine(Path.GetTempPath(), "wavemp-missing-" + Guid.NewGuid().ToString("N")),
            }),
            TimeProvider.System,
            NullLogger<RecapVideoCleaner>.Instance);

        cleaner.CleanUp().Should().Be(0);
    }
}
