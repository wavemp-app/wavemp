// The kind of this device for the list of devices (an icon per kind). The id and the name come from
// src/player/device.ts, which the saved playback state uses as well.

import { DeviceTypeObject, type DeviceType } from '@/api/generated/models'

/** What the browser tells about the device. */
export interface DeviceHints {
    readonly userAgent: string
    /** Touch points of the screen; iPads ask for desktop pages and only tell this way. */
    readonly maxTouchPoints?: number
}

const tvPattern =
    /SmartTV|SMART-TV|Tizen|Web0S|webOS.*TV|NetCast|BRAVIA|GoogleTV|Android TV|AFT[A-Z]|CrKey|HbbTV|AppleTV/i
const tabletPattern = /iPad|Tablet|PlayBook|Silk\/|Kindle/i
const phonePattern = /iPhone|iPod|Mobile|Windows Phone|IEMobile|BlackBerry|Opera Mini/i

/**
 * The kind of a device from its browser: a TV, a tablet (also an iPad that asks for desktop pages
 * and an Android device without "Mobile"), a phone, otherwise a desktop.
 */
export function deviceType(hints: DeviceHints): DeviceType {
    const agent = hints.userAgent
    if (tvPattern.test(agent)) {
        return DeviceTypeObject.Tv
    }

    if (tabletPattern.test(agent) || isDesktopIpad(hints)) {
        return DeviceTypeObject.Tablet
    }

    if (/Android/.test(agent)) {
        return /Mobile/.test(agent) ? DeviceTypeObject.Phone : DeviceTypeObject.Tablet
    }

    if (phonePattern.test(agent)) {
        return DeviceTypeObject.Phone
    }

    return DeviceTypeObject.Desktop
}

function isDesktopIpad(hints: DeviceHints): boolean {
    return /Macintosh/.test(hints.userAgent) && (hints.maxTouchPoints ?? 0) > 1
}

/**
 * Whether the device is an iPhone or iPad: their browsers close the connection as soon as the page
 * goes to the background, so remote control only works while WaveMP is in the foreground there.
 */
export function isAppleMobile(hints: DeviceHints): boolean {
    return /iPhone|iPad|iPod/.test(hints.userAgent) || isDesktopIpad(hints)
}

/** The hints of this browser. */
export function browserHints(): DeviceHints {
    return typeof navigator === 'undefined'
        ? { userAgent: '' }
        : { userAgent: navigator.userAgent, maxTouchPoints: navigator.maxTouchPoints }
}
