// Creates the text of the GitHub release of a milestone from docs/releases/release-m<milestone>.md,
// used by the release workflow (release.yml) and locally.
//
// Usage (from any directory):
//   node .github/scripts/release-notes.mjs --milestone <M> --tag <v1.0.N> [--output <file>]
//
// The release page of GitHub cannot resolve the relative links and images of the release notes, so
// they become absolute addresses of this repository at the tag of the release (pictures on
// raw.githubusercontent.com, files on github.com, anchors in the release notes on github.com); code
// stays as it is. The release notes file itself is never changed.
//
// Without --output the text is written to stdout. Exits with 1 if the release notes do not exist,
// a relative link points to a file that does not exist or outside of the repository, or the
// arguments are invalid.

import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { convertLinks, readmeUrl } from './dockerhub-description.mjs'

export const scriptsDirectory = path.dirname(fileURLToPath(import.meta.url))
export const repositoryRoot = path.resolve(scriptsDirectory, '..', '..')
export const releaseNotesDirectory = 'docs/releases'

const tagPattern = /^v\d+\.\d+\.\d+$/
const milestonePattern = /^[1-9]\d*$/

/** The path of the release notes of a milestone, relative to the repository root. */
export function releaseNotesPath(milestone) {
    return `${releaseNotesDirectory}/release-m${milestone}.md`
}

/**
 * The text of the release: the release notes with absolute addresses at the tag. exists tells
 * whether a file of the repository exists; links to missing files throw.
 */
export function createReleaseNotes(markdown, { milestone, tag, exists = () => true }) {
    const missing = []
    const text = convertLinks(
        markdown,
        (filePath) => {
            if (!exists(filePath)) {
                missing.push(filePath)
            }
        },
        {
            directory: releaseNotesDirectory,
            ref: tag,
            page: `${readmeUrl}/blob/${tag}/${releaseNotesPath(milestone)}`,
        },
    )
    if (missing.length > 0) {
        throw new Error(
            `${releaseNotesPath(milestone)} links to files that do not exist: ${missing.join(', ')}`,
        )
    }
    return text
}

/** Reads the options of the command line: --milestone, --tag and the optional --output. */
export function parseArguments(argv) {
    const options = { milestone: null, tag: null, output: null }
    for (let index = 0; index < argv.length; index += 2) {
        const name = argv[index]
        const value = argv[index + 1]
        if (name === '--milestone' && value !== undefined && milestonePattern.test(value)) {
            options.milestone = value
        } else if (name === '--tag' && value !== undefined && tagPattern.test(value)) {
            options.tag = value
        } else if (name === '--output' && value !== undefined) {
            options.output = value
        } else {
            throw new Error(`Unknown or invalid argument ${name} ${value ?? ''}`.trim())
        }
    }
    if (!options.milestone || !options.tag) {
        throw new Error(
            'Usage: release-notes.mjs --milestone <M> --tag <v1.0.N> [--output <file>]',
        )
    }
    return options
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    try {
        const { milestone, tag, output } = parseArguments(process.argv.slice(2))
        const file = path.join(repositoryRoot, releaseNotesPath(milestone))
        if (!existsSync(file)) {
            throw new Error(`${releaseNotesPath(milestone)} does not exist.`)
        }
        const text = createReleaseNotes(readFileSync(file, 'utf8'), {
            milestone,
            tag,
            exists: (filePath) => existsSync(path.join(repositoryRoot, filePath)),
        })
        if (output) {
            writeFileSync(output, text)
        } else {
            process.stdout.write(text)
        }
    } catch (error) {
        console.error(`::error::${error.message}`)
        process.exit(1)
    }
}
