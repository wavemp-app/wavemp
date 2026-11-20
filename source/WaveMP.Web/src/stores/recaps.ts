import { defineStore } from 'pinia'
import { computed, ref, watch } from 'vue'
import {
    RecapShareScopeObject,
    type ListeningStatistics,
    type RecapPeriod,
    type RecapShare,
} from '@/api/generated/models'
import { fetchAllPages } from '@/api/paging'
import { getStatistics, listRecapShares, shareRecap, stopSharingRecap } from '@/recaps/api'
import {
    browserTimeZone,
    compareDays,
    defaultCustomRange,
    defaultRecapPeriod,
    formatDay,
    periodRange,
    todayIn,
    type CalendarDay,
    type CustomRange,
    type PeriodRange,
} from '@/recaps/periods'
import { useSessionStore } from '@/stores/session'

export type RecapStatus = 'idle' | 'loading' | 'ready' | 'error' | 'invalid'

function sameDay(value: unknown, day: CalendarDay | null): boolean {
    const other = value as Partial<CalendarDay> | null | undefined
    if (!day || !other) {
        return !day && !other
    }
    return other.year === day.year && other.month === day.month && other.day === day.day
}

function keyOf(range: PeriodRange): string {
    return `${range.from ? formatDay(range.from) : ''}/${formatDay(range.to)}`
}

/**
 * The period of the recap and the statistics, shared by both pages, the statistics of that period
 * and the recaps the profile shares with the other profiles. Days and hours are counted in the
 * time zone of the browser; the rolling periods end today.
 */
export const useRecapsStore = defineStore('recaps', () => {
    const session = useSessionStore()
    const timeZone = browserTimeZone()
    const today = ref<CalendarDay>(todayIn(timeZone))
    const period = ref<RecapPeriod>(defaultRecapPeriod)
    const custom = ref<CustomRange>(defaultCustomRange(today.value))
    const statistics = ref<ListeningStatistics | null>(null)
    const status = ref<RecapStatus>('idle')
    let loadedKey: string | null = null
    let generation = 0

    const range = computed(() => periodRange(period.value, today.value, custom.value))

    /** Loads the statistics of the period unless they are loaded or loading already. */
    async function load(force = false): Promise<void> {
        // A new day moves the rolling periods; the same day keeps the range (and its watchers).
        const now = todayIn(timeZone)
        if (compareDays(now, today.value) !== 0) {
            today.value = now
        }
        const current = range.value
        if (!current) {
            generation++
            loadedKey = null
            statistics.value = null
            status.value = 'invalid'
            return
        }

        const key = keyOf(current)
        if (!force && key === loadedKey && status.value !== 'error') {
            return
        }

        const run = ++generation
        loadedKey = key
        status.value = 'loading'
        try {
            const result = await getStatistics({ from: current.from, to: current.to, timeZone })
            if (run === generation) {
                statistics.value = result ?? null
                status.value = 'ready'
            }
        } catch {
            if (run === generation) {
                loadedKey = null
                status.value = 'error'
            }
        }
    }

    function setPeriod(value: RecapPeriod): void {
        period.value = value
    }

    function setCustom(value: CustomRange): void {
        custom.value = { ...value }
    }

    const ownShares = ref<RecapShare[]>([])
    const sharesStatus = ref<'idle' | 'loading' | 'ready' | 'error'>('idle')

    async function loadOwnShares(): Promise<void> {
        sharesStatus.value = 'loading'
        try {
            ownShares.value = await fetchAllPages((offset, limit) =>
                listRecapShares(RecapShareScopeObject.Own, offset, limit),
            )
            sharesStatus.value = 'ready'
        } catch {
            sharesStatus.value = 'error'
        }
    }

    /** The share of the shown period, if the profile shares it. */
    const currentShare = computed(() => {
        const current = range.value
        if (!current) {
            return undefined
        }
        return ownShares.value.find(
            (share) =>
                share.period === period.value &&
                share.timeZone === timeZone &&
                sameDay(share.from, current.from) &&
                sameDay(share.to, current.to),
        )
    })

    /** Shares the shown period with the other profiles, or changes the design of its share. */
    async function share(design: string): Promise<void> {
        const current = range.value
        if (!current) {
            return
        }
        const shared = await shareRecap({
            period: period.value,
            from: current.from,
            to: current.to,
            timeZone,
            design,
        })
        if (shared?.id) {
            ownShares.value = [shared, ...ownShares.value.filter((item) => item.id !== shared.id)]
        }
    }

    async function stopSharing(id: string): Promise<void> {
        await stopSharingRecap(id)
        ownShares.value = ownShares.value.filter((item) => item.id !== id)
    }

    function clear(): void {
        generation++
        loadedKey = null
        statistics.value = null
        status.value = 'idle'
        ownShares.value = []
        sharesStatus.value = 'idle'
    }

    watch(
        () => session.token,
        () => clear(),
    )

    return {
        timeZone,
        period,
        custom,
        range,
        statistics,
        status,
        load,
        setPeriod,
        setCustom,
        ownShares,
        sharesStatus,
        loadOwnShares,
        currentShare,
        share,
        stopSharing,
    }
})
