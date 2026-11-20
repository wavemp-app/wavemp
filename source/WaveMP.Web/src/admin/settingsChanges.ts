// Changes of a settings form. A form sends only the values that were changed in it, so it never
// puts back an older value of a setting that was changed elsewhere (by another admin, another tab
// or another form) since the form was loaded. Pure functions only.

type SettingValue = string | number | boolean | readonly string[] | null | undefined

function sameValue(saved: SettingValue, edited: SettingValue): boolean {
    if (Array.isArray(saved) || Array.isArray(edited)) {
        const before = (saved ?? []) as readonly string[]
        const after = (edited ?? []) as readonly string[]
        return (
            before.length === after.length && before.every((value, index) => value === after[index])
        )
    }

    return saved === edited
}

/**
 * The values of `edited` that differ from the settings the form was loaded with. Lists count as
 * changed if an entry or their order changed. Without loaded settings every value counts as
 * changed.
 */
export function changedSettings<T extends Record<string, SettingValue>>(
    saved: object | null | undefined,
    edited: T,
): Partial<T> {
    const loaded = saved as Partial<Record<keyof T, SettingValue>> | null | undefined
    const changes: Partial<T> = {}
    for (const key of Object.keys(edited) as (keyof T)[]) {
        if (!loaded || !sameValue(loaded[key], edited[key])) {
            changes[key] = edited[key]
        }
    }

    return changes
}
