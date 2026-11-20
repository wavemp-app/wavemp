<script setup lang="ts">
import { onActivated, onMounted, ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import { RecapShareScopeObject, type RecapShare } from '@/api/generated/models'
import { fetchAllPages } from '@/api/paging'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import RecapNav from '@/components/recaps/RecapNav.vue'
import RecapShareList from '@/components/recaps/RecapShareList.vue'
import { listRecapShares } from '@/recaps/api'
import { useRecapsStore } from '@/stores/recaps'

/**
 * The recaps the other profiles share with this one, newest first, and the recaps this profile
 * shares, which it can stop sharing here.
 */
const { t } = useI18n()
const recaps = useRecapsStore()
const received = ref<RecapShare[]>([])
const status = ref<'loading' | 'ready' | 'error'>('loading')
const stopFailed = ref(false)
const stopping = ref<string | null>(null)
const receivedTitleId = useId()
const ownTitleId = useId()

async function load(): Promise<void> {
    status.value = 'loading'
    try {
        const [items] = await Promise.all([
            fetchAllPages((offset, limit) =>
                listRecapShares(RecapShareScopeObject.Received, offset, limit),
            ),
            recaps.loadOwnShares(),
        ])
        received.value = items
        status.value = 'ready'
    } catch {
        status.value = 'error'
    }
}

async function stop(share: RecapShare): Promise<void> {
    if (!share.id) {
        return
    }
    stopping.value = share.id
    stopFailed.value = false
    try {
        await recaps.stopSharing(share.id)
    } catch {
        stopFailed.value = true
    } finally {
        stopping.value = null
    }
}

onMounted(load)
onActivated(load)
</script>

<template>
    <div data-part="view" data-view="shared-recaps" :aria-busy="status === 'loading' || undefined">
        <RecapNav />
        <div v-if="status === 'error'" data-part="empty-state" data-item="shared-recaps-error">
            <p data-part="empty-state-title">{{ t('recap.sharedLoadError') }}</p>
            <BaseButton data-item="retry" :label="t('library.retry')" @click="load" />
        </div>
        <template v-else>
            <section data-part="section" data-item="received" :aria-labelledby="receivedTitleId">
                <h2 :id="receivedTitleId" data-part="section-title">
                    {{ t('recap.receivedTitle') }}
                </h2>
                <p
                    v-if="status === 'ready' && received.length === 0"
                    data-part="form-note"
                    data-item="received-empty"
                >
                    {{ t('recap.receivedEmpty') }}
                </p>
                <RecapShareList v-else :shares="received" />
            </section>
            <section data-part="section" data-item="own" :aria-labelledby="ownTitleId">
                <h2 :id="ownTitleId" data-part="section-title">{{ t('recap.ownTitle') }}</h2>
                <p
                    v-if="recaps.sharesStatus === 'ready' && recaps.ownShares.length === 0"
                    data-part="form-note"
                    data-item="own-empty"
                >
                    {{ t('recap.ownEmpty') }}
                </p>
                <RecapShareList
                    v-else
                    :shares="recaps.ownShares"
                    own
                    :stopping="stopping"
                    @stop="stop"
                />
                <BaseAlert v-if="stopFailed" tone="error" :message="t('recap.stopSharingFailed')" />
            </section>
        </template>
    </div>
</template>
