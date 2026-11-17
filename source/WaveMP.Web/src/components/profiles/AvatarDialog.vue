<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import AvatarCropper from './AvatarCropper.vue'
import AvatarPresetPicker from './AvatarPresetPicker.vue'
import ProfileAvatar from './ProfileAvatar.vue'
import { ProblemCodeObject } from '@/api/generated/models'
import { problemCodeOf } from '@/api/problems'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import BaseDialog from '@/components/base/BaseDialog.vue'
import BaseSegmentedControl, {
    type SegmentOption,
} from '@/components/base/BaseSegmentedControl.vue'
import {
    chooseAvatarPreset,
    listAvatarPresets,
    removeAvatar,
    uploadAvatar,
    type AvatarTarget,
} from '@/profiles/avatarApi'
import {
    avatarUploadProblem,
    avatarUploadTypes,
    cropRectangle,
    initialCrop,
    type AvatarCrop,
} from '@/profiles/avatarCrop'
import { drawCrop, loadPicture, releasePicture, type LoadedPicture } from '@/profiles/avatarImage'

/** The profile whose avatar changes, as the dialog shows it. */
export interface AvatarDialogProfile {
    readonly id: string
    readonly name: string
    readonly initials: string
    readonly color: string
    /** Version of the current avatar; null without one. */
    readonly avatarVersion: string | null
    /** Id of the current built-in avatar; null for an uploaded picture or none. */
    readonly avatarPresetId: string | null
}

type AvatarMode = 'preset' | 'upload'

/**
 * Changes the avatar of a profile: a built-in avatar or an own picture, which is cropped to a
 * square before the upload, or no avatar (the initials). The own profile uses the target "me",
 * admins change other profiles by their id. Emits saved after every change.
 */
const props = defineProps<{
    target: AvatarTarget
    profile: AvatarDialogProfile | null
}>()

const emit = defineEmits<{
    saved: []
}>()

const open = defineModel<boolean>('open', { default: false })

const { t } = useI18n()

const mode = ref<AvatarMode>('preset')
const presets = ref<string[]>([])
const presetsFailed = ref(false)
const chosenPreset = ref<string | null>(null)
const picture = ref<LoadedPicture | null>(null)
const crop = ref<AvatarCrop>({ x: 0, y: 0, zoom: 1 })
const fileInput = ref<HTMLInputElement | null>(null)
const busy = ref(false)
const error = ref<string | null>(null)

const acceptedTypes = avatarUploadTypes.join(',')

const modeOptions = computed<SegmentOption[]>(() => [
    { value: 'preset', item: 'preset', label: t('avatars.modes.preset') },
    { value: 'upload', item: 'upload', label: t('avatars.modes.upload') },
])

const hasAvatar = computed(() => !!props.profile?.avatarVersion)
const canSave = computed(() =>
    mode.value === 'preset'
        ? !!chosenPreset.value && chosenPreset.value !== props.profile?.avatarPresetId
        : !!picture.value,
)

function setPicture(next: LoadedPicture | null): void {
    releasePicture(picture.value)
    picture.value = next
    crop.value = next ? initialCrop(next) : { x: 0, y: 0, zoom: 1 }
}

async function loadPresets(): Promise<void> {
    presetsFailed.value = false
    try {
        presets.value = await listAvatarPresets()
    } catch {
        presetsFailed.value = true
    }
}

function reset(): void {
    error.value = null
    busy.value = false
    setPicture(null)
    const profile = props.profile
    chosenPreset.value = profile?.avatarPresetId ?? null
    mode.value = profile?.avatarVersion && !profile.avatarPresetId ? 'upload' : 'preset'
    if (presets.value.length === 0) {
        void loadPresets()
    }
}

watch(open, (isOpen) => {
    if (isOpen) {
        reset()
    } else {
        setPicture(null)
    }
})

onBeforeUnmount(() => setPicture(null))

function chooseFile(): void {
    fileInput.value?.click()
}

async function onFileChosen(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement
    const file = input.files?.[0]
    input.value = ''
    if (!file) {
        return
    }

    error.value = null
    const problem = avatarUploadProblem(file)
    if (problem) {
        error.value = t(problem === 'type' ? 'avatars.errors.type' : 'avatars.errors.size')
        return
    }

    try {
        setPicture(await loadPicture(file))
    } catch {
        setPicture(null)
        error.value = t('avatars.errors.unreadable')
    }
}

function failureMessage(failure: unknown): string {
    switch (problemCodeOf(failure)) {
        case ProblemCodeObject.AvatarUnsupported_format:
            return t('avatars.errors.type')
        case ProblemCodeObject.AvatarToo_large:
            return t('avatars.errors.size')
        case ProblemCodeObject.AvatarUnreadable:
            return t('avatars.errors.unreadable')
        case ProblemCodeObject.UserNot_found:
            return t('avatars.errors.notFound')
        default:
            return t('avatars.errors.saveFailed')
    }
}

async function run(action: () => Promise<unknown>): Promise<void> {
    if (busy.value) {
        return
    }

    busy.value = true
    error.value = null
    try {
        await action()
        emit('saved')
        open.value = false
    } catch (failure) {
        error.value = failureMessage(failure)
    } finally {
        busy.value = false
    }
}

function save(): Promise<void> {
    const loaded = picture.value
    const preset = chosenPreset.value
    if (mode.value === 'upload' && loaded) {
        return run(async () => {
            const blob = await drawCrop(loaded, cropRectangle(crop.value, loaded))
            await uploadAvatar(props.target, blob, 'avatar.png')
        })
    }

    if (mode.value === 'preset' && preset) {
        return run(() => chooseAvatarPreset(props.target, preset))
    }

    return Promise.resolve()
}

function remove(): Promise<void> {
    return run(() => removeAvatar(props.target))
}
</script>

<template>
    <BaseDialog v-model:open="open" data-item="avatar" :title="t('avatars.title')">
        <div v-if="profile && open" data-part="avatar-editor" :data-state="mode">
            <ProfileAvatar
                :initials="profile.initials"
                :color="profile.color"
                :user-id="profile.id"
                :avatar-version="profile.avatarVersion"
            />
            <BaseSegmentedControl
                v-model="mode"
                item="avatar-mode"
                :options="modeOptions"
                :label="t('avatars.mode')"
                :disabled="busy"
            />
            <BaseAlert v-if="error" tone="error" :message="error" />
            <template v-if="mode === 'preset'">
                <template v-if="presetsFailed">
                    <BaseAlert tone="error" :message="t('avatars.errors.presetsFailed')" />
                    <BaseButton
                        data-item="retry"
                        :label="t('library.retry')"
                        @click="loadPresets"
                    />
                </template>
                <AvatarPresetPicker
                    v-else
                    v-model="chosenPreset"
                    :label="t('avatars.presetList')"
                    :presets="presets"
                    :disabled="busy"
                />
            </template>
            <template v-else>
                <AvatarCropper
                    v-if="picture"
                    v-model="crop"
                    :src="picture.url"
                    :picture="picture"
                    :disabled="busy"
                />
                <p v-else data-part="form-note">{{ t('avatars.uploadHint') }}</p>
                <BaseButton
                    data-item="choose-file"
                    icon="change-avatar"
                    :label="picture ? t('avatars.chooseOther') : t('avatars.choose')"
                    :disabled="busy"
                    @click="chooseFile"
                />
                <input
                    ref="fileInput"
                    type="file"
                    :accept="acceptedTypes"
                    hidden
                    @change="onFileChosen"
                />
            </template>
        </div>
        <template #footer>
            <BaseButton
                v-if="hasAvatar"
                data-item="remove-avatar"
                icon="delete"
                :label="t('avatars.remove')"
                :disabled="busy"
                @click="remove"
            />
            <BaseButton :label="t('actions.cancel')" :disabled="busy" @click="open = false" />
            <BaseButton
                variant="primary"
                data-item="save-avatar"
                :label="t('avatars.save')"
                :disabled="busy || !canSave"
                @click="save"
            />
        </template>
    </BaseDialog>
</template>
