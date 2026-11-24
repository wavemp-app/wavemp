<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ArtistPictureCandidate } from '@/api/generated/models'
import { nextSegmentIndex, segmentTabStop } from '@/components/base/segmented'
import { useFormat } from '@/composables/useFormat'
import { indicatorVariables, useIndicator } from '@/composables/useIndicator'
import { creditPartsOf } from '@/library/artistPictures'
import { artistCandidateId } from '@/stores/covers'
import LibraryCover from './LibraryCover.vue'

/**
 * Pictures of an artist found online, as a radio group: each with its preview (loaded through the
 * server, which also keeps the address of the viewer away from the services), the service, the
 * author, the license and the size in pixels, all as text. Nothing is taken over before the admin
 * confirms. Like the other radio groups, the list holds an indicator part and tells themes the
 * position of the chosen picture. v-model is the id of the candidate.
 */
const props = defineProps<{
    artistId: string
    candidates: readonly ArtistPictureCandidate[]
    /** Element with the accessible name of the group. */
    labelledby: string
    disabled?: boolean
}>()

const selected = defineModel<string | null>({ default: null })

const { t } = useI18n()
const format = useFormat()

const group = ref<HTMLElement | null>(null)
const checked = computed(() =>
    props.candidates.findIndex((candidate) => candidate.id === selected.value),
)
const tabStop = computed(() =>
    segmentTabStop(
        checked.value,
        props.candidates.map(() => false),
    ),
)
const indicator = useIndicator(group, '[aria-checked="true"]', () => [
    checked.value,
    props.candidates.length,
])

function choose(candidate: ArtistPictureCandidate): void {
    if (!props.disabled && candidate.id) {
        selected.value = candidate.id
    }
}

async function onKeydown(event: KeyboardEvent): Promise<void> {
    const next = nextSegmentIndex(
        event.key,
        tabStop.value,
        props.candidates.map(() => false),
    )
    if (next === null || props.disabled) {
        return
    }

    event.preventDefault()
    const candidate = props.candidates[next]
    if (candidate) {
        choose(candidate)
    }
    await nextTick()
    group.value?.querySelectorAll<HTMLElement>('[data-part="picture-candidate"]')[next]?.focus()
}

/** What each candidate shows, as text: service, author, license and size. */
const rows = computed(() =>
    props.candidates.map((candidate, index) => {
        const credit = creditPartsOf(candidate)
        return {
            key: candidate.id ?? String(index),
            candidate,
            provider: t(credit?.providerKey ?? 'artistPictures.providers.wikimedia'),
            author: credit?.author ?? null,
            license: credit?.license ?? null,
            size:
                candidate.width && candidate.height
                    ? t('artistPictures.online.size', {
                          width: format.number(candidate.width),
                          height: format.number(candidate.height),
                      })
                    : null,
        }
    }),
)
</script>

<template>
    <div
        ref="group"
        data-part="picture-candidate-list"
        role="radiogroup"
        :aria-labelledby="labelledby"
        :aria-disabled="disabled ? 'true' : undefined"
        :style="indicatorVariables(candidates.length, checked, indicator)"
        @keydown="onKeydown"
    >
        <span data-part="indicator" aria-hidden="true"></span>
        <button
            v-for="({ key, candidate, provider, author, license, size }, index) in rows"
            :key="key"
            type="button"
            data-part="picture-candidate"
            role="radio"
            :data-provider="candidate.provider ?? undefined"
            :data-state="candidate.id === selected ? 'selected' : undefined"
            :aria-checked="candidate.id === selected ? 'true' : 'false'"
            :aria-disabled="disabled ? 'true' : undefined"
            :tabindex="index === tabStop ? 0 : -1"
            @click="choose(candidate)"
        >
            <LibraryCover
                kind="artist-candidate"
                :id="artistCandidateId(artistId, candidate.id ?? '')"
                :available="!!candidate.id"
            />
            <span data-part="picture-candidate-provider">{{ provider }}</span>
            <span v-if="author" data-part="picture-candidate-author">{{ author }}</span>
            <span v-if="license" data-part="picture-candidate-license">{{ license }}</span>
            <span v-if="size" data-part="picture-candidate-size">{{ size }}</span>
        </button>
    </div>
</template>
