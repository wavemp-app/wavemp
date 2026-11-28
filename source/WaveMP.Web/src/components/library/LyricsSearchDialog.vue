<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { ProblemCodeObject } from '@/api/generated/models'
import { problemCodeOf } from '@/api/problems'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import BaseProgress from '@/components/base/BaseProgress.vue'
import BaseTextField from '@/components/base/BaseTextField.vue'
import { chooseLyrics, searchLyrics, type LyricsMatch } from '@/lyrics/api'
import { downloadFailureOf } from '@/lyrics/presentation'
import type { TrackLyrics } from '@/lyrics/timing'
import LyricsMatchList from './LyricsMatchList.vue'

/**
 * Searches the online lyrics service for the lyrics of a song when it opens, with the title
 * without additions and the main artist of the song, and shows the tracks with lyrics, the best
 * first. The admin can change title and artist (an empty artist searches by title only) and search
 * again, then chooses a track: its lyrics replace those of the song, also a lyrics file. A track of
 * another duration gives only its plain lyrics, marked as another version.
 */
const props = defineProps<{
    trackId: string
}>()

const emit = defineEmits<{
    /** The chosen lyrics were saved. */
    saved: [lyrics: TrackLyrics | null]
}>()

const open = defineModel<boolean>('open', { default: false })

type State =
    | 'loading'
    | 'ready'
    | 'disabled'
    | 'unavailable'
    | 'rateLimited'
    | 'notFound'
    | 'error'
    | 'applying'
    | 'applied'

const { t } = useI18n()
const matchesTitleId = useId()

const state = ref<State>('loading')
const title = ref('')
const artist = ref('')
const matches = ref<LyricsMatch[]>([])
const selectedId = ref<number | null>(null)
const titleError = ref<string | undefined>(undefined)
const applyError = ref<string | null>(null)
const saved = ref<TrackLyrics | null>(null)
let generation = 0

const busy = computed(() => state.value === 'loading' || state.value === 'applying')
const selected = computed(
    () => matches.value.find((match) => match.id === selectedId.value) ?? null,
)
const canApply = computed(() => state.value === 'ready' && selected.value !== null)

async function search(withNames: boolean): Promise<void> {
    if (withNames && title.value.trim() === '') {
        titleError.value = t('trackLyrics.search.titleRequired')
        return
    }

    const current = ++generation
    titleError.value = undefined
    applyError.value = null
    selectedId.value = null
    matches.value = []
    state.value = 'loading'
    try {
        const found = await searchLyrics(
            props.trackId,
            withNames ? { title: title.value, artist: artist.value } : undefined,
        )
        if (current !== generation) {
            return
        }

        title.value = found.title
        artist.value = found.artist ?? ''
        matches.value = [...found.matches]
        selectedId.value = found.matches[0]?.id ?? null
        state.value = 'ready'
    } catch (failure) {
        if (current !== generation) {
            return
        }

        switch (problemCodeOf(failure)) {
            case ProblemCodeObject.LyricsDownload_disabled:
                state.value = 'disabled'
                break
            case ProblemCodeObject.LyricsService_unavailable:
                state.value = 'unavailable'
                break
            case ProblemCodeObject.LyricsRate_limited:
                state.value = 'rateLimited'
                break
            case ProblemCodeObject.TrackNot_found:
                state.value = 'notFound'
                break
            default:
                state.value = 'error'
        }
    }
}

async function apply(): Promise<void> {
    const match = selected.value
    if (!canApply.value || !match) {
        return
    }

    const current = generation
    state.value = 'applying'
    applyError.value = null
    try {
        const lyrics = await chooseLyrics(props.trackId, match.id)
        if (current !== generation) {
            return
        }

        saved.value = lyrics
        state.value = 'applied'
        emit('saved', lyrics)
    } catch (failure) {
        if (current === generation) {
            applyError.value = t(downloadFailureOf(problemCodeOf(failure)).labelKey)
            state.value = 'ready'
        }
    }
}

function failureMessage(value: State): string {
    switch (value) {
        case 'unavailable':
            return t('trackLyrics.errors.serviceUnavailable')
        case 'rateLimited':
            return t('trackLyrics.errors.rateLimited')
        case 'notFound':
            return t('trackLyrics.errors.trackNotFound')
        default:
            return t('trackLyrics.search.failed')
    }
}

watch(
    [open, () => props.trackId],
    ([isOpen]) => {
        if (isOpen) {
            title.value = ''
            artist.value = ''
            saved.value = null
            void search(false)
        } else {
            generation++
        }
    },
    { immediate: true },
)
</script>

<template>
    <BaseDialog
        v-model:open="open"
        data-item="lyrics-search"
        :title="t('trackLyrics.search.title')"
    >
        <div
            data-part="online-search"
            data-owner="lyrics"
            :data-state="state"
            :aria-busy="busy ? 'true' : undefined"
        >
            <template v-if="state === 'applied'">
                <BaseAlert tone="success" :message="t('trackLyrics.search.saved')" />
                <BaseAlert
                    v-if="saved?.fromOtherVersion"
                    tone="info"
                    :message="t('trackLyrics.otherVersion')"
                />
            </template>
            <template v-else-if="state === 'disabled'">
                <BaseAlert tone="info" :message="t('trackLyrics.errors.downloadDisabled')" />
                <BaseLink data-item="lyrics-settings" :to="{ name: 'admin-library' }">
                    {{ t('onlineMetadata.toSettings') }}
                </BaseLink>
            </template>
            <template v-else>
                <form
                    data-part="online-search-form"
                    :aria-label="t('trackLyrics.search.formLabel')"
                    novalidate
                    @submit.prevent="search(true)"
                >
                    <BaseTextField
                        v-model="title"
                        data-field="title"
                        :label="t('trackLyrics.search.titleLabel')"
                        :error="titleError"
                        :disabled="busy"
                        autocomplete="off"
                    />
                    <BaseTextField
                        v-model="artist"
                        data-field="artist"
                        :label="t('trackLyrics.search.artistLabel')"
                        :disabled="busy"
                        autocomplete="off"
                    />
                    <BaseButton
                        type="submit"
                        data-item="search"
                        icon="search-online"
                        :label="t('trackLyrics.search.search')"
                        :disabled="busy"
                    />
                    <p data-part="form-note" data-item="artist-hint">
                        {{ t('trackLyrics.search.artistHint') }}
                    </p>
                </form>
                <BaseProgress
                    v-if="state === 'loading'"
                    :label="t('trackLyrics.search.searching')"
                />
                <BaseAlert
                    v-else-if="
                        state === 'unavailable' ||
                        state === 'rateLimited' ||
                        state === 'notFound' ||
                        state === 'error'
                    "
                    tone="error"
                    :message="failureMessage(state)"
                />
                <template v-else>
                    <BaseAlert v-if="applyError" tone="error" :message="applyError" />
                    <BaseAlert
                        v-if="matches.length === 0"
                        tone="info"
                        :message="t('trackLyrics.search.noMatches')"
                    />
                    <section v-else data-part="online-section" data-item="matches">
                        <h3 :id="matchesTitleId" data-part="online-section-title">
                            {{ t('trackLyrics.search.matches') }}
                        </h3>
                        <LyricsMatchList
                            v-model="selectedId"
                            :matches="matches"
                            :labelledby="matchesTitleId"
                            :disabled="busy"
                        />
                        <p
                            v-if="selected && !selected.fitsDuration"
                            data-part="form-note"
                            data-item="other-version"
                        >
                            {{ t('trackLyrics.search.otherVersionHint') }}
                        </p>
                    </section>
                </template>
                <p data-part="form-note" data-item="lyrics-source">
                    {{ t('trackLyrics.search.source') }}
                </p>
            </template>
        </div>
        <template #footer>
            <BaseButton
                v-if="state === 'applied' || state === 'disabled'"
                variant="primary"
                :label="t('dialog.close')"
                @click="open = false"
            />
            <template v-else>
                <BaseButton :label="t('actions.cancel')" @click="open = false" />
                <BaseButton
                    data-item="apply"
                    variant="primary"
                    :label="
                        state === 'applying'
                            ? t('trackLyrics.search.applying')
                            : t('trackLyrics.search.apply')
                    "
                    :disabled="!canApply"
                    @click="apply"
                />
            </template>
        </template>
    </BaseDialog>
</template>
