import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { useViewRoute } from '../useViewRoute'

const component = { render: () => null }

async function setup(path: string) {
    const router = createRouter({
        history: createMemoryHistory(),
        routes: [
            { path: '/library/songs', component },
            { path: '/library/tracks/:id', component },
        ],
    })
    await router.push(path)
    let route: ReturnType<typeof useViewRoute> | undefined
    const wrapper = mount(
        defineComponent({
            setup() {
                route = useViewRoute()
                return () => h('div')
            },
        }),
        { global: { plugins: [router] } },
    )
    return { router, route: route!, wrapper }
}

describe('useViewRoute', () => {
    it('follows changes of the query of its page', async () => {
        const { router, route, wrapper } = await setup('/library/songs')

        await router.replace('/library/songs?sort=Title&myMusic=true')

        expect(route.value.query).toEqual({ sort: 'Title', myMusic: 'true' })
        wrapper.unmount()
    })

    it('keeps its route while another page is shown', async () => {
        const { router, route, wrapper } = await setup('/library/songs?myMusic=true')

        await router.push('/library/tracks/1?edit=metadata')

        expect(route.value.fullPath).toBe('/library/songs?myMusic=true')
        expect(route.value.query).toEqual({ myMusic: 'true' })

        await router.push('/library/songs?sort=Title')
        expect(route.value.fullPath).toBe('/library/songs?sort=Title')
        wrapper.unmount()
    })
})
