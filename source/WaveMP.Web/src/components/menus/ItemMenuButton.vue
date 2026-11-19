<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import BaseButton from '@/components/base/BaseButton.vue'

/**
 * The button "More options" (the "..." of touch screens) that opens the context menu of the
 * element it belongs to, below itself. The base shows it where there is no pointer that can hover;
 * themes decide otherwise. Not a tab stop in lists (focusable false), where the keyboard opens the
 * menu of the row.
 */
withDefaults(
    defineProps<{
        focusable?: boolean
    }>(),
    { focusable: true },
)

const emit = defineEmits<{
    open: [x: number, y: number]
}>()

const { t } = useI18n()

function onClick(event: MouseEvent): void {
    event.stopPropagation()
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect()
    emit('open', rect.left, rect.bottom)
}
</script>

<template>
    <BaseButton
        part="btn-item-menu"
        variant="icon"
        icon="item-menu"
        :label="t('contextMenu.more')"
        aria-haspopup="menu"
        :tabindex="focusable ? undefined : -1"
        @click="onClick"
        @dblclick.stop
        @pointerdown.stop
    />
</template>
