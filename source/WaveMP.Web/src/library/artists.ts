// The artists of songs and albums: a song has one or more artists, each a main or a featured
// artist, an album one or more album artists. Pure functions only.

import {
    ArtistRoleObject,
    type ArtistCredit,
    type ArtistReference,
    type ArtistRole,
} from '@/api/generated/models'

/** What the API tells about the artists of a song or album: all of them, and the first alone. */
export interface ArtistSubject {
    readonly artists?: readonly ArtistCredit[] | null
    readonly artist?: ArtistReference | null
}

/** The separators of a text of artists, translated: ", " between names, " feat. " before guests. */
export interface ArtistSeparators {
    readonly list: string
    readonly featuring: string
}

/**
 * The separators of a text of artists outside the translated views, e.g. for the media session of
 * the system: the same in every language.
 */
export const plainArtistSeparators: ArtistSeparators = { list: ', ', featuring: ' feat. ' }

/** One artist in the text of several artists, with the separator in front of it. */
export interface ArtistPart {
    readonly artist: ArtistCredit
    /** Null for the first artist; "featuring" before the first featured artist after a main one. */
    readonly separator: 'list' | 'featuring' | null
}

/**
 * All artists of a song or album in their order. Falls back to the first artist alone for
 * answers without the list (e.g. a state stored before songs had several artists).
 */
export function artistsOf(subject: ArtistSubject | null | undefined): ArtistCredit[] {
    const artists = (subject?.artists ?? []).filter((artist) => !!artist.name)
    if (artists.length > 0) {
        return artists
    }

    const first = subject?.artist
    return first?.name ? [{ id: first.id, name: first.name, role: ArtistRoleObject.Main }] : []
}

function isFeatured(artist: Pick<ArtistCredit, 'role'>): boolean {
    return artist.role === ArtistRoleObject.Featured
}

/**
 * The artists as a text shows them: the main artists first, then the featured ones, each group in
 * its order, with the separator in front of every artist.
 */
export function artistParts(artists: readonly ArtistCredit[]): ArtistPart[] {
    const main = artists.filter((artist) => !isFeatured(artist))
    const featured = artists.filter(isFeatured)
    return [...main, ...featured].map((artist, index) => ({
        artist,
        separator:
            index === 0 ? null : index === main.length && main.length > 0 ? 'featuring' : 'list',
    }))
}

/** The artists as one text, e.g. "Simon & Garfunkel, AC/DC feat. K.Flay"; empty without artists. */
export function artistText(artists: readonly ArtistCredit[], separators: ArtistSeparators): string {
    return artistParts(artists)
        .map(({ artist, separator }) => (separator ? separators[separator] : '') + artist.name)
        .join('')
}

/**
 * The key a name is found by, like the server: lower case, without accents, only letters and
 * digits, e.g. "kflay" for "K.Flay" and "beyonce" for "Beyoncé". Empty for a name without letters
 * and digits.
 */
export function nameKey(name: string): string {
    return name
        .normalize('NFKD')
        .replace(/\p{M}/gu, '')
        .toLocaleLowerCase('en')
        .replace(/[^\p{L}\p{N}]/gu, '')
}

/** The same artists in the same order: by id where both have one, otherwise by name. */
export function sameArtists(
    first: readonly Pick<ArtistCredit, 'id' | 'name'>[],
    second: readonly Pick<ArtistCredit, 'id' | 'name'>[],
): boolean {
    return (
        first.length === second.length &&
        first.every((artist, index) => {
            const other = second[index]!
            if (artist.id && other.id) {
                return artist.id === other.id
            }

            return nameKey(artist.name ?? '') === nameKey(other.name ?? '')
        })
    )
}

/** One artist of the metadata editor: a name, its role and, for an artist of the library, its id. */
export interface ArtistEntry {
    readonly name: string
    readonly role: ArtistRole
    /** Id of the artist of the library the name was picked from; undefined for a new name. */
    readonly id?: string
}

/** Whether the list already holds an artist with the same normalized name (or the same id). */
export function hasArtist(entries: readonly ArtistEntry[], candidate: ArtistEntry): boolean {
    const key = nameKey(candidate.name)
    return entries.some(
        (entry) =>
            (candidate.id !== undefined && entry.id === candidate.id) ||
            (key !== '' && nameKey(entry.name) === key),
    )
}

/** The list with the entry moved from one index to another. */
export function moveEntry<T>(entries: readonly T[], from: number, to: number): T[] {
    const moved = [...entries]
    const [entry] = moved.splice(from, 1)
    if (entry === undefined) {
        return moved
    }

    moved.splice(Math.max(0, Math.min(to, moved.length)), 0, entry)
    return moved
}
