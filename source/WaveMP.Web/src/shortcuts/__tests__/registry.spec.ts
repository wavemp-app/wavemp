import { afterEach, describe, expect, it, vi } from 'vitest'
import { defaultShortcuts, registerDefaultShortcuts } from '../defaults'
import {
    createShortcutRegistry,
    findShortcutConflicts,
    isTypingTarget,
    ShortcutConflictError,
    type ShortcutDefinition,
} from '../registry'

function shortcut(id: string, keys: string[], repeat = false): ShortcutDefinition {
    return { id, keys, group: 'general', label: `test.${id}`, repeat }
}

/** Dispatches a keydown on the element and returns the event. */
function press(target: EventTarget, key: string, options: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, ...options })
    target.dispatchEvent(event)
    return event
}

/** A registry whose handle() listens to the document, like the shell. */
function listening(definitions: readonly ShortcutDefinition[]) {
    const registry = createShortcutRegistry(false)
    registry.define(definitions)
    const listener = (event: Event) => registry.handle(event as KeyboardEvent)
    document.addEventListener('keydown', listener)
    cleanups.push(() => document.removeEventListener('keydown', listener))
    return registry
}

const cleanups: (() => void)[] = []

afterEach(() => {
    cleanups.splice(0).forEach((cleanup) => cleanup())
    document.body.innerHTML = ''
})

describe('the default shortcuts', () => {
    it('have no conflicts', () => {
        expect(findShortcutConflicts(defaultShortcuts)).toEqual([])
        expect(() => registerDefaultShortcuts(createShortcutRegistry(false))).not.toThrow()
    })

    it('are the shortcuts of the issue', () => {
        const keys = Object.fromEntries(defaultShortcuts.map((item) => [item.id, item.keys]))
        expect(keys).toEqual({
            'play-pause': ['space'],
            'seek-backward': ['arrowleft'],
            'seek-forward': ['arrowright'],
            previous: ['mod+arrowleft'],
            next: ['mod+arrowright'],
            shuffle: ['s'],
            repeat: ['r'],
            'volume-up': ['arrowup'],
            'volume-down': ['arrowdown'],
            mute: ['m'],
            fullscreen: ['f'],
            'group-overview': ['mod+-', 'escape'],
            search: ['mod+f'],
            shortcuts: ['?'],
        })
    })
})

describe('conflicts', () => {
    it('are found for keys used twice, also in another spelling', () => {
        expect(
            findShortcutConflicts([
                shortcut('a', ['Mod+ArrowLeft']),
                shortcut('b', ['mod+arrowleft']),
            ]),
        ).toEqual([{ kind: 'combo', combo: 'mod+arrowleft', ids: ['a', 'b'] }])
    })

    it('are found for ids defined twice and keys the app reserves', () => {
        expect(findShortcutConflicts([shortcut('a', ['x']), shortcut('a', ['y'])])).toEqual([
            { kind: 'id', ids: ['a'] },
        ])
        expect(findShortcutConflicts([shortcut('close', ['Escape'])])).toEqual([
            { kind: 'reserved', combo: 'escape', ids: ['close'] },
        ])
        expect(findShortcutConflicts([shortcut('menu', ['shift+F10'])])).toEqual([
            { kind: 'reserved', combo: 'shift+f10', ids: ['menu'] },
        ])
    })

    it('allow reserved keys for fallback shortcuts, but not twice', () => {
        const fallback = (id: string) => ({ ...shortcut(id, ['Escape']), fallback: true })
        expect(findShortcutConflicts([fallback('overview')])).toEqual([])
        expect(
            findShortcutConflicts([fallback('overview'), shortcut('close', ['escape'])]),
        ).toEqual([{ kind: 'reserved', combo: 'escape', ids: ['close'] }])
        expect(findShortcutConflicts([fallback('one'), fallback('two')])).toEqual([
            { kind: 'combo', combo: 'escape', ids: ['one', 'two'] },
        ])
    })

    it('make define() throw and add none of the new shortcuts', () => {
        const registry = createShortcutRegistry(false)
        registerDefaultShortcuts(registry)

        // A later feature that takes M (mute) for itself.
        const later = [shortcut('later-feature', ['l']), shortcut('marker', ['M'])]
        let error: unknown
        try {
            registry.define(later)
        } catch (caught) {
            error = caught
        }

        expect(error).toBeInstanceOf(ShortcutConflictError)
        expect((error as ShortcutConflictError).conflicts).toEqual([
            { kind: 'combo', combo: 'm', ids: ['mute', 'marker'] },
        ])
        expect((error as Error).message).toContain('"m" used by mute, marker')
        expect(registry.find('l')).toBeUndefined()
        expect(registry.definitions()).toHaveLength(defaultShortcuts.length)
    })

    it('allow shortcuts of later features with free keys', () => {
        const registry = createShortcutRegistry(false)
        registerDefaultShortcuts(registry)
        registry.define([shortcut('later-feature', ['l'])])
        expect(registry.find('L')?.id).toBe('later-feature')
    })
})

describe('handling', () => {
    it('runs the bound handler and prevents the default action', () => {
        const registry = listening([shortcut('mute', ['m'])])
        const run = vi.fn()
        registry.bind('mute', run)

        const event = press(document.body, 'm')

        expect(run).toHaveBeenCalledOnce()
        expect(event.defaultPrevented).toBe(true)
    })

    it('runs the last bound handler first and hands on when it returns false', () => {
        const registry = listening([shortcut('fullscreen', ['f'])])
        const shell = vi.fn()
        const stage = vi.fn(() => false)
        registry.bind('fullscreen', shell)
        const release = registry.bind('fullscreen', stage)

        press(document.body, 'f')
        expect(stage).toHaveBeenCalledOnce()
        expect(shell).toHaveBeenCalledOnce()

        stage.mockReturnValue(true as never)
        press(document.body, 'f')
        expect(shell).toHaveBeenCalledOnce()

        release()
        press(document.body, 'f')
        expect(stage).toHaveBeenCalledTimes(2)
        expect(shell).toHaveBeenCalledTimes(2)
    })

    it('leaves the key alone when no handler applies', () => {
        const registry = listening([shortcut('search', ['mod+f'])])
        registry.bind('search', () => false)
        const event = press(document.body, 'f', { ctrlKey: true })
        expect(event.defaultPrevented).toBe(false)
    })

    it('refuses to bind an undefined shortcut', () => {
        const registry = createShortcutRegistry(false)
        expect(() => registry.bind('unknown', () => undefined)).toThrow(/not defined/)
    })

    it('repeats only shortcuts meant for held keys', () => {
        const registry = listening([shortcut('louder', ['arrowup'], true), shortcut('mute', ['m'])])
        const louder = vi.fn()
        const mute = vi.fn()
        registry.bind('louder', louder)
        registry.bind('mute', mute)

        press(document.body, 'ArrowUp', { repeat: true })
        const held = press(document.body, 'm', { repeat: true })

        expect(louder).toHaveBeenCalledOnce()
        expect(mute).not.toHaveBeenCalled()
        expect(held.defaultPrevented).toBe(true)
    })

    it('ignores keys another element handled already and keys of an input method', () => {
        const registry = listening([shortcut('mute', ['m'])])
        const mute = vi.fn()
        registry.bind('mute', mute)
        const element = document.createElement('div')
        element.addEventListener('keydown', (event) => event.preventDefault())
        document.body.append(element)

        press(element, 'm')
        press(document.body, 'm', { isComposing: true })

        expect(mute).not.toHaveBeenCalled()
    })

    it('does not apply while a field has the focus', () => {
        const registry = listening([
            shortcut('play-pause', ['space']),
            shortcut('mute', ['m']),
            shortcut('search', ['mod+f']),
        ])
        const run = vi.fn()
        registry.bind('play-pause', run)
        registry.bind('mute', run)
        registry.bind('search', run)
        document.body.innerHTML = `
            <input id="text" type="search">
            <textarea id="area"></textarea>
            <select id="select"><option>a</option></select>
            <div id="editable" contenteditable="true"><span id="inside">x</span></div>`

        for (const id of ['text', 'area', 'select', 'inside']) {
            const element = document.getElementById(id)!
            expect(press(element, ' ').defaultPrevented, id).toBe(false)
            press(element, 'm')
            press(element, 'f', { ctrlKey: true })
        }

        expect(run).not.toHaveBeenCalled()
    })

    it('leaves Space to buttons and the arrow keys to widgets that move with them', () => {
        const registry = listening([
            shortcut('play-pause', ['space']),
            shortcut('seek', ['arrowleft'], true),
            shortcut('previous', ['mod+arrowleft']),
            shortcut('mute', ['m']),
        ])
        const play = vi.fn()
        const seek = vi.fn()
        const previous = vi.fn()
        const mute = vi.fn()
        registry.bind('play-pause', play)
        registry.bind('seek', seek)
        registry.bind('previous', previous)
        registry.bind('mute', mute)
        document.body.innerHTML = `
            <button id="button" type="button">Next</button>
            <div role="radiogroup"><button id="radio" role="radio" type="button">A</button></div>
            <div id="slider" role="slider" tabindex="0"></div>`

        press(document.getElementById('button')!, ' ')
        press(document.getElementById('radio')!, 'ArrowLeft')
        press(document.getElementById('slider')!, 'ArrowLeft')
        expect(play).not.toHaveBeenCalled()
        expect(seek).not.toHaveBeenCalled()

        // Other keys still work there, and the arrow keys with Ctrl.
        press(document.getElementById('button')!, 'ArrowLeft')
        press(document.getElementById('slider')!, 'ArrowLeft', { ctrlKey: true })
        press(document.getElementById('slider')!, 'm')
        expect(seek).toHaveBeenCalledOnce()
        expect(previous).toHaveBeenCalledOnce()
        expect(mute).toHaveBeenCalledOnce()
    })

    it('does not apply inside an open dialog or menu', () => {
        const registry = listening([shortcut('mute', ['m'])])
        const mute = vi.fn()
        registry.bind('mute', mute)
        document.body.innerHTML = `
            <dialog open><button id="in-dialog" type="button">OK</button></dialog>
            <div role="menu"><button id="in-menu" role="menuitem" type="button">A</button></div>`

        press(document.getElementById('in-dialog')!, 'm')
        press(document.getElementById('in-menu')!, 'm')

        expect(mute).not.toHaveBeenCalled()
    })
})

describe('isTypingTarget', () => {
    it('takes text fields but not checkboxes and buttons', () => {
        document.body.innerHTML = `
            <input id="text"><input id="number" type="number">
            <input id="check" type="checkbox"><input id="range" type="range">
            <button id="button" type="button"></button>`
        expect(isTypingTarget(document.getElementById('text'))).toBe(true)
        expect(isTypingTarget(document.getElementById('number'))).toBe(true)
        expect(isTypingTarget(document.getElementById('check'))).toBe(false)
        expect(isTypingTarget(document.getElementById('range'))).toBe(false)
        expect(isTypingTarget(document.getElementById('button'))).toBe(false)
        expect(isTypingTarget(null)).toBe(false)
    })
})
