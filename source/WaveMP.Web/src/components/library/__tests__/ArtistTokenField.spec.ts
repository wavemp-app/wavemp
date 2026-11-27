import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import type { Artist } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import type { ArtistEntry } from '@/library/artists'

const api = vi.hoisted(() => ({ listArtists: vi.fn() }))

vi.mock('@/library/api', () => api)

const ArtistTokenField = (await import('../ArtistTokenField.vue')).default

const library: Artist[] = [
    { id: 'kflay', name: 'K.Flay' },
    { id: 'kda', name: 'K/DA' },
]

function mountField(entries: ArtistEntry[] = [], props: Record<string, unknown> = {}) {
    const model = { value: entries }
    const wrapper = mount(ArtistTokenField, {
        props: {
            label: 'Artists',
            roles: true,
            modelValue: entries,
            'onUpdate:modelValue': (value: ArtistEntry[]) => {
                model.value = value
                void wrapper.setProps({ modelValue: value })
            },
            ...props,
        },
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    return { wrapper, model }
}

type Wrapper = ReturnType<typeof mountField>['wrapper']

function input(wrapper: Wrapper) {
    return wrapper.get('input[role="combobox"]')
}

async function type(wrapper: Wrapper, text: string) {
    await input(wrapper).trigger('focus')
    await input(wrapper).setValue(text)
    await flushPromises()
}

async function press(wrapper: Wrapper, key: string) {
    await input(wrapper).trigger('keydown', { key })
    await flushPromises()
}

function names(wrapper: Wrapper): string[] {
    return wrapper
        .findAll('[data-part="artist-token"] [data-field="name"]')
        .map((chip) => chip.text())
}

function options(wrapper: Wrapper): string[] {
    return wrapper.findAll('[data-part="artist-suggestion"]').map((option) => option.text())
}

describe('ArtistTokenField', () => {
    beforeEach(() => {
        api.listArtists.mockReset()
        api.listArtists.mockResolvedValue({ items: library })
    })

    it('is a labelled combobox that searches the library on every keystroke', async () => {
        const { wrapper } = mountField()

        await type(wrapper, 'k')
        await type(wrapper, 'kfl')

        expect(api.listArtists).toHaveBeenCalledTimes(2)
        expect(api.listArtists).toHaveBeenLastCalledWith({
            q: 'kfl',
            limit: 8,
            sort: 'Relevance',
            direction: 'Desc',
        })
        const combobox = input(wrapper)
        const label = wrapper.get('[data-part="field"] > [data-part="label"]')
        expect(combobox.attributes('aria-labelledby')).toBe(label.attributes('id'))
        expect(combobox.attributes('aria-expanded')).toBe('true')
        const listbox = wrapper.get('[role="listbox"]')
        expect(combobox.attributes('aria-controls')).toBe(listbox.attributes('id'))
        expect(options(wrapper)).toEqual(['K.Flay', 'K/DA', 'Add “kfl” as a new artist'])
        expect(wrapper.get('[data-part="artist-tokens"]').attributes('data-state')).toContain(
            'open',
        )
        wrapper.unmount()
    })

    it('adds the chosen match with the arrow keys and Enter', async () => {
        const { wrapper, model } = mountField()

        await type(wrapper, 'k')
        await press(wrapper, 'ArrowDown')
        await press(wrapper, 'ArrowDown')
        const active = wrapper.findAll('[role="option"]')[1]!
        expect(active.attributes('aria-selected')).toBe('true')
        expect(input(wrapper).attributes('aria-activedescendant')).toBe(active.attributes('id'))
        await press(wrapper, 'Enter')

        expect(model.value).toEqual([{ name: 'K/DA', role: 'Main', id: 'kda' }])
        expect(names(wrapper)).toEqual(['K/DA'])
        expect((input(wrapper).element as HTMLInputElement).value).toBe('')
        expect(wrapper.get('[data-part="artist-tokens-status"]').text()).toBe('K/DA added')
        wrapper.unmount()
    })

    it('adds the typed text as a new artist, but the spelling of the library for the same name', async () => {
        const { wrapper, model } = mountField()

        await type(wrapper, 'kflay')
        await press(wrapper, 'Enter')
        await type(wrapper, '  Earth, Wind  & Fire ')
        api.listArtists.mockResolvedValue({ items: [] })
        await type(wrapper, '  Earth, Wind  & Fire ')
        await press(wrapper, 'Enter')

        expect(model.value).toEqual([
            { name: 'K.Flay', role: 'Main', id: 'kflay' },
            { name: 'Earth, Wind & Fire', role: 'Main' },
        ])
        expect(
            wrapper
                .findAll('[data-part="artist-token"]')
                .map((chip) => chip.attributes('data-state')),
        ).toEqual(['known', 'new'])
        wrapper.unmount()
    })

    it('never splits at commas, "&" or slashes and adds a name only once', async () => {
        api.listArtists.mockResolvedValue({ items: [] })
        const { wrapper, model } = mountField([{ name: 'AC/DC', role: 'Main' }])

        await type(wrapper, 'ac dc')
        expect(options(wrapper)).toEqual([])
        await press(wrapper, 'Enter')
        await type(wrapper, 'Simon & Garfunkel, Live')
        await press(wrapper, 'Enter')

        expect(model.value.map((entry) => entry.name)).toEqual(['AC/DC', 'Simon & Garfunkel, Live'])
        wrapper.unmount()
    })

    it('hides matches already in the list and leaves Enter to the form when the input is empty', async () => {
        const { wrapper } = mountField([{ name: 'K.Flay', role: 'Main', id: 'kflay' }])

        await type(wrapper, 'k')
        expect(options(wrapper)).toEqual(['K/DA', 'Add “k” as a new artist'])

        await type(wrapper, '')
        const event = new KeyboardEvent('keydown', { key: 'Enter', cancelable: true })
        input(wrapper).element.dispatchEvent(event)
        expect(event.defaultPrevented).toBe(false)
        wrapper.unmount()
    })

    it('closes the suggestions with Escape without closing the dialog around it', async () => {
        const { wrapper } = mountField()
        const outer = vi.fn()
        wrapper.element.parentElement?.addEventListener('keydown', outer)

        await type(wrapper, 'k')
        await press(wrapper, 'Escape')

        expect(outer).not.toHaveBeenCalled()
        expect((input(wrapper).element as HTMLInputElement).value).toBe('')
        expect(input(wrapper).attributes('aria-expanded')).toBe('false')
        wrapper.unmount()
    })

    it('toggles the role of a song artist and removes a chip with its button', async () => {
        const { wrapper, model } = mountField([
            { name: 'Bob Sinclar', role: 'Main' },
            { name: 'K.Flay', role: 'Main' },
        ])

        const role = wrapper.findAll('[data-part="artist-role"]')[1]!
        expect(role.attributes('aria-pressed')).toBe('false')
        expect(role.attributes('aria-label')).toBe('K.Flay as featured artist')
        await role.trigger('click')
        expect(model.value[1]).toEqual({ name: 'K.Flay', role: 'Featured' })
        expect(wrapper.findAll('[data-part="artist-token"]')[1]!.attributes('data-role')).toBe(
            'featured',
        )
        expect(wrapper.findAll('[data-part="artist-role"]')[1]!.attributes('aria-pressed')).toBe(
            'true',
        )

        await wrapper.findAll('[data-item="remove"]')[0]!.trigger('click')
        expect(model.value).toEqual([{ name: 'K.Flay', role: 'Featured' }])
        expect(wrapper.get('[data-part="artist-tokens-status"]').text()).toBe('Bob Sinclar removed')
        wrapper.unmount()
    })

    it('moves a chip with the arrow keys on its handle and keeps the focus on it', async () => {
        const { wrapper, model } = mountField([
            { name: 'A', role: 'Main' },
            { name: 'B', role: 'Main' },
            { name: 'C', role: 'Main' },
        ])

        const handle = wrapper.findAll('[data-part="drag-handle"]')[0]!
        expect(handle.element.tagName).toBe('BUTTON')
        expect(handle.text()).toBe('Move A, position 1 of 3')
        await handle.trigger('keydown', { key: 'ArrowDown' })
        await flushPromises()
        expect(model.value.map((entry) => entry.name)).toEqual(['B', 'A', 'C'])
        expect(document.activeElement).toBe(
            wrapper.findAll('[data-part="drag-handle"]')[1]!.element,
        )

        await wrapper.findAll('[data-part="drag-handle"]')[1]!.trigger('keydown', { key: 'End' })
        expect(model.value.map((entry) => entry.name)).toEqual(['B', 'C', 'A'])
        expect(wrapper.get('[data-part="artist-tokens-status"]').text()).toBe(
            'A moved to position 3 of 3',
        )
        wrapper.unmount()
    })

    it('goes from an empty input to the last chip with Backspace', async () => {
        const { wrapper, model } = mountField([{ name: 'A', role: 'Main' }])

        await input(wrapper).trigger('focus')
        await press(wrapper, 'Backspace')

        expect(document.activeElement).toBe(wrapper.get('[data-item="remove"]').element)
        expect(model.value).toHaveLength(1)
        wrapper.unmount()
    })

    it('has no roles for album artists, and stops at the most artists', async () => {
        const { wrapper } = mountField([{ name: 'A', role: 'Main' }], { roles: false, max: 1 })

        expect(wrapper.find('[data-part="artist-role"]').exists()).toBe(false)
        expect(input(wrapper).attributes('disabled')).toBeDefined()
        expect(wrapper.get('[data-part="field"]').attributes('data-state')).toBe('full')
        // A single artist has nothing to move.
        expect(wrapper.get('[data-part="drag-handle"]').attributes('disabled')).toBeDefined()
        wrapper.unmount()
    })

    it('is disabled with the form and marks an error', async () => {
        const { wrapper } = mountField([{ name: 'A', role: 'Main' }], {
            disabled: true,
            error: 'Too many artists.',
        })

        const field = wrapper.get('[data-part="field"]')
        expect(field.attributes('data-state')).toBe('invalid disabled')
        expect(input(wrapper).attributes('aria-invalid')).toBe('true')
        expect(wrapper.get('[data-part="field-error"]').text()).toBe('Too many artists.')
        expect(
            wrapper
                .findAll('button')
                .every((button) => button.attributes('disabled') !== undefined),
        ).toBe(true)
        wrapper.unmount()
    })

    it('still offers the typed text when the search fails', async () => {
        api.listArtists.mockRejectedValue(new Error('offline'))
        const { wrapper } = mountField()

        await type(wrapper, 'Beyoncé')

        expect(options(wrapper)).toEqual(['Add “Beyoncé” as a new artist'])
        wrapper.unmount()
    })
})
