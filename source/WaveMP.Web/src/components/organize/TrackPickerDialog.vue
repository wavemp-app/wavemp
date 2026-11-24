<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Track } from '@/api/generated/models'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import BaseSearchField from '@/components/base/BaseSearchField.vue'
import { useFormat } from '@/composables/useFormat'
import { listTracks } from '@/library/api'
import { normalizeQuery } from '@/library/search'
import TrackPickerList from './TrackPickerList.vue'

/**
 * Picks songs of the library for an own album: a search over title, artist and album, the results
 * as checkable rows (track-picker-item, state selected; songs without audio file and songs already
 * in the album are disabled with the reason), and "Add selected songs" (event pick, in the order
 * they were checked).
 */
const props = withDefaults(
    defineProps<{
        /** Songs already in the album. */
        excludeIds?: readonly string[]
    }>(),
    { excludeIds: () => [] },
)

const emit = defineEmits<{
    pick: [tracks: Track[]]
}>()

const open = defineModel<boolean>('open', { default: false })

/** Results shown per search. */
const resultLimit = 50
/** Wait after the last key before searching. */
const searchDelayMs = 300

const { t } = useI18n()
const format = useFormat()
const query = ref('')
const results = ref<Track[]>([])
const status = ref<'idle' | 'loading' | 'ready' | 'error'>('idle')
const chosen = ref<Track[]>([])
let timer: ReturnType<typeof setTimeout> | undefined
let request = 0

const excluded = computed(() => new Set(props.excludeIds))
const chosenIds = computed(() => chosen.value.flatMap((track) => (track.id ? [track.id] : [])))

watch(open, (isOpen) => {
    if (isOpen) {
        query.value = ''
        results.value = []
        chosen.value = []
        status.value = 'idle'
    }
})

watch(query, () => {
    clearTimeout(timer)
    timer = setTimeout(() => void search(), searchDelayMs)
})

onBeforeUnmount(() => clearTimeout(timer))

async function search(): Promise<void> {
    const text = normalizeQuery(query.value)
    const own = ++request
    if (!text) {
        results.value = []
        status.value = 'idle'
        return
    }

    status.value = 'loading'
    try {
        const page = await listTracks({ q: text, limit: resultLimit })
        if (own === request) {
            results.value = page?.items ?? []
            status.value = 'ready'
        }
    } catch {
        if (own === request) {
            status.value = 'error'
        }
    }
}

function blocked(track: Track): boolean {
    return !track.hasAudio || (!!track.id && excluded.value.has(track.id))
}

function toggle(track: Track): void {
    if (!track.id || blocked(track)) {
        return
    }

    chosen.value = chosenIds.value.includes(track.id)
        ? chosen.value.filter((item) => item.id !== track.id)
        : [...chosen.value, track]
}

function add(): void {
    if (chosen.value.length === 0) {
        return
    }

    emit('pick', [...chosen.value])
    open.value = false
}
</script>

<template>
    <BaseDialog v-model:open="open" data-item="track-picker" :title="t('ownAlbums.picker.title')">
        <BaseSearchField
            v-model="query"
            :label="t('ownAlbums.picker.search')"
            :placeholder="t('ownAlbums.picker.searchPlaceholder')"
            :clear-label="t('ownAlbums.picker.clear')"
        />
        <p v-if="status === 'idle'" data-part="form-note">{{ t('ownAlbums.picker.hint') }}</p>
        <BaseAlert
            v-else-if="status === 'error'"
            tone="error"
            :message="t('ownAlbums.picker.error')"
        />
        <p v-else-if="status === 'ready' && results.length === 0" data-part="form-note">
            {{ t('ownAlbums.picker.noResults') }}
        </p>
        <TrackPickerList
            v-if="results.length > 0"
            :tracks="results"
            :chosen-ids="chosenIds"
            :exclude-ids="excludeIds"
            :busy="status === 'loading'"
            @toggle="toggle"
        />
        <template #footer>
            <BaseButton :label="t('actions.cancel')" @click="open = false" />
            <BaseButton
                data-item="add-songs"
                variant="primary"
                icon="add"
                :label="t('ownAlbums.picker.add', { count: format.number(chosen.length) })"
                :disabled="chosen.length === 0"
                @click="add"
            />
        </template>
    </BaseDialog>
</template>
