// The keyboard shortcuts of the app, like those of a desktop player, and the registry they live
// in. registerDefaultShortcuts() defines them at startup (src/main.ts); a conflict among them, or
// with a shortcut a later feature defines, throws there. The shell binds what they do (see
// src/composables/useKeyboardShortcuts.ts); the Now Playing stage binds full screen while it is
// shown.

import { createShortcutRegistry, type ShortcutDefinition, type ShortcutRegistry } from './registry'

export const defaultShortcuts: readonly ShortcutDefinition[] = [
    { id: 'play-pause', keys: ['space'], group: 'playback', label: 'shortcuts.actions.playPause' },
    {
        id: 'seek-backward',
        keys: ['arrowleft'],
        group: 'playback',
        label: 'shortcuts.actions.seekBackward',
        repeat: true,
    },
    {
        id: 'seek-forward',
        keys: ['arrowright'],
        group: 'playback',
        label: 'shortcuts.actions.seekForward',
        repeat: true,
    },
    {
        id: 'previous',
        keys: ['mod+arrowleft'],
        group: 'playback',
        label: 'shortcuts.actions.previous',
    },
    { id: 'next', keys: ['mod+arrowright'], group: 'playback', label: 'shortcuts.actions.next' },
    { id: 'shuffle', keys: ['s'], group: 'playback', label: 'shortcuts.actions.shuffle' },
    { id: 'repeat', keys: ['r'], group: 'playback', label: 'shortcuts.actions.repeat' },
    {
        id: 'volume-up',
        keys: ['arrowup'],
        group: 'volume',
        label: 'shortcuts.actions.volumeUp',
        repeat: true,
    },
    {
        id: 'volume-down',
        keys: ['arrowdown'],
        group: 'volume',
        label: 'shortcuts.actions.volumeDown',
        repeat: true,
    },
    { id: 'mute', keys: ['m'], group: 'volume', label: 'shortcuts.actions.mute' },
    { id: 'fullscreen', keys: ['f'], group: 'view', label: 'shortcuts.actions.fullscreen' },
    { id: 'search', keys: ['mod+f'], group: 'general', label: 'shortcuts.actions.search' },
    { id: 'shortcuts', keys: ['?'], group: 'general', label: 'shortcuts.actions.overview' },
]

/** The registry of the app. */
export const shortcutRegistry: ShortcutRegistry = createShortcutRegistry()

let registered = false

/** Defines the shortcuts above once; throws a ShortcutConflictError on a conflict. */
export function registerDefaultShortcuts(registry: ShortcutRegistry = shortcutRegistry): void {
    if (registry === shortcutRegistry) {
        if (registered) {
            return
        }

        registered = true
    }

    registry.define(defaultShortcuts)
}
