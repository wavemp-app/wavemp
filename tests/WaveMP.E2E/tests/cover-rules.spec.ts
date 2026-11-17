import {
    expect,
    test,
    type APIRequestContext,
    type Page,
} from "@playwright/test";
import {
    adminHeaders,
    findAlbumId,
    findTrackId,
    latestJobRunId,
    updateLibrarySettings,
} from "./helpers/api";

// Cover rules of the library, an album and a song. The E2E Album has only an album cover, so every
// rule shows it (the other cover is the fallback); the tests check the choice, the preview, the
// stored rule and the global setting.

async function setRule(
    request: APIRequestContext,
    owner: "albums" | "tracks",
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
    owner: "albums" | "tracks",
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

function ruleSegment(page: Page, item: string) {
    return page
        .locator(
            "[data-part='section'][data-item='cover-rule'] [data-field='cover-rule']",
        )
        .locator(`[data-part='segment'][data-item='${item}']`);
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
        await setRule(
            request,
            "tracks",
            await findTrackId(request, "First Song"),
            "Inherit",
        );
        await setRule(
            request,
            "albums",
            await findAlbumId(request, "E2E Album"),
            "Inherit",
        );
        await updateLibrarySettings(request, { coverPriority: "AlbumFirst" });
    });

    test("chooses the cover rule of a song with a preview", async ({
        page,
        request,
    }) => {
        const id = await findTrackId(request, "First Song");
        await page.goto(`/library/tracks/${id}`);

        const section = page.locator(
            "[data-part='section'][data-item='cover-rule']",
        );
        await expect(section.locator("[data-part='section-title']")).toHaveText(
            "Cover",
        );
        await expect(ruleSegment(page, "inherit")).toHaveAttribute(
            "aria-checked",
            "true",
        );
        await expect(ruleSegment(page, "inherit")).toHaveText(
            "Inherited (album cover first)",
        );
        const preview = section.locator("[data-part='cover-preview']");
        await expect(preview).toHaveAttribute("data-state", "ready");

        await ruleSegment(page, "track-first").click();
        // The song has no cover of its own: the preview falls back to the album cover.
        await expect(preview).toHaveAttribute("data-state", "ready");
        await expect(
            preview.locator("[data-part='cover-image']"),
        ).toBeVisible();
        await section.locator("[data-item='save-cover-rule']").click();

        await expect(
            section.locator("[data-part='alert'][data-state='success']"),
        ).toHaveText("Saved. The covers are updated.");
        expect((await ruleOf(request, "tracks", id)).rule).toBe("TrackFirst");
        await page.reload();
        await expect(ruleSegment(page, "track-first")).toHaveAttribute(
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

    test("lets a song inherit the rule of its album", async ({
        page,
        request,
    }) => {
        const album = await findAlbumId(request, "E2E Album");
        await page.goto(`/library/albums/${album}`);

        await ruleSegment(page, "track-first").click();
        await page.locator("[data-item='save-cover-rule']").click();
        await expect(
            page.locator("[data-part='alert'][data-state='success']"),
        ).toBeVisible();

        const track = await findTrackId(request, "First Song");
        const rule = await ruleOf(request, "tracks", track);
        expect(rule.rule).toBe("Inherit");
        expect(rule.effectivePriority).toBe("TrackFirst");
        await page.goto(`/library/tracks/${track}`);
        await expect(ruleSegment(page, "inherit")).toHaveText(
            "Inherited (song cover first)",
        );
    });

    test("saves the global cover rule in the library settings", async ({
        page,
        request,
    }) => {
        await page.goto("/admin/library");
        const field = page.locator("[data-field='cover-priority']");
        const albumFirst = field.locator(
            "[data-part='segment'][data-item='album-first']",
        );
        const trackFirst = field.locator(
            "[data-part='segment'][data-item='track-first']",
        );
        await expect(albumFirst).toHaveAttribute("aria-checked", "true");

        await trackFirst.click();
        await page.getByRole("button", { name: "Save", exact: true }).click();

        await expect(
            page.locator("[data-part='alert'][data-state='success']"),
        ).toHaveText("The settings are saved.");
        await page.reload();
        await expect(trackFirst).toHaveAttribute("aria-checked", "true");
        const album = await findAlbumId(request, "E2E Album");
        expect((await ruleOf(request, "albums", album)).effectivePriority).toBe(
            "TrackFirst",
        );
    });
});
