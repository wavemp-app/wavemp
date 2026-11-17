<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { CoverRule, CoverRuleOption } from '@/api/generated/models'
import { statusOf } from '@/api/problems'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseCover from '@/components/base/BaseCover.vue'
import type { SegmentOption } from '@/components/base/BaseSegmentedControl.vue'
import BaseSegmentedField from '@/components/base/BaseSegmentedField.vue'
import {
    fetchCoverRulePreview,
    getCoverRule,
    setCoverRule,
    type CoverRuleOwner,
} from '@/library/api'
import {
    coverPriorityKey,
    coverRuleOptionKey,
    coverRuleOptionOf,
    coverRuleOptions,
    coverRuleToken,
} from '@/library/coverRules'
import { defaultCoverSize } from '@/library/covers'
import { useCoversStore } from '@/stores/covers'

/**
 * The cover rule of an album or song, for admins: the cover of the album or of the song first, or
 * the rule of the next level (the album of a song, the library settings). The preview shows the
 * cover the chosen rule would show before it is saved; saving loads the covers of albums and
 * songs again, so every page shows the new one.
 */
const props = defineProps<{
    owner: CoverRuleOwner
    id: string
    /** Whether the album or song has any cover (hasCover); otherwise no preview is loaded. */
    available: boolean
}>()

const emit = defineEmits<{
    saved: [rule: CoverRule]
}>()

const { t } = useI18n()
const covers = useCoversStore()

const rule = ref<CoverRule | null>(null)
const draft = ref<string>('Inherit')
const status = ref<'loading' | 'ready' | 'error'>('loading')
const saving = ref(false)
const saved = ref(false)
const failed = ref(false)
const previewSrc = ref<string | null>(null)
const previewState = ref<'loading' | 'ready' | 'empty'>('empty')
const previews = new Map<CoverRuleOption, string | null>()
let generation = 0
let previewGeneration = 0

const options = computed<SegmentOption[]>(() =>
    coverRuleOptions.map((option) => ({
        value: option,
        item: coverRuleToken(option),
        label: t(coverRuleOptionKey(option), {
            priority: t(coverPriorityKey(rule.value?.inheritedPriority)),
        }),
    })),
)
const changed = computed(() => draft.value !== coverRuleOptionOf(rule.value?.rule))
const hint = computed(() =>
    t(props.owner === 'album' ? 'coverRule.albumHint' : 'coverRule.trackHint'),
)

function clearPreviews(): void {
    for (const url of previews.values()) {
        if (url) {
            URL.revokeObjectURL(url)
        }
    }

    previews.clear()
    previewSrc.value = null
}

async function loadPreview(option: CoverRuleOption): Promise<string | null> {
    try {
        const buffer = await fetchCoverRulePreview(props.owner, props.id, option, defaultCoverSize)
        return buffer && buffer.byteLength > 0 ? URL.createObjectURL(new Blob([buffer])) : null
    } catch (error) {
        if (statusOf(error) === 404) {
            return null
        }

        throw error
    }
}

/** Shows the cover of the chosen rule; every rule is loaded once while the form is open. */
async function showPreview(option: CoverRuleOption): Promise<void> {
    const current = ++previewGeneration
    if (!props.available) {
        previewSrc.value = null
        previewState.value = 'empty'
        return
    }

    if (previews.has(option)) {
        previewSrc.value = previews.get(option) ?? null
        previewState.value = previewSrc.value ? 'ready' : 'empty'
        return
    }

    previewState.value = 'loading'
    const formGeneration = generation
    try {
        const url = await loadPreview(option)
        if (formGeneration !== generation) {
            if (url) {
                URL.revokeObjectURL(url)
            }

            return
        }

        previews.set(option, url)
        if (current === previewGeneration) {
            previewSrc.value = url
            previewState.value = url ? 'ready' : 'empty'
        }
    } catch {
        if (current === previewGeneration) {
            previewSrc.value = null
            previewState.value = 'empty'
        }
    }
}

async function load(): Promise<void> {
    const current = ++generation
    clearPreviews()
    status.value = 'loading'
    saved.value = false
    failed.value = false
    try {
        const loaded = (await getCoverRule(props.owner, props.id)) ?? null
        if (current !== generation) {
            return
        }

        rule.value = loaded
        draft.value = coverRuleOptionOf(loaded?.rule)
        status.value = 'ready'
        void showPreview(coverRuleOptionOf(loaded?.rule))
    } catch {
        if (current === generation) {
            status.value = 'error'
        }
    }
}

async function save(): Promise<void> {
    if (saving.value || status.value !== 'ready') {
        return
    }

    saving.value = true
    saved.value = false
    failed.value = false
    try {
        const result = await setCoverRule(
            props.owner,
            props.id,
            coverRuleOptionOf(draft.value as CoverRuleOption),
        )
        if (result) {
            rule.value = result
            draft.value = coverRuleOptionOf(result.rule)
            emit('saved', result)
        }

        // The same album or song may show another picture now, and so may the songs of an album.
        covers.invalidate()
        saved.value = true
    } catch (error) {
        if (statusOf(error) === 404) {
            status.value = 'error'
        } else {
            failed.value = true
        }
    } finally {
        saving.value = false
    }
}

watch(draft, (option) => {
    saved.value = false
    void showPreview(coverRuleOptionOf(option as CoverRuleOption))
})

watch(
    () => [props.owner, props.id] as const,
    () => void load(),
    { immediate: true },
)

watch(
    () => props.available,
    () => {
        if (status.value === 'ready') {
            void showPreview(coverRuleOptionOf(draft.value as CoverRuleOption))
        }
    },
)

onBeforeUnmount(() => {
    generation++
    clearPreviews()
})
</script>

<template>
    <BaseAlert v-if="status === 'error'" tone="error" :message="t('coverRule.loadError')" />
    <form
        v-else
        data-part="form"
        :aria-busy="status === 'loading' ? 'true' : undefined"
        @submit.prevent="save"
    >
        <figure
            data-part="cover-preview"
            :data-state="previewState"
            :aria-busy="previewState === 'loading' ? 'true' : undefined"
        >
            <BaseCover :src="previewSrc" />
            <figcaption data-part="label">
                {{ t(previewState === 'empty' ? 'coverRule.previewNone' : 'coverRule.preview') }}
            </figcaption>
        </figure>
        <BaseSegmentedField
            v-model="draft"
            data-field="cover-rule"
            item="cover-rule"
            :label="t('coverRule.label')"
            :options="options"
            :hint="hint"
            :disabled="status !== 'ready' || saving"
        />
        <BaseAlert v-if="saved" tone="success" :message="t('coverRule.saved')" />
        <BaseAlert v-if="failed" tone="error" :message="t('coverRule.saveError')" />
        <div data-part="view-actions">
            <BaseButton
                type="submit"
                variant="primary"
                data-item="save-cover-rule"
                :label="t('coverRule.save')"
                :disabled="status !== 'ready' || saving || !changed"
            />
        </div>
    </form>
</template>
