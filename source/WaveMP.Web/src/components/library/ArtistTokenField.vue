<script setup lang="ts">
import { computed, nextTick, ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import {
    ArtistRoleObject,
    ArtistSortObject,
    SortDirectionObject,
    type Artist,
} from '@/api/generated/models'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'
import { useSortableList } from '@/composables/useSortableList'
import { listArtists } from '@/library/api'
import { hasArtist, moveEntry, nameKey, type ArtistEntry } from '@/library/artists'
import { normalizeText } from '@/library/metadata'

/**
 * Token input of the artists of a song or the album artists of the metadata editor: every artist
 * is a chip (artist-token) in its order, the input after the chips is a combobox that searches the
 * artists of the library on every keystroke (by their normalized names and tolerant to typos).
 * Enter adds the chosen match, or the typed text as a new artist (an artist of the library with
 * the same normalized name wins, so its spelling stays). Commas and "&" are part of names, so
 * only Enter adds. A chip is removed with its button; it moves by dragging its handle (mouse, pen
 * and touch) or with the arrow keys on the handle. With roles, every chip has a toggle for a
 * featured artist (aria-pressed). Changes are announced in a status for screen readers.
 */
const props = withDefaults(
    defineProps<{
        label: string
        /** Main and featured artists (songs); without, every artist is a main one (album artists). */
        roles?: boolean
        disabled?: boolean
        /** Longest name. */
        maxlength?: number
        /** Most artists; the input is disabled once the list is full. */
        max?: number
        hint?: string
        /** Error message; marks the field as invalid. */
        error?: string
        /** For the component overview: a typed text and its matches, shown open without focus. */
        preview?: { readonly text: string; readonly matches: readonly Artist[] }
    }>(),
    {
        roles: false,
        disabled: false,
        maxlength: undefined,
        max: undefined,
        hint: undefined,
        error: undefined,
        preview: undefined,
    },
)

const entries = defineModel<ArtistEntry[]>({ default: () => [] })

const { t } = useI18n()
const labelId = useId()
const inputId = useId()
const listboxId = useId()
const hintId = useId()
const errorId = useId()
const optionPrefix = useId()

/** Most matches of the library the suggestions show. */
const matchLimit = 8

interface Option {
    readonly kind: 'known' | 'new'
    readonly entry: ArtistEntry
}

const text = ref(props.preview?.text ?? '')
const matches = ref<Artist[]>([...(props.preview?.matches ?? [])])
const searching = ref(false)
const focused = ref(false)
const activeIndex = ref(-1)
const status = ref('')
const listElement = ref<HTMLElement | null>(null)
const inputElement = ref<HTMLInputElement | null>(null)
let generation = 0

const full = computed(() => props.max !== undefined && entries.value.length >= props.max)
const inputDisabled = computed(() => props.disabled || full.value)
const typed = computed(() => normalizeText(text.value))

const options = computed<Option[]>(() => {
    if (typed.value === '') {
        return []
    }

    const known: Option[] = matches.value
        .filter((artist) => !!artist.name)
        .map((artist) => ({
            kind: 'known' as const,
            entry: { name: artist.name!, role: ArtistRoleObject.Main, id: artist.id ?? undefined },
        }))
        .filter((option) => !hasArtist(entries.value, option.entry))
    const key = nameKey(typed.value)
    const candidate: ArtistEntry = { name: typed.value, role: ArtistRoleObject.Main }
    const sameName = matches.value.some((artist) => nameKey(artist.name ?? '') === key)
    const fresh: Option[] =
        key !== '' && !sameName && !hasArtist(entries.value, candidate)
            ? [{ kind: 'new', entry: candidate }]
            : []
    return [...known, ...fresh]
})

const open = computed(
    () => (focused.value || props.preview !== undefined) && options.value.length > 0,
)
const activeOptionId = computed(() =>
    open.value && activeIndex.value >= 0 ? `${optionPrefix}-${activeIndex.value}` : undefined,
)
const describedBy = computed(
    () =>
        [props.error ? errorId : undefined, hintId].filter((id): id is string => !!id).join(' ') ||
        undefined,
)
const fieldState = computed(
    () =>
        [props.error ? 'invalid' : '', props.disabled ? 'disabled' : '', full.value ? 'full' : '']
            .filter(Boolean)
            .join(' ') || undefined,
)
const tokensState = computed(
    () =>
        [
            open.value ? 'open' : '',
            entries.value.length === 0 ? 'empty' : '',
            searching.value ? 'busy' : '',
        ]
            .filter(Boolean)
            .join(' ') || undefined,
)

useSortableList(listElement, {
    draggable: '[data-part="artist-token"]',
    handle: '[data-part="drag-handle"]',
    disabled: computed(() => props.disabled),
    onMove: (from, to) => move(from, to, false),
})

function roleOf(entry: ArtistEntry): 'main' | 'featured' {
    return props.roles && entry.role === ArtistRoleObject.Featured ? 'featured' : 'main'
}

async function search(): Promise<void> {
    const current = ++generation
    activeIndex.value = -1
    const query = typed.value
    if (query === '') {
        matches.value = []
        searching.value = false
        return
    }

    searching.value = true
    try {
        const page = await listArtists({
            q: query,
            limit: matchLimit,
            sort: ArtistSortObject.Relevance,
            direction: SortDirectionObject.Desc,
        })
        if (current === generation) {
            matches.value = page?.items ?? []
        }
    } catch {
        // Without matches the typed text can still be added as a new artist.
        if (current === generation) {
            matches.value = []
        }
    } finally {
        if (current === generation) {
            searching.value = false
        }
    }
}

function add(entry: ArtistEntry): void {
    if (full.value || hasArtist(entries.value, entry)) {
        return
    }

    entries.value = [...entries.value, entry]
    status.value = t('artistTokens.added', { name: entry.name })
    text.value = ''
    matches.value = []
    activeIndex.value = -1
    generation++
}

/** What Enter adds: the chosen option, else a match with the same name, else the typed text. */
function choice(): ArtistEntry | null {
    const active = options.value[activeIndex.value]
    if (active) {
        return active.entry
    }

    const key = nameKey(typed.value)
    if (key === '') {
        return null
    }

    const match = matches.value.find((artist) => nameKey(artist.name ?? '') === key)
    return match?.name
        ? { name: match.name, role: ArtistRoleObject.Main, id: match.id ?? undefined }
        : { name: typed.value, role: ArtistRoleObject.Main }
}

function step(offset: 1 | -1): void {
    const count = options.value.length
    if (count === 0) {
        return
    }

    activeIndex.value =
        activeIndex.value < 0
            ? offset > 0
                ? 0
                : count - 1
            : (activeIndex.value + offset + count) % count
}

function onInputKeydown(event: KeyboardEvent): void {
    switch (event.key) {
        case 'ArrowDown':
        case 'ArrowUp':
            if (options.value.length > 0) {
                event.preventDefault()
                step(event.key === 'ArrowDown' ? 1 : -1)
            }
            break
        case 'Enter': {
            // An empty input leaves Enter to the form (save).
            if (typed.value === '' || event.isComposing) {
                return
            }

            event.preventDefault()
            const entry = choice()
            if (entry) {
                add(entry)
            }
            break
        }
        case 'Escape':
            if (open.value || text.value !== '') {
                // Closes the suggestions, not the dialog around the field.
                event.preventDefault()
                event.stopPropagation()
                if (open.value && activeIndex.value >= 0) {
                    activeIndex.value = -1
                } else {
                    text.value = ''
                    matches.value = []
                    generation++
                }
            }
            break
        case 'Backspace':
            if (text.value === '' && entries.value.length > 0) {
                event.preventDefault()
                focusPart(entries.value.length - 1, 'remove')
            }
            break
    }
}

function onOptionClick(index: number): void {
    const option = options.value[index]
    if (option) {
        add(option.entry)
        inputElement.value?.focus()
    }
}

function focusPart(index: number, item: 'remove' | 'move'): void {
    void nextTick(() => {
        const token = listElement.value?.querySelectorAll<HTMLElement>(
            '[data-part="artist-token"]',
        )[index]
        const target = token?.querySelector<HTMLElement>(
            item === 'move' ? '[data-part="drag-handle"]' : '[data-item="remove"]',
        )
        ;(target ?? inputElement.value)?.focus()
    })
}

function remove(index: number): void {
    const entry = entries.value[index]
    if (!entry || props.disabled) {
        return
    }

    entries.value = entries.value.filter((_, position) => position !== index)
    status.value = t('artistTokens.removed', { name: entry.name })
    if (index < entries.value.length) {
        focusPart(index, 'remove')
    } else {
        void nextTick(() => inputElement.value?.focus())
    }
}

function move(from: number, to: number, keepFocus: boolean): void {
    const entry = entries.value[from]
    if (!entry || to < 0 || to >= entries.value.length || from === to) {
        return
    }

    entries.value = moveEntry(entries.value, from, to)
    status.value = t('artistTokens.moved', {
        name: entry.name,
        position: to + 1,
        count: entries.value.length,
    })
    if (keepFocus) {
        focusPart(to, 'move')
    }
}

function onHandleKeydown(event: KeyboardEvent, index: number): void {
    const last = entries.value.length - 1
    const targets: Record<string, number> = {
        ArrowUp: index - 1,
        ArrowLeft: index - 1,
        ArrowDown: index + 1,
        ArrowRight: index + 1,
        Home: 0,
        End: last,
    }
    const to = targets[event.key]
    if (to === undefined || props.disabled) {
        return
    }

    event.preventDefault()
    move(index, Math.max(0, Math.min(to, last)), true)
}

function toggleRole(index: number): void {
    const entry = entries.value[index]
    if (!entry || props.disabled) {
        return
    }

    const featured = entry.role !== ArtistRoleObject.Featured
    const role = featured ? ArtistRoleObject.Featured : ArtistRoleObject.Main
    entries.value = entries.value.map((current, position) =>
        position === index ? { ...current, role } : current,
    )
    status.value = featured
        ? t('artistTokens.nowFeatured', { name: entry.name })
        : t('artistTokens.nowMain', { name: entry.name })
}

function onFocus(): void {
    focused.value = true
}

function onBlur(): void {
    focused.value = false
    activeIndex.value = -1
}
</script>

<template>
    <div data-part="field" :data-state="fieldState">
        <span :id="labelId" data-part="label">{{ label }}</span>
        <div data-part="artist-tokens" :data-state="tokensState">
            <ul
                v-show="entries.length > 0"
                ref="listElement"
                data-part="artist-token-list"
                :aria-labelledby="labelId"
            >
                <li
                    v-for="(entry, index) in entries"
                    :key="`${nameKey(entry.name)}:${entry.id ?? ''}`"
                    data-part="artist-token"
                    :data-role="roleOf(entry)"
                    :data-state="entry.id ? 'known' : 'new'"
                >
                    <button
                        type="button"
                        data-part="drag-handle"
                        :disabled="disabled || entries.length < 2"
                        :aria-describedby="hintId"
                        @keydown="onHandleKeydown($event, index)"
                    >
                        <BaseIcon name="drag-handle" />
                        <span data-part="label">{{
                            t('artistTokens.move', {
                                name: entry.name,
                                position: index + 1,
                                count: entries.length,
                            })
                        }}</span>
                    </button>
                    <span data-part="label" data-field="name">{{ entry.name }}</span>
                    <BaseButton
                        v-if="roles"
                        part="artist-role"
                        :label="t('artistTokens.featured')"
                        :aria-label="t('artistTokens.featuredNamed', { name: entry.name })"
                        :pressed="entry.role === ArtistRoleObject.Featured"
                        :disabled="disabled"
                        @click="toggleRole(index)"
                    />
                    <BaseButton
                        variant="icon"
                        icon="artist-remove"
                        data-item="remove"
                        :label="t('artistTokens.remove', { name: entry.name })"
                        :disabled="disabled"
                        @click="remove(index)"
                    />
                </li>
            </ul>
            <input
                :id="inputId"
                ref="inputElement"
                v-model="text"
                data-part="input"
                type="text"
                role="combobox"
                autocomplete="off"
                enterkeyhint="enter"
                aria-autocomplete="list"
                :aria-labelledby="labelId"
                :aria-expanded="open ? 'true' : 'false'"
                :aria-controls="listboxId"
                :aria-activedescendant="activeOptionId"
                :aria-describedby="describedBy"
                :aria-invalid="error ? 'true' : undefined"
                :placeholder="full ? t('artistTokens.full') : t('artistTokens.placeholder')"
                :maxlength="maxlength"
                :disabled="inputDisabled"
                @input="search"
                @keydown="onInputKeydown"
                @focus="onFocus"
                @blur="onBlur"
            />
            <ul
                v-show="open"
                :id="listboxId"
                data-part="artist-suggestions"
                role="listbox"
                :aria-labelledby="labelId"
                :aria-busy="searching ? 'true' : undefined"
            >
                <li
                    v-for="(option, index) in options"
                    :id="`${optionPrefix}-${index}`"
                    :key="`${option.kind}:${option.entry.id ?? nameKey(option.entry.name)}`"
                    data-part="artist-suggestion"
                    role="option"
                    :data-state="option.kind"
                    :aria-selected="index === activeIndex ? 'true' : 'false'"
                    @pointerdown.prevent
                    @click="onOptionClick(index)"
                >
                    <BaseIcon :name="option.kind === 'new' ? 'artist-new' : 'artist-match'" />
                    <span data-part="label">{{
                        option.kind === 'new'
                            ? t('artistTokens.addNew', { name: option.entry.name })
                            : option.entry.name
                    }}</span>
                </li>
            </ul>
        </div>
        <span :id="hintId" data-part="field-hint">{{ hint ?? t('artistTokens.hint') }}</span>
        <span v-if="error" :id="errorId" data-part="field-error">{{ error }}</span>
        <span data-part="artist-tokens-status" role="status">{{ status }}</span>
    </div>
</template>
