<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import {
    SmartPlaylistMatchObject,
    SortDirectionObject,
    type SmartPlaylist,
    type SmartPlaylistTemplate,
} from '@/api/generated/models'
import { statusOf, validationErrorsOf, type FieldError } from '@/api/problems'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseCheckbox from '@/components/base/BaseCheckbox.vue'
import BaseLink from '@/components/base/BaseLink.vue'
import BaseProgress from '@/components/base/BaseProgress.vue'
import BaseSegmentedField from '@/components/base/BaseSegmentedField.vue'
import BaseSelect from '@/components/base/BaseSelect.vue'
import BaseTextField from '@/components/base/BaseTextField.vue'
import SmartPlaylistRuleRow, {
    type RuleErrors,
} from '@/components/playlists/SmartPlaylistRuleRow.vue'
import { useDetailTitle } from '@/composables/useDetailTitle'
import { validationMessage } from '@/i18n/validation'
import { playlistNameMaxLength } from '@/playlists/presentation'
import { getSmartPlaylist } from '@/playlists/smartApi'
import {
    draftOf,
    emptyDraft,
    formatDurationValue,
    kindOf,
    maxLimit,
    maxRules,
    messageKey,
    newRule,
    parseLimit,
    ruleErrorOf,
    ruleProblems,
    smartSorts,
    toSave,
    type RuleDraft,
    type SmartPlaylistDraft,
} from '@/playlists/smartRules'
import { useSmartPlaylistsStore } from '@/stores/smartPlaylists'

/**
 * Creates a smart playlist (without id), optionally from a template, or changes the rules of one:
 * its name, whether all or any rules must match, the rules, the order and an optional limit. The
 * web app checks the input first; the errors of the server show at the same places.
 */
const props = defineProps<{
    id?: string
}>()

const { t } = useI18n()
const router = useRouter()
const store = useSmartPlaylistsStore()

const draft = ref<SmartPlaylistDraft>(emptyDraft())
const original = ref<SmartPlaylist | null>(null)
const status = ref<'loading' | 'ready' | 'notFound' | 'error'>(props.id ? 'loading' : 'ready')
const templates = ref<SmartPlaylistTemplate[]>([])
const appliedTemplate = ref<string | null>(null)
const saving = ref(false)
const error = ref<string | null>(null)
const nameError = ref<string | undefined>()
const limitError = ref<string | undefined>()
const rulesError = ref<string | undefined>()
const ruleErrors = ref<RuleErrors[]>([])
let generation = 0

const editing = computed(() => !!props.id)

const matchOptions = computed(() =>
    Object.values(SmartPlaylistMatchObject).map((match) => ({
        value: match,
        label: t(`smartPlaylists.match.${messageKey(match)}`),
    })),
)

const sortOptions = computed(() =>
    smartSorts.map((sort) => ({
        value: sort,
        label: t(`smartPlaylists.sorts.${messageKey(sort)}`),
    })),
)

const directionOptions = computed(() =>
    Object.values(SortDirectionObject).map((direction) => ({
        value: direction,
        label: t(`smartPlaylists.direction.${messageKey(direction)}`),
    })),
)

const match = computed({
    get: () => draft.value.match,
    set: (value: string) => {
        draft.value.match = value as SmartPlaylistDraft['match']
    },
})

const sort = computed({
    get: () => draft.value.sort,
    set: (value: string) => {
        draft.value.sort = value as SmartPlaylistDraft['sort']
    },
})

const direction = computed({
    get: () => draft.value.direction,
    set: (value: string) => {
        draft.value.direction = value as SmartPlaylistDraft['direction']
    },
})

/** "FourStarsAndMore" as "four-stars-and-more", the data-item of its button. */
function itemOf(id: string): string {
    return id.replace(/([a-z])([A-Z0-9])/g, '$1-$2').toLowerCase()
}

function templateName(template: SmartPlaylistTemplate): string {
    return t(`smartPlaylists.templates.${messageKey(template.id ?? '')}`)
}

function clearErrors(): void {
    error.value = null
    nameError.value = undefined
    limitError.value = undefined
    rulesError.value = undefined
    ruleErrors.value = []
}

/** Takes over the rules of a template; a name the user typed stays. */
function applyTemplate(template: SmartPlaylistTemplate): void {
    const previous = templates.value.find((item) => item.id === appliedTemplate.value)
    const keepName =
        draft.value.name.trim().length > 0 &&
        (!previous || draft.value.name !== templateName(previous))
    draft.value = draftOf(template, keepName ? draft.value.name : templateName(template))
    appliedTemplate.value = template.id ?? null
    clearErrors()
}

function updateRule(index: number, rule: RuleDraft): void {
    draft.value.rules.splice(index, 1, rule)
    if (ruleErrors.value[index]) {
        ruleErrors.value.splice(index, 1, {})
    }
}

function addRule(): void {
    if (draft.value.rules.length < maxRules) {
        draft.value.rules.push(newRule())
        rulesError.value = undefined
    }
}

function removeRule(index: number): void {
    draft.value.rules.splice(index, 1)
    ruleErrors.value.splice(index, 1)
}

function message(fieldError: FieldError, field?: RuleDraft['field']): string {
    const duration = field !== undefined && kindOf(field) === 'duration'
    return validationMessage(fieldError, (key, parameters) => t(key, parameters), {
        keys: {
            invalid_format: duration
                ? 'smartPlaylists.errors.invalidDuration'
                : 'smartPlaylists.errors.invalidNumber',
        },
        formatLimit: duration ? formatDurationValue : String,
    })
}

/** Checks the input like the server does; true if it can be sent. */
function check(): boolean {
    clearErrors()
    if (draft.value.name.trim().length === 0) {
        nameError.value = t('validation.required')
    }

    if (draft.value.limited && parseLimit(draft.value.limit) === undefined) {
        limitError.value = message({ field: 'limit', code: 'out_of_range', min: 1, max: maxLimit })
    }

    ruleErrors.value = draft.value.rules.map((rule) => {
        const errors: Record<string, string> = {}
        for (const problem of ruleProblems(rule)) {
            errors[problem.part] = message({ field: problem.part, ...problem }, rule.field)
        }

        return errors
    })

    return (
        !nameError.value &&
        !limitError.value &&
        ruleErrors.value.every((errors) => Object.keys(errors).length === 0)
    )
}

/** Shows the errors of the server at their fields; true if there were any. */
function showServerErrors(failure: unknown): boolean {
    const errors = validationErrorsOf(failure)
    const rules: Record<string, string>[] = draft.value.rules.map(() => ({}))
    for (const fieldError of errors) {
        const rule = ruleErrorOf(fieldError.field)
        if (rule && rules[rule.index]) {
            rules[rule.index]![rule.part] = message(
                fieldError,
                draft.value.rules[rule.index]?.field,
            )
        } else if (fieldError.field === 'name') {
            nameError.value = message(fieldError)
        } else if (fieldError.field === 'limit') {
            limitError.value = message(fieldError)
        } else if (fieldError.field === 'rules') {
            rulesError.value = message(fieldError)
        } else {
            error.value = t('smartPlaylists.errors.saveFailed')
        }
    }

    ruleErrors.value = rules
    return errors.length > 0
}

async function save(): Promise<void> {
    if (saving.value) {
        return
    }

    if (!check()) {
        error.value = t('smartPlaylists.errors.checkInput')
        return
    }

    saving.value = true
    try {
        const body = toSave(draft.value)
        const saved = props.id ? await store.update(props.id, body) : await store.create(body)
        void router.replace({ name: 'smart-playlist', params: { id: saved.id ?? '' } })
    } catch (failure) {
        if (statusOf(failure) === 404 && props.id) {
            store.forget(props.id)
            status.value = 'notFound'
        } else if (showServerErrors(failure)) {
            error.value ??= t('smartPlaylists.errors.checkInput')
        } else {
            error.value = t('smartPlaylists.errors.saveFailed')
        }
    } finally {
        saving.value = false
    }
}

function cancel(): void {
    void router.push(
        props.id ? { name: 'smart-playlist', params: { id: props.id } } : { name: 'playlists' },
    )
}

async function load(): Promise<void> {
    const current = ++generation
    clearErrors()
    appliedTemplate.value = null
    if (!props.id) {
        original.value = null
        draft.value = emptyDraft()
        status.value = 'ready'
        try {
            const loaded = await store.loadTemplates()
            if (current === generation) {
                templates.value = loaded
            }
        } catch {
            // Without templates the editor still works; the rules are entered by hand.
        }
        return
    }

    status.value = 'loading'
    try {
        const loaded = await getSmartPlaylist(props.id)
        if (current !== generation) {
            return
        }

        original.value = loaded ?? null
        status.value = loaded ? 'ready' : 'notFound'
        if (loaded) {
            draft.value = draftOf(loaded)
            store.apply(loaded)
        }
    } catch (failure) {
        if (current === generation) {
            status.value = statusOf(failure) === 404 ? 'notFound' : 'error'
        }
    }
}

watch(() => props.id, load, { immediate: true })
useDetailTitle(() => (editing.value ? original.value?.name : null))
</script>

<template>
    <div
        data-part="view"
        data-view="smart-playlist-edit"
        :data-state="editing ? 'edit' : 'create'"
        :aria-busy="status === 'loading' || saving ? 'true' : undefined"
    >
        <BaseProgress v-if="status === 'loading'" :label="t('smartPlaylists.loading')" />
        <div v-else-if="status === 'notFound'" data-part="empty-state">
            <p data-part="empty-state-text">{{ t('smartPlaylists.errors.notFound') }}</p>
            <BaseLink :to="{ name: 'playlists' }">{{ t('playlists.toPlaylists') }}</BaseLink>
        </div>
        <div v-else-if="status === 'error'" data-part="empty-state">
            <p data-part="empty-state-title">{{ t('smartPlaylists.errors.loadFailed') }}</p>
            <BaseButton :label="t('library.retry')" @click="load" />
        </div>
        <form v-else data-part="form" data-kind="smart-playlist" novalidate @submit.prevent="save">
            <BaseAlert v-if="error" tone="error" :message="error" />
            <BaseTextField
                v-model="draft.name"
                data-field="name"
                :label="t('smartPlaylists.name')"
                :error="nameError"
                :maxlength="playlistNameMaxLength"
                :disabled="saving"
                autocomplete="off"
                required
            />
            <section
                v-if="!editing && templates.length > 0"
                data-part="section"
                data-item="templates"
                aria-labelledby="smart-templates-title"
            >
                <h3 id="smart-templates-title" data-part="section-title">
                    {{ t('smartPlaylists.templatesTitle') }}
                </h3>
                <div data-part="smart-templates">
                    <BaseButton
                        v-for="template in templates"
                        :key="template.id ?? ''"
                        :data-item="itemOf(template.id ?? '')"
                        :data-state="appliedTemplate === template.id ? 'applied' : undefined"
                        icon="create-smart-playlist"
                        :label="templateName(template)"
                        :disabled="saving"
                        @click="applyTemplate(template)"
                    />
                </div>
            </section>
            <section data-part="section" data-item="rules" aria-labelledby="smart-rules-title">
                <h3 id="smart-rules-title" data-part="section-title">
                    {{ t('smartPlaylists.rulesTitle') }}
                </h3>
                <BaseSegmentedField
                    v-model="match"
                    item="match"
                    :label="t('smartPlaylists.matchLabel')"
                    :options="matchOptions"
                    :disabled="saving"
                />
                <ol v-if="draft.rules.length > 0" data-part="smart-rules">
                    <SmartPlaylistRuleRow
                        v-for="(rule, index) in draft.rules"
                        :key="rule.key"
                        :rule="rule"
                        :index="index"
                        :errors="ruleErrors[index]"
                        :disabled="saving"
                        @update:rule="updateRule(index, $event)"
                        @remove="removeRule(index)"
                    />
                </ol>
                <p v-else data-part="form-note">{{ t('smartPlaylists.noRules') }}</p>
                <span v-if="rulesError" data-part="field-error">{{ rulesError }}</span>
                <div data-part="view-actions">
                    <BaseButton
                        data-item="add-rule"
                        icon="add"
                        :label="t('smartPlaylists.addRule')"
                        :disabled="saving || draft.rules.length >= maxRules"
                        @click="addRule"
                    />
                </div>
            </section>
            <section data-part="section" data-item="order" aria-labelledby="smart-order-title">
                <h3 id="smart-order-title" data-part="section-title">
                    {{ t('smartPlaylists.orderTitle') }}
                </h3>
                <div data-part="field-row">
                    <BaseSelect
                        v-model="sort"
                        data-field="sort"
                        :label="t('smartPlaylists.sortLabel')"
                        :options="sortOptions"
                        :disabled="saving"
                    />
                    <BaseSegmentedField
                        v-model="direction"
                        item="direction"
                        data-field="direction"
                        :label="t('smartPlaylists.directionLabel')"
                        :options="directionOptions"
                        :disabled="saving"
                    />
                </div>
                <BaseCheckbox
                    v-model="draft.limited"
                    data-field="limited"
                    :label="t('smartPlaylists.limited')"
                    :disabled="saving"
                />
                <BaseTextField
                    v-if="draft.limited"
                    v-model="draft.limit"
                    data-field="limit"
                    :label="t('smartPlaylists.limit')"
                    :hint="t('smartPlaylists.limitHint')"
                    :error="limitError"
                    inputmode="numeric"
                    :disabled="saving"
                    autocomplete="off"
                />
            </section>
            <div data-part="view-actions">
                <BaseButton
                    data-item="cancel"
                    :label="t('actions.cancel')"
                    :disabled="saving"
                    @click="cancel"
                />
                <BaseButton
                    data-item="save"
                    type="submit"
                    variant="primary"
                    :label="editing ? t('smartPlaylists.save') : t('smartPlaylists.create')"
                    :disabled="saving"
                />
            </div>
        </form>
    </div>
</template>
