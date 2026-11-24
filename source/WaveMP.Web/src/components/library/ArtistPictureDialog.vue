<script setup lang="ts">
import { computed, onBeforeUnmount, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import {
    ProblemCodeObject,
    type Artist,
    type ArtistPictureCandidate,
    type MusicBrainzArtistMatch,
} from '@/api/generated/models'
import { problemCodeOf } from '@/api/problems'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import BaseProgress from '@/components/base/BaseProgress.vue'
import BaseSegmentedControl, {
    type SegmentOption,
} from '@/components/base/BaseSegmentedControl.vue'
import AvatarCropper from '@/components/profiles/AvatarCropper.vue'
import {
    fetchCandidatePreview,
    listMusicBrainzMatches,
    listPictureCandidates,
    removeOnlineArtistPicture,
    removeUploadedArtistPicture,
    takeOverPicture,
    uploadArtistPicture,
} from '@/library/artistPictureApi'
import {
    pictureFailureKey,
    pictureSourceRows,
    takeOverCropOf,
    type PictureSourceRow,
} from '@/library/artistPictures'
import {
    avatarUploadProblem,
    avatarUploadTypes,
    cropRectangle,
    initialCrop,
    type AvatarCrop,
} from '@/profiles/avatarCrop'
import { drawCrop, loadPicture, releasePicture, type LoadedPicture } from '@/profiles/avatarImage'
import { artistPictureSlotKinds, useCoversStore } from '@/stores/covers'
import ArtistCredit from './ArtistCredit.vue'
import ArtistMatchList from './ArtistMatchList.vue'
import LibraryCover from './LibraryCover.vue'
import PictureCandidateList from './PictureCandidateList.vue'

/**
 * Changes the picture of an artist (admins). The sources of the picture are listed in the order
 * in which they win (picture-sources): the picture in the artist folder, the uploaded picture, the
 * online picture and the album covers, each with its state, so the admin sees which one viewers
 * get and why a stored picture does not show. Upload: a JPEG, PNG or WebP picture is cropped to a
 * square like an avatar and uploaded. Online: only on request, the artist is looked up in
 * MusicBrainz (or the one chosen last time is used), then its pictures on Wikimedia Commons and,
 * with a key, fanart.tv are shown with previews; the chosen picture can be cropped and is taken
 * over only on confirmation. The dialog stays open after a change and shows the new state; emits
 * changed with the artist from the answer, or without one when the page must load it again.
 */
const props = defineProps<{
    artist: Artist
}>()

const emit = defineEmits<{
    changed: [artist?: Artist]
}>()

const open = defineModel<boolean>('open', { default: false })

type Mode = 'upload' | 'online'
type MatchState = 'idle' | 'loading' | 'ready' | 'disabled' | 'unavailable' | 'error'
type CandidateState = 'idle' | 'loading' | 'ready' | 'unavailable' | 'error'
type PreviewState = 'idle' | 'loading' | 'ready' | 'error'

const { t } = useI18n()
const covers = useCoversStore()
const sourcesTitleId = useId()
const matchesTitleId = useId()
const candidatesTitleId = useId()
const cropTitleId = useId()

const mode = ref<Mode>('upload')
const busy = ref(false)
const error = ref<string | null>(null)
const success = ref<string | null>(null)

// Upload
const fileInput = ref<HTMLInputElement | null>(null)
const upload = ref<LoadedPicture | null>(null)
const uploadCrop = ref<AvatarCrop>({ x: 0, y: 0, zoom: 1 })

// Online lookup
const matchState = ref<MatchState>('idle')
const matches = ref<MusicBrainzArtistMatch[]>([])
const showMatches = ref(false)
const selectedMatch = ref<string | null>(null)
const candidateState = ref<CandidateState>('idle')
const candidates = ref<ArtistPictureCandidate[]>([])
const selectedCandidate = ref<string | null>(null)
const previewState = ref<PreviewState>('idle')
const preview = ref<LoadedPicture | null>(null)
const previewCrop = ref<AvatarCrop>({ x: 0, y: 0, zoom: 1 })
let lookupGeneration = 0
let previewGeneration = 0

const acceptedTypes = avatarUploadTypes.join(',')
const artistId = computed(() => props.artist.id ?? '')
const picture = computed(() => props.artist.picture ?? null)
const sources = computed(() => pictureSourceRows(picture.value))
const hasUploaded = computed(() => picture.value?.hasUploadedPicture ?? false)
const hasOnline = computed(() => picture.value?.hasOnlinePicture ?? false)
const remembered = computed(() => picture.value?.musicBrainzArtistId ?? null)
const chosenCandidate = computed(
    () => candidates.value.find((candidate) => candidate.id === selectedCandidate.value) ?? null,
)

const modeOptions = computed<SegmentOption[]>(() => [
    { value: 'upload', item: 'upload', label: t('artistPictures.modes.upload'), icon: 'upload' },
    {
        value: 'online',
        item: 'online',
        label: t('artistPictures.modes.online'),
        icon: 'search-online',
    },
])

const canUpload = computed(() => mode.value === 'upload' && !!upload.value && !busy.value)
const canTakeOver = computed(
    () =>
        mode.value === 'online' &&
        !!chosenCandidate.value?.id &&
        previewState.value !== 'loading' &&
        !busy.value,
)

/** The text of the state of a source in the list of sources. */
function stateText(row: PictureSourceRow): string {
    if (row.state === 'active') {
        return t('artistPictures.sources.shown')
    }

    if (row.state === 'missing') {
        return t(`artistPictures.sources.missing.${row.source}`)
    }

    if (row.source === 'albums') {
        return t('artistPictures.sources.fallback')
    }

    return row.hiddenBy
        ? t(`artistPictures.sources.hiddenBy.${row.hiddenBy}`)
        : t('artistPictures.sources.stored')
}

function setUpload(next: LoadedPicture | null): void {
    releasePicture(upload.value)
    upload.value = next
    uploadCrop.value = next ? initialCrop(next) : { x: 0, y: 0, zoom: 1 }
}

function setPreview(next: LoadedPicture | null): void {
    releasePicture(preview.value)
    preview.value = next
    previewCrop.value = next ? initialCrop(next) : { x: 0, y: 0, zoom: 1 }
}

function resetLookup(): void {
    lookupGeneration++
    previewGeneration++
    matchState.value = 'idle'
    matches.value = []
    showMatches.value = false
    selectedMatch.value = null
    candidateState.value = 'idle'
    candidates.value = []
    selectedCandidate.value = null
    previewState.value = 'idle'
    setPreview(null)
}

function reset(): void {
    busy.value = false
    error.value = null
    success.value = null
    setUpload(null)
    resetLookup()
    mode.value = hasOnline.value && !hasUploaded.value ? 'online' : 'upload'
}

watch(open, (isOpen) => {
    if (isOpen) {
        reset()
    } else {
        setUpload(null)
        resetLookup()
    }
})

watch(mode, () => {
    error.value = null
    success.value = null
})

onBeforeUnmount(() => {
    setUpload(null)
    setPreview(null)
})

function chooseFile(): void {
    fileInput.value?.click()
}

async function onFileChosen(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0]
    input.value = ''
    if (!file) {
        return
    }

    error.value = null
    success.value = null
    const problem = avatarUploadProblem(file)
    if (problem) {
        error.value = t(
            problem === 'type' ? 'artistPictures.errors.type' : 'artistPictures.errors.size',
        )
        return
    }

    try {
        setUpload(await loadPicture(file))
    } catch {
        setUpload(null)
        error.value = t('artistPictures.errors.unreadable')
    }
}

/** Runs a change; afterwards the pictures of the artist are loaded again. */
async function run(action: () => Promise<Artist | undefined>, done: string): Promise<boolean> {
    if (busy.value) {
        return false
    }

    busy.value = true
    error.value = null
    success.value = null
    try {
        const changed = await action()
        covers.invalidate(['artist', ...artistPictureSlotKinds])
        success.value = done
        emit('changed', changed)
        return true
    } catch (failure) {
        error.value = t(pictureFailureKey(problemCodeOf(failure)))
        return false
    } finally {
        busy.value = false
    }
}

async function saveUpload(): Promise<void> {
    const loaded = upload.value
    if (!loaded) {
        return
    }

    const saved = await run(async () => {
        const blob = await drawCrop(loaded, cropRectangle(uploadCrop.value, loaded))
        return uploadArtistPicture(artistId.value, blob, 'artist.png')
    }, t('artistPictures.upload.saved'))
    if (saved) {
        setUpload(null)
    }
}

function removeUploaded(): Promise<boolean> {
    return run(async () => {
        await removeUploadedArtistPicture(artistId.value)
        return undefined
    }, t('artistPictures.upload.removed'))
}

function removeOnline(): Promise<boolean> {
    return run(async () => {
        await removeOnlineArtistPicture(artistId.value)
        return undefined
    }, t('artistPictures.online.removed'))
}

function lookupState(failure: unknown): 'disabled' | 'unavailable' | 'error' {
    switch (problemCodeOf(failure)) {
        case ProblemCodeObject.ArtistPicture_lookup_disabled:
            return 'disabled'
        case ProblemCodeObject.ArtistPicture_lookup_unavailable:
            return 'unavailable'
        default:
            return 'error'
    }
}

/** Starts the lookup: the artist chosen last time, otherwise the artists of MusicBrainz. */
function find(): Promise<void> {
    error.value = null
    success.value = null
    return remembered.value ? loadCandidates(null) : loadMatches()
}

async function loadMatches(): Promise<void> {
    const current = ++lookupGeneration
    showMatches.value = true
    matchState.value = 'loading'
    candidateState.value = 'idle'
    candidates.value = []
    selectedCandidate.value = null
    try {
        const page = await listMusicBrainzMatches(artistId.value)
        if (current !== lookupGeneration) {
            return
        }

        matches.value = (page?.items ?? []).filter((match) => !!match.musicBrainzArtistId)
        matchState.value = 'ready'
        const first = matches.value.find((match) => match.isRemembered) ?? matches.value[0] ?? null
        selectedMatch.value = first?.musicBrainzArtistId ?? null
        if (selectedMatch.value) {
            void loadCandidates(selectedMatch.value)
        }
    } catch (failure) {
        if (current === lookupGeneration) {
            matchState.value = lookupState(failure)
        }
    }
}

/** Loads the pictures of the MusicBrainz artist (null: the one remembered for the artist). */
async function loadCandidates(musicBrainzArtistId: string | null): Promise<void> {
    const current = ++lookupGeneration
    previewGeneration++
    candidateState.value = 'loading'
    candidates.value = []
    selectedCandidate.value = null
    previewState.value = 'idle'
    setPreview(null)
    try {
        const page = await listPictureCandidates(artistId.value, musicBrainzArtistId)
        if (current !== lookupGeneration) {
            return
        }

        candidates.value = (page?.items ?? []).filter((candidate) => !!candidate.id)
        candidateState.value = 'ready'
        selectedCandidate.value = candidates.value[0]?.id ?? null
    } catch (failure) {
        if (current !== lookupGeneration) {
            return
        }

        const state = lookupState(failure)
        if (state === 'disabled') {
            matchState.value = 'disabled'
            candidateState.value = 'idle'
        } else {
            candidateState.value = state
        }
    }
}

/** Choosing another MusicBrainz artist shows its pictures. */
function onMatchChosen(id: string | null): void {
    if (id && id !== selectedMatch.value) {
        selectedMatch.value = id
        void loadCandidates(id)
    }
}

/** Loads the preview of the chosen picture for cropping. */
async function loadPreview(candidateId: string | null): Promise<void> {
    const current = ++previewGeneration
    setPreview(null)
    if (!candidateId) {
        previewState.value = 'idle'
        return
    }

    previewState.value = 'loading'
    try {
        const buffer = await fetchCandidatePreview(artistId.value, candidateId, 500)
        if (current !== previewGeneration) {
            return
        }

        if (!buffer || buffer.byteLength === 0) {
            previewState.value = 'error'
            return
        }

        const loaded = await loadPicture(new Blob([buffer], { type: 'image/webp' }))
        if (current !== previewGeneration) {
            releasePicture(loaded)
            return
        }

        setPreview(loaded)
        previewState.value = 'ready'
    } catch (failure) {
        if (current !== previewGeneration) {
            return
        }

        previewState.value = 'error'
        if (problemCodeOf(failure) === ProblemCodeObject.ArtistPicture_candidate_not_found) {
            error.value = t('artistPictures.errors.candidateExpired')
        }
    }
}

watch(selectedCandidate, (id) => void loadPreview(id))

async function takeOver(): Promise<void> {
    const candidate = chosenCandidate.value
    if (!candidate?.id) {
        return
    }

    const loaded = preview.value
    const crop = loaded ? takeOverCropOf(previewCrop.value, loaded) : null
    const candidateId = candidate.id
    const saved = await run(
        () => takeOverPicture(artistId.value, candidateId, crop),
        t('artistPictures.online.saved'),
    )
    if (saved) {
        resetLookup()
    }
}
</script>

<template>
    <BaseDialog v-model:open="open" data-item="artist-picture" :title="t('artistPictures.title')">
        <div
            v-if="open"
            data-part="artist-picture-editor"
            :data-state="mode"
            :aria-busy="busy ? 'true' : undefined"
        >
            <section
                data-part="online-section"
                data-item="sources"
                :aria-labelledby="sourcesTitleId"
            >
                <h3 :id="sourcesTitleId" data-part="online-section-title">
                    {{ t('artistPictures.sources.title') }}
                </h3>
                <ol data-part="picture-sources">
                    <li
                        v-for="row in sources"
                        :key="row.source"
                        data-part="picture-source"
                        :data-source="row.source"
                        :data-state="row.state"
                        :aria-current="row.state === 'active' ? 'true' : undefined"
                    >
                        <span data-part="picture-source-name">
                            {{ t(`artistPictures.sources.names.${row.source}`) }}
                        </span>
                        <span data-part="picture-source-state">{{ stateText(row) }}</span>
                    </li>
                </ol>
            </section>
            <BaseSegmentedControl
                v-model="mode"
                item="artist-picture-mode"
                :options="modeOptions"
                :label="t('artistPictures.mode')"
                :disabled="busy"
            />
            <BaseAlert v-if="error" tone="error" :message="error" />
            <BaseAlert v-if="success" tone="success" :message="success" />
            <section v-if="mode === 'upload'" data-part="online-section" data-item="upload">
                <AvatarCropper
                    v-if="upload"
                    v-model="uploadCrop"
                    :src="upload.url"
                    :picture="upload"
                    :disabled="busy"
                />
                <template v-else>
                    <LibraryCover
                        v-if="hasUploaded"
                        kind="artist-uploaded"
                        data-kind="artist"
                        :id="artistId"
                        :available="true"
                    />
                    <p data-part="form-note">{{ t('artistPictures.upload.hint') }}</p>
                </template>
                <div data-part="view-actions">
                    <BaseButton
                        data-item="choose-file"
                        icon="upload"
                        :label="
                            upload
                                ? t('artistPictures.upload.chooseOther')
                                : t('artistPictures.upload.choose')
                        "
                        :disabled="busy"
                        @click="chooseFile"
                    />
                    <BaseButton
                        v-if="hasUploaded && !upload"
                        data-item="remove-uploaded"
                        icon="delete"
                        :label="t('artistPictures.upload.remove')"
                        :disabled="busy"
                        @click="removeUploaded"
                    />
                </div>
                <input
                    ref="fileInput"
                    type="file"
                    :accept="acceptedTypes"
                    hidden
                    @change="onFileChosen"
                />
            </section>
            <template v-else>
                <section
                    v-if="hasOnline && candidateState === 'idle' && matchState === 'idle'"
                    data-part="online-section"
                    data-item="online-picture"
                >
                    <LibraryCover
                        kind="artist-online"
                        data-kind="artist"
                        :id="artistId"
                        :available="true"
                    />
                    <ArtistCredit :credit="picture?.onlineCredit" />
                </section>
                <template v-if="matchState === 'disabled'">
                    <BaseAlert tone="info" :message="t('artistPictures.online.disabled')" />
                    <BaseLink data-item="artist-picture-settings" :to="{ name: 'admin-library' }">
                        {{ t('artistPictures.online.toSettings') }}
                    </BaseLink>
                </template>
                <template v-else>
                    <p
                        v-if="matchState === 'idle' && candidateState === 'idle'"
                        data-part="form-note"
                        data-item="online-privacy"
                    >
                        {{ t('artistPictures.online.hint') }}
                    </p>
                    <section
                        v-if="showMatches"
                        data-part="online-section"
                        data-item="matches"
                        :data-state="matchState"
                        :aria-busy="matchState === 'loading' ? 'true' : undefined"
                    >
                        <h3 :id="matchesTitleId" data-part="online-section-title">
                            {{ t('artistPictures.online.matches') }}
                        </h3>
                        <BaseProgress
                            v-if="matchState === 'loading'"
                            :label="t('artistPictures.online.searchingArtists')"
                        />
                        <BaseAlert
                            v-else-if="matchState === 'unavailable' || matchState === 'error'"
                            tone="error"
                            :message="
                                matchState === 'unavailable'
                                    ? t('artistPictures.online.musicBrainzUnavailable')
                                    : t('artistPictures.online.failed')
                            "
                        />
                        <BaseAlert
                            v-else-if="matches.length === 0"
                            tone="info"
                            :message="t('artistPictures.online.noMatches')"
                        />
                        <ArtistMatchList
                            v-else
                            :model-value="selectedMatch"
                            :matches="matches"
                            :labelledby="matchesTitleId"
                            :disabled="busy"
                            @update:model-value="onMatchChosen"
                        />
                    </section>
                    <section
                        v-if="candidateState !== 'idle'"
                        data-part="online-section"
                        data-item="candidates"
                        :data-state="candidateState"
                        :aria-busy="candidateState === 'loading' ? 'true' : undefined"
                    >
                        <h3 :id="candidatesTitleId" data-part="online-section-title">
                            {{ t('artistPictures.online.candidates') }}
                        </h3>
                        <BaseProgress
                            v-if="candidateState === 'loading'"
                            :label="t('artistPictures.online.searchingPictures')"
                        />
                        <BaseAlert
                            v-else-if="
                                candidateState === 'unavailable' || candidateState === 'error'
                            "
                            tone="error"
                            :message="
                                candidateState === 'unavailable'
                                    ? t('artistPictures.online.servicesUnavailable')
                                    : t('artistPictures.online.failed')
                            "
                        />
                        <BaseAlert
                            v-else-if="candidates.length === 0"
                            tone="info"
                            :message="t('artistPictures.online.noPictures')"
                        />
                        <template v-else>
                            <PictureCandidateList
                                v-model="selectedCandidate"
                                :artist-id="artistId"
                                :candidates="candidates"
                                :labelledby="candidatesTitleId"
                                :disabled="busy"
                            />
                            <ArtistCredit v-if="chosenCandidate" :credit="chosenCandidate" />
                        </template>
                    </section>
                    <section
                        v-if="chosenCandidate && previewState !== 'idle'"
                        data-part="online-section"
                        data-item="crop"
                        :data-state="previewState"
                        :aria-labelledby="cropTitleId"
                    >
                        <h3 :id="cropTitleId" data-part="online-section-title">
                            {{ t('artistPictures.online.crop') }}
                        </h3>
                        <BaseProgress
                            v-if="previewState === 'loading'"
                            :label="t('artistPictures.online.loadingPreview')"
                        />
                        <AvatarCropper
                            v-else-if="preview"
                            v-model="previewCrop"
                            :src="preview.url"
                            :picture="preview"
                            :disabled="busy"
                        />
                        <p v-else data-part="form-note">
                            {{ t('artistPictures.online.previewFailed') }}
                        </p>
                    </section>
                    <div data-part="view-actions">
                        <BaseButton
                            v-if="matchState === 'idle' && candidateState === 'idle'"
                            data-item="find-online"
                            icon="search-online"
                            :label="t('artistPictures.online.find')"
                            :disabled="busy"
                            @click="find"
                        />
                        <BaseButton
                            v-else-if="
                                !showMatches ||
                                matchState !== 'ready' ||
                                candidateState === 'unavailable' ||
                                candidateState === 'error'
                            "
                            data-item="change-match"
                            icon="search-online"
                            :label="
                                remembered && !showMatches
                                    ? t('artistPictures.online.otherArtist')
                                    : t('artistPictures.online.searchAgain')
                            "
                            :disabled="
                                busy || matchState === 'loading' || candidateState === 'loading'
                            "
                            @click="loadMatches"
                        />
                        <BaseButton
                            v-if="hasOnline"
                            data-item="remove-online"
                            icon="delete"
                            :label="t('artistPictures.online.remove')"
                            :disabled="busy"
                            @click="removeOnline"
                        />
                    </div>
                </template>
            </template>
        </div>
        <template #footer>
            <BaseButton :label="t('dialog.close')" :disabled="busy" @click="open = false" />
            <BaseButton
                v-if="mode === 'upload'"
                variant="primary"
                data-item="save-upload"
                :label="t('artistPictures.upload.save')"
                :disabled="!canUpload"
                @click="saveUpload"
            />
            <BaseButton
                v-else
                variant="primary"
                data-item="take-over"
                :label="t('artistPictures.online.takeOver')"
                :disabled="!canTakeOver"
                @click="takeOver"
            />
        </template>
    </BaseDialog>
</template>
