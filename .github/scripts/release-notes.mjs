// Creates the text of the GitHub release of a milestone from docs/releases/release-m<milestone>.md,
// used by the release workflow (release.yml) and locally, and pins the links of released notes to
// their tag.
//
// Usage (from any directory):
//   node .github/scripts/release-notes.mjs --milestone <M> --tag <v1.0.N> [--output <file>]
//   node .github/scripts/release-notes.mjs --pin [--milestone <M>]
//
// The release page of GitHub cannot resolve the relative links and images of the release notes, so
// they become absolute addresses of this repository at the tag of the release (pictures on
// raw.githubusercontent.com, files on github.com, anchors in the release notes on github.com); code
// stays as it is. Absolute addresses of this repository at a branch (for example .../main/...)
// are moved to the tag as well, so no picture of a release breaks when a file is renamed on main
// later. Addresses at a tag or a commit stay as they are. The release notes file itself is not
// changed.
//
// --pin changes the release notes files themselves: every released milestone (or only <M>) gets
// the same absolute addresses at the tag of its release, the first tag (v1.0.N, in the order of
// the versions) whose commit contains its release notes; anchors into the same file stay as they
// are. Release notes without a release are left as they are. Needs git and the tags of the
// repository (git fetch --tags).
//
// Without --output the text is written to stdout. Exits with 1 if the release notes do not exist,
// a link points to a file that does not exist (with --pin: at the tag) or outside of the
// repository, or the arguments are invalid.

import { execFileSync } from 'node:child_process'
import { existsSync, readdirSync, readFileSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import {
    absoluteUrl,
    convertTargets,
    githubRepository,
    readmeUrl,
} from './dockerhub-description.mjs'

export const scriptsDirectory = path.dirname(fileURLToPath(import.meta.url))
export const repositoryRoot = path.resolve(scriptsDirectory, '..', '..')
export const releaseNotesDirectory = 'docs/releases'

export const tagPattern = /^v\d+\.\d+\.\d+$/
const commitPattern = /^[0-9a-f]{40}$/
const milestonePattern = /^[1-9]\d*$/
const releaseNotesPattern = /^release-m([1-9]\d*)\.md$/

const escapedRepository = githubRepository.replace(/[.]/g, '\\.')
/** Addresses of files of this repository: prefix, ref (branch, tag or commit), path and suffix. */
const repositoryUrlPattern = new RegExp(
    `^(https://raw\\.githubusercontent\\.com/${escapedRepository}/|https://github\\.com/${escapedRepository}/(?:blob|tree|raw)/)([^/?#]+)/([^?#]*)(.*)$`,
)

/** The path of the release notes of a milestone, relative to the repository root. */
export function releaseNotesPath(milestone) {
    return `${releaseNotesDirectory}/release-m${milestone}.md`
}

/**
 * Splits an absolute address of a file of this repository on GitHub into prefix, ref, file path
 * and suffix (query and anchor); null for every other address.
 */
export function parseRepositoryUrl(target) {
    const match = repositoryUrlPattern.exec(target)
    if (!match) {
        return null
    }
    const [, prefix, ref, filePath, suffix] = match
    return { prefix, ref, filePath, suffix }
}

/** Whether a ref of an address names a fixed state of the repository: a release tag or a commit. */
export function isFixedRef(ref) {
    return tagPattern.test(ref) || commitPattern.test(ref)
}

/**
 * The addresses of this repository in Markdown that point to a branch instead of a tag or a
 * commit, outside of code.
 */
export function findBranchLinks(markdown) {
    const links = []
    convertTargets(markdown, (target) => {
        const parsed = parseRepositoryUrl(target)
        if (parsed && !isFixedRef(parsed.ref)) {
            links.push(target)
        }
        return null
    })
    return links
}

/**
 * A converter of link targets to addresses at the tag: relative paths like absoluteUrl, addresses
 * of this repository at a branch moved to the tag. keepAnchors keeps anchors into the same file
 * (#section) as they are instead of pointing them to the release notes on GitHub. The path of every
 * linked file is passed to onFile.
 */
function toTag({ milestone, tag, onFile, keepAnchors }) {
    const location = {
        directory: releaseNotesDirectory,
        ref: tag,
        page: `${readmeUrl}/blob/${tag}/${releaseNotesPath(milestone)}`,
    }
    return (target) => {
        if (keepAnchors && target.startsWith('#')) {
            return null
        }
        const parsed = parseRepositoryUrl(target)
        if (parsed) {
            if (isFixedRef(parsed.ref)) {
                return null
            }
            onFile(decodeURIComponent(parsed.filePath.replace(/\/$/, '')))
            return `${parsed.prefix}${tag}/${parsed.filePath}${parsed.suffix}`
        }
        return absoluteUrl(target, onFile, location)
    }
}

/** Converts the links of release notes with toTag and throws if a linked file does not exist. */
function convertToTag(markdown, { milestone, tag, exists, keepAnchors, where }) {
    const missing = new Set()
    const onFile = (filePath) => {
        if (!exists(filePath)) {
            missing.add(filePath)
        }
    }
    const text = convertTargets(markdown, toTag({ milestone, tag, onFile, keepAnchors }))
    if (missing.size > 0) {
        throw new Error(
            `${releaseNotesPath(milestone)} links to files that do not exist${where}: ${[...missing].join(', ')}`,
        )
    }
    return text
}

/**
 * The text of the release: the release notes with absolute addresses at the tag. exists tells
 * whether a file of the repository exists; links to missing files throw.
 */
export function createReleaseNotes(markdown, { milestone, tag, exists = () => true }) {
    return convertToTag(markdown, { milestone, tag, exists, keepAnchors: false, where: '' })
}

/**
 * The release notes with all links into the repository pinned to the tag of their release, for the
 * file itself: like createReleaseNotes, but anchors into the same file stay as they are. exists
 * tells whether a file exists at the tag.
 */
export function pinReleaseNotes(markdown, { milestone, tag, exists = () => true }) {
    return convertToTag(markdown, {
        milestone,
        tag,
        exists,
        keepAnchors: true,
        where: ` at ${tag}`,
    })
}

/** Runs git in the repository and returns its output, or null if it fails. */
function git(args) {
    try {
        return execFileSync('git', args, {
            cwd: repositoryRoot,
            encoding: 'utf8',
            stdio: ['ignore', 'pipe', 'ignore'],
        })
    } catch {
        return null
    }
}

/** Whether a file exists at a ref of the repository. */
export function existsAt(ref, filePath) {
    return git(['cat-file', '-e', `${ref}:${filePath}`]) !== null
}

/**
 * The release tags (v1.0.N) of the repository in the order of their versions. Throws if git cannot
 * list them.
 */
export function releaseTags() {
    const output = git(['tag', '--list', 'v*', '--sort=v:refname'])
    if (output === null) {
        throw new Error('git cannot list the tags of the repository.')
    }
    return output.split('\n').filter((tag) => tagPattern.test(tag))
}

/**
 * The tag of the release of a milestone: the first of the tags (in the order of their versions)
 * that contains its release notes, or null if it is not released yet.
 */
export function findReleaseTag(milestone, tags, exists = existsAt) {
    return tags.find((tag) => exists(tag, releaseNotesPath(milestone))) ?? null
}

/** The milestones with release notes in docs/releases, in ascending order. */
export function releaseNotesMilestones(
    directory = path.join(repositoryRoot, releaseNotesDirectory),
) {
    return readdirSync(directory)
        .map((name) => releaseNotesPattern.exec(name)?.[1])
        .filter(Boolean)
        .sort((a, b) => Number(a) - Number(b))
}

/** Reads the options of the command line: --milestone, --tag, --output and --pin. */
export function parseArguments(argv) {
    const options = { milestone: null, tag: null, output: null, pin: false }
    for (let index = 0; index < argv.length; index += 2) {
        const name = argv[index]
        const value = argv[index + 1]
        if (name === '--pin') {
            options.pin = true
            index -= 1
        } else if (name === '--milestone' && value !== undefined && milestonePattern.test(value)) {
            options.milestone = value
        } else if (name === '--tag' && value !== undefined && tagPattern.test(value)) {
            options.tag = value
        } else if (name === '--output' && value !== undefined) {
            options.output = value
        } else {
            throw new Error(`Unknown or invalid argument ${name} ${value ?? ''}`.trim())
        }
    }
    const usage =
        'Usage: release-notes.mjs --milestone <M> --tag <v1.0.N> [--output <file>] | --pin [--milestone <M>]'
    if (options.pin ? options.tag || options.output : !options.milestone || !options.tag) {
        throw new Error(usage)
    }
    return options
}

/** Pins the release notes of the released milestones (or of one) to their tags, in place. */
function pinFiles(onlyMilestone) {
    const tags = releaseTags()
    const milestones = onlyMilestone ? [onlyMilestone] : releaseNotesMilestones()
    for (const milestone of milestones) {
        const file = path.join(repositoryRoot, releaseNotesPath(milestone))
        if (!existsSync(file)) {
            throw new Error(`${releaseNotesPath(milestone)} does not exist.`)
        }
        const tag = findReleaseTag(milestone, tags)
        if (!tag) {
            console.error(`${releaseNotesPath(milestone)}: not released yet, left as it is.`)
            continue
        }
        const markdown = readFileSync(file, 'utf8')
        const pinned = pinReleaseNotes(markdown, {
            milestone,
            tag,
            exists: (filePath) => existsAt(tag, filePath),
        })
        if (pinned === markdown) {
            console.error(`${releaseNotesPath(milestone)}: pinned to ${tag} already.`)
        } else {
            writeFileSync(file, pinned)
            console.error(`${releaseNotesPath(milestone)}: links pinned to ${tag}.`)
        }
    }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    try {
        const { milestone, tag, output, pin } = parseArguments(process.argv.slice(2))
        if (pin) {
            pinFiles(milestone)
        } else {
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
        }
    } catch (error) {
        console.error(`::error::${error.message}`)
        process.exit(1)
    }
}
