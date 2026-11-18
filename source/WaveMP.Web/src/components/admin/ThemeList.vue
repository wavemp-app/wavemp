<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Theme } from '@/api/generated/models'
import BaseButton from '@/components/base/BaseButton.vue'
import { useFormat } from '@/composables/useFormat'
import { toThemeDefinition } from '@/themes/custom'
import { hasVariants, themePreview } from '@/themes/themes'

/**
 * The uploaded themes with their preview, author, version, size and installation date; each with
 * a button to upload a new version and one to delete it.
 */
const props = withDefaults(
    defineProps<{
        themes: readonly Theme[]
        /** Id of the theme that is being replaced or deleted. */
        busy?: string | null
        disabled?: boolean
    }>(),
    { busy: null, disabled: false },
)

const emit = defineEmits<{
    replace: [theme: Theme]
    delete: [theme: Theme]
}>()

const { t } = useI18n()
const format = useFormat()
/** Previews that could not be loaded; their items show no picture. */
const missing = ref<ReadonlySet<string>>(new Set())

const rows = computed(() =>
    props.themes.flatMap((theme) => {
        const definition = toThemeDefinition(theme)
        if (!definition) {
            return []
        }

        const variant = definition.variants[0]
        return [
            {
                theme,
                definition,
                variant,
                preview: themePreview(definition, variant),
                variants: definition.variants.join(' '),
                variantsLabel: hasVariants(definition)
                    ? t('settings.themeVariants.both')
                    : t(`settings.themeVariants.${variant}`),
                installedAt: theme.installedAt
                    ? format.date(theme.installedAt, { dateStyle: 'medium' })
                    : '',
            },
        ]
    }),
)

function previewFailed(preview: string): void {
    missing.value = new Set([...missing.value, preview])
}
</script>

<template>
    <div v-if="rows.length === 0" data-part="empty-state">
        <p data-part="empty-state-text">{{ t('adminThemes.empty') }}</p>
    </div>
    <ul v-else data-part="theme-list" :aria-label="t('adminThemes.list')">
        <li
            v-for="row in rows"
            :key="row.definition.id"
            data-part="theme-item"
            :data-item="row.definition.id"
            :data-variants="row.variants"
            :data-state="busy === row.definition.id ? 'busy' : undefined"
        >
            <span
                data-part="theme-preview"
                :data-kind="row.variant"
                :data-state="missing.has(row.preview) || !row.preview ? 'missing' : undefined"
            >
                <img
                    v-if="row.preview && !missing.has(row.preview)"
                    data-part="theme-preview-image"
                    :src="row.preview"
                    alt=""
                    draggable="false"
                    loading="lazy"
                    @error="previewFailed(row.preview)"
                />
            </span>
            <div data-part="theme-info">
                <span data-part="theme-name">{{ row.definition.name }}</span>
                <span data-part="theme-meta">
                    <span data-part="label">{{
                        t('adminThemes.by', { author: row.theme.author ?? '' })
                    }}</span>
                    <span data-part="label">{{
                        t('adminThemes.version', { version: row.theme.version ?? '' })
                    }}</span>
                    <span data-part="theme-variants">{{ row.variantsLabel }}</span>
                    <span data-part="label">{{ format.fileSize(row.theme.sizeBytes ?? 0) }}</span>
                    <span v-if="row.installedAt" data-part="label">{{
                        t('adminThemes.installedAt', { date: row.installedAt })
                    }}</span>
                </span>
            </div>
            <div data-part="theme-actions">
                <BaseButton
                    variant="icon"
                    icon="upload"
                    data-item="replace"
                    :label="t('adminThemes.replace', { name: row.definition.name })"
                    :disabled="disabled || busy !== null"
                    @click="emit('replace', row.theme)"
                />
                <BaseButton
                    variant="icon"
                    icon="delete"
                    data-item="delete"
                    :label="t('adminThemes.delete', { name: row.definition.name })"
                    :disabled="disabled || busy !== null"
                    @click="emit('delete', row.theme)"
                />
            </div>
        </li>
    </ul>
</template>
