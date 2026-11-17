// Presentation of the file selection per song: the filters and sort orders of the overview, the
// states of a song and the choices of a category on the page of a song.

import {
    FileSelectionFilterObject,
    FileSelectionSortObject,
    FileSelectionStateObject,
    type FileSelection,
    type FileSelectionFilter,
    type FileSelectionSort,
    type FileSelectionState,
    type MediaFile,
    type TrackFileSelection,
    type TrackFileSummary,
    type UpdateTrackFileSelection,
} from '@/api/generated/models'

export const fileCategories = ['audio', 'video', 'cover'] as const

export type FileCategory = (typeof fileCategories)[number]

export interface FileSelectionOption<T extends string> {
    readonly value: T
    /** data-item of the segment. */
    readonly item: string
    readonly labelKey: string
}

/** Filters of the overview, in the order they are offered. */
export const fileSelectionFilters: readonly FileSelectionOption<FileSelectionFilter>[] = [
    { value: FileSelectionFilterObject.All, item: 'all', labelKey: 'adminFiles.filters.all' },
    {
        value: FileSelectionFilterObject.Choices,
        item: 'choices',
        labelKey: 'adminFiles.filters.choices',
    },
    {
        value: FileSelectionFilterObject.SeveralAudio,
        item: 'several-audio',
        labelKey: 'adminFiles.filters.severalAudio',
    },
    {
        value: FileSelectionFilterObject.SeveralVideo,
        item: 'several-video',
        labelKey: 'adminFiles.filters.severalVideo',
    },
    {
        value: FileSelectionFilterObject.SeveralCovers,
        item: 'several-covers',
        labelKey: 'adminFiles.filters.severalCovers',
    },
    {
        value: FileSelectionFilterObject.Selected,
        item: 'selected',
        labelKey: 'adminFiles.filters.selected',
    },
    {
        value: FileSelectionFilterObject.Missing,
        item: 'missing',
        labelKey: 'adminFiles.filters.missing',
    },
]

/** Sort orders of the overview; the API sorts the number of files descending by default. */
export const fileSelectionSorts: readonly FileSelectionOption<FileSelectionSort>[] = [
    { value: FileSelectionSortObject.Path, item: 'path', labelKey: 'adminFiles.sorts.path' },
    { value: FileSelectionSortObject.Title, item: 'title', labelKey: 'adminFiles.sorts.title' },
    {
        value: FileSelectionSortObject.FileCount,
        item: 'file-count',
        labelKey: 'adminFiles.sorts.fileCount',
    },
]

export function isFileSelectionFilter(value: unknown): value is FileSelectionFilter {
    return fileSelectionFilters.some((option) => option.value === value)
}

export function isFileSelectionSort(value: unknown): value is FileSelectionSort {
    return fileSelectionSorts.some((option) => option.value === value)
}

function stateOf(state: FileSelectionState | null | undefined): FileSelectionState {
    return state ?? FileSelectionStateObject.Automatic
}

/** Message key of the state of a category, e.g. "Audio selected". */
export function categoryStateKey(category: FileCategory, state: FileSelectionState): string {
    return `adminFiles.states.${category}${state}`
}

/** The categories of a song that do not use the automatic choice, with their state. */
export function summaryStates(
    summary: Pick<TrackFileSummary, FileCategory>,
): { category: FileCategory; state: FileSelectionState }[] {
    return fileCategories
        .map((category) => ({ category, state: stateOf(summary[category]) }))
        .filter(({ state }) => state !== FileSelectionStateObject.Automatic)
}

/**
 * data-state of a song in the overview: "selected" if a category uses a selected file, "missing"
 * if a selected file is gone, "choices" if a category has more than one file.
 */
export function summaryStateTokens(summary: TrackFileSummary): string {
    const states = summaryStates(summary).map(({ state }) => state)
    const tokens: string[] = []
    if (states.includes(FileSelectionStateObject.Selected)) {
        tokens.push('selected')
    }

    if (states.includes(FileSelectionStateObject.Missing)) {
        tokens.push('missing')
    }

    if (
        (summary.audioCount ?? 0) > 1 ||
        (summary.videoCount ?? 0) > 1 ||
        (summary.coverCount ?? 0) > 1
    ) {
        tokens.push('choices')
    }

    return tokens.join(' ')
}

/** One option of a category on the page of a song. */
export interface FileChoice {
    /** "automatic", the id of a file, or "missing" for a selected file that is gone. */
    readonly key: string
    readonly kind: 'automatic' | 'file' | 'missing'
    /** File id sent to the API; null for the automatic choice. */
    readonly fileId: string | null
    /** The file of the option; for the automatic choice the file it currently picks. */
    readonly file: MediaFile | null
    /** Path of a missing file. */
    readonly path: string | null
    readonly checked: boolean
    /** The file the song uses now. */
    readonly active: boolean
    /** The file the quality rule picks. */
    readonly best: boolean
}

/**
 * The options of a category: the automatic choice, every file the song can use and, while a
 * selected file is gone, that file. The selected option is checked, the automatic one if nothing
 * is selected.
 */
export function fileChoices(
    selection: FileSelection | null | undefined,
    files: readonly MediaFile[],
): FileChoice[] {
    const byId = new Map(files.map((file) => [file.id ?? '', file]))
    const state = stateOf(selection?.state)
    const selected = selection?.selectedFileId ?? null
    const automatic = selection?.automaticFileId ?? null
    const active = selection?.activeFileId ?? null

    const choices: FileChoice[] = [
        {
            key: 'automatic',
            kind: 'automatic',
            fileId: null,
            file: automatic ? (byId.get(automatic) ?? null) : null,
            path: null,
            checked: state === FileSelectionStateObject.Automatic,
            active: false,
            best: false,
        },
    ]

    for (const id of selection?.candidateFileIds ?? []) {
        choices.push({
            key: id,
            kind: 'file',
            fileId: id,
            file: byId.get(id) ?? null,
            path: null,
            checked: state === FileSelectionStateObject.Selected && id === selected,
            active: id === active,
            best: id === automatic,
        })
    }

    if (state === FileSelectionStateObject.Missing && selected) {
        choices.push({
            key: 'missing',
            kind: 'missing',
            fileId: selected,
            file: null,
            path: selection?.selectedPath ?? null,
            checked: true,
            active: false,
            best: false,
        })
    }

    return choices
}

/** data-state of an option: checked, active (in use), best (picked by the quality rule), missing. */
export function choiceStateTokens(choice: FileChoice): string {
    return [
        ...(choice.checked ? ['checked'] : []),
        ...(choice.active ? ['active'] : []),
        ...(choice.best ? ['best'] : []),
        ...(choice.kind === 'missing' ? ['missing'] : []),
    ].join(' ')
}

const requestFields = {
    audio: 'audioFileId',
    video: 'videoFileId',
    cover: 'coverFileId',
} as const satisfies Record<FileCategory, keyof UpdateTrackFileSelection>

/** Category of a field of the request, e.g. "audio" for "audioFileId". */
export function categoryOfField(field: string): FileCategory | undefined {
    return fileCategories.find((category) => requestFields[category] === field)
}

/**
 * The request that changes one category and keeps the others as they are, including a selected
 * file that is gone (the API keeps it while it is sent again).
 */
export function changedSelection(
    current: TrackFileSelection,
    category: FileCategory,
    fileId: string | null,
): UpdateTrackFileSelection {
    const request: UpdateTrackFileSelection = {}
    for (const each of fileCategories) {
        const id = each === category ? fileId : (current[each]?.selectedFileId ?? null)
        request[requestFields[each]] = id
    }

    return request
}

/** Whether any category of the song uses a selection (also one whose file is gone). */
export function hasSelection(selection: TrackFileSelection | null | undefined): boolean {
    return fileCategories.some(
        (category) => stateOf(selection?.[category]?.state) !== FileSelectionStateObject.Automatic,
    )
}
