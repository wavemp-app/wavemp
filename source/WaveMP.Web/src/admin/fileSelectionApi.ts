// Requests of the file selection per song (admins only). The only place of the web app that calls
// these endpoints, so components and tests can replace it as a whole
// (vi.mock('@/admin/fileSelectionApi')).

import { ref } from 'vue'
import { apiClient } from '@/api/client'
import type { FileSelectionsRequestBuilderGetQueryParameters } from '@/api/generated/api/v1/fileSelections'
import type {
    TrackFileSelection,
    TrackFileSummaryPage,
    UpdateTrackFileSelection,
} from '@/api/generated/models'

export type FileSelectionQuery = FileSelectionsRequestBuilderGetQueryParameters

/**
 * Counts the changes of a file selection in this tab, so the overview kept in the background
 * loads its list again when the admin comes back after a change.
 */
export const fileSelectionRevision = ref(0)

/** The songs with the number of their files per category and the state of their selection. */
export function listFileSelections(
    query: FileSelectionQuery,
): Promise<TrackFileSummaryPage | undefined> {
    return apiClient.api.v1.fileSelections.get({ queryParameters: query })
}

/** Which audio, video and cover file a song uses, and which files it can use. */
export function getTrackFileSelection(id: string): Promise<TrackFileSelection | undefined> {
    return apiClient.api.v1.tracks.byId(id).fileSelection.get()
}

/** Replaces the selection of a song; a category without a file id uses the automatic choice. */
export async function setTrackFileSelection(
    id: string,
    selection: UpdateTrackFileSelection,
): Promise<TrackFileSelection | undefined> {
    const result = await apiClient.api.v1.tracks.byId(id).fileSelection.put(selection)
    fileSelectionRevision.value++
    return result
}

/** Removes the selection of a song, so every category uses the automatic choice again. */
export async function resetTrackFileSelection(id: string): Promise<void> {
    await apiClient.api.v1.tracks.byId(id).fileSelection.delete()
    fileSelectionRevision.value++
}
