// Logic of the review inbox: the filters and sort orders of the list, the hints on missing tags,
// the selection for the bulk action and the way through the list on the page of a song.

import {
    MetadataTagObject,
    TrackReviewSortObject,
    TrackReviewStatusObject,
    type MetadataTag,
    type TrackReview,
    type TrackReviewSort,
} from '@/api/generated/models'

export interface ReviewOption<T extends string> {
    readonly value: T
    /** data-item of the segment. */
    readonly item: string
    readonly labelKey: string
}

/** Which songs the list shows by their tags. */
export type MissingTagsFilter = 'all' | 'missing' | 'complete'

export const missingTagsFilters: readonly ReviewOption<MissingTagsFilter>[] = [
    { value: 'all', item: 'all', labelKey: 'reviewInbox.filters.all' },
    { value: 'missing', item: 'missing-tags', labelKey: 'reviewInbox.filters.missing' },
    { value: 'complete', item: 'complete-tags', labelKey: 'reviewInbox.filters.complete' },
]

/** Sort orders of the list; the API shows the songs added last first by default. */
export const reviewSorts: readonly ReviewOption<TrackReviewSort>[] = [
    {
        value: TrackReviewSortObject.FirstSeenAt,
        item: 'first-seen-at',
        labelKey: 'reviewInbox.sorts.firstSeenAt',
    },
    { value: TrackReviewSortObject.Artist, item: 'artist', labelKey: 'reviewInbox.sorts.artist' },
    { value: TrackReviewSortObject.Album, item: 'album', labelKey: 'reviewInbox.sorts.album' },
    { value: TrackReviewSortObject.Title, item: 'title', labelKey: 'reviewInbox.sorts.title' },
]

export function isMissingTagsFilter(value: unknown): value is MissingTagsFilter {
    return missingTagsFilters.some((option) => option.value === value)
}

export function isReviewSort(value: unknown): value is TrackReviewSort {
    return reviewSorts.some((option) => option.value === value)
}

/** The missingTags parameter of the API for a filter; undefined for all songs. */
export function missingTagsParameter(filter: MissingTagsFilter): boolean | undefined {
    return filter === 'all' ? undefined : filter === 'missing'
}

/** data-field and message key of each tag field the inbox checks. */
const tagFields: Readonly<Record<MetadataTag, { field: string; labelKey: string }>> = {
    [MetadataTagObject.Title]: { field: 'title', labelKey: 'reviewInbox.tags.title' },
    [MetadataTagObject.Artist]: { field: 'artist', labelKey: 'reviewInbox.tags.artist' },
    [MetadataTagObject.Album]: { field: 'album', labelKey: 'reviewInbox.tags.album' },
    [MetadataTagObject.Year]: { field: 'year', labelKey: 'reviewInbox.tags.year' },
    [MetadataTagObject.TrackNumber]: { field: 'number', labelKey: 'reviewInbox.tags.trackNumber' },
    [MetadataTagObject.Genre]: { field: 'genre', labelKey: 'reviewInbox.tags.genre' },
}

export interface MissingTagHint {
    readonly tag: MetadataTag
    /** data-field of the hint, the same as the field of the metadata editor. */
    readonly field: string
    readonly labelKey: string
}

/** The hints on the tags a song lacks, in the order of the API; unknown values are left out. */
export function missingTagHints(tags: readonly MetadataTag[] | null | undefined): MissingTagHint[] {
    return (tags ?? []).flatMap((tag) => {
        const known = tagFields[tag]
        return known ? [{ tag, ...known }] : []
    })
}

/** Whether a song is reviewed: by the API or in this tab since the list was loaded. */
export function isReviewed(item: TrackReview, reviewed: ReadonlySet<string>): boolean {
    return item.status === TrackReviewStatusObject.Reviewed || reviewed.has(item.trackId ?? '')
}

/** The songs of a list that can still be marked as reviewed. */
export function openIds(items: readonly TrackReview[], reviewed: ReadonlySet<string>): string[] {
    return items.filter((item) => !isReviewed(item, reviewed)).map((item) => item.trackId ?? '')
}

/** The selection with one song added or removed. */
export function toggled(selected: ReadonlySet<string>, id: string, on: boolean): Set<string> {
    const next = new Set(selected)
    if (on) {
        next.add(id)
    } else {
        next.delete(id)
    }
    return next
}

/** The selection without the songs that are reviewed or no longer in the list. */
export function selectionOf(
    selected: ReadonlySet<string>,
    items: readonly TrackReview[],
    reviewed: ReadonlySet<string>,
): Set<string> {
    const open = new Set(openIds(items, reviewed))
    return new Set([...selected].filter((id) => open.has(id)))
}

export interface ReviewPosition {
    /** Position of the song in the list, from 0; -1 if it is not in the list. */
    readonly index: number
    readonly previous: string | null
    readonly next: string | null
}

/** Where a song is in the list and which songs come before and after it. */
export function reviewPosition(ids: readonly string[], current: string): ReviewPosition {
    const index = ids.indexOf(current)
    if (index < 0) {
        return { index, previous: null, next: null }
    }

    return {
        index,
        previous: ids[index - 1] ?? null,
        next: ids[index + 1] ?? null,
    }
}

/**
 * The song that opens after an action on the current one: the next song after it that is not
 * reviewed yet. Songs before it were reviewed or skipped, so the way through the list never goes
 * back. A song that is not in the list starts at the beginning of the list.
 */
export function nextOpenId(
    ids: readonly string[],
    current: string,
    reviewed: ReadonlySet<string>,
): string | null {
    const start = ids.indexOf(current) + 1
    return ids.slice(start).find((id) => id !== current && !reviewed.has(id)) ?? null
}
