<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import {
    OwnAlbumTrackStateObject,
    type OrganizeOperation,
    type OwnAlbum,
    type Track,
    type UpdateOwnAlbum,
} from '@/api/generated/models'
import { fetchAllPages } from '@/api/paging'
import { problemCodeOf, statusOf, validationErrorsOf } from '@/api/problems'
import { organizeProblemKey } from '@/admin/organize'
import {
    createOwnAlbum,
    deleteOwnAlbum,
    deleteOwnAlbumCover,
    getOwnAlbum,
    listOwnAlbumTracks,
    maxOwnAlbumTracks,
    setOwnAlbumTracks,
    updateOwnAlbum,
    uploadOwnAlbumCover,
} from '@/admin/ownAlbumApi'
import AdminNav from '@/components/admin/AdminNav.vue'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import BaseTextField from '@/components/base/BaseTextField.vue'
import DetailHeader from '@/components/library/DetailHeader.vue'
import MetaItem from '@/components/library/MetaItem.vue'
import OrganizePreviewDialog from '@/components/organize/OrganizePreviewDialog.vue'
import OwnAlbumCoverField from '@/components/organize/OwnAlbumCoverField.vue'
import OwnAlbumTrackList from '@/components/organize/OwnAlbumTrackList.vue'
import TrackPickerDialog from '@/components/organize/TrackPickerDialog.vue'
import { useDetailTitle } from '@/composables/useDetailTitle'
import { validationMessagesByField } from '@/i18n/validation'
import { getTrack } from '@/library/api'
import { coverProblemKey, coverUploadProblem, moveItem } from '@/playlists/presentation'
import { useOrganizeStore } from '@/stores/organize'

/**
 * Editor of an own album (admins): the cover (drop zone), name, album artist, year and genre, and
 * the songs in their order (drag and drop, buttons and Alt+Arrow keys; "Add songs" opens a search).
 * Saving changes no file. "Preview changes" saves and shows where the files go and which tags are
 * written; only "Move files" in that dialog changes the files. Without id it creates a new album,
 * with the songs chosen in a menu, if any.
 */
const props = defineProps<{
    id?: string
}>()

type Field = 'title' | 'albumArtist' | 'year' | 'genre'

const { t } = useI18n()
const router = useRouter()
const organize = useOrganizeStore()

const status = ref<'loading' | 'ready' | 'error' | 'notFound'>('loading')
const album = ref<OwnAlbum | null>(null)
const title = ref('')
const albumArtist = ref('')
const year = ref('')
const genre = ref('')
const tracks = ref<Track[]>([])
const savedTrackIds = ref<string[]>([])
const coverFile = ref<File | null>(null)
const removeCover = ref(false)
const saving = ref(false)
const invalid = ref<Partial<Record<Field, string>>>({})
const message = ref<{ tone: 'error' | 'success' | 'info'; text: string } | null>(null)
const pickerOpen = ref(false)
const deleteOpen = ref(false)
const dialogOpen = ref(false)
const preview = ref<OrganizeOperation | null>(null)

const isNew = computed(() => !props.id)
const trackIds = computed(() => tracks.value.flatMap((track) => (track.id ? [track.id] : [])))
const tracksChanged = computed(() => trackIds.value.join(',') !== savedTrackIds.value.join(','))
const valuesChanged = computed(
    () =>
        title.value.trim() !== (album.value?.title ?? '') ||
        albumArtist.value.trim() !== (album.value?.albumArtist ?? '') ||
        year.value.trim() !== (album.value?.year ? String(album.value.year) : '') ||
        genre.value.trim() !== (album.value?.genre ?? ''),
)
const dirty = computed(
    () =>
        isNew.value ||
        valuesChanged.value ||
        tracksChanged.value ||
        !!coverFile.value ||
        removeCover.value,
)
const blocked = computed(() => organize.blocked)
const pageTitle = computed(() =>
    isNew.value ? t('ownAlbums.newTitle') : (album.value?.title ?? t('ownAlbums.title')),
)

useDetailTitle(() => (status.value === 'ready' ? pageTitle.value : null))

function show(value: OwnAlbum | null | undefined): void {
    album.value = value ?? null
    title.value = value?.title ?? ''
    albumArtist.value = value?.albumArtist ?? ''
    year.value = value?.year ? String(value.year) : ''
    genre.value = value?.genre ?? ''
    coverFile.value = null
    removeCover.value = false
}

async function loadTracks(id: string): Promise<void> {
    const rows = await fetchAllPages((offset, limit) => listOwnAlbumTracks(id, offset, limit))
    tracks.value = rows
        .filter((row) => row.state !== OwnAlbumTrackStateObject.Removed && row.track)
        .sort((a, b) => (a.position ?? 0) - (b.position ?? 0))
        .map((row) => row.track!)
    savedTrackIds.value = [...trackIds.value]
}

async function load(): Promise<void> {
    status.value = 'loading'
    message.value = null
    invalid.value = {}
    void organize.loadAvailability()
    try {
        if (props.id) {
            const loaded = await getOwnAlbum(props.id)
            show(loaded)
            await loadTracks(props.id)
        } else {
            show(null)
            const pending = organize.takePendingTracks()
            const found = await Promise.all(
                pending.map((id) => getTrack(id).catch(() => undefined)),
            )
            tracks.value = found.filter((track): track is Track => !!track && !!track.hasAudio)
            savedTrackIds.value = []
        }
        status.value = 'ready'
    } catch (failure) {
        status.value = statusOf(failure) === 404 ? 'notFound' : 'error'
    }
}

watch(() => props.id, load, { immediate: true })

function validate(): boolean {
    const errors: Partial<Record<Field, string>> = {}
    if (!title.value.trim()) {
        errors.title = t('validation.required')
    }
    if (!albumArtist.value.trim()) {
        errors.albumArtist = t('validation.required')
    }
    const typedYear = year.value.trim()
    if (typedYear && !/^\d{1,4}$/.test(typedYear)) {
        errors.year = t('ownAlbums.errors.year')
    } else if (typedYear && Number(typedYear) < 1) {
        errors.year = t('ownAlbums.errors.year')
    }
    invalid.value = errors
    return Object.keys(errors).length === 0
}

function showFailure(failure: unknown, fallback: string): void {
    const fields = validationMessagesByField<Field>(
        validationErrorsOf(failure),
        { title: 'title', albumArtist: 'albumArtist', year: 'year', genre: 'genre' },
        (key, parameters) => t(key, parameters),
    )
    if (Object.keys(fields).length > 0) {
        invalid.value = fields
        return
    }

    message.value = { tone: 'error', text: t(organizeProblemKey(problemCodeOf(failure), fallback)) }
}

/** Saves the album, its cover and its songs; returns the id, or null if it failed. */
async function save(): Promise<string | null> {
    if (saving.value || !validate()) {
        return null
    }

    saving.value = true
    message.value = null
    try {
        let current = album.value
        const typedYear = year.value.trim()
        if (!current?.id) {
            current =
                (await createOwnAlbum({
                    title: title.value.trim(),
                    albumArtist: albumArtist.value.trim(),
                    ...(typedYear ? { year: Number(typedYear) } : {}),
                    ...(genre.value.trim() ? { genre: genre.value.trim() } : {}),
                })) ?? null
        } else if (valuesChanged.value) {
            const changes: UpdateOwnAlbum = {}
            if (title.value.trim() !== current.title) changes.title = title.value.trim()
            if (albumArtist.value.trim() !== current.albumArtist)
                changes.albumArtist = albumArtist.value.trim()
            if (typedYear !== (current.year ? String(current.year) : ''))
                changes.year = typedYear ? Number(typedYear) : 0
            if (genre.value.trim() !== (current.genre ?? '')) changes.genre = genre.value.trim()
            current = (await updateOwnAlbum(current.id, changes)) ?? current
        }

        const id = current?.id
        if (!id) {
            throw new Error('The own album has no id.')
        }

        if (coverFile.value) {
            current =
                (await uploadOwnAlbumCover(
                    id,
                    await coverFile.value.arrayBuffer(),
                    coverFile.value.type,
                )) ?? current
        } else if (removeCover.value) {
            await deleteOwnAlbumCover(id)
            current = { ...current, hasCover: false }
        }

        if (tracksChanged.value) {
            current = (await setOwnAlbumTracks(id, trackIds.value)) ?? current
            savedTrackIds.value = [...trackIds.value]
        }

        show(current)
        void organize.loadOwnAlbums()
        if (isNew.value) {
            await router.replace({ name: 'admin-own-album', params: { id } })
        }
        message.value = { tone: 'success', text: t('ownAlbums.saved') }
        return id
    } catch (failure) {
        showFailure(failure, 'ownAlbums.errors.saveFailed')
        return null
    } finally {
        saving.value = false
    }
}

async function previewChanges(): Promise<void> {
    const id = dirty.value ? await save() : (album.value?.id ?? null)
    if (!id) {
        return
    }

    saving.value = true
    try {
        preview.value = await organize.previewOwnAlbum(id)
        message.value = null
        dialogOpen.value = !!preview.value
    } catch (failure) {
        showFailure(failure, 'organize.problems.previewFailed')
    } finally {
        saving.value = false
    }
}

function chooseCover(file: File): void {
    const problem = coverUploadProblem(file)
    if (problem) {
        message.value = { tone: 'error', text: t(coverProblemKey(problem)) }
        return
    }

    message.value = null
    coverFile.value = file
    removeCover.value = false
}

function clearCover(): void {
    coverFile.value = null
    removeCover.value = !!album.value?.hasCover
}

function move(from: number, to: number): void {
    tracks.value = moveItem(tracks.value, from, to)
}

function remove(index: number): void {
    tracks.value = tracks.value.filter((_, position) => position !== index)
}

function pick(picked: Track[]): void {
    const present = new Set(trackIds.value)
    const added = picked.filter((track) => track.id && !present.has(track.id))
    tracks.value = [...tracks.value, ...added].slice(0, maxOwnAlbumTracks)
}

async function confirmDelete(): Promise<void> {
    const id = album.value?.id
    if (!id) {
        return
    }

    saving.value = true
    try {
        await deleteOwnAlbum(id)
        deleteOpen.value = false
        void organize.loadOwnAlbums()
        void router.push({ name: 'admin-organize' })
    } catch (failure) {
        deleteOpen.value = false
        showFailure(failure, 'ownAlbums.errors.deleteFailed')
    } finally {
        saving.value = false
    }
}

/** The files move in the background; the organize page shows the progress. */
function applied(): void {
    void router.push({ name: 'admin-organize' })
}

async function discarded(): Promise<void> {
    if (album.value?.id) {
        show(await getOwnAlbum(album.value.id).catch(() => album.value))
    }
}
</script>

<template>
    <div
        data-part="view"
        data-view="admin-own-album"
        :data-state="isNew ? 'create' : 'edit'"
        :aria-busy="status === 'loading' || saving ? 'true' : undefined"
    >
        <AdminNav />
        <div v-if="status === 'notFound'" data-part="empty-state">
            <p data-part="empty-state-text">{{ t('organize.problems.ownAlbumNotFound') }}</p>
            <BaseButton
                :label="t('ownAlbums.toOverview')"
                @click="router.push({ name: 'admin-organize' })"
            />
        </div>
        <div v-else-if="status === 'error'" data-part="empty-state">
            <p data-part="empty-state-title">{{ t('ownAlbums.errors.loadFailed') }}</p>
            <BaseButton :label="t('library.retry')" @click="load" />
        </div>
        <template v-else-if="status === 'ready'">
            <DetailHeader :title="pageTitle" :back="{ name: 'admin-organize' }">
                <template #media>
                    <OwnAlbumCoverField
                        :id="album?.id"
                        :has-cover="!!album?.hasCover && !removeCover"
                        :version="album?.coverUpdatedAt?.toISOString() ?? null"
                        :file="coverFile"
                        :disabled="saving"
                        @choose="chooseCover"
                        @remove="clearCover"
                    />
                </template>
                <MetaItem v-if="album" field="status" :label="t('ownAlbums.status')">
                    {{ t(`ownAlbums.statuses.${(album.status ?? 'Draft').toLowerCase()}`) }}
                </MetaItem>
                <MetaItem
                    v-if="(album?.removedTrackCount ?? 0) > 0"
                    field="removed"
                    :label="t('ownAlbums.removedLabel')"
                >
                    {{ t('ownAlbums.removed', { count: album?.removedTrackCount ?? 0 }) }}
                </MetaItem>
            </DetailHeader>
            <BaseAlert v-if="message" :tone="message.tone" :message="message.text" />
            <BaseAlert v-if="blocked" tone="info" :message="t(`organize.blocked.${blocked}`)" />
            <form data-part="form" data-item="own-album" novalidate @submit.prevent="save">
                <BaseTextField
                    v-model="title"
                    data-field="title"
                    :label="t('ownAlbums.fields.title')"
                    :error="invalid.title"
                    :maxlength="512"
                    :disabled="saving"
                    required
                />
                <BaseTextField
                    v-model="albumArtist"
                    data-field="album-artist"
                    :label="t('ownAlbums.fields.albumArtist')"
                    :error="invalid.albumArtist"
                    :maxlength="512"
                    :disabled="saving"
                    required
                />
                <div data-part="field-row">
                    <BaseTextField
                        v-model="year"
                        data-field="year"
                        inputmode="numeric"
                        :label="t('ownAlbums.fields.year')"
                        :hint="t('ownAlbums.fields.keepHint')"
                        :error="invalid.year"
                        :maxlength="4"
                        :disabled="saving"
                    />
                    <BaseTextField
                        v-model="genre"
                        data-field="genre"
                        :label="t('ownAlbums.fields.genre')"
                        :hint="t('ownAlbums.fields.keepHint')"
                        :error="invalid.genre"
                        :maxlength="128"
                        :disabled="saving"
                    />
                </div>
                <section data-part="section" data-item="own-album-tracks">
                    <h2 data-part="section-title">{{ t('ownAlbums.tracks.title') }}</h2>
                    <p data-part="form-note">{{ t('ownAlbums.tracks.hint') }}</p>
                    <OwnAlbumTrackList
                        v-if="tracks.length > 0"
                        :tracks="tracks"
                        :disabled="saving"
                        @move="move"
                        @remove="remove"
                    />
                    <div v-else data-part="empty-state">
                        <p data-part="empty-state-text">{{ t('ownAlbums.tracks.empty') }}</p>
                    </div>
                    <div data-part="view-actions">
                        <BaseButton
                            data-item="add-songs"
                            icon="add"
                            :label="t('ownAlbums.tracks.add')"
                            :disabled="saving || tracks.length >= maxOwnAlbumTracks"
                            @click="pickerOpen = true"
                        />
                    </div>
                </section>
                <div data-part="view-actions" data-item="own-album-actions">
                    <BaseButton
                        v-if="!isNew"
                        data-item="delete"
                        icon="delete"
                        :label="t('ownAlbums.delete')"
                        :disabled="saving"
                        @click="deleteOpen = true"
                    />
                    <BaseButton
                        type="submit"
                        data-item="save"
                        :label="isNew ? t('ownAlbums.create') : t('ownAlbums.save')"
                        :disabled="saving || !dirty"
                    />
                    <BaseButton
                        data-item="preview-changes"
                        variant="primary"
                        icon="organize-preview"
                        :label="t('ownAlbums.preview')"
                        :disabled="saving || tracks.length === 0 || !!blocked"
                        @click="previewChanges"
                    />
                </div>
            </form>
        </template>
        <TrackPickerDialog v-model:open="pickerOpen" :exclude-ids="trackIds" @pick="pick" />
        <BaseDialog
            v-model:open="deleteOpen"
            data-item="delete-own-album"
            :title="t('ownAlbums.deleteTitle')"
        >
            <p>{{ t('ownAlbums.deleteText', { title: album?.title ?? '' }) }}</p>
            <template #footer>
                <BaseButton :label="t('actions.cancel')" @click="deleteOpen = false" />
                <BaseButton
                    data-item="confirm-delete"
                    variant="primary"
                    icon="delete"
                    :label="t('ownAlbums.delete')"
                    :disabled="saving"
                    @click="confirmDelete"
                />
            </template>
        </BaseDialog>
        <OrganizePreviewDialog
            v-if="preview"
            v-model:open="dialogOpen"
            :operation="preview"
            :blocked="blocked"
            @applied="applied"
            @discarded="discarded"
        />
    </div>
</template>
