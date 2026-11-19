<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { SmartPlaylistField, SmartPlaylistOperator } from '@/api/generated/models'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseSelect, { type SelectOption } from '@/components/base/BaseSelect.vue'
import BaseTextField from '@/components/base/BaseTextField.vue'
import {
    kindOf,
    messageKey,
    operatorsOf,
    smartFields,
    valuesOf,
    withField,
    type RuleDraft,
} from '@/playlists/smartRules'

/** Messages of the parts of a rule that are wrong. */
export interface RuleErrors {
    readonly field?: string
    readonly operator?: string
    readonly text?: string
    readonly number?: string
    readonly numberTo?: string
}

/**
 * One rule of the editor of a smart playlist: field, condition and the values the condition needs
 * (none, one or a range). data-kind is the kind of the field (text, number, duration, days, flag)
 * and data-values the values shown (none, text, number, range), so themes can lay out the row.
 */
const props = withDefaults(
    defineProps<{
        rule: RuleDraft
        /** Position of the rule, from 0. */
        index: number
        errors?: RuleErrors
        disabled?: boolean
    }>(),
    { errors: () => ({}), disabled: false },
)

const emit = defineEmits<{
    'update:rule': [rule: RuleDraft]
    remove: []
}>()

const { t } = useI18n()

const kind = computed(() => kindOf(props.rule.field))
const values = computed(() => valuesOf(props.rule))
const invalid = computed(() => Object.values(props.errors).some(Boolean))

const fieldOptions = computed<SelectOption[]>(() =>
    smartFields.map((field) => ({
        value: field,
        label: t(`smartPlaylists.fields.${messageKey(field)}`),
    })),
)

const operatorOptions = computed<SelectOption[]>(() =>
    operatorsOf(props.rule.field).map((operator) => ({
        value: operator,
        label:
            kind.value === 'flag'
                ? t(`smartPlaylists.flags.${messageKey(props.rule.field)}.${messageKey(operator)}`)
                : t(`smartPlaylists.operators.${messageKey(operator)}`),
    })),
)

const valueLabel = computed(() => {
    switch (kind.value) {
        case 'days':
            return t('smartPlaylists.days')
        case 'duration':
            return values.value === 'range'
                ? t('smartPlaylists.from')
                : t('smartPlaylists.duration')
        default:
            return values.value === 'range' ? t('smartPlaylists.from') : t('smartPlaylists.value')
    }
})

const hint = computed(() => {
    if (kind.value === 'duration') {
        return t('smartPlaylists.durationHint')
    }

    return props.rule.field === 'Rating' ? t('smartPlaylists.ratingHint') : undefined
})

const inputmode = computed(() => (kind.value === 'text' ? undefined : ('numeric' as const)))

function update(change: Partial<Omit<RuleDraft, 'key'>>): void {
    emit('update:rule', { ...props.rule, ...change })
}

const field = computed({
    get: () => props.rule.field,
    set: (value: string) => emit('update:rule', withField(props.rule, value as SmartPlaylistField)),
})

const operator = computed({
    get: () => props.rule.operator,
    set: (value: string) => update({ operator: value as SmartPlaylistOperator }),
})

const text = computed({
    get: () => props.rule.text,
    set: (value: string) => update({ text: value }),
})

const number = computed({
    get: () => props.rule.number,
    set: (value: string) => update({ number: value }),
})

const numberTo = computed({
    get: () => props.rule.numberTo,
    set: (value: string) => update({ numberTo: value }),
})
</script>

<template>
    <li
        data-part="smart-rule"
        :data-kind="kind"
        :data-values="values"
        :data-state="invalid ? 'invalid' : undefined"
        :aria-label="t('smartPlaylists.ruleLabel', { number: index + 1 })"
    >
        <BaseSelect
            v-model="field"
            data-field="rule-field"
            :label="t('smartPlaylists.field')"
            :options="fieldOptions"
            :error="errors.field"
            :disabled="disabled"
        />
        <BaseSelect
            v-model="operator"
            data-field="rule-operator"
            :label="t('smartPlaylists.condition')"
            :options="operatorOptions"
            :error="errors.operator"
            :disabled="disabled"
        />
        <div data-part="smart-rule-values" :data-values="values">
            <BaseTextField
                v-if="values === 'text'"
                v-model="text"
                data-field="rule-text"
                :label="t('smartPlaylists.value')"
                :error="errors.text"
                :disabled="disabled"
                autocomplete="off"
            />
            <template v-else-if="values !== 'none'">
                <BaseTextField
                    v-model="number"
                    data-field="rule-number"
                    :label="valueLabel"
                    :hint="hint"
                    :error="errors.number"
                    :inputmode="inputmode"
                    :disabled="disabled"
                    autocomplete="off"
                />
                <BaseTextField
                    v-if="values === 'range'"
                    v-model="numberTo"
                    data-field="rule-number-to"
                    :label="t('smartPlaylists.to')"
                    :error="errors.numberTo"
                    :inputmode="inputmode"
                    :disabled="disabled"
                    autocomplete="off"
                />
            </template>
        </div>
        <BaseButton
            data-item="remove-rule"
            variant="icon"
            icon="remove-rule"
            :label="t('smartPlaylists.removeRule', { number: index + 1 })"
            :disabled="disabled"
            @click="emit('remove')"
        />
    </li>
</template>
