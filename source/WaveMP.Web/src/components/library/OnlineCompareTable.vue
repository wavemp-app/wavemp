<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import BaseCheckbox from '@/components/base/BaseCheckbox.vue'
import BaseCover from '@/components/base/BaseCover.vue'
import BaseProgress from '@/components/base/BaseProgress.vue'
import {
    isApplicable,
    onlineDataFields,
    type OnlineCompareRow,
    type OnlineCoverState,
    type OnlineField,
    type OnlineOwner,
    type OnlineValue,
} from '@/library/onlineMetadata'
import LibraryCover from './LibraryCover.vue'

/**
 * The current and the suggested values of a match side by side, one row per field with the
 * checkbox that chooses it, and a row for the cover: the current cover next to the preview of the
 * release. Only fields with a value other than the current one can be chosen.
 */
const props = defineProps<{
    owner: OnlineOwner
    /** Id of the song or album, for its current cover. */
    id: string | null
    hasCover: boolean
    rows: readonly OnlineCompareRow[]
    chosen: ReadonlySet<OnlineField>
    /** Whether the tags can be written; otherwise only the cover can be chosen. */
    tagsWritable: boolean
    /** Whether the match names a release, which may have a cover. */
    showCover: boolean
    coverState: OnlineCoverState
    coverUrl: string | null
    disabled?: boolean
}>()

const useCover = defineModel<boolean>('useCover', { default: false })

const emit = defineEmits<{
    toggle: [field: OnlineField, chosen: boolean]
}>()

const { t } = useI18n()

function label(field: OnlineField): string {
    return t(`trackMetadata.fields.${field}`)
}

function display(value: OnlineValue | undefined): string {
    if (value === null || value === undefined || value === '') {
        return t('onlineMetadata.empty')
    }
    // Years and numbers of songs and discs are shown as entered, without grouping.
    return String(value)
}

function suggestion(row: OnlineCompareRow): string {
    if (row.difference === 'same') {
        return t('onlineMetadata.same')
    }
    if (row.difference === 'unknown') {
        return t('onlineMetadata.unknown')
    }
    return display(row.suggested)
}

function isChosen(row: OnlineCompareRow): boolean {
    return props.chosen.has(row.field) && isApplicable(row.difference)
}
</script>

<template>
    <table data-part="online-compare">
        <thead>
            <tr data-part="online-compare-head">
                <th scope="col">{{ t('onlineMetadata.field') }}</th>
                <th scope="col">{{ t('onlineMetadata.current') }}</th>
                <th scope="col">{{ t('onlineMetadata.suggested') }}</th>
            </tr>
        </thead>
        <tbody>
            <tr
                v-for="row in rows"
                :key="row.field"
                data-part="online-compare-row"
                :data-field="onlineDataFields[row.field]"
                :data-state="row.difference"
                :data-active="isChosen(row) ? 'true' : undefined"
            >
                <th scope="row" data-part="online-compare-field">
                    <BaseCheckbox
                        :model-value="isChosen(row)"
                        :label="label(row.field)"
                        :disabled="disabled || !tagsWritable || !isApplicable(row.difference)"
                        @update:model-value="(value) => emit('toggle', row.field, value)"
                    />
                </th>
                <td data-part="online-compare-current">
                    <span data-part="label">{{ display(row.current) }}</span>
                </td>
                <td data-part="online-compare-suggested">
                    <span data-part="label">{{ suggestion(row) }}</span>
                </td>
            </tr>
            <tr
                v-if="showCover"
                data-part="online-compare-row"
                data-field="cover"
                :data-state="coverState"
                :data-active="useCover && coverState === 'ready' ? 'true' : undefined"
            >
                <th scope="row" data-part="online-compare-field">
                    <BaseCheckbox
                        v-model="useCover"
                        :label="t('onlineMetadata.useCover')"
                        :disabled="disabled || coverState !== 'ready'"
                    />
                </th>
                <td data-part="online-compare-current">
                    <LibraryCover :kind="owner" :id="id" :available="hasCover" />
                </td>
                <td data-part="online-compare-suggested">
                    <BaseCover v-if="coverState === 'ready'" :src="coverUrl" />
                    <BaseProgress
                        v-else-if="coverState === 'loading'"
                        :label="t('onlineMetadata.coverLoading')"
                    />
                    <span v-else data-part="label">
                        {{
                            coverState === 'missing'
                                ? t('onlineMetadata.coverMissing')
                                : t('onlineMetadata.coverUnavailable')
                        }}
                    </span>
                </td>
            </tr>
        </tbody>
    </table>
</template>
