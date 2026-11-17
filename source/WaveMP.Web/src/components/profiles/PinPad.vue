<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseIcon from '@/components/base/BaseIcon.vue'
import {
    appendDigit,
    isCompletePin,
    pinMaxLength,
    pinPadKeys,
    removeLastDigit,
    sanitizePin,
    type PinPadKey,
} from '@/profiles/pin'

/**
 * Digit pad for a PIN: a password field that also takes digits from a keyboard, the entered
 * digits as dots and the keys 0 to 9, delete and submit. The submit key is the submit button of
 * the surrounding form.
 */
const props = withDefaults(
    defineProps<{
        /** Accessible name of the field. */
        label: string
        disabled?: boolean
        /** Focuses the field when shown, so a keyboard can type at once. */
        autofocus?: boolean
    }>(),
    { disabled: false, autofocus: false },
)

const pin = defineModel<string>({ default: '' })

const { t } = useI18n()
const field = ref<HTMLInputElement | null>(null)

function keyLabel(key: PinPadKey): string {
    switch (key) {
        case 'delete':
            return t('pin.delete')
        case 'submit':
            return t('pin.submit')
        default:
            return key
    }
}

function press(key: PinPadKey): void {
    if (key === 'delete') {
        pin.value = removeLastDigit(pin.value)
    } else if (key !== 'submit') {
        pin.value = appendDigit(pin.value, key)
    }
}

/** Digits and Backspace also work while a key of the pad has the focus. */
function onKeydown(event: KeyboardEvent): void {
    if (
        event.target === field.value ||
        props.disabled ||
        event.ctrlKey ||
        event.metaKey ||
        event.altKey
    ) {
        return
    }

    if (/^[0-9]$/.test(event.key)) {
        event.preventDefault()
        press(event.key as PinPadKey)
    } else if (event.key === 'Backspace') {
        event.preventDefault()
        press('delete')
    }
}

function onInput(event: Event): void {
    const input = event.target as HTMLInputElement
    const value = sanitizePin(input.value)
    input.value = value
    pin.value = value
}

onMounted(() => {
    if (props.autofocus) {
        field.value?.focus({ preventScroll: true })
    }
})
</script>

<template>
    <div data-part="pin-pad" :data-state="disabled ? 'disabled' : undefined" @keydown="onKeydown">
        <!-- inputmode none: the digit pad replaces the on-screen keyboard; keyboards still type. -->
        <input
            ref="field"
            data-part="pin-field"
            type="password"
            inputmode="none"
            autocomplete="off"
            :maxlength="pinMaxLength"
            :value="pin"
            :aria-label="label"
            :disabled="disabled"
            @input="onInput"
        />
        <span data-part="pin-dots" aria-hidden="true" :data-count="pin.length">
            <span v-for="index in pin.length" :key="index" data-part="pin-dot"></span>
        </span>
        <div data-part="pin-keys" role="group" :aria-label="t('pin.keypad')">
            <button
                v-for="key in pinPadKeys"
                :key="key"
                :type="key === 'submit' ? 'submit' : 'button'"
                data-part="pin-key"
                :data-key="key"
                :title="key === 'delete' || key === 'submit' ? keyLabel(key) : undefined"
                :disabled="
                    disabled ||
                    (key === 'submit' && !isCompletePin(pin)) ||
                    (key === 'delete' && pin.length === 0)
                "
                @click="press(key)"
            >
                <BaseIcon v-if="key === 'delete'" name="pin-delete" />
                <BaseIcon v-else-if="key === 'submit'" name="pin-submit" />
                <span data-part="label">{{ keyLabel(key) }}</span>
            </button>
        </div>
    </div>
</template>
