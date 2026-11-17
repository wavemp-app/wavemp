<script setup lang="ts">
import { computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { RouterLink, useRoute } from 'vue-router'
import BaseIcon from '@/components/base/BaseIcon.vue'
import { useFormat } from '@/composables/useFormat'
import { usePolling } from '@/composables/usePolling'
import { useReviewStore } from '@/stores/review'
import { useSessionStore } from '@/stores/session'

/**
 * Entry of the review inbox in the header (admins only): an icon, a label and a badge with the
 * number of unreviewed songs. The number is loaded when the profile is selected, while it is shown
 * every minute (so songs of a scan show up) and after every review.
 */
const props = withDefaults(
    defineProps<{
        /** Loads the number itself; false for previews that set it in the store. */
        poll?: boolean
    }>(),
    { poll: true },
)

/** How often the number of unreviewed songs is loaded again, in milliseconds. */
const refreshInterval = 60_000

const { t } = useI18n()
const route = useRoute()
const format = useFormat()
const review = useReviewStore()
const session = useSessionStore()

const count = computed(() => review.unreviewedCount)
const active = computed(() => route.name === 'admin-review' || route.name === 'admin-review-track')
const state = computed(() =>
    [count.value ? 'pending' : 'empty', ...(active.value ? ['active'] : [])].join(' '),
)
const label = computed(() =>
    count.value
        ? t('reviewInbox.buttonWithCount', { count: format.number(count.value) })
        : t('reviewInbox.button'),
)

const polling = usePolling(() => review.refreshCount(), refreshInterval)

watch(
    () => session.token,
    (token) => {
        if (token && props.poll) {
            void review.refreshCount()
            polling.start()
        } else {
            polling.stop()
        }
    },
    { immediate: true, flush: 'post' },
)
</script>

<template>
    <RouterLink v-slot="{ href, navigate }" :to="{ name: 'admin-review' }" custom>
        <a
            data-part="btn-review-inbox"
            :href="href"
            :data-state="state"
            :aria-label="label"
            :aria-current="active ? 'page' : undefined"
            :title="label"
            @click="navigate"
        >
            <BaseIcon name="review-inbox" />
            <span data-part="label">{{ t('reviewInbox.button') }}</span>
            <span v-if="count" data-part="badge" data-state="unreviewed" aria-hidden="true">
                <span data-part="label">{{ format.number(count) }}</span>
            </span>
        </a>
    </RouterLink>
</template>
