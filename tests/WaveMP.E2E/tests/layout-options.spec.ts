import { expect, test, type Page } from "@playwright/test";
import { findAlbumId, findTrackId } from "./helpers/api";
import { withoutSavedPlayback } from "./helpers/player";

// Layout options of the theme API (THEMING.md, section "Layout options"): a theme switches optional
// parts of the desktop layout on with variables on :root. The tests use the default theme with a
// style that plays the part of such a theme; without it, the default theme sets no option.

/**
 * A theme that moves the breadcrumb into the header and keeps the base layout of the header for
 * it (revert-layer lets the base template for a header with a breadcrumb apply).
 */
const breadcrumbInHeader = `
    :root {
        --wmp-layout-breadcrumb: header;
    }

    [data-part='header']:has(> [data-part='breadcrumb']) {
        grid-template-areas: revert-layer;
        grid-template-columns: revert-layer;
    }
`;

/** Adds the style to every page before the app starts, as if it were part of the theme. */
async function useThemeStyle(page: Page, css: string): Promise<void> {
    await page.addInitScript((text) => {
        const style = document.createElement("style");
        style.dataset.e2e = "layout-options";
        style.textContent = text;
        const attach = () => {
            if (!document.head) {
                return false;
            }
            document.head.append(style);
            return true;
        };
        if (!attach()) {
            const observer = new MutationObserver(() => {
                if (attach()) {
                    observer.disconnect();
                }
            });
            observer.observe(document, { childList: true, subtree: true });
        }
    }, css);
}

const header = "[data-part='app'] > [data-part='header']";
const toolbar = "[data-part='app'] > [data-part='toolbar']";

test("keeps the breadcrumb in the toolbar while the theme sets no option", async ({
    page,
}) => {
    await page.goto("/library/albums");
    await expect(page.locator("[data-part='tile']").first()).toBeVisible();

    await expect(page.locator("[data-part='breadcrumb']")).toHaveCount(1);
    await expect(
        page.locator(`${toolbar} > [data-part='breadcrumb']`),
    ).toHaveCount(1);
    await expect(
        page.locator(`${header} > [data-part='breadcrumb']`),
    ).toHaveCount(0);
});

test.describe("with the breadcrumb in the header", () => {
    test.beforeEach(async ({ page }) => {
        await useThemeStyle(page, breadcrumbInHeader);
    });

    test("shows the breadcrumb in the header on the desktop", async ({
        page,
        isMobile,
    }) => {
        test.skip(isMobile, "Desktop layout only");
        await page.goto("/library/albums");

        const breadcrumb = page.locator(`${header} > [data-part='breadcrumb']`);
        await expect(breadcrumb).toBeVisible();
        await expect(page.locator("[data-part='breadcrumb']")).toHaveCount(1);
        await expect(
            page.locator(`${toolbar} > [data-part='breadcrumb']`),
        ).toHaveCount(0);
        await expect(
            breadcrumb.locator("[data-part='breadcrumb-link']"),
        ).toHaveText(["Music", "Library", "Album"]);

        // In the base layout of the header: after the history buttons, before the tabs, in one row.
        const history = (await page
            .locator(`${header} > [data-part='nav-history']`)
            .boundingBox())!;
        const tabs = (await page
            .locator(`${header} > [data-part='tabs']`)
            .boundingBox())!;
        const crumbs = (await breadcrumb.boundingBox())!;
        const bar = (await page.locator(header).boundingBox())!;
        expect(crumbs.x).toBeGreaterThanOrEqual(history.x + history.width);
        expect(crumbs.x + crumbs.width).toBeLessThanOrEqual(tabs.x + 0.5);
        expect(crumbs.y).toBeGreaterThanOrEqual(bar.y);
        expect(crumbs.y + crumbs.height).toBeLessThanOrEqual(
            bar.y + bar.height + 0.5,
        );

        await breadcrumb.getByRole("link", { name: "Library" }).click();
        await expect(page).toHaveURL(/\/library\/songs$/);
        await expect(
            page.locator(`${header} > [data-part='breadcrumb']`),
        ).toContainText("Songs");
    });

    test("keeps the breadcrumb in the toolbar on phones", async ({
        page,
        isMobile,
    }) => {
        test.skip(!isMobile, "Mobile layout only");
        await page.goto("/library/albums");
        await expect(page.locator("[data-part='tile']").first()).toBeVisible();

        await expect(page.locator("[data-part='breadcrumb']")).toHaveCount(1);
        await expect(
            page.locator(`${toolbar} > [data-part='breadcrumb']`),
        ).toHaveCount(1);
        await expect(
            page.locator(`${header} > [data-part='breadcrumb']`),
        ).toHaveCount(0);
    });
});

const viewOptions = `
    :root {
        --wmp-layout-view-options: on;
    }
`;

const viewSwitch = `${toolbar} > [data-part='view-switch']`;
const viewMenu = "[data-part='context-menu'][aria-label='View options']";

// A long list of songs with three pages of ten songs per album, answered in the browser
// (page.route), so the library of the test media stays as it is. The songs do not exist on the
// server.
const longListSize = 250;

function longSong(index: number) {
    const album = Math.floor(index / 10);
    return {
        id: `00000000-0000-4000-8000-${String(index).padStart(12, "0")}`,
        title: `Long Song ${index}`,
        artist: {
            id: "00000000-0000-4000-9000-000000000001",
            name: "Long Artist",
        },
        album: {
            id: `00000000-0000-4000-a000-${String(album).padStart(12, "0")}`,
            title: `Long Album ${album}`,
        },
        trackNumber: (index % 10) + 1,
        discNumber: null,
        year: 2026,
        genre: null,
        durationMilliseconds: 120_000,
        hasAudio: true,
        hasVideo: false,
        hasCover: false,
        firstSeenAt: "2026-01-01T00:00:00Z",
        isFavorite: false,
        rating: 0,
    };
}

async function mockLongList(page: Page): Promise<void> {
    await page.route(
        (url) => url.pathname === "/api/v1/tracks",
        (route) => {
            const url = new URL(route.request().url());
            const offset = Number(url.searchParams.get("offset") ?? 0);
            const limit = Number(url.searchParams.get("limit") ?? 100);
            const items = [];
            for (
                let index = offset;
                index < Math.min(offset + limit, longListSize);
                index++
            ) {
                items.push(longSong(index));
            }
            return route.fulfill({
                json: { items, offset, limit, total: longListSize },
            });
        },
    );
}

test("keeps the view options disabled while the theme does not offer them", async ({
    page,
}) => {
    await page.goto("/library/songs?view=list");
    await expect(page.locator("[data-part='list-row']").first()).toBeVisible();

    await expect(page.locator(viewSwitch)).toBeDisabled();
    await expect(page.locator(viewMenu)).toHaveCount(0);
    // The view of the address is ignored: the songs stay grouped by album.
    await expect(
        page.locator("[data-part='list'][data-state~='ungrouped']"),
    ).toHaveCount(0);
    await expect(
        page.locator("[data-part='list-group-header']").first(),
    ).toBeVisible();
});

test.describe("with the view options", () => {
    // Requests of the page must reach page.route, which does not see what the service worker
    // fetches.
    test.use({ serviceWorkers: "block" });

    test.beforeEach(async ({ page }) => {
        await useThemeStyle(page, viewOptions);
    });

    test("switches a long list of songs to a plain list and back", async ({
        page,
        isMobile,
    }) => {
        test.skip(isMobile, "Desktop layout only");
        await mockLongList(page);
        await page.goto("/library/songs");
        const list = page.locator("[data-part='app'] [data-part='list']");
        const rows = list.locator("[data-part='list-row']");
        await expect(rows.first()).toBeVisible();
        await expect(
            list.locator("[data-part='list-group-info']").first(),
        ).toBeVisible();

        async function choose(
            item: "view-grouped" | "view-list",
        ): Promise<void> {
            await page.locator(viewSwitch).click();
            const menu = page.locator(viewMenu);
            await expect(menu).toBeVisible();
            await expect(menu.locator("[data-part='menu-item']")).toHaveText([
                "Grouped by album",
                "List",
            ]);
            await menu.locator(`[data-item='${item}']`).click();
            await expect(menu).toBeHidden();
        }

        await expect(page.locator(viewSwitch)).toBeEnabled();
        await choose("view-list");
        await expect(page).toHaveURL(/\/library\/songs\?view=list$/);
        await expect(list).toHaveAttribute("data-state", /(^| )ungrouped( |$)/);
        await expect(list.locator("[data-part='list-group']")).toHaveCount(1);
        await expect(
            list.locator("[data-part='list-group-header']"),
        ).toHaveCount(0);
        await expect(list.locator("[data-part='list-group-info']")).toHaveCount(
            0,
        );

        // The rows take the whole width of the list: the column headers start at its start.
        const listBox = (await list.boundingBox())!;
        const number = (await list
            .locator("[data-part='list-header'] > [data-part='list-column']")
            .first()
            .boundingBox())!;
        const cell = (await rows
            .first()
            .locator("[data-part='list-cell']")
            .first()
            .boundingBox())!;
        expect(number.x - listBox.x).toBeLessThan(1);
        expect(Math.abs(cell.x - number.x)).toBeLessThan(1);

        // Loading more pages keeps the plain list.
        await expect(rows).toHaveCount(100);
        await page.locator("[data-part='load-more']").click();
        await expect(rows).toHaveCount(200);
        await expect(list.locator("[data-part='list-group']")).toHaveCount(1);
        await expect(rows.filter({ hasText: "Long Song 150" })).toHaveCount(1);

        // The menu marks the chosen view and leads back to the groups, with all loaded songs.
        await page.locator(viewSwitch).click();
        await expect(
            page.locator(`${viewMenu} [data-item='view-list']`),
        ).toHaveAttribute("aria-checked", "true");
        await page.keyboard.press("Escape");
        await choose("view-grouped");
        await expect(page).toHaveURL(/\/library\/songs$/);
        await expect(list).not.toHaveAttribute(
            "data-state",
            /(^| )ungrouped( |$)/,
        );
        await expect(list.locator("[data-part='list-group']")).toHaveCount(20);
        await expect(rows).toHaveCount(200);
        await expect(
            list.locator("[data-part='list-group-info']").first(),
        ).toBeVisible();
    });

    test("keeps the view options disabled where no songs are listed", async ({
        page,
        isMobile,
    }) => {
        test.skip(isMobile, "Desktop layout only");
        await page.goto("/library/albums");
        await expect(page.locator("[data-part='tile']").first()).toBeVisible();

        await expect(page.locator(viewSwitch)).toBeDisabled();
    });

    test("keeps the view options disabled on phones", async ({
        page,
        isMobile,
    }) => {
        test.skip(!isMobile, "Mobile layout only");
        await page.goto("/library/songs?view=list");
        await expect(
            page.locator("[data-part='list-row']").first(),
        ).toBeVisible();

        await expect(page.locator(viewSwitch)).toBeDisabled();
        await expect(
            page.locator("[data-part='list'][data-state~='ungrouped']"),
        ).toHaveCount(0);
    });
});

/**
 * A theme that docks the play queue beside the content and keeps the base layout of the app for
 * it (revert-layer lets the base template for an app with a queue pane apply).
 */
const dockedQueue = `
    :root {
        --wmp-layout-queue-pane: docked;
    }

    [data-part='app']:has(> [data-part='queue-pane']) {
        grid-template-areas: revert-layer;
        grid-template-columns: revert-layer;
    }
`;

const queuePane = "[data-part='app'] > [data-part='queue-pane']";

test("shows no queue pane while the theme sets no option", async ({ page }) => {
    await page.goto("/library/albums");
    await expect(page.locator("[data-part='tile']").first()).toBeVisible();

    await expect(page.locator("[data-part='queue-pane']")).toHaveCount(0);
});

test.describe("with the docked queue pane", () => {
    test.beforeEach(async ({ page }) => {
        // The tests of the built-in admin share one profile: the queue starts empty instead of
        // with the saved playback state of another test.
        await withoutSavedPlayback(page);
        await useThemeStyle(page, dockedQueue);
    });

    test("shows the queue beside the content, plays its entries and shows the lyrics", async ({
        page,
        request,
        isMobile,
    }) => {
        test.skip(isMobile, "Desktop layout only");
        await page.goto("/library/albums");
        await expect(page.locator("[data-part='tile']").first()).toBeVisible();

        // Empty at first, as a third column between the content and the right edge.
        const pane = page.locator(queuePane);
        await expect(pane).toBeVisible();
        await expect(pane).toHaveAttribute("aria-label", "Play queue");
        await expect(
            pane.locator("[data-part='queue-pane-header']"),
        ).toHaveAttribute("data-state", "empty");
        await expect(
            pane.locator("[data-part='empty-state-title']"),
        ).toHaveText("The queue is empty");
        await expect(
            pane.locator(
                "[data-part='queue-pane-summary'] [data-field='count']",
            ),
        ).toHaveText("0 songs");
        const content = (await page
            .locator("[data-part='app'] > [data-part='content']")
            .boundingBox())!;
        const paneBox = (await pane.boundingBox())!;
        expect(paneBox.x).toBeGreaterThanOrEqual(
            content.x + content.width - 0.5,
        );
        expect(paneBox.x + paneBox.width).toBeGreaterThan(
            page.viewportSize()!.width - 2,
        );
        expect(paneBox.y).toBeGreaterThanOrEqual(content.y - 0.5);

        // Playing an album fills it; pausing keeps the two-second songs from moving on.
        const album = await findAlbumId(request, "E2E Album");
        await page.goto(`/library/albums/${album}`);
        await page
            .locator(
                "[data-part='app'] [data-part='detail-actions'] [data-item='play']",
            )
            .click();
        const playButton = page.locator(
            "[data-part='app'] [data-part='transport'] [data-part='btn-play']",
        );
        await expect(pane.locator("[data-part='queue-item']")).toHaveCount(2);
        await playButton.click();
        await expect(playButton).toHaveAttribute("data-state", "paused");

        await expect(pane.locator("[data-part='queue-item-title']")).toHaveText(
            ["First Song", "Second Song"],
        );
        const header = pane.locator("[data-part='queue-pane-header']");
        await expect(header).not.toHaveAttribute("data-state", "empty");
        await expect(header.locator("[data-part='cover']")).toBeVisible();
        await expect(
            header.locator("[data-part='queue-pane-subtitle']"),
        ).toHaveText("E2E Artist");
        const summary = pane.locator("[data-part='queue-pane-summary']");
        await expect(summary.locator("[data-field='count']")).toHaveText(
            "2 songs",
        );
        await expect(summary.locator("[data-field='duration']")).toHaveText(
            /^0:0\d in total$/,
        );

        // An entry plays when it is chosen.
        await pane
            .locator("[data-part='queue-item']")
            .nth(1)
            .locator("[data-part='queue-item-info']")
            .click();
        await expect(
            page.locator(
                "[data-part='app'] [data-part='now-playing-mini'] [data-part='now-playing-title']",
            ),
        ).toHaveText("Second Song");
        await expect(
            header.locator("[data-part='queue-pane-title']"),
        ).toHaveText("Second Song");
        await expect(
            pane.locator("[data-part='queue-item'][aria-current='true']"),
        ).toContainText("Second Song");

        // The lyrics tab shows the lyrics of the current song.
        const loose = await findTrackId(request, "Loose Song");
        await page.goto(`/library/tracks/${loose}`);
        await page
            .locator(
                "[data-part='app'] [data-part='detail-actions'] [data-item='play']",
            )
            .click();
        await expect(
            header.locator("[data-part='queue-pane-title']"),
        ).toHaveText("Loose Song");
        const lyricsTab = pane.locator(
            "[data-part='segmented-control'][data-item='queue-pane-view'] [data-part='segment'][data-item='lyrics']",
        );
        await lyricsTab.click();
        await expect(lyricsTab).toHaveAttribute("aria-checked", "true");
        const lyrics = pane.locator("[data-part='lyrics']");
        await expect(lyrics).toBeVisible();
        await expect(lyrics.locator("[data-part='lyrics-line']")).toHaveCount(
            4,
        );
        await expect(pane.locator("[data-part='queue']")).toBeHidden();

        // Now Playing shows the queue itself: no pane there.
        await page
            .locator("[data-part='app'] a[href='/now-playing']")
            .filter({ visible: true })
            .click();
        await expect(page).toHaveURL(/\/now-playing$/);
        await expect(
            page.locator("[data-part='app'] [data-part='stage']"),
        ).toBeVisible();
        await expect(page.locator("[data-part='queue-pane']")).toHaveCount(0);
    });

    test("shows no queue pane on phones", async ({ page, isMobile }) => {
        test.skip(!isMobile, "Mobile layout only");
        await page.goto("/library/albums");
        await expect(page.locator("[data-part='tile']").first()).toBeVisible();

        await expect(page.locator("[data-part='queue-pane']")).toHaveCount(0);
    });
});
