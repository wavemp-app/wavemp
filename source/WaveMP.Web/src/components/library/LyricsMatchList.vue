<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { nextSegmentIndex, segmentTabStop } from '@/components/base/segmented'
import { useFormat } from '@/composables/useFormat'
import { indicatorVariables, useIndicator } from '@/composables/useIndicator'
import type { LyricsMatch } from '@/lyrics/api'

/**
 * The tracks of the online lyrics service an admin can choose for a song, as a radio group with the
 * parts of the online matches: title, artist, album and as details the duration, whether the lyrics
 * have times, and whether the track is another version (another duration) of which only the plain
 * lyrics are kept. Like the other radio groups, the list holds an indicator part and tells themes
 * the position of the selected track (--wmp-active-index, --wmp-item-count, --wmp-indicator-*).
 */
const props = defineProps<{
    matches: readonly LyricsMatch[]
    /** Element with the accessible name of the group. */
    labelledby: string
    disabled?: boolean
}>()

const selected = defineModel<number | null>({ default: null })

const { t } = useI18n()
const format = useFormat()

const group = ref<HTMLElement | null>(null)
const checked = computed(() => props.matches.findIndex((match) => match.id === selected.value))
const tabStop = computed(() =>
    segmentTabStop(
        checked.value,
        props.matches.map(() => false),
    ),
)
const indicator = useIndicator(group, '[aria-checked="true"]', () => [
    checked.value,
    props.matches.length,
])

function choose(match: LyricsMatch): void {
    if (!props.disabled) {
        selected.value = match.id
    }
}

async function onKeydown(event: KeyboardEvent): Promise<void> {
    const next = nextSegmentIndex(
        event.key,
        tabStop.value,
        props.matches.map(() => false),
    )
    if (next === null || props.disabled) {
        return
    }

    event.preventDefault()
    const match = props.matches[next]
    if (match) {
        selected.value = match.id
    }
    await nextTick()
    group.value?.querySelectorAll<HTMLElement>('[data-part="online-match"]')[next]?.focus()
}
</script>

<template>
    <div
        ref="group"
        data-part="online-matches"
        data-item="lyrics"
        role="radiogroup"
        :aria-labelledby="labelledby"
        :aria-disabled="disabled ? 'true' : undefined"
        :style="indicatorVariables(matches.length, checked, indicator)"
        @keydown="onKeydown"
    >
        <span data-part="indicator" aria-hidden="true"></span>
        <button
            v-for="(match, index) in matches"
            :key="match.id"
            type="button"
            data-part="online-match"
            role="radio"
            :data-state="match.id === selected ? 'selected' : undefined"
            :aria-checked="match.id === selected ? 'true' : 'false'"
            :aria-disabled="disabled ? 'true' : undefined"
            :tabindex="index === tabStop ? 0 : -1"
            @click="choose(match)"
        >
            <span data-part="online-match-title">{{ match.title }}</span>
            <span v-if="match.artist" data-part="online-match-artist">{{ match.artist }}</span>
            <span data-part="online-match-album">
                {{ match.album || t('onlineMetadata.withoutAlbum') }}
            </span>
            <span data-part="online-match-release">
                <span
                    v-if="match.duration !== null"
                    data-part="online-match-detail"
                    data-field="duration"
                >
                    {{ format.duration(match.duration) }}
                </span>
                <span
                    data-part="online-match-detail"
                    data-field="synchronized"
                    :data-state="match.synchronized ? 'synchronized' : 'static'"
                >
                    {{
                        match.synchronized
                            ? t('trackLyrics.search.synchronized')
                            : t('trackLyrics.search.static')
                    }}
                </span>
                <span
                    v-if="!match.fitsDuration"
                    data-part="online-match-detail"
                    data-field="other-version"
                >
                    {{ t('trackLyrics.search.otherVersion') }}
                </span>
            </span>
        </button>
    </div>
</template>
