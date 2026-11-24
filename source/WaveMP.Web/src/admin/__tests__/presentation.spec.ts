import { describe, expect, it } from 'vitest'
import {
    activityOf,
    backupRetentionOptions,
    backupTriggerPresentation,
    cacheLimitOptions,
    dailyScheduleKey,
    durationSeconds,
    errorLabelKey,
    formatList,
    intervalOptions,
    isActive,
    isTimeOfDay,
    issueLabelKey,
    jobNameKey,
    jobScheduleKey,
    mediaSourceStates,
    parseList,
    phaseLabelKey,
    progressRatio,
    runDateTimeFormat,
    statusPresentation,
    triggerLabelKey,
    usageRatio,
} from '../presentation'
import { formatDate } from '@/i18n/format'

describe('presentation of the administration', () => {
    it('offers intervals the server accepts, in ascending order', () => {
        const minutes = intervalOptions.map((option) => option.minutes)

        expect(minutes).toEqual([...minutes].sort((a, b) => a - b))
        expect(minutes.every((value) => value >= 15 && value <= 1440)).toBe(true)
        expect(minutes).toContain(360)
    })

    it('parses and formats the lists of the settings', () => {
        expect(parseList(' mp3, .FLAC ;ogg\n\n ,')).toEqual(['mp3', '.FLAC', 'ogg'])
        expect(parseList('')).toEqual([])
        expect(formatList(['mp3', 'flac'])).toBe('mp3, flac')
        expect(formatList(null)).toBe('')
    })

    it('computes the progress of a scan', () => {
        expect(progressRatio(null)).toBeNull()
        expect(progressRatio({ phase: 'Discovering', filesFound: 10 })).toBeNull()
        expect(progressRatio({ phase: 'Processing', filesToRead: 40, filesRead: 10 })).toBe(0.25)
        expect(
            progressRatio({
                phase: 'Processing',
                filesToRead: 0,
                directoriesToProcess: 4,
                directoriesProcessed: 3,
            }),
        ).toBe(0.75)
        expect(progressRatio({ phase: 'Processing', filesToRead: 2, filesRead: 5 })).toBe(1)
        expect(progressRatio({ phase: 'CleaningUp' })).toBe(1)
    })

    it('maps the status of runs to activities, labels and badge tokens', () => {
        expect(activityOf('Queued')).toBe('queued')
        expect(activityOf('Running')).toBe('running')
        expect(activityOf('Succeeded')).toBe('idle')
        expect(activityOf(null)).toBe('idle')
        expect(isActive('Running')).toBe(true)
        expect(isActive('Failed')).toBe(false)
        expect(statusPresentation('Interrupted')).toEqual({
            labelKey: 'jobRuns.statuses.interrupted',
            token: 'interrupted',
        })
    })

    it('translates the codes of the API', () => {
        expect(triggerLabelKey('FileWatcher')).toBe('jobRuns.triggers.fileWatcher')
        expect(errorLabelKey('scan.media_folder_empty')).toBe('jobRuns.errors.mediaFolderEmpty')
        expect(errorLabelKey('something.new')).toBe('jobRuns.errors.jobFailed')
        expect(errorLabelKey(null)).toBeNull()
        expect(issueLabelKey('file.path_too_long')).toBe('adminLibrary.issues.pathTooLong')
        expect(phaseLabelKey('CleaningUp')).toBe('adminLibrary.phases.cleaningUp')
        expect(jobNameKey('library-scan')).toBe('adminJobs.names.libraryScan')
        expect(jobNameKey('unknown')).toBeNull()
    })

    it('translates the codes of the media folders', () => {
        expect(errorLabelKey('scan.no_media_sources')).toBe('jobRuns.errors.noMediaSources')
        expect(issueLabelKey('source.not_found')).toBe('adminLibrary.issues.sourceNotFound')
        expect(issueLabelKey('source.unreadable')).toBe('adminLibrary.issues.sourceUnreadable')
        expect(issueLabelKey('source.empty')).toBe('adminLibrary.issues.sourceEmpty')
    })

    it('describes the state of a media folder with tokens', () => {
        expect(
            mediaSourceStates({
                isActive: true,
                isAvailable: true,
                isWatched: true,
                followsMediaPath: true,
            }),
        ).toEqual(['active', 'watched', 'media-path'])
        expect(mediaSourceStates({ isActive: false, isAvailable: false })).toEqual([
            'inactive',
            'unavailable',
        ])
    })

    it('translates the codes of the cache', () => {
        expect(triggerLabelKey('LibraryScan')).toBe('jobRuns.triggers.libraryScan')
        expect(triggerLabelKey('CoverRule')).toBe('jobRuns.triggers.coverRule')
        expect(errorLabelKey('cache.ffmpeg_unavailable')).toBe('jobRuns.errors.ffmpegUnavailable')
        expect(errorLabelKey('cache.folder_unwritable')).toBe(
            'jobRuns.errors.cacheFolderUnwritable',
        )
        expect(jobNameKey('cover-cache')).toBe('adminJobs.names.coverCache')
        expect(jobScheduleKey('cover-cache')).toBe('adminJobs.afterScan')
        expect(jobNameKey('library-check')).toBe('adminJobs.names.libraryCheck')
        expect(jobScheduleKey('library-check')).toBe('adminJobs.afterScan')
        expect(jobNameKey('library-organize')).toBe('adminJobs.names.libraryOrganize')
        expect(jobScheduleKey('library-organize')).toBe('adminJobs.whenOrganizing')
        expect(jobScheduleKey('library-scan')).toBe('adminJobs.manualOnly')
        expect(jobScheduleKey(null)).toBe('adminJobs.manualOnly')
    })

    it('translates the codes of the database backups', () => {
        expect(jobNameKey('database-backup')).toBe('adminJobs.names.databaseBackup')
        expect(errorLabelKey('backup.folder_unwritable')).toBe(
            'jobRuns.errors.backupFolderUnwritable',
        )
        expect(errorLabelKey('backup.pg_dump_unavailable')).toBe('jobRuns.errors.pgDumpUnavailable')
        expect(errorLabelKey('backup.failed')).toBe('jobRuns.errors.backupFailed')
        expect(backupTriggerPresentation('Migration')).toEqual({
            labelKey: 'adminBackups.triggers.migration',
            token: 'migration',
        })
        expect(backupTriggerPresentation('Schedule').token).toBe('schedule')
        expect(backupTriggerPresentation(undefined).token).toBe('manual')
    })

    it('offers numbers of kept exports the server accepts, in ascending order', () => {
        expect(backupRetentionOptions).toEqual([...backupRetentionOptions].sort((a, b) => a - b))
        expect(backupRetentionOptions.every((value) => value >= 1 && value <= 100)).toBe(true)
        expect(backupRetentionOptions).toContain(7)
    })

    it('accepts times of day as the server expects them', () => {
        expect(isTimeOfDay('03:00')).toBe(true)
        expect(isTimeOfDay('23:59')).toBe(true)
        expect(isTimeOfDay('24:00')).toBe(false)
        expect(isTimeOfDay('3:00')).toBe(false)
        expect(isTimeOfDay('03:00:00')).toBe(false)
        expect(isTimeOfDay('')).toBe(false)
    })

    it('names the time zone of the server next to the time of a daily schedule', () => {
        expect(dailyScheduleKey('adminBackups', 'Europe/Berlin')).toBe('adminBackups.dailyAtZone')
        expect(dailyScheduleKey('adminJobs', 'UTC')).toBe('adminJobs.dailyAtZone')
        expect(dailyScheduleKey('adminBackups', null)).toBe('adminBackups.dailyAt')
        expect(dailyScheduleKey('adminJobs', undefined)).toBe('adminJobs.dailyAt')
        expect(dailyScheduleKey('adminJobs', '')).toBe('adminJobs.dailyAt')
    })

    it('names the time zone of the browser in the date of a run', () => {
        // 03:00 UTC, the default schedule of a server with TZ=UTC, seen in a browser in Berlin.
        const nextRun = new Date('2026-11-17T03:00:00Z')
        const berlin = { ...runDateTimeFormat, timeZone: 'Europe/Berlin' }

        expect(formatDate(nextRun, 'en', berlin)).toBe('Nov 17, 2026, 4:00 AM GMT+1')
        expect(formatDate(nextRun, 'de', berlin)).toBe('17. Nov. 2026, 04:00 MEZ')
        expect(formatDate(nextRun, 'en', { ...runDateTimeFormat, timeZone: 'UTC' })).toBe(
            'Nov 17, 2026, 3:00 AM UTC',
        )
    })

    it('offers size limits of the cache the server accepts, in ascending order', () => {
        expect(cacheLimitOptions).toEqual([...cacheLimitOptions].sort((a, b) => a - b))
        expect(cacheLimitOptions.every((value) => value >= 100 && value <= 1_000_000)).toBe(true)
        expect(cacheLimitOptions).toContain(2000)
    })

    it('computes the share of the limit the cache uses', () => {
        expect(usageRatio(500, 2000)).toBe(0.25)
        expect(usageRatio(3000, 2000)).toBe(1)
        expect(usageRatio(0, 2000)).toBe(0)
        expect(usageRatio(500, 0)).toBe(0)
        expect(usageRatio(null, undefined)).toBe(0)
    })

    it('converts durations to seconds', () => {
        expect(durationSeconds(83_000)).toBe(83)
        expect(durationSeconds(null)).toBeNull()
        expect(durationSeconds(undefined)).toBeNull()
    })
})
