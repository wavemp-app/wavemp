// Texts of shared recaps: the days of a share for lists and titles.

import type { RecapShare } from '@/api/generated/models'
import { dayOf, formatRange, type RecapTexts } from './slides'

/** The days of a share, e.g. "Oct 21 – Nov 20, 2026", or "Until Nov 20, 2026" for all time. */
export function shareDays(texts: RecapTexts, share: RecapShare): string {
    const from = dayOf(share.from)
    const to = dayOf(share.to)
    if (!to) {
        return ''
    }
    if (!from) {
        return texts.t('recap.until', {
            date: new Intl.DateTimeFormat(texts.locale, {
                dateStyle: 'medium',
                timeZone: 'UTC',
            }).format(new Date(Date.UTC(to.year, to.month - 1, to.day))),
        })
    }
    return formatRange(texts, from, to)
}
