// Message keys and data-item values of the periods.

import { RecapPeriodObject, type RecapPeriod } from '@/api/generated/models'

const labelKeys: Record<RecapPeriod, string> = {
    [RecapPeriodObject.LastMonth]: 'recap.periods.lastMonth',
    [RecapPeriodObject.LastThreeMonths]: 'recap.periods.lastThreeMonths',
    [RecapPeriodObject.LastYear]: 'recap.periods.lastYear',
    [RecapPeriodObject.CurrentYear]: 'recap.periods.currentYear',
    [RecapPeriodObject.AllTime]: 'recap.periods.allTime',
    [RecapPeriodObject.Custom]: 'recap.periods.custom',
}

/** Message key of the name of a period, e.g. "Last month". */
export function periodLabelKey(period: RecapPeriod | null | undefined): string {
    return (period && labelKeys[period]) || labelKeys[RecapPeriodObject.Custom]
}

/** data-item of a period, e.g. last-three-months. */
export function periodItem(period: RecapPeriod): string {
    return period.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase()
}
