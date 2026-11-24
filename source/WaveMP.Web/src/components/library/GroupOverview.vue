<script setup lang="ts">
import { computed, nextTick, onDeactivated, onMounted, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ArtistReference } from '@/api/generated/models'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseCover from '@/components/base/BaseCover.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'
import { isTypeaheadKey, typeaheadIndex } from '@/components/base/menu'
import LibraryCover from './LibraryCover.vue'
import { useFormat } from '@/composables/useFormat'
import { indicatorVariables, useIndicator } from '@/composables/useIndicator'
import type { ListGroupsStatus } from '@/composables/useListGroups'
import {
    gridMove,
    groupIndexAt,
    overviewLetters,
    showsLetterBar,
    type OverviewGroup,
    type OverviewUnit,
} from '@/library/groupOverview'
import { otherIndexLetter } from '@/library/indexLetters'
import { artistLabelKey } from '@/library/presentation'

/**
 * The overview of the groups of a list (the quick jump), on the native modal <dialog>: focus
 * handling, the inert page behind it and Escape come from the browser. Every group is a tile with
 * its name (album with a small cover and its artist, artist, letter, year or disc) and its number
 * of entries; the current group is marked (aria-current, data-state "current" and the indicator of
 * the grid). Many groups with letters get a letter bar A to Z and # that moves to the first group
 * of a letter. The tiles are one tab stop: the arrow keys move through the rows and columns, Home,
 * End, Page Up and Page Down further, and typing moves to the next group with that name.
 */
const props = withDefaults(
    defineProps<{
        groups: readonly OverviewGroup[]
        /** Whether the groups are being loaded, ready or could not be loaded. */
        status?: ListGroupsStatus
        /** Position of an entry of the current group. */
        current?: number
        /** What the entries of the list are, for the number of entries of each group. */
        unit: OverviewUnit
    }>(),
    { status: 'ready', current: undefined },
)

const open = defineModel<boolean>('open', { default: false })

const emit = defineEmits<{
    /** The chosen group; the overview closes. */
    choose: [group: OverviewGroup]
    /** Load the groups again after an error. */
    retry: []
}>()

const { t, locale } = useI18n()
const format = useFormat()
const titleId = useId()
const dialog = ref<HTMLDialogElement | null>(null)
const grid = ref<HTMLElement | null>(null)

const state = computed(() =>
    props.status === 'error' ? 'error' : props.status === 'ready' ? 'ready' : 'loading',
)
const currentIndex = computed(() => groupIndexAt(props.groups, props.current))
const currentLetter = computed(() => props.groups[currentIndex.value]?.letter)
const letterBar = computed(() => showsLetterBar(props.groups))
const letters = computed(() => (letterBar.value ? overviewLetters(props.groups) : []))
/** The tile with the tab stop. */
const focusIndex = ref(0)

function label(group: OverviewGroup): string {
    switch (group.kind) {
        case 'album':
            return group.album?.title || t('library.unknownAlbum')
        case 'artist':
            return group.artist?.name || t('library.unknownArtist')
        case 'letter':
            return group.letter ?? otherIndexLetter
        case 'year':
            return group.year === undefined ? t('library.unknownYear') : format.year(group.year)
        case 'disc':
            return t('library.disc', { number: format.number(group.disc ?? 1) })
        default:
            return group.unknown === 'artist'
                ? t('library.unknownArtist')
                : group.unknown === 'year'
                  ? t('library.unknownYear')
                  : t('library.unknownAlbum')
    }
}

function artistName(artist: ArtistReference | null | undefined, isCompilation = false): string {
    const key = artistLabelKey(artist, isCompilation)
    return key ? t(key) : (artist?.name ?? '')
}

/** The artist of an album, or of the songs without album. */
function detail(group: OverviewGroup): string | undefined {
    if (group.kind === 'album') {
        return artistName(group.album?.artist, group.album?.isCompilation ?? false)
    }

    return group.kind === 'unknown' && group.artist ? artistName(group.artist) : undefined
}

function countText(group: OverviewGroup): string {
    const count = format.number(group.count)
    switch (props.unit) {
        case 'albums':
            return t('library.albumCount', { count })
        case 'artists':
            return t('library.groups.artistCount', { count })
        default:
            return t('library.songCount', { count })
    }
}

/** "#" is read out as what it stands for. */
function accessibleName(group: OverviewGroup): string | undefined {
    return group.kind === 'letter' && group.letter === otherIndexLetter
        ? `${t('library.otherLetter')}, ${countText(group)}`
        : undefined
}

/** Albums show their cover, the songs without album the placeholder, artists the artist icon. */
function coverOf(group: OverviewGroup): 'album' | 'placeholder' | 'artist' | null {
    if (group.kind === 'album') {
        return 'album'
    }

    if (group.kind === 'artist' || (group.kind === 'unknown' && group.unknown === 'artist')) {
        return 'artist'
    }

    return group.kind === 'unknown' && group.unknown === 'album' ? 'placeholder' : null
}

const labels = computed(() => props.groups.map((group) => ({ label: label(group) })))

const indicator = useIndicator(grid, "[data-part='group-tile'][data-state~='current']", () => [
    currentIndex.value,
    props.groups.length,
    open.value,
    state.value,
])

function tiles(): HTMLElement[] {
    return grid.value
        ? Array.from(grid.value.querySelectorAll<HTMLElement>("[data-part='group-tile']"))
        : []
}

/** Tiles per row and rows in view, measured from the laid-out tiles. */
function layout(): { columns: number; rows: number } {
    const all = tiles()
    const first = all[0]?.getBoundingClientRect()
    if (!first) {
        return { columns: 1, rows: 1 }
    }

    let columns = 1
    while (columns < all.length) {
        const next = all[columns]!.getBoundingClientRect()
        if (Math.abs(next.top - first.top) >= 1) {
            break
        }
        columns++
    }

    const height = first.height || 1
    const rows = Math.max(1, Math.floor((grid.value?.clientHeight ?? height) / height))
    return { columns, rows }
}

async function focusTile(index: number, block: ScrollLogicalPosition = 'nearest'): Promise<void> {
    if (index < 0 || index >= props.groups.length) {
        return
    }

    focusIndex.value = index
    await nextTick()
    const tile = tiles()[index]
    tile?.focus({ preventScroll: true })
    tile?.scrollIntoView?.({ block, inline: 'nearest' })
}

let typed = ''
let typedAt = 0
const typeaheadTimeout = 700

function onGridKeydown(event: KeyboardEvent): void {
    const next = gridMove(event.key, focusIndex.value, {
        count: props.groups.length,
        ...layout(),
    })
    if (next !== null && !event.ctrlKey && !event.metaKey && !event.altKey) {
        event.preventDefault()
        void focusTile(next)
        return
    }

    if (isTypeaheadKey(event)) {
        const now = Date.now()
        typed = now - typedAt > typeaheadTimeout ? event.key : typed + event.key
        typedAt = now
        const found = typeaheadIndex(labels.value, focusIndex.value, typed, locale.value)
        if (found >= 0) {
            event.preventDefault()
            void focusTile(found)
        }
    }
}

function goToLetter(index: number): void {
    void focusTile(index, 'start')
}

function choose(group: OverviewGroup): void {
    emit('choose', group)
}

// The tab stop starts at the current group; the focus moves there once the groups are shown.
let focused = false

async function focusCurrent(): Promise<void> {
    if (!open.value || focused || state.value !== 'ready' || props.groups.length === 0) {
        return
    }

    focused = true
    await focusTile(Math.max(0, currentIndex.value), 'center')
}

function sync(): void {
    const element = dialog.value
    if (!element) {
        return
    }

    if (open.value && !element.open) {
        focusIndex.value = Math.max(0, currentIndex.value)
        focused = false
        element.showModal?.()
        void focusCurrent()
    } else if (!open.value && element.open) {
        element.close?.()
    }
}

watch(open, sync)
watch([state, () => props.groups], () => void focusCurrent(), { flush: 'post' })
onMounted(sync)

function close(): void {
    open.value = false
}

// A page kept alive in the background takes its dialogs out of the document: they close.
onDeactivated(close)
</script>

<template>
    <dialog
        ref="dialog"
        data-part="group-overview"
        :data-state="state"
        :aria-labelledby="titleId"
        :aria-busy="state === 'loading' ? 'true' : undefined"
        @close="close"
    >
        <div data-part="group-overview-header">
            <h2 :id="titleId" data-part="group-overview-title">{{ t('library.groups.title') }}</h2>
            <BaseButton
                part="btn-close"
                variant="icon"
                icon="close"
                :label="t('dialog.close')"
                @click="close"
            />
        </div>
        <nav
            v-if="letterBar && state === 'ready'"
            data-part="letter-bar"
            :aria-label="t('library.groups.letters')"
        >
            <button
                v-for="entry in letters"
                :key="entry.letter"
                type="button"
                data-part="letter"
                :data-item="entry.letter"
                :data-state="entry.letter === currentLetter ? 'current' : undefined"
                :aria-disabled="entry.index < 0 ? 'true' : undefined"
                :aria-label="
                    entry.letter === otherIndexLetter ? t('library.otherLetter') : undefined
                "
                @click="entry.index >= 0 && goToLetter(entry.index)"
            >
                <span data-part="label">{{ entry.letter }}</span>
            </button>
        </nav>
        <div v-if="state === 'error'" data-part="empty-state">
            <p data-part="empty-state-title">{{ t('library.groups.loadError') }}</p>
            <BaseButton :label="t('library.retry')" @click="emit('retry')" />
        </div>
        <div
            v-else
            ref="grid"
            data-part="group-grid"
            role="list"
            :aria-label="t('library.groups.label')"
            :style="indicatorVariables(groups.length, currentIndex, indicator)"
            @keydown="onGridKeydown"
        >
            <span data-part="indicator" aria-hidden="true"></span>
            <template v-if="state === 'ready'">
                <div
                    v-for="(group, index) in groups"
                    :key="group.id"
                    data-part="group-item"
                    role="listitem"
                >
                    <button
                        type="button"
                        data-part="group-tile"
                        :data-kind="group.kind"
                        :data-state="index === currentIndex ? 'current' : undefined"
                        :aria-current="index === currentIndex ? 'true' : undefined"
                        :aria-label="accessibleName(group)"
                        :tabindex="index === focusIndex ? 0 : -1"
                        @click="choose(group)"
                        @focus="focusIndex = index"
                    >
                        <LibraryCover
                            v-if="coverOf(group) === 'album'"
                            kind="album"
                            :id="group.album?.id"
                            :available="group.hasCover ?? false"
                        />
                        <BaseCover v-else-if="coverOf(group) === 'placeholder'" />
                        <BaseCover v-else-if="coverOf(group) === 'artist'">
                            <BaseIcon name="artist" />
                        </BaseCover>
                        <span data-part="label">{{ labels[index]?.label }}</span>
                        <span v-if="detail(group)" data-part="group-tile-detail">{{
                            detail(group)
                        }}</span>
                        <span data-part="group-tile-count">{{ countText(group) }}</span>
                    </button>
                </div>
            </template>
        </div>
    </dialog>
</template>
