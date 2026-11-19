import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ShortcutOverview from '../ShortcutOverview.vue'
import { createWaveMpI18n } from '@/i18n'
import { defaultShortcuts } from '@/shortcuts/defaults'

function mountOverview(locale: 'en' | 'de' = 'en', apple = false) {
    return mount(ShortcutOverview, {
        props: { shortcuts: defaultShortcuts, apple, open: true },
        global: { plugins: [createWaveMpI18n(locale)] },
    })
}

describe('ShortcutOverview', () => {
    it('lists every shortcut by group', () => {
        const wrapper = mountOverview()

        expect(wrapper.get('[data-part="dialog"]').attributes('data-item')).toBe('shortcuts')
        expect(wrapper.get('[data-part="dialog-title"]').text()).toBe('Keyboard shortcuts')
        const groups = wrapper.findAll('[data-part="shortcut-group"]')
        expect(groups.map((group) => group.attributes('data-item'))).toEqual([
            'playback',
            'volume',
            'view',
            'general',
        ])
        expect(
            groups.map((group) => group.get('[data-part="shortcut-group-title"]').text()),
        ).toEqual(['Playback', 'Volume', 'View', 'General'])
        expect(
            wrapper
                .findAll('[data-part="shortcut-item"]')
                .map((item) => item.attributes('data-item')),
        ).toEqual(defaultShortcuts.map((shortcut) => shortcut.id))
        expect(wrapper.get('[data-part="shortcut-hint"]').text()).toContain('type in a field')
        // Every description is translated, none shows its message key.
        const descriptions = wrapper
            .findAll('[data-part="shortcut-description"]')
            .map((item) => item.text())
        expect(descriptions.filter((text) => text.startsWith('shortcuts.'))).toEqual([])
    })

    it('shows the keys of a shortcut as key caps', () => {
        const wrapper = mountOverview()

        const previous = wrapper.get('[data-part="shortcut-item"][data-item="previous"]')
        expect(previous.get('[data-part="shortcut-description"]').text()).toBe('Previous song')
        const keys = previous.findAll('[data-part="shortcut-key"]')
        expect(keys.map((key) => key.attributes('data-key'))).toEqual(['mod', 'arrowleft'])
        expect(keys.map((key) => key.text())).toEqual(['Ctrl', '←'])
        expect(previous.get('[data-part="shortcut-key-separator"]').attributes('aria-hidden')).toBe(
            'true',
        )

        const space = wrapper.get('[data-item="play-pause"] [data-part="shortcut-key"]')
        expect(space.text()).toBe('Space')
        expect(
            wrapper
                .get(
                    '[data-part="shortcut-item"][data-item="shortcuts"] [data-part="shortcut-key"]',
                )
                .text(),
        ).toBe('?')
        expect(wrapper.get('[data-item="mute"] [data-part="shortcut-key"]').text()).toBe('M')
    })

    it('names the Command key on Apple devices and translates the texts', () => {
        const wrapper = mountOverview('de', true)

        expect(wrapper.get('[data-part="dialog-title"]').text()).toBe('Tastenkürzel')
        const search = wrapper.get('[data-item="search"]')
        expect(search.get('[data-part="shortcut-description"]').text()).toBe(
            'Bibliothek durchsuchen',
        )
        expect(search.findAll('[data-part="shortcut-key"]').map((key) => key.text())).toEqual([
            '⌘',
            'F',
        ])
        expect(wrapper.get('[data-item="play-pause"] [data-part="shortcut-key"]').text()).toBe(
            'Leertaste',
        )
    })
})
