<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { colorTabStop, nextColorIndex } from '@/components/base/colorPicker'
import { indicatorVariables, useIndicator } from '@/composables/useIndicator'
import type { CoverSize } from '@/library/covers'
import { useCoversStore } from '@/stores/covers'

/**
 * Choice of a built-in avatar (radio group of pictures); v-model is the id of the chosen avatar,
 * null for none. Like the color picker it holds an indicator part and tells themes the position of
 * the chosen avatar (--wmp-active-index, --wmp-item-count, --wmp-indicator-x, -y, -width, -height).
 */
const props = defineProps<{
    label: string
    /** Ids of the built-in avatars in the order of the API. */
    presets: readonly string[]
    disabled?: boolean
    /** Pictures to show instead of loading them, by id, e.g. in the component overview. */
    pictures?: Readonly<Record<string, string>>
}>()

const value = defineModel<string | null>({ default: null })

const { t, te } = useI18n()
const covers = useCoversStore()
const picker = ref<HTMLElement | null>(null)
const loaded = ref<Record<string, string | null>>({})

/** The pictures are shown small; 300 pixels stay sharp on screens with a high density. */
const pictureSize: CoverSize = 300
let held: string[] = []

const selected = computed(() => props.presets.indexOf(value.value ?? ''))
const tabStop = computed(() => colorTabStop(selected.value))
const indicator = useIndicator(picker, '[aria-checked="true"]', () => [
    selected.value,
    props.presets.length,
])

function nameOf(id: string): string {
    const key = `avatars.presets.${id}`
    return te(key) ? t(key) : id
}

function releaseAll(): void {
    for (const id of held) {
        covers.release('avatar-preset', id, pictureSize)
    }

    held = []
}

function pictureOf(id: string): string | undefined {
    return (props.pictures ? props.pictures[id] : loaded.value[id]) ?? undefined
}

function load(ids: readonly string[]): void {
    releaseAll()
    loaded.value = {}
    if (props.pictures) {
        return
    }

    held = [...ids]
    for (const id of ids) {
        void covers.acquire('avatar-preset', id, pictureSize).then((url) => {
            if (held.includes(id)) {
                loaded.value = { ...loaded.value, [id]: url }
            }
        })
    }
}

function choose(id: string): void {
    if (!props.disabled) {
        value.value = id
    }
}

async function onKeydown(event: KeyboardEvent): Promise<void> {
    if (props.disabled) {
        return
    }

    const next = nextColorIndex(event.key, selected.value, props.presets.length)
    const id = next === null ? undefined : props.presets[next]
    if (id === undefined) {
        return
    }

    event.preventDefault()
    value.value = id
    await nextTick()
    picker.value?.querySelectorAll<HTMLElement>('[data-part="avatar-preset"]')[next ?? 0]?.focus()
}

watch(() => props.presets, load, { immediate: true })
onBeforeUnmount(releaseAll)
</script>

<template>
    <div
        ref="picker"
        data-part="avatar-presets"
        role="radiogroup"
        :aria-label="label"
        :aria-disabled="disabled ? 'true' : undefined"
        :style="indicatorVariables(presets.length, selected, indicator)"
        @keydown="onKeydown"
    >
        <span data-part="indicator" aria-hidden="true"></span>
        <button
            v-for="(id, index) in presets"
            :key="id"
            type="button"
            data-part="avatar-preset"
            role="radio"
            :data-item="id"
            :aria-checked="index === selected ? 'true' : 'false'"
            :aria-disabled="disabled ? 'true' : undefined"
            :tabindex="index === tabStop ? 0 : -1"
            :title="nameOf(id)"
            @click="choose(id)"
        >
            <img
                v-if="pictureOf(id)"
                data-part="avatar-preset-image"
                :src="pictureOf(id)"
                alt=""
                draggable="false"
            />
            <span data-part="label">{{ nameOf(id) }}</span>
        </button>
    </div>
</template>
