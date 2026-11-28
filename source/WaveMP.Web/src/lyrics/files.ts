// Lyrics files an admin uploads: LRC files with times or plain text files.

/** The file types the file picker offers. */
export const lyricsFileTypes = '.lrc,.txt,text/plain'

/** The most characters the server accepts (256 KiB of text). */
export const maxLyricsLength = 256 * 1024

/**
 * The text of a lyrics file: UTF-8 (with or without byte order mark), otherwise Windows-1252, the
 * encoding of older LRC files that are not valid UTF-8.
 */
export function decodeLyricsFile(content: ArrayBuffer): string {
    let text: string
    try {
        text = new TextDecoder('utf-8', { fatal: true }).decode(content)
    } catch {
        text = new TextDecoder('windows-1252').decode(content)
    }

    return text.replace(/^﻿/, '')
}

const timestamp = /^\s*\[\d{1,3}:\d{1,2}(?:[.:]\d{1,3})?\]/

/** Whether a text has at least one line with a time, so it follows the music as LRC. */
export function hasTimestamps(text: string): boolean {
    return text.split(/\r\n|\r|\n/).some((line) => timestamp.test(line))
}
