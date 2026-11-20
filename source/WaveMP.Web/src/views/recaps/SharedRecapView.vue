<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { SharedRecap } from '@/api/generated/models'
import { problemCodeOf, statusOf } from '@/api/problems'
import BaseButton from '@/components/base/BaseButton.vue'
import RecapExport from '@/components/recaps/RecapExport.vue'
import RecapNav from '@/components/recaps/RecapNav.vue'
import RecapStory from '@/components/recaps/RecapStory.vue'
import { useDetailTitle } from '@/composables/useDetailTitle'
import { useRecapSlides } from '@/composables/useRecapSlides'
import { getSharedRecap } from '@/recaps/api'
import { findRecapDesign } from '@/recaps/designs'
import { periodItem, periodLabelKey } from '@/recaps/labels'

/**
 * A recap another profile (or this one) shares: its story in the design the owner chose, counted
 * from the play history of the owner for the days of the share, with pictures and a video.
 */
const props = defineProps<{ id: string }>()

const { t } = useI18n()
const recap = ref<SharedRecap | null>(null)
const status = ref<'loading' | 'ready' | 'not-found' | 'error'>('loading')
const index = ref(0)
const story = ref<InstanceType<typeof RecapStory> | null>(null)
let generation = 0

const share = computed(() => recap.value?.share)
const owner = computed(() => share.value?.owner?.name ?? '')
const periodLabel = computed(() => t(periodLabelKey(share.value?.period)))
const design = computed(() => findRecapDesign(share.value?.design).id)
const slides = useRecapSlides(
    computed(() => recap.value?.statistics),
    owner,
    periodLabel,
)
const storyElement = computed(() => (story.value?.element as HTMLElement | null) ?? null)

useDetailTitle(() =>
    share.value ? t('recap.sharedTitle', { name: owner.value, period: periodLabel.value }) : null,
)

async function load(): Promise<void> {
    const current = ++generation
    status.value = 'loading'
    try {
        const loaded = await getSharedRecap(props.id)
        if (current === generation) {
            recap.value = loaded ?? null
            status.value = loaded ? 'ready' : 'not-found'
        }
    } catch (error) {
        if (current === generation) {
            recap.value = null
            status.value =
                statusOf(error) === 404 || problemCodeOf(error) === 'recap_share.not_found'
                    ? 'not-found'
                    : 'error'
        }
    }
}

function onBusy(busy: boolean): void {
    if (busy) {
        story.value?.pause()
    }
}

watch(() => props.id, load, { immediate: true })
</script>

<template>
    <div
        data-part="view"
        data-view="shared-recap"
        :data-state="status"
        :aria-busy="status === 'loading' || undefined"
    >
        <RecapNav />
        <div v-if="status === 'not-found'" data-part="empty-state" data-item="shared-recap-missing">
            <p data-part="empty-state-title">{{ t('recap.sharedMissingTitle') }}</p>
            <p data-part="empty-state-text">{{ t('recap.sharedMissingText') }}</p>
        </div>
        <div v-else-if="status === 'error'" data-part="empty-state" data-item="shared-recap-error">
            <p data-part="empty-state-title">{{ t('recap.loadError') }}</p>
            <BaseButton data-item="retry" :label="t('library.retry')" @click="load" />
        </div>
        <div v-else data-part="recap-layout">
            <div data-part="recap-stage">
                <RecapStory
                    v-if="slides.length > 0"
                    ref="story"
                    v-model:index="index"
                    :slides="slides"
                    :design="design"
                    :label="t('recap.sharedTitle', { name: owner, period: periodLabel })"
                />
                <div
                    v-else
                    data-part="recap-story"
                    :data-recap-design="design"
                    data-state="loading"
                    aria-hidden="true"
                >
                    <span data-part="recap-backdrop"></span>
                </div>
            </div>
            <div data-part="recap-panel">
                <section data-part="section" data-item="recap-share">
                    <RecapExport
                        :slides="slides"
                        :index="index"
                        :design="design"
                        :story="storyElement"
                        :file-stem="share?.period ? periodItem(share.period) : ''"
                        @busy="onBusy"
                    />
                </section>
            </div>
        </div>
    </div>
</template>
