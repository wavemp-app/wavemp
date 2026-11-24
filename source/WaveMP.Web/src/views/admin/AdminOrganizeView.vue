<script setup lang="ts">
import { computed, onMounted, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { OrganizeOperationStatusObject, type OrganizeOperation } from '@/api/generated/models'
import { problemCodeOf } from '@/api/problems'
import { availabilityReason, isOperationActive, organizeProblemKey } from '@/admin/organize'
import { getOrganizingSettings } from '@/admin/organizeApi'
import AdminNav from '@/components/admin/AdminNav.vue'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import BaseSelect from '@/components/base/BaseSelect.vue'
import OrganizeLog from '@/components/organize/OrganizeLog.vue'
import OrganizePreviewDialog from '@/components/organize/OrganizePreviewDialog.vue'
import OrganizeProgress from '@/components/organize/OrganizeProgress.vue'
import OwnAlbumList from '@/components/organize/OwnAlbumList.vue'
import { useOrganizeStore } from '@/stores/organize'

/**
 * Admin page of the organization of the media files: whether the media folders can be written
 * (otherwise every action is disabled with the reason), the own albums, reorganizing the library
 * by the layout template (a preview first, then its progress with Stop), and the log of all
 * previews, runs and undos with "Show changes" and "Undo".
 */
const { t } = useI18n()
const router = useRouter()
const organize = useOrganizeStore()

const albumsTitleId = useId()
const libraryTitleId = useId()
const logTitleId = useId()

const template = ref<string | null>(null)
const sourceId = ref('')
const previewing = ref(false)
const message = ref<string | null>(null)
/** The library preview that was started here; its dialog opens once it is computed. */
const waitingFor = ref<string | null>(null)
const dialogOpen = ref(false)
const dialogOperation = ref<OrganizeOperation | null>(null)
const dialogMode = ref<'preview' | 'changes'>('preview')

const sources = computed(() => organize.availability?.sources ?? [])
const sourceOptions = computed(() => [
    { value: '', label: t('organize.library.allSources') },
    ...sources.value.map((source) => ({
        value: source.sourceId ?? '',
        label: source.name ?? '',
        disabled: !!availabilityReason(source.status),
    })),
])
/** Sources that cannot be organized, each with the reason. */
const blockedSources = computed(() =>
    sources.value.filter((source) => !!availabilityReason(source.status)),
)
/** data-state of the availability: loading, error, available or why organizing is blocked. */
const blockedStates: Readonly<Record<string, string>> = {
    readOnly: 'read-only',
    missing: 'missing',
    inactive: 'inactive',
    busy: 'busy',
    unavailable: 'unavailable',
}
const availabilityStateValue = computed(() => {
    if (organize.availabilityStatus !== 'ready') {
        return organize.availabilityStatus
    }
    return organize.blocked ? blockedStates[organize.blocked] : 'available'
})
const following = computed(() =>
    organize.current && isOperationActive(organize.current.status) ? organize.current : null,
)
const busy = computed(() => !!organize.blocked || previewing.value || !!following.value)

async function load(): Promise<void> {
    await Promise.all([
        organize.loadAvailability(),
        organize.loadOwnAlbums(),
        organize.loadOperations(),
        getOrganizingSettings()
            .then((settings) => {
                template.value = settings?.layoutTemplate ?? null
            })
            .catch(() => {}),
    ])
}

function openDialog(operation: OrganizeOperation, mode: 'preview' | 'changes'): void {
    dialogOperation.value = operation
    dialogMode.value = mode
    dialogOpen.value = true
}

async function previewLibrary(): Promise<void> {
    previewing.value = true
    message.value = null
    try {
        const operation = await organize.previewLibrary(sourceId.value || undefined)
        waitingFor.value = operation?.id ?? null
        if (operation?.status === OrganizeOperationStatusObject.Previewed) {
            waitingFor.value = null
            openDialog(operation, 'preview')
        }
    } catch (failure) {
        message.value = t(
            organizeProblemKey(problemCodeOf(failure), 'organize.problems.previewFailed'),
        )
    } finally {
        previewing.value = false
    }
}

// The preview of the library is computed in the background; its dialog opens when it is ready.
watch(
    () => organize.current,
    (operation) => {
        if (
            !operation ||
            operation.id !== waitingFor.value ||
            isOperationActive(operation.status)
        ) {
            return
        }

        waitingFor.value = null
        if (operation.status === OrganizeOperationStatusObject.Previewed) {
            openDialog(operation, 'preview')
        } else if (operation.status === OrganizeOperationStatusObject.Failed) {
            message.value = t('organize.problems.previewFailed')
        }
    },
)

async function stop(operation: OrganizeOperation): Promise<void> {
    if (!operation.id) {
        return
    }

    try {
        await organize.cancel(operation.id)
    } catch (failure) {
        message.value = t(
            organizeProblemKey(problemCodeOf(failure), 'organize.problems.stopFailed'),
        )
    }
}

function show(operation: OrganizeOperation): void {
    openDialog(
        operation,
        operation.status === OrganizeOperationStatusObject.Previewed ? 'preview' : 'changes',
    )
}

async function undo(operation: OrganizeOperation): Promise<void> {
    if (!operation.id) {
        return
    }

    message.value = null
    try {
        const preview = await organize.previewUndo(operation.id)
        if (preview) {
            openDialog(preview, 'preview')
        }
        void organize.loadOperations()
    } catch (failure) {
        message.value = t(
            organizeProblemKey(problemCodeOf(failure), 'organize.problems.undoFailed'),
        )
    }
}

function applied(): void {
    message.value = null
    void organize.loadOperations()
}

onMounted(load)
</script>

<template>
    <div
        data-part="view"
        data-view="admin-organize"
        :aria-busy="organize.availabilityStatus === 'loading' ? 'true' : undefined"
    >
        <AdminNav />
        <div data-part="organize-availability" :data-state="availabilityStateValue">
            <BaseAlert
                v-if="organize.availabilityStatus === 'error'"
                tone="error"
                :message="t('organize.availability.loadError')"
            />
            <BaseAlert
                v-else-if="organize.blocked && organize.blocked !== 'busy'"
                tone="info"
                :message="t(`organize.blocked.${organize.blocked}`)"
            />
            <BaseAlert
                v-else-if="blockedSources.length > 0"
                tone="info"
                :message="
                    t('organize.availability.someSources', {
                        names: blockedSources.map((source) => source.name).join(', '),
                    })
                "
            />
        </div>
        <BaseAlert v-if="message" tone="error" :message="message" />
        <OrganizeProgress v-if="following" :operation="following" @stop="stop(following)" />

        <section data-part="section" data-item="own-albums" :aria-labelledby="albumsTitleId">
            <h2 :id="albumsTitleId" data-part="section-title">{{ t('ownAlbums.title') }}</h2>
            <p data-part="form-note">{{ t('ownAlbums.hint') }}</p>
            <BaseAlert
                v-if="organize.ownAlbumsStatus === 'error'"
                tone="error"
                :message="t('ownAlbums.errors.loadFailed')"
            />
            <OwnAlbumList v-else-if="organize.ownAlbums.length > 0" :albums="organize.ownAlbums" />
            <div v-else-if="organize.ownAlbumsStatus === 'ready'" data-part="empty-state">
                <p data-part="empty-state-text">{{ t('ownAlbums.empty') }}</p>
            </div>
            <div data-part="view-actions">
                <BaseButton
                    data-item="new-own-album"
                    icon="add"
                    :label="t('ownAlbums.new')"
                    @click="router.push({ name: 'admin-own-album-new' })"
                />
            </div>
        </section>

        <section data-part="section" data-item="reorganize" :aria-labelledby="libraryTitleId">
            <h2 :id="libraryTitleId" data-part="section-title">
                {{ t('organize.library.title') }}
            </h2>
            <p data-part="form-note">{{ t('organize.library.hint') }}</p>
            <p v-if="template" data-part="form-note" data-item="layout-template">
                <span data-part="label">{{ t('organize.library.template') }}</span>
                <span data-part="path-segment" data-kind="template">{{ template }}</span>
                <BaseLink :to="{ name: 'admin-library', hash: '#file-layout' }">
                    {{ t('organize.library.changeTemplate') }}
                </BaseLink>
            </p>
            <BaseSelect
                v-if="sources.length > 1"
                v-model="sourceId"
                data-field="organize-source"
                :label="t('organize.library.source')"
                :options="sourceOptions"
                :disabled="busy"
            />
            <div data-part="view-actions">
                <BaseButton
                    data-item="preview-library"
                    variant="primary"
                    icon="organize-preview"
                    :label="t('organize.library.preview')"
                    :disabled="busy"
                    @click="previewLibrary"
                />
            </div>
        </section>

        <section data-part="section" data-item="organize-log" :aria-labelledby="logTitleId">
            <h2 :id="logTitleId" data-part="section-title">{{ t('organize.log.title') }}</h2>
            <BaseAlert
                v-if="organize.logStatus === 'error'"
                tone="error"
                :message="t('organize.log.loadError')"
            />
            <OrganizeLog
                :operations="organize.operations"
                :has-more="organize.hasMoreOperations"
                :loading="organize.logStatus === 'loading'"
                :can-undo="!organize.blocked"
                @show="show"
                @undo="undo"
                @more="organize.loadOperations({ reset: false })"
            />
        </section>

        <OrganizePreviewDialog
            v-if="dialogOperation"
            v-model:open="dialogOpen"
            :operation="dialogOperation"
            :mode="dialogMode"
            :blocked="organize.blocked"
            @applied="applied"
            @discarded="organize.loadOperations()"
        />
    </div>
</template>
