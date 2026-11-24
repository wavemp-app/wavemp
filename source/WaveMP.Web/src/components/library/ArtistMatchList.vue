<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { MusicBrainzArtistMatch } from '@/api/generated/models'
import { nextSegmentIndex, segmentTabStop } from '@/components/base/segmented'
import { indicatorVariables, useIndicator } from '@/composables/useIndicator'
import { lifeSpanOf } from '@/library/artistPictures'

/**
 * Artists of MusicBrainz whose name matches the artist, as a radio group, the best first: the name
 * and what tells artists of the same name apart (description, kind, country, years). The artist
 * chosen in an earlier lookup is marked (data-state remembered). Like the other radio groups, the
 * list holds an indicator part and tells themes the position of the chosen artist
 * (--wmp-active-index, --wmp-item-count, --wmp-indicator-*). v-model is the MusicBrainz id.
 */
const props = defineProps<{
    matches: readonly MusicBrainzArtistMatch[]
    /** Element with the accessible name of the group. */
    labelledby: string
    disabled?: boolean
}>()

const selected = defineModel<string | null>({ default: null })

const { t } = useI18n()

const group = ref<HTMLElement | null>(null)
const checked = computed(() =>
    props.matches.findIndex((match) => match.musicBrainzArtistId === selected.value),
)
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

function choose(match: MusicBrainzArtistMatch): void {
    if (!props.disabled && match.musicBrainzArtistId) {
        selected.value = match.musicBrainzArtistId
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
        choose(match)
    }
    await nextTick()
    group.value?.querySelectorAll<HTMLElement>('[data-part="artist-match"]')[next]?.focus()
}

/** The details of a match that tell it apart from artists of the same name. */
function details(match: MusicBrainzArtistMatch): string[] {
    const span = lifeSpanOf(match)
    return [
        match.disambiguation,
        match.type,
        match.country,
        span
            ? span.end
                ? t('artistPictures.online.lifeSpan', { begin: span.begin, end: span.end })
                : t('artistPictures.online.lifeSpanOpen', { begin: span.begin })
            : null,
    ].filter((value): value is string => !!value && value.trim() !== '')
}

function states(match: MusicBrainzArtistMatch): string | undefined {
    const tokens = [
        match.musicBrainzArtistId === selected.value ? 'selected' : null,
        match.isRemembered ? 'remembered' : null,
    ].filter(Boolean)
    return tokens.length > 0 ? tokens.join(' ') : undefined
}
</script>

<template>
    <div
        ref="group"
        data-part="artist-match-list"
        role="radiogroup"
        :aria-labelledby="labelledby"
        :aria-disabled="disabled ? 'true' : undefined"
        :style="indicatorVariables(matches.length, checked, indicator)"
        @keydown="onKeydown"
    >
        <span data-part="indicator" aria-hidden="true"></span>
        <button
            v-for="(match, index) in matches"
            :key="match.musicBrainzArtistId ?? index"
            type="button"
            data-part="artist-match"
            role="radio"
            :data-state="states(match)"
            :aria-checked="match.musicBrainzArtistId === selected ? 'true' : 'false'"
            :aria-disabled="disabled ? 'true' : undefined"
            :tabindex="index === tabStop ? 0 : -1"
            @click="choose(match)"
        >
            <span data-part="artist-match-name">{{ match.name }}</span>
            <span v-if="details(match).length > 0" data-part="artist-match-details">
                <span
                    v-for="(detail, position) in details(match)"
                    :key="position"
                    data-part="label"
                    >{{ detail }}</span
                >
            </span>
            <span v-if="match.isRemembered" data-part="badge" data-state="remembered">
                <span data-part="label">{{ t('artistPictures.online.remembered') }}</span>
            </span>
        </button>
    </div>
</template>
