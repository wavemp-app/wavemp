import {
    expect,
    test,
    type APIRequestContext,
    type Locator,
    type Page,
} from "@playwright/test";
import { adminHeaders, uniqueSuffix } from "./helpers/api";

// Smart playlists of the built-in admin profile with the synthetic test media of
// scripts/generate-media.sh: E2E Album holds First Song and Second Song; no other test changes
// their album. Every test uses its own names, so the tests can run in parallel.

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`);
}

function openDialog(page: Page): Locator {
    return page.locator("[data-part='dialog'][open]");
}

function rowTitles(page: Page): Locator {
    return part(page, "list-row").locator(
        "[data-column='title'] > [data-part='label']",
    );
}

function ruleRow(page: Page, index: number): Locator {
    return part(page, "smart-rule").nth(index);
}

interface Named {
    id: string;
    name: string;
}

async function findByName(
    request: APIRequestContext,
    path: string,
    name: string,
): Promise<Named | undefined> {
    const response = await request.get(`${path}?limit=100`, {
        headers: await adminHeaders(request),
    });
    expect(response.status()).toBe(200);
    return ((await response.json()).items as Named[]).find(
        (item) => item.name === name,
    );
}

function findSmartPlaylist(request: APIRequestContext, name: string) {
    return findByName(request, "/api/v1/me/smart-playlists", name);
}

async function createSmartPlaylist(
    request: APIRequestContext,
    name: string,
): Promise<string> {
    const response = await request.post("/api/v1/me/smart-playlists", {
        data: {
            name,
            match: "All",
            rules: [{ field: "Album", operator: "Is", text: "E2E Album" }],
            sort: "Title",
            direction: "Asc",
        },
        headers: await adminHeaders(request),
    });
    expect(response.status()).toBe(201);
    return (await response.json()).id;
}

async function cleanUp(
    request: APIRequestContext,
    name: string,
): Promise<void> {
    const headers = await adminHeaders(request);
    const smart = await findSmartPlaylist(request, name);
    if (smart) {
        await request.delete(`/api/v1/me/smart-playlists/${smart.id}`, {
            headers,
        });
    }
    const playlist = await findByName(request, "/api/v1/me/playlists", name);
    if (playlist) {
        await request.delete(`/api/v1/me/playlists/${playlist.id}`, {
            headers,
        });
    }
}

test("creates a smart playlist from a template and rules, edits and converts it", async ({
    page,
    request,
    isMobile,
}) => {
    const name = `E2E Smart ${uniqueSuffix()}`;
    try {
        // Create: a template fills the rules, then the rule is changed.
        await page.goto("/playlists");
        await part(page, "view-actions")
            .locator("[data-item='create-smart-playlist']")
            .click();
        await expect(page).toHaveURL(/\/playlists\/smart\/new$/);
        await expect(
            page.locator("[data-view='smart-playlist-edit']"),
        ).toHaveAttribute("data-state", "create");

        const templates = part(page, "smart-templates");
        await templates.locator("[data-item='four-stars-and-more']").click();
        await expect(
            templates.locator("[data-item='four-stars-and-more']"),
        ).toHaveAttribute("data-state", "applied");
        await expect(
            ruleRow(page, 0).locator("[data-field='rule-field'] select"),
        ).toHaveValue("Rating");

        await page.getByLabel("Name", { exact: true }).fill(name);
        await ruleRow(page, 0)
            .locator("[data-field='rule-field'] select")
            .selectOption("Album");
        await expect(ruleRow(page, 0)).toHaveAttribute("data-kind", "text");
        await ruleRow(page, 0)
            .locator("[data-field='rule-operator'] select")
            .selectOption("Is");
        await ruleRow(page, 0)
            .locator("[data-field='rule-text'] input")
            .fill("E2E Album");
        await page.locator("[data-field='sort'] select").selectOption("Title");
        await page
            .locator(
                "[data-item='direction'] [data-part='segment'][data-item='Desc']",
            )
            .click();
        await page.locator("[data-item='save']").click();

        await expect(page).toHaveURL(/\/playlists\/smart\/[0-9a-f-]+$/);
        await expect(part(page, "detail-title")).toHaveText(name);
        await expect(part(page, "smart-summary-rule")).toHaveText(
            'Album is "E2E Album"',
        );
        await expect(rowTitles(page)).toHaveText(["Second Song", "First Song"]);

        // The overview shows it as a tile with its symbol; the sidebar lists it on the desktop.
        await page.goto("/playlists");
        const tile = part(page, "tile").filter({ hasText: name });
        await expect(tile).toHaveAttribute("data-kind", "smart-playlist");
        await expect(tile.locator("[data-part='tile-badge']")).toBeVisible();
        if (!isMobile) {
            await expect(
                part(page, "sidebar").getByRole("treeitem", { name }),
            ).toBeVisible();
        }
        await tile.locator("[data-part='tile-title']").click();
        await expect(part(page, "detail-title")).toHaveText(name);

        // Edit: one song at most.
        await part(page, "detail-actions")
            .locator("[data-item='edit-rules']")
            .click();
        await expect(page).toHaveURL(/\/playlists\/smart\/[0-9a-f-]+\/edit$/);
        await expect(part(page, "smart-templates")).toHaveCount(0);
        await page.locator("[data-field='limited'] input").check();
        await page.locator("[data-field='limit'] input").fill("1");
        await page.locator("[data-item='save']").click();
        await expect(page).toHaveURL(/\/playlists\/smart\/[0-9a-f-]+$/);
        await expect(rowTitles(page)).toHaveText(["Second Song"]);

        // Convert: a normal playlist with the songs of the moment takes its place.
        await part(page, "detail-actions")
            .locator("[data-item='convert']")
            .click();
        await expect(openDialog(page)).toContainText(name);
        await openDialog(page).locator("[data-item='confirm-convert']").click();
        await expect(page).toHaveURL(/\/playlists\/[0-9a-f-]+$/);
        await expect(page.locator("[data-view='playlist']")).toBeVisible();
        await expect(part(page, "detail-title")).toHaveText(name);
        await expect(rowTitles(page)).toHaveText(["Second Song"]);
        expect(await findSmartPlaylist(request, name)).toBeUndefined();
    } finally {
        await cleanUp(request, name);
    }
});

test("checks the rules before saving", async ({ page }) => {
    await page.goto("/playlists/smart/new");
    await page
        .getByLabel("Name", { exact: true })
        .fill(`E2E Invalid ${uniqueSuffix()}`);
    await ruleRow(page, 0)
        .locator("[data-field='rule-field'] select")
        .selectOption("Year");
    await ruleRow(page, 0)
        .locator("[data-field='rule-operator'] select")
        .selectOption("Between");
    await ruleRow(page, 0)
        .locator("[data-field='rule-number'] input")
        .fill("2000");
    await ruleRow(page, 0)
        .locator("[data-field='rule-number-to'] input")
        .fill("1990");
    await page.locator("[data-item='save']").click();

    await expect(ruleRow(page, 0)).toHaveAttribute("data-state", "invalid");
    await expect(
        ruleRow(page, 0).locator("[data-part='field-error']"),
    ).toBeVisible();
    await expect(page).toHaveURL(/\/playlists\/smart\/new$/);
});

test("deletes a smart playlist after asking", async ({ page, request }) => {
    const name = `E2E Smart Delete ${uniqueSuffix()}`;
    const id = await createSmartPlaylist(request, name);
    try {
        await page.goto(`/playlists/smart/${id}`);
        await expect(part(page, "detail-title")).toHaveText(name);
        await expect(rowTitles(page)).toHaveText(["First Song", "Second Song"]);

        await part(page, "detail-actions")
            .locator("[data-item='delete']")
            .click();
        await expect(
            openDialog(page).locator("[data-part='dialog-title']"),
        ).toHaveText("Delete smart playlist");
        await openDialog(page)
            .locator("[data-part='dialog-footer'] button", {
                hasText: "Delete",
            })
            .click();

        await expect(page).toHaveURL(/\/playlists$/);
        await expect(part(page, "tile").filter({ hasText: name })).toHaveCount(
            0,
        );
        expect(await findSmartPlaylist(request, name)).toBeUndefined();
    } finally {
        await cleanUp(request, name);
    }
});

test("offers the built-in templates through the API", async ({ request }) => {
    const response = await request.get(
        "/api/v1/smart-playlist-templates?limit=10",
        {
            headers: await adminHeaders(request),
        },
    );

    expect(response.status()).toBe(200);
    expect(
        ((await response.json()).items as { id: string }[]).map(
            (item) => item.id,
        ),
    ).toEqual([
        "FourStarsAndMore",
        "NeverPlayed",
        "RecentlyAdded",
        "MostPlayed",
        "Favorites",
    ]);
});

test("links rules to the library by their normalized name and marks the others", async ({
    page,
    request,
}) => {
    const name = `E2E Smart Linked ${uniqueSuffix()}`;
    const missing = `Nobody ${uniqueSuffix()}`;
    const response = await request.post("/api/v1/me/smart-playlists", {
        data: {
            name,
            match: "Any",
            rules: [
                { field: "Album", operator: "Is", text: "e2e-album" },
                { field: "Artist", operator: "Is", text: missing },
            ],
            sort: "Title",
            direction: "Asc",
        },
        headers: await adminHeaders(request),
    });
    expect(response.status()).toBe(201);
    const created = await response.json();
    try {
        // The server stored the id of E2E Album and names it as the library spells it.
        expect(created.rules[0].referenceId).toEqual(expect.any(String));
        expect(created.rules[0].text).toBe("E2E Album");
        expect(created.rules[1].referenceId ?? null).toBeNull();

        await page.goto(`/playlists/smart/${created.id}`);
        const summaries = part(page, "smart-summary-rule");
        await expect(summaries.nth(0)).toHaveAttribute(
            "data-reference",
            "linked",
        );
        await expect(summaries.nth(0)).toHaveText('Album is "E2E Album"');
        await expect(summaries.nth(1)).toHaveAttribute(
            "data-reference",
            "unlinked",
        );
        await expect(
            summaries.nth(1).locator("[data-part='smart-summary-note']"),
        ).toHaveText("Not in the library");
        await expect(rowTitles(page)).toHaveText(["First Song", "Second Song"]);

        await page.goto(`/playlists/smart/${created.id}/edit`);
        await expect(ruleRow(page, 0)).toHaveAttribute(
            "data-reference",
            "linked",
        );
        await expect(
            ruleRow(page, 0).locator("[data-part='field-hint']"),
        ).toHaveCount(0);
        await expect(ruleRow(page, 1)).toHaveAttribute(
            "data-reference",
            "unlinked",
        );
        await expect(
            ruleRow(page, 1).locator("[data-part='field-hint']"),
        ).toContainText("Not found in the library");
    } finally {
        await cleanUp(request, name);
    }
});
