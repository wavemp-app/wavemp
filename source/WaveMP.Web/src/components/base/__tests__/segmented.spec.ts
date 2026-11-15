import { describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import BaseSegmentedControl, { type SegmentOption } from '../BaseSegmentedControl.vue'
import BaseSegmentedField from '../BaseSegmentedField.vue'
import { nextSegmentIndex, segmentTabStop } from '../segmented'

const none = [false, false, false]

describe('nextSegmentIndex', () => {
    it('moves with the arrow keys and wraps around', () => {
        expect(nextSegmentIndex('ArrowRight', 0, none)).toBe(1)
        expect(nextSegmentIndex('ArrowDown', 2, none)).toBe(0)
        expect(nextSegmentIndex('ArrowLeft', 0, none)).toBe(2)
        expect(nextSegmentIndex('ArrowUp', 1, none)).toBe(0)
    })

    it('goes to the first and the last option with Home and End', () => {
        expect(nextSegmentIndex('Home', 2, none)).toBe(0)
        expect(nextSegmentIndex('End', 0, none)).toBe(2)
    })

    it('skips disabled options', () => {
        const disabled = [true, false, true, false]
        expect(nextSegmentIndex('ArrowRight', 1, disabled)).toBe(3)
        expect(nextSegmentIndex('ArrowRight', 3, disabled)).toBe(1)
        expect(nextSegmentIndex('ArrowLeft', 1, disabled)).toBe(3)
        expect(nextSegmentIndex('Home', 3, disabled)).toBe(1)
        expect(nextSegmentIndex('End', 1, [false, false, true])).toBe(1)
    })

    it('starts at the edge without a checked option', () => {
        expect(nextSegmentIndex('ArrowRight', -1, none)).toBe(0)
        expect(nextSegmentIndex('ArrowLeft', -1, none)).toBe(2)
    })

    it('does nothing for other keys and when all options are disabled', () => {
        expect(nextSegmentIndex('Enter', 0, none)).toBeNull()
        expect(nextSegmentIndex('a', 0, none)).toBeNull()
        expect(nextSegmentIndex('ArrowRight', 0, [true, true])).toBeNull()
        expect(nextSegmentIndex('ArrowRight', 0, [])).toBeNull()
    })
})

describe('segmentTabStop', () => {
    it('is the checked option, or the first enabled one', () => {
        expect(segmentTabStop(2, none)).toBe(2)
        expect(segmentTabStop(-1, none)).toBe(0)
        expect(segmentTabStop(0, [true, false, false])).toBe(1)
        expect(segmentTabStop(-1, [true, true])).toBe(0)
    })
})

const options: SegmentOption[] = [
    { value: 'Cover', item: 'cover', label: 'Cover', icon: 'view-cover' },
    { value: 'Video', item: 'video', label: 'Video', disabled: true },
    { value: 'Visualization', label: 'Visualization' },
]

function mountControl(props: Record<string, unknown> = {}) {
    return mount(BaseSegmentedControl, {
        props: { options, label: 'View', item: 'stage-view', modelValue: 'Cover', ...props },
        attachTo: document.body,
    })
}

describe('BaseSegmentedControl', () => {
    it('is a radio group with one checked segment and an indicator', () => {
        const wrapper = mountControl()
        const group = wrapper.get('[data-part="segmented-control"]')

        expect(group.attributes('role')).toBe('radiogroup')
        expect(group.attributes('aria-label')).toBe('View')
        expect(group.attributes('data-item')).toBe('stage-view')
        expect(group.element.firstElementChild?.getAttribute('data-part')).toBe('indicator')
        expect(group.get('[data-part="indicator"]').attributes('aria-hidden')).toBe('true')

        const segments = wrapper.findAll('[data-part="segment"]')
        expect(
            segments.map((segment) => [
                segment.attributes('role'),
                segment.attributes('data-item'),
                segment.attributes('aria-checked'),
                segment.attributes('tabindex'),
                segment.attributes('data-variant'),
            ]),
        ).toEqual([
            ['radio', 'cover', 'true', '0', 'default'],
            ['radio', 'video', 'false', '-1', 'default'],
            ['radio', 'Visualization', 'false', '-1', 'default'],
        ])
        expect(segments[0]!.find('[data-part="icon"]').exists()).toBe(true)
        expect(segments[0]!.get('[data-part="label"]').text()).toBe('Cover')
        expect(segments[1]!.attributes('disabled')).toBeDefined()
        wrapper.unmount()
    })

    it('tells the theme the position of the checked segment', async () => {
        const wrapper = mountControl()
        const style = () => wrapper.get('[data-part="segmented-control"]').element as HTMLElement

        expect(style().style.getPropertyValue('--wmp-item-count')).toBe('3')
        expect(style().style.getPropertyValue('--wmp-active-index')).toBe('0')
        // jsdom has no layout, so the measured box is empty.
        expect(style().style.getPropertyValue('--wmp-indicator-x')).toBe('0px')
        expect(style().style.getPropertyValue('--wmp-indicator-width')).toBe('0px')

        await wrapper.setProps({ modelValue: 'Visualization' })
        expect(style().style.getPropertyValue('--wmp-active-index')).toBe('2')

        await wrapper.setProps({ modelValue: 'Unknown' })
        expect(style().style.getPropertyValue('--wmp-active-index')).toBe('-1')
        expect(wrapper.get('[data-part="segment"]').attributes('tabindex')).toBe('0')
        wrapper.unmount()
    })

    it('checks a segment on a click, but not a disabled one', async () => {
        const wrapper = mountControl()

        await wrapper.get('[data-item="Visualization"]').trigger('click')
        await wrapper.get('[data-item="video"]').trigger('click')

        expect(wrapper.emitted('update:modelValue')).toEqual([['Visualization']])
        wrapper.unmount()
    })

    it('checks and focuses the next enabled segment with the arrow keys', async () => {
        const wrapper = mountControl()
        const update = vi.fn((value: string) => wrapper.setProps({ modelValue: value }))
        await wrapper.setProps({ 'onUpdate:modelValue': update })

        await wrapper.get('[data-item="cover"]').trigger('keydown', { key: 'ArrowRight' })
        await nextTick()
        expect(update).toHaveBeenLastCalledWith('Visualization')
        expect(document.activeElement).toBe(wrapper.get('[data-item="Visualization"]').element)

        await wrapper.get('[data-item="Visualization"]').trigger('keydown', { key: 'Home' })
        await nextTick()
        expect(update).toHaveBeenLastCalledWith('Cover')
        expect(document.activeElement).toBe(wrapper.get('[data-item="cover"]').element)

        await wrapper.get('[data-item="cover"]').trigger('keydown', { key: 'Enter' })
        expect(update).toHaveBeenCalledTimes(2)
        wrapper.unmount()
    })

    it('can be disabled as a whole', async () => {
        const wrapper = mountControl({ disabled: true })

        expect(wrapper.get('[data-part="segmented-control"]').attributes('aria-disabled')).toBe(
            'true',
        )
        expect(
            wrapper
                .findAll('[data-part="segment"]')
                .every((segment) => segment.attributes('disabled') !== undefined),
        ).toBe(true)
        await wrapper.get('[data-item="cover"]').trigger('keydown', { key: 'ArrowRight' })
        expect(wrapper.emitted('update:modelValue')).toBeUndefined()
        wrapper.unmount()
    })
})

describe('BaseSegmentedField', () => {
    it('names the group by its label and describes it by its hint and error', async () => {
        const wrapper = mount(BaseSegmentedField, {
            props: {
                label: 'Mode',
                options,
                item: 'mode',
                hint: 'Applies at once.',
                modelValue: 'Cover',
            },
        })
        const field = wrapper.get('[data-part="field"]')
        const group = field.get('[data-part="segmented-control"]')
        const hint = field.get('[data-part="field-hint"]')

        expect(group.attributes('aria-labelledby')).toBe(
            field.get('[data-part="label"]').attributes('id'),
        )
        expect(group.attributes('aria-label')).toBeUndefined()
        expect(group.attributes('aria-describedby')).toBe(hint.attributes('id'))
        expect(group.attributes('data-item')).toBe('mode')

        await wrapper.setProps({ error: 'Required' })
        expect(field.attributes('data-state')).toBe('invalid')
        expect(group.attributes('aria-invalid')).toBe('true')
        expect(group.attributes('aria-describedby')).toBe(
            `${field.get('[data-part="field-error"]').attributes('id')} ${hint.attributes('id')}`,
        )

        await wrapper.get('[data-item="Visualization"]').trigger('click')
        expect(wrapper.emitted('update:modelValue')).toEqual([['Visualization']])
    })
})
