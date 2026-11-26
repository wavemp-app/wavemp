import { describe, expect, it } from 'vitest'
import {
    ProblemCodeObject,
    SmartPlaylistFieldObject,
    SmartPlaylistMatchObject,
    SmartPlaylistOperatorObject,
    SmartPlaylistTemplateIdObject,
    SmartPlaylistTrackSortObject,
    SortDirectionObject,
    type SmartPlaylistField,
} from '@/api/generated/models'
import de from '@/locales/de.json'
import en from '@/locales/en.json'
import { convertFailureKey } from '../presentation'
import {
    canReference,
    draftOf,
    emptyDraft,
    formatDurationValue,
    kindOf,
    maxLimit,
    messageKey,
    newRule,
    operatorsOf,
    parseDuration,
    parseLimit,
    parseValue,
    referenceOf,
    ruleErrorOf,
    ruleProblems,
    ruleTextMaxLength,
    smartFields,
    smartSorts,
    summaryOf,
    toRuleDraft,
    toSave,
    toSaveRule,
    valuesOf,
    withField,
    type RuleDraft,
} from '../smartRules'

function rule(field: SmartPlaylistField, change: Partial<RuleDraft> = {}): RuleDraft {
    return { ...newRule(field), ...change }
}

/** Whether a dotted message key leads to a text in a message file. */
function hasMessage(messages: unknown, key: string): boolean {
    let current: unknown = messages
    for (const segment of key.split('.')) {
        if (typeof current !== 'object' || current === null || !(segment in current)) {
            return false
        }
        current = (current as Record<string, unknown>)[segment]
    }
    // The build compiles the messages, so a text is no string here.
    return current !== undefined
}

describe('fields and operators', () => {
    it('offers every field of the API', () => {
        expect([...smartFields].sort()).toEqual(Object.values(SmartPlaylistFieldObject).sort())
        expect([...smartSorts].sort()).toEqual(Object.values(SmartPlaylistTrackSortObject).sort())
    })

    it('groups the fields by kind', () => {
        expect(kindOf(SmartPlaylistFieldObject.Genre)).toBe('text')
        expect(kindOf(SmartPlaylistFieldObject.Year)).toBe('number')
        expect(kindOf(SmartPlaylistFieldObject.Rating)).toBe('number')
        expect(kindOf(SmartPlaylistFieldObject.PlayCount)).toBe('number')
        expect(kindOf(SmartPlaylistFieldObject.Duration)).toBe('duration')
        expect(kindOf(SmartPlaylistFieldObject.LastPlayed)).toBe('days')
        expect(kindOf(SmartPlaylistFieldObject.DateAdded)).toBe('days')
        expect(kindOf(SmartPlaylistFieldObject.Favorite)).toBe('flag')
        expect(kindOf(SmartPlaylistFieldObject.HasVideo)).toBe('flag')
    })

    it('offers only the operators that fit a field', () => {
        expect(operatorsOf(SmartPlaylistFieldObject.Artist)).toContain(
            SmartPlaylistOperatorObject.Contains,
        )
        expect(operatorsOf(SmartPlaylistFieldObject.Year)).toContain(
            SmartPlaylistOperatorObject.Between,
        )
        expect(operatorsOf(SmartPlaylistFieldObject.DateAdded)).toEqual([
            SmartPlaylistOperatorObject.InLast,
            SmartPlaylistOperatorObject.NotInLast,
        ])
        expect(operatorsOf(SmartPlaylistFieldObject.Favorite)).toEqual([
            SmartPlaylistOperatorObject.IsTrue,
            SmartPlaylistOperatorObject.IsFalse,
        ])
    })

    it('knows which values an operator needs', () => {
        expect(valuesOf(rule(SmartPlaylistFieldObject.Album))).toBe('text')
        expect(valuesOf(rule(SmartPlaylistFieldObject.Year))).toBe('number')
        expect(
            valuesOf(
                rule(SmartPlaylistFieldObject.Year, {
                    operator: SmartPlaylistOperatorObject.Between,
                }),
            ),
        ).toBe('range')
        expect(valuesOf(rule(SmartPlaylistFieldObject.HasAudio))).toBe('none')
    })
})

describe('newRule and withField', () => {
    it('starts with the first operator and a sensible value', () => {
        expect(newRule(SmartPlaylistFieldObject.Rating)).toMatchObject({
            operator: SmartPlaylistOperatorObject.Is,
            number: '4',
        })
        expect(newRule(SmartPlaylistFieldObject.LastPlayed)).toMatchObject({
            operator: SmartPlaylistOperatorObject.InLast,
            number: '30',
        })
        expect(newRule().field).toBe(SmartPlaylistFieldObject.Artist)
        expect(newRule().key).not.toBe(newRule().key)
    })

    it('keeps operator and values while the kind stays the same', () => {
        const artist = rule(SmartPlaylistFieldObject.Artist, {
            operator: SmartPlaylistOperatorObject.IsNot,
            text: 'Alpha',
        })

        expect(withField(artist, SmartPlaylistFieldObject.Album)).toEqual({
            ...artist,
            field: SmartPlaylistFieldObject.Album,
        })
    })

    it('resets operator and values for another kind', () => {
        const artist = rule(SmartPlaylistFieldObject.Artist, {
            operator: SmartPlaylistOperatorObject.Contains,
            text: 'Alpha',
        })

        const changed = withField(artist, SmartPlaylistFieldObject.DateAdded)
        expect(changed).toMatchObject({
            key: artist.key,
            operator: SmartPlaylistOperatorObject.InLast,
            text: '',
            number: '30',
        })
    })

    it('starts the rating with its own default', () => {
        const year = rule(SmartPlaylistFieldObject.Year, { number: '1999' })

        expect(withField(year, SmartPlaylistFieldObject.Rating).number).toBe('4')
    })
})

describe('values', () => {
    it('reads durations as minutes and seconds or as seconds', () => {
        expect(parseDuration('3:30')).toBe(210)
        expect(parseDuration(' 210 ')).toBe(210)
        expect(parseDuration('0:05')).toBe(5)
        expect(parseDuration('3:75')).toBeUndefined()
        expect(parseDuration('three')).toBeUndefined()
        expect(parseDuration('')).toBeUndefined()
        expect(formatDurationValue(210)).toBe('3:30')
        expect(formatDurationValue(5)).toBe('0:05')
    })

    it('reads whole numbers only', () => {
        expect(parseValue(SmartPlaylistFieldObject.Year, '1999')).toBe(1999)
        expect(parseValue(SmartPlaylistFieldObject.Year, '19.5')).toBeUndefined()
        expect(parseValue(SmartPlaylistFieldObject.Year, '-1')).toBeUndefined()
        expect(parseValue(SmartPlaylistFieldObject.Duration, '1:00')).toBe(60)
    })

    it('reads the limit between 1 and the maximum', () => {
        expect(parseLimit('25')).toBe(25)
        expect(parseLimit('0')).toBeUndefined()
        expect(parseLimit(String(maxLimit + 1))).toBeUndefined()
        expect(parseLimit('ten')).toBeUndefined()
    })
})

describe('ruleProblems', () => {
    it('needs a text for text fields, and not too long a one', () => {
        expect(ruleProblems(rule(SmartPlaylistFieldObject.Genre, { text: '  ' }))).toEqual([
            { part: 'text', code: 'required' },
        ])
        expect(
            ruleProblems(
                rule(SmartPlaylistFieldObject.Genre, {
                    text: 'x'.repeat(ruleTextMaxLength + 1),
                }),
            ),
        ).toEqual([{ part: 'text', code: 'max_length', max: ruleTextMaxLength }])
        expect(ruleProblems(rule(SmartPlaylistFieldObject.Genre, { text: 'Rock' }))).toEqual([])
    })

    it('checks numbers against the range of the field', () => {
        expect(ruleProblems(rule(SmartPlaylistFieldObject.Rating, { number: '6' }))).toEqual([
            { part: 'number', code: 'out_of_range', min: 0, max: 5 },
        ])
        expect(ruleProblems(rule(SmartPlaylistFieldObject.DateAdded, { number: '0' }))).toEqual([
            { part: 'number', code: 'out_of_range', min: 1, max: 36_500 },
        ])
        expect(ruleProblems(rule(SmartPlaylistFieldObject.Year, { number: 'abc' }))).toEqual([
            { part: 'number', code: 'invalid_format' },
        ])
        expect(ruleProblems(rule(SmartPlaylistFieldObject.Year, { number: '' }))).toEqual([
            { part: 'number', code: 'required' },
        ])
    })

    it('needs the end of a range at or after its start', () => {
        const between = { operator: SmartPlaylistOperatorObject.Between }

        expect(
            ruleProblems(
                rule(SmartPlaylistFieldObject.Year, { ...between, number: '1990', numberTo: '' }),
            ),
        ).toEqual([{ part: 'numberTo', code: 'required' }])
        expect(
            ruleProblems(
                rule(SmartPlaylistFieldObject.Year, {
                    ...between,
                    number: '1990',
                    numberTo: '1980',
                }),
            ),
        ).toEqual([{ part: 'numberTo', code: 'out_of_range', min: 1990, max: 9999 }])
        expect(
            ruleProblems(
                rule(SmartPlaylistFieldObject.Duration, {
                    ...between,
                    number: '3:00',
                    numberTo: '5:00',
                }),
            ),
        ).toEqual([])
    })

    it('needs no value for yes/no fields', () => {
        expect(ruleProblems(rule(SmartPlaylistFieldObject.Favorite))).toEqual([])
    })
})

describe('conversion from and to the API', () => {
    it('sends only the values the operator needs', () => {
        expect(
            toSaveRule(rule(SmartPlaylistFieldObject.Genre, { text: ' Rock ', number: '3' })),
        ).toEqual({
            field: SmartPlaylistFieldObject.Genre,
            operator: SmartPlaylistOperatorObject.Contains,
            text: 'Rock',
        })
        expect(
            toSaveRule(
                rule(SmartPlaylistFieldObject.Duration, {
                    operator: SmartPlaylistOperatorObject.Between,
                    number: '3:00',
                    numberTo: '330',
                }),
            ),
        ).toEqual({
            field: SmartPlaylistFieldObject.Duration,
            operator: SmartPlaylistOperatorObject.Between,
            number: 180,
            numberTo: 330,
        })
        expect(
            toSaveRule(rule(SmartPlaylistFieldObject.HasVideo, { text: 'x', number: '1' })),
        ).toEqual({
            field: SmartPlaylistFieldObject.HasVideo,
            operator: SmartPlaylistOperatorObject.IsTrue,
        })
    })

    it('shows stored values the way they are typed', () => {
        expect(
            toRuleDraft({
                field: SmartPlaylistFieldObject.Duration,
                operator: SmartPlaylistOperatorObject.AtLeast,
                number: 210,
            }),
        ).toMatchObject({ number: '3:30', numberTo: '', text: '' })
        expect(toRuleDraft({ field: SmartPlaylistFieldObject.Year })).toBeUndefined()
    })

    it('starts a new smart playlist with one rule, all songs by artist', () => {
        const draft = emptyDraft()

        expect(draft.rules).toHaveLength(1)
        expect(toSave({ ...draft, name: ' Mix ', rules: [] })).toEqual({
            name: 'Mix',
            match: SmartPlaylistMatchObject.All,
            rules: [],
            sort: SmartPlaylistTrackSortObject.Artist,
            direction: SortDirectionObject.Asc,
            limit: null,
        })
    })

    it('edits a stored smart playlist or a template and saves it unchanged', () => {
        const stored = {
            name: 'Top',
            match: SmartPlaylistMatchObject.Any,
            rules: [
                {
                    field: SmartPlaylistFieldObject.PlayCount,
                    operator: SmartPlaylistOperatorObject.AtLeast,
                    number: 1,
                },
            ],
            sort: SmartPlaylistTrackSortObject.PlayCount,
            direction: SortDirectionObject.Desc,
            limit: 50,
        }

        const draft = draftOf(stored)
        expect(draft).toMatchObject({ name: 'Top', limited: true, limit: '50' })
        expect(toSave(draft)).toEqual(stored)
        expect(draftOf({ ...stored, limit: null }, 'Named').limited).toBe(false)
        expect(draftOf({ ...stored, limit: null }, 'Named').name).toBe('Named')
    })

    it('sends no limit while the limit is off', () => {
        expect(toSave({ ...emptyDraft(), limited: false, limit: '10' }).limit).toBeNull()
        expect(toSave({ ...emptyDraft(), limited: true, limit: '10' }).limit).toBe(10)
    })

    it('finds the rule of an error field of the server', () => {
        expect(ruleErrorOf('rules[2].numberTo')).toEqual({ index: 2, part: 'numberTo' })
        expect(ruleErrorOf('rules[0].text')).toEqual({ index: 0, part: 'text' })
        expect(ruleErrorOf('name')).toBeUndefined()
    })
})

describe('links to the library', () => {
    const linkedId = '01990000-0000-7000-8000-000000000001'

    it('links only "is" and "is not" of artists, albums and genres', () => {
        const is = SmartPlaylistOperatorObject.Is
        expect(canReference({ field: SmartPlaylistFieldObject.Artist, operator: is })).toBe(true)
        expect(
            canReference({
                field: SmartPlaylistFieldObject.Genre,
                operator: SmartPlaylistOperatorObject.IsNot,
            }),
        ).toBe(true)
        expect(
            canReference({
                field: SmartPlaylistFieldObject.Album,
                operator: SmartPlaylistOperatorObject.Contains,
            }),
        ).toBe(false)
        expect(canReference({ field: SmartPlaylistFieldObject.Year, operator: is })).toBe(false)
    })

    it('marks stored rules the server could not link', () => {
        const stored = {
            field: SmartPlaylistFieldObject.Artist,
            operator: SmartPlaylistOperatorObject.Is,
            text: 'K.Flay',
        }

        expect(referenceOf(toRuleDraft(stored)!)).toBe('unlinked')
        expect(referenceOf(toRuleDraft({ ...stored, referenceId: linkedId })!)).toBe('linked')
        expect(
            referenceOf(
                toRuleDraft({ ...stored, operator: SmartPlaylistOperatorObject.Contains })!,
            ),
        ).toBeUndefined()
        expect(referenceOf(newRule(SmartPlaylistFieldObject.Artist))).toBeUndefined()
        expect(summaryOf(stored)?.reference).toBe('unlinked')
        expect(summaryOf({ ...stored, referenceId: linkedId })?.reference).toBe('linked')
        expect(
            summaryOf({ field: SmartPlaylistFieldObject.Rating, operator: stored.operator })
                ?.reference,
        ).toBeUndefined()
    })

    it('sends the id of a linked rule back and drops it with another field', () => {
        const linked = rule(SmartPlaylistFieldObject.Artist, {
            operator: SmartPlaylistOperatorObject.Is,
            text: 'K.Flay',
            referenceId: linkedId,
        })

        expect(toSaveRule(linked)).toEqual({
            field: SmartPlaylistFieldObject.Artist,
            operator: SmartPlaylistOperatorObject.Is,
            text: 'K.Flay',
            referenceId: linkedId,
        })
        expect(toSaveRule({ ...linked, referenceId: null }).referenceId).toBeUndefined()
        expect(withField(linked, SmartPlaylistFieldObject.Album).referenceId).toBeUndefined()
        expect(
            toSaveRule({ ...linked, operator: SmartPlaylistOperatorObject.Contains }).referenceId,
        ).toBeUndefined()
    })
})

describe('messages', () => {
    it('turns values of the API into message keys', () => {
        expect(messageKey('FourStarsAndMore')).toBe('fourStarsAndMore')
        expect(messageKey('Asc')).toBe('asc')
    })

    it('summarizes a rule in words', () => {
        expect(
            summaryOf({
                field: SmartPlaylistFieldObject.Duration,
                operator: SmartPlaylistOperatorObject.Between,
                number: 180,
                numberTo: 300,
            }),
        ).toEqual({
            fieldKey: 'smartPlaylists.fields.duration',
            operatorKey: 'smartPlaylists.summary.between',
            value: '3:00',
            to: '5:00',
        })
        expect(
            summaryOf({
                field: SmartPlaylistFieldObject.Favorite,
                operator: SmartPlaylistOperatorObject.IsFalse,
            }),
        ).toMatchObject({ operatorKey: 'smartPlaylists.summary.favorite.isFalse' })
        expect(summaryOf({ field: SmartPlaylistFieldObject.Genre })).toBeUndefined()
    })

    for (const [language, messages] of [
        ['en', en],
        ['de', de],
    ] as const) {
        it(`has every text of the editor and the summary in ${language}`, () => {
            const keys = [
                ...Object.values(SmartPlaylistMatchObject).flatMap((match) => [
                    `smartPlaylists.match.${messageKey(match)}`,
                    `smartPlaylists.summary.match.${messageKey(match)}`,
                ]),
                ...Object.values(SmartPlaylistTemplateIdObject).map(
                    (id) => `smartPlaylists.templates.${messageKey(id)}`,
                ),
                ...smartSorts.map((sort) => `smartPlaylists.sorts.${messageKey(sort)}`),
                ...Object.values(SortDirectionObject).map(
                    (direction) => `smartPlaylists.direction.${messageKey(direction)}`,
                ),
                ...smartFields.flatMap((field) => [
                    `smartPlaylists.fields.${messageKey(field)}`,
                    ...operatorsOf(field).flatMap((operator) => {
                        const summary = summaryOf({ field, operator })!
                        return [
                            summary.operatorKey,
                            kindOf(field) === 'flag'
                                ? `smartPlaylists.flags.${messageKey(field)}.${messageKey(operator)}`
                                : `smartPlaylists.operators.${messageKey(operator)}`,
                        ]
                    }),
                ]),
                convertFailureKey(ProblemCodeObject.PlaylistFull),
                convertFailureKey(ProblemCodeObject.ServiceUnavailable),
                convertFailureKey(ProblemCodeObject.PlaylistSmart_not_found),
                convertFailureKey(undefined),
            ]

            expect(keys.filter((key) => !hasMessage(messages, key))).toEqual([])
        })
    }
})
