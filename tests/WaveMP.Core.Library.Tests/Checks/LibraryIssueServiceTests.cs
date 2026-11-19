using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Library.Checks;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Tests.Checks;

[TestFixture]
public class LibraryIssueServiceTests
{
    private static readonly DateTimeOffset _checkedAt = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);

    private ILibraryIssueRepository _repository = null!;
    private LibraryIssueService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<ILibraryIssueRepository>();
        _repository.ListAsync(Arg.Any<LibraryIssueListQuery>(), Arg.Any<CancellationToken>())
            .Returns(new Page<LibraryIssueRow>([], 0, 10, 0));
        _service = new LibraryIssueService(_repository);
    }

    [Test]
    public async Task ListAsync_WithoutOptions_ListsAllProblemsByPath()
    {
        await _service.ListAsync(new LibraryIssueListRequest(20, 10, Search: "  "));

        await _repository.Received(1).ListAsync(
            Arg.Is<LibraryIssueListQuery>(query => query.Offset == 20
                && query.Limit == 10
                && query.Sort == LibraryIssueSortField.Path
                && query.Direction == SortDirection.Asc
                && query.Types == null
                && query.Search == null),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListAsync_SortByTimeFound_ListsTheNewestFirst()
    {
        await _service.ListAsync(new LibraryIssueListRequest(0, 10, LibraryIssueSortField.DetectedAt, Search: " x "));

        await _repository.Received(1).ListAsync(
            Arg.Is<LibraryIssueListQuery>(query => query.Direction == SortDirection.Desc && query.Search == "x"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListAsync_TypesAndSeverity_ListsTheKindsOfBoth()
    {
        await _service.ListAsync(new LibraryIssueListRequest(
            0,
            10,
            Types: [LibraryIssueType.AudioWithoutVideo, LibraryIssueType.MissingCover],
            Severity: LibraryIssueSeverity.Hint));

        await _repository.Received(1).ListAsync(
            Arg.Is<LibraryIssueListQuery>(query =>
                query.Types!.SequenceEqual(new[] { LibraryIssueType.AudioWithoutVideo })),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListAsync_SeverityOnly_ListsItsKinds()
    {
        await _service.ListAsync(new LibraryIssueListRequest(0, 10, Severity: LibraryIssueSeverity.Problem));

        await _repository.Received(1).ListAsync(
            Arg.Is<LibraryIssueListQuery>(query => query.Types!.SequenceEqual(new[]
            {
                LibraryIssueType.UnreadableFile,
                LibraryIssueType.MissingTags,
                LibraryIssueType.UnknownAlbum,
                LibraryIssueType.MissingCover,
                LibraryIssueType.PossibleDuplicate,
            })),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListAsync_TypesWithoutTheSeverity_IsEmptyWithoutAskingTheDatabase()
    {
        Page<LibraryIssueRow> page = await _service.ListAsync(new LibraryIssueListRequest(
            5,
            10,
            Types: [LibraryIssueType.MissingCover],
            Severity: LibraryIssueSeverity.Hint));

        page.Should().BeEquivalentTo(new Page<LibraryIssueRow>([], 5, 10, 0));
        await _repository.DidNotReceive()
            .ListAsync(Arg.Any<LibraryIssueListQuery>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetSummaryAsync_Counts_NameEveryKindWithItsSeverityAndTheLastRun()
    {
        _repository.CountAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<LibraryIssueType, int>
        {
            [LibraryIssueType.MissingCover] = 3,
            [LibraryIssueType.AudioWithoutVideo] = 7,
        });
        _repository.FindLastCheckAsync(Arg.Any<CancellationToken>())
            .Returns(new LibraryCheck { Id = LibraryCheck.SingletonId, CheckedAt = _checkedAt, TrackCount = 12 });

        LibraryIssueSummary summary = await _service.GetSummaryAsync();

        summary.CheckedAt.Should().Be(_checkedAt);
        summary.TrackCount.Should().Be(12);
        summary.Counts.Should().Equal(
            new LibraryIssueCount(LibraryIssueType.UnreadableFile, LibraryIssueSeverity.Problem, 0),
            new LibraryIssueCount(LibraryIssueType.MissingTags, LibraryIssueSeverity.Problem, 0),
            new LibraryIssueCount(LibraryIssueType.UnknownAlbum, LibraryIssueSeverity.Problem, 0),
            new LibraryIssueCount(LibraryIssueType.MissingCover, LibraryIssueSeverity.Problem, 3),
            new LibraryIssueCount(LibraryIssueType.PossibleDuplicate, LibraryIssueSeverity.Problem, 0),
            new LibraryIssueCount(LibraryIssueType.VideoWithoutAudio, LibraryIssueSeverity.Hint, 0),
            new LibraryIssueCount(LibraryIssueType.AudioWithoutVideo, LibraryIssueSeverity.Hint, 7));
    }

    [Test]
    public async Task GetSummaryAsync_NeverChecked_HasNoTimeOfTheLastRun()
    {
        _repository.CountAsync(Arg.Any<CancellationToken>()).Returns(new Dictionary<LibraryIssueType, int>());

        LibraryIssueSummary summary = await _service.GetSummaryAsync();

        summary.CheckedAt.Should().BeNull();
        summary.TrackCount.Should().BeNull();
        summary.Counts.Should().AllSatisfy(count => count.Count.Should().Be(0));
    }
}
