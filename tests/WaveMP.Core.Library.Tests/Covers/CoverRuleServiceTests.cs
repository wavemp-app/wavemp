using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Tests.Covers;

[TestFixture]
public class CoverRuleServiceTests
{
    private readonly Guid _albumId = Guid.NewGuid();
    private readonly Guid _trackId = Guid.NewGuid();

    private ICoverRepository _covers = null!;
    private ISettingsSnapshot<LibrarySettings> _settings = null!;
    private ICoverRuleChangeHandler _handler = null!;
    private CoverRuleService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _covers = Substitute.For<ICoverRepository>();
        _covers.GetAlbumRuleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((CoverRuleRow?)null);
        _covers.GetTrackRuleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((CoverRuleRow?)null);
        _covers.GetAlbumCoverFallbackAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((AlbumCoverFallbackRow?)null);
        _covers.SetAlbumCoverFallbackAsync(_albumId, Arg.Any<AlbumCoverFallback?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _covers.SetAlbumRuleAsync(_albumId, Arg.Any<CoverPriority?>(), Arg.Any<CancellationToken>()).Returns(true);
        _covers.SetTrackRuleAsync(_trackId, Arg.Any<CoverPriority?>(), Arg.Any<CancellationToken>()).Returns(true);
        _settings = Substitute.For<ISettingsSnapshot<LibrarySettings>>();
        _settings.Current.Returns(new LibrarySettings
        {
            TrackCoverPriority = CoverPriority.TrackFirst,
            AlbumCoverFallback = AlbumCoverFallback.ArtistPicture,
        });
        _handler = Substitute.For<ICoverRuleChangeHandler>();
        _service = new CoverRuleService(_covers, _settings, [_handler], NullLogger<CoverRuleService>.Instance);
    }

    [Test]
    public async Task GetAlbumRuleAsync_UnknownAlbum_ReturnsNotFound()
    {
        CoverRuleResult result = await _service.GetAlbumRuleAsync(Guid.NewGuid());

        result.IsFound.Should().BeFalse();
    }

    [TestCase(null, CoverPriority.TrackFirst)]
    [TestCase(CoverPriority.AlbumFirst, CoverPriority.AlbumFirst)]
    public async Task GetAlbumRuleAsync_KnownAlbum_InheritsTheGlobalRule(CoverPriority? rule, CoverPriority effective)
    {
        _covers.GetAlbumRuleAsync(_albumId, Arg.Any<CancellationToken>()).Returns(new CoverRuleRow(rule, null));

        CoverRuleResult result = await _service.GetAlbumRuleAsync(_albumId);

        result.Should().Be(new CoverRuleResult(true, rule, CoverPriority.TrackFirst));
        result.Effective.Should().Be(effective);
    }

    [TestCase(null, null, CoverPriority.TrackFirst, CoverPriority.TrackFirst)]
    [TestCase(null, CoverPriority.AlbumFirst, CoverPriority.AlbumFirst, CoverPriority.AlbumFirst)]
    [TestCase(CoverPriority.TrackFirst, CoverPriority.AlbumFirst, CoverPriority.AlbumFirst, CoverPriority.TrackFirst)]
    [TestCase(CoverPriority.AlbumFirst, null, CoverPriority.TrackFirst, CoverPriority.AlbumFirst)]
    public async Task GetTrackRuleAsync_KnownTrack_InheritsTheRuleOfItsAlbumOtherwiseTheGlobalOne(
        CoverPriority? rule,
        CoverPriority? albumRule,
        CoverPriority inherited,
        CoverPriority effective)
    {
        _covers.GetTrackRuleAsync(_trackId, Arg.Any<CancellationToken>()).Returns(new CoverRuleRow(rule, albumRule));

        CoverRuleResult result = await _service.GetTrackRuleAsync(_trackId);

        result.IsFound.Should().BeTrue();
        result.Rule.Should().Be(rule);
        result.Inherited.Should().Be(inherited);
        result.Effective.Should().Be(effective);
    }

    [Test]
    public async Task SetAlbumRuleAsync_NewRule_StoresItAndNotifiesTheHandlers()
    {
        _covers.GetAlbumRuleAsync(_albumId, Arg.Any<CancellationToken>()).Returns(new CoverRuleRow(null, null));

        CoverRuleResult result = await _service.SetAlbumRuleAsync(_albumId, CoverPriority.AlbumFirst);

        result.Should().Be(new CoverRuleResult(true, CoverPriority.AlbumFirst, CoverPriority.TrackFirst));
        await _covers.Received(1).SetAlbumRuleAsync(_albumId, CoverPriority.AlbumFirst, Arg.Any<CancellationToken>());
        await _handler.Received(1).OnCoverRuleChangedAsync(
            new CoverRuleChange(CoverRuleLevel.Album, _albumId, null, CoverPriority.AlbumFirst),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetTrackRuleAsync_Inherit_RemovesTheRuleAndNotifiesTheHandlers()
    {
        _covers.GetTrackRuleAsync(_trackId, Arg.Any<CancellationToken>())
            .Returns(new CoverRuleRow(CoverPriority.TrackFirst, CoverPriority.AlbumFirst));

        CoverRuleResult result = await _service.SetTrackRuleAsync(_trackId, null);

        result.Should().Be(new CoverRuleResult(true, null, CoverPriority.AlbumFirst));
        await _covers.Received(1).SetTrackRuleAsync(_trackId, null, Arg.Any<CancellationToken>());
        await _handler.Received(1).OnCoverRuleChangedAsync(
            new CoverRuleChange(CoverRuleLevel.Track, _trackId, CoverPriority.TrackFirst, null),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetTrackRuleAsync_SameRule_DoesNotNotifyTheHandlers()
    {
        _covers.GetTrackRuleAsync(_trackId, Arg.Any<CancellationToken>())
            .Returns(new CoverRuleRow(CoverPriority.TrackFirst, null));

        CoverRuleResult result = await _service.SetTrackRuleAsync(_trackId, CoverPriority.TrackFirst);

        result.IsFound.Should().BeTrue();
        await _handler.DidNotReceiveWithAnyArgs().OnCoverRuleChangedAsync(default!, default);
    }

    [Test]
    public async Task SetAlbumRuleAsync_UnknownAlbum_ReturnsNotFoundAndChangesNothing()
    {
        CoverRuleResult result = await _service.SetAlbumRuleAsync(Guid.NewGuid(), CoverPriority.AlbumFirst);

        result.IsFound.Should().BeFalse();
        await _covers.DidNotReceiveWithAnyArgs().SetAlbumRuleAsync(default, default, default);
        await _handler.DidNotReceiveWithAnyArgs().OnCoverRuleChangedAsync(default!, default);
    }

    [Test]
    public async Task SetTrackRuleAsync_TrackRemovedInTheMeantime_ReturnsNotFound()
    {
        var removed = Guid.NewGuid();
        _covers.GetTrackRuleAsync(removed, Arg.Any<CancellationToken>()).Returns(new CoverRuleRow(null, null));
        _covers.SetTrackRuleAsync(removed, Arg.Any<CoverPriority?>(), Arg.Any<CancellationToken>()).Returns(false);

        CoverRuleResult result = await _service.SetTrackRuleAsync(removed, CoverPriority.AlbumFirst);

        result.IsFound.Should().BeFalse();
        await _handler.DidNotReceiveWithAnyArgs().OnCoverRuleChangedAsync(default!, default);
    }

    [Test]
    public async Task SetAlbumRuleAsync_FailingHandler_KeepsTheChange()
    {
        _covers.GetAlbumRuleAsync(_albumId, Arg.Any<CancellationToken>()).Returns(new CoverRuleRow(null, null));
        _handler.OnCoverRuleChangedAsync(Arg.Any<CoverRuleChange>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("broken"));

        CoverRuleResult result = await _service.SetAlbumRuleAsync(_albumId, CoverPriority.TrackFirst);

        result.Rule.Should().Be(CoverPriority.TrackFirst);
    }

    [TestCase(null, AlbumCoverFallback.ArtistPicture)]
    [TestCase(AlbumCoverFallback.None, AlbumCoverFallback.None)]
    public async Task GetAlbumFallbackAsync_KnownAlbum_InheritsTheGlobalRule(
        AlbumCoverFallback? fallback,
        AlbumCoverFallback effective)
    {
        _covers.GetAlbumCoverFallbackAsync(_albumId, Arg.Any<CancellationToken>())
            .Returns(new AlbumCoverFallbackRow(fallback, null));

        AlbumCoverFallbackResult result = await _service.GetAlbumFallbackAsync(_albumId);

        result.Should().Be(new AlbumCoverFallbackResult(true, fallback, AlbumCoverFallback.ArtistPicture));
        result.Effective.Should().Be(effective);
    }

    [Test]
    public async Task GetAlbumFallbackAsync_UnknownAlbum_ReturnsNotFound()
    {
        AlbumCoverFallbackResult result = await _service.GetAlbumFallbackAsync(Guid.NewGuid());

        result.IsFound.Should().BeFalse();
    }

    [Test]
    public async Task SetAlbumFallbackAsync_KnownAlbum_StoresTheExceptionWithoutStartingTheCoverCache()
    {
        _covers.GetAlbumCoverFallbackAsync(_albumId, Arg.Any<CancellationToken>())
            .Returns(new AlbumCoverFallbackRow(null, Guid.NewGuid()));

        AlbumCoverFallbackResult result = await _service.SetAlbumFallbackAsync(_albumId, AlbumCoverFallback.None);

        result.Should().Be(
            new AlbumCoverFallbackResult(true, AlbumCoverFallback.None, AlbumCoverFallback.ArtistPicture));
        await _covers.Received(1).SetAlbumCoverFallbackAsync(
            _albumId,
            AlbumCoverFallback.None,
            Arg.Any<CancellationToken>());
        await _handler.DidNotReceiveWithAnyArgs().OnCoverRuleChangedAsync(default!, default);
    }

    [Test]
    public async Task SetAlbumFallbackAsync_UnknownAlbum_ReturnsNotFoundAndChangesNothing()
    {
        AlbumCoverFallbackResult result = await _service.SetAlbumFallbackAsync(Guid.NewGuid(), null);

        result.IsFound.Should().BeFalse();
        await _covers.DidNotReceiveWithAnyArgs().SetAlbumCoverFallbackAsync(default, default, default);
    }
}
