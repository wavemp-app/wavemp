// Requests of the statistics, the shared recaps and the conversion of recap videos. The only place
// of the web app that calls these endpoints, so components and tests can replace it as a whole
// (vi.mock('@/recaps/api')).

import { DateOnly } from '@microsoft/kiota-abstractions'
import { apiClient } from '@/api/client'
import type {
    ListeningStatistics,
    RecapPeriod,
    RecapShare,
    RecapSharePage,
    RecapShareScope,
    SharedRecap,
} from '@/api/generated/models'
import type { CalendarDay } from './periods'

/** Length of the rankings of the statistics page; the story shows the first five. */
export const statisticsRankingLength = 25

/** The story of a shared recap needs five entries per ranking. */
const storyRankingTop = 5

export interface StatisticsRequest {
    readonly from: CalendarDay | null
    readonly to: CalendarDay
    readonly timeZone: string
    readonly top?: number
}

function toDateOnly(day: CalendarDay): DateOnly {
    return new DateOnly(day)
}

export function getStatistics(
    request: StatisticsRequest,
): Promise<ListeningStatistics | undefined> {
    return apiClient.api.v1.me.stats.get({
        queryParameters: {
            from: request.from ? toDateOnly(request.from) : undefined,
            to: toDateOnly(request.to),
            timeZone: request.timeZone,
            top: request.top ?? statisticsRankingLength,
        },
    })
}

export interface ShareRequest {
    readonly period: RecapPeriod
    readonly from: CalendarDay | null
    readonly to: CalendarDay
    readonly timeZone: string
    readonly design: string
}

export function listRecapShares(
    scope: RecapShareScope,
    offset: number,
    limit: number,
): Promise<RecapSharePage | undefined> {
    return apiClient.api.v1.recapShares.get({ queryParameters: { scope, offset, limit } })
}

/** Shares a recap with the other profiles; sharing the same days again changes its design. */
export function shareRecap(request: ShareRequest): Promise<RecapShare | undefined> {
    return apiClient.api.v1.recapShares.post({
        period: request.period,
        from: request.from ? toDateOnly(request.from) : undefined,
        to: toDateOnly(request.to),
        timeZone: request.timeZone,
        design: request.design,
    })
}

export async function stopSharingRecap(id: string): Promise<void> {
    await apiClient.api.v1.recapShares.byId(id).delete()
}

export function getSharedRecap(
    id: string,
    top = storyRankingTop,
): Promise<SharedRecap | undefined> {
    return apiClient.api.v1.recapShares.byId(id).statistics.get({ queryParameters: { top } })
}

/** Converts a recorded video (WebM, Matroska or MP4) into MP4 with H.264 and AAC. */
export async function convertRecapVideo(video: Blob): Promise<Blob> {
    const type = video.type.split(';')[0]?.trim() || 'video/webm'
    const converted = await apiClient.api.v1.me.recapVideos.post(await video.arrayBuffer(), type)
    if (!converted) {
        throw new Error('The server returned no video.')
    }
    return new Blob([converted], { type: 'video/mp4' })
}
