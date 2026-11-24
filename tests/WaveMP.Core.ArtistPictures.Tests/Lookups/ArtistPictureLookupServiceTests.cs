using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.ArtistPictures.Lookups;
using WaveMP.Core.ArtistPictures.Options;
using WaveMP.Core.ArtistPictures.Stores;
using WaveMP.Core.ArtistPictures.Tests.Fakes;
using WaveMP.Core.Caching.Images;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Files;

namespace WaveMP.Core.ArtistPictures.Tests.Lookups;

/// <summary>
/// The online lookup of artist pictures with simulated clients (no request leaves the process), a cache in a temporary
/// folder and a simulated encoder.
/// </summary>
[TestFixture]
public class ArtistPictureLookupServiceTests
{
    private const string ApiKey = "0123456789abcdef0123456789abcdef";
    private const string ItemId = "Q11649";

    private static readonly Guid _musicBrainzId = Guid.Parse("5b11f4ce-a62d-471e-81fc-a69a8278c7da");
    private static readonly byte[] _jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 74, 70, 73, 70, 0, 1, 1, 0, 0, 1];
    private static readonly Uri _preview = new("https://upload.wikimedia.org/thumb/Band.jpg/500px-Band.jpg");
    private static readonly Uri _full = new("https://upload.wikimedia.org/thumb/Band.jpg/1280px-Band.jpg");
    private static readonly Uri _fanart = new("https://assets.fanart.tv/fanart/music/x/artistthumb/band.jpg");

    private readonly Guid _artistId = Guid.NewGuid();
    private readonly ArtistPictureOptions _options = new();

    private TemporaryDirectory _folder = null!;
    private FolderCacheStore _cacheStore = null!;
    private ManualClock _clock = null!;
    private IArtistPictureRepository _repository = null!;
    private IArtistPictureStore _store = null!;
    private IMusicBrainzArtistClient _musicBrainz = null!;
    private IWikidataClient _wikidata = null!;
    private IWikimediaCommonsClient _commons = null!;
    private IFanartTvClient _fanartTv = null!;
    private ICoverImageEncoder _encoder = null!;
    private ISettingsSnapshot<ArtistPictureSettings> _settings = null!;
    private ArtistPictureLookupService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = new TemporaryDirectory();
        _cacheStore = new FolderCacheStore(_folder.Path);
        _clock = new ManualClock(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero));
        _repository = Substitute.For<IArtistPictureRepository>();
        _repository.FindLookupAsync(_artistId, Arg.Any<CancellationToken>())
            .Returns(new ArtistLookupRow(_artistId, "Band", null));
        _repository.ReplaceAsync(Arg.Any<ArtistPicture>(), Arg.Any<CancellationToken>())
            .Returns(new ArtistPictureReplacement(true, []));
        _repository.RememberMusicBrainzArtistAsync(default, default, default).ReturnsForAnyArgs(true);
        _store = Substitute.For<IArtistPictureStore>();
        _store.SaveAsync(Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<CoverCrop?>(), Arg.Any<CancellationToken>())
            .Returns(new ArtistPictureSaveResult(ArtistPictureSaveStatus.Saved, 1000, 1000));
        _musicBrainz = Substitute.For<IMusicBrainzArtistClient>();
        _musicBrainz.SearchAsync(default!, default, default, default)
            .ReturnsForAnyArgs(OnlineResponse<MusicBrainzArtistPage>.Found(new MusicBrainzArtistPage(
                1,
                [new MusicBrainzArtistMatch(_musicBrainzId, "Band", null, "rock band", "Group", "GB", "1990", null, 98)])));
        _musicBrainz.GetWikidataItemAsync(_musicBrainzId, Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<string>.Found(ItemId));
        _wikidata = Substitute.For<IWikidataClient>();
        _wikidata.GetImageFileNamesAsync(ItemId, Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<IReadOnlyList<string>>.Found(["Band.jpg"]));
        _commons = Substitute.For<IWikimediaCommonsClient>();
        GivenImage(IWikimediaCommonsClient.PreviewWidth, _preview);
        GivenImage(IWikimediaCommonsClient.FullWidth, _full);
        _commons.DownloadAsync(default!, default).ReturnsForAnyArgs(OnlineResponse<byte[]>.Found(_jpeg));
        _fanartTv = Substitute.For<IFanartTvClient>();
        _fanartTv.GetArtistPicturesAsync(default, default!, default)
            .ReturnsForAnyArgs(OnlineResponse<IReadOnlyList<FanartTvPicture>>.Found(
                [new FanartTvPicture("7", _fanart, 3)]));
        _fanartTv.DownloadAsync(default!, default).ReturnsForAnyArgs(OnlineResponse<byte[]>.Found(_jpeg));
        _encoder = Substitute.For<ICoverImageEncoder>();
        _encoder.EncodeAsync(default!, default!, default!, default)
            .ReturnsForAnyArgs(call =>
            {
                foreach (CoverEncodeTarget target in call.ArgAt<IReadOnlyList<CoverEncodeTarget>>(2))
                {
                    File.WriteAllBytes(target.OutputPath, "RIFF\0\0\0\0WEBP"u8.ToArray());
                }

                return CoverEncodeStatus.Encoded;
            });
        _settings = Substitute.For<ISettingsSnapshot<ArtistPictureSettings>>();
        GivenSettings(enabled: true);
        ISettingsSnapshot<OnlineMetadataSettings> onlineSettings =
            Substitute.For<ISettingsSnapshot<OnlineMetadataSettings>>();
        onlineSettings.Current.Returns(new OnlineMetadataSettings());
        Microsoft.Extensions.Options.IOptions<ArtistPictureOptions> options =
            Microsoft.Extensions.Options.Options.Create(_options);
        _service = new ArtistPictureLookupService(
            _repository,
            _store,
            new ArtistLookupCache(_cacheStore, options, _clock, NullLogger<ArtistLookupCache>.Instance),
            _musicBrainz,
            _wikidata,
            _commons,
            _fanartTv,
            _encoder,
            _settings,
            onlineSettings,
            _clock,
            NullLogger<ArtistPictureLookupService>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _folder.Dispose();
    }

    [Test]
    public async Task AllSteps_LookupOff_AskNoServiceAndChangeNothing()
    {
        GivenSettings(enabled: false, ApiKey);

        ArtistMatchesResult matches = await _service.FindMatchesAsync(_artistId, 0, 10);
        ArtistCandidatesResult candidates = await _service.FindCandidatesAsync(_artistId, _musicBrainzId);
        ArtistCandidatePreview preview = await _service.GetPreviewAsync(_artistId, new string('a', 32), 300);
        ArtistLookupStatus takeOver = await _service.TakeOverAsync(_artistId, new string('a', 32), null);

        matches.Status.Should().Be(ArtistLookupStatus.Disabled);
        candidates.Status.Should().Be(ArtistLookupStatus.Disabled);
        preview.Status.Should().Be(ArtistLookupStatus.Disabled);
        takeOver.Should().Be(ArtistLookupStatus.Disabled);
        _musicBrainz.ReceivedCalls().Should().BeEmpty();
        _wikidata.ReceivedCalls().Should().BeEmpty();
        _commons.ReceivedCalls().Should().BeEmpty();
        _fanartTv.ReceivedCalls().Should().BeEmpty();
        _store.ReceivedCalls().Should().BeEmpty();
        _repository.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task FindMatchesAsync_UnknownArtist_IsArtistNotFoundWithoutRequest()
    {
        ArtistMatchesResult result = await _service.FindMatchesAsync(Guid.NewGuid(), 0, 10);

        result.Status.Should().Be(ArtistLookupStatus.ArtistNotFound);
        _musicBrainz.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task FindMatchesAsync_Matches_AreSearchedByNameAndReusedUntilTheyExpire()
    {
        _repository.FindLookupAsync(_artistId, Arg.Any<CancellationToken>())
            .Returns(new ArtistLookupRow(_artistId, "Band", _musicBrainzId));

        ArtistMatchesResult first = await _service.FindMatchesAsync(_artistId, 5, 99);
        _clock.Advance(_options.LookupCacheDuration - TimeSpan.FromMinutes(1));
        ArtistMatchesResult cached = await _service.FindMatchesAsync(_artistId, 5, 99);
        _clock.Advance(TimeSpan.FromMinutes(2));
        await _service.FindMatchesAsync(_artistId, 5, 99);

        first.Status.Should().Be(ArtistLookupStatus.Found);
        first.Total.Should().Be(1);
        first.RememberedId.Should().Be(_musicBrainzId);
        first.Matches.Should().ContainSingle().Which.Id.Should().Be(_musicBrainzId);
        cached.Should().BeEquivalentTo(first);
        await _musicBrainz.Received(2).SearchAsync("Band", 5, IArtistPictureLookupService.MaxMatchLimit, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FindMatchesAsync_MusicBrainzUnavailable_IsUnavailableAndNotCached()
    {
        _musicBrainz.SearchAsync(default!, default, default, default)
            .ReturnsForAnyArgs(OnlineResponse<MusicBrainzArtistPage>.Failed(OnlineStatus.Unavailable));

        ArtistMatchesResult first = await _service.FindMatchesAsync(_artistId, 0, 10);
        await _service.FindMatchesAsync(_artistId, 0, 10);

        first.Status.Should().Be(ArtistLookupStatus.Unavailable);
        await _musicBrainz.ReceivedWithAnyArgs(2).SearchAsync(default!, default, default, default);
    }

    [Test]
    public async Task FindCandidatesAsync_WithoutMusicBrainzArtist_IsMissing()
    {
        ArtistCandidatesResult result = await _service.FindCandidatesAsync(_artistId, null);

        result.Status.Should().Be(ArtistLookupStatus.MusicBrainzArtistMissing);
        _musicBrainz.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task FindCandidatesAsync_Wikimedia_KeepsOnlyPlainTextAndHttpsAddresses()
    {
        GivenImage(
            IWikimediaCommonsClient.PreviewWidth,
            _preview,
            artist: "<a href=\"//commons.wikimedia.org/wiki/User:Jane\">Jane <b>Doe</b></a><script>alert(1)</script>",
            license: "CC BY-SA 4.0<img src=x onerror=alert(1)>",
            licenseUrl: "javascript:alert(1)",
            description: "https://commons.wikimedia.org/wiki/File:Band.jpg");

        ArtistCandidatesResult result = await _service.FindCandidatesAsync(_artistId, _musicBrainzId);

        result.Status.Should().Be(ArtistLookupStatus.Found);
        ArtistPictureCandidate candidate = result.Candidates.Should().ContainSingle().Subject;
        candidate.Should().BeEquivalentTo(new ArtistPictureCandidate(
            candidate.Id,
            ArtistPictureProvider.Wikimedia,
            _musicBrainzId,
            2000,
            1500,
            "Jane Doe",
            "CC BY-SA 4.0",
            null,
            "https://commons.wikimedia.org/wiki/File:Band.jpg"));
        candidate.Id.Should().MatchRegex("^[0-9a-f]{32}$");
        await _commons.Received(1).GetImagesAsync(
            Arg.Is<IReadOnlyList<string>>(names => names.SequenceEqual(new[] { "Band.jpg" })),
            IWikimediaCommonsClient.PreviewWidth,
            Arg.Any<CancellationToken>());
        await _fanartTv.DidNotReceiveWithAnyArgs().GetArtistPicturesAsync(default, default!, default);
        await _repository.DidNotReceiveWithAnyArgs().RememberMusicBrainzArtistAsync(default, default, default);
    }

    [Test]
    public async Task FindCandidatesAsync_RememberedArtist_IsUsedWithoutAnId()
    {
        _repository.FindLookupAsync(_artistId, Arg.Any<CancellationToken>())
            .Returns(new ArtistLookupRow(_artistId, "Band", _musicBrainzId));

        ArtistCandidatesResult result = await _service.FindCandidatesAsync(_artistId, null);

        result.Candidates.Should().ContainSingle();
        await _musicBrainz.Received(1).GetWikidataItemAsync(_musicBrainzId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FindCandidatesAsync_WithApiKey_AsksFanartTvAndKeepsTheKeyOutOfTheCache()
    {
        GivenSettings(enabled: true, ApiKey);

        ArtistCandidatesResult result = await _service.FindCandidatesAsync(_artistId, _musicBrainzId);

        result.Candidates.Select(candidate => candidate.Provider).Should().Equal(
            ArtistPictureProvider.Wikimedia,
            ArtistPictureProvider.FanartTv);
        result.Candidates[1].SourceUrl.Should().Be($"https://fanart.tv/artist/{_musicBrainzId:D}");
        result.Candidates[1].Author.Should().BeNull();
        await _fanartTv.Received(1).GetArtistPicturesAsync(_musicBrainzId, ApiKey, Arg.Any<CancellationToken>());
        _cacheStore.Names().Should().NotContain(name => name.Contains(ApiKey));
        foreach (string file in Directory.EnumerateFiles(_folder.Path, "*", SearchOption.AllDirectories))
        {
            (await File.ReadAllTextAsync(file)).Should().NotContain(ApiKey);
        }
    }

    [Test]
    public async Task FindCandidatesAsync_KeyRemoved_NeitherAsksFanartTvNorUsesItsCachedAnswer()
    {
        GivenSettings(enabled: true, ApiKey);
        await _service.FindCandidatesAsync(_artistId, _musicBrainzId);
        GivenSettings(enabled: true);

        ArtistCandidatesResult result = await _service.FindCandidatesAsync(_artistId, _musicBrainzId);

        result.Candidates.Should().ContainSingle().Which.Provider.Should().Be(ArtistPictureProvider.Wikimedia);
        await _fanartTv.ReceivedWithAnyArgs(1).GetArtistPicturesAsync(default, default!, default);
    }

    [Test]
    public async Task FindCandidatesAsync_SecondLookup_ReusesTheAnswersOfTheServices()
    {
        GivenSettings(enabled: true, ApiKey);

        ArtistCandidatesResult first = await _service.FindCandidatesAsync(_artistId, _musicBrainzId);
        ArtistCandidatesResult second = await _service.FindCandidatesAsync(_artistId, _musicBrainzId);

        second.Candidates.Should().HaveCount(2);
        second.Candidates.Select(candidate => candidate.Id).Should().NotIntersectWith(
            first.Candidates.Select(candidate => candidate.Id));
        await _musicBrainz.ReceivedWithAnyArgs(1).GetWikidataItemAsync(default, default);
        await _wikidata.ReceivedWithAnyArgs(1).GetImageFileNamesAsync(default!, default);
        await _commons.ReceivedWithAnyArgs(1).GetImagesAsync(default!, default, default);
        await _fanartTv.ReceivedWithAnyArgs(1).GetArtistPicturesAsync(default, default!, default);
    }

    [Test]
    public async Task FindCandidatesAsync_NoWikidataLinkAndNoKey_IsAnEmptyList()
    {
        _musicBrainz.GetWikidataItemAsync(_musicBrainzId, Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<string>.Failed(OnlineStatus.NotFound));

        ArtistCandidatesResult result = await _service.FindCandidatesAsync(_artistId, _musicBrainzId);

        result.Status.Should().Be(ArtistLookupStatus.Found);
        result.Candidates.Should().BeEmpty();
        await _wikidata.DidNotReceiveWithAnyArgs().GetImageFileNamesAsync(default!, default);
    }

    [Test]
    public async Task FindCandidatesAsync_OneServiceFails_ReturnsThePicturesOfTheOther()
    {
        GivenSettings(enabled: true, ApiKey);
        _wikidata.GetImageFileNamesAsync(ItemId, Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<IReadOnlyList<string>>.Failed(OnlineStatus.Unavailable));

        ArtistCandidatesResult result = await _service.FindCandidatesAsync(_artistId, _musicBrainzId);

        result.Status.Should().Be(ArtistLookupStatus.Found);
        result.Candidates.Should().ContainSingle().Which.Provider.Should().Be(ArtistPictureProvider.FanartTv);
    }

    [Test]
    public async Task FindCandidatesAsync_ServicesFailWithoutPictures_IsUnavailable()
    {
        _musicBrainz.GetWikidataItemAsync(_musicBrainzId, Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<string>.Failed(OnlineStatus.Unavailable));

        ArtistCandidatesResult result = await _service.FindCandidatesAsync(_artistId, _musicBrainzId);

        result.Status.Should().Be(ArtistLookupStatus.Unavailable);
    }

    [Test]
    public async Task FindCandidatesAsync_SwitchedOffWhileLookingUp_IsDisabled()
    {
        _wikidata.GetImageFileNamesAsync(ItemId, Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<IReadOnlyList<string>>.Failed(OnlineStatus.Disabled));

        ArtistCandidatesResult result = await _service.FindCandidatesAsync(_artistId, _musicBrainzId);

        result.Status.Should().Be(ArtistLookupStatus.Disabled);
    }

    [Test]
    public async Task GetPreviewAsync_Candidate_IsDownloadedOnceAndKeptAsWebPInBothSizes()
    {
        string id = await FirstCandidateIdAsync();

        ArtistCandidatePreview small = await _service.GetPreviewAsync(_artistId, id, 120);
        ArtistCandidatePreview large = await _service.GetPreviewAsync(_artistId, id, 480);
        await small.Content!.DisposeAsync();
        await large.Content!.DisposeAsync();

        small.Status.Should().Be(ArtistLookupStatus.Found);
        large.Status.Should().Be(ArtistLookupStatus.Found);
        await _commons.Received(1).DownloadAsync(_preview, Arg.Any<CancellationToken>());
        await _encoder.Received(1).EncodeAsync(
            Arg.Any<Stream>(),
            "image/jpeg",
            Arg.Is<IReadOnlyList<CoverEncodeTarget>>(targets =>
                targets.Select(target => target.MaxSize).SequenceEqual(new[] { 300, 500 })
                && targets.All(target => !target.Square && target.Crop == null)),
            Arg.Any<CancellationToken>());
        _cacheStore.Names().Should().Contain(name => name.StartsWith("previews/") && name.EndsWith("-300.webp"))
            .And.Contain(name => name.StartsWith("previews/") && name.EndsWith("-500.webp"));
    }

    [Test]
    public async Task GetPreviewAsync_DownloadIsNoPicture_IsUnavailableWithoutEncoding()
    {
        string id = await FirstCandidateIdAsync();
        _commons.DownloadAsync(default!, default)
            .ReturnsForAnyArgs(OnlineResponse<byte[]>.Found("<html><script>x</script></html>"u8.ToArray()));

        ArtistCandidatePreview preview = await _service.GetPreviewAsync(_artistId, id, 300);

        preview.Status.Should().Be(ArtistLookupStatus.Unavailable);
        await _encoder.DidNotReceiveWithAnyArgs().EncodeAsync(default!, default!, default!, default);
    }

    [Test]
    public async Task GetPreviewAsync_CandidateOfAnotherArtist_IsNotFound()
    {
        string id = await FirstCandidateIdAsync();
        var other = Guid.NewGuid();
        _repository.FindLookupAsync(other, Arg.Any<CancellationToken>())
            .Returns(new ArtistLookupRow(other, "Other", null));

        ArtistCandidatePreview preview = await _service.GetPreviewAsync(other, id, 300);
        ArtistLookupStatus takeOver = await _service.TakeOverAsync(other, id, null);

        preview.Status.Should().Be(ArtistLookupStatus.CandidateNotFound);
        takeOver.Should().Be(ArtistLookupStatus.CandidateNotFound);
        await _commons.DidNotReceiveWithAnyArgs().DownloadAsync(default!, default);
    }

    [TestCase("../../../../etc/passwd")]
    [TestCase("ABCDEF0123456789ABCDEF0123456789")]
    [TestCase("0123")]
    [TestCase("https://evil.example/picture.jpg")]
    public async Task GetPreviewAsync_NoCandidateId_IsNotFound(string candidateId)
    {
        ArtistCandidatePreview preview = await _service.GetPreviewAsync(_artistId, candidateId, 300);

        preview.Status.Should().Be(ArtistLookupStatus.CandidateNotFound);
        _commons.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task TakeOverAsync_ExpiredCandidate_IsNotFound()
    {
        string id = await FirstCandidateIdAsync();
        _clock.Advance(_options.LookupCacheDuration);

        ArtistLookupStatus status = await _service.TakeOverAsync(_artistId, id, null);

        status.Should().Be(ArtistLookupStatus.CandidateNotFound);
    }

    [Test]
    public async Task TakeOverAsync_WikimediaCandidate_StoresTheFullPictureWithItsCreditAndRemembersTheArtist()
    {
        GivenImage(
            IWikimediaCommonsClient.PreviewWidth,
            _preview,
            artist: "<i>Jane Doe</i>",
            license: "CC BY 4.0",
            licenseUrl: "https://creativecommons.org/licenses/by/4.0",
            description: "https://commons.wikimedia.org/wiki/File:Band.jpg");
        var replaced = Guid.NewGuid();
        _repository.ReplaceAsync(Arg.Any<ArtistPicture>(), Arg.Any<CancellationToken>())
            .Returns(new ArtistPictureReplacement(true, [replaced]));
        string id = await FirstCandidateIdAsync();
        CoverCrop crop = new(0.1, 0.2, 0.5);

        ArtistLookupStatus status = await _service.TakeOverAsync(_artistId, id, crop);

        status.Should().Be(ArtistLookupStatus.Found);
        await _commons.Received(1).GetImagesAsync(
            Arg.Any<IReadOnlyList<string>>(),
            IWikimediaCommonsClient.FullWidth,
            Arg.Any<CancellationToken>());
        await _commons.Received(1).DownloadAsync(_full, Arg.Any<CancellationToken>());
        await _store.Received(1).SaveAsync(Arg.Any<Guid>(), Arg.Any<Stream>(), crop, Arg.Any<CancellationToken>());
        await _repository.Received(1).ReplaceAsync(
            Arg.Is<ArtistPicture>(picture =>
                picture.ArtistId == _artistId
                && picture.Kind == ArtistPictureKind.Online
                && picture.Provider == ArtistPictureProvider.Wikimedia
                && picture.MusicBrainzArtistId == _musicBrainzId
                && picture.Author == "Jane Doe"
                && picture.License == "CC BY 4.0"
                && picture.LicenseUrl == "https://creativecommons.org/licenses/by/4.0"
                && picture.SourceUrl == "https://commons.wikimedia.org/wiki/File:Band.jpg"
                && picture.Width == 1000
                && picture.Height == 1000),
            Arg.Any<CancellationToken>());
        _store.Received(1).Delete(replaced);
        await _repository.Received(1).RememberMusicBrainzArtistAsync(
            _artistId,
            _musicBrainzId,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TakeOverAsync_FanartTvCandidate_DownloadsThePictureItself()
    {
        GivenSettings(enabled: true, ApiKey);
        ArtistCandidatesResult candidates = await _service.FindCandidatesAsync(_artistId, _musicBrainzId);

        ArtistLookupStatus status = await _service.TakeOverAsync(_artistId, candidates.Candidates[1].Id, null);

        status.Should().Be(ArtistLookupStatus.Found);
        await _fanartTv.Received(1).DownloadAsync(_fanart, Arg.Any<CancellationToken>());
        await _repository.Received(1).ReplaceAsync(
            Arg.Is<ArtistPicture>(picture =>
                picture.Provider == ArtistPictureProvider.FanartTv
                && picture.SourceUrl == $"https://fanart.tv/artist/{_musicBrainzId:D}"),
            Arg.Any<CancellationToken>());
    }

    [TestCase(ArtistPictureSaveStatus.UnsupportedFormat, ArtistLookupStatus.Unavailable)]
    [TestCase(ArtistPictureSaveStatus.Unreadable, ArtistLookupStatus.Unavailable)]
    [TestCase(ArtistPictureSaveStatus.Unavailable, ArtistLookupStatus.ProcessingUnavailable)]
    public async Task TakeOverAsync_PictureNotStored_ChangesNothing(
        ArtistPictureSaveStatus saveStatus,
        ArtistLookupStatus expected)
    {
        string id = await FirstCandidateIdAsync();
        _store.SaveAsync(Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<CoverCrop?>(), Arg.Any<CancellationToken>())
            .Returns(ArtistPictureSaveResult.Failed(saveStatus));

        ArtistLookupStatus status = await _service.TakeOverAsync(_artistId, id, null);

        status.Should().Be(expected);
        await _repository.DidNotReceiveWithAnyArgs().ReplaceAsync(default!, default);
        await _repository.DidNotReceiveWithAnyArgs().RememberMusicBrainzArtistAsync(default, default, default);
    }

    [Test]
    public async Task TakeOverAsync_ArtistRemovedMeanwhile_RemovesTheNewFiles()
    {
        string id = await FirstCandidateIdAsync();
        _repository.ReplaceAsync(Arg.Any<ArtistPicture>(), Arg.Any<CancellationToken>())
            .Returns(new ArtistPictureReplacement(false, []));

        ArtistLookupStatus status = await _service.TakeOverAsync(_artistId, id, null);

        status.Should().Be(ArtistLookupStatus.ArtistNotFound);
        _store.ReceivedWithAnyArgs(1).Delete(default);
        await _repository.DidNotReceiveWithAnyArgs().RememberMusicBrainzArtistAsync(default, default, default);
    }

    [Test]
    public async Task TakeOverAsync_SwitchedOffAfterTheLookup_DownloadsNothing()
    {
        string id = await FirstCandidateIdAsync();
        GivenSettings(enabled: false);

        ArtistLookupStatus status = await _service.TakeOverAsync(_artistId, id, null);

        status.Should().Be(ArtistLookupStatus.Disabled);
        await _commons.DidNotReceiveWithAnyArgs().DownloadAsync(default!, default);
    }

    [Test]
    public async Task FindCandidatesAsync_CacheNotWritable_IsProcessingUnavailable()
    {
        _cacheStore.IsAvailable = false;

        ArtistCandidatesResult result = await _service.FindCandidatesAsync(_artistId, _musicBrainzId);

        result.Status.Should().Be(ArtistLookupStatus.ProcessingUnavailable);
        _musicBrainz.ReceivedCalls().Should().BeEmpty();
    }

    private async Task<string> FirstCandidateIdAsync()
    {
        ArtistCandidatesResult result = await _service.FindCandidatesAsync(_artistId, _musicBrainzId);
        result.Status.Should().Be(ArtistLookupStatus.Found);
        return result.Candidates[0].Id;
    }

    private void GivenSettings(bool enabled, string? apiKey = null)
    {
        _settings.Current.Returns(new ArtistPictureSettings { OnlineLookupEnabled = enabled, FanartTvApiKey = apiKey });
    }

    private void GivenImage(
        int width,
        Uri thumbnail,
        string? artist = null,
        string? license = null,
        string? licenseUrl = null,
        string? description = null)
    {
        _commons.GetImagesAsync(Arg.Any<IReadOnlyList<string>>(), width, Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<IReadOnlyList<WikimediaImage>>.Found(
            [
                new WikimediaImage(
                    "Band.jpg",
                    thumbnail,
                    description is null ? null : new Uri(description),
                    2000,
                    1500,
                    "image/jpeg",
                    artist,
                    null,
                    license,
                    licenseUrl,
                    true),
            ]));
    }
}
