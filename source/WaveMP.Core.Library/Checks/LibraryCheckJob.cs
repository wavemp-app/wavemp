using Quartz;

namespace WaveMP.Core.Library.Checks;

/// <summary>
/// The library check as background job: started after every scan and by an admin; it has no schedule of its own.
/// Never runs in parallel to itself; the result becomes part of the run history.
/// </summary>
[DisallowConcurrentExecution]
public sealed class LibraryCheckJob(ILibraryCheckRunner runner) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        context.Result = await runner.RunAsync(cancellationToken);
    }
}
