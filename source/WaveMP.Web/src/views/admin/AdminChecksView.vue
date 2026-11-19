<script setup lang="ts">
import { computed, onActivated, onBeforeUnmount, onDeactivated, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import {
    LibraryIssueSortObject,
    ProblemCodeObject,
    type Job,
    type LibraryIssueSort,
    type LibraryIssueSummary,
} from '@/api/generated/models'
import { problemCodeOf } from '@/api/problems'
import { typeParameter, type LibraryIssueFilter } from '@/admin/libraryIssues'
import {
    getLibraryCheckJob,
    getLibraryIssueSummary,
    listLibraryIssues,
    startLibraryCheck,
} from '@/admin/libraryIssueApi'
import { isActive } from '@/admin/presentation'
import AdminNav from '@/components/admin/AdminNav.vue'
import LibraryCheckStatus from '@/components/admin/LibraryCheckStatus.vue'
import LibraryIssueFilters from '@/components/admin/LibraryIssueFilters.vue'
import LibraryIssueList from '@/components/admin/LibraryIssueList.vue'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import LoadMore from '@/components/library/LoadMore.vue'
import { usePagedList } from '@/composables/usePagedList'
import { usePolling } from '@/composables/usePolling'
import { searchDelay } from '@/composables/useSearchField'
import { normalizeQuery } from '@/library/search'

/**
 * Admin page of the library check: when it ran last, the number of problems per kind and the
 * problems themselves, filtered by kind, searched and sorted. Each problem leads to where it is
 * fixed (metadata editor, online search, files of the song). The check runs after every scan; the
 * button starts it now, and the page follows the run and loads the result when it ends. Kept alive
 * in the background, so back from a song returns to the same list.
 */
const { t } = useI18n()
const statusTitleId = useId()
const issuesTitleId = useId()

const summary = ref<LibraryIssueSummary | null>(null)
const job = ref<Job | null>(null)
const summaryStatus = ref<'loading' | 'ready' | 'error'>('loading')
const starting = ref(false)
const message = ref<string | null>(null)

const filter = ref<LibraryIssueFilter>('all')
const sort = ref<LibraryIssueSort>(LibraryIssueSortObject.Path)
const search = ref('')
const query = ref('')
let timer: ReturnType<typeof setTimeout> | undefined

const list = usePagedList((offset, limit) => {
    const type = typeParameter(filter.value)
    return listLibraryIssues({
        offset,
        limit,
        sort: sort.value,
        ...(type ? { type } : {}),
        ...(query.value ? { q: query.value } : {}),
    })
})

const active = computed(() => isActive(job.value?.state === 'Idle' ? null : job.value?.state))

async function loadSummary(): Promise<void> {
    try {
        const [loadedSummary, loadedJob] = await Promise.all([
            getLibraryIssueSummary(),
            getLibraryCheckJob(),
        ])
        summary.value = loadedSummary ?? null
        job.value = loadedJob ?? null
        summaryStatus.value = 'ready'
    } catch {
        summaryStatus.value = 'error'
    }
}

/** When the check ran that found the problems the list shows. */
let listedCheck: number | undefined

function reloadList(): Promise<void> {
    listedCheck = summary.value?.checkedAt?.getTime()
    return list.reload()
}

async function load(): Promise<void> {
    await Promise.all([loadSummary(), list.reload()])
    listedCheck = summary.value?.checkedAt?.getTime()
    if (active.value) {
        polling.start()
    }
}

/** Loads the state of the check; once no check runs and its result is new, the list loads too. */
async function refresh(): Promise<void> {
    await loadSummary()
    if (active.value) {
        polling.start()
        return
    }

    polling.stop()
    if (summaryStatus.value === 'ready' && summary.value?.checkedAt?.getTime() !== listedCheck) {
        await reloadList()
    }
}

// While a check runs, the page asks for its state; when it ends, it loads the new result.
const polling = usePolling(refresh, 2000)

async function run(): Promise<void> {
    starting.value = true
    message.value = null
    try {
        await startLibraryCheck()
    } catch (error) {
        message.value =
            problemCodeOf(error) === ProblemCodeObject.JobAlready_running
                ? t('adminChecks.alreadyRunning')
                : t('adminChecks.runError')
    } finally {
        starting.value = false
    }

    await refresh()
}

watch([filter, sort], () => void reloadList())
watch(search, (value) => {
    clearTimeout(timer)
    timer = setTimeout(() => {
        const next = normalizeQuery(value)
        if (next !== query.value) {
            query.value = next
            void reloadList()
        }
    }, searchDelay)
})

let mounted = false
void load().then(() => {
    mounted = true
})
// Back from a song: the result may have changed meanwhile (a scan ran the check again).
onActivated(() => {
    if (mounted) {
        void refresh()
    }
})
onDeactivated(() => polling.stop())
onBeforeUnmount(() => clearTimeout(timer))

const checked = computed(() => !!summary.value?.checkedAt)
const filtered = computed(() => filter.value !== 'all' || query.value !== '')
</script>

<template>
    <div
        data-part="view"
        data-view="admin-checks"
        :aria-busy="summaryStatus === 'loading' ? 'true' : undefined"
    >
        <AdminNav />
        <section data-part="section" data-item="check-status" :aria-labelledby="statusTitleId">
            <h2 :id="statusTitleId" data-part="section-title">{{ t('adminChecks.title') }}</h2>
            <template v-if="summaryStatus === 'error'">
                <BaseAlert tone="error" :message="t('adminChecks.loadError')" />
                <div data-part="view-actions">
                    <BaseButton data-item="retry" :label="t('library.retry')" @click="load" />
                </div>
            </template>
            <BaseAlert v-if="message" tone="error" :message="message" />
            <LibraryCheckStatus
                v-if="summaryStatus === 'ready'"
                :summary="summary"
                :job="job"
                :starting="starting"
                @run="run"
            />
        </section>
        <section data-part="section" data-item="issues" :aria-labelledby="issuesTitleId">
            <h2 :id="issuesTitleId" data-part="section-title">
                {{ t('adminChecks.issuesTitle') }}
            </h2>
            <LibraryIssueFilters
                v-model:search="search"
                v-model:filter="filter"
                v-model:sort="sort"
                :summary="summary"
            />
            <div v-if="list.status.value === 'error'" data-part="empty-state">
                <p data-part="empty-state-title">{{ t('adminChecks.listError') }}</p>
                <BaseButton :label="t('library.retry')" @click="reloadList" />
            </div>
            <div
                v-else-if="list.status.value === 'ready' && list.items.value.length === 0"
                data-part="empty-state"
                :data-state="filtered ? 'filtered' : checked ? 'clean' : 'unchecked'"
            >
                <p data-part="empty-state-title">
                    {{
                        filtered
                            ? t('adminChecks.noMatches')
                            : checked
                              ? t('adminChecks.empty')
                              : t('adminChecks.unchecked')
                    }}
                </p>
            </div>
            <template v-else-if="list.items.value.length > 0">
                <LibraryIssueList
                    :items="list.items.value"
                    :label="t('adminChecks.listLabel')"
                    :busy="list.status.value === 'loading'"
                />
                <LoadMore
                    v-if="list.hasMore.value"
                    :loading="list.loadingMore.value"
                    @load="list.loadMore()"
                />
            </template>
        </section>
    </div>
</template>
