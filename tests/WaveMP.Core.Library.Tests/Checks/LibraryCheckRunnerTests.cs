using System.Reflection;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quartz;
using WaveMP.Core.Library.Checks;
using WaveMP.Core.Library.Options;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Tests.Checks;

[TestFixture]
public class LibraryCheckRunnerTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);

    [Test]
    public void LibraryCheckJob_Always_DisallowsConcurrentExecution()
    {
        typeof(LibraryCheckJob).GetCustomAttribute<DisallowConcurrentExecutionAttribute>().Should().NotBeNull();
    }

    [Test]
    public async Task RunAsync_Library_StoresTheProblemsItFoundAndCountsThem()
    {
        var artist = Guid.NewGuid();
        LibraryCheckTrackRow complete = new(
            Guid.NewGuid(), "Complete", artist, Guid.NewGuid(), TimeSpan.FromSeconds(100), true, true, true, []);
        LibraryCheckTrackRow bare = new(
            Guid.NewGuid(), "Bare", null, null, null, true, false, false, [MetadataTag.Album]);
        LibraryCheckFileRow broken = new(Guid.NewGuid(), null, MediaFileIssue.Unreadable);
        ILibraryIssueRepository repository = Substitute.For<ILibraryIssueRepository>();
        repository.ListTracksAsync(Arg.Any<CancellationToken>()).Returns([complete, bare]);
        repository.ListUnreadableFilesAsync(Arg.Any<CancellationToken>()).Returns([broken]);
        IReadOnlyCollection<DetectedLibraryIssue>? stored = null;
        repository.ReplaceAsync(
                Arg.Do<IReadOnlyCollection<DetectedLibraryIssue>>(issues => stored = issues),
                Arg.Any<int>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(new LibraryIssueChanges(3, 1, 2));
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(_now);
        LibraryCheckRunner runner = new(
            repository,
            Microsoft.Extensions.Options.Options.Create(new LibraryCheckOptions()),
            clock,
            NullLogger<LibraryCheckRunner>.Instance);

        LibraryCheckJobResult result = await runner.RunAsync();

        result.Should().Be(new LibraryCheckJobResult(2, 4, 1, 3, 1));
        await repository.Received(1).ReplaceAsync(
            Arg.Any<IReadOnlyCollection<DetectedLibraryIssue>>(),
            2,
            _now,
            Arg.Any<CancellationToken>());
        stored!.Select(issue => (issue.Type, issue.TrackId)).Should().BeEquivalentTo(
        [
            (LibraryIssueType.MissingTags, (Guid?)bare.TrackId),
            (LibraryIssueType.UnknownAlbum, (Guid?)bare.TrackId),
            (LibraryIssueType.MissingCover, (Guid?)bare.TrackId),
            (LibraryIssueType.AudioWithoutVideo, (Guid?)bare.TrackId),
            (LibraryIssueType.UnreadableFile, (Guid?)null),
        ]);
    }
}
