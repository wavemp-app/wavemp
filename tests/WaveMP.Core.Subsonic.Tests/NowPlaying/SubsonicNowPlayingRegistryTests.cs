using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Subsonic.NowPlaying;

namespace WaveMP.Core.Subsonic.Tests.NowPlaying;

[TestFixture]
public class SubsonicNowPlayingRegistryTests
{
    private readonly Guid _userId = Guid.NewGuid();

    private DateTimeOffset _now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    private SubsonicNowPlayingRegistry _registry = null!;

    [SetUp]
    public void SetUp()
    {
        TimeProvider time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(_ => _now);
        _registry = new SubsonicNowPlayingRegistry(time);
    }

    [Test]
    public void Report_ReplacesTheSongOfTheSamePlayer()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        _registry.Report(_userId, first, "Symfonium");
        _now = _now.AddMinutes(3);
        _registry.Report(_userId, second, "Symfonium");

        _registry.List().Should().Equal(new SubsonicNowPlayingEntry(_userId, second, "Symfonium", _now));
    }

    [Test]
    public void Report_KeepsTheSongsOfOtherPlayersLatestFirst()
    {
        var phone = Guid.NewGuid();
        var web = Guid.NewGuid();

        _registry.Report(_userId, phone, "Symfonium");
        _now = _now.AddMinutes(1);
        _registry.Report(_userId, web, null);

        _registry.List().Select(entry => entry.TrackId).Should().Equal(web, phone);
    }

    [Test]
    public void List_ForgetsOldSongs()
    {
        _registry.Report(_userId, Guid.NewGuid(), "Symfonium");
        _now += SubsonicNowPlayingRegistry.MaxAge + TimeSpan.FromSeconds(1);

        _registry.List().Should().BeEmpty();
    }
}
