import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PinEntry from '../PinEntry.vue'
import PinPad from '../PinPad.vue'
import { createWaveMpI18n } from '@/i18n'

const global = { plugins: [createWaveMpI18n('en')] }
const anna = { id: 'anna', name: 'Anna', initials: 'A', color: '#2e7d32', isBuiltIn: false }

function mountPad(modelValue = '') {
    const wrapper = mount(PinPad, {
        props: {
            label: 'PIN',
            modelValue,
            'onUpdate:modelValue': (value: string) => wrapper.setProps({ modelValue: value }),
        },
        global,
    })
    return wrapper
}

function key(wrapper: ReturnType<typeof mountPad>, name: string) {
    return wrapper.get(`[data-part="pin-key"][data-key="${name}"]`)
}

describe('PinPad', () => {
    it('shows the keys of a phone keypad', () => {
        const wrapper = mountPad()

        const keys = wrapper.findAll('[data-part="pin-key"]')
        expect(keys.map((item) => item.attributes('data-key'))).toEqual([
            '1',
            '2',
            '3',
            '4',
            '5',
            '6',
            '7',
            '8',
            '9',
            'delete',
            '0',
            'submit',
        ])
        expect(key(wrapper, '5').text()).toBe('5')
        expect(key(wrapper, 'delete').attributes('title')).toBe('Delete the last digit')
        expect(key(wrapper, 'delete').get('[data-part="icon"]').attributes('data-icon')).toBe(
            'pin-delete',
        )
        expect(key(wrapper, 'submit').attributes('type')).toBe('submit')
        expect(key(wrapper, '1').attributes('type')).toBe('button')
        const field = wrapper.get('[data-part="pin-field"]')
        expect(field.attributes('type')).toBe('password')
        expect(field.attributes('aria-label')).toBe('PIN')
    })

    it('enters and deletes digits with the keys', async () => {
        const wrapper = mountPad()

        await key(wrapper, '4').trigger('click')
        await key(wrapper, '8').trigger('click')
        await key(wrapper, '2').trigger('click')
        expect(wrapper.props('modelValue')).toBe('482')
        expect(wrapper.findAll('[data-part="pin-dot"]')).toHaveLength(3)
        expect(wrapper.get('[data-part="pin-dots"]').attributes('data-count')).toBe('3')

        await key(wrapper, 'delete').trigger('click')
        expect(wrapper.props('modelValue')).toBe('48')
    })

    it('enables submit from four digits and delete with at least one', async () => {
        const wrapper = mountPad()
        expect(key(wrapper, 'submit').attributes('disabled')).toBeDefined()
        expect(key(wrapper, 'delete').attributes('disabled')).toBeDefined()

        await wrapper.setProps({ modelValue: '123' })
        expect(key(wrapper, 'submit').attributes('disabled')).toBeDefined()
        expect(key(wrapper, 'delete').attributes('disabled')).toBeUndefined()

        await wrapper.setProps({ modelValue: '1234' })
        expect(key(wrapper, 'submit').attributes('disabled')).toBeUndefined()
    })

    it('stops at eight digits', async () => {
        const wrapper = mountPad('12345678')

        await key(wrapper, '9').trigger('click')

        expect(wrapper.props('modelValue')).toBe('12345678')
    })

    it('takes only digits typed into the field', async () => {
        const wrapper = mountPad()

        await wrapper.get('[data-part="pin-field"]').setValue('12a3')

        expect(wrapper.props('modelValue')).toBe('123')
    })

    it('takes digits and Backspace while a key has the focus', async () => {
        const wrapper = mountPad('1')

        await key(wrapper, '5').trigger('keydown', { key: '7' })
        expect(wrapper.props('modelValue')).toBe('17')

        await key(wrapper, '5').trigger('keydown', { key: 'Backspace' })
        expect(wrapper.props('modelValue')).toBe('1')
    })

    it('disables every key while disabled', () => {
        const wrapper = mount(PinPad, {
            props: { label: 'PIN', modelValue: '1234', disabled: true },
            global,
        })

        expect(wrapper.get('[data-part="pin-pad"]').attributes('data-state')).toBe('disabled')
        for (const item of wrapper.findAll('[data-part="pin-key"]')) {
            expect(item.attributes('disabled')).toBeDefined()
        }
    })
})

describe('PinEntry', () => {
    it('asks for the PIN of the profile and passes it on', async () => {
        const wrapper = mount(PinEntry, { props: { profile: anna }, global })

        expect(wrapper.get('[data-part="pin-entry-title"]').text()).toBe('Enter the PIN of Anna')
        for (const digit of ['2', '5', '8', '0']) {
            await wrapper.get(`[data-key="${digit}"]`).trigger('click')
        }
        await wrapper.get('[data-part="pin-entry"]').trigger('submit')

        expect(wrapper.emitted('submit')).toEqual([['2580']])
        // The entered PIN is not kept.
        expect(wrapper.findAll('[data-part="pin-dot"]')).toHaveLength(0)
    })

    it('does not submit an incomplete PIN', async () => {
        const wrapper = mount(PinEntry, { props: { profile: anna }, global })

        await wrapper.get('[data-key="1"]').trigger('click')
        await wrapper.get('[data-part="pin-entry"]').trigger('submit')

        expect(wrapper.emitted('submit')).toBeUndefined()
    })

    it('shows an error and goes back to the profiles', async () => {
        const wrapper = mount(PinEntry, {
            props: { profile: anna, error: 'Wrong PIN. Please try again.' },
            global,
        })

        expect(wrapper.get('[data-part="pin-entry"]').attributes('data-state')).toBe('error')
        expect(wrapper.get('[data-part="alert"]').text()).toBe('Wrong PIN. Please try again.')

        await wrapper.get('[data-item="back"]').trigger('click')

        expect(wrapper.emitted('cancel')).toHaveLength(1)
    })
})
