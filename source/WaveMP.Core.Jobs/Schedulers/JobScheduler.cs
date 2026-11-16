using System.Globalization;
using Microsoft.Extensions.Logging;
using Quartz;
using WaveMP.Core.Jobs.Definitions;

namespace WaveMP.Core.Jobs.Schedulers;

internal sealed class JobScheduler(
    ISchedulerFactory schedulerFactory,
    IEnumerable<JobRegistration> registrations,
    IEnumerable<IJobScheduleProvider> scheduleProviders,
    TimeProvider timeProvider,
    ILogger<JobScheduler> logger) : IJobScheduler
{
    /// <summary>
    /// How late a scheduled run may start before it counts as missed and is skipped. Quartz' default of one minute
    /// would start a scheduled run right after a manual or file watcher run that held the job for less than that.
    /// </summary>
    internal static readonly TimeSpan ScheduledMisfireThreshold = TimeSpan.FromSeconds(1);

    private readonly JobRegistration[] _registrations = [.. registrations];
    private readonly IJobScheduleProvider[] _scheduleProviders = [.. scheduleProviders];

    public async Task RegisterAndScheduleAllAsync(CancellationToken cancellationToken = default)
    {
        IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        foreach (JobRegistration registration in _registrations)
        {
            IJobDetail job = JobBuilder.Create()
                .OfType(registration.JobType)
                .WithIdentity(JobKeyOf(registration.JobId))
                .StoreDurably(true)
                .Build();
            await scheduler.AddJob(job, AddJobOptions.Replacing, cancellationToken);
        }

        foreach (IJobScheduleProvider provider in _scheduleProviders)
        {
            await ScheduleAsync(scheduler, provider, JobScheduleReason.Startup, cancellationToken);
        }
    }

    public async Task RescheduleAsync(string jobId, CancellationToken cancellationToken = default)
    {
        if (FindScheduleProvider(jobId) is not { } provider)
        {
            return;
        }

        IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        await ScheduleAsync(scheduler, provider, JobScheduleReason.SettingsChanged, cancellationToken);
    }

    public async Task<DateTimeOffset?> GetNextRunAsync(string jobId, CancellationToken cancellationToken = default)
    {
        IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        ITrigger? trigger = await scheduler.GetTrigger(TriggerKeyOf(jobId), cancellationToken);
        return trigger?.NextFireTimeUtc;
    }

    public async Task TriggerAsync(
        string jobId,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default)
    {
        IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        JobDataMap map = [];
        foreach ((string key, string value) in data)
        {
            map[key] = value;
        }

        await scheduler.TriggerJob(JobKeyOf(jobId), map, cancellationToken);
    }

    public IJobScheduleProvider? FindScheduleProvider(string jobId)
    {
        return _scheduleProviders.FirstOrDefault(provider => provider.JobId == jobId);
    }

    internal static JobKey JobKeyOf(string jobId)
    {
        return new JobKey(jobId);
    }

    internal static TriggerKey TriggerKeyOf(string jobId)
    {
        return new TriggerKey(jobId + ".schedule");
    }

    /// <summary>
    /// The Quartz cron expression of every day at the hour and minute of <paramref name="time"/>, e.g.
    /// <c>0 30 3 * * ?</c> for 03:30.
    /// </summary>
    internal static string DailyCronExpression(TimeOnly time)
    {
        return string.Create(CultureInfo.InvariantCulture, $"0 {time.Minute} {time.Hour} * * ?");
    }

    private async Task ScheduleAsync(
        IScheduler scheduler,
        IJobScheduleProvider provider,
        JobScheduleReason reason,
        CancellationToken cancellationToken)
    {
        TriggerKey key = TriggerKeyOf(provider.JobId);
        if (provider.GetSchedule(reason) is not { } schedule)
        {
            await scheduler.UnscheduleJob(key, cancellationToken);
            return;
        }

        // A run that was missed, e.g. while a manual run of the same job was running, is skipped instead of being
        // made up right after: once the job is free again more than the threshold after the scheduled time, the
        // trigger moves on to its next scheduled time.
        TriggerBuilder<IJob> builder = TriggerBuilder.Create(timeProvider)
            .WithIdentity(key)
            .ForJob(JobKeyOf(provider.JobId))
            .WithMisfireThreshold(ScheduledMisfireThreshold);
        if (schedule.DailyAt is { } time)
        {
            TimeZoneInfo timeZone = schedule.TimeZone ?? timeProvider.LocalTimeZone;
            builder = builder
                .StartAt(timeProvider.GetUtcNow())
                .WithSchedule(CronScheduleBuilder.Create(DailyCronExpression(time))
                    .InTimeZone(timeZone)
                    .WithMisfireInstruction(CronTriggerMisfireInstruction.DoNothing));
        }
        else
        {
            builder = builder
                .StartAt(timeProvider.GetUtcNow() + schedule.FirstRunDelay)
                .WithSchedule(SimpleScheduleBuilder.Create()
                    .WithInterval(schedule.Interval)
                    .RepeatForever()
                    .WithMisfireInstruction(SimpleTriggerMisfireInstruction.NextWithRemainingCount));
        }

        ITrigger trigger = builder.Build();
        DateTimeOffset nextRun = await scheduler.ScheduleJob(trigger, ScheduleJobOptions.Replacing, cancellationToken);
        if (schedule.DailyAt is { } dailyAt)
        {
            logger.LogInformation(
                "Scheduled job {JobId} daily at {Time}, next run at {NextRun}",
                provider.JobId,
                dailyAt,
                nextRun);
        }
        else
        {
            logger.LogInformation(
                "Scheduled job {JobId} every {Interval}, next run at {NextRun}",
                provider.JobId,
                schedule.Interval,
                nextRun);
        }
    }
}
