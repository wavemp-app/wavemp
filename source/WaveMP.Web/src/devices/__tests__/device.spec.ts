import { describe, expect, it } from 'vitest'
import { deviceType, isAppleMobile } from '../device'

const agents = {
    firefoxWindows:
        'Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:132.0) Gecko/20100101 Firefox/132.0',
    safariMac:
        'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) ' +
        'Version/18.1 Safari/605.1.15',
    safariIphone:
        'Mozilla/5.0 (iPhone; CPU iPhone OS 18_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like ' +
        'Gecko) Version/18.1 Mobile/15E148 Safari/604.1',
    safariIpad:
        'Mozilla/5.0 (iPad; CPU OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) ' +
        'Version/17.0 Mobile/15E148 Safari/604.1',
    chromeAndroidPhone:
        'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) ' +
        'Chrome/131.0.0.0 Mobile Safari/537.36',
    chromeAndroidTablet:
        'Mozilla/5.0 (Linux; Android 14; SM-X710) AppleWebKit/537.36 (KHTML, like Gecko) ' +
        'Chrome/131.0.0.0 Safari/537.36',
    tizenTv:
        'Mozilla/5.0 (SMART-TV; LINUX; Tizen 6.5) AppleWebKit/537.36 (KHTML, like Gecko) ' +
        '85.0.4183.93/6.5 TV Safari/537.36',
}

describe('deviceType', () => {
    it('tells desktops, phones, tablets and TVs apart', () => {
        expect(deviceType({ userAgent: agents.firefoxWindows })).toBe('Desktop')
        expect(deviceType({ userAgent: agents.safariMac, maxTouchPoints: 0 })).toBe('Desktop')
        expect(deviceType({ userAgent: agents.safariIphone })).toBe('Phone')
        expect(deviceType({ userAgent: agents.safariIpad })).toBe('Tablet')
        expect(deviceType({ userAgent: agents.chromeAndroidPhone })).toBe('Phone')
        expect(deviceType({ userAgent: agents.chromeAndroidTablet })).toBe('Tablet')
        expect(deviceType({ userAgent: agents.tizenTv })).toBe('Tv')
        expect(deviceType({ userAgent: '' })).toBe('Desktop')
    })

    it('recognizes an iPad that asks for desktop pages by its touch screen', () => {
        expect(deviceType({ userAgent: agents.safariMac, maxTouchPoints: 5 })).toBe('Tablet')
    })
})

describe('isAppleMobile', () => {
    it('is true for iPhones and iPads only', () => {
        expect(isAppleMobile({ userAgent: agents.safariIphone })).toBe(true)
        expect(isAppleMobile({ userAgent: agents.safariIpad })).toBe(true)
        expect(isAppleMobile({ userAgent: agents.safariMac, maxTouchPoints: 5 })).toBe(true)
        expect(isAppleMobile({ userAgent: agents.safariMac })).toBe(false)
        expect(isAppleMobile({ userAgent: agents.chromeAndroidPhone })).toBe(false)
    })
})
