import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { h } from 'vue'
import BaseList from '../BaseList.vue'

const columns = [
    { id: 'number', label: '#' },
    { id: 'title', label: 'Title' },
]

function rows(...ids: string[]) {
    return ids.map((id) => ({ id }))
}

function mountList(groups: { id: string; title?: string; rows: { id: string }[] }[]) {
    return mount(BaseList, {
        props: { columns, groups, label: 'Songs' },
        slots: {
            cell: ({ row, column }: { row: { id: string }; column: { id: string } }) =>
                h('span', `${column.id}:${row.id}`),
            'group-info': () => h('span', 'info'),
        },
        attachTo: document.body,
    })
}

describe('BaseList', () => {
    it('labels each group with its heading row', () => {
        const wrapper = mountList([
            { id: 'first', title: 'First Album', rows: rows('one', 'two') },
            { id: 'second', title: 'Second Album', rows: rows('three') },
        ])

        const groups = wrapper.findAll('[data-part="list-group"]')
        expect(groups).toHaveLength(2)
        for (const group of groups) {
            const title = group.get(
                '[data-part="list-group-header"] [data-part="list-group-title"]',
            )
            expect(group.attributes('aria-labelledby')).toBe(title.attributes('id'))
        }
        expect(groups[0]!.get('[data-part="list-group-title"]').text()).toBe('First Album')
        wrapper.unmount()
    })

    it('shows a group without title without a heading row and without a label', () => {
        const wrapper = mountList([{ id: 'list', rows: rows('one', 'two', 'three') }])

        const group = wrapper.get('[data-part="list-group"]')
        expect(group.attributes('role')).toBe('rowgroup')
        expect(group.attributes('aria-labelledby')).toBeUndefined()
        expect(group.find('[data-part="list-group-header"]').exists()).toBe(false)
        expect(group.find('[data-part="list-group-title"]').exists()).toBe(false)
        expect(group.findAll('[data-part="list-row"]')).toHaveLength(3)
        expect(wrapper.get('[data-part="list"]').attributes('aria-label')).toBe('Songs')
        wrapper.unmount()
    })

    it('moves through the rows of a group without title with the arrow keys', async () => {
        const wrapper = mountList([{ id: 'list', rows: rows('one', 'two') }])

        const [first, second] = wrapper.findAll('[data-part="list-row"]')
        expect(first!.attributes('tabindex')).toBe('0')
        await first!.trigger('keydown', { key: 'ArrowDown' })
        expect(second!.attributes('aria-selected')).toBe('true')
        expect(wrapper.emitted('update:selected')?.at(-1)).toEqual(['two'])
        wrapper.unmount()
    })
})
