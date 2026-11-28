import { expect, test, type Locator, type Page } from "@playwright/test";
import {
    adminHeaders,
    createUser,
    deleteUser,
    findTrackId,
    uniqueSuffix,
    type TestUser,
} from "./helpers/api";
import { homeUrl, profileTile } from "./helpers/navigation";
import { emptyStorageState } from "./helpers/storage";

// The lyrics with the synthetic test media of scripts/generate-media.sh:
//   Loose Song: 20 seconds of audio with "Loose Artist - Loose Song.lrc" next to it, enhanced LRC
//               with lines at 0.5, 5, 10 (a part without singing) and 13 seconds
//   First Song: no lyrics
// The download from the online lyrics service stays switched off: the tests never reach it.

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`);
}

function viewSegment(page: Page, item: string): Locator {
    return page.locator(
        `[data-part='app'] [data-part='stage-actions'] [data-part='segment'][data-item='${item}']`,
    );
}

function lyricsLine(page: Page, index: number): Locator {
    return page.locator(
        `[data-part='app'] [data-part='lyrics'] [data-part='lyrics-line'][data-index='${index}']`,
    );
}

async function openNowPlaying(page: Page): Promise<void> {
    await page
        .locator("[data-part='app'] a[href='/now-playing']")
        .filter({ visible: true })
        .click();
    await expect(page).toHaveURL(/\/now-playing$/);
}

async function playSong(page: Page, id: string, title: string): Promise<void> {
    await page.goto(`/library/tracks/${id}`);
    await part(page, "detail-actions").locator("[data-item='play']").click();
    await expect(part(page, "now-playing-title")).toHaveText(title);
}

function audioPosition(page: Page): Promise<number> {
    return page
        .locator("[data-part='app'] [data-part='seek'] [role='slider']")
        .evaluate((element) => Number(element.getAttribute("aria-valuenow")));
}

test("the API returns the lyrics file of a song with its word times", async ({
    request,
}) => {
    const id = await findTrackId(request, "Loose Song");
    const response = await request.get(`/api/v1/tracks/${id}/lyrics`, {
        headers: await adminHeaders(request),
    });

    expect(response.status()).toBe(200);
    const lyrics = await response.json();
    expect(lyrics.type).toBe("WordSynchronized");
    expect(lyrics.source).toBe("File");
    expect(
        (lyrics.lines as { startMilliseconds: number; text: string }[]).map(
            (line) => [line.startMilliseconds, line.text],
        ),
    ).toEqual([
        [500, "Loose first line"],
        [5000, "Loose second line"],
        [10000, ""],
        [13000, "Loose last line"],
    ]);
    expect(
        (lyrics.lines[0].words as { startMilliseconds: number }[]).map(
            (word) => word.startMilliseconds,
        ),
    ).toEqual([500, 1500, 2500]);

    const firstSong = await findTrackId(request, "First Song");
    const missing = await request.get(`/api/v1/tracks/${firstSong}/lyrics`, {
        headers: await adminHeaders(request),
    });
    expect(missing.status()).toBe(404);
    expect((await missing.json()).code).toBe("lyrics.not_found");
});

test("admins see where the lyrics of a song come from and that the download is off", async ({
    page,
    request,
}) => {
    const id = await findTrackId(request, "Loose Song");
    await page.goto(`/library/tracks/${id}`);

    const section = page.locator(
        "[data-part='app'] [data-part='section'][data-item='lyrics']",
    );
    const status = section.locator(
        "[data-part='form-note'][data-item='lyrics-status']",
    );
    await expect(status).toHaveAttribute("data-state", "found");
    await expect(status).toHaveText(
        "Synchronized word by word, from the lyrics file.",
    );

    await section.locator("[data-item='download-lyrics']").click();
    await expect(section.locator("[data-part='alert']")).toContainText(
        "The download of lyrics is switched off.",
    );
});

test("the API has no lookup results while the download is off and forgets them on request", async ({
    request,
}) => {
    const headers = await adminHeaders(request);
    const summary = await request.get("/api/v1/lyrics-lookups/summary", {
        headers,
    });
    expect(summary.status()).toBe(200);
    expect(await summary.json()).toEqual({
        notFoundCount: 0,
        instrumentalCount: 0,
        lookUpAgainAfterMinutes: 43_200,
    });

    const id = await findTrackId(request, "First Song");
    const lookup = await request.get(`/api/v1/tracks/${id}/lyrics/lookup`, {
        headers,
    });
    expect(lookup.status()).toBe(404);
    expect((await lookup.json()).code).toBe("lyrics.lookup_not_found");

    expect(
        (await request.delete("/api/v1/lyrics-lookups", { headers })).status(),
    ).toBe(204);
});

test("the API keeps the search, the choice and the upload of lyrics to admins and checks them", async ({
    request,
}) => {
    const headers = await adminHeaders(request);
    const id = await findTrackId(request, "First Song");

    // While the download is off, LRCLIB is neither searched nor asked for a chosen track.
    const search = await request.get(
        `/api/v1/tracks/${id}/lyrics/online-matches`,
        { headers },
    );
    expect(search.status()).toBe(409);
    expect((await search.json()).code).toBe("lyrics.download_disabled");
    const choice = await request.put(
        `/api/v1/tracks/${id}/lyrics/online-match`,
        {
            headers,
            data: { matchId: 1 },
        },
    );
    expect(choice.status()).toBe(409);
    expect((await choice.json()).code).toBe("lyrics.download_disabled");

    // Lyrics without a line of text are refused before anything is written.
    const empty = await request.put(`/api/v1/tracks/${id}/lyrics`, {
        headers,
        data: { text: "[ar:E2E Artist]\n[00:01.00]" },
    });
    expect(empty.status()).toBe(400);
    expect(
        (
            await request.get(`/api/v1/tracks/${id}/lyrics`, { headers })
        ).status(),
    ).toBe(404);

    const anonymous = await request.put(`/api/v1/tracks/${id}/lyrics`, {
        data: { text: "Line" },
    });
    expect(anonymous.status()).toBe(401);
});

test("admins search lyrics and are told that the download is off", async ({
    page,
    request,
}) => {
    const id = await findTrackId(request, "First Song");
    await page.goto(`/library/tracks/${id}`);

    const section = page.locator(
        "[data-part='app'] [data-part='section'][data-item='lyrics']",
    );
    await section.locator("[data-item='search-lyrics']").click();

    const dialog = page.locator(
        "[data-part='dialog'][data-item='lyrics-search']",
    );
    await expect(dialog.locator("[data-part='online-search']")).toHaveAttribute(
        "data-state",
        "disabled",
    );
    await expect(dialog.locator("[data-part='alert']")).toContainText(
        "The download of lyrics is switched off.",
    );
    await dialog
        .locator("[data-part='dialog-footer'] [data-part='button']")
        .click();
    await expect(dialog).toHaveCount(0);
});

test("admins upload own lyrics and publish them", async ({ page, request }) => {
    const id = await findTrackId(request, "First Song");
    // The server is simulated, so the media folder keeps the song without lyrics.
    let sent: unknown = null;
    await page.route(`**/api/v1/tracks/${id}/lyrics`, async (route) => {
        if (route.request().method() !== "PUT") {
            await route.fallback();
            return;
        }

        sent = route.request().postDataJSON();
        await route.fulfill({
            json: {
                lyrics: {
                    type: "LineSynchronized",
                    source: "File",
                    fromOtherVersion: false,
                    lines: [
                        {
                            startMilliseconds: 1000,
                            endMilliseconds: null,
                            text: "Own line",
                            words: [],
                        },
                    ],
                },
                publication: "MissingMetadata",
            },
        });
    });
    await page.goto(`/library/tracks/${id}`);

    const section = page.locator(
        "[data-part='app'] [data-part='section'][data-item='lyrics']",
    );
    await section.locator("[data-item='upload-lyrics']").click();
    const dialog = page.locator(
        "[data-part='dialog'][data-item='lyrics-upload']",
    );
    await dialog.locator("[data-field='lyrics-file']").setInputFiles({
        name: "First Song.lrc",
        mimeType: "text/plain",
        buffer: Buffer.from("[00:01.00]Own line\n"),
    });
    const text = dialog.locator("[data-field='lyrics-text'] textarea");
    await expect(text).toHaveValue("[00:01.00]Own line\n");
    await expect(
        dialog.locator("[data-field='lyrics-text'] [data-part='field-hint']"),
    ).toHaveText("Synced: the lines have times.");
    await dialog.locator("[data-field='publish-lyrics'] input").check();
    await dialog.locator("[data-item='save-lyrics']").click();

    await expect(dialog.locator("[data-part='alert']").first()).toHaveText(
        "The lyrics are saved.",
    );
    await expect(
        dialog.locator("[data-part='alert'][data-item='lyrics-publication']"),
    ).toContainText("LRCLIB needs title, artist, album and length of the song");
    expect(sent).toEqual({ text: "[00:01.00]Own line\n", publish: true });
    await dialog
        .locator("[data-part='dialog-footer'] [data-part='button']")
        .click();
    await expect(section.locator("[data-item='lyrics-status']")).toHaveText(
        "Synchronized line by line, from the lyrics file.",
    );
});

test("admins see the lyrics lookup on the library page of the administration", async ({
    page,
}) => {
    await page.goto("/admin/library");

    const section = page.locator(
        "[data-part='app'] [data-part='section'][data-item='lyrics-lookups']",
    );
    await expect(section.locator("[data-part='section-title']")).toHaveText(
        "Lyrics lookup",
    );
    const results = section.locator("[data-part='lyrics-lookups']");
    await expect(results).toHaveAttribute("data-state", "empty");
    await expect(
        results.locator(
            "[data-part='stat-item'][data-item='wait'] [data-part='stat-value']",
        ),
    ).toHaveText("30 days");
    await expect(
        results.locator("[data-item='forget-lyrics-lookups']"),
    ).toBeDisabled();
});

test.describe("with a profile of its own", () => {
    test.use({ storageState: emptyStorageState, locale: "en-US" });

    let user: TestUser;

    test.beforeEach(async ({ page, request }) => {
        user = await createUser(request, `Singer ${uniqueSuffix()}`);
        await page.goto("/profiles");
        await profileTile(page, user.name).click();
        await expect(page).toHaveURL(homeUrl);
    });

    test.afterEach(async ({ request }) => {
        await deleteUser(request, user.id);
    });

    test("follows the music line by line and word by word and seeks to a line", async ({
        page,
        request,
    }) => {
        const id = await findTrackId(request, "Loose Song");
        await playSong(page, id, "Loose Song");
        await openNowPlaying(page);

        await expect(viewSegment(page, "lyrics")).toBeEnabled();
        await viewSegment(page, "lyrics").click();
        const stage = part(page, "stage");
        await expect(stage).toHaveAttribute("data-state", "lyrics");
        await expect(viewSegment(page, "lyrics")).toHaveAttribute(
            "aria-checked",
            "true",
        );
        const lyrics = part(page, "lyrics");
        await expect(lyrics).toBeVisible();
        await expect(lyrics).toHaveAttribute("data-state", "synchronized");
        await expect(lyrics).toHaveAttribute("data-words", "true");
        await expect(part(page, "lyrics-line")).toHaveCount(4);

        // A line becomes active while it is sung, and so do its words.
        await expect(lyricsLine(page, 0)).toHaveAttribute(
            "data-state",
            "active",
            {
                timeout: 10_000,
            },
        );
        await expect(lyricsLine(page, 0)).toHaveAttribute(
            "aria-current",
            "true",
        );
        await expect(
            lyricsLine(page, 0).locator(
                "[data-part='lyrics-word'][data-state='active']",
            ),
        ).toHaveCount(1);
        await expect(lyricsLine(page, 2)).toHaveAttribute("data-empty", "true");

        // A click on the last line jumps there.
        await lyricsLine(page, 3)
            .locator("[data-part='lyrics-line-button']")
            .click();
        await expect
            .poll(() => audioPosition(page), { timeout: 10_000 })
            .toBeGreaterThanOrEqual(13);
        await expect(lyricsLine(page, 3)).toHaveAttribute(
            "data-state",
            "active",
        );
        await expect(lyricsLine(page, 0)).toHaveAttribute("data-state", "past");
        await expect(part(page, "transport")).toHaveAttribute(
            "data-state",
            /\bplaying\b/,
        );

        // A song without lyrics shows its cover, and the lyrics cannot be chosen.
        const firstSong = await findTrackId(request, "First Song");
        await playSong(page, firstSong, "First Song");
        await openNowPlaying(page);
        await expect(stage).toHaveAttribute("data-state", "cover");
        await expect(viewSegment(page, "lyrics")).toBeDisabled();
        await expect(viewSegment(page, "cover")).toHaveAttribute(
            "aria-checked",
            "true",
        );
    });
});
