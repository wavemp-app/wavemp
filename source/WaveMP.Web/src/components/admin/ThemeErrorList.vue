<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import {
    themeErrorLocationKey,
    themeErrorMessageKey,
    themeErrorParameters,
    type ThemePackageError,
} from '@/admin/themeErrors'
import { useFormat } from '@/composables/useFormat'

/**
 * Why the server rejected a theme package: one entry per problem with its place (file and line)
 * and its message.
 */
const props = defineProps<{
    errors: readonly ThemePackageError[]
}>()

const { t } = useI18n()
const format = useFormat()

const rows = computed(() =>
    props.errors.map((error, index) => {
        const parameters = themeErrorParameters(error, format.fileSize, (value) =>
            format.number(value),
        )
        const locationKey = themeErrorLocationKey(error)
        return {
            key: `${index}-${error.code ?? 'unknown'}`,
            code: error.code ?? 'unknown',
            location: locationKey ? t(locationKey, parameters) : null,
            message: t(themeErrorMessageKey(error), parameters),
        }
    }),
)
</script>

<template>
    <ul data-part="theme-errors" :aria-label="t('adminThemes.errorList')">
        <li v-for="row in rows" :key="row.key" data-part="theme-error" :data-item="row.code">
            <span v-if="row.location" data-part="theme-error-location">{{ row.location }}</span>
            <span data-part="theme-error-message">{{ row.message }}</span>
        </li>
    </ul>
</template>
