import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import FileChoiceGroup from '../FileChoiceGroup.vue'
import { fileChoices } from '@/admin/fileSelections'
import type { MediaFile } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'

const files: MediaFile[] = [
    { id: 'flac', kind: 'Audio', relativePath: 'A/Song.flac', size: 10 },
    { id: 'mp3', kind: 'Audio', relativePath: 'A/Song.mp3', size: 5 },
]

function mountGroup(disabled = false) {
    return mount(FileChoiceGroup, {
        props: {
            category: 'audio',
            labelledby: 'title',
            disabled,
            choices: fileChoices(
                {
                    state: 'Automatic',
                    activeFileId: 'flac',
                    automaticFileId: 'flac',
                    candidateFileIds: ['flac', 'mp3'],
                },
                files,
            ),
        },
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
}

describe('FileChoiceGroup', () => {
    it('is a radio group with one tab stop, an indicator and the variables of the checked option', () => {
        const wrapper = mountGroup()

        const group = wrapper.get('[data-part="file-choices"]')
        expect(group.attributes('role')).toBe('radiogroup')
        expect(group.attributes('aria-labelledby')).toBe('title')
        expect(group.element.firstElementChild?.getAttribute('data-part')).toBe('indicator')
        expect(group.attributes('style')).toContain('--wmp-item-count: 3')
        expect(group.attributes('style')).toContain('--wmp-active-index: 0')
        const options = wrapper.findAll('[data-part="file-choice"]')
        expect(options.map((option) => option.attributes('tabindex'))).toEqual(['0', '-1', '-1'])
        expect(options.map((option) => option.attributes('data-item'))).toEqual([
            'automatic',
            'file',
            'file',
        ])
        wrapper.unmount()
    })

    it('moves the focus with the arrow keys and selects only on click', async () => {
        const wrapper = mountGroup()
        const options = wrapper.findAll('[data-part="file-choice"]')

        await options[0]?.trigger('keydown', { key: 'ArrowDown' })
        expect(document.activeElement).toBe(options[1]?.element)
        expect(wrapper.emitted('choose')).toBeUndefined()
        expect(options[1]?.attributes('tabindex')).toBe('0')

        await options[2]?.trigger('click')
        await options[0]?.trigger('click')
        expect(wrapper.emitted('choose')).toEqual([['mp3']])
        wrapper.unmount()
    })

    it('does nothing while a change is saved', async () => {
        const wrapper = mountGroup(true)

        await wrapper.findAll('[data-part="file-choice"]')[1]?.trigger('click')

        expect(wrapper.get('[data-part="file-choices"]').attributes('aria-disabled')).toBe('true')
        expect(wrapper.emitted('choose')).toBeUndefined()
        wrapper.unmount()
    })
})
