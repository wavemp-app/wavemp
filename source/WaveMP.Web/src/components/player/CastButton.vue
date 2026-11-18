<script setup lang="ts">
import { computed, ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseButton from '@/components/base/BaseButton.vue'
import { castStateTokens, type TransportCast } from '@/components/shell/transport'
import { castButtonText } from '@/casting/presentation'

/**
 * The cast button of the transport bar. It opens the device selection of the browser or the
 * operating system, which also stops casting. Hidden in browsers that cannot cast; without HTTPS,
 * Chromecast is greyed out and the button shows a hint instead. Controlled through props and events
 * only.
 */
const props = defineProps<{
    cast?: TransportCast
}>()

const emit = defineEmits<{
    cast: []
}>()

const { t } = useI18n()
const hintId = useId()
const hintOpen = ref(false)

const visible = computed(
    () => props.cast !== undefined && props.cast.availability !== 'unsupported',
)
const insecure = computed(() => props.cast?.availability === 'insecure')
const tokens = computed(() =>
    [castStateTokens(props.cast), ...(insecure.value && hintOpen.value ? ['hint'] : [])].join(' '),
)
const label = computed(() => {
    const text = castButtonText(props.cast)
    return t(text.key, { ...text.params })
})

function onClick(): void {
    if (insecure.value) {
        hintOpen.value = !hintOpen.value
        return
    }

    emit('cast')
}
</script>

<template>
    <div v-if="visible" data-part="cast" :data-state="tokens">
        <BaseButton
            part="btn-cast"
            variant="icon"
            icon="cast"
            :label="label"
            :state="cast?.connection"
            :aria-disabled="insecure ? 'true' : undefined"
            :aria-expanded="insecure ? (hintOpen ? 'true' : 'false') : undefined"
            :aria-describedby="insecure ? hintId : undefined"
            @click="onClick"
        />
        <p
            v-if="insecure"
            :id="hintId"
            data-part="cast-hint"
            role="note"
            :data-state="hintOpen ? 'open' : 'closed'"
        >
            {{ t('cast.httpsRequired') }}
        </p>
    </div>
</template>
