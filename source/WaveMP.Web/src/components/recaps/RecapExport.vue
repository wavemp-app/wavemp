<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseProgress from '@/components/base/BaseProgress.vue'
import BaseSegmentedField from '@/components/base/BaseSegmentedField.vue'
import { useMediaQuery } from '@/composables/useMediaQuery'
import { convertRecapVideo } from '@/recaps/api'
import { canvasToBlob, renderRecapPicture, type RecapPictureFormat } from '@/recaps/canvas'
import { loadRecapCovers } from '@/recaps/covers'
import { findRecapDesign, readRecapPalette } from '@/recaps/designs'
import { recapFileName, shareOrDownload } from '@/recaps/share'
import type { RecapSlide } from '@/recaps/slides'
import { canRecordVideo, recordRecapVideo } from '@/recaps/video'

/**
 * Pictures and videos of a recap: the shown section as PNG in the story format (1080 x 1920) or
 * square (1080 x 1080), and the whole story as MP4 video. Phones and tablets hand the file to the
 * share sheet of the system, the desktop downloads it. The colors come from the shown story, so
 * the files look like the page. Emits busy while it works, so the story can pause.
 */
const props = defineProps<{
    slides: readonly RecapSlide[]
    index: number
    design: string
    /** The shown story, whose design variables color the pictures. */
    story: HTMLElement | null
    /** Part of the file names, e.g. the period. */
    fileStem: string
}>()

const emit = defineEmits<{ busy: [busy: boolean] }>()

const { t } = useI18n()
const touch = useMediaQuery('(pointer: coarse)')
const format = ref<RecapPictureFormat>('story')
const busy = ref<'picture' | 'video' | null>(null)
const videoProgress = ref<number | null>(null)
const converting = ref(false)
const message = ref<{ tone: 'success' | 'error'; text: string } | null>(null)
const videoSupported = canRecordVideo()
let abort: AbortController | null = null

const formats = computed(() => [
    { value: 'story', label: t('recap.formats.story') },
    { value: 'square', label: t('recap.formats.square') },
])

const selectedFormat = computed({
    get: () => format.value as string,
    set: (value: string) => (format.value = value === 'square' ? 'square' : 'story'),
})

function setBusy(value: 'picture' | 'video' | null): void {
    busy.value = value
    emit('busy', value !== null)
}

function frameBase() {
    return {
        palette: readRecapPalette(props.story),
        decoration: findRecapDesign(props.design).decoration,
        brand: t('app.name'),
    }
}

async function savePicture(): Promise<void> {
    const slide = props.slides[props.index]
    if (!slide || busy.value) {
        return
    }

    setBusy('picture')
    message.value = null
    const covers = await loadRecapCovers([slide])
    try {
        const canvas = renderRecapPicture({
            ...frameBase(),
            slide,
            format: format.value,
            covers: covers.images,
        })
        const blob = await canvasToBlob(canvas)
        const file = new File([blob], recapFileName(props.fileStem, slide.kind, 'png'), {
            type: 'image/png',
        })
        const outcome = await shareOrDownload(file, t('recap.shareTitle'))
        if (outcome !== 'cancelled') {
            message.value = {
                tone: 'success',
                text: outcome === 'shared' ? t('recap.pictureShared') : t('recap.pictureSaved'),
            }
        }
    } catch {
        message.value = { tone: 'error', text: t('recap.pictureFailed') }
    } finally {
        covers.release()
        setBusy(null)
    }
}

async function saveVideo(): Promise<void> {
    if (busy.value || props.slides.length === 0) {
        return
    }

    setBusy('video')
    message.value = null
    videoProgress.value = 0
    abort = new AbortController()
    const covers = await loadRecapCovers(props.slides)
    try {
        const recorded = await recordRecapVideo({
            slides: props.slides,
            frame: { ...frameBase(), covers: covers.images },
            signal: abort.signal,
            onProgress: (share) => (videoProgress.value = share),
        })
        let video = recorded.blob
        if (!recorded.mp4) {
            converting.value = true
            video = await convertRecapVideo(recorded.blob)
        }
        const file = new File([video], recapFileName(props.fileStem, 'video', 'mp4'), {
            type: 'video/mp4',
        })
        const outcome = await shareOrDownload(file, t('recap.shareTitle'))
        if (outcome !== 'cancelled') {
            message.value = {
                tone: 'success',
                text: outcome === 'shared' ? t('recap.videoShared') : t('recap.videoSaved'),
            }
        }
    } catch (error) {
        if (!(error instanceof DOMException && error.name === 'AbortError')) {
            message.value = { tone: 'error', text: t('recap.videoFailed') }
        }
    } finally {
        covers.release()
        abort = null
        converting.value = false
        videoProgress.value = null
        setBusy(null)
    }
}

function cancelVideo(): void {
    abort?.abort()
}

onBeforeUnmount(cancelVideo)
</script>

<template>
    <div data-part="recap-export" :aria-busy="busy ? 'true' : undefined">
        <BaseSegmentedField
            v-model="selectedFormat"
            item="recap-picture-format"
            :label="t('recap.pictureFormat')"
            :options="formats"
        />
        <div data-part="view-actions" data-item="recap-export-actions">
            <BaseButton
                data-item="save-picture"
                icon="save-picture"
                variant="primary"
                :label="touch ? t('recap.sharePicture') : t('recap.savePicture')"
                :disabled="busy !== null || slides.length === 0"
                @click="savePicture"
            />
            <BaseButton
                v-if="videoSupported"
                data-item="save-video"
                icon="save-video"
                :label="touch ? t('recap.shareVideo') : t('recap.saveVideo')"
                :disabled="busy !== null || slides.length === 0"
                @click="saveVideo"
            />
            <BaseButton
                v-if="busy === 'video' && !converting"
                data-item="cancel-video"
                icon="close"
                :label="t('recap.cancelVideo')"
                @click="cancelVideo"
            />
        </div>
        <BaseProgress
            v-if="busy === 'video'"
            :label="converting ? t('recap.converting') : t('recap.recording')"
            :value="converting ? null : videoProgress"
        />
        <p v-if="!videoSupported" data-part="form-note" data-item="video-unsupported">
            {{ t('recap.videoUnsupported') }}
        </p>
        <BaseAlert v-if="message" :tone="message.tone" :message="message.text" />
    </div>
</template>
