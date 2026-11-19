// Presentation of the administration: message keys of the codes of the API, the tokens of badges and
// states, the intervals of the library scan, the lists of the settings form and the size limits of
// the cache, and the exports of the database. Pure functions only.

import type { ScanProgress } from '@/api/generated/models'

/** Activity of a scan or job as data-state token. */
export type Activity = 'idle' | 'queued' | 'running'

export interface IntervalOption {
    readonly minutes: number
    readonly labelKey: string
}

/** Intervals offered for the periodic scan; the server accepts 15 to 1440 minutes. */
export const intervalOptions: readonly IntervalOption[] = [
    { minutes: 15, labelKey: 'adminLibrary.intervals.every15Minutes' },
    { minutes: 30, labelKey: 'adminLibrary.intervals.every30Minutes' },
    { minutes: 60, labelKey: 'adminLibrary.intervals.everyHour' },
    { minutes: 180, labelKey: 'adminLibrary.intervals.every3Hours' },
    { minutes: 360, labelKey: 'adminLibrary.intervals.every6Hours' },
    { minutes: 720, labelKey: 'adminLibrary.intervals.every12Hours' },
    { minutes: 1440, labelKey: 'adminLibrary.intervals.everyDay' },
]

/** Message key of an interval that is not offered, with the placeholder minutes. */
export const customIntervalKey = 'adminLibrary.intervals.custom'

/**
 * Size limits offered for the cache in megabytes (1 MB = 1,000,000 bytes, like the shown sizes);
 * the server accepts 100 to 1,000,000.
 */
export const cacheLimitOptions: readonly number[] = [
    500, 1000, 2000, 5000, 10_000, 20_000, 50_000, 100_000,
]

/** Bytes of a megabyte of the cache settings. */
export const bytesPerMegabyte = 1_000_000

/** Share of the limit the cache uses, between 0 and 1; 0 without a limit. */
export function usageRatio(
    sizeBytes: number | null | undefined,
    maxSizeBytes: number | null | undefined,
): number {
    if (!sizeBytes || !maxSizeBytes || maxSizeBytes <= 0) {
        return 0
    }

    return clamp(sizeBytes / maxSizeBytes)
}

/** Splits a list typed by the user at commas, semicolons and line breaks; empty entries are dropped. */
export function parseList(text: string): string[] {
    return text
        .split(/[,;\n]/)
        .map((entry) => entry.trim())
        .filter((entry) => entry.length > 0)
}

/** Shows a list of the settings as text for an input. */
export function formatList(values: readonly string[] | null | undefined): string {
    return (values ?? []).join(', ')
}

/**
 * Share of the work a running scan has done, between 0 and 1, or null while it cannot be known
 * (the scan still looks for files).
 */
export function progressRatio(progress: ScanProgress | null | undefined): number | null {
    if (!progress || progress.phase === 'Discovering' || !progress.phase) {
        return null
    }

    if (progress.phase === 'CleaningUp') {
        return 1
    }

    const filesToRead = progress.filesToRead ?? 0
    if (filesToRead > 0) {
        return clamp((progress.filesRead ?? 0) / filesToRead)
    }

    const directoriesToProcess = progress.directoriesToProcess ?? 0
    return directoriesToProcess > 0
        ? clamp((progress.directoriesProcessed ?? 0) / directoriesToProcess)
        : 1
}

function clamp(value: number): number {
    return Math.min(1, Math.max(0, value))
}

/** The activity of a run by its status. */
export function activityOf(status: string | null | undefined): Activity {
    switch (status) {
        case 'Queued':
            return 'queued'
        case 'Running':
            return 'running'
        default:
            return 'idle'
    }
}

/** Whether a run is still queued or running. */
export function isActive(status: string | null | undefined): boolean {
    return activityOf(status) !== 'idle'
}

interface Labeled {
    readonly labelKey: string
}

interface StatusPresentation extends Labeled {
    /** data-state token of the badge. */
    readonly token: string
}

const statuses: Record<string, StatusPresentation> = {
    Queued: { labelKey: 'jobRuns.statuses.queued', token: 'queued' },
    Running: { labelKey: 'jobRuns.statuses.running', token: 'running' },
    Succeeded: { labelKey: 'jobRuns.statuses.succeeded', token: 'succeeded' },
    Failed: { labelKey: 'jobRuns.statuses.failed', token: 'failed' },
    Canceled: { labelKey: 'jobRuns.statuses.canceled', token: 'canceled' },
    Interrupted: { labelKey: 'jobRuns.statuses.interrupted', token: 'interrupted' },
}

/** Label and badge token of the status of a run. */
export function statusPresentation(status: string | null | undefined): StatusPresentation {
    return statuses[status ?? ''] ?? { labelKey: 'jobRuns.statuses.queued', token: 'queued' }
}

const triggers: Record<string, Labeled> = {
    Schedule: { labelKey: 'jobRuns.triggers.schedule' },
    Manual: { labelKey: 'jobRuns.triggers.manual' },
    FileWatcher: { labelKey: 'jobRuns.triggers.fileWatcher' },
    LibraryScan: { labelKey: 'jobRuns.triggers.libraryScan' },
    CoverRule: { labelKey: 'jobRuns.triggers.coverRule' },
    Settings: { labelKey: 'jobRuns.triggers.settings' },
}

/** Message key of what started a run. */
export function triggerLabelKey(trigger: string | null | undefined): string {
    return (triggers[trigger ?? ''] ?? { labelKey: 'jobRuns.triggers.manual' }).labelKey
}

const errors: Record<string, Labeled> = {
    'job.failed': { labelKey: 'jobRuns.errors.jobFailed' },
    'job.interrupted': { labelKey: 'jobRuns.errors.jobInterrupted' },
    'scan.media_folder_not_found': { labelKey: 'jobRuns.errors.mediaFolderNotFound' },
    'scan.media_folder_unreadable': { labelKey: 'jobRuns.errors.mediaFolderUnreadable' },
    'scan.media_folder_empty': { labelKey: 'jobRuns.errors.mediaFolderEmpty' },
    'scan.probe_unavailable': { labelKey: 'jobRuns.errors.probeUnavailable' },
    'scan.no_media_sources': { labelKey: 'jobRuns.errors.noMediaSources' },
    'cache.ffmpeg_unavailable': { labelKey: 'jobRuns.errors.ffmpegUnavailable' },
    'cache.folder_unwritable': { labelKey: 'jobRuns.errors.cacheFolderUnwritable' },
    'backup.folder_unwritable': { labelKey: 'jobRuns.errors.backupFolderUnwritable' },
    'backup.pg_dump_unavailable': { labelKey: 'jobRuns.errors.pgDumpUnavailable' },
    'backup.failed': { labelKey: 'jobRuns.errors.backupFailed' },
    'lyrics.download_disabled': { labelKey: 'jobRuns.errors.lyricsDownloadDisabled' },
    'lyrics.service_unavailable': { labelKey: 'jobRuns.errors.lyricsServiceUnavailable' },
}

/** Message key of the error code of a run; unknown codes are shown as a general failure. */
export function errorLabelKey(code: string | null | undefined): string | null {
    if (!code || code.startsWith('openapi-json-null')) {
        return null
    }

    return (errors[code] ?? { labelKey: 'jobRuns.errors.jobFailed' }).labelKey
}

const issues: Record<string, Labeled> = {
    'file.unreadable': { labelKey: 'adminLibrary.issues.fileUnreadable' },
    'file.tags_unreadable': { labelKey: 'adminLibrary.issues.tagsUnreadable' },
    'file.probe_failed': { labelKey: 'adminLibrary.issues.probeFailed' },
    'file.path_too_long': { labelKey: 'adminLibrary.issues.pathTooLong' },
    'source.not_found': { labelKey: 'adminLibrary.issues.sourceNotFound' },
    'source.unreadable': { labelKey: 'adminLibrary.issues.sourceUnreadable' },
    'source.empty': { labelKey: 'adminLibrary.issues.sourceEmpty' },
}

/** Message key of the problem of a file or of a media folder the scan skipped. */
export function issueLabelKey(code: string | null | undefined): string {
    return (issues[code ?? ''] ?? { labelKey: 'adminLibrary.issues.fileUnreadable' }).labelKey
}

const phases: Record<string, Labeled> = {
    Discovering: { labelKey: 'adminLibrary.phases.discovering' },
    Processing: { labelKey: 'adminLibrary.phases.processing' },
    CleaningUp: { labelKey: 'adminLibrary.phases.cleaningUp' },
}

/** Message key of the phase of a running scan. */
export function phaseLabelKey(phase: string | null | undefined): string {
    return (phases[phase ?? ''] ?? { labelKey: 'adminLibrary.phases.discovering' }).labelKey
}

const scanStates: Record<Activity, Labeled> = {
    idle: { labelKey: 'adminLibrary.states.idle' },
    queued: { labelKey: 'adminLibrary.states.queued' },
    running: { labelKey: 'adminLibrary.states.running' },
}

/** Message key of the activity of the scan. */
export function scanStateLabelKey(activity: Activity): string {
    return scanStates[activity].labelKey
}

interface JobPresentation extends Labeled {
    /** Message key of when a job without schedule runs. */
    readonly scheduleKey: string
}

const jobNames: Record<string, JobPresentation> = {
    'library-scan': {
        labelKey: 'adminJobs.names.libraryScan',
        scheduleKey: 'adminJobs.manualOnly',
    },
    'cover-cache': { labelKey: 'adminJobs.names.coverCache', scheduleKey: 'adminJobs.afterScan' },
    'database-backup': {
        labelKey: 'adminJobs.names.databaseBackup',
        scheduleKey: 'adminJobs.manualOnly',
    },
    'lyrics-download': {
        labelKey: 'adminJobs.names.lyricsDownload',
        scheduleKey: 'adminJobs.afterScanWhenLyricsOn',
    },
}

/** Message key of the name of a job; unknown jobs are shown with their id. */
export function jobNameKey(id: string | null | undefined): string | null {
    return jobNames[id ?? '']?.labelKey ?? null
}

/** Message key of when a job without a schedule runs, e.g. after scans or only when started. */
export function jobScheduleKey(id: string | null | undefined): string {
    return jobNames[id ?? '']?.scheduleKey ?? 'adminJobs.manualOnly'
}

/**
 * Date format of the runs of a job: the moment in the time zone of the browser together with the
 * name of that zone, e.g. "Nov 17, 2026, 4:00 AM GMT+1". The zone is named because the time of
 * day of a daily schedule is in the time zone of the server, which may be another one.
 */
export const runDateTimeFormat: Intl.DateTimeFormatOptions = {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    timeZoneName: 'short',
}

/**
 * Message key of the time of day of a daily schedule ({time}, in the time zone of the server):
 * with the IANA id of that zone ({timeZone}) when the API names it, otherwise marked as the time
 * of the server.
 */
export function dailyScheduleKey(
    namespace: 'adminJobs' | 'adminBackups',
    timeZone: string | null | undefined,
): string {
    return timeZone ? `${namespace}.dailyAtZone` : `${namespace}.dailyAt`
}

/** Duration in seconds of a run that took the given milliseconds. */
export function durationSeconds(milliseconds: number | null | undefined): number | null {
    return milliseconds === null || milliseconds === undefined
        ? null
        : Math.max(0, milliseconds / 1000)
}

/** Numbers of kept exports of the database offered in the settings; the server accepts 1 to 100. */
export const backupRetentionOptions: readonly number[] = [1, 3, 5, 7, 14, 30, 60, 100]

interface BackupTriggerPresentation extends Labeled {
    /** data-kind of the export and data-state token of its badge. */
    readonly token: string
}

const manualBackup: BackupTriggerPresentation = {
    labelKey: 'adminBackups.triggers.manual',
    token: 'manual',
}

const backupTriggers: Record<string, BackupTriggerPresentation> = {
    Schedule: { labelKey: 'adminBackups.triggers.schedule', token: 'schedule' },
    Manual: manualBackup,
    Migration: { labelKey: 'adminBackups.triggers.migration', token: 'migration' },
}

/** Label and token of what created an export of the database. */
export function backupTriggerPresentation(
    trigger: string | null | undefined,
): BackupTriggerPresentation {
    return backupTriggers[trigger ?? ''] ?? manualBackup
}

/** Whether a text is a time of day as the server expects it (HH:mm, 24 hours). */
export function isTimeOfDay(text: string): boolean {
    return /^([01][0-9]|2[0-3]):[0-5][0-9]$/.test(text)
}

/** The parts of a media folder that mark its state, as tokens of data-state. */
interface MediaSourceStateInput {
    readonly isActive?: boolean | null
    readonly isAvailable?: boolean | null
    readonly isWatched?: boolean | null
    readonly followsMediaPath?: boolean | null
}

/**
 * data-state tokens of a media folder: active or inactive, plus unavailable (the folder is
 * missing), watched (the file watcher follows it) and media-path (it comes from MEDIA_PATH).
 */
export function mediaSourceStates(source: MediaSourceStateInput): string[] {
    const states = [source.isActive ? 'active' : 'inactive']
    if (source.isAvailable === false) {
        states.push('unavailable')
    }
    if (source.isWatched) {
        states.push('watched')
    }
    if (source.followsMediaPath) {
        states.push('media-path')
    }
    return states
}
