import { beforeEach, describe, expect, it } from 'vitest'
import { defineComponent, h, KeepAlive, nextTick, ref, type Ref } from 'vue'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { usePageTitleStore } from '@/stores/pageTitle'
import { useDetailTitle } from '../useDetailTitle'

const albumTitle = ref<string | null>('First Album')

const Album = defineComponent({
    name: 'AlbumPage',
    setup() {
        useDetailTitle(() => albumTitle.value)
        return () => h('div', 'album')
    },
})

const Year = defineComponent({
    name: 'YearPage',
    setup() {
        useDetailTitle(() => '2026')
        return () => h('div', 'year')
    },
})

const Plain = defineComponent({
    name: 'PlainPage',
    setup() {
        return () => h('div', 'plain')
    },
})

/** The pages one after the other, kept alive like in the app shell (max entries). */
function pages(shown: Ref<string>, max = 10) {
    const components: Record<string, unknown> = { album: Album, year: Year, plain: Plain }
    return mount(
        defineComponent({
            setup() {
                return () =>
                    h(KeepAlive, { max }, [
                        h(components[shown.value] as typeof Album, { key: shown.value }),
                    ])
            },
        }),
    )
}

describe('useDetailTitle', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        albumTitle.value = 'First Album'
    })

    it('shows the title while the page is shown and follows it', async () => {
        const store = usePageTitleStore()
        const shown = ref('album')
        const wrapper = pages(shown)
        await nextTick()
        expect(store.title).toBe('First Album')

        albumTitle.value = 'Renamed Album'
        await nextTick()
        expect(store.title).toBe('Renamed Album')
        wrapper.unmount()
        expect(store.title).toBeNull()
    })

    it('shows the title of the page that is shown again', async () => {
        const store = usePageTitleStore()
        const shown = ref('album')
        const wrapper = pages(shown)
        await nextTick()

        shown.value = 'year'
        await nextTick()
        expect(store.title).toBe('2026')

        shown.value = 'plain'
        await nextTick()
        expect(store.title).toBeNull()

        shown.value = 'album'
        await nextTick()
        expect(store.title).toBe('First Album')
        wrapper.unmount()
    })

    it('ignores the title of a page in the background', async () => {
        const store = usePageTitleStore()
        const shown = ref('album')
        const wrapper = pages(shown)
        await nextTick()
        shown.value = 'plain'
        await nextTick()

        albumTitle.value = 'Loaded Later'
        await nextTick()

        expect(store.title).toBeNull()
        wrapper.unmount()
    })

    it('keeps the title of the shown page when a page in the background is dropped', async () => {
        const store = usePageTitleStore()
        const shown = ref('album')
        // Only one page in the background: showing the third page drops the album.
        const wrapper = pages(shown, 2)
        await nextTick()
        shown.value = 'plain'
        await nextTick()

        shown.value = 'year'
        await nextTick()

        expect(store.title).toBe('2026')
        wrapper.unmount()
    })
})
