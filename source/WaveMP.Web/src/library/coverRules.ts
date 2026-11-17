// Cover rules: whether albums and songs show the cover of the album or of the song first. The
// global rule lives in the library settings, albums and songs may have a rule of their own or
// inherit the next level. Pure functions only.

import type { CoverPriority, CoverRuleOption } from '@/api/generated/models'

/** The priorities of the global rule, in the order they are offered. */
export const coverPriorities: readonly CoverPriority[] = ['AlbumFirst', 'TrackFirst']

/** The rules of an album or song, in the order they are offered. */
export const coverRuleOptions: readonly CoverRuleOption[] = ['Inherit', 'AlbumFirst', 'TrackFirst']

const optionKeys: Record<CoverRuleOption, string> = {
    Inherit: 'coverRule.inherit',
    AlbumFirst: 'coverRule.albumFirst',
    TrackFirst: 'coverRule.trackFirst',
}

const priorityKeys: Record<CoverPriority, string> = {
    AlbumFirst: 'coverRule.priorities.albumFirst',
    TrackFirst: 'coverRule.priorities.trackFirst',
}

/** Message key of a rule of an album or song, e.g. on its segment. */
export function coverRuleOptionKey(option: CoverRuleOption): string {
    return optionKeys[option]
}

/** Message key of a priority, e.g. the inherited one; unknown values read as album first. */
export function coverPriorityKey(priority: CoverPriority | null | undefined): string {
    return priorityKeys[priority ?? 'AlbumFirst'] ?? priorityKeys.AlbumFirst
}

/** Stable token of a rule or priority for data-item, e.g. album-first. */
export function coverRuleToken(value: CoverRuleOption | CoverPriority): string {
    return value.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase()
}

/** The rule a form starts with: the stored one, Inherit while none is known. */
export function coverRuleOptionOf(rule: CoverRuleOption | null | undefined): CoverRuleOption {
    return rule && coverRuleOptions.includes(rule) ? rule : 'Inherit'
}

/** The global priority a form starts with: the stored one, album first while none is known. */
export function coverPriorityOf(priority: CoverPriority | null | undefined): CoverPriority {
    return priority && coverPriorities.includes(priority) ? priority : 'AlbumFirst'
}
