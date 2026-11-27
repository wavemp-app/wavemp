using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.ArtistPictures.Lookups;
using WaveMP.Core.ArtistPictures.Options;
using WaveMP.Core.ArtistPictures.Tests.Fakes;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Files;

namespace WaveMP.Core.ArtistPictures.Tests.Lookups;

/// <summary>
/// Taking the aliases of an artist from MusicBrainz with a simulated client and repository and a cache in a temporary
/// folder.
/// </summary>
[TestFixture]
public class ArtistAliasLookupServiceTests
{
    private static readonly Guid _musicBrainzId = Guid.Parse("f4d5cc07-3bc9-4836-9b15-88a08359bc63");

    private readonly Guid _artistId = Guid.NewGuid();

    private TemporaryDirectory _folder = null!;
    private ManualClock _clock = null!;
    private IArtistPictureRepository _pictures = null!;
    private IArtistAliasRepository _aliases = null!;
    private IMusicBrainzArtistClient _musicBrainz = null!;
    private ISettingsSnapshot<ArtistPictureSettings> _settings = null!;
    private ArtistAliasLookupService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = new TemporaryDirectory();
        _clock = new ManualClock(new DateTimeOffset(2026, 11, 27, 10, 0, 0, TimeSpan.Zero));
        _pictures = Substitute.For<IArtistPictureRepository>();
        _pictures.FindLookupAsync(_artistId, Arg.Any<CancellationToken>())
            .Returns(new ArtistLookupRow(_artistId, "P!nk", _musicBrainzId));
        _aliases = Substitute.For<IArtistAliasRepository>();
        _aliases.ReplaceMusicBrainzAliasesAsync(default, default!, default, default)
            .ReturnsForAnyArgs(call => (IReadOnlyList<ArtistAliasRow>?)
            [
                .. call.ArgAt<IReadOnlyList<string>>(1).Select(name =>
                    new ArtistAliasRow(Guid.NewGuid(), _artistId, name, ArtistAliasSource.MusicBrainz)),
            ]);
        _musicBrainz = Substitute.For<IMusicBrainzArtistClient>();
        _musicBrainz.GetArtistAsync(_musicBrainzId, Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<MusicBrainzArtistDetails>.Found(
                new MusicBrainzArtistDetails("Q1", "P!nk", ["Pink", "Alecia  Moore", "\u0007"])));
        _settings = Substitute.For<ISettingsSnapshot<ArtistPictureSettings>>();
        _settings.Current.Returns(new ArtistPictureSettings { OnlineLookupEnabled = true });
        ISettingsSnapshot<OnlineMetadataSettings> onlineSettings =
            Substitute.For<ISettingsSnapshot<OnlineMetadataSettings>>();
        onlineSettings.Current.Returns(new OnlineMetadataSettings());
        ArtistLookupCache cache = new(
            new FolderCacheStore(_folder.Path),
            Microsoft.Extensions.Options.Options.Create(new ArtistPictureOptions()),
            _clock,
            NullLogger<ArtistLookupCache>.Instance);
        _service = new ArtistAliasLookupService(
            _pictures,
            _aliases,
            cache,
            _musicBrainz,
            _settings,
            onlineSettings,
            _clock);
    }

    [TearDown]
    public void TearDown()
    {
        _folder.Dispose();
    }

    [Test]
    public async Task LookUpAsync_KnownMusicBrainzArtist_SavesItsSortNameAndAliasesCleaned()
    {
        ArtistAliasLookupResult result = await _service.LookUpAsync(_artistId);

        result.Status.Should().Be(ArtistLookupStatus.Found);
        result.Aliases.Select(alias => alias.Name).Should().Equal("P!nk", "Pink", "Alecia Moore");
        await _aliases.Received(1).ReplaceMusicBrainzAliasesAsync(
            _artistId,
            Arg.Any<IReadOnlyList<string>>(),
            _clock.GetUtcNow(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task LookUpAsync_Twice_AsksMusicBrainzOnce()
    {
        await _service.LookUpAsync(_artistId);
        await _service.LookUpAsync(_artistId);

        await _musicBrainz.ReceivedWithAnyArgs(1).GetArtistAsync(default, default);
        await _aliases.ReceivedWithAnyArgs(2).ReplaceMusicBrainzAliasesAsync(default, default!, default, default);
    }

    [Test]
    public async Task LookUpAsync_LookupOff_AsksNothing()
    {
        _settings.Current.Returns(new ArtistPictureSettings { OnlineLookupEnabled = false });

        ArtistAliasLookupResult result = await _service.LookUpAsync(_artistId);

        result.Status.Should().Be(ArtistLookupStatus.Disabled);
        await _musicBrainz.DidNotReceiveWithAnyArgs().GetArtistAsync(default, default);
        await _aliases.DidNotReceiveWithAnyArgs().ReplaceMusicBrainzAliasesAsync(default, default!, default, default);
    }

    [Test]
    public async Task LookUpAsync_ArtistWithoutMusicBrainzArtist_IsMissing()
    {
        _pictures.FindLookupAsync(_artistId, Arg.Any<CancellationToken>())
            .Returns(new ArtistLookupRow(_artistId, "Band", null));

        ArtistAliasLookupResult result = await _service.LookUpAsync(_artistId);

        result.Status.Should().Be(ArtistLookupStatus.MusicBrainzArtistMissing);
        await _musicBrainz.DidNotReceiveWithAnyArgs().GetArtistAsync(default, default);
    }

    [Test]
    public async Task LookUpAsync_UnknownArtist_IsNotFound()
    {
        ArtistAliasLookupResult result = await _service.LookUpAsync(Guid.NewGuid());

        result.Status.Should().Be(ArtistLookupStatus.ArtistNotFound);
    }

    [TestCase(OnlineStatus.Unavailable, ArtistLookupStatus.Unavailable)]
    [TestCase(OnlineStatus.Disabled, ArtistLookupStatus.Disabled)]
    public async Task LookUpAsync_MusicBrainzFails_ChangesNothing(OnlineStatus failure, ArtistLookupStatus expected)
    {
        _musicBrainz.GetArtistAsync(_musicBrainzId, Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<MusicBrainzArtistDetails>.Failed(failure));

        ArtistAliasLookupResult result = await _service.LookUpAsync(_artistId);

        result.Status.Should().Be(expected);
        await _aliases.DidNotReceiveWithAnyArgs().ReplaceMusicBrainzAliasesAsync(default, default!, default, default);
    }

    [Test]
    public async Task LookUpAsync_MusicBrainzDoesNotKnowTheArtist_RemovesItsAliasesOfMusicBrainz()
    {
        _musicBrainz.GetArtistAsync(_musicBrainzId, Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<MusicBrainzArtistDetails>.Failed(OnlineStatus.NotFound));

        ArtistAliasLookupResult result = await _service.LookUpAsync(_artistId);

        result.Status.Should().Be(ArtistLookupStatus.Found);
        await _aliases.Received(1).ReplaceMusicBrainzAliasesAsync(
            _artistId,
            Arg.Is<IReadOnlyList<string>>(names => names.Count == 0),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }
}
