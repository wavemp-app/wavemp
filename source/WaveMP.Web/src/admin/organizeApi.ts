// Requests of the organization of the media files (admins only): the layout template and its
// preview, whether the media folders can be written, the previews of own albums, of the library
// and of undos, applying them, the log and the items of an operation. The only place of the web
// app that calls these endpoints, so components and tests can replace it as a whole
// (vi.mock('@/admin/organizeApi')).

import { apiClient } from '@/api/client'
import type { OperationsRequestBuilderGetQueryParameters } from '@/api/generated/api/v1/organize/operations'
import type { ItemsRequestBuilderGetQueryParameters } from '@/api/generated/api/v1/organize/operations/item/items'
import type {
    CreateOrganizePreview,
    LayoutTemplatePreview,
    OrganizeAvailability,
    OrganizeItemPage,
    OrganizeOperation,
    OrganizeOperationPage,
    OrganizingSettings,
    PreviewLayoutTemplate,
    UpdateOrganizingSettings,
} from '@/api/generated/models'

export type OrganizeOperationQuery = OperationsRequestBuilderGetQueryParameters
export type OrganizeItemQuery = ItemsRequestBuilderGetQueryParameters

const organize = () => apiClient.api.v1.organize

/** The layout template of the files and whether it lays them out in folders. */
export function getOrganizingSettings(): Promise<OrganizingSettings | undefined> {
    return apiClient.api.v1.settings.organizing.get()
}

/** Saves the layout template; nothing moves until an organization is applied. */
export function updateOrganizingSettings(
    changes: UpdateOrganizingSettings,
): Promise<OrganizingSettings | undefined> {
    return apiClient.api.v1.settings.organizing.patch(changes)
}

/** Where a template would put songs of the library or example songs; nothing is saved. */
export function previewLayoutTemplate(
    request: PreviewLayoutTemplate,
): Promise<LayoutTemplatePreview | undefined> {
    return organize().templatePreview.post(request)
}

/** Whether each media folder can be organized, and the operation that is queued or running. */
export function getOrganizeAvailability(): Promise<OrganizeAvailability | undefined> {
    return organize().availability.get()
}

/** Computes the preview of an own album (at once) or of the library (in the background). */
export function createOrganizePreview(
    request: CreateOrganizePreview,
): Promise<OrganizeOperation | undefined> {
    return organize().preview.post(request)
}

/** Computes the preview that undoes an operation of the log. */
export function createOrganizeUndoPreview(id: string): Promise<OrganizeOperation | undefined> {
    return organize().undo.byId(id).post()
}

/** Moves the files of a preview; the run continues in the background. */
export function applyOrganizeOperation(
    operationId: string,
    skipConflicts: boolean,
): Promise<OrganizeOperation | undefined> {
    return organize().apply.post({ operationId, skipConflicts })
}

/** The log: previews, runs and undos, newest first. */
export function listOrganizeOperations(
    query: OrganizeOperationQuery,
): Promise<OrganizeOperationPage | undefined> {
    return organize().operations.get({ queryParameters: query })
}

export function getOrganizeOperation(id: string): Promise<OrganizeOperation | undefined> {
    return organize().operations.byId(id).get()
}

/** The items of an operation in their order: all, or only those with a conflict or warning. */
export function listOrganizeItems(
    id: string,
    query: OrganizeItemQuery,
): Promise<OrganizeItemPage | undefined> {
    return organize().operations.byId(id).items.get({ queryParameters: query })
}

/** Stops a queued or running operation after the song it is moving. */
export function cancelOrganizeOperation(id: string): Promise<OrganizeOperation | undefined> {
    return organize().operations.byId(id).cancel.post()
}

/** Throws a preview away that will not be applied. */
export async function discardOrganizeOperation(id: string): Promise<void> {
    await organize().operations.byId(id).delete()
}
