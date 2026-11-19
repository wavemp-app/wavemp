<script setup lang="ts">
import { computed, onBeforeUnmount, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import {
    OnlineCoverOutcomeObject,
    ProblemCodeObject,
    TrackMetadataStatusObject,
    type OnlineAlbumApplyResult,
    type OnlineCoverOutcome,
    type OnlineTrackApplyResult,
    type TrackMetadataStatus,
} from '@/api/generated/models'
import { problemCodeOf } from '@/api/problems'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import BaseProgress from '@/components/base/BaseProgress.vue'
import { useFormat } from '@/composables/useFormat'
import {
    albumValues,
    compareRows,
    applyAlbumOnline,
    applyTrackOnline,
    defaultSelection,
    fetchReleaseCover,
    fromAlbumMatch,
    fromTrackMatch,
    onlineAlbumFields,
    onlineTrackFields,
    searchAlbumOnline,
    searchTrackOnline,
    tagsOf,
    trackValues,
    type OnlineCoverState,
    type OnlineField,
    type OnlineMatch,
    type OnlineOwner,
    type OnlineValues,
} from '@/library/onlineMetadata'
import { useCoversStore } from '@/stores/covers'
import OnlineCompareTable from './OnlineCompareTable.vue'
import OnlineMatchList from './OnlineMatchList.vue'

/**
 * Searches a song or an album in MusicBrainz when it opens and shows the matches, the best first.
 * The selected match is compared with the current values field by field; the admin chooses the
 * fields and the cover of the release (preview from the Cover Art Archive) to apply. Fields the
 * song or album is missing are chosen at first, nothing else. Nothing is written before Apply;
 * the tags go through the metadata editor, so only audio files are written, after a backup.
 */
const props = defineProps<{
    owner: OnlineOwner
    id: string
    /** Whether the song or album has a cover now, for the comparison. */
    hasCover: boolean
}>()

const emit = defineEmits<{
    /** Something was written; the album id is the one after applying (it may change). */
    applied: [result: { albumId: string | null; cover: boolean }]
}>()

const open = defineModel<boolean>('open', { default: false })

type State =
    'loading' | 'ready' | 'disabled' | 'unavailable' | 'notFound' | 'error' | 'applying' | 'applied'

const { t } = useI18n()
const format = useFormat()
const covers = useCoversStore()
const matchesTitleId = useId()
const compareTitleId = useId()

const state = ref<State>('loading')
const matches = ref<OnlineMatch[]>([])
const current = ref<OnlineValues>({})
const trackStatus = ref<TrackMetadataStatus | null>(null)
const trackCount = ref(0)
const editableTrackCount = ref(0)
const selectedKey = ref<string | null>(null)
const chosen = ref<Set<OnlineField>>(new Set())
const useCover = ref(false)
const coverUrl = ref<string | null>(null)
const coverState = ref<OnlineCoverState>('none')
const applyError = ref<string | null>(null)
const results = ref<string[]>([])
let generation = 0
let coverGeneration = 0

const fields = computed(() => (props.owner === 'album' ? onlineAlbumFields : onlineTrackFields))
const selected = computed(
    () => matches.value.find((match) => match.key === selectedKey.value) ?? null,
)
const busy = computed(() => state.value === 'applying')
const tagsWritable = computed(() =>
    props.owner === 'album'
        ? editableTrackCount.value > 0
        : trackStatus.value === TrackMetadataStatusObject.Editable,
)
const writeHint = computed(() => {
    if (props.owner === 'album') {
        if (editableTrackCount.value === 0) {
            return t('onlineMetadata.albumNone')
        }
        return editableTrackCount.value < trackCount.value
            ? t('onlineMetadata.albumPartial', {
                  editable: format.number(editableTrackCount.value),
                  count: format.number(trackCount.value),
              })
            : t('onlineMetadata.albumNote')
    }
    if (trackStatus.value === TrackMetadataStatusObject.NoAudio) {
        return `${t('trackMetadata.hints.noAudio')} ${t('onlineMetadata.coverOnly')}`
    }
    if (trackStatus.value === TrackMetadataStatusObject.NotWritable) {
        return `${t('trackMetadata.hints.notWritable')} ${t('onlineMetadata.coverOnly')}`
    }
    return null
})

const rows = computed(() =>
    selected.value ? compareRows(fields.value, current.value, selected.value.values) : [],
)

const tags = computed(() =>
    selected.value && tagsWritable.value
        ? tagsOf(fields.value, chosen.value, current.value, selected.value.values)
        : null,
)
const coverReleaseId = computed(() =>
    useCover.value && coverState.value === 'ready' ? (selected.value?.release?.id ?? null) : null,
)
const canApply = computed(
    () => state.value === 'ready' && (tags.value !== null || coverReleaseId.value !== null),
)
const title = computed(() =>
    props.owner === 'album' ? t('onlineMetadata.titleAlbum') : t('onlineMetadata.titleTrack'),
)

function toggle(field: OnlineField, value: boolean): void {
    const next = new Set(chosen.value)
    if (value) {
        next.add(field)
    } else {
        next.delete(field)
    }
    chosen.value = next
}

function freeCover(): void {
    if (coverUrl.value) {
        URL.revokeObjectURL(coverUrl.value)
        coverUrl.value = null
    }
}

async function loadCover(releaseId: string | null | undefined): Promise<void> {
    const currentCover = ++coverGeneration
    freeCover()
    useCover.value = false
    if (!releaseId) {
        coverState.value = 'none'
        return
    }

    coverState.value = 'loading'
    try {
        const buffer = await fetchReleaseCover(releaseId)
        if (currentCover !== coverGeneration) {
            return
        }
        if (!buffer || buffer.byteLength === 0) {
            coverState.value = 'missing'
            return
        }
        coverUrl.value = URL.createObjectURL(new Blob([buffer]))
        coverState.value = 'ready'
        // A cover is suggested when the song or album has none.
        useCover.value = !props.hasCover
    } catch (failure) {
        if (currentCover === coverGeneration) {
            coverState.value =
                problemCodeOf(failure) === ProblemCodeObject.LookupCover_not_found
                    ? 'missing'
                    : 'error'
        }
    }
}

// A new match chooses the fields that are missing and loads the cover of its release.
watch(selected, (match) => {
    chosen.value = match
        ? defaultSelection(fields.value, current.value, match.values)
        : new Set<OnlineField>()
    void loadCover(match?.release?.id)
})

async function search(): Promise<void> {
    const currentSearch = ++generation
    state.value = 'loading'
    applyError.value = null
    results.value = []
    selectedKey.value = null
    matches.value = []
    try {
        if (props.owner === 'album') {
            const found = await searchAlbumOnline(props.id)
            if (currentSearch !== generation) {
                return
            }
            current.value = albumValues(found?.current)
            trackCount.value = found?.trackCount ?? 0
            editableTrackCount.value = found?.editableTrackCount ?? 0
            matches.value = (found?.matches ?? []).map(fromAlbumMatch)
        } else {
            const found = await searchTrackOnline(props.id)
            if (currentSearch !== generation) {
                return
            }
            current.value = trackValues(found?.current)
            trackStatus.value = found?.current?.status ?? null
            matches.value = (found?.matches ?? []).map(fromTrackMatch)
        }
        selectedKey.value = matches.value[0]?.key ?? null
        state.value = 'ready'
    } catch (failure) {
        if (currentSearch !== generation) {
            return
        }
        const code = problemCodeOf(failure)
        if (code === ProblemCodeObject.LookupDisabled) {
            state.value = 'disabled'
        } else if (code === ProblemCodeObject.LookupUnavailable) {
            state.value = 'unavailable'
        } else if (
            code === ProblemCodeObject.TrackNot_found ||
            code === ProblemCodeObject.AlbumNot_found
        ) {
            state.value = 'notFound'
        } else {
            state.value = 'error'
        }
    }
}

function coverMessage(outcome: OnlineCoverOutcome | null | undefined): string | null {
    switch (outcome) {
        case OnlineCoverOutcomeObject.MediaFolder:
            return t('onlineMetadata.results.coverMediaFolder')
        case OnlineCoverOutcomeObject.DataFolder:
            return t('onlineMetadata.results.coverDataFolder')
        case OnlineCoverOutcomeObject.NotSaved:
            return t('onlineMetadata.results.coverNotSaved')
        default:
            return null
    }
}

function applyFailure(failure: unknown): string {
    switch (problemCodeOf(failure)) {
        case ProblemCodeObject.LookupUnavailable:
            return t('onlineMetadata.errors.coverUnavailable')
        case ProblemCodeObject.LookupCover_not_found:
            return t('onlineMetadata.errors.coverNotFound')
        case ProblemCodeObject.TrackNot_writable:
            return t('onlineMetadata.errors.notWritable')
        case ProblemCodeObject.TrackNo_audio:
            return t('onlineMetadata.errors.noAudio')
        case ProblemCodeObject.TrackNot_found:
            return t('onlineMetadata.notFoundTrack')
        case ProblemCodeObject.AlbumNot_found:
            return t('onlineMetadata.notFoundAlbum')
        case ProblemCodeObject.LookupDisabled:
            return t('onlineMetadata.disabled')
        default:
            return t('onlineMetadata.errors.applyFailed')
    }
}

async function apply(): Promise<void> {
    if (!canApply.value) {
        return
    }

    state.value = 'applying'
    applyError.value = null
    try {
        const messages: string[] = []
        let albumId: string | null = null
        let cover: OnlineCoverOutcome | null | undefined
        if (props.owner === 'album') {
            const result: OnlineAlbumApplyResult | undefined = await applyAlbumOnline(
                props.id,
                tags.value,
                coverReleaseId.value,
            )
            albumId = result?.albumId ?? props.id
            cover = result?.cover
            if (tags.value) {
                const written = result?.tracksWritten ?? 0
                const skipped = result?.tracksSkipped ?? 0
                messages.push(t('onlineMetadata.results.album', { count: format.number(written) }))
                if (skipped > 0) {
                    messages.push(
                        t('onlineMetadata.results.skipped', { count: format.number(skipped) }),
                    )
                }
            }
        } else {
            const result: OnlineTrackApplyResult | undefined = await applyTrackOnline(
                props.id,
                tags.value,
                coverReleaseId.value,
            )
            cover = result?.cover
            if (tags.value) {
                messages.push(t('onlineMetadata.results.track'))
            }
        }

        const coverText = coverMessage(cover)
        if (coverText) {
            messages.push(coverText)
        }
        const coverChanged =
            cover === OnlineCoverOutcomeObject.MediaFolder ||
            cover === OnlineCoverOutcomeObject.DataFolder
        if (coverChanged) {
            covers.invalidate()
        }
        results.value = messages
        state.value = 'applied'
        emit('applied', { albumId, cover: coverChanged })
    } catch (failure) {
        applyError.value = applyFailure(failure)
        state.value = 'ready'
    }
}

watch(
    [open, () => props.id, () => props.owner],
    ([isOpen]) => {
        if (isOpen) {
            void search()
        } else {
            generation++
            coverGeneration++
            freeCover()
        }
    },
    { immediate: true },
)

onBeforeUnmount(freeCover)
</script>

<template>
    <BaseDialog v-model:open="open" data-item="online-search" :title="title">
        <div
            data-part="online-search"
            :data-owner="owner"
            :data-state="state"
            :aria-busy="state === 'loading' || state === 'applying' ? 'true' : undefined"
        >
            <BaseProgress v-if="state === 'loading'" :label="t('onlineMetadata.searching')" />
            <template v-else-if="state === 'disabled'">
                <BaseAlert tone="info" :message="t('onlineMetadata.disabled')" />
                <BaseLink data-item="online-settings" :to="{ name: 'admin-library' }">
                    {{ t('onlineMetadata.toSettings') }}
                </BaseLink>
            </template>
            <template v-else-if="state === 'unavailable' || state === 'error'">
                <BaseAlert
                    tone="error"
                    :message="
                        state === 'unavailable'
                            ? t('onlineMetadata.unavailable')
                            : t('onlineMetadata.searchFailed')
                    "
                />
                <div data-part="view-actions">
                    <BaseButton
                        data-item="search-again"
                        :label="t('onlineMetadata.searchAgain')"
                        @click="search"
                    />
                </div>
            </template>
            <BaseAlert
                v-else-if="state === 'notFound'"
                tone="error"
                :message="
                    owner === 'album'
                        ? t('onlineMetadata.notFoundAlbum')
                        : t('onlineMetadata.notFoundTrack')
                "
            />
            <template v-else-if="state === 'applied'">
                <BaseAlert
                    v-for="(message, index) in results"
                    :key="index"
                    tone="success"
                    :message="message"
                />
            </template>
            <template v-else>
                <BaseAlert v-if="applyError" tone="error" :message="applyError" />
                <BaseAlert
                    v-if="matches.length === 0"
                    tone="info"
                    :message="t('onlineMetadata.noMatches')"
                />
                <template v-else>
                    <section data-part="online-section" data-item="matches">
                        <h3 :id="matchesTitleId" data-part="online-section-title">
                            {{ t('onlineMetadata.matches') }}
                        </h3>
                        <OnlineMatchList
                            v-model="selectedKey"
                            :owner="owner"
                            :matches="matches"
                            :labelledby="matchesTitleId"
                            :disabled="busy"
                        />
                    </section>
                    <section
                        v-if="selected"
                        data-part="online-section"
                        data-item="compare"
                        :aria-labelledby="compareTitleId"
                    >
                        <h3 :id="compareTitleId" data-part="online-section-title">
                            {{ t('onlineMetadata.compare') }}
                        </h3>
                        <BaseAlert v-if="writeHint" tone="info" :message="writeHint" />
                        <OnlineCompareTable
                            v-model:use-cover="useCover"
                            :owner="owner"
                            :id="id"
                            :has-cover="hasCover"
                            :rows="rows"
                            :chosen="chosen"
                            :tags-writable="tagsWritable"
                            :show-cover="!!selected.release"
                            :cover-state="coverState"
                            :cover-url="coverUrl"
                            :disabled="busy"
                            @toggle="toggle"
                        />
                        <p v-if="!canApply && state === 'ready'" data-part="form-note">
                            {{ t('onlineMetadata.nothingChosen') }}
                        </p>
                    </section>
                </template>
                <p data-part="form-note" data-item="online-source">
                    {{ t('onlineMetadata.source') }}
                </p>
            </template>
        </div>
        <template #footer>
            <template v-if="state === 'applied'">
                <BaseButton variant="primary" :label="t('dialog.close')" @click="open = false" />
            </template>
            <template v-else>
                <BaseButton :label="t('actions.cancel')" @click="open = false" />
                <BaseButton
                    data-item="apply"
                    variant="primary"
                    :label="busy ? t('onlineMetadata.applying') : t('onlineMetadata.apply')"
                    :disabled="!canApply"
                    @click="apply"
                />
            </template>
        </template>
    </BaseDialog>
</template>
