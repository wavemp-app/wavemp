import { defineStore } from 'pinia'
import { ref } from 'vue'
import { apiClient } from '@/api/client'
import { ProblemCodeObject, type Theme } from '@/api/generated/models'
import { fetchAllPages } from '@/api/paging'
import { problemCodeOf } from '@/api/problems'
import { themeErrorsOf, type ThemePackageError } from '@/admin/themeErrors'
import { toThemeDefinitions, writeCustomThemes } from '@/themes/custom'
import { customThemes } from '@/themes/themes'

export type ThemesStatus = 'idle' | 'loading' | 'ready' | 'error'

export type ThemeUploadResult =
    | { readonly status: 'installed' | 'replaced'; readonly theme: Theme }
    | { readonly status: 'invalid'; readonly errors: readonly ThemePackageError[] }
    | {
          readonly status:
              'tooLarge' | 'unsupportedFormat' | 'alreadyInstalled' | 'notFound' | 'error'
      }

export type ThemeDeleteResult = 'deleted' | 'notFound' | 'error'

/** Content type of a theme package. */
export const themePackageType = 'application/zip'

/** File types the file picker of an upload offers. */
export const themePackageAccept = '.zip,application/zip,application/x-zip-compressed'

function uploadProblem(error: unknown): ThemeUploadResult {
    switch (problemCodeOf(error)) {
        case ProblemCodeObject.ThemeInvalid:
            return { status: 'invalid', errors: themeErrorsOf(error) }
        case ProblemCodeObject.ThemeToo_large:
            return { status: 'tooLarge' }
        case ProblemCodeObject.ThemeUnsupported_format:
            return { status: 'unsupportedFormat' }
        case ProblemCodeObject.ThemeAlready_installed:
            return { status: 'alreadyInstalled' }
        case ProblemCodeObject.ThemeNot_found:
            return { status: 'notFound' }
        default:
            return { status: 'error' }
    }
}

/**
 * The themes admins uploaded. Every profile loads the list (load), which also fills the theme
 * registry (customThemes) and remembers it for the first paint; admins install, replace and
 * delete themes on the admin page.
 */
export const useThemesStore = defineStore('themes', () => {
    const items = ref<Theme[]>([])
    const status = ref<ThemesStatus>('idle')

    function publish(list: Theme[]): void {
        items.value = list
        const definitions = toThemeDefinitions(list)
        customThemes.value = definitions
        writeCustomThemes(definitions)
    }

    async function load(): Promise<void> {
        if (status.value !== 'ready') {
            status.value = 'loading'
        }

        try {
            publish(
                await fetchAllPages((offset, limit) =>
                    apiClient.api.v1.themes.get({ queryParameters: { offset, limit } }),
                ),
            )
            status.value = 'ready'
        } catch {
            status.value = 'error'
        }
    }

    /** Installs a theme package (a ZIP file) as new theme. */
    async function install(content: ArrayBuffer): Promise<ThemeUploadResult> {
        let result: ThemeUploadResult
        try {
            const theme = await apiClient.api.v1.themes.post(content)
            result = theme ? { status: 'installed', theme } : { status: 'error' }
        } catch (error) {
            result = uploadProblem(error)
        }

        await load()
        return result
    }

    /** Replaces an uploaded theme with a new version of its package. */
    async function replace(id: string, content: ArrayBuffer): Promise<ThemeUploadResult> {
        let result: ThemeUploadResult
        try {
            const theme = await apiClient.api.v1.themes.byId(id).put(content)
            result = theme ? { status: 'replaced', theme } : { status: 'error' }
        } catch (error) {
            result = uploadProblem(error)
        }

        await load()
        return result
    }

    /** Deletes an uploaded theme; profiles that chose it get the default theme. */
    async function remove(id: string): Promise<ThemeDeleteResult> {
        let result: ThemeDeleteResult
        try {
            await apiClient.api.v1.themes.byId(id).delete()
            result = 'deleted'
        } catch (error) {
            result =
                problemCodeOf(error) === ProblemCodeObject.ThemeNot_found ? 'notFound' : 'error'
        }

        await load()
        return result
    }

    return { items, status, load, install, replace, remove }
})
