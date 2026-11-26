import {
    expect,
    test,
    type APIRequestContext,
    type Locator,
    type Page,
} from "@playwright/test";
import {
    adminHeaders,
    findAlbumId,
    findTrackId,
    latestJobRunId,
    updateLibrarySettings,
} from "./helpers/api";

// Cover rules of the library, an album and a song. The E2E Album has only an album cover and its
// songs have no picture of their own, so "Own only" leaves a song without a cover; the tests check
// the choice in the editors, the preview, the stored rules and the global settings.

type Owner = "albums" | "tracks";

async function setRule(
    request: APIRequestContext,
    owner: Owner,
    id: string,
    rule: string,
): Promise<void> {
    const response = await request.put(`/api/v1/${owner}/${id}/cover-rule`, {
        data: { rule },
        headers: await adminHeaders(request),
    });
    expect(response.status()).toBe(200);
}

async function ruleOf(
    request: APIRequestContext,
    owner: Owner,
    id: string,
): Promise<{
    rule: string;
    inheritedPriority: string;
    effectivePriority: string;
}> {
    const response = await request.get(`/api/v1/${owner}/${id}/cover-rule`, {
        headers: await adminHeaders(request),
    });
    expect(response.status()).toBe(200);
    return response.json();
}

async function fallbackOf(
    request: APIRequestContext,
    album: string,
): Promise<{ fallback: string; effectiveFallback: string }> {
    const response = await request.get(`/api/v1/albums/${album}/cover-fallback`, {
        headers: await adminHeaders(request),
    });
    expect(response.status()).toBe(200);
    return response.json();
}

function coverSection(page: Page): Locator {
    return page.locator(
        "[data-part='dialog'][open] [data-part='section'][data-item='cover-rule']",
    );
}

function segment(page: Page, field: string, item: string): Locator {
    return coverSection(page)
        .locator(`[data-field='${field}']`)
        .locator(`[data-part='segment'][data-item='${item}']`);
}

function saveButton(page: Page): Locator {
    return page.locator(
        "[data-part='dialog'][open] [data-part='dialog-footer'] button[data-variant='primary']",
    );
}

test.describe("cover rules", () => {
    // The tests change the same rules, so they run one after the other.
    test.describe.configure({ mode: "serial" });

    test.beforeEach(({}, testInfo) => {
        test.skip(
            testInfo.project.name !== "chromium",
            "Rules are changed on one project only.",
        );
    });

    test.afterEach(async ({ request }) => {
        const album = await findAlbumId(request, "E2E Album");
        await setRule(
            request,
            "tracks",
            await findTrackId(request, "First Song"),
            "Inherit",
        );
        await setRule(request, "albums", album, "Inherit");
        const response = await request.put(
            `/api/v1/albums/${album}/cover-fallback`,
            {
                data: { fallback: "Inherit" },
                headers: await adminHeaders(request),
            },
        );
        expect(response.status()).toBe(200);
        await updateLibrarySettings(request, {
            coverPriority: "TrackFirst",
            albumCoverFallback: "ArtistPicture",
        });
    });

    test("chooses the cover rule of a song in its editor with a preview", async ({
        page,
        request,
    }) => {
        const id = await findTrackId(request, "First Song");
        await page.goto(`/library/tracks/${id}?edit=metadata`);

        // The page itself no longer has a section for the cover rule.
        await expect(
            page.locator(
                "[data-part='section'][data-item='cover-rule']:not([data-part='dialog'] *)",
            ),
        ).toHaveCount(0);
        const section = coverSection(page);
        await expect(section.locator("[data-part='section-title']")).toHaveText(
            "Cover",
        );
        await expect(segment(page, "cover-rule", "inherit")).toHaveAttribute(
            "aria-checked",
            "true",
        );
        await expect(segment(page, "cover-rule", "inherit")).toHaveText(
            "Default",
        );
        await expect(
            section.locator("[data-field='cover-rule'] [data-part='field-hint']"),
        ).toContainText("Default: Own first");
        const preview = section.locator("[data-part='cover-preview']");
        await expect(preview).toHaveAttribute("data-state", "ready");

        // The song has no picture of its own: with "Own only" it has no cover.
        await segment(page, "cover-rule", "track-only").click();
        await expect(preview).toHaveAttribute("data-state", "empty");
        await segment(page, "cover-rule", "album-first").click();
        await expect(preview).toHaveAttribute("data-state", "ready");
        await expect(
            preview.locator("[data-part='cover-image']"),
        ).toBeVisible();
        await saveButton(page).click();

        await expect(page.locator("[data-part='dialog'][open]")).toHaveCount(0);
        expect((await ruleOf(request, "tracks", id)).rule).toBe("AlbumFirst");
        await page.goto(`/library/tracks/${id}?edit=metadata`);
        await expect(segment(page, "cover-rule", "album-first")).toHaveAttribute(
            "aria-checked",
            "true",
        );

        // The change prepares the covers shown now.
        await expect
            .poll(
                async () => {
                    const runId = await latestJobRunId(request, "cover-cache");
                    const response = await request.get(
                        `/api/v1/jobs/cover-cache/runs/${runId}`,
                        {
                            headers: await adminHeaders(request),
                        },
                    );
                    return (await response.json()).trigger as string;
                },
                { timeout: 30_000, intervals: [500] },
            )
            .toBe("CoverRule");
    });

    test("sets the exceptions of an album in its editor", async ({
        page,
        request,
    }) => {
        const album = await findAlbumId(request, "E2E Album");
        await page.goto(`/library/albums/${album}`);
        await expect(
            page.locator("[data-part='section'][data-item='cover-rule']:not([data-part='dialog'] *)"),
        ).toHaveCount(0);

        await page
            .locator("[data-part='detail-actions'] [data-item='edit-metadata']")
            .click();
        await expect(page.locator("[data-part='dialog'][open] [data-part='dialog-title']")).toHaveText(
            "Edit album",
        );
        await expect(coverSection(page).locator("[data-part='cover-preview']"))
            .toHaveCount(0);
        await expect(segment(page, "cover-fallback", "inherit")).toHaveAttribute(
            "aria-checked",
            "true",
        );
        await segment(page, "cover-fallback", "none").click();
        await segment(page, "cover-rule", "track-only").click();
        await saveButton(page).click();

        await expect(page.locator("[data-part='dialog'][open]")).toHaveCount(0);
        expect(await fallbackOf(request, album)).toMatchObject({
            fallback: "None",
            effectiveFallback: "None",
        });
        const track = await findTrackId(request, "First Song");
        const rule = await ruleOf(request, "tracks", track);
        expect(rule.rule).toBe("Inherit");
        expect(rule.effectivePriority).toBe("TrackOnly");
        await page.goto(`/library/tracks/${track}?edit=metadata`);
        await expect(
            coverSection(page).locator(
                "[data-field='cover-rule'] [data-part='field-hint']",
            ),
        ).toContainText("Default: Own only");
    });

    test("saves the global cover rules in the library settings", async ({
        page,
        request,
    }) => {
        await page.goto("/admin/library");
        const songs = page.locator("[data-field='cover-priority']");
        const albums = page.locator("[data-field='album-cover-fallback']");
        const trackFirst = songs.locator(
            "[data-part='segment'][data-item='track-first']",
        );
        const trackOnly = songs.locator(
            "[data-part='segment'][data-item='track-only']",
        );
        const artistPicture = albums.locator(
            "[data-part='segment'][data-item='artist-picture']",
        );
        const none = albums.locator("[data-part='segment'][data-item='none']");
        await expect(trackFirst).toHaveAttribute("aria-checked", "true");
        await expect(artistPicture).toHaveAttribute("aria-checked", "true");

        await trackOnly.click();
        await none.click();
        // The library page has further forms (the online search) with their own Save button.
        await page
            .locator("form[data-part='form']")
            .filter({ has: songs })
            .getByRole("button", { name: "Save", exact: true })
            .click();

        await expect(
            page.locator("[data-part='alert'][data-state='success']"),
        ).toHaveText("The settings are saved.");
        await page.reload();
        await expect(trackOnly).toHaveAttribute("aria-checked", "true");
        await expect(none).toHaveAttribute("aria-checked", "true");
        const album = await findAlbumId(request, "E2E Album");
        expect((await ruleOf(request, "albums", album)).effectivePriority).toBe(
            "TrackOnly",
        );
        expect((await fallbackOf(request, album)).effectiveFallback).toBe("None");
    });
});
