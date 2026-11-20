import { describe, expect, it } from 'vitest'
import { RecapPeriodObject } from '@/api/generated/models'
import {
    addDays,
    addMonths,
    compareDays,
    defaultCustomRange,
    formatDay,
    isRecapPeriod,
    parseDay,
    periodRange,
    recapPeriods,
    todayIn,
} from '../periods'

const today = { year: 2026, month: 11, day: 20 }

describe('periods of the recap', () => {
    it('lists all six periods with the last month first', () => {
        expect(recapPeriods).toEqual([
            RecapPeriodObject.LastMonth,
            RecapPeriodObject.LastThreeMonths,
            RecapPeriodObject.LastYear,
            RecapPeriodObject.CurrentYear,
            RecapPeriodObject.AllTime,
            RecapPeriodObject.Custom,
        ])
        expect(isRecapPeriod('LastYear')).toBe(true)
        expect(isRecapPeriod('Yesterday')).toBe(false)
        expect(isRecapPeriod(null)).toBe(false)
    })

    it('lets the rolling periods end today and start the day after the earlier date', () => {
        expect(periodRange(RecapPeriodObject.LastMonth, today)).toEqual({
            from: { year: 2026, month: 10, day: 21 },
            to: today,
        })
        expect(periodRange(RecapPeriodObject.LastThreeMonths, today)).toEqual({
            from: { year: 2026, month: 8, day: 21 },
            to: today,
        })
        expect(periodRange(RecapPeriodObject.LastYear, today)).toEqual({
            from: { year: 2025, month: 11, day: 21 },
            to: today,
        })
    })

    it('starts the current year on January 1 and all time without a first day', () => {
        expect(periodRange(RecapPeriodObject.CurrentYear, today)).toEqual({
            from: { year: 2026, month: 1, day: 1 },
            to: today,
        })
        expect(periodRange(RecapPeriodObject.AllTime, today)).toEqual({ from: null, to: today })
    })

    it('crosses the turn of the year and short months', () => {
        expect(periodRange(RecapPeriodObject.LastMonth, { year: 2026, month: 1, day: 10 })).toEqual(
            { from: { year: 2025, month: 12, day: 11 }, to: { year: 2026, month: 1, day: 10 } },
        )
        // March 31 minus one month is February 28; the period starts the day after.
        expect(
            periodRange(RecapPeriodObject.LastMonth, { year: 2026, month: 3, day: 31 })?.from,
        ).toEqual({ year: 2026, month: 3, day: 1 })
        expect(addMonths({ year: 2024, month: 3, day: 31 }, -1)).toEqual({
            year: 2024,
            month: 2,
            day: 29,
        })
    })

    it('accepts a valid custom period only', () => {
        expect(
            periodRange(RecapPeriodObject.Custom, today, { from: '2026-02-01', to: '2026-02-28' }),
        ).toEqual({ from: { year: 2026, month: 2, day: 1 }, to: { year: 2026, month: 2, day: 28 } })
        expect(
            periodRange(RecapPeriodObject.Custom, today, { from: '2026-03-01', to: '2026-02-28' }),
        ).toBeNull()
        expect(
            periodRange(RecapPeriodObject.Custom, today, { from: '2026-02-30', to: '2026-03-01' }),
        ).toBeNull()
        expect(
            periodRange(RecapPeriodObject.Custom, today, { from: '1899-12-31', to: '2026-03-01' }),
        ).toBeNull()
        expect(periodRange(RecapPeriodObject.Custom, today, { from: '', to: '' })).toBeNull()
        expect(periodRange(RecapPeriodObject.Custom, today)).toBeNull()
    })

    it('reads, writes, compares and moves days', () => {
        expect(parseDay('2024-02-29')).toEqual({ year: 2024, month: 2, day: 29 })
        expect(parseDay('2025-02-29')).toBeNull()
        expect(parseDay('2025-13-01')).toBeNull()
        expect(parseDay('20.11.2026')).toBeNull()
        expect(formatDay({ year: 987, month: 3, day: 4 })).toBe('0987-03-04')
        expect(compareDays(today, { year: 2026, month: 11, day: 21 })).toBeLessThan(0)
        expect(compareDays(today, today)).toBe(0)
        expect(addDays({ year: 2026, month: 12, day: 31 }, 1)).toEqual({
            year: 2027,
            month: 1,
            day: 1,
        })
    })

    it('offers the last 30 days as custom period', () => {
        expect(defaultCustomRange(today)).toEqual({ from: '2026-10-22', to: '2026-11-20' })
    })

    it('finds today in a time zone', () => {
        const now = new Date(Date.UTC(2026, 10, 20, 23, 30))
        expect(todayIn('UTC', now)).toEqual(today)
        expect(todayIn('Europe/Berlin', now)).toEqual({ year: 2026, month: 11, day: 21 })
        expect(todayIn('America/New_York', now)).toEqual(today)
    })
})
