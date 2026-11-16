import { describe, expect, it } from 'vitest'
import { defineComponent, h, KeepAlive, nextTick, ref } from 'vue'
import { mount } from '@vue/test-utils'
import { createWaveMpI18n } from '@/i18n'
import BaseDialog from '../BaseDialog.vue'

describe('BaseDialog', () => {
    it('puts the content of header-start before the title', () => {
        const wrapper = mount(BaseDialog, {
            props: { title: 'Edit', open: false },
            slots: { 'header-start': '<button data-part="btn-detail-back">Back</button>' },
            global: { plugins: [createWaveMpI18n('en')] },
        })

        const parts = [...wrapper.get('[data-part="dialog-header"]').element.children].map(
            (element) => element.getAttribute('data-part'),
        )
        expect(parts).toEqual(['btn-detail-back', 'dialog-title', 'btn-close'])
    })

    it('closes when its page goes to the background', async () => {
        const open = ref(true)
        const Page = defineComponent({
            name: 'DialogPage',
            setup() {
                return () =>
                    h(BaseDialog, {
                        title: 'Rename',
                        open: open.value,
                        'onUpdate:open': (value: boolean) => (open.value = value),
                    })
            },
        })
        const Other = defineComponent({ name: 'OtherPage', render: () => h('div') })
        const shown = ref<'page' | 'other'>('page')
        const wrapper = mount(
            defineComponent({
                setup: () => () =>
                    h(KeepAlive, null, [
                        shown.value === 'page'
                            ? h(Page, { key: 'page' })
                            : h(Other, { key: 'other' }),
                    ]),
            }),
            { global: { plugins: [createWaveMpI18n('en')] } },
        )

        shown.value = 'other'
        await nextTick()
        await nextTick()

        expect(open.value).toBe(false)
        wrapper.unmount()
    })
})
