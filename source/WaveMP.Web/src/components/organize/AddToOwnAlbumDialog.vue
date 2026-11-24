<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import type { OwnAlbum } from '@/api/generated/models'
import { problemCodeOf } from '@/api/problems'
import { organizeProblemKey } from '@/admin/organize'
import { addOwnAlbumTracks } from '@/admin/ownAlbumApi'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import { useOrganizeStore } from '@/stores/organize'
import OwnAlbumChoiceList from './OwnAlbumChoiceList.vue'

/**
 * Adds songs from a context menu to an own album (admins): one own-album-choice per album, or "New
 * own album", which opens the editor with the songs. Adding opens the editor of the album, where
 * the songs can be sorted and the files organized; nothing moves yet.
 */
const props = defineProps<{
    trackIds: readonly string[]
}>()

const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
const router = useRouter()
const organize = useOrganizeStore()
const busy = ref<string | null>(null)
const error = ref<string | null>(null)

const loading = computed(
    () => organize.ownAlbumsStatus === 'loading' && organize.ownAlbums.length === 0,
)

watch(
    open,
    (value) => {
        if (value) {
            busy.value = null
            error.value = null
            void organize.loadOwnAlbums()
        }
    },
    { immediate: true },
)

async function add(album: OwnAlbum): Promise<void> {
    if (!album.id || busy.value) {
        return
    }

    busy.value = album.id
    error.value = null
    try {
        await addOwnAlbumTracks(album.id, props.trackIds)
        open.value = false
        void router.push({ name: 'admin-own-album', params: { id: album.id } })
    } catch (failure) {
        error.value = t(organizeProblemKey(problemCodeOf(failure), 'ownAlbums.errors.addFailed'))
    } finally {
        busy.value = null
    }
}

function createNew(): void {
    organize.pendingTrackIds = [...props.trackIds]
    open.value = false
    void router.push({ name: 'admin-own-album-new' })
}
</script>

<template>
    <BaseDialog
        v-model:open="open"
        data-item="add-to-own-album"
        :title="t('ownAlbums.addDialog.title')"
    >
        <BaseAlert v-if="error" tone="error" :message="error" />
        <p v-if="loading" data-part="form-note">{{ t('ownAlbums.addDialog.loading') }}</p>
        <BaseAlert
            v-else-if="organize.ownAlbumsStatus === 'error'"
            tone="error"
            :message="t('ownAlbums.errors.loadFailed')"
        />
        <p v-else-if="organize.ownAlbums.length === 0" data-part="form-note">
            {{ t('ownAlbums.addDialog.empty') }}
        </p>
        <OwnAlbumChoiceList v-else :albums="organize.ownAlbums" :busy="busy" @choose="add" />
        <template #footer>
            <BaseButton :label="t('actions.cancel')" @click="open = false" />
            <BaseButton
                data-item="new-own-album"
                variant="primary"
                icon="add"
                :label="t('ownAlbums.addDialog.newAlbum')"
                :disabled="busy !== null"
                @click="createNew"
            />
        </template>
    </BaseDialog>
</template>
