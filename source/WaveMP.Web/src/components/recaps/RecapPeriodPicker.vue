<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { RecapPeriodObject, type RecapPeriod } from '@/api/generated/models'
import BaseSegmentedControl from '@/components/base/BaseSegmentedControl.vue'
import BaseTextField from '@/components/base/BaseTextField.vue'
import { recapPeriods, type CustomRange } from '@/recaps/periods'
import { periodItem, periodLabelKey } from '@/recaps/labels'

/**
 * Choice of the period of the recap and the statistics: a segmented control with the periods and,
 * for a custom period, two date fields. v-model is the period, v-model:custom the custom days.
 */
const props = withDefaults(defineProps<{ invalid?: boolean }>(), { invalid: false })

const period = defineModel<RecapPeriod>({ required: true })
const custom = defineModel<CustomRange>('custom', { required: true })

const { t } = useI18n()

const options = computed(() =>
    recapPeriods.map((value) => ({
        value,
        item: periodItem(value),
        label: t(periodLabelKey(value)),
    })),
)

const selected = computed({
    get: () => period.value as string,
    set: (value: string) => (period.value = value as RecapPeriod),
})

const from = computed({
    get: () => custom.value.from,
    set: (value: string) => (custom.value = { ...custom.value, from: value }),
})

const to = computed({
    get: () => custom.value.to,
    set: (value: string) => (custom.value = { ...custom.value, to: value }),
})

const error = computed(() => (props.invalid ? t('recap.customInvalid') : undefined))
</script>

<template>
    <div data-part="recap-period">
        <BaseSegmentedControl
            v-model="selected"
            item="recap-period"
            :options="options"
            :label="t('recap.period')"
        />
        <div
            v-if="period === RecapPeriodObject.Custom"
            data-part="field-row"
            data-item="custom-period"
        >
            <BaseTextField
                v-model="from"
                data-field="from"
                type="date"
                :label="t('recap.from')"
                :error="error"
            />
            <BaseTextField v-model="to" data-field="to" type="date" :label="t('recap.to')" />
        </div>
    </div>
</template>
