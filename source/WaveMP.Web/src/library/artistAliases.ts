// Presentation of the aliases of an artist: where an alias comes from and which message a failed
// request shows.

import {
    ArtistAliasSourceObject,
    ProblemCodeObject,
    type ArtistAlias,
} from '@/api/generated/models'

/** Origin of an alias as value of data-source: added by an admin or taken from MusicBrainz. */
export type AliasSource = 'admin' | 'musicbrainz'

export function aliasSourceOf(alias: ArtistAlias): AliasSource {
    return alias.source === ArtistAliasSourceObject.MusicBrainz ? 'musicbrainz' : 'admin'
}

/** Kind of request a failure belongs to; each has its own message for unknown failures. */
export type AliasAction = 'load' | 'add' | 'remove' | 'import'

/** The message key of a failed request, from the problem code of the server. */
export function aliasFailureKey(code: string | null | undefined, action: AliasAction): string {
    switch (code) {
        case ProblemCodeObject.ArtistAlias_exists:
            return 'artistAliases.errors.exists'
        case ProblemCodeObject.ArtistAlias_not_found:
            return 'artistAliases.errors.aliasNotFound'
        case ProblemCodeObject.ArtistNot_found:
            return 'artistAliases.errors.artistNotFound'
        case ProblemCodeObject.ArtistPicture_lookup_disabled:
            return 'artistAliases.errors.lookupDisabled'
        case ProblemCodeObject.ArtistMusicbrainz_artist_missing:
            return 'artistAliases.errors.musicBrainzArtistMissing'
        case ProblemCodeObject.ArtistPicture_lookup_unavailable:
            return 'artistAliases.errors.unavailable'
        default:
            return `artistAliases.errors.${action}Failed`
    }
}

/** Aliases in the order the server lists them: by name, ignoring case and accents. */
export function sortAliases(aliases: readonly ArtistAlias[]): ArtistAlias[] {
    return [...aliases].sort((first, second) =>
        (first.name ?? '').localeCompare(second.name ?? '', undefined, { sensitivity: 'base' }),
    )
}
