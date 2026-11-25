import { describe, expect, it } from 'vitest'
import { deviceTabStop, nextDeviceIndex, pickerPosition } from '../picker'

describe('pickerPosition', () => {
    const viewport = { width: 1280, height: 800 }

    it('opens above the button with the end edges aligned', () => {
        const anchor = { left: 1200, top: 740, right: 1232, bottom: 772 }
        expect(pickerPosition(anchor, { width: 352, height: 300 }, viewport)).toEqual({
            placement: 'above',
            x: 880,
            y: 436,
            maxHeight: 732,
        })
    })

    it('stays within the viewport', () => {
        const anchor = { left: 8, top: 740, right: 40, bottom: 772 }
        expect(pickerPosition(anchor, { width: 352, height: 300 }, viewport).x).toBe(4)
        const narrow = { width: 300, height: 800 }
        expect(
            pickerPosition(
                { left: 260, top: 740, right: 292, bottom: 772 },
                {
                    width: 352,
                    height: 300,
                },
                narrow,
            ).x,
        ).toBe(4)
    })

    it('opens below a button at the top of the window', () => {
        const anchor = { left: 1200, top: 40, right: 1232, bottom: 72 }
        expect(pickerPosition(anchor, { width: 352, height: 300 }, viewport)).toEqual({
            placement: 'below',
            x: 880,
            y: 76,
            maxHeight: 720,
        })
    })

    it('scrolls instead of leaving the screen when neither room is large enough', () => {
        const anchor = { left: 1200, top: 400, right: 1232, bottom: 432 }
        expect(pickerPosition(anchor, { width: 352, height: 500 }, viewport)).toEqual({
            placement: 'above',
            x: 880,
            y: 4,
            maxHeight: 392,
        })
        expect(
            pickerPosition(
                { ...anchor, top: 300, bottom: 332 },
                { width: 352, height: 500 },
                viewport,
            ),
        ).toMatchObject({ placement: 'below', y: 336, maxHeight: 460 })
    })
})

describe('nextDeviceIndex', () => {
    it('moves through the devices and wraps around', () => {
        expect(nextDeviceIndex('ArrowDown', 0, 3)).toBe(1)
        expect(nextDeviceIndex('ArrowRight', 2, 3)).toBe(0)
        expect(nextDeviceIndex('ArrowUp', 0, 3)).toBe(2)
        expect(nextDeviceIndex('ArrowLeft', 1, 3)).toBe(0)
        expect(nextDeviceIndex('Home', 2, 3)).toBe(0)
        expect(nextDeviceIndex('End', 0, 3)).toBe(2)
    })

    it('starts at the ends without a focused device', () => {
        expect(nextDeviceIndex('ArrowDown', -1, 3)).toBe(0)
        expect(nextDeviceIndex('ArrowUp', -1, 3)).toBe(2)
    })

    it('ignores other keys and empty lists', () => {
        expect(nextDeviceIndex('Enter', 0, 3)).toBeNull()
        expect(nextDeviceIndex('ArrowDown', 0, 0)).toBeNull()
    })
})

describe('deviceTabStop', () => {
    it('is the device that plays, otherwise this device, otherwise the first one', () => {
        expect(
            deviceTabStop([
                { active: false, current: true },
                { active: true, current: false },
            ]),
        ).toBe(1)
        expect(
            deviceTabStop([
                { active: false, current: false },
                { active: false, current: true },
            ]),
        ).toBe(1)
        expect(deviceTabStop([{ active: false, current: false }])).toBe(0)
        expect(deviceTabStop([])).toBe(0)
    })
})
