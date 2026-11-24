using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quartz;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Runs;
using WaveMP.Core.Organizing.Jobs;
using WaveMP.Core.Organizing.Operations;
using WaveMP.Core.Organizing.Previews;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Organizing.Tests.Jobs;

[TestFixture]
public sealed class LibraryOrganizeJobTests
{
    private IOrganizeRepository _repository = null!;
    private IOrganizePreviewService _previews = null!;
    private IOrganizeExecutor _executor = null!;
    private IJobRunTracker _tracker = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IOrganizeRepository>();
        _previews = Substitute.For<IOrganizePreviewService>();
        _executor = Substitute.For<IOrganizeExecutor>();
        _tracker = Substitute.For<IJobRunTracker>();
    }

    [Test]
    public void The_payload_names_the_operation()
    {
        var id = Guid.NewGuid();
        JobDataMap data = new() { [JobDataKeys.Payload] = LibraryOrganizeJob.CreatePayload(id) };

        LibraryOrganizeJob.ReadOperationId(data).Should().Be(id);
        LibraryOrganizeJob.ReadOperationId([]).Should().BeNull();
        LibraryOrganizeJob.ReadOperationId(new JobDataMap { [JobDataKeys.Payload] = "{ broken" }).Should().BeNull();
    }

    [Test]
    public async Task A_queued_operation_is_run_with_the_id_of_the_job_run()
    {
        var id = Guid.NewGuid();
        var runId = Guid.NewGuid();
        _tracker.FindRunId("fire").Returns(runId);
        _repository.FindOperationAsync(id, Arg.Any<CancellationToken>())
            .Returns(Operation(id, OrganizeOperationStatus.Queued));
        _executor.RunAsync(id, runId, Arg.Any<CancellationToken>())
            .Returns(new OrganizeRunResult(id, OrganizeOperationStatus.Completed, 3, 1, 0));
        IJobExecutionContext context = Context(LibraryOrganizeJob.CreatePayload(id));

        await Job().Execute(context, CancellationToken.None);

        context.Result.Should().Be(new LibraryOrganizeResult(id, OrganizeOperationStatus.Completed, 0, 3, 1, 0));
        await _previews.DidNotReceiveWithAnyArgs().PlanLibraryAsync(default);
    }

    [Test]
    public async Task An_operation_that_is_planning_is_planned()
    {
        var id = Guid.NewGuid();
        _repository.FindOperationAsync(id, Arg.Any<CancellationToken>()).Returns(
            Operation(id, OrganizeOperationStatus.Planning),
            Operation(id, OrganizeOperationStatus.Previewed, totalItems: 12));
        IJobExecutionContext context = Context(LibraryOrganizeJob.CreatePayload(id));

        await Job().Execute(context, CancellationToken.None);

        await _previews.Received(1).PlanLibraryAsync(id, Arg.Any<CancellationToken>());
        context.Result.Should().Be(new LibraryOrganizeResult(id, OrganizeOperationStatus.Previewed, 12, 0, 0, 0));
        await _executor.DidNotReceiveWithAnyArgs().RunAsync(default, default, default);
    }

    [Test]
    public async Task Without_a_payload_the_oldest_queued_operation_runs_and_without_one_nothing_happens()
    {
        var id = Guid.NewGuid();
        _repository.ListOperationIdsAsync(OrganizeOperationStatus.Queued, Arg.Any<CancellationToken>())
            .Returns(_ => [id], _ => []);
        _repository.FindOperationAsync(id, Arg.Any<CancellationToken>())
            .Returns(Operation(id, OrganizeOperationStatus.Queued));
        _executor.RunAsync(id, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new OrganizeRunResult(id, OrganizeOperationStatus.Completed, 1, 0, 0));

        await Job().Execute(Context(null), CancellationToken.None);
        IJobExecutionContext idle = Context(null);
        await Job().Execute(idle, CancellationToken.None);

        await _executor.Received(1).RunAsync(id, Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        idle.Result.Should().Be(new LibraryOrganizeResult(null, null, 0, 0, 0, 0));
    }

    private LibraryOrganizeJob Job()
    {
        return new LibraryOrganizeJob(
            _repository,
            _previews,
            _executor,
            _tracker,
            NullLogger<LibraryOrganizeJob>.Instance);
    }

    private static IJobExecutionContext Context(string? payload)
    {
        JobDataMap data = [];
        if (payload is not null)
        {
            data[JobDataKeys.Payload] = payload;
        }

        IJobExecutionContext context = Substitute.For<IJobExecutionContext>();
        context.MergedJobDataMap.Returns(data);
        context.FireInstanceId.Returns("fire");
        return context;
    }

    private static OrganizeOperation Operation(Guid id, OrganizeOperationStatus status, int totalItems = 0)
    {
        return new OrganizeOperation
        {
            Id = id,
            Kind = OrganizeOperationKind.Library,
            Status = status,
            Template = "{Title}",
            CreatedAt = DateTimeOffset.UnixEpoch,
            TotalItems = totalItems,
        };
    }
}
