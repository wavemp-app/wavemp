// The rules of smart playlists in the web app: which operators fit a field, the ranges of the
// values, the form state of the rule editor and its conversion from and to the API. The server
// checks everything again; the web app checks first, so most mistakes show without a request.

import {
    SmartPlaylistFieldObject,
    SmartPlaylistMatchObject,
    SmartPlaylistOperatorObject,
    SmartPlaylistTrackSortObject,
    SortDirectionObject,
    type SaveSmartPlaylist,
    type SaveSmartPlaylistRule,
    type SmartPlaylist,
    type SmartPlaylistField,
    type SmartPlaylistMatch,
    type SmartPlaylistOperator,
    type SmartPlaylistRule,
    type SmartPlaylistTemplate,
    type SmartPlaylistTrackSort,
    type SortDirection,
} from '@/api/generated/models'

export type FieldKind = 'text' | 'number' | 'duration' | 'days' | 'flag'

/** Largest number of rules, as on the server. */
export const maxRules = 20

/** Longest text of a rule, as on the server. */
export const ruleTextMaxLength = 200

/** Largest limit of a smart playlist, as on the server. */
export const maxLimit = 10_000

/** The fields in the order the editor offers them. */
export const smartFields: readonly SmartPlaylistField[] = [
    SmartPlaylistFieldObject.Artist,
    SmartPlaylistFieldObject.Album,
    SmartPlaylistFieldObject.Genre,
    SmartPlaylistFieldObject.Year,
    SmartPlaylistFieldObject.Rating,
    SmartPlaylistFieldObject.Favorite,
    SmartPlaylistFieldObject.PlayCount,
    SmartPlaylistFieldObject.LastPlayed,
    SmartPlaylistFieldObject.DateAdded,
    SmartPlaylistFieldObject.Duration,
    SmartPlaylistFieldObject.HasAudio,
    SmartPlaylistFieldObject.HasVideo,
]

/** The sort keys in the order the editor offers them. */
export const smartSorts: readonly SmartPlaylistTrackSort[] = Object.values(
    SmartPlaylistTrackSortObject,
)

const textOperators: readonly SmartPlaylistOperator[] = [
    SmartPlaylistOperatorObject.Contains,
    SmartPlaylistOperatorObject.DoesNotContain,
    SmartPlaylistOperatorObject.Is,
    SmartPlaylistOperatorObject.IsNot,
]

const numberOperators: readonly SmartPlaylistOperator[] = [
    SmartPlaylistOperatorObject.Is,
    SmartPlaylistOperatorObject.IsNot,
    SmartPlaylistOperatorObject.AtLeast,
    SmartPlaylistOperatorObject.AtMost,
    SmartPlaylistOperatorObject.Between,
]

const dayOperators: readonly SmartPlaylistOperator[] = [
    SmartPlaylistOperatorObject.InLast,
    SmartPlaylistOperatorObject.NotInLast,
]

const flagOperators: readonly SmartPlaylistOperator[] = [
    SmartPlaylistOperatorObject.IsTrue,
    SmartPlaylistOperatorObject.IsFalse,
]

/**
 * Whether a rule names an artist, album or genre that the server links by id: "is" and "is not"
 * of these fields. "Contains" compares the text.
 */
export function canReference(rule: Pick<SmartPlaylistRule, 'field' | 'operator'>): boolean {
    return (
        kindOf(rule.field ?? SmartPlaylistFieldObject.Year) === 'text' &&
        (rule.operator === SmartPlaylistOperatorObject.Is ||
            rule.operator === SmartPlaylistOperatorObject.IsNot)
    )
}

/**
 * Whether a rule is linked to an entry of the library: linked, unlinked (the server found no
 * single entry of its name, so it compares the text) or undefined (no such rule, or not known).
 */
export function referenceOf(
    rule: Pick<RuleDraft, 'field' | 'operator' | 'referenceId'>,
): 'linked' | 'unlinked' | undefined {
    if (!canReference(rule) || rule.referenceId === undefined) {
        return undefined
    }

    return rule.referenceId === null ? 'unlinked' : 'linked'
}

export function kindOf(field: SmartPlaylistField): FieldKind {
    switch (field) {
        case SmartPlaylistFieldObject.Artist:
        case SmartPlaylistFieldObject.Album:
        case SmartPlaylistFieldObject.Genre:
            return 'text'
        case SmartPlaylistFieldObject.Duration:
            return 'duration'
        case SmartPlaylistFieldObject.LastPlayed:
        case SmartPlaylistFieldObject.DateAdded:
            return 'days'
        case SmartPlaylistFieldObject.Favorite:
        case SmartPlaylistFieldObject.HasAudio:
        case SmartPlaylistFieldObject.HasVideo:
            return 'flag'
        default:
            return 'number'
    }
}

/** The operators that fit a field; the first one is the default of a new rule. */
export function operatorsOf(field: SmartPlaylistField): readonly SmartPlaylistOperator[] {
    switch (kindOf(field)) {
        case 'text':
            return textOperators
        case 'days':
            return dayOperators
        case 'flag':
            return flagOperators
        default:
            return numberOperators
    }
}

/** The allowed numbers of a field: seconds for the duration, days for the dates. */
export function rangeOf(field: SmartPlaylistField): { min: number; max: number } {
    switch (field) {
        case SmartPlaylistFieldObject.Year:
            return { min: 0, max: 9999 }
        case SmartPlaylistFieldObject.Rating:
            return { min: 0, max: 5 }
        case SmartPlaylistFieldObject.PlayCount:
            return { min: 0, max: 1_000_000 }
        case SmartPlaylistFieldObject.Duration:
            return { min: 0, max: 359_999 }
        default:
            return { min: 1, max: 36_500 }
    }
}

/** A rule as the editor holds it: the values as typed. */
export interface RuleDraft {
    /** Stable key of the row while rules are added and removed. */
    readonly key: number
    field: SmartPlaylistField
    operator: SmartPlaylistOperator
    text: string
    number: string
    numberTo: string
    /**
     * The artist, album or genre a rule with "is" or "is not" names, as the server linked it: its
     * id, null if the server found no single entry of that name, or undefined while unknown (a new
     * rule, or one whose field or text changed; the server links it on saving).
     */
    referenceId?: string | null
}

/** The whole smart playlist as the editor holds it. */
export interface SmartPlaylistDraft {
    name: string
    match: SmartPlaylistMatch
    rules: RuleDraft[]
    sort: SmartPlaylistTrackSort
    direction: SortDirection
    limited: boolean
    limit: string
}

let nextKey = 1

function newKey(): number {
    return nextKey++
}

/** Sensible first values, so a new rule is complete as far as possible. */
function defaultNumber(field: SmartPlaylistField): string {
    switch (field) {
        case SmartPlaylistFieldObject.Rating:
            return '4'
        case SmartPlaylistFieldObject.PlayCount:
            return '1'
        case SmartPlaylistFieldObject.LastPlayed:
        case SmartPlaylistFieldObject.DateAdded:
            return '30'
        default:
            return ''
    }
}

export function newRule(field: SmartPlaylistField = SmartPlaylistFieldObject.Artist): RuleDraft {
    return {
        key: newKey(),
        field,
        operator: operatorsOf(field)[0]!,
        text: '',
        number: defaultNumber(field),
        numberTo: '',
    }
}

/** The rule with another field: the operator and the values stay where they still fit. */
export function withField(rule: RuleDraft, field: SmartPlaylistField): RuleDraft {
    if (field === rule.field) {
        return rule
    }

    const sameKind = kindOf(field) === kindOf(rule.field)
    const operators = operatorsOf(field)
    return {
        key: rule.key,
        field,
        operator: operators.includes(rule.operator) ? rule.operator : operators[0]!,
        text: sameKind ? rule.text : '',
        number:
            sameKind && field !== SmartPlaylistFieldObject.Rating
                ? rule.number
                : defaultNumber(field),
        numberTo: sameKind && field !== SmartPlaylistFieldObject.Rating ? rule.numberTo : '',
    }
}

/** "3:30" or "210" as seconds; undefined if it is neither. */
export function parseDuration(value: string): number | undefined {
    const trimmed = value.trim()
    const parts = /^(?:(\d{1,3}):)?(\d{1,6})$/.exec(trimmed)
    if (!parts) {
        return undefined
    }

    const minutes = parts[1] === undefined ? 0 : Number(parts[1])
    const seconds = Number(parts[2])
    if (parts[1] !== undefined && seconds >= 60) {
        return undefined
    }

    return minutes * 60 + seconds
}

/** Seconds as "m:ss", the way the editor shows a duration. */
export function formatDurationValue(seconds: number): string {
    const minutes = Math.floor(seconds / 60)
    const rest = seconds % 60
    return `${minutes}:${String(rest).padStart(2, '0')}`
}

/** A typed number of a field as the API wants it; undefined if it is no whole number. */
export function parseValue(field: SmartPlaylistField, value: string): number | undefined {
    if (kindOf(field) === 'duration') {
        return parseDuration(value)
    }

    const trimmed = value.trim()
    return /^\d{1,7}$/.test(trimmed) ? Number(trimmed) : undefined
}

function showValue(field: SmartPlaylistField, value: number | null | undefined): string {
    if (value === null || value === undefined) {
        return ''
    }

    return kindOf(field) === 'duration' ? formatDurationValue(value) : String(value)
}

/** Which values a rule needs with its operator. */
export function valuesOf(
    rule: Pick<RuleDraft, 'field' | 'operator'>,
): 'text' | 'number' | 'range' | 'none' {
    const kind = kindOf(rule.field)
    if (kind === 'flag') {
        return 'none'
    }

    if (kind === 'text') {
        return 'text'
    }

    return rule.operator === SmartPlaylistOperatorObject.Between ? 'range' : 'number'
}

export type RuleProblem =
    | { readonly part: 'text' | 'number' | 'numberTo'; readonly code: 'required' }
    | { readonly part: 'text'; readonly code: 'max_length'; readonly max: number }
    | { readonly part: 'number' | 'numberTo'; readonly code: 'invalid_format' }
    | {
          readonly part: 'number' | 'numberTo'
          readonly code: 'out_of_range'
          readonly min: number
          readonly max: number
      }

/** The mistakes of a rule, as the server would report them. */
export function ruleProblems(rule: RuleDraft): RuleProblem[] {
    const values = valuesOf(rule)
    if (values === 'none') {
        return []
    }

    if (values === 'text') {
        const text = rule.text.trim()
        if (text.length === 0) {
            return [{ part: 'text', code: 'required' }]
        }

        return text.length > ruleTextMaxLength
            ? [{ part: 'text', code: 'max_length', max: ruleTextMaxLength }]
            : []
    }

    const { min, max } = rangeOf(rule.field)
    const problems: RuleProblem[] = []
    const check = (part: 'number' | 'numberTo', value: string, lower: number) => {
        if (value.trim().length === 0) {
            problems.push({ part, code: 'required' })
            return undefined
        }

        const number = parseValue(rule.field, value)
        if (number === undefined) {
            problems.push({ part, code: 'invalid_format' })
            return undefined
        }

        if (number < lower || number > max) {
            problems.push({ part, code: 'out_of_range', min: lower, max })
            return undefined
        }

        return number
    }

    const from = check('number', rule.number, min)
    if (values === 'range') {
        check('numberTo', rule.numberTo, from ?? min)
    }

    return problems
}

/** The rule for the API, with only the values its operator needs. */
export function toSaveRule(rule: RuleDraft): SaveSmartPlaylistRule {
    const values = valuesOf(rule)
    const saved: SaveSmartPlaylistRule = { field: rule.field, operator: rule.operator }
    if (values === 'text') {
        saved.text = rule.text.trim()
        if (canReference(rule) && typeof rule.referenceId === 'string') {
            saved.referenceId = rule.referenceId
        }
    } else if (values !== 'none') {
        saved.number = parseValue(rule.field, rule.number) ?? null
        if (values === 'range') {
            saved.numberTo = parseValue(rule.field, rule.numberTo) ?? null
        }
    }

    return saved
}

export function toRuleDraft(rule: SmartPlaylistRule): RuleDraft | undefined {
    if (!rule.field || !rule.operator) {
        return undefined
    }

    return {
        key: newKey(),
        field: rule.field,
        operator: rule.operator,
        text: rule.text ?? '',
        number: showValue(rule.field, rule.number),
        numberTo: showValue(rule.field, rule.numberTo),
        // The server leaves the id out of a rule it could not link.
        referenceId: canReference(rule) ? (rule.referenceId ?? null) : undefined,
    }
}

/** The editor state of a new smart playlist: one empty rule, all songs by artist. */
export function emptyDraft(): SmartPlaylistDraft {
    return {
        name: '',
        match: SmartPlaylistMatchObject.All,
        rules: [newRule()],
        sort: SmartPlaylistTrackSortObject.Artist,
        direction: SortDirectionObject.Asc,
        limited: false,
        limit: '25',
    }
}

/** The editor state of a stored smart playlist or of a template (with the name given). */
export function draftOf(
    source: SmartPlaylist | SmartPlaylistTemplate,
    name: string = (source as SmartPlaylist).name ?? '',
): SmartPlaylistDraft {
    const limit = source.limit ?? null
    return {
        name,
        match: source.match ?? SmartPlaylistMatchObject.All,
        rules: (source.rules ?? []).flatMap((rule) => toRuleDraft(rule) ?? []),
        sort: source.sort ?? SmartPlaylistTrackSortObject.Artist,
        direction: source.direction ?? SortDirectionObject.Asc,
        limited: limit !== null,
        limit: limit === null ? '25' : String(limit),
    }
}

export function parseLimit(value: string): number | undefined {
    const trimmed = value.trim()
    if (!/^\d{1,6}$/.test(trimmed)) {
        return undefined
    }

    const limit = Number(trimmed)
    return limit >= 1 && limit <= maxLimit ? limit : undefined
}

/** The smart playlist for the API. */
export function toSave(draft: SmartPlaylistDraft): SaveSmartPlaylist {
    return {
        name: draft.name.trim(),
        match: draft.match,
        rules: draft.rules.map(toSaveRule),
        sort: draft.sort,
        direction: draft.direction,
        limit: draft.limited ? (parseLimit(draft.limit) ?? null) : null,
    }
}

/** The rule and the value of an error field of the server, e.g. "rules[2].numberTo". */
export function ruleErrorOf(
    field: string,
): { index: number; part: 'field' | 'operator' | 'text' | 'number' | 'numberTo' } | undefined {
    const match = /^rules\[(\d+)\]\.(field|operator|text|number|numberTo)$/.exec(field)
    return match
        ? {
              index: Number(match[1]),
              part: match[2] as 'field' | 'operator' | 'text' | 'number' | 'numberTo',
          }
        : undefined
}

/** Part of a message key for a value of the API, e.g. "fourStarsAndMore" for "FourStarsAndMore". */
export function messageKey(value: string): string {
    return value.charAt(0).toLowerCase() + value.slice(1)
}

/** The parameters of the readable text of a rule, e.g. for "Rating at least {value}". */
export interface RuleSummary {
    /** Message key of the field. */
    readonly fieldKey: string
    /** Message key of the operator, with {field}, {value} and {to}. */
    readonly operatorKey: string
    readonly value: string
    readonly to: string
    /** Whether a rule that names an artist, album or genre is linked to it; see referenceOf. */
    readonly reference?: 'linked' | 'unlinked'
}

export function summaryOf(rule: SmartPlaylistRule): RuleSummary | undefined {
    if (!rule.field || !rule.operator) {
        return undefined
    }

    const kind = kindOf(rule.field)
    const show = (value: number | null | undefined) => showValue(rule.field!, value)
    const flagKey = kind === 'flag' ? `${messageKey(rule.field)}.` : ''
    return {
        fieldKey: `smartPlaylists.fields.${messageKey(rule.field)}`,
        operatorKey: `smartPlaylists.summary.${flagKey}${messageKey(rule.operator)}`,
        value: kind === 'text' ? (rule.text ?? '') : show(rule.number),
        to: show(rule.numberTo),
        reference: canReference(rule) ? (rule.referenceId ? 'linked' : 'unlinked') : undefined,
    }
}
