import {
    expect,
    test,
    type Locator,
    type Page,
    type Route,
} from "@playwright/test";
import {
    adminHeaders,
    findAlbumId,
    findTrackId,
    uniqueSuffix,
} from "./helpers/api";

// Phones leave out the column artist of the song lists; the artist of a song stands below its
// title (list-subtitle) wherever the list does not name it otherwise: in compilations, for songs
// of other artists than the album and in playlists.
test.skip(
    ({ isMobile }) => !isMobile,
    "The column artist is left out on phones only",
);

const guest = {
    id: "00000000-0000-4000-8000-000000000120",
    name: "Guest Singer",
};

function rows(page: Page): Locator {
    return page.locator("[data-part='app'] [data-part='list-row']");
}

function subtitle(row: Locator): Locator {
    return row.locator("[data-column='title'] > [data-part='list-subtitle']");
}

/** Answers the album as a compilation, and its second song by another artist. */
async function asCompilation(page: Page): Promise<void> {
    const change = async (
        route: Route,
        edit: (body: Record<string, unknown>) => void,
    ) => {
        const response = await route.fetch();
        const body = (await response.json()) as Record<string, unknown>;
        edit(body);
        await route.fulfill({ response, json: body });
    };

    await page.route(/\/api\/v1\/albums\/[^/?]+$/, (route) =>
        change(route, (album) => {
            album.isCompilation = true;
            album.artist = null;
        }),
    );
    await page.route(/\/api\/v1\/albums\/[^/?]+\/tracks(\?|$)/, (route) =>
        change(route, (list) => {
            const items = list.items as Record<string, unknown>[];
            items.forEach((track, index) => {
                const album = track.album as Record<string, unknown> | null;
                if (album) {
                    album.isCompilation = true;
                    album.artist = null;
                }
                if (index === 1) {
                    track.artist = guest;
                }
            });
        }),
    );
}

/** The subtitle shows below the title label, inside its row and cut off instead of wrapping. */
async function expectBelowTitle(row: Locator, artist: string): Promise<void> {
    const line = subtitle(row);
    await expect(line).toBeVisible();
    await expect(line).toHaveText(artist);
    const title = await row
        .locator("[data-column='title'] > [data-part='label']")
        .boundingBox();
    const below = await line.boundingBox();
    const box = await row.boundingBox();
    expect(below!.y).toBeGreaterThanOrEqual(title!.y + title!.height - 1);
    expect(below!.y + below!.height).toBeLessThanOrEqual(
        box!.y + box!.height + 0.5,
    );
    await expect(line).toHaveCSS("white-space", "nowrap");
}

test("names no artist below the titles of an album by one artist", async ({
    page,
    request,
}) => {
    await page.goto(
        `/library/albums/${await findAlbumId(request, "E2E Album")}`,
    );
    await expect(rows(page)).toHaveCount(2);

    for (const row of await rows(page).all()) {
        await expect(subtitle(row)).toBeHidden();
    }
    await expect(
        page.locator("[data-part='app'] [data-column='artist']").first(),
    ).toBeHidden();
});

test("names the artist below the title of every song of a compilation", async ({
    page,
    request,
}) => {
    await asCompilation(page);
    await page.goto(
        `/library/albums/${await findAlbumId(request, "E2E Album")}`,
    );
    await expect(rows(page)).toHaveCount(2);

    await expectBelowTitle(rows(page).nth(0), "E2E Artist");
    await expectBelowTitle(rows(page).nth(1), "Guest Singer");
    const overflow = await page
        .locator("[data-part='app'] > [data-part='content']")
        .evaluate((content) => content.scrollWidth - content.clientWidth);
    expect(overflow).toBe(0);
});

test("names the artist below the title of every entry of a playlist", async ({
    page,
    request,
}) => {
    const created = await request.post("/api/v1/me/playlists", {
        data: {
            name: `Artists ${uniqueSuffix()}`,
            trackIds: [
                await findTrackId(request, "First Song"),
                await findTrackId(request, "Loose Song"),
            ],
        },
        headers: await adminHeaders(request),
    });
    expect(created.status()).toBe(201);
    const { id } = await created.json();

    try {
        await page.goto(`/playlists/${id}`);
        await expect(rows(page)).toHaveCount(2);
        await expectBelowTitle(rows(page).nth(0), "E2E Artist");
        await expectBelowTitle(rows(page).nth(1), "Loose Artist");
    } finally {
        await request.delete(`/api/v1/me/playlists/${id}`, {
            headers: await adminHeaders(request),
        });
    }
});
