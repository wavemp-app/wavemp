// Cover rules. Songs show their own cover, else the album cover (the default); the album cover,
// else their own; or only their own. Albums show their own cover, else the picture of their artist
// (the default); or only their own. The global rules live in the library settings; admins set
// exceptions for single albums and songs in their editors, which inherit the next level otherwise.
// Pure functions only.

import type {
    AlbumCoverFallback,
    AlbumCoverFallbackOption,
    CoverPriority,
    CoverRuleOption,
} from '@/api/generated/models'

/** The rules of the songs, in the order they are offered; the default first. */
export const coverPriorities: readonly CoverPriority[] = ['TrackFirst', 'AlbumFirst', 'TrackOnly']

/** The exceptions of a song or of the songs of an album, in the order they are offered. */
export const coverRuleOptions: readonly CoverRuleOption[] = [
    'Inherit',
    'TrackFirst',
    'AlbumFirst',
    'TrackOnly',
]

/** What albums without a cover of their own show, in the order they are offered; the default first. */
export const albumCoverFallbacks: readonly AlbumCoverFallback[] = ['ArtistPicture', 'None']

/** The exceptions of an album for its own cover, in the order they are offered. */
export const albumCoverFallbackOptions: readonly AlbumCoverFallbackOption[] = [
    'Inherit',
    'ArtistPicture',
    'None',
]

const priorityKeys: Record<CoverPriority, string> = {
    TrackFirst: 'coverRule.priorities.trackFirst',
    AlbumFirst: 'coverRule.priorities.albumFirst',
    TrackOnly: 'coverRule.priorities.trackOnly',
}

const fallbackKeys: Record<AlbumCoverFallback, string> = {
    ArtistPicture: 'coverRule.fallbacks.artistPicture',
    None: 'coverRule.fallbacks.none',
}

/** Message key of a rule of the songs; unknown values read as the default. */
export function coverPriorityKey(priority: CoverPriority | null | undefined): string {
    return priorityKeys[priority ?? 'TrackFirst'] ?? priorityKeys.TrackFirst
}

/** Message key of what albums show without a cover; unknown values read as the default. */
export function albumCoverFallbackKey(fallback: AlbumCoverFallback | null | undefined): string {
    return fallbackKeys[fallback ?? 'ArtistPicture'] ?? fallbackKeys.ArtistPicture
}

/** Stable token of a rule for data-item, e.g. album-first. */
export function coverRuleToken(
    value: CoverRuleOption | CoverPriority | AlbumCoverFallback | AlbumCoverFallbackOption,
): string {
    return value.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase()
}

/** The exception a form starts with: the stored one, Inherit while none is known. */
export function coverRuleOptionOf(rule: CoverRuleOption | null | undefined): CoverRuleOption {
    return rule && coverRuleOptions.includes(rule) ? rule : 'Inherit'
}

/** The exception of an album a form starts with: the stored one, Inherit while none is known. */
export function albumCoverFallbackOptionOf(
    fallback: AlbumCoverFallbackOption | null | undefined,
): AlbumCoverFallbackOption {
    return fallback && albumCoverFallbackOptions.includes(fallback) ? fallback : 'Inherit'
}

/** The global rule of the songs a form starts with: the stored one, the default while none is known. */
export function coverPriorityOf(priority: CoverPriority | null | undefined): CoverPriority {
    return priority && coverPriorities.includes(priority) ? priority : 'TrackFirst'
}

/** The global rule of the albums a form starts with: the stored one, the default while none is known. */
export function albumCoverFallbackOf(
    fallback: AlbumCoverFallback | null | undefined,
): AlbumCoverFallback {
    return fallback && albumCoverFallbacks.includes(fallback) ? fallback : 'ArtistPicture'
}
