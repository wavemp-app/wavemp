// The cover of a playlist without a picture of its own: a 2x2 grid of song covers whose tiles
// change one at a time. These pure helpers decide what each tile shows, which tile changes next,
// from which outer edge the new cover slides in and when; PlaylistCover.vue draws it.

/** Number of tiles of the grid, in reading order: top start, top end, bottom start, bottom end. */
export const coverTileCount = 4

/**
 * Edge a new cover slides in from, as logical directions: `start` and `end` follow the writing
 * direction (left and right in English), `top` and `bottom` are the block edges.
 */
export type CoverEdge = 'start' | 'end' | 'top' | 'bottom'

/** The outer edges of each tile: a new cover always comes from outside the grid. */
const tileEdges: readonly (readonly [CoverEdge, CoverEdge])[] = [
    ['start', 'top'],
    ['end', 'top'],
    ['start', 'bottom'],
    ['end', 'bottom'],
]

/** The two outer edges of a tile of the grid. */
export function edgesOf(tile: number): readonly [CoverEdge, CoverEdge] {
    return tileEdges[tile] ?? tileEdges[0]!
}

/**
 * The covers of the four tiles at the start: the first four, or with fewer covers each of them
 * repeated on the diagonal, so equal covers never stand next to each other where it can be
 * avoided (two covers: AB/BA, three: AB/CA).
 */
export function initialTiles(trackIds: readonly string[]): string[] {
    const count = trackIds.length
    if (count === 0) {
        return []
    }

    const pattern =
        count >= coverTileCount ? [0, 1, 2, 3] : count === 3 ? [0, 1, 2, 0] : [0, 1, 1, 0]
    return pattern.map((index) => trackIds[index % count]!)
}

/** What the grid shows and which covers wait for their turn. */
export interface CoverRotation {
    /** Track of the cover of each tile. */
    readonly tiles: readonly string[]
    /** Covers not shown, the one waiting longest first. */
    readonly waiting: readonly string[]
    /** Tile that changed last; it is not chosen again right away. */
    readonly lastTile: number | null
}

export function startRotation(trackIds: readonly string[]): CoverRotation {
    const tiles = initialTiles(trackIds)
    return {
        tiles,
        waiting: trackIds.filter((id) => !tiles.includes(id)),
        lastTile: null,
    }
}

/** Whether the grid has covers to change at all: more covers than tiles. */
export function canRotate(rotation: CoverRotation): boolean {
    return rotation.waiting.length > 0
}

/** One change of the grid: the cover that slides into a tile and the edge it comes from. */
export interface CoverChange {
    readonly tile: number
    readonly trackId: string
    readonly from: CoverEdge
}

/**
 * The next change: the cover waiting longest slides into a random tile from one of its two outer
 * edges, chosen at random. Not the tile that changed last and none that is still changing
 * (`busy`); null without a waiting cover or a free tile. `random` returns numbers in [0, 1).
 * The cover leaves the queue while its picture loads; applyChange completes the change, skipChange
 * drops it.
 */
export function planChange(
    rotation: CoverRotation,
    random: () => number,
    busy: ReadonlySet<number> = new Set(),
): PlannedChange | null {
    const trackId = rotation.waiting[0]
    if (trackId === undefined) {
        return null
    }

    const free = rotation.tiles
        .map((_, index) => index)
        .filter((index) => index !== rotation.lastTile && !busy.has(index))
    if (free.length === 0) {
        return null
    }

    const tile = free[pick(random, free.length)]!
    return {
        change: { tile, trackId, from: edgesOf(tile)[pick(random, 2)]! },
        rotation: { ...rotation, waiting: rotation.waiting.slice(1), lastTile: tile },
    }
}

/** A planned change and the grid meanwhile. */
export interface PlannedChange {
    readonly change: CoverChange
    readonly rotation: CoverRotation
}

/** The grid after a planned change: the tile shows the new cover, the replaced one waits at the end. */
export function applyChange(rotation: CoverRotation, change: CoverChange): CoverRotation {
    const replaced = rotation.tiles[change.tile]
    const waiting = rotation.waiting.filter((id) => id !== change.trackId)
    return {
        ...rotation,
        tiles: rotation.tiles.map((id, index) => (index === change.tile ? change.trackId : id)),
        waiting: replaced === undefined ? waiting : [...waiting, replaced],
    }
}

/** The grid after a planned change that could not happen (the cover did not load): it waits again. */
export function skipChange(rotation: CoverRotation, change: CoverChange): CoverRotation {
    return {
        ...rotation,
        waiting: [...rotation.waiting.filter((id) => id !== change.trackId), change.trackId],
    }
}

/** Time between two changes of one cover: calm, a little different each time. */
export const coverChangeMilliseconds = { min: 5000, spread: 4000 } as const

/** Time until the first change of a cover, so covers shown together never change in step. */
export const firstCoverChangeMilliseconds = { min: 3000, spread: 6000 } as const

/**
 * Shortest time between two changes on the whole page: with many playlists on screen only one
 * tile changes at a time.
 */
export const coverChangeGapMilliseconds = 1500

/**
 * How long a tile waits for the picture of its new cover, and for the end of its slide, before
 * it gives up or finishes the change anyway (a theme without the animation never reports its end).
 */
export const coverLoadTimeoutMilliseconds = 8000
export const coverSlideTimeoutMilliseconds = 4000

export function nextChangeDelay(random: () => number, first = false): number {
    const { min, spread } = first ? firstCoverChangeMilliseconds : coverChangeMilliseconds
    return Math.round(min + random() * spread)
}

/**
 * Lets only one change through per gap on the whole page. A cover asks before it changes a tile;
 * when another change was too recent, it learns how long to wait.
 */
export class CoverChangeGate {
    private last = Number.NEGATIVE_INFINITY

    constructor(private readonly gap: number = coverChangeGapMilliseconds) {}

    /** 0 if the change may happen now (and claims the slot), otherwise the time left to wait. */
    claim(now: number): number {
        const wait = this.last + this.gap - now
        if (wait > 0) {
            return wait
        }

        this.last = now
        return 0
    }
}

/** The gate shared by all playlist covers of the page. */
export const coverChangeGate = new CoverChangeGate()

/**
 * Name of the flag on `window` that keeps every playlist cover still. The automated screenshots
 * (tests/WaveMP.E2E/screenshots) set it before the app loads, so the pictures stay the same on
 * every run.
 */
export const stillCoversFlag = '__WAVEMP_STILL_COVERS__'

export function coversStandStill(): boolean {
    return (globalThis as Record<string, unknown>)[stillCoversFlag] === true
}

function pick(random: () => number, count: number): number {
    return Math.min(count - 1, Math.max(0, Math.floor(random() * count)))
}
