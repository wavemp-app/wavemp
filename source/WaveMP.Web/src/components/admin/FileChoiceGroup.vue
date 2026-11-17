<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { FileCategory, FileChoice } from '@/admin/fileSelections'
import { choiceStateTokens } from '@/admin/fileSelections'
import { nextSegmentIndex, segmentTabStop } from '@/components/base/segmented'
import { useFileDetails } from '@/composables/useFileDetails'
import { indicatorVariables, useIndicator } from '@/composables/useIndicator'
import { fileKindKey, fileKindToken } from '@/library/presentation'

/**
 * The files a song can use in one category (audio, video or cover) as a radio group: the
 * automatic choice and every file with its format and quality. Clicking a file selects it. Like
 * the segmented control, the group holds an indicator part and tells themes the position of the
 * checked option (--wmp-active-index, --wmp-item-count, --wmp-indicator-x, -y, -width, -height).
 */
const props = withDefaults(
    defineProps<{
        category: FileCategory
        choices: readonly FileChoice[]
        /** Element with the accessible name of the group. */
        labelledby: string
        /** While a change is saved the group stays focusable but does nothing. */
        disabled?: boolean
    }>(),
    { disabled: false },
)

const emit = defineEmits<{
    choose: [fileId: string | null]
}>()

const { t } = useI18n()
const details = useFileDetails()

const group = ref<HTMLElement | null>(null)
const checked = computed(() => props.choices.findIndex((choice) => choice.checked))
// The arrow keys only move the focus, since every check saves at once; Space and Enter select.
const focused = ref<number | null>(null)
const tabStop = computed(() =>
    focused.value !== null && focused.value < props.choices.length
        ? focused.value
        : segmentTabStop(
              checked.value,
              props.choices.map(() => false),
          ),
)
const indicator = useIndicator(group, '[aria-checked="true"]', () => [
    checked.value,
    props.choices.length,
])

function choose(choice: FileChoice): void {
    if (!props.disabled && !choice.checked) {
        emit('choose', choice.fileId)
    }
}

async function onKeydown(event: KeyboardEvent): Promise<void> {
    const next = nextSegmentIndex(
        event.key,
        tabStop.value,
        props.choices.map(() => false),
    )
    if (next === null) {
        return
    }

    event.preventDefault()
    focused.value = next
    await nextTick()
    group.value?.querySelectorAll<HTMLElement>('[data-part="file-choice"]')[next]?.focus()
}

/** Leaving the group puts the tab stop back on the checked option. */
function onFocusOut(event: FocusEvent): void {
    if (!(event.relatedTarget instanceof Node && group.value?.contains(event.relatedTarget))) {
        focused.value = null
    }
}

function title(choice: FileChoice): string {
    switch (choice.kind) {
        case 'automatic':
            return t('adminFiles.automatic')
        case 'missing':
            return choice.path ?? ''
        default:
            return choice.file?.relativePath ?? choice.fileId ?? ''
    }
}

/** A cover inside an audio or video file instead of a picture file. */
function isEmbedded(choice: FileChoice): boolean {
    return props.category === 'cover' && choice.file !== null && choice.file.kind !== 'Image'
}
</script>

<template>
    <div
        ref="group"
        data-part="file-choices"
        role="radiogroup"
        :data-item="category"
        :aria-labelledby="labelledby"
        :aria-disabled="disabled ? 'true' : undefined"
        :style="indicatorVariables(choices.length, checked, indicator)"
        @keydown="onKeydown"
        @focusout="onFocusOut"
    >
        <span data-part="indicator" aria-hidden="true"></span>
        <button
            v-for="(choice, index) in choices"
            :key="choice.key"
            type="button"
            data-part="file-choice"
            role="radio"
            :data-item="choice.kind"
            :data-state="choiceStateTokens(choice) || undefined"
            :aria-checked="choice.checked ? 'true' : 'false'"
            :aria-disabled="disabled ? 'true' : undefined"
            :tabindex="index === tabStop ? 0 : -1"
            @focus="focused = index"
            @click="choose(choice)"
        >
            <span data-part="file-path">{{ title(choice) }}</span>
            <span data-part="file-meta">
                <template v-if="choice.kind === 'automatic'">
                    <span data-part="label">
                        {{
                            choice.file
                                ? t('adminFiles.automaticFile', {
                                      path: choice.file.relativePath ?? '',
                                  })
                                : t('adminFiles.noFile')
                        }}
                    </span>
                </template>
                <template v-else-if="choice.kind === 'missing'">
                    <span data-part="badge" data-state="missing">
                        <span data-part="label">{{ t('adminFiles.missingFile') }}</span>
                    </span>
                </template>
                <template v-else>
                    <span
                        v-if="choice.file"
                        data-part="badge"
                        :data-state="fileKindToken(choice.file.kind)"
                    >
                        <span data-part="label">{{ t(fileKindKey(choice.file.kind)) }}</span>
                    </span>
                    <span v-if="isEmbedded(choice)" data-part="badge" data-state="embedded">
                        <span data-part="label">{{ t('adminFiles.embedded') }}</span>
                    </span>
                    <span v-if="choice.active" data-part="badge" data-state="active">
                        <span data-part="label">{{ t('adminFiles.inUse') }}</span>
                    </span>
                    <span v-if="choice.best" data-part="badge" data-state="best">
                        <span data-part="label">{{ t('adminFiles.best') }}</span>
                    </span>
                    <span v-if="choice.file?.isLossless" data-part="badge" data-state="lossless">
                        <span data-part="label">{{ t('files.lossless') }}</span>
                    </span>
                    <template v-if="choice.file">
                        <span
                            v-for="(detail, detailIndex) in details(choice.file)"
                            :key="detailIndex"
                            data-part="label"
                        >
                            {{ detail }}
                        </span>
                    </template>
                </template>
            </span>
        </button>
    </div>
</template>
