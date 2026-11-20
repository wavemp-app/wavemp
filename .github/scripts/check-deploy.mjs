// Check of the templates for the app catalogs in deploy/, used locally and in CI.
//
// Usage (from any directory):
//   node .github/scripts/check-deploy.mjs [--release <1.0.N>] [--digest <sha256:...>]
//
// The templates are raised by hand to the version of every milestone release (see deploy/README.md).
// This check makes sure that they stay installable and consistent:
// - every image has a fixed version, never latest, edge or only a major version,
// - all templates use the same version of WaveMP and the same version of PostgreSQL, whose major
//   version matches docker-compose.yml (pg_dump in the image must match the server),
// - the version fields of the catalogs (app_version of TrueNAS, version of CasaOS) name that version,
// - the Unraid templates are well-formed XML,
// - every address of a file of this repository (icons, screenshots, template URLs) points to a file
//   that exists,
// - the icon and the screenshots in the CasaOS folder are exact copies of their sources in assets/.
// With --release (used by the release workflow, release.yml) every template must name exactly this
// version of WaveMP. With --digest every pinned digest of WaveMP must be this digest of the published
// image, and at least one template (TrueNAS) must pin it.
// Exits with 0 if everything is fine, otherwise with 1 (also for unknown or invalid arguments).

import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

export const scriptsDirectory = path.dirname(fileURLToPath(import.meta.url))
export const repositoryRoot = path.resolve(scriptsDirectory, '..', '..')

/** Repositories of the WaveMP image (Docker Hub and GHCR). */
export const wavempRepositories = new Set(['wavemp/wavemp', 'ghcr.io/wavemp-app/wavemp'])
/** Repositories of the PostgreSQL image. */
export const postgresRepositories = new Set([
    'postgres',
    'docker.io/library/postgres',
    'library/postgres',
])

/** Files of the CasaOS app folder and the files in assets/ they are copies of. */
export const casaosAssetSources = {
    'icon.svg': 'assets/logo/wavemp-logo-default.svg',
    'icon.png': 'assets/logo/wavemp-logo-default-512.png',
    'screenshot-1.png': 'assets/screenshots/catalogs/home.png',
    'screenshot-2.png': 'assets/screenshots/catalogs/now-playing.png',
    'screenshot-3.png': 'assets/screenshots/catalogs/album.png',
}

/** Addresses of files of this repository on its default branch. */
const repositoryFileUrlPattern =
    /https:\/\/raw\.githubusercontent\.com\/wavemp-app\/wavemp\/main\/([^\s"'<>)]+)/g
const digestPattern = /^sha256:[0-9a-f]{64}$/
const wavempVersionPattern = /^\d+\.\d+\.\d+$/
// PostgreSQL with major and minor version, optionally with a variant, e.g. 18.7 or 18.7-trixie.
const postgresVersionPattern = /^(\d+)\.\d+(?:-[0-9a-z.]+)?$/
// A tag with at least major and minor version (images of the catalogs, e.g. their helpers).
const fixedVersionPattern = /^v?\d+\.\d+/

/** The version of a release (1.0.N), also given as its tag (v1.0.N). Throws for anything else. */
export function parseReleaseVersion(value) {
    const version = String(value ?? '').replace(/^v/, '')
    if (!wavempVersionPattern.test(version)) {
        throw new Error(`The release "${value}" is not a version of the form 1.0.N or v1.0.N.`)
    }
    return version
}

/** Reads the options of the command line: --release <1.0.N> and --digest <sha256:...>. */
export function parseArguments(argv) {
    const options = { release: null, digest: null }
    for (let index = 0; index < argv.length; index += 2) {
        const name = argv[index]
        const value = argv[index + 1]
        if (name === '--release' && value !== undefined) {
            options.release = parseReleaseVersion(value)
        } else if (name === '--digest' && value !== undefined) {
            if (!digestPattern.test(value)) {
                throw new Error(`The digest "${value}" is not of the form sha256:<64 hex digits>.`)
            }
            options.digest = value
        } else {
            throw new Error(
                `Unknown argument ${name}. Usage: check-deploy.mjs [--release <1.0.N>] [--digest <sha256:...>]`,
            )
        }
    }
    return options
}

/**
 * Splits an image reference (repository:tag@digest) into its parts. A port in the registry
 * (host:5000/name) is not mistaken for a tag.
 */
export function parseImage(reference) {
    let rest = reference.trim()
    let digest = null
    const at = rest.indexOf('@')
    if (at >= 0) {
        digest = rest.slice(at + 1)
        rest = rest.slice(0, at)
    }
    let tag = null
    const colon = rest.lastIndexOf(':')
    if (colon > rest.lastIndexOf('/')) {
        tag = rest.slice(colon + 1)
        rest = rest.slice(0, colon)
    }
    return { repository: rest, tag, digest }
}

/** Lists the files below a directory, with paths relative to the repository root. */
export function listFiles(root, directory) {
    const result = []
    const absolute = path.join(root, directory)
    if (!existsSync(absolute)) {
        return result
    }
    for (const entry of readdirSync(absolute)) {
        const relative = path.posix.join(directory, entry)
        if (statSync(path.join(root, relative)).isDirectory()) {
            result.push(...listFiles(root, relative))
        } else {
            result.push(relative)
        }
    }
    return result.sort()
}

/**
 * Finds the image references in a template: image: lines of compose files, repository and tag of
 * the TrueNAS values and the Repository element of Unraid templates.
 */
export function findImages(file, content) {
    const images = []
    const add = (reference, line) => images.push({ file, line, ...parseImage(reference) })
    const lineOf = (index) => content.slice(0, index).split('\n').length

    // [ \t] instead of \s, so that a key without value (images: / image:) never reaches into the
    // next line.
    for (const match of content.matchAll(/^[ \t]*image:[ \t]*["']?([^"'\s#]+)["']?/gm)) {
        add(match[1], lineOf(match.index))
    }
    for (const match of content.matchAll(
        /^[ \t]*repository:[ \t]*["']?([^"'\s#]+)["']?[ \t]*\r?\n[ \t]*tag:[ \t]*["']?([^"'\s#]+)["']?/gm,
    )) {
        add(`${match[1]}:${match[2]}`, lineOf(match.index))
    }
    for (const match of content.matchAll(/<Repository>([^<]+)<\/Repository>/g)) {
        add(match[1], lineOf(match.index))
    }
    return images
}

/**
 * Checks that an XML document is well-formed: one root element, matching tags, quoted attributes
 * and only known or numeric entities. Returns the problem or null.
 */
export function findXmlProblem(content) {
    const stack = []
    let roots = 0
    let index = 0
    const checkEntities = (text) => {
        for (const match of text.matchAll(/&([^;\s]*);?/g)) {
            if (
                !/^(?:amp|lt|gt|quot|apos|#\d+|#x[0-9A-Fa-f]+)$/.test(match[1]) ||
                !match[0].endsWith(';')
            ) {
                return `invalid entity "${match[0]}"`
            }
        }
        return null
    }
    while (index < content.length) {
        const open = content.indexOf('<', index)
        const text = content.slice(index, open < 0 ? content.length : open)
        if (text.trim() && stack.length === 0) {
            return 'text outside of the root element'
        }
        const entityProblem = checkEntities(text)
        if (entityProblem) {
            return entityProblem
        }
        if (open < 0) {
            break
        }
        if (content.startsWith('<?', open)) {
            const end = content.indexOf('?>', open)
            if (end < 0) {
                return 'unterminated processing instruction'
            }
            index = end + 2
            continue
        }
        if (content.startsWith('<!--', open)) {
            const end = content.indexOf('-->', open)
            if (end < 0) {
                return 'unterminated comment'
            }
            index = end + 3
            continue
        }
        const close = content.indexOf('>', open)
        if (close < 0) {
            return 'unterminated tag'
        }
        const tag = content.slice(open + 1, close)
        if (tag.startsWith('/')) {
            const name = tag.slice(1).trim()
            const expected = stack.pop()
            if (name !== expected) {
                return `closing tag </${name}> does not match <${expected ?? ''}>`
            }
        } else {
            const selfClosing = tag.endsWith('/')
            const body = selfClosing ? tag.slice(0, -1) : tag
            const nameMatch = /^([A-Za-z_][\w.-]*)/.exec(body)
            if (!nameMatch) {
                return `invalid tag <${tag}>`
            }
            const attributes = body.slice(nameMatch[1].length)
            if (!/^(?:\s+[A-Za-z_][\w.:-]*\s*=\s*(?:"[^"<]*"|'[^'<]*'))*\s*$/.test(attributes)) {
                return `invalid attributes in <${nameMatch[1]}>`
            }
            const attributeProblem = checkEntities(attributes)
            if (attributeProblem) {
                return attributeProblem
            }
            if (stack.length === 0) {
                roots += 1
                if (roots > 1) {
                    return 'more than one root element'
                }
            }
            if (!selfClosing) {
                stack.push(nameMatch[1])
            }
        }
        index = close + 1
    }
    if (stack.length > 0) {
        return `element <${stack.at(-1)}> is not closed`
    }
    return roots === 1 ? null : 'no root element'
}

/** Reads the major version of PostgreSQL from docker-compose.yml (image postgres:<major>). */
export function readPostgresMajor(root) {
    const file = path.join(root, 'docker-compose.yml')
    if (!existsSync(file)) {
        return null
    }
    const image = findImages('docker-compose.yml', readFileSync(file, 'utf8')).find((candidate) =>
        postgresRepositories.has(candidate.repository),
    )
    return image?.tag ? (/^(\d+)/.exec(image.tag)?.[1] ?? null) : null
}

/**
 * Checks all templates below deploy/ and returns the problems found, as readable lines.
 * options.release is the version the templates must name (1.0.N), options.digest the digest of its
 * published image (see parseArguments).
 */
export function findDeployProblems(root = repositoryRoot, options = {}) {
    const { release = null, digest = null } = options
    const problems = []
    const files = listFiles(root, 'deploy')
    if (files.length === 0) {
        return ['deploy/ contains no templates']
    }
    const textFiles = files.filter((file) => /\.(?:ya?ml|xml|md)$/i.test(file))
    const contents = new Map(
        textFiles.map((file) => [file, readFileSync(path.join(root, file), 'utf8')]),
    )

    const images = textFiles
        .filter((file) => !file.endsWith('.md'))
        .flatMap((file) => findImages(file, contents.get(file)))
    const wavempVersions = new Set()
    const postgresVersions = new Set()
    let pinnedWavempDigests = 0
    for (const image of images) {
        const where = `${image.file}:${image.line}`
        if (image.digest !== null && !digestPattern.test(image.digest)) {
            problems.push(`${where}: invalid digest "${image.digest}" of ${image.repository}`)
        }
        if (!image.tag) {
            problems.push(`${where}: ${image.repository} has no tag; use a fixed version`)
            continue
        }
        if (wavempRepositories.has(image.repository)) {
            if (!wavempVersionPattern.test(image.tag)) {
                problems.push(
                    `${where}: ${image.repository}:${image.tag} is not a fixed version (1.0.N)`,
                )
            }
            wavempVersions.add(image.tag)
            if (image.digest !== null) {
                pinnedWavempDigests += 1
                if (digest && image.digest !== digest) {
                    problems.push(
                        `${where}: ${image.repository}:${image.tag} pins ${image.digest}, but the published image is ${digest}`,
                    )
                }
            }
        } else if (postgresRepositories.has(image.repository)) {
            if (!postgresVersionPattern.test(image.tag)) {
                problems.push(
                    `${where}: ${image.repository}:${image.tag} is not a fixed version (major.minor)`,
                )
            }
            postgresVersions.add(image.tag.split('-')[0])
        } else if (!fixedVersionPattern.test(image.tag)) {
            problems.push(`${where}: ${image.repository}:${image.tag} is not a fixed version`)
        }
    }

    if (wavempVersions.size === 0) {
        problems.push('deploy/ contains no WaveMP image')
    } else if (wavempVersions.size > 1) {
        problems.push(
            `deploy/ uses different versions of WaveMP: ${[...wavempVersions].join(', ')}`,
        )
    }
    if (release) {
        for (const version of wavempVersions) {
            if (version !== release) {
                problems.push(`deploy/ uses WaveMP ${version}, but the release is ${release}`)
            }
        }
    }
    if (digest && pinnedWavempDigests === 0) {
        problems.push(`deploy/ pins no digest of WaveMP; pin ${digest} (TrueNAS)`)
    }
    if (postgresVersions.size > 1) {
        problems.push(
            `deploy/ uses different versions of PostgreSQL: ${[...postgresVersions].join(', ')}`,
        )
    }
    const postgresMajor = readPostgresMajor(root)
    for (const version of postgresVersions) {
        if (postgresMajor && version.split('.')[0] !== postgresMajor) {
            problems.push(
                `deploy/ uses PostgreSQL ${version}, but docker-compose.yml uses PostgreSQL ${postgresMajor}`,
            )
        }
    }

    const wavempVersion = wavempVersions.size === 1 ? [...wavempVersions][0] : null
    for (const file of textFiles) {
        const content = contents.get(file)
        if (wavempVersion && /(?:^|\/)app\.yaml$/.test(file)) {
            const appVersion = /^app_version:\s*["']?([^"'\s]+)/m.exec(content)?.[1]
            if (appVersion !== wavempVersion) {
                problems.push(
                    `${file}: app_version ${appVersion ?? '(missing)'} is not ${wavempVersion}`,
                )
            }
        }
        if (wavempVersion && /docker-compose\.ya?ml$/.test(file) && content.includes('x-casaos:')) {
            const version = /^ {2}version:\s*["']?([^"'\s]+)/m.exec(content)?.[1]
            if (version !== wavempVersion) {
                problems.push(
                    `${file}: x-casaos version ${version ?? '(missing)'} is not ${wavempVersion}`,
                )
            }
        }
        if (file.endsWith('.xml')) {
            const xmlProblem = findXmlProblem(content)
            if (xmlProblem) {
                problems.push(`${file}: not well-formed XML: ${xmlProblem}`)
            }
        }
        for (const match of content.matchAll(repositoryFileUrlPattern)) {
            if (!existsSync(path.join(root, match[1]))) {
                problems.push(`${file}: ${match[0]} points to a file that does not exist`)
            }
        }
    }

    for (const file of files.filter((candidate) =>
        /(?:^|\/)deploy\/casaos\/[^/]+\/docker-compose\.yml$/.test(candidate),
    )) {
        const folder = path.posix.dirname(file)
        for (const [name, source] of Object.entries(casaosAssetSources)) {
            const copy = path.join(root, folder, name)
            const original = path.join(root, source)
            if (!existsSync(copy)) {
                problems.push(`${folder}/${name} is missing (copy of ${source})`)
            } else if (!existsSync(original)) {
                problems.push(`${source} is missing (source of ${folder}/${name})`)
            } else if (!readFileSync(copy).equals(readFileSync(original))) {
                problems.push(`${folder}/${name} differs from ${source}; copy it again`)
            }
        }
    }

    return problems
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    let options
    try {
        options = parseArguments(process.argv.slice(2))
    } catch (error) {
        console.error(error.message)
        process.exit(1)
    }
    const problems = findDeployProblems(repositoryRoot, options)
    if (problems.length > 0) {
        for (const problem of problems) {
            console.error(problem)
        }
        process.exit(1)
    }
    console.log(
        options.release
            ? `The templates in deploy/ are consistent and name the release ${options.release}.`
            : 'The templates in deploy/ are consistent.',
    )
}
