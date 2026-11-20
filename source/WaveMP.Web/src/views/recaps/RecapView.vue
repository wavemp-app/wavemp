<script setup lang="ts">
import { computed, onActivated, onMounted, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseCheckbox from '@/components/base/BaseCheckbox.vue'
import BaseSegmentedField from '@/components/base/BaseSegmentedField.vue'
import RecapExport from '@/components/recaps/RecapExport.vue'
import RecapNav from '@/components/recaps/RecapNav.vue'
import RecapPeriodPicker from '@/components/recaps/RecapPeriodPicker.vue'
import RecapStory from '@/components/recaps/RecapStory.vue'
import { useRecapSlides } from '@/composables/useRecapSlides'
import { loadRecapDesign, recapDesigns, saveRecapDesign } from '@/recaps/designs'
import { periodItem, periodLabelKey } from '@/recaps/labels'
import { useRecapsStore } from '@/stores/recaps'
import { useSessionStore } from '@/stores/session'

/**
 * The recap of the profile: its listening of a period as a story of animated sections, in one of
 * the designs, with pictures and a video to share and the choice to share the recap with the
 * other profiles (off by default).
 */
const { t } = useI18n()
const recaps = useRecapsStore()
const session = useSessionStore()

const design = ref<string>(loadRecapDesign())
const index = ref(0)
const story = ref<InstanceType<typeof RecapStory> | null>(null)
const sharing = ref(false)
const shareError = ref(false)
const designTitleId = useId()
const shareTitleId = useId()

const period = computed({
    get: () => recaps.period,
    set: (value) => recaps.setPeriod(value),
})
const custom = computed({
    get: () => recaps.custom,
    set: (value) => recaps.setCustom(value),
})

const owner = computed(() => session.profile?.name ?? '')
const periodLabel = computed(() => t(periodLabelKey(recaps.period)))
const slides = useRecapSlides(
    computed(() => recaps.statistics),
    owner,
    periodLabel,
)
const designOptions = computed(() =>
    recapDesigns.map((entry) => ({ value: entry.id, label: t(entry.labelKey) })),
)
const storyElement = computed(() => (story.value?.element as HTMLElement | null) ?? null)
const shared = computed(() => !!recaps.currentShare)

watch(design, async (value) => {
    saveRecapDesign(value)
    // A shared recap shows the chosen design to the others as well.
    if (recaps.currentShare) {
        await updateSharing(true)
    }
})

async function updateSharing(value: boolean): Promise<void> {
    sharing.value = true
    shareError.value = false
    try {
        if (value) {
            await recaps.share(design.value)
        } else if (recaps.currentShare?.id) {
            await recaps.stopSharing(recaps.currentShare.id)
        }
    } catch {
        shareError.value = true
    } finally {
        sharing.value = false
    }
}

const shareToggle = computed({
    get: () => shared.value,
    set: (value: boolean) => void updateSharing(value),
})

function onBusy(busy: boolean): void {
    if (busy) {
        story.value?.pause()
    }
}

function load(): void {
    void recaps.load()
    if (recaps.sharesStatus === 'idle' || recaps.sharesStatus === 'error') {
        void recaps.loadOwnShares()
    }
}

watch(
    () => recaps.range,
    () => void recaps.load(),
    { deep: true },
)

onMounted(load)
onActivated(load)
</script>

<template>
    <div
        data-part="view"
        data-view="recap"
        :data-state="recaps.status"
        :aria-busy="recaps.status === 'loading' || undefined"
    >
        <RecapNav />
        <RecapPeriodPicker
            v-model="period"
            v-model:custom="custom"
            :invalid="recaps.status === 'invalid'"
        />
        <div data-part="recap-layout">
            <div data-part="recap-stage">
                <RecapStory
                    v-if="slides.length > 0"
                    ref="story"
                    v-model:index="index"
                    :slides="slides"
                    :design="design"
                    :label="t('recap.storyLabel', { period: periodLabel })"
                />
                <div
                    v-else-if="recaps.status === 'error'"
                    data-part="empty-state"
                    data-item="recap-error"
                >
                    <p data-part="empty-state-title">{{ t('recap.loadError') }}</p>
                    <BaseButton
                        data-item="retry"
                        :label="t('library.retry')"
                        @click="recaps.load(true)"
                    />
                </div>
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
                <section
                    data-part="section"
                    data-item="recap-design"
                    :aria-labelledby="designTitleId"
                >
                    <h2 :id="designTitleId" data-part="section-title">{{ t('recap.design') }}</h2>
                    <BaseSegmentedField
                        v-model="design"
                        item="recap-design"
                        :label="t('recap.designLabel')"
                        :options="designOptions"
                    />
                </section>
                <section
                    data-part="section"
                    data-item="recap-share"
                    :aria-labelledby="shareTitleId"
                >
                    <h2 :id="shareTitleId" data-part="section-title">{{ t('recap.share') }}</h2>
                    <RecapExport
                        :slides="slides"
                        :index="index"
                        :design="design"
                        :story="storyElement"
                        :file-stem="periodItem(recaps.period)"
                        @busy="onBusy"
                    />
                    <BaseCheckbox
                        v-model="shareToggle"
                        data-field="share-with-profiles"
                        :label="t('recap.shareWithProfiles')"
                        :hint="t('recap.shareWithProfilesHint')"
                        :disabled="sharing || !recaps.range || recaps.sharesStatus !== 'ready'"
                    />
                    <BaseAlert v-if="shareError" tone="error" :message="t('recap.shareFailed')" />
                </section>
            </div>
        </div>
    </div>
</template>
