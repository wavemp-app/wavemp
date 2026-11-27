<script setup lang="ts">
import { computed, ref, useId, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Artist, ArtistAlias } from '@/api/generated/models'
import { problemCodeOf, statusOf, validationErrorsOf } from '@/api/problems'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import BaseProgress from '@/components/base/BaseProgress.vue'
import BaseTextField from '@/components/base/BaseTextField.vue'
import { validationMessagesByField } from '@/i18n/validation'
import {
    addArtistAlias,
    artistAliasMaxLength,
    importArtistAliases,
    listArtistAliases,
    removeArtistAlias,
} from '@/library/artistAliasApi'
import {
    aliasFailureKey,
    aliasSourceOf,
    sortAliases,
    type AliasAction,
} from '@/library/artistAliases'

/**
 * The aliases of an artist (admins): other names the search finds the artist by, for example a
 * spelling without symbols or a real name. Lists them with their origin, adds an alias of the
 * admin, removes one and, on request, takes the aliases of the MusicBrainz artist. The search
 * still shows the name of the artist.
 */
const props = defineProps<{
    artist: Artist
}>()

const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()
const formId = useId()

const aliases = ref<ArtistAlias[]>([])
const status = ref<'loading' | 'ready' | 'error'>('loading')
const pending = ref<AliasAction | null>(null)
const name = ref('')
const nameError = ref<string | undefined>(undefined)
const error = ref<string | null>(null)
const message = ref<string | null>(null)
let generation = 0

const busy = computed(() => status.value === 'loading' || pending.value !== null)
const artistId = computed(() => props.artist.id ?? '')

async function load(): Promise<void> {
    const current = ++generation
    status.value = 'loading'
    try {
        const page = await listArtistAliases(artistId.value)
        if (current === generation) {
            aliases.value = sortAliases(page?.items ?? [])
            status.value = 'ready'
        }
    } catch (failure) {
        if (current === generation) {
            error.value = t(aliasFailureKey(problemCodeOf(failure), 'load'))
            status.value = 'error'
        }
    }
}

watch(
    open,
    (value) => {
        if (value) {
            aliases.value = []
            name.value = ''
            nameError.value = undefined
            error.value = null
            message.value = null
            void load()
        } else {
            generation++
        }
    },
    { immediate: true },
)

/** Runs one change at a time and shows its result or failure. */
async function run(action: AliasAction, change: () => Promise<string | null>): Promise<void> {
    if (busy.value) {
        return
    }

    pending.value = action
    error.value = null
    message.value = null
    try {
        message.value = await change()
    } catch (failure) {
        report(failure, action)
    } finally {
        pending.value = null
    }
}

function report(failure: unknown, action: AliasAction): void {
    if (action === 'add' && statusOf(failure) === 400) {
        const messages = validationMessagesByField(
            validationErrorsOf(failure),
            { name: 'name' } as const,
            (key, parameters) => t(key, parameters),
        )
        nameError.value = messages.name ?? t('artistAliases.errors.nameInvalid')
        return
    }

    const key = aliasFailureKey(problemCodeOf(failure), action)
    if (action === 'add' && key === 'artistAliases.errors.exists') {
        nameError.value = t(key)
        return
    }

    error.value = t(key)
    if (key === 'artistAliases.errors.aliasNotFound') {
        void load()
    }
}

function add(): Promise<void> {
    nameError.value = undefined
    const trimmed = name.value.trim()
    if (trimmed === '') {
        nameError.value = t('artistAliases.errors.nameRequired')
        return Promise.resolve()
    }

    return run('add', async () => {
        const added = await addArtistAlias(artistId.value, trimmed)
        if (!added) {
            return null
        }

        aliases.value = sortAliases([...aliases.value, added])
        name.value = ''
        return t('artistAliases.added', { name: added.name ?? trimmed })
    })
}

function remove(alias: ArtistAlias): Promise<void> {
    return run('remove', async () => {
        await removeArtistAlias(artistId.value, alias.id ?? '')
        aliases.value = aliases.value.filter((item) => item.id !== alias.id)
        return t('artistAliases.removed', { name: alias.name ?? '' })
    })
}

function importFromMusicBrainz(): Promise<void> {
    return run('import', async () => {
        const page = await importArtistAliases(artistId.value)
        aliases.value = sortAliases(page?.items ?? [])
        return t('artistAliases.imported')
    })
}
</script>

<template>
    <BaseDialog v-model:open="open" data-item="artist-aliases" :title="t('artistAliases.title')">
        <div
            v-if="open"
            data-part="artist-alias-editor"
            :data-state="pending ?? status"
            :aria-busy="busy ? 'true' : undefined"
        >
            <p data-part="form-note" data-item="artist-aliases-hint">
                {{ t('artistAliases.hint', { name: artist.name ?? '' }) }}
            </p>
            <BaseAlert v-if="error" tone="error" :message="error" />
            <BaseAlert v-else-if="message" tone="success" :message="message" />
            <BaseProgress v-if="status === 'loading'" :label="t('artistAliases.loading')" />
            <ul
                v-else-if="aliases.length > 0"
                data-part="alias-list"
                :aria-label="t('artistAliases.listLabel', { name: artist.name ?? '' })"
            >
                <li
                    v-for="alias in aliases"
                    :key="alias.id ?? alias.name ?? ''"
                    data-part="alias"
                    :data-source="aliasSourceOf(alias)"
                >
                    <span data-part="alias-name">{{ alias.name }}</span>
                    <span data-part="alias-source">
                        {{ t(`artistAliases.sources.${aliasSourceOf(alias)}`) }}
                    </span>
                    <BaseButton
                        data-item="remove-alias"
                        variant="icon"
                        icon="alias-remove"
                        :label="t('artistAliases.remove', { name: alias.name ?? '' })"
                        :disabled="busy"
                        @click="remove(alias)"
                    />
                </li>
            </ul>
            <p v-else-if="status === 'ready'" data-part="form-note" data-item="no-aliases">
                {{ t('artistAliases.empty') }}
            </p>
            <form
                :id="formId"
                data-part="form"
                data-item="add-alias"
                novalidate
                @submit.prevent="add"
            >
                <BaseTextField
                    v-model="name"
                    data-field="alias-name"
                    :label="t('artistAliases.name')"
                    :error="nameError"
                    :maxlength="artistAliasMaxLength"
                    autocomplete="off"
                />
                <BaseButton
                    type="submit"
                    data-item="add-alias"
                    icon="add"
                    :label="t('artistAliases.add')"
                    :disabled="busy"
                />
            </form>
            <div data-part="view-actions">
                <BaseButton
                    data-item="import-aliases"
                    icon="search-online"
                    :label="t('artistAliases.import')"
                    :disabled="busy"
                    @click="importFromMusicBrainz"
                />
            </div>
            <p data-part="form-note" data-item="artist-aliases-privacy">
                {{ t('artistAliases.privacy') }}
            </p>
        </div>
        <template #footer>
            <BaseButton :label="t('dialog.close')" @click="open = false" />
        </template>
    </BaseDialog>
</template>
