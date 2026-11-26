<script setup lang="ts">
import { computed, onActivated, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import type { SmartPlaylist, Track } from '@/api/generated/models'
import { fetchAllPages } from '@/api/paging'
import { problemCodeOf, statusOf } from '@/api/problems'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import DetailHeader from '@/components/library/DetailHeader.vue'
import MetaItem from '@/components/library/MetaItem.vue'
import PlaylistCover from '@/components/playlists/PlaylistCover.vue'
import PlaylistDeleteDialog from '@/components/playlists/PlaylistDeleteDialog.vue'
import PlaylistTrackList, {
    type PlaylistTrackRow,
} from '@/components/playlists/PlaylistTrackList.vue'
import SmartPlaylistConvertDialog from '@/components/playlists/SmartPlaylistConvertDialog.vue'
import { useDetailTitle } from '@/composables/useDetailTitle'
import { useFormat } from '@/composables/useFormat'
import { durationSeconds } from '@/library/presentation'
import { convertFailureKey } from '@/playlists/presentation'
import { getSmartPlaylist, listSmartPlaylistTracks } from '@/playlists/smartApi'
import { messageKey, summaryOf } from '@/playlists/smartRules'
import { usePlayerStore } from '@/stores/player'
import { useSmartPlaylistsStore } from '@/stores/smartPlaylists'

/**
 * A smart playlist of the current profile: its rules in words and the songs that meet them now,
 * computed by the server whenever the page loads. It is played, its rules are edited, it becomes
 * a normal playlist or it is deleted.
 */
const props = defineProps<{
    id: string
}>()

const { t } = useI18n()
const format = useFormat()
const router = useRouter()
const player = usePlayerStore()
const store = useSmartPlaylistsStore()

const smartPlaylist = ref<SmartPlaylist | null>(null)
const tracks = ref<Track[]>([])
const status = ref<'loading' | 'ready' | 'notFound' | 'error'>('loading')
const busy = ref(false)
const action = ref<'convert' | 'delete' | null>(null)
const actionError = ref<string | null>(null)
let generation = 0

const rows = computed<PlaylistTrackRow[]>(() =>
    tracks.value.filter((track) => track.id).map((track) => ({ id: track.id!, track })),
)
const hasAudio = computed(() => tracks.value.some((track) => track.hasAudio))
const duration = computed(() => durationSeconds(smartPlaylist.value?.durationMilliseconds))

const converting = computed({
    get: () => action.value === 'convert',
    set: (value) => {
        action.value = value ? 'convert' : null
    },
})

const deleting = computed({
    get: () => action.value === 'delete',
    set: (value) => {
        action.value = value ? 'delete' : null
    },
})

/** The rules in words, e.g. 'Genre is "Rock"'. */
const rules = computed(() =>
    (smartPlaylist.value?.rules ?? []).flatMap((rule, index) => {
        const summary = summaryOf(rule)
        return summary
            ? [
                  {
                      key: index,
                      reference: summary.reference,
                      text: t(summary.operatorKey, {
                          field: t(summary.fieldKey),
                          value: summary.value,
                          to: summary.to,
                      }),
                  },
              ]
            : []
    }),
)

const ruleIntro = computed(() =>
    rules.value.length === 0
        ? t('smartPlaylists.summary.everything')
        : t(`smartPlaylists.summary.match.${messageKey(smartPlaylist.value?.match ?? 'All')}`),
)

const order = computed(() => {
    const value = smartPlaylist.value
    if (!value?.sort) {
        return ''
    }

    const sorted = t('smartPlaylists.summary.sorted', {
        sort: t(`smartPlaylists.sorts.${messageKey(value.sort)}`),
        direction: t(`smartPlaylists.direction.${messageKey(value.direction ?? 'Asc')}`),
    })
    return value.limit
        ? `${sorted} ${t('smartPlaylists.summary.limit', { count: format.number(value.limit) })}`
        : sorted
})

async function load(): Promise<void> {
    const current = ++generation
    status.value = 'loading'
    try {
        const [loaded, loadedTracks] = await Promise.all([
            getSmartPlaylist(props.id),
            fetchAllPages((offset, limit) => listSmartPlaylistTracks(props.id, offset, limit)),
        ])
        if (current !== generation) {
            return
        }

        smartPlaylist.value = loaded ?? null
        tracks.value = loadedTracks
        status.value = loaded ? 'ready' : 'notFound'
        store.apply(loaded)
    } catch (failure) {
        if (current === generation) {
            smartPlaylist.value = null
            status.value = statusOf(failure) === 404 ? 'notFound' : 'error'
            if (status.value === 'notFound') {
                store.forget(props.id)
            }
        }
    }
}

function play(shuffle: boolean): void {
    player.playTracks(tracks.value, shuffle ? -1 : 0, {
        shuffle,
        source: { kind: 'smart-playlist', id: props.id },
    })
}

function edit(): void {
    void router.push({ name: 'smart-playlist-edit', params: { id: props.id } })
}

function ask(value: 'convert' | 'delete'): void {
    actionError.value = null
    action.value = value
}

async function confirm(): Promise<void> {
    const current = action.value
    busy.value = true
    actionError.value = null
    try {
        if (current === 'convert') {
            const playlist = await store.convert(props.id)
            action.value = null
            void router.replace({ name: 'playlist', params: { id: playlist.id ?? '' } })
        } else {
            await store.remove(props.id)
            action.value = null
            void router.push({ name: 'playlists' })
        }
    } catch (failure) {
        if (statusOf(failure) === 404) {
            store.forget(props.id)
            action.value = null
            void router.push({ name: 'playlists' })
        } else {
            actionError.value = t(
                current === 'convert'
                    ? convertFailureKey(problemCodeOf(failure))
                    : 'smartPlaylists.errors.deleteFailed',
            )
        }
    } finally {
        busy.value = false
    }
}

watch(() => props.id, load, { immediate: true })
useDetailTitle(() => (status.value === 'ready' ? smartPlaylist.value?.name : null))

// Back and forward return to the page; its songs follow the library, so they are computed again.
onActivated(() => {
    if (status.value === 'ready') {
        if (store.status === 'ready' && store.find(props.id) === undefined) {
            status.value = 'notFound'
        } else {
            void load()
        }
    }
})
</script>

<template>
    <div
        data-part="view"
        data-view="smart-playlist"
        :aria-busy="status === 'loading' || busy ? 'true' : undefined"
    >
        <div v-if="status === 'notFound'" data-part="empty-state">
            <p data-part="empty-state-text">{{ t('smartPlaylists.errors.notFound') }}</p>
            <BaseLink :to="{ name: 'playlists' }">{{ t('playlists.toPlaylists') }}</BaseLink>
        </div>
        <div v-else-if="status === 'error'" data-part="empty-state">
            <p data-part="empty-state-title">{{ t('smartPlaylists.errors.loadFailed') }}</p>
            <BaseButton :label="t('library.retry')" @click="load" />
        </div>
        <template v-else-if="smartPlaylist">
            <DetailHeader :title="smartPlaylist.name ?? ''" :back="{ name: 'playlists' }">
                <template #media>
                    <PlaylistCover
                        :id="smartPlaylist.id"
                        :cover="smartPlaylist.cover"
                        icon="smart-playlist"
                    />
                </template>
                <MetaItem field="kind" :label="t('smartPlaylists.kindLabel')">
                    {{ t('smartPlaylists.kind') }}
                </MetaItem>
                <MetaItem field="tracks" :label="t('list.songs')">
                    {{ format.number(smartPlaylist.trackCount ?? 0) }}
                </MetaItem>
                <MetaItem v-if="duration !== null" field="duration" :label="t('list.duration')">
                    {{ format.duration(duration) }}
                </MetaItem>
                <template #actions>
                    <BaseButton
                        v-if="hasAudio"
                        data-item="play"
                        variant="primary"
                        icon="play-now"
                        :label="t('library.play')"
                        @click="play(false)"
                    />
                    <BaseButton
                        v-if="hasAudio"
                        data-item="shuffle"
                        icon="shuffle-play"
                        :label="t('library.shufflePlay')"
                        @click="play(true)"
                    />
                    <BaseButton
                        data-item="edit-rules"
                        icon="edit"
                        :label="t('smartPlaylists.editRules')"
                        :disabled="busy"
                        @click="edit"
                    />
                    <BaseButton
                        data-item="convert"
                        icon="convert-playlist"
                        :label="t('smartPlaylists.convert')"
                        :disabled="busy"
                        @click="ask('convert')"
                    />
                    <BaseButton
                        data-item="delete"
                        icon="delete"
                        :label="t('smartPlaylists.delete')"
                        :disabled="busy"
                        @click="ask('delete')"
                    />
                </template>
            </DetailHeader>
            <section data-part="smart-summary" :aria-label="t('smartPlaylists.rulesTitle')">
                <p data-part="smart-summary-intro">{{ ruleIntro }}</p>
                <ul v-if="rules.length > 0" data-part="smart-summary-rules">
                    <li
                        v-for="rule in rules"
                        :key="rule.key"
                        data-part="smart-summary-rule"
                        :data-reference="rule.reference"
                    >
                        <span data-part="label">{{ rule.text }}</span>
                        <span v-if="rule.reference === 'unlinked'" data-part="smart-summary-note">
                            <BaseIcon name="rule-unlinked" />
                            <span data-part="label">{{ t('smartPlaylists.unlinked') }}</span>
                        </span>
                    </li>
                </ul>
                <p data-part="smart-summary-order">{{ order }}</p>
            </section>
            <div v-if="rows.length === 0" data-part="empty-state">
                <p data-part="empty-state-title">{{ t('smartPlaylists.emptyTitle') }}</p>
                <p data-part="empty-state-text">{{ t('smartPlaylists.emptyText') }}</p>
            </div>
            <PlaylistTrackList
                v-else
                :rows="rows"
                :label="smartPlaylist.name ?? ''"
                :source="{ kind: 'smart-playlist', id: props.id }"
            />
            <SmartPlaylistConvertDialog
                v-if="converting"
                v-model:open="converting"
                :smart-playlist="smartPlaylist"
                :busy="busy"
                :error="actionError"
                @confirm="confirm"
            />
            <PlaylistDeleteDialog
                v-if="deleting"
                v-model:open="deleting"
                kind="smart-playlist"
                :playlist="smartPlaylist"
                :busy="busy"
                :error="actionError"
                @confirm="confirm"
            />
        </template>
    </div>
</template>
