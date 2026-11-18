<script setup lang="ts">
import { computed, onMounted, ref, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import type { Theme } from '@/api/generated/models'
import type { ThemePackageError } from '@/admin/themeErrors'
import AdminNav from '@/components/admin/AdminNav.vue'
import ThemeDeleteDialog from '@/components/admin/ThemeDeleteDialog.vue'
import ThemeErrorList from '@/components/admin/ThemeErrorList.vue'
import ThemeList from '@/components/admin/ThemeList.vue'
import BaseAlert, { type AlertTone } from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import { themeTemplateUrl } from '@/themes/custom'
import { themePackageAccept, useThemesStore, type ThemeUploadResult } from '@/stores/themes'

/**
 * Admin page of the uploaded themes: uploading a theme package (a ZIP file, see THEMING.md and the
 * template), the list of the uploaded themes with their previews, new versions and deleting. Every
 * profile can choose an uploaded theme on the settings page.
 */
const { t } = useI18n()
const store = useThemesStore()

const fileInput = ref<HTMLInputElement | null>(null)
/** The theme a picked file replaces; null for a new theme. */
const replacing = ref<Theme | null>(null)
const uploading = ref(false)
const busy = ref<string | null>(null)
const pendingDelete = ref<Theme | null>(null)
const dialogOpen = ref(false)
const message = ref<{ text: string; tone: AlertTone } | null>(null)
const errors = ref<readonly ThemePackageError[]>([])
const uploadTitleId = useId()
const themesTitleId = useId()

const working = computed(() => uploading.value || busy.value !== null)

function pick(theme: Theme | null): void {
    replacing.value = theme
    message.value = null
    errors.value = []
    if (fileInput.value) {
        // The same file can be picked again after it was fixed.
        fileInput.value.value = ''
        fileInput.value.click()
    }
}

function show(result: ThemeUploadResult): void {
    switch (result.status) {
        case 'installed':
            message.value = {
                text: t('adminThemes.installed', { name: result.theme.name ?? '' }),
                tone: 'success',
            }
            break
        case 'replaced':
            message.value = {
                text: t('adminThemes.replaced', { name: result.theme.name ?? '' }),
                tone: 'success',
            }
            break
        case 'invalid':
            message.value = { text: t('adminThemes.invalid'), tone: 'error' }
            errors.value = result.errors
            break
        default:
            message.value = { text: t(`adminThemes.uploadErrors.${result.status}`), tone: 'error' }
    }
}

async function upload(event: Event): Promise<void> {
    const file = (event.target as HTMLInputElement).files?.[0]
    if (!file) {
        return
    }

    const target = replacing.value
    uploading.value = true
    busy.value = target?.id ?? null
    const content = await file.arrayBuffer()
    const result = target?.id
        ? await store.replace(target.id, content)
        : await store.install(content)
    uploading.value = false
    busy.value = null
    replacing.value = null
    show(result)
}

function askDelete(theme: Theme): void {
    pendingDelete.value = theme
    dialogOpen.value = true
}

async function confirmDelete(): Promise<void> {
    const theme = pendingDelete.value
    if (!theme?.id) {
        return
    }

    busy.value = theme.id
    message.value = null
    errors.value = []
    const result = await store.remove(theme.id)
    busy.value = null
    dialogOpen.value = false
    pendingDelete.value = null
    if (result === 'deleted') {
        message.value = { text: t('adminThemes.deleted'), tone: 'success' }
    } else if (result === 'error') {
        message.value = { text: t('adminThemes.deleteError'), tone: 'error' }
    }
}

onMounted(() => store.load())
</script>

<template>
    <div
        data-part="view"
        data-view="admin-themes"
        :aria-busy="store.status === 'loading' || uploading || undefined"
    >
        <AdminNav />
        <section data-part="section" data-item="theme-upload" :aria-labelledby="uploadTitleId">
            <h2 :id="uploadTitleId" data-part="section-title">
                {{ t('adminThemes.uploadTitle') }}
            </h2>
            <p data-part="form-note">
                {{ t('adminThemes.uploadHint') }}
                <a data-part="link" data-item="theme-template" :href="themeTemplateUrl" download>{{
                    t('adminThemes.template')
                }}</a>
            </p>
            <div data-part="view-actions">
                <BaseButton
                    data-item="upload-theme"
                    variant="primary"
                    icon="upload"
                    :label="t('adminThemes.upload')"
                    :disabled="working"
                    @click="pick(null)"
                />
            </div>
            <input
                ref="fileInput"
                data-item="theme-file"
                type="file"
                :accept="themePackageAccept"
                hidden
                @change="upload"
            />
            <BaseAlert v-if="message" :tone="message.tone" :message="message.text" />
            <ThemeErrorList v-if="errors.length > 0" :errors="errors" />
        </section>
        <section data-part="section" data-item="themes" :aria-labelledby="themesTitleId">
            <h2 :id="themesTitleId" data-part="section-title">
                {{ t('adminThemes.themesTitle') }}
            </h2>
            <template v-if="store.status === 'error'">
                <BaseAlert tone="error" :message="t('adminThemes.loadError')" />
                <div data-part="view-actions">
                    <BaseButton
                        data-item="retry"
                        :label="t('library.retry')"
                        @click="store.load()"
                    />
                </div>
            </template>
            <ThemeList
                v-else-if="store.status !== 'loading'"
                :themes="store.items"
                :busy="busy"
                :disabled="working"
                @replace="pick"
                @delete="askDelete"
            />
        </section>
        <ThemeDeleteDialog
            v-model:open="dialogOpen"
            :theme="pendingDelete"
            :busy="busy !== null"
            @confirm="confirmDelete"
        />
    </div>
</template>
