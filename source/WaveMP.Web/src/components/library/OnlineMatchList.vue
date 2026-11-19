<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseMeter from '@/components/base/BaseMeter.vue'
import { nextSegmentIndex, segmentTabStop } from '@/components/base/segmented'
import { useFormat } from '@/composables/useFormat'
import { indicatorVariables, useIndicator } from '@/composables/useIndicator'
import { releaseYear, type OnlineMatch, type OnlineOwner } from '@/library/onlineMetadata'

/**
 * The matches of the online search as a radio group, the best first: what each suggests (title or
 * album, artist), its release (year, country, format, number of songs) and its agreement as meter
 * and in percent. The agreement is also the runtime variable --wmp-agreement (0 to 1) of each
 * match. Like the other radio groups, the list holds an indicator part and tells themes the
 * position of the selected match (--wmp-active-index, --wmp-item-count, --wmp-indicator-*).
 */
const props = defineProps<{
    owner: OnlineOwner
    matches: readonly OnlineMatch[]
    /** Element with the accessible name of the group. */
    labelledby: string
    disabled?: boolean
}>()

const selected = defineModel<string | null>({ default: null })

const { t } = useI18n()
const format = useFormat()

const group = ref<HTMLElement | null>(null)
const checked = computed(() => props.matches.findIndex((match) => match.key === selected.value))
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

function choose(match: OnlineMatch): void {
    if (!props.disabled) {
        selected.value = match.key
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
        selected.value = match.key
    }
    await nextTick()
    group.value?.querySelectorAll<HTMLElement>('[data-part="online-match"]')[next]?.focus()
}

function heading(match: OnlineMatch): string {
    const value = props.owner === 'album' ? match.values.album : match.values.title
    return typeof value === 'string' ? value : ''
}

function artist(match: OnlineMatch): string | null {
    const value = props.owner === 'album' ? match.values.albumArtist : match.values.artist
    return typeof value === 'string' && value ? value : null
}

function album(match: OnlineMatch): string {
    const value = match.values.album
    return typeof value === 'string' && value ? value : t('onlineMetadata.withoutAlbum')
}

function agreementText(match: OnlineMatch): string {
    return t('onlineMetadata.agreementValue', { value: format.number(match.agreement) })
}
</script>

<template>
    <div
        ref="group"
        data-part="online-matches"
        role="radiogroup"
        :aria-labelledby="labelledby"
        :aria-disabled="disabled ? 'true' : undefined"
        :style="indicatorVariables(matches.length, checked, indicator)"
        @keydown="onKeydown"
    >
        <span data-part="indicator" aria-hidden="true"></span>
        <button
            v-for="(match, index) in matches"
            :key="match.key"
            type="button"
            data-part="online-match"
            role="radio"
            :data-state="match.key === selected ? 'selected' : undefined"
            :aria-checked="match.key === selected ? 'true' : 'false'"
            :aria-disabled="disabled ? 'true' : undefined"
            :tabindex="index === tabStop ? 0 : -1"
            :style="{ '--wmp-agreement': match.agreement / 100 }"
            @click="choose(match)"
        >
            <span data-part="online-match-title">{{ heading(match) }}</span>
            <span v-if="artist(match)" data-part="online-match-artist">{{ artist(match) }}</span>
            <span v-if="owner === 'track'" data-part="online-match-album">{{ album(match) }}</span>
            <span v-if="match.release" data-part="online-match-release">
                <span
                    v-if="releaseYear(match.release)"
                    data-part="online-match-detail"
                    data-field="year"
                >
                    {{ format.year(releaseYear(match.release) ?? 0) }}
                </span>
                <span
                    v-if="match.release.country"
                    data-part="online-match-detail"
                    data-field="country"
                >
                    {{ match.release.country }}
                </span>
                <span
                    v-if="match.release.format"
                    data-part="online-match-detail"
                    data-field="format"
                >
                    {{ match.release.format }}
                </span>
                <span
                    v-if="match.release.trackCount"
                    data-part="online-match-detail"
                    data-field="tracks"
                >
                    {{
                        t('onlineMetadata.trackCount', {
                            count: format.number(match.release.trackCount),
                        })
                    }}
                </span>
            </span>
            <span data-part="online-match-agreement">
                <BaseMeter
                    :label="t('onlineMetadata.agreement')"
                    :value="match.agreement / 100"
                    :value-text="agreementText(match)"
                />
                <span data-part="label" aria-hidden="true">{{ agreementText(match) }}</span>
            </span>
        </button>
    </div>
</template>
