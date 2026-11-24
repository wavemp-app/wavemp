import { afterEach, describe, expect, it } from 'vitest'
import {
    defaultLayoutOptions,
    effectiveLayoutOptions,
    layoutOptions,
    layoutOptionVariables,
    parseLayoutOptions,
    readLayoutOptions,
    syncLayoutOptions,
} from '../layoutOptions'

function reader(values: Record<string, string>): (name: string) => string {
    return (name) => values[name] ?? ''
}

function clearRootVariables(): void {
    for (const name of Object.values(layoutOptionVariables)) {
        document.documentElement.style.removeProperty(name)
    }
}

describe('parseLayoutOptions', () => {
    it('keeps the layout as it is while a theme sets nothing', () => {
        expect(parseLayoutOptions(reader({}))).toEqual({
            breadcrumb: 'toolbar',
            queuePane: 'none',
            viewOptions: 'off',
        })
        expect(defaultLayoutOptions).toEqual(parseLayoutOptions(reader({})))
    })

    it('reads the options a theme switches on', () => {
        expect(
            parseLayoutOptions(
                reader({
                    '--wmp-layout-breadcrumb': 'header',
                    '--wmp-layout-queue-pane': 'docked',
                    '--wmp-layout-view-options': 'on',
                }),
            ),
        ).toEqual({ breadcrumb: 'header', queuePane: 'docked', viewOptions: 'on' })
    })

    it('ignores white space and case of the values', () => {
        expect(
            parseLayoutOptions(
                reader({
                    '--wmp-layout-breadcrumb': '  Header ',
                    '--wmp-layout-view-options': 'ON',
                }),
            ),
        ).toEqual({ breadcrumb: 'header', queuePane: 'none', viewOptions: 'on' })
    })

    it('takes the default for an unknown value', () => {
        expect(
            parseLayoutOptions(
                reader({
                    '--wmp-layout-breadcrumb': 'sidebar',
                    '--wmp-layout-queue-pane': 'docked floating',
                    '--wmp-layout-view-options': '"on"',
                }),
            ),
        ).toEqual(defaultLayoutOptions)
    })

    it('reads each option from its own variable', () => {
        const asked: string[] = []
        parseLayoutOptions((name) => {
            asked.push(name)
            return ''
        })

        expect(asked).toEqual([
            '--wmp-layout-breadcrumb',
            '--wmp-layout-queue-pane',
            '--wmp-layout-view-options',
        ])
    })
})

describe('readLayoutOptions and syncLayoutOptions', () => {
    afterEach(() => {
        clearRootVariables()
        layoutOptions.value = defaultLayoutOptions
    })

    it('reads the options from the computed style of the root element', () => {
        document.documentElement.style.setProperty('--wmp-layout-breadcrumb', 'header')

        expect(readLayoutOptions(document)).toEqual({
            ...defaultLayoutOptions,
            breadcrumb: 'header',
        })
    })

    it('updates the shared options only when they change', () => {
        syncLayoutOptions(document)
        expect(layoutOptions.value).toBe(defaultLayoutOptions)

        document.documentElement.style.setProperty('--wmp-layout-breadcrumb', 'header')
        syncLayoutOptions(document)
        const switched = layoutOptions.value
        expect(switched.breadcrumb).toBe('header')

        syncLayoutOptions(document)
        expect(layoutOptions.value).toBe(switched)

        clearRootVariables()
        syncLayoutOptions(document)
        expect(layoutOptions.value).toEqual(defaultLayoutOptions)
    })
})

describe('effectiveLayoutOptions', () => {
    const themed = { breadcrumb: 'header', queuePane: 'docked', viewOptions: 'on' } as const

    it('applies the options of the theme on the desktop', () => {
        expect(effectiveLayoutOptions(themed, true)).toBe(themed)
    })

    it('keeps the fixed mobile base on smaller screens', () => {
        expect(effectiveLayoutOptions(themed, false)).toEqual(defaultLayoutOptions)
    })
})
