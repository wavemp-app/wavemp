// Requests of the avatars. The only place of the web app that calls the avatar endpoints, so
// components and tests can replace it as a whole (vi.mock('@/profiles/avatarApi')).

import { MultipartBody } from '@microsoft/kiota-abstractions'
import { apiClient } from '@/api/client'
import type { Profile, User } from '@/api/generated/models'
import type { CoverSize } from '@/library/covers'

/**
 * Whose avatar changes: the profile of the session, or (for admins) any profile by its id.
 */
export type AvatarTarget = { readonly kind: 'me' } | { readonly kind: 'user'; readonly id: string }

/** The avatar of a profile as square WebP in the size; available without a session. */
export function fetchAvatar(userId: string, size: CoverSize): Promise<ArrayBuffer | undefined> {
    return apiClient.api.v1.users.byId(userId).avatar.get({ queryParameters: { size } })
}

/** A built-in avatar as square WebP in the size. */
export function fetchAvatarPreset(id: string, size: CoverSize): Promise<ArrayBuffer | undefined> {
    return apiClient.api.v1.avatarPresets.byId(id).picture.get({ queryParameters: { size } })
}

/** Page size for the built-in avatars; there are only a few, so one page holds them all. */
const presetPageSize = 100

/** The ids of the built-in avatars in the order the API offers them. */
export async function listAvatarPresets(): Promise<string[]> {
    const page = await apiClient.api.v1.avatarPresets.get({
        queryParameters: { limit: presetPageSize },
    })
    return (page?.items ?? [])
        .map((preset) => preset.id)
        .filter((id): id is string => typeof id === 'string' && id !== '')
}

function send(target: AvatarTarget, body: MultipartBody): Promise<Profile | User | undefined> {
    return target.kind === 'me'
        ? apiClient.api.v1.me.avatar.put(body)
        : apiClient.api.v1.users.byId(target.id).avatar.put(body)
}

/** Uploads a JPEG, PNG or WebP picture as avatar; the server crops it to a square. */
export async function uploadAvatar(
    target: AvatarTarget,
    picture: Blob,
    fileName: string,
): Promise<Profile | User | undefined> {
    const body = new MultipartBody()
    body.addOrReplacePart('file', picture.type, await picture.arrayBuffer(), undefined, fileName)
    return send(target, body)
}

/** Sets a built-in avatar. */
export function chooseAvatarPreset(
    target: AvatarTarget,
    presetId: string,
): Promise<Profile | User | undefined> {
    const body = new MultipartBody()
    body.addOrReplacePart('presetId', 'text/plain', presetId)
    return send(target, body)
}

/** Removes the avatar; the profile shows its initials again. */
export async function removeAvatar(target: AvatarTarget): Promise<void> {
    if (target.kind === 'me') {
        await apiClient.api.v1.me.avatar.delete()
    } else {
        await apiClient.api.v1.users.byId(target.id).avatar.delete()
    }
}
