// Keyboard logic of BaseSegmentedControl (radio group pattern with roving tabindex): the arrow keys
// check the next or previous option and wrap around, Home and End the first and last one. Disabled
// options are skipped.

/** Index of the option to check for a key, or null if the key does nothing. */
export function nextSegmentIndex(
    key: string,
    current: number,
    disabled: readonly boolean[],
): number | null {
    const count = disabled.length
    const enabled = disabled.map((_, index) => index).filter((index) => !disabled[index])
    if (enabled.length === 0) {
        return null
    }

    // The enabled option at a step from the current one, wrapping around.
    const step = (direction: 1 | -1): number => {
        let index = current < 0 ? (direction === 1 ? -1 : count) : current
        do {
            index = (index + direction + count) % count
        } while (disabled[index])
        return index
    }

    switch (key) {
        case 'ArrowRight':
        case 'ArrowDown':
            return step(1)
        case 'ArrowLeft':
        case 'ArrowUp':
            return step(-1)
        case 'Home':
            return enabled[0] ?? null
        case 'End':
            return enabled.at(-1) ?? null
        default:
            return null
    }
}

/** Index of the option in the tab order: the checked one if enabled, otherwise the first enabled. */
export function segmentTabStop(checked: number, disabled: readonly boolean[]): number {
    if (checked >= 0 && !disabled[checked]) {
        return checked
    }

    const first = disabled.findIndex((isDisabled) => !isDisabled)
    return first < 0 ? 0 : first
}
