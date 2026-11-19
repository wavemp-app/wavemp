// The central registry of the context menus: actions are registered per element type, each with
// its visibility (e.g. admins only), its state (enabled or greyed out, checked) and what it does.
// resolveMenu turns the actions of a target into the entries of BaseContextMenu (with separators
// between the groups), runMenuAction runs the chosen one.

import type { IconSlotName } from '@/themes/iconSlots'
import type { MenuServices, MenuTarget, MenuTargetType, TargetOf } from './targets'

type Getter<Target, Value> = (target: Target, services: MenuServices) => Value

export interface MenuAction<Target extends MenuTarget = MenuTarget> {
    /** Id of the entry (data-item); unique within the menu, including the submenus. */
    readonly id: string
    /** Entries of different groups are divided by a separator. */
    readonly group: string
    /** i18n key of the label; it gets the number of songs as parameter count. */
    readonly label: string | Getter<Target, string>
    readonly icon?: IconSlotName | Getter<Target, IconSlotName>
    /** Hidden entries are left out, e.g. those for admins only. Default: visible. */
    readonly visible?: Getter<Target, boolean>
    /** Disabled entries stay in the menu, greyed out. Default: enabled. */
    readonly enabled?: Getter<Target, boolean>
    /** Entry of a choice (menuitemradio): whether it is the chosen one. */
    readonly checked?: Getter<Target, boolean>
    /** Entries of a submenu; an entry with a submenu does nothing itself. */
    readonly children?: readonly MenuAction<Target>[]
    readonly run?: (target: Target, services: MenuServices) => void | Promise<void>
}

/** An entry as BaseContextMenu shows it, with the i18n key of its label. */
export interface ResolvedMenuEntry {
    readonly id: string
    readonly labelKey?: string
    readonly icon?: IconSlotName
    readonly disabled?: boolean
    readonly separator?: boolean
    readonly checked?: boolean
    readonly children?: readonly ResolvedMenuEntry[]
}

const registry = new Map<MenuTargetType, MenuAction[]>()

/** Registers actions of an element type after those registered before. */
export function registerMenuActions<Type extends MenuTargetType>(
    type: Type,
    actions: readonly MenuAction<TargetOf<Type>>[],
): void {
    registry.set(type, [...(registry.get(type) ?? []), ...(actions as readonly MenuAction[])])
}

/** Removes all actions, e.g. between tests. */
export function clearMenuActions(): void {
    registry.clear()
}

export function menuActionsOf(type: MenuTargetType): readonly MenuAction[] {
    return registry.get(type) ?? []
}

function value<Value>(
    getter: Value | Getter<MenuTarget, Value> | undefined,
    target: MenuTarget,
    services: MenuServices,
): Value | undefined {
    return typeof getter === 'function'
        ? (getter as Getter<MenuTarget, Value>)(target, services)
        : getter
}

function resolveAction(
    action: MenuAction,
    target: MenuTarget,
    services: MenuServices,
): ResolvedMenuEntry {
    const children = action.children
        ?.filter((child) => value(child.visible, target, services) ?? true)
        .map((child) => resolveAction(child, target, services))
    const enabled = value(action.enabled, target, services) ?? true
    const checked = value(action.checked, target, services)
    return {
        id: action.id,
        labelKey: value(action.label, target, services),
        icon: value(action.icon, target, services),
        ...(enabled && (!children || children.length > 0) ? {} : { disabled: true }),
        ...(checked === undefined ? {} : { checked }),
        ...(children ? { children } : {}),
    }
}

/** The visible entries of the target's menu, with a separator between groups. */
export function resolveMenu(target: MenuTarget, services: MenuServices): ResolvedMenuEntry[] {
    const entries: ResolvedMenuEntry[] = []
    let group: string | undefined
    for (const action of menuActionsOf(target.type)) {
        if (!(value(action.visible, target, services) ?? true)) {
            continue
        }

        if (group !== undefined && action.group !== group) {
            entries.push({ id: `separator-${action.group}`, separator: true })
        }

        group = action.group
        entries.push(resolveAction(action, target, services))
    }

    return entries
}

function find(actions: readonly MenuAction[], id: string): MenuAction | undefined {
    for (const action of actions) {
        if (action.id === id) {
            return action
        }

        const child = action.children ? find(action.children, id) : undefined
        if (child) {
            return child
        }
    }

    return undefined
}

/**
 * Runs the action with the id for the target, unless it is hidden or disabled (also through its
 * parent entry). Returns whether it ran.
 */
export async function runMenuAction(
    target: MenuTarget,
    id: string,
    services: MenuServices,
): Promise<boolean> {
    const runnable = (action: MenuAction): boolean =>
        (value(action.visible, target, services) ?? true) &&
        (value(action.enabled, target, services) ?? true)

    for (const action of menuActionsOf(target.type)) {
        const match = action.id === id ? action : find(action.children ?? [], id)
        if (!match) {
            continue
        }

        if (!runnable(action) || !runnable(match) || !match.run) {
            return false
        }

        await match.run(target, services)
        return true
    }

    return false
}
