<script setup lang="ts">
import { computed, onBeforeUnmount, reactive, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import {
    SoundSourceObject,
    type FileSongSegment,
    type TrackSongSegments,
} from '@/api/generated/models'
import { problemCodeOf, validationErrorsOf } from '@/api/problems'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'
import BaseTextField from '@/components/base/BaseTextField.vue'
import { useFormat } from '@/composables/useFormat'
import {
    analyzeTrackSong,
    getTrackSongSegments,
    resetTrackSongSegment,
    setTrackSongSegment,
    type SongSegmentKind,
} from '@/library/api'
import {
    confidencePercent,
    methodLabelKey,
    parseSeconds,
    previewRange,
    secondsText,
    segmentState,
    stripPieces,
    type PreviewRange,
} from '@/library/songSegments'
import { getPlayback } from '@/player/api'
import { browserAudioFormats } from '@/player/formats'
import { browserHlsSupport, MediaSourceSlot } from '@/player/hls'
import { usePlayerStore } from '@/stores/player'

/**
 * Where the song lies in the audio file and in the video of a song, for admins in the metadata
 * editor: per file a strip of the file with the parts that belong to the song (intro and outro
 * stay empty), how the analysis found them and how sure it is, and the start and end of the song
 * as a correction, in seconds of the file. An empty field leaves the value to the analysis; a
 * correction is never replaced by an analysis. The preview plays the file around the start or the
 * end as the fields have them (the player pauses meanwhile). The fields have no save button of
 * their own: the editor around them calls save, which sends only what changed; the player applies
 * saved corrections at once (event saved). "Analyze again" queues a new analysis right away.
 */
const props = withDefaults(
    defineProps<{
        trackId: string
        /** Loads the segments while true (e.g. while the dialog is open). */
        active?: boolean
    }>(),
    { active: true },
)

const emit = defineEmits<{
    saved: [segments: TrackSongSegments]
}>()

const { t } = useI18n()
const format = useFormat()
const player = usePlayerStore()
const titleId = useId()

interface Draft {
    start: string
    end: string
    startError: string | undefined
    endError: string | undefined
}

type Edge = 'start' | 'end'

const kinds: readonly SongSegmentKind[] = ['audio', 'video']

const segments = ref<TrackSongSegments | null>(null)
const state = ref<'loading' | 'ready' | 'error'>('loading')
const saving = ref(false)
const failed = ref(false)
const analyzing = ref(false)
const analysis = ref<'queued' | 'failed' | null>(null)
const drafts = reactive<Record<SongSegmentKind, Draft>>({
    audio: emptyDraft(),
    video: emptyDraft(),
})
/** The file and edge a preview plays, and where it is; null without a preview. */
const preview = ref<{ kind: SongSegmentKind; edge: Edge; position: number } | null>(null)
let generation = 0
let previewElement: HTMLAudioElement | null = null
let previewSlot: MediaSourceSlot | null = null
let previewEnd = 0

const files = computed(() =>
    kinds.flatMap((kind) => {
        const file = fileOf(kind)
        return file ? [{ kind, file }] : []
    }),
)

/** Whether a field differs from the saved correction. */
const dirty = computed(() => kinds.some((kind) => changed(kind)))

function emptyDraft(): Draft {
    return { start: '', end: '', startError: undefined, endError: undefined }
}

function fileOf(kind: SongSegmentKind): FileSongSegment | null {
    return (kind === 'audio' ? segments.value?.audio : segments.value?.video) ?? null
}

function changed(kind: SongSegmentKind): boolean {
    const file = fileOf(kind)
    if (!file) {
        return false
    }

    return (
        drafts[kind].start.trim() !== secondsText(file.manualStartMilliseconds) ||
        drafts[kind].end.trim() !== secondsText(file.manualEndMilliseconds)
    )
}

function fill(value: TrackSongSegments | undefined | null): void {
    segments.value = value ?? null
    for (const kind of kinds) {
        const file = fileOf(kind)
        drafts[kind] = {
            ...emptyDraft(),
            start: secondsText(file?.manualStartMilliseconds),
            end: secondsText(file?.manualEndMilliseconds),
        }
    }
}

async function load(): Promise<void> {
    const current = ++generation
    state.value = 'loading'
    failed.value = false
    stopPreview()
    try {
        const loaded = await getTrackSongSegments(props.trackId)
        if (current === generation) {
            fill(loaded)
            state.value = 'ready'
        }
    } catch {
        if (current === generation) {
            state.value = 'error'
        }
    }
}

function title(kind: SongSegmentKind): string {
    return t(kind === 'audio' ? 'trackSongSegments.audio' : 'trackSongSegments.video')
}

function seconds(milliseconds: number | null | undefined): string {
    return format.number((milliseconds ?? 0) / 1000, {
        minimumFractionDigits: 1,
        maximumFractionDigits: 1,
    })
}

function statusText(file: FileSongSegment): string {
    const fileState = segmentState(file)
    if (fileState === 'sure' || fileState === 'unsure') {
        return t(`trackSongSegments.states.${fileState}`, {
            method: t(methodLabelKey(file)),
            percent: format.number(confidencePercent(file) ?? 0),
        })
    }

    return t(`trackSongSegments.states.${camelCase(fileState)}`)
}

function camelCase(value: string): string {
    return value.replace(/-([a-z])/g, (_match, letter: string) => letter.toUpperCase())
}

function noticed(file: FileSongSegment): boolean {
    return ['unsure', 'mismatch', 'failed'].includes(segmentState(file))
}

function summary(file: FileSongSegment): string {
    const effective = file.effective
    return t('trackSongSegments.summary', {
        start: seconds(Math.max(0, effective?.startMilliseconds ?? 0)),
        end: seconds(effective?.endMilliseconds ?? file.durationMilliseconds),
        duration: seconds(file.durationMilliseconds),
    })
}

function fieldsHint(file: FileSongSegment): string {
    if (file.pending) {
        return t('trackSongSegments.fieldsHintPending')
    }

    return t('trackSongSegments.fieldsHint', {
        start: secondsText(file.detected?.startMilliseconds ?? 0),
        end: secondsText(file.detected?.endMilliseconds ?? file.durationMilliseconds),
    })
}

function rangeMessage(edge: Edge, min?: number | null, max?: number | null): string {
    return t(`trackSongSegments.${edge}Range`, {
        min: secondsText(min ?? 0),
        max: secondsText(max ?? 0),
    })
}

/** The fields in milliseconds, or null after marking the fields that hold no number. */
function values(kind: SongSegmentKind): { start: number | null; end: number | null } | null {
    const draft = drafts[kind]
    const start = parseSeconds(draft.start)
    const end = parseSeconds(draft.end)
    draft.startError = start === undefined ? t('trackSongSegments.invalidNumber') : undefined
    draft.endError = end === undefined ? t('trackSongSegments.invalidNumber') : undefined
    return start === undefined || end === undefined ? null : { start, end }
}

/** Marks the fields the server rejected; true when it named one. */
function showProblems(kind: SongSegmentKind, error: unknown): boolean {
    const problems = problemCodeOf(error) === 'request.invalid' ? validationErrorsOf(error) : []
    for (const problem of problems) {
        if (problem.field === 'startMilliseconds') {
            drafts[kind].startError = rangeMessage('start', problem.min, problem.max)
        } else if (problem.field === 'endMilliseconds') {
            drafts[kind].endError = rangeMessage('end', problem.min, problem.max)
        }
    }

    return problems.length > 0
}

/**
 * Saves the corrections that changed (empty fields remove a correction); true when everything
 * was saved, false when a field holds no valid value or a request failed.
 */
async function save(): Promise<boolean> {
    if (state.value !== 'ready' || !dirty.value) {
        return true
    }

    const pending = kinds.filter((kind) => changed(kind))
    const parsed = pending.map((kind) => ({ kind, value: values(kind) }))
    if (parsed.some((entry) => entry.value === null)) {
        return false
    }

    saving.value = true
    failed.value = false
    let result: TrackSongSegments | undefined
    try {
        for (const { kind, value } of parsed) {
            try {
                result =
                    value!.start === null && value!.end === null
                        ? await resetTrackSongSegment(props.trackId, kind)
                        : await setTrackSongSegment(props.trackId, kind, value!.start, value!.end)
            } catch (error) {
                failed.value = !showProblems(kind, error)
                return false
            }
        }

        if (result) {
            fill(result)
            emit('saved', result)
        }

        return true
    } finally {
        saving.value = false
    }
}

/** Empties the fields of a file: the analysis decides again once the editor saves. */
function useAnalysis(kind: SongSegmentKind): void {
    drafts[kind] = emptyDraft()
}

async function analyzeAgain(): Promise<void> {
    if (analyzing.value) {
        return
    }

    analyzing.value = true
    analysis.value = null
    try {
        await analyzeTrackSong(props.trackId)
        analysis.value = 'queued'
        const kept = { audio: { ...drafts.audio }, video: { ...drafts.video } }
        await load()
        // Fields the admin is still editing stay as they are.
        drafts.audio = kept.audio
        drafts.video = kept.video
    } catch {
        analysis.value = 'failed'
    } finally {
        analyzing.value = false
    }
}

function previewing(kind: SongSegmentKind, edge: Edge): boolean {
    return preview.value?.kind === kind && preview.value.edge === edge
}

/** Plays the file around the start or end of the song as the fields have it (the player pauses). */
async function togglePreview(kind: SongSegmentKind, edge: Edge): Promise<void> {
    if (previewing(kind, edge)) {
        stopPreview()
        return
    }

    stopPreview()
    const file = fileOf(kind)
    const value = values(kind)
    if (!file || !value) {
        return
    }

    const range: PreviewRange = previewRange(
        edge,
        value.start ?? file.effective?.startMilliseconds ?? 0,
        value.end ?? file.effective?.endMilliseconds ?? null,
        file.durationMilliseconds ?? 0,
    )
    const sound = kind === 'video' ? SoundSourceObject.Video : SoundSourceObject.Audio
    preview.value = { kind, edge, position: range.from }
    const request = generation
    try {
        const playback = await getPlayback(props.trackId, browserAudioFormats(), sound)
        if (request !== generation || !previewing(kind, edge) || !playback?.streamUrl) {
            if (previewing(kind, edge)) {
                stopPreview()
            }

            return
        }

        if (player.playback === 'playing') {
            player.pause()
        }

        const element = new Audio()
        previewElement = element
        previewEnd = range.to
        previewSlot = new MediaSourceSlot(element, browserHlsSupport)
        element.addEventListener('loadedmetadata', () => {
            element.currentTime = range.from
        })
        element.addEventListener('timeupdate', onPreviewTime)
        element.addEventListener('ended', stopPreview)
        const slot = previewSlot
        slot.set(
            playback.streamUrl,
            playback.method === 'Transcode' || playback.protocol === 'Hls',
            stopPreview,
        )
        // A stream attaches asynchronously and would drop a play from before.
        if (!(await slot.ready()) || previewElement !== element) {
            return
        }

        await element.play()
    } catch {
        stopPreview()
    }
}

function onPreviewTime(): void {
    const element = previewElement
    if (!element || !preview.value) {
        return
    }

    preview.value = { ...preview.value, position: element.currentTime }
    if (element.currentTime >= previewEnd) {
        stopPreview()
    }
}

function stopPreview(): void {
    preview.value = null
    const element = previewElement
    previewElement = null
    if (element) {
        element.removeEventListener('timeupdate', onPreviewTime)
        element.removeEventListener('ended', stopPreview)
        element.pause()
    }

    previewSlot?.clear()
    previewSlot = null
}

function playheadShare(file: FileSongSegment): number {
    const duration = (file.durationMilliseconds ?? 0) / 1000
    return duration > 0 && preview.value ? Math.min(1, preview.value.position / duration) : 0
}

watch(
    [() => props.active, () => props.trackId],
    ([active]) => {
        if (active) {
            void load()
        } else {
            generation++
            stopPreview()
        }
    },
    { immediate: true },
)

onBeforeUnmount(() => {
    generation++
    stopPreview()
})

defineExpose({ state, saving, dirty, save, load })
</script>

<template>
    <section
        v-if="state !== 'ready' || files.length > 0"
        data-part="section"
        data-item="song-segments"
        :aria-labelledby="titleId"
        :aria-busy="state === 'loading' ? 'true' : undefined"
    >
        <h3 :id="titleId" data-part="section-title">{{ t('trackSongSegments.title') }}</h3>
        <BaseAlert
            v-if="state === 'error'"
            tone="error"
            :message="t('trackSongSegments.loadError')"
        />
        <div v-else data-part="song-segments" :data-state="state">
            <p data-part="form-note">{{ t('trackSongSegments.hint') }}</p>
            <div
                v-for="{ kind, file } in files"
                :key="kind"
                data-part="song-segment"
                :data-kind="kind"
                :data-state="segmentState(file)"
                role="group"
                :aria-labelledby="`${titleId}-${kind}`"
            >
                <div data-part="song-segment-header">
                    <BaseIcon :name="kind === 'audio' ? 'song-audio' : 'song-video'" />
                    <h4 :id="`${titleId}-${kind}`" data-part="song-segment-title">
                        {{ title(kind) }}
                    </h4>
                    <span data-part="song-segment-status" :data-state="segmentState(file)">
                        <BaseIcon v-if="noticed(file)" name="song-segment-notice" />
                        <span data-part="label">{{ statusText(file) }}</span>
                    </span>
                </div>
                <div
                    data-part="song-strip"
                    role="img"
                    :aria-label="summary(file)"
                    :data-state="preview?.kind === kind ? 'previewing' : undefined"
                >
                    <span
                        v-for="(piece, index) in stripPieces(file)"
                        :key="index"
                        data-part="song-strip-piece"
                        :style="{
                            '--wmp-piece-start': piece.start,
                            '--wmp-piece-length': piece.length,
                        }"
                    ></span>
                    <span
                        v-if="preview?.kind === kind"
                        data-part="song-strip-playhead"
                        :style="{ '--wmp-playhead': playheadShare(file) }"
                    ></span>
                </div>
                <p data-part="song-strip-legend">{{ summary(file) }}</p>
                <div data-part="field-row">
                    <BaseTextField
                        v-model="drafts[kind].start"
                        data-field="song-start"
                        :label="t('trackSongSegments.start')"
                        :placeholder="secondsText(file.detected?.startMilliseconds ?? 0)"
                        :error="drafts[kind].startError"
                        :disabled="state !== 'ready' || saving"
                        inputmode="decimal"
                        autocomplete="off"
                    />
                    <BaseTextField
                        v-model="drafts[kind].end"
                        data-field="song-end"
                        :label="t('trackSongSegments.end')"
                        :placeholder="
                            secondsText(file.detected?.endMilliseconds ?? file.durationMilliseconds)
                        "
                        :error="drafts[kind].endError"
                        :disabled="state !== 'ready' || saving"
                        inputmode="decimal"
                        autocomplete="off"
                    />
                </div>
                <p data-part="form-note">{{ fieldsHint(file) }}</p>
                <div data-part="view-actions">
                    <BaseButton
                        icon="preview"
                        data-item="preview-song-start"
                        :label="t('trackSongSegments.previewStart')"
                        :pressed="previewing(kind, 'start')"
                        @click="togglePreview(kind, 'start')"
                    />
                    <BaseButton
                        icon="preview"
                        data-item="preview-song-end"
                        :label="t('trackSongSegments.previewEnd')"
                        :pressed="previewing(kind, 'end')"
                        @click="togglePreview(kind, 'end')"
                    />
                    <BaseButton
                        v-if="drafts[kind].start !== '' || drafts[kind].end !== ''"
                        data-item="use-analysis"
                        :label="t('trackSongSegments.useAnalysis')"
                        :disabled="saving"
                        @click="useAnalysis(kind)"
                    />
                </div>
            </div>
            <BaseAlert v-if="failed" tone="error" :message="t('trackSongSegments.saveError')" />
            <BaseAlert
                v-if="analysis === 'queued'"
                tone="success"
                :message="t('trackSongSegments.analysisQueued')"
            />
            <BaseAlert
                v-if="analysis === 'failed'"
                tone="error"
                :message="t('trackSongSegments.analysisError')"
            />
            <div v-if="files.length > 0" data-part="view-actions">
                <BaseButton
                    icon="analyze-again"
                    data-item="analyze-song"
                    :label="t('trackSongSegments.analyzeAgain')"
                    :disabled="analyzing || state !== 'ready'"
                    @click="analyzeAgain"
                />
            </div>
        </div>
    </section>
</template>
