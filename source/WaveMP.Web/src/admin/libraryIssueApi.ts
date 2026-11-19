// Requests of the library check (admins only): the problems it found, their numbers per type, the
// job that finds them and its manual start. The only place of the web app that calls these
// endpoints, so components and tests can replace it as a whole (vi.mock('@/admin/libraryIssueApi')).

import { apiClient } from '@/api/client'
import type { IssuesRequestBuilderGetQueryParameters } from '@/api/generated/api/v1/library/issues'
import {
    JobIdObject,
    type Job,
    type LibraryIssuePage,
    type LibraryIssueSummary,
} from '@/api/generated/models'

export type LibraryIssueQuery = IssuesRequestBuilderGetQueryParameters

/** One page of the problems of the library, filtered, searched and sorted. */
export function listLibraryIssues(query: LibraryIssueQuery): Promise<LibraryIssuePage | undefined> {
    return apiClient.api.v1.library.issues.get({ queryParameters: query })
}

/** The number of problems per type and when the check ran last. */
export function getLibraryIssueSummary(): Promise<LibraryIssueSummary | undefined> {
    return apiClient.api.v1.library.issues.summary.get()
}

/** The job of the library check with its state and last run. */
export function getLibraryCheckJob(): Promise<Job | undefined> {
    return apiClient.api.v1.jobs.byId(JobIdObject.LibraryCheck).get()
}

/** Starts the library check now; it runs in the background. */
export async function startLibraryCheck(): Promise<void> {
    await apiClient.api.v1.jobs.byId(JobIdObject.LibraryCheck).runs.post()
}
