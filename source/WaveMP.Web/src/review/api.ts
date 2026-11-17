// Requests of the review inbox (admins only). The only place of the web app that calls these
// endpoints, so components and tests can replace it as a whole (vi.mock('@/review/api')).

import { apiClient } from '@/api/client'
import type { TrackReviewsRequestBuilderGetQueryParameters } from '@/api/generated/api/v1/trackReviews'
import type {
    TrackReviewPage,
    TrackReviewState,
    TrackReviewStatus,
    TrackReviewSummary,
    UpdatedTrackReviews,
} from '@/api/generated/models'

export type TrackReviewQuery = TrackReviewsRequestBuilderGetQueryParameters

/** The songs of a review status (unreviewed by default) with the tags their file lacks. */
export function listTrackReviews(query: TrackReviewQuery): Promise<TrackReviewPage | undefined> {
    return apiClient.api.v1.trackReviews.get({ queryParameters: query })
}

/** The number of unreviewed and reviewed songs. */
export function getTrackReviewSummary(): Promise<TrackReviewSummary | undefined> {
    return apiClient.api.v1.trackReviews.summary.get()
}

/** The review status of a song. */
export function getTrackReview(id: string): Promise<TrackReviewState | undefined> {
    return apiClient.api.v1.tracks.byId(id).review.get()
}

/** Sets the review status of a song; reviewed by the current profile now. */
export function setTrackReview(
    id: string,
    status: TrackReviewStatus,
): Promise<TrackReviewState | undefined> {
    return apiClient.api.v1.tracks.byId(id).review.put({ status })
}

/** Sets the review status of up to 500 songs at once. */
export function setTrackReviews(
    trackIds: readonly string[],
    status: TrackReviewStatus,
): Promise<UpdatedTrackReviews | undefined> {
    return apiClient.api.v1.trackReviews.batch.post({ trackIds: [...trackIds], status })
}
