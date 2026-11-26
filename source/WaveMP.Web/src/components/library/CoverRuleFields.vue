<script setup lang="ts">
import { computed, onBeforeUnmount, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type {
    AlbumCoverFallbackOption,
    AlbumCoverFallbackRule,
    CoverRule,
    CoverRuleOption,
} from '@/api/generated/models'
import { statusOf } from '@/api/problems'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseCover from '@/components/base/BaseCover.vue'
import type { SegmentOption } from '@/components/base/BaseSegmentedControl.vue'
import BaseSegmentedField from '@/components/base/BaseSegmentedField.vue'
import {
    fetchCoverRulePreview,
    getAlbumCoverFallback,
    getCoverRule,
    setAlbumCoverFallback,
    setCoverRule,
    type CoverRuleOwner,
} from '@/library/api'
import {
    albumCoverFallbackKey,
    albumCoverFallbackOptionOf,
    albumCoverFallbackOptions,
    coverPriorityKey,
    coverRuleOptionOf,
    coverRuleOptions,
    coverRuleToken,
} from '@/library/coverRules'
import { defaultCoverSize } from '@/library/covers'
import { useCoversStore } from '@/stores/covers'

/**
 * The exceptions of an album or a song from the cover rules of the library settings, for admins
 * in the editor of the song or album. A song chooses its own rule (own cover first, album cover
 * first, only its own cover) or inherits the rule of its album; the preview shows the cover the
 * chosen rule gives before it is saved. An album chooses what it shows without a cover of its own
 * (the picture of its artist or nothing) and the rule of its songs. The fields have no button of
 * their own: the editor around them calls save, which sends only what changed and then loads the
 * covers of albums and songs again.
 */
const props = withDefaults(
    defineProps<{
        owner: CoverRuleOwner
        id: string
        /** Loads the rules while true (e.g. while the dialog is open). */
        active?: boolean
    }>(),
    { active: true },
)

const { t } = useI18n()
const covers = useCoversStore()
const titleId = useId()

const state = ref<'loading' | 'ready' | 'error'>('loading')
const saving = ref(false)
const failed = ref(false)
const rule = ref<CoverRule | null>(null)
const fallback = ref<AlbumCoverFallbackRule | null>(null)
const ruleDraft = ref<string>('Inherit')
const fallbackDraft = ref<string>('Inherit')
const previewSrc = ref<string | null>(null)
const previewState = ref<'loading' | 'ready' | 'empty'>('empty')
const previews = new Map<CoverRuleOption, string | null>()
let generation = 0
let previewGeneration = 0

const ruleChanged = computed(
    () => state.value === 'ready' && ruleDraft.value !== coverRuleOptionOf(rule.value?.rule),
)
const fallbackChanged = computed(
    () =>
        state.value === 'ready' &&
        props.owner === 'album' &&
        fallbackDraft.value !== albumCoverFallbackOptionOf(fallback.value?.fallback),
)
const dirty = computed(() => ruleChanged.value || fallbackChanged.value)
/** The rules the album or song inherits, named in the hints of the fields. */
const inheritedRule = computed(() => t(coverPriorityKey(rule.value?.inheritedPriority)))
const inheritedFallback = computed(() =>
    t(albumCoverFallbackKey(fallback.value?.inheritedFallback)),
)

const ruleOptions = computed<SegmentOption[]>(() =>
    coverRuleOptions.map((option) => ({
        value: option,
        item: coverRuleToken(option),
        label: option === 'Inherit' ? t('coverRule.inherit') : t(coverPriorityKey(option)),
    })),
)
const fallbackOptions = computed<SegmentOption[]>(() =>
    albumCoverFallbackOptions.map((option) => ({
        value: option,
        item: coverRuleToken(option),
        label: option === 'Inherit' ? t('coverRule.inherit') : t(albumCoverFallbackKey(option)),
    })),
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
        const buffer = await fetchCoverRulePreview('track', props.id, option, defaultCoverSize)
        return buffer && buffer.byteLength > 0 ? URL.createObjectURL(new Blob([buffer])) : null
    } catch (error) {
        if (statusOf(error) === 404) {
            return null
        }

        throw error
    }
}

/** Shows the cover of the chosen rule of a song; every rule is loaded once while it is open. */
async function showPreview(option: CoverRuleOption): Promise<void> {
    if (props.owner !== 'track') {
        return
    }

    const current = ++previewGeneration
    if (previews.has(option)) {
        previewSrc.value = previews.get(option) ?? null
        previewState.value = previewSrc.value ? 'ready' : 'empty'
        return
    }

    previewState.value = 'loading'
    const loadGeneration = generation
    try {
        const url = await loadPreview(option)
        if (loadGeneration !== generation) {
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
    state.value = 'loading'
    failed.value = false
    try {
        const [loadedRule, loadedFallback] = await Promise.all([
            getCoverRule(props.owner, props.id),
            props.owner === 'album' ? getAlbumCoverFallback(props.id) : Promise.resolve(undefined),
        ])
        if (current !== generation) {
            return
        }

        rule.value = loadedRule ?? null
        fallback.value = loadedFallback ?? null
        ruleDraft.value = coverRuleOptionOf(loadedRule?.rule)
        fallbackDraft.value = albumCoverFallbackOptionOf(loadedFallback?.fallback)
        state.value = 'ready'
        void showPreview(coverRuleOptionOf(loadedRule?.rule))
    } catch {
        if (current === generation) {
            state.value = 'error'
        }
    }
}

/**
 * Saves the changed rules. Returns whether the editor may close: true after saving and when
 * nothing changed, false if saving failed (the fields show the problem).
 */
async function save(): Promise<boolean> {
    if (!dirty.value) {
        return state.value !== 'loading'
    }

    if (saving.value) {
        return false
    }

    saving.value = true
    failed.value = false
    try {
        if (fallbackChanged.value) {
            const result = await setAlbumCoverFallback(
                props.id,
                albumCoverFallbackOptionOf(fallbackDraft.value as AlbumCoverFallbackOption),
            )
            fallback.value = result ?? fallback.value
        }

        if (ruleChanged.value) {
            const result = await setCoverRule(
                props.owner,
                props.id,
                coverRuleOptionOf(ruleDraft.value as CoverRuleOption),
            )
            rule.value = result ?? rule.value
        }

        // The album or song may show another picture now, and so may the songs of an album.
        covers.invalidate()
        return true
    } catch {
        failed.value = true
        return false
    } finally {
        saving.value = false
    }
}

watch(ruleDraft, (option) => void showPreview(coverRuleOptionOf(option as CoverRuleOption)))

watch(
    [() => props.active, () => props.owner, () => props.id],
    ([active]) => {
        if (active) {
            void load()
        } else {
            generation++
        }
    },
    { immediate: true },
)

onBeforeUnmount(() => {
    generation++
    clearPreviews()
})

defineExpose({ state, saving, dirty, save, load })
</script>

<template>
    <section
        data-part="section"
        data-item="cover-rule"
        :aria-labelledby="titleId"
        :aria-busy="state === 'loading' ? 'true' : undefined"
    >
        <h3 :id="titleId" data-part="section-title">{{ t('coverRule.title') }}</h3>
        <BaseAlert v-if="state === 'error'" tone="error" :message="t('coverRule.loadError')" />
        <template v-else>
            <figure
                v-if="owner === 'track'"
                data-part="cover-preview"
                :data-state="previewState"
                :aria-busy="previewState === 'loading' ? 'true' : undefined"
            >
                <BaseCover :src="previewSrc" />
                <figcaption data-part="label">
                    {{
                        t(previewState === 'empty' ? 'coverRule.previewNone' : 'coverRule.preview')
                    }}
                </figcaption>
            </figure>
            <BaseSegmentedField
                v-if="owner === 'album'"
                v-model="fallbackDraft"
                data-field="cover-fallback"
                item="cover-fallback"
                :label="t('coverRule.albumLabel')"
                :options="fallbackOptions"
                :hint="t('coverRule.albumHint', { rule: inheritedFallback })"
                :disabled="state !== 'ready' || saving"
            />
            <BaseSegmentedField
                v-model="ruleDraft"
                data-field="cover-rule"
                item="cover-rule"
                :label="t(owner === 'album' ? 'coverRule.songsLabel' : 'coverRule.label')"
                :options="ruleOptions"
                :hint="
                    t(owner === 'album' ? 'coverRule.songsHint' : 'coverRule.trackHint', {
                        rule: inheritedRule,
                    })
                "
                :disabled="state !== 'ready' || saving"
            />
            <BaseAlert v-if="failed" tone="error" :message="t('coverRule.saveError')" />
        </template>
    </section>
</template>
