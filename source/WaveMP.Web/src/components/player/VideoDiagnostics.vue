<script setup lang="ts">
import { computed, ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseButton from '@/components/base/BaseButton.vue'
import type { VideoDiagnosticsEvent, VideoDiagnosticsSnapshot } from '@/player/videoDiagnostics'

/**
 * The diagnostics of the video on the stage, a developer setting of the device: how the video
 * follows the music (drift, speed, seeks, buffer, dropped pictures, file or HLS) and the newest
 * entries of its log, with each stutter. The whole log of the current video can be copied.
 */
const props = defineProps<{
    snapshot: VideoDiagnosticsSnapshot
}>()

const { t } = useI18n()
const titleId = useId()
const copyState = ref<'idle' | 'copied' | 'failed'>('idle')

/** Entries of the log the overlay shows, newest first. */
const shownEvents = 6

function seconds(value: number, digits = 1): string {
    return t('videoDiagnostics.seconds', { value: value.toFixed(digits) })
}

function milliseconds(value: number): string {
    const rounded = Math.round(value * 1000)
    return t('videoDiagnostics.milliseconds', { value: rounded > 0 ? `+${rounded}` : `${rounded}` })
}

const values = computed(() => {
    const snapshot = props.snapshot
    const unknown = t('videoDiagnostics.unknown')
    return [
        { field: 'delivery', label: t('videoDiagnostics.delivery'), value: deliveryLabel() },
        {
            field: 'drift',
            label: t('videoDiagnostics.drift'),
            value: snapshot.drift === null ? unknown : milliseconds(snapshot.drift),
        },
        {
            field: 'rate',
            label: t('videoDiagnostics.rate'),
            value: t('videoDiagnostics.rateValue', { value: snapshot.rate.toFixed(2) }),
        },
        {
            field: 'action',
            label: t('videoDiagnostics.action'),
            value: snapshot.action ? actionLabel(snapshot.action) : unknown,
        },
        {
            field: 'seeks',
            label: t('videoDiagnostics.seeks'),
            value: t('videoDiagnostics.seeksValue', {
                seeks: snapshot.seeks,
                missed: snapshot.missedSeeks,
            }),
        },
        {
            field: 'seek-latency',
            label: t('videoDiagnostics.seekLatency'),
            value: snapshot.seekLatency === null ? unknown : seconds(snapshot.seekLatency, 2),
        },
        {
            field: 'ready-state',
            label: t('videoDiagnostics.readyState'),
            value: String(snapshot.readyState),
        },
        {
            field: 'buffered',
            label: t('videoDiagnostics.buffered'),
            value: snapshot.buffered
                ? t('videoDiagnostics.bufferedValue', {
                      start: snapshot.buffered.start.toFixed(1),
                      end: snapshot.buffered.end.toFixed(1),
                      ahead: snapshot.bufferedAhead.toFixed(1),
                  })
                : t('videoDiagnostics.nothingBuffered'),
        },
        {
            field: 'dropped-frames',
            label: t('videoDiagnostics.droppedFrames'),
            value:
                snapshot.droppedFrames === null
                    ? unknown
                    : t('videoDiagnostics.droppedFramesValue', {
                          dropped: snapshot.droppedFrames,
                          total: snapshot.totalFrames ?? 0,
                      }),
        },
        {
            field: 'stutter',
            label: t('videoDiagnostics.stutter'),
            value: snapshot.stuttering
                ? t('videoDiagnostics.stuttering', { count: snapshot.stutters })
                : t('videoDiagnostics.smooth', { count: snapshot.stutters }),
        },
    ]
})

const events = computed(() => props.snapshot.events.slice(-shownEvents).reverse())

function deliveryLabel(): string {
    return props.snapshot.delivery === 'hls'
        ? t('videoDiagnostics.deliveries.hls')
        : t('videoDiagnostics.deliveries.file')
}

function actionLabel(action: string): string {
    switch (action) {
        case 'hold':
            return t('videoDiagnostics.actions.hold')
        case 'seek':
            return t('videoDiagnostics.actions.seek')
        case 'wait':
            return t('videoDiagnostics.actions.wait')
        default:
            return t('videoDiagnostics.actions.rate')
    }
}

function eventLabel(event: VideoDiagnosticsEvent): string {
    const value = event.value ?? 0
    switch (event.kind) {
        case 'seek':
            return t('videoDiagnostics.events.seek', { position: value.toFixed(2) })
        case 'rate':
            return t('videoDiagnostics.events.rate', { value: value.toFixed(2) })
        case 'waiting':
            return t('videoDiagnostics.events.waiting')
        case 'stalled':
            return t('videoDiagnostics.events.stalled')
        case 'stutter-start':
            return t('videoDiagnostics.events.stutterStart')
        case 'stutter-end':
            return t('videoDiagnostics.events.stutterEnd', { length: value.toFixed(1) })
        default:
            return t('videoDiagnostics.events.error')
    }
}

function eventDetails(event: VideoDiagnosticsEvent): string {
    return t('videoDiagnostics.eventDetails', {
        drift: event.drift === null ? t('videoDiagnostics.unknown') : milliseconds(event.drift),
        readyState: event.readyState,
        ahead: event.bufferedAhead.toFixed(1),
    })
}

/** Copies the whole log of the current video as JSON, e.g. for a bug report. */
async function copyLog(): Promise<void> {
    const report = {
        userAgent: typeof navigator === 'undefined' ? null : navigator.userAgent,
        ...props.snapshot,
    }
    try {
        await navigator.clipboard.writeText(JSON.stringify(report, null, 2))
        copyState.value = 'copied'
    } catch {
        copyState.value = 'failed'
    }
}
</script>

<template>
    <aside
        data-part="video-diagnostics"
        :data-state="snapshot.stuttering ? 'stutter' : 'smooth'"
        :data-delivery="snapshot.delivery"
        :aria-labelledby="titleId"
        @dblclick.stop
    >
        <h2 :id="titleId" data-part="diagnostics-title">{{ t('videoDiagnostics.title') }}</h2>
        <dl data-part="diagnostics-values">
            <div
                v-for="item in values"
                :key="item.field"
                data-part="diagnostics-value"
                :data-field="item.field"
            >
                <dt data-part="label">{{ item.label }}</dt>
                <dd data-part="diagnostics-figure">{{ item.value }}</dd>
            </div>
        </dl>
        <ol
            v-if="events.length > 0"
            data-part="diagnostics-log"
            :aria-label="t('videoDiagnostics.log')"
        >
            <li
                v-for="(event, index) in events"
                :key="`${event.at}-${index}`"
                data-part="diagnostics-event"
                :data-kind="event.kind"
            >
                <span data-part="diagnostics-time">{{ seconds(event.at) }}</span>
                <span data-part="label">{{ eventLabel(event) }}</span>
                <span data-part="diagnostics-figure">{{ eventDetails(event) }}</span>
            </li>
        </ol>
        <BaseButton
            data-item="copy-video-diagnostics"
            :label="
                copyState === 'copied'
                    ? t('videoDiagnostics.copied')
                    : copyState === 'failed'
                      ? t('videoDiagnostics.copyFailed')
                      : t('videoDiagnostics.copy')
            "
            :state="copyState === 'idle' ? undefined : copyState"
            @click.stop="copyLog"
        />
    </aside>
</template>
