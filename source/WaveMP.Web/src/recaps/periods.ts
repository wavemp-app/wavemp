// Periods of the recap and the statistics: the days the client asks the API for. The rolling
// periods end today in the time zone of the browser; "This year" starts on January 1, "All time"
// with the first play (the API finds it). Pure functions over calendar days, without times.

import { RecapPeriodObject, type RecapPeriod } from '@/api/generated/models'

/** A day of the calendar, independent of a time zone. */
export interface CalendarDay {
    readonly year: number
    readonly month: number
    readonly day: number
}

/** The days of a period; without from the period starts with the first play. */
export interface PeriodRange {
    readonly from: CalendarDay | null
    readonly to: CalendarDay
}

/** Days of a custom period as the date fields hold them (YYYY-MM-DD). */
export interface CustomRange {
    readonly from: string
    readonly to: string
}

/** The periods in the order the period picker shows them. */
export const recapPeriods: readonly RecapPeriod[] = [
    RecapPeriodObject.LastMonth,
    RecapPeriodObject.LastThreeMonths,
    RecapPeriodObject.LastYear,
    RecapPeriodObject.CurrentYear,
    RecapPeriodObject.AllTime,
    RecapPeriodObject.Custom,
]

export const defaultRecapPeriod: RecapPeriod = RecapPeriodObject.LastMonth

/** The first and last day the API accepts. */
export const firstDay: CalendarDay = { year: 1900, month: 1, day: 1 }
export const lastDay: CalendarDay = { year: 2999, month: 12, day: 31 }

export function isRecapPeriod(value: unknown): value is RecapPeriod {
    return typeof value === 'string' && (recapPeriods as readonly string[]).includes(value)
}

function daysInMonth(year: number, month: number): number {
    return new Date(Date.UTC(year, month, 0)).getUTCDate()
}

/** The day of a text YYYY-MM-DD, or null if it is no valid day. */
export function parseDay(value: string | null | undefined): CalendarDay | null {
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value?.trim() ?? '')
    if (!match) {
        return null
    }

    const year = Number(match[1])
    const month = Number(match[2])
    const day = Number(match[3])
    if (month < 1 || month > 12 || day < 1 || day > daysInMonth(year, month)) {
        return null
    }

    return { year, month, day }
}

/** The day as YYYY-MM-DD. */
export function formatDay(day: CalendarDay): string {
    const pad = (value: number, length: number) => String(value).padStart(length, '0')
    return `${pad(day.year, 4)}-${pad(day.month, 2)}-${pad(day.day, 2)}`
}

/** Negative if a is before b, 0 for the same day, positive if a is after b. */
export function compareDays(a: CalendarDay, b: CalendarDay): number {
    return a.year - b.year || a.month - b.month || a.day - b.day
}

export function addDays(day: CalendarDay, days: number): CalendarDay {
    const date = new Date(Date.UTC(day.year, day.month - 1, day.day + days))
    return { year: date.getUTCFullYear(), month: date.getUTCMonth() + 1, day: date.getUTCDate() }
}

/** The same day some months later or earlier; the 31st becomes the last day of a shorter month. */
export function addMonths(day: CalendarDay, months: number): CalendarDay {
    const index = day.year * 12 + (day.month - 1) + months
    const year = Math.floor(index / 12)
    const month = index - year * 12 + 1
    return { year, month, day: Math.min(day.day, daysInMonth(year, month)) }
}

/** The day as a date at midnight UTC, for formatting it without a shift by the time zone. */
export function dayToDate(day: CalendarDay): Date {
    return new Date(Date.UTC(day.year, day.month - 1, day.day))
}

/** Today in the time zone. */
export function todayIn(timeZone: string, now: Date = new Date()): CalendarDay {
    const parts = new Intl.DateTimeFormat('en-US', {
        timeZone,
        year: 'numeric',
        month: 'numeric',
        day: 'numeric',
    }).formatToParts(now)
    const value = (type: string) => Number(parts.find((part) => part.type === type)?.value)
    return { year: value('year'), month: value('month'), day: value('day') }
}

/** IANA time zone of the browser, for example Europe/Berlin; UTC if it has none. */
export function browserTimeZone(): string {
    try {
        return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC'
    } catch {
        return 'UTC'
    }
}

/**
 * The days of a period that ends today: the last month, three months and year end today and
 * start the day after the same day of the earlier month or year (2026-10-21 to 2026-11-20). A
 * custom period needs two valid days within the limits of the API, the first not after the last;
 * otherwise the result is null.
 */
export function periodRange(
    period: RecapPeriod,
    today: CalendarDay,
    custom?: CustomRange,
): PeriodRange | null {
    switch (period) {
        case RecapPeriodObject.LastMonth:
            return { from: addDays(addMonths(today, -1), 1), to: today }
        case RecapPeriodObject.LastThreeMonths:
            return { from: addDays(addMonths(today, -3), 1), to: today }
        case RecapPeriodObject.LastYear:
            return { from: addDays(addMonths(today, -12), 1), to: today }
        case RecapPeriodObject.CurrentYear:
            return { from: { year: today.year, month: 1, day: 1 }, to: today }
        case RecapPeriodObject.AllTime:
            return { from: null, to: today }
        default: {
            const from = parseDay(custom?.from)
            const to = parseDay(custom?.to)
            if (
                !from ||
                !to ||
                compareDays(from, to) > 0 ||
                compareDays(from, firstDay) < 0 ||
                compareDays(to, lastDay) > 0
            ) {
                return null
            }
            return { from, to }
        }
    }
}

/** The default custom period: the last 30 days. */
export function defaultCustomRange(today: CalendarDay): CustomRange {
    return { from: formatDay(addDays(today, -29)), to: formatDay(today) }
}
