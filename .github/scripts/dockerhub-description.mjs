// Creates the description of the Docker Hub repository wavemp/wavemp from README.md, used by the CI
// (main.yml) and locally.
//
// Usage (from any directory): node .github/scripts/dockerhub-description.mjs [--output <file>]
//
// Docker Hub limits the full description to 25,000 bytes and the short description to 100 bytes, and
// it cannot resolve the relative links and images of the README. The copy for Docker Hub therefore
// - starts with the part of the README before its first section (logo, title, tagline, badges),
// - takes the sections of dockerHubSections (split at their ### subsections) in the order of the
//   README, as many as fit into the limit,
// - ends with a link to the README on GitHub that names the sections it leaves out,
// - turns relative links and images into absolute addresses of this repository on GitHub: pictures
//   to raw.githubusercontent.com, files to their page on github.com and anchors (#quick-start) to
//   the README on github.com, since Docker Hub creates no anchors for headings. Code blocks and
//   inline code stay as they are.
// The short description is the bold tagline below the title of the README.
//
// Without --output the copy is written to stdout. If GITHUB_OUTPUT is set (GitHub Actions), the
// short description is written there as short-description. Exits with 1 if the README has no
// tagline, the tagline or the first part are too long, or a relative link points to a missing file.
// README.md itself is never changed.

import { appendFileSync, existsSync, readFileSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

export const scriptsDirectory = path.dirname(fileURLToPath(import.meta.url))
export const repositoryRoot = path.resolve(scriptsDirectory, '..', '..')

/** GitHub repository and branch the absolute addresses point to. */
export const githubRepository = 'wavemp-app/wavemp'
export const githubBranch = 'main'
export const readmeUrl = `https://github.com/${githubRepository}`

/** Limits of Docker Hub, in bytes (UTF-8). */
export const fullDescriptionLimit = 25000
export const shortDescriptionLimit = 100

/** Sections (## headings) of the README that are shown on Docker Hub, if they fit. */
export const dockerHubSections = ['About', 'Features', 'Screenshots', 'Quick Start', 'License']

/** File extensions that are linked as pictures (raw file) instead of their page on GitHub. */
export const imageExtensions = new Set(['avif', 'bmp', 'gif', 'jpeg', 'jpg', 'png', 'svg', 'webp'])

const fencePattern = /^ {0,3}(`{3,}|~{3,})/
const headingPattern = /^ {0,3}(#{1,6})[ \t]+(.*?)[ \t#]*$/
const schemePattern = /^[a-z][a-z0-9+.-]*:/i

/** Number of bytes of a text in UTF-8, the unit of the Docker Hub limits. */
export function byteLength(text) {
    return Buffer.byteLength(text, 'utf8')
}

/**
 * Calls visit(line, inFence) for every line, with inFence true for the lines of fenced code blocks
 * including their fences.
 */
function forEachLine(lines, visit) {
    let fence = null
    for (const line of lines) {
        const match = fencePattern.exec(line)
        if (fence) {
            visit(line, true)
            if (match && match[1][0] === fence[0] && match[1].length >= fence.length) {
                fence = null
            }
        } else if (match) {
            fence = match[1]
            visit(line, true)
        } else {
            visit(line, false)
        }
    }
}

/**
 * The anchor GitHub creates for a heading: lower case, without punctuation, spaces as dashes, and
 * -1, -2 ... for repeated headings (counted in usedSlugs).
 */
export function githubSlug(heading, usedSlugs = new Map()) {
    const base = heading
        .replace(/`/g, '')
        .trim()
        .toLowerCase()
        .replace(/[^\p{L}\p{M}\p{N}\s_-]/gu, '')
        .replace(/\s/g, '-')
    const count = usedSlugs.get(base) ?? 0
    usedSlugs.set(base, count + 1)
    return count === 0 ? base : `${base}-${count}`
}

/**
 * Splits a README into the part before its first ## section and its ## sections, each split into
 * chunks at its ### subsections. Headings in code blocks do not count. Every heading gets the anchor
 * GitHub creates for it.
 */
export function splitSections(markdown) {
    const lines = markdown.replace(/\r\n?/g, '\n').split('\n')
    const usedSlugs = new Map()
    const intro = []
    const sections = []
    let target = intro
    forEachLine(lines, (line, inFence) => {
        const heading = inFence ? null : headingPattern.exec(line)
        if (heading) {
            const level = heading[1].length
            const title = heading[2]
            const slug = githubSlug(title, usedSlugs)
            if (level === 2) {
                const chunk = { title, slug, level, lines: [] }
                sections.push({ title, slug, chunks: [chunk] })
                target = chunk.lines
            } else if (level === 3 && sections.length > 0) {
                const chunk = { title, slug, level, lines: [] }
                sections.at(-1).chunks.push(chunk)
                target = chunk.lines
            }
        }
        target.push(line)
    })
    const join = (chunkLines) => chunkLines.join('\n')
    return {
        intro: join(intro),
        sections: sections.map((section) => ({
            ...section,
            chunks: section.chunks.map(({ lines: chunkLines, ...chunk }) => ({
                ...chunk,
                text: join(chunkLines),
            })),
        })),
    }
}

/**
 * The absolute address of a link target of the README, or null if it is absolute already. Relative
 * paths are taken from the repository root, where the README is; the path of every file is passed to
 * onFile, so the caller can check that it exists.
 */
export function absoluteUrl(target, onFile = () => {}) {
    if (target === '' || schemePattern.test(target) || target.startsWith('//')) {
        return null
    }
    if (target.startsWith('#')) {
        return `${readmeUrl}${target}`
    }
    const suffixStart = target.search(/[?#]/)
    const filePart = suffixStart >= 0 ? target.slice(0, suffixStart) : target
    const suffix = suffixStart >= 0 ? target.slice(suffixStart) : ''
    const isDirectory = filePart.endsWith('/')
    const normalized = path.posix.normalize(filePart.startsWith('/') ? `.${filePart}` : filePart)
    if (normalized === '..' || normalized.startsWith('../')) {
        throw new Error(`The link ${target} points outside of the repository.`)
    }
    const filePath = normalized.replace(/\/$/, '')
    if (filePath === '.' || filePath === '') {
        return `${readmeUrl}${suffix}`
    }
    onFile(decodeURIComponent(filePath))
    const extension = path.posix.extname(filePath).slice(1).toLowerCase()
    if (!isDirectory && imageExtensions.has(extension)) {
        return `https://raw.githubusercontent.com/${githubRepository}/${githubBranch}/${filePath}${suffix}`
    }
    if (isDirectory) {
        return `${readmeUrl}/tree/${githubBranch}/${filePath}/${suffix}`
    }
    return `${readmeUrl}/blob/${githubBranch}/${filePath}${suffix}`
}

/** Replaces the relative link targets in a piece of Markdown text without code. */
function convertText(text, convert) {
    const replaceTarget = (target) => {
        const angle = target.startsWith('<') && target.endsWith('>')
        const bare = angle ? target.slice(1, -1) : target
        const url = convert(bare)
        if (url === null) {
            return target
        }
        return angle ? `<${url}>` : url
    }
    return (
        text
            // Inline links and images: [text](target "title"), ![alt](target).
            .replace(
                /\]\((\s*)(<[^>\n]*>|[^\s()]+)/g,
                (_match, space, target) => `](${space}${replaceTarget(target)}`,
            )
            // Reference definitions: [name]: target "title".
            .replace(
                /^( {0,3}\[[^\]\n]+\]:[ \t]*)(<[^>\n]*>|\S+)/gm,
                (_match, start, target) => `${start}${replaceTarget(target)}`,
            )
            // HTML: <img src="..."> and <a href="...">.
            .replace(
                /\b(src|href)=("([^"]*)"|'([^']*)')/g,
                (match, attribute, _quoted, double, single) => {
                    const target = double ?? single
                    const url = convert(target)
                    if (url === null) {
                        return match
                    }
                    const quote = double !== undefined ? '"' : "'"
                    return `${attribute}=${quote}${url}${quote}`
                },
            )
    )
}

/**
 * Turns the relative links and images of Markdown into absolute addresses (see absoluteUrl), except
 * in fenced code blocks and inline code.
 */
export function convertLinks(markdown, onFile = () => {}) {
    const convert = (target) => absoluteUrl(target, onFile)
    const result = []
    forEachLine(markdown.split('\n'), (line, inFence) => {
        if (inFence) {
            result.push(line)
            return
        }
        let converted = ''
        let position = 0
        for (const code of line.matchAll(/(`+)[^`](?:.*?[^`])?\1(?!`)/g)) {
            converted += convertText(line.slice(position, code.index), convert) + code[0]
            position = code.index + code[0].length
        }
        result.push(converted + convertText(line.slice(position), convert))
    })
    return result.join('\n')
}

/** The short description: the bold tagline below the title of the README. */
export function findShortDescription(markdown) {
    const lines = markdown.replace(/\r\n?/g, '\n').split('\n')
    const title = lines.findIndex((line) => /^# /.test(line))
    const tagline = lines
        .slice(title + 1)
        .map((line) => line.trim())
        .find((line) => line !== '')
    const match = tagline && /^\*\*([^*]+)\*\*$/.exec(tagline)
    if (title < 0 || !match) {
        throw new Error(
            'The README needs a bold tagline (**...**) as the first line below its title.',
        )
    }
    const description = match[1].trim()
    if (byteLength(description) > shortDescriptionLimit) {
        throw new Error(
            `The tagline of the README has ${byteLength(description)} bytes, Docker Hub allows ${shortDescriptionLimit}.`,
        )
    }
    return description
}

/** The closing part with the link to the README on GitHub and the sections left out. */
export function createFooter(omitted) {
    const links = omitted.map((chunk) => `[${chunk.title}](${readmeUrl}#${chunk.slug})`)
    const list =
        links.length > 1 ? `${links.slice(0, -1).join(', ')} and ${links.at(-1)}` : links.join('')
    const covers = list ? `, which also covers ${list}` : ''
    return `---\n\nThis description is a part of the [README on GitHub](${readmeUrl}#readme)${covers}.\n`
}

/**
 * Creates the Docker Hub copy of a README. Returns the full description, the short description and
 * the chunks left out. exists(filePath) tells whether a file of the repository exists.
 */
export function createDockerHubDescription(markdown, options = {}) {
    const {
        sections: wanted = dockerHubSections,
        limit = fullDescriptionLimit,
        exists = () => true,
    } = options
    const missing = new Set()
    const convert = (text) =>
        convertLinks(text, (filePath) => {
            if (!exists(filePath)) {
                missing.add(filePath)
            }
        })

    const shortDescription = findShortDescription(markdown)
    const { intro, sections } = splitSections(markdown)
    // Every part ends with one blank line, so the parts stay apart whichever are left out.
    const asPart = (text) => text.replace(/\s*$/, '\n\n')
    const candidates = sections
        .filter((section) => wanted.includes(section.title))
        .flatMap((section) => section.chunks)
        .map((chunk) => ({ ...chunk, text: asPart(convert(chunk.text)) }))
    const alwaysOmitted = sections
        .filter((section) => !wanted.includes(section.title))
        .map((section) => section.chunks[0])

    // The footer can only get shorter than this, so the chunks get the rest of the limit.
    const footerBudget = byteLength(createFooter([...alwaysOmitted, ...candidates]))
    const parts = [asPart(convert(intro))]
    let used = byteLength(parts[0])
    if (used + footerBudget > limit) {
        throw new Error(
            `The part of the README before its first section does not fit into ${limit} bytes.`,
        )
    }
    const omittedChunks = []
    for (const chunk of candidates) {
        const size = byteLength(chunk.text)
        if (used + size + footerBudget <= limit) {
            parts.push(chunk.text)
            used += size
        } else {
            omittedChunks.push(chunk)
        }
    }
    if (missing.size > 0) {
        throw new Error(`The README links to files that do not exist: ${[...missing].join(', ')}.`)
    }

    // A section left out completely is named once, by its ## heading, otherwise its left out
    // subsections are named.
    const omittedSlugs = new Set(omittedChunks.map((chunk) => chunk.slug))
    const omitted = sections.flatMap((section) => {
        const left = section.chunks.filter((chunk) => omittedSlugs.has(chunk.slug))
        if (!wanted.includes(section.title) || left.length === section.chunks.length) {
            return [section.chunks[0]]
        }
        return left
    })
    const fullDescription = parts.join('') + createFooter(omitted)
    return { fullDescription, shortDescription, omitted: omitted.map((chunk) => chunk.title) }
}

function parseArguments(argv) {
    const result = { output: null }
    for (let index = 0; index < argv.length; index++) {
        if (argv[index] === '--output' && argv[index + 1]) {
            result.output = argv[++index]
        } else {
            throw new Error(
                `Unknown argument ${argv[index]}. Usage: dockerhub-description.mjs [--output <file>]`,
            )
        }
    }
    return result
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    try {
        const { output } = parseArguments(process.argv.slice(2))
        const readme = readFileSync(path.join(repositoryRoot, 'README.md'), 'utf8')
        const result = createDockerHubDescription(readme, {
            exists: (filePath) => existsSync(path.join(repositoryRoot, filePath)),
        })
        if (output) {
            writeFileSync(output, result.fullDescription)
        } else {
            process.stdout.write(result.fullDescription)
        }
        if (process.env.GITHUB_OUTPUT) {
            appendFileSync(
                process.env.GITHUB_OUTPUT,
                `short-description=${result.shortDescription}\n`,
            )
        }
        console.error(
            `Docker Hub description: ${byteLength(result.fullDescription)} of ${fullDescriptionLimit} bytes, ` +
                `short description: ${byteLength(result.shortDescription)} of ${shortDescriptionLimit} bytes.`,
        )
        console.error(
            `Left out (linked to the README on GitHub): ${result.omitted.join(', ') || 'nothing'}.`,
        )
    } catch (error) {
        console.error(`::error::${error.message}`)
        process.exit(1)
    }
}
