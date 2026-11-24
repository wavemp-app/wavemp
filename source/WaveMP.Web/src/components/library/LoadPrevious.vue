<script setup lang="ts">
import { nextTick, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseButton from '@/components/base/BaseButton.vue'
import { useInView } from '@/composables/useInView'
import { captureScrollAnchor } from '@/router/scrollAnchor'

/**
 * Loads the page before the loaded part of a list that starts in the middle (after a jump to a
 * group): by click, and by itself when it scrolls into view. The entry visible at the top stays in
 * place while the page is inserted above it.
 */
const props = withDefaults(
    defineProps<{
        /** Loads the previous page; resolves when it is in the list. */
        load: () => Promise<unknown>
        loading?: boolean
        /** The entries of the list next to the button that stay in place, e.g. its rows. */
        anchor: string
    }>(),
    { loading: false },
)

const { t } = useI18n()
const button = ref<InstanceType<typeof BaseButton> | null>(null)
const visible = useInView(button)

async function loadPage(): Promise<void> {
    if (props.loading) {
        return
    }

    const element: unknown = button.value?.$el
    const container = element instanceof Element ? element.parentElement : null
    const restore = captureScrollAnchor(container, props.anchor)
    await props.load()
    await nextTick()
    restore()
}

// Only when it comes into view; after a page the button lies above the view again.
watch(visible, (isVisible) => {
    if (isVisible) {
        void loadPage()
    }
})
</script>

<template>
    <BaseButton
        ref="button"
        part="load-previous"
        :label="t('library.loadPrevious')"
        :state="loading ? 'loading' : undefined"
        :aria-busy="loading ? 'true' : undefined"
        @click="loadPage"
    />
</template>
