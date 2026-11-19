import {
    expect,
    test,
    type APIRequestContext,
    type Locator,
    type Page,
} from "@playwright/test";
import { adminHeaders, findAlbumId, findTrackId } from "./helpers/api";

// The online search for album info. It is off by default, and the tests never reach MusicBrainz
// or the Cover Art Archive: they check the switched-off search, an address that cannot be reached
// and, with the answers of the API simulated in the browser, the comparison and what Apply sends.

/** A PNG of 1 x 1 pixel as preview of a release cover. */
const coverPng = Buffer.from(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
    "base64",
);

const defaults = {
    enabled: false,
    musicBrainzUrl: "https://musicbrainz.org",
    coverArtArchiveUrl: "https://coverartarchive.org",
    musicBrainzRequestIntervalMilliseconds: 1000,
};

/** An address on the server that refuses every connection. */
const unreachable = "http://127.0.0.1:9";

async function updateSettings(
    request: APIRequestContext,
    changes: Partial<typeof defaults>,
): Promise<void> {
    const response = await request.patch("/api/v1/settings/online-metadata", {
        data: changes,
        headers: await adminHeaders(request),
    });
    expect(response.status(), await response.text()).toBe(200);
}

function dialog(page: Page): Locator {
    return page.locator("[data-part='dialog'][data-item='online-search']");
}

function search(page: Page): Locator {
    return dialog(page).locator("[data-part='online-search']");
}

function row(page: Page, field: string): Locator {
    return dialog(page).locator(
        `[data-part='online-compare-row'][data-field='${field}']`,
    );
}

async function openSearch(
    page: Page,
    path: string,
    title: string,
): Promise<void> {
    await page.goto(path);
    await expect(
        page.locator("[data-part='app'] [data-part='detail-title']"),
    ).toHaveText(title);
    await page
        .locator(
            "[data-part='app'] [data-part='detail-actions'] [data-item='search-online']",
        )
        .click();
    await expect(dialog(page)).toBeVisible();
}

/** Simulates the cover endpoint of the API in the browser. */
async function routeCover(page: Page): Promise<void> {
    await page.route("**/api/v1/online-releases/*/cover*", (route) =>
        route.fulfill({
            status: 200,
            contentType: "image/png",
            body: coverPng,
        }),
    );
}

test.describe("settings and errors", () => {
    // The tests change the global settings, so they run one after the other on one project.
    test.describe.configure({ mode: "serial" });

    test.beforeEach(({}, testInfo) => {
        test.skip(
            testInfo.project.name !== "chromium",
            "Settings are changed on one project only.",
        );
    });

    test.afterAll(async ({ request }, testInfo) => {
        // afterAll runs in the skipping projects as well; only the project that changed them resets.
        if (testInfo.project.name === "chromium") {
            await updateSettings(request, defaults);
        }
    });

    test("the search is off by default and says so", async ({
        page,
        request,
    }) => {
        await updateSettings(request, defaults);
        const id = await findTrackId(request, "First Song");

        const response = await request.get(
            `/api/v1/tracks/${id}/online-matches`,
            {
                headers: await adminHeaders(request),
            },
        );
        expect(response.status()).toBe(409);
        expect((await response.json()).code).toBe("lookup.disabled");

        await openSearch(page, `/library/tracks/${id}`, "First Song");
        await expect(search(page)).toHaveAttribute("data-state", "disabled");
        await expect(search(page)).toContainText("The online search is off.");
        await dialog(page).locator("[data-item='online-settings']").click();
        await expect(page).toHaveURL(/\/admin\/library$/);
        await expect(
            page.locator("[data-part='section'][data-item='online-metadata']"),
        ).toBeVisible();
    });

    test("admins switch the search on in the library settings", async ({
        page,
    }) => {
        await page.goto("/admin/library");
        const form = page.locator(
            "[data-part='form'][data-item='online-metadata-settings']",
        );
        const enabled = form.locator(
            "[data-field='online-metadata-enabled'] input",
        );
        await expect(enabled).not.toBeChecked();
        await expect(
            form.locator("[data-field='musicbrainz-url'] input"),
        ).toHaveValue(defaults.musicBrainzUrl);

        await form
            .locator("[data-field='musicbrainz-url'] input")
            .fill("ftp://mirror");
        await form.locator("button[type='submit']").click();
        await expect(
            form.locator(
                "[data-field='musicbrainz-url'] [data-part='field-error']",
            ),
        ).toHaveText("Enter an address that starts with http:// or https://.");

        await enabled.check();
        await form
            .locator("[data-field='musicbrainz-url'] input")
            .fill(unreachable);
        await form.locator("button[type='submit']").click();
        await expect(
            form.locator("[data-part='alert'][data-state='success']"),
        ).toHaveText("The settings of the online search were saved.");
    });

    test("without a connection the search says so and changes nothing", async ({
        page,
        request,
    }) => {
        await updateSettings(request, {
            enabled: true,
            musicBrainzUrl: unreachable,
        });
        const id = await findTrackId(request, "First Song");

        const response = await request.get(
            `/api/v1/tracks/${id}/online-matches`,
            {
                headers: await adminHeaders(request),
            },
        );
        expect(response.status()).toBe(503);
        expect((await response.json()).code).toBe("lookup.unavailable");

        await openSearch(page, `/library/tracks/${id}`, "First Song");
        await expect(search(page)).toHaveAttribute(
            "data-state",
            "unavailable",
            { timeout: 15_000 },
        );
        await expect(search(page)).toContainText(
            "MusicBrainz cannot be reached.",
        );
        await expect(
            dialog(page).locator("[data-item='search-again']"),
        ).toBeVisible();
    });
});

test("the matches of a song are compared field by field and applied as chosen", async ({
    page,
    request,
}) => {
    const id = await findTrackId(request, "First Song");
    await page.route("**/api/v1/tracks/*/online-matches", (route) =>
        route.fulfill({
            json: {
                current: {
                    trackId: id,
                    status: "Editable",
                    title: "First Song",
                    artist: "E2E Artist",
                    album: "E2E Album",
                    albumArtist: "E2E Artist",
                    year: null,
                    trackNumber: 1,
                    discNumber: null,
                    genre: null,
                },
                matches: [
                    {
                        recordingId: "a6f1d1c4-6e7b-4f43-9a1e-0b5f1f6b1c01",
                        release: {
                            id: "b7e2e2d5-7f8c-4a54-8b2f-1c6a2a7c2d02",
                            date: "2004-06-01",
                            country: "DE",
                            format: "CD",
                            trackCount: 3,
                        },
                        agreement: 94,
                        title: "First Song",
                        artist: "E2E Artist",
                        album: "E2E Album",
                        albumArtist: "E2E Artist",
                        year: 2004,
                        trackNumber: 1,
                        discNumber: 1,
                        genre: "Electronic",
                    },
                ],
            },
        }),
    );
    await routeCover(page);
    let applied: unknown;
    await page.route("**/api/v1/tracks/*/online-metadata", (route) => {
        applied = route.request().postDataJSON();
        return route.fulfill({ json: { cover: "None" } });
    });

    await openSearch(page, `/library/tracks/${id}`, "First Song");
    await expect(search(page)).toHaveAttribute("data-state", "ready");
    const match = dialog(page).locator("[data-part='online-match']");
    await expect(match).toHaveCount(1);
    await expect(match).toHaveAttribute("aria-checked", "true");
    await expect(match.locator("[role='meter']")).toHaveAttribute(
        "aria-valuetext",
        "94 % agreement",
    );
    await expect(row(page, "title")).toHaveAttribute("data-state", "same");
    await expect(row(page, "year")).toHaveAttribute("data-state", "missing");
    await expect(row(page, "year").locator("input")).toBeChecked();
    await expect(row(page, "genre").locator("input")).toBeChecked();
    await expect(row(page, "cover")).toHaveAttribute("data-state", "ready");

    // The song has a cover: the suggested one is only applied when chosen.
    await expect(row(page, "cover").locator("input")).not.toBeChecked();
    await row(page, "genre").locator("input").uncheck();
    await dialog(page).locator("[data-item='apply']").click();

    await expect(search(page)).toHaveAttribute("data-state", "applied");
    await expect(dialog(page).locator("[data-part='alert']")).toHaveText(
        "The tags were written.",
    );
    expect(applied).toEqual({
        tags: { year: 2004, discNumber: 1 },
        coverReleaseId: null,
    });
});

test("an album takes only the album fields", async ({ page, request }) => {
    const id = await findAlbumId(request, "E2E Album");
    await page.route("**/api/v1/albums/*/online-matches", (route) =>
        route.fulfill({
            json: {
                current: {
                    album: "E2E Album",
                    albumArtist: "E2E Artist",
                    year: null,
                    genre: null,
                },
                trackCount: 3,
                editableTrackCount: 3,
                matches: [
                    {
                        release: {
                            id: "c8f3f3e6-8a9d-4b65-9c3a-2d7b3b8d3e03",
                            date: "2004",
                        },
                        agreement: 81,
                        values: {
                            album: "E2E Album",
                            albumArtist: "E2E Artist",
                            year: 2004,
                            genre: "Electronic",
                        },
                    },
                ],
            },
        }),
    );
    await routeCover(page);

    await openSearch(page, `/library/albums/${id}`, "E2E Album");
    await expect(search(page)).toHaveAttribute("data-owner", "album");
    await expect(
        dialog(page).locator("[data-part='online-compare-row']"),
    ).toHaveCount(5);
    await expect(row(page, "title")).toHaveCount(0);
    await expect(
        dialog(page).locator("[data-item='compare'] [data-part='alert']"),
    ).toHaveText("The album fields are written into every song of the album.");
    await dialog(page)
        .locator("[data-part='dialog-footer'] button")
        .first()
        .click();
    await expect(dialog(page)).toBeHidden();
});
