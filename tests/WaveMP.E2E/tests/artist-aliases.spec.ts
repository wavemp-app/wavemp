import {
    expect,
    test,
    type APIRequestContext,
    type Locator,
    type Page,
} from "@playwright/test";
import { adminHeaders, uniqueSuffix } from "./helpers/api";

// Aliases of artists: an admin adds another name of an artist in the dialog on its page, the
// search of the library finds the artist by it and shows its real name, and the admin removes it
// again. Taking aliases from MusicBrainz is covered by the integration tests of the API, which
// simulate MusicBrainz; here the online lookup is off, so the server says so and sends nothing.
test.use({ locale: "en-US" });

interface ArtistItem {
    readonly id: string;
    readonly name: string;
}

async function findArtist(
    request: APIRequestContext,
    name: string,
): Promise<ArtistItem> {
    const response = await request.get("/api/v1/artists?limit=100", {
        headers: await adminHeaders(request),
    });
    expect(response.status()).toBe(200);
    const artist = ((await response.json()).items as ArtistItem[]).find(
        (item) => item.name === name,
    );
    expect(artist, name).toBeDefined();
    return artist!;
}

function aliasDialog(page: Page): Locator {
    return page.locator("[data-part='dialog'][data-item='artist-aliases']");
}

async function openAliasDialog(page: Page, artistId: string): Promise<Locator> {
    await page.goto(`/library/artists/${artistId}`);
    await page
        .locator("[data-part='detail-actions'] [data-item='artist-aliases']")
        .filter({ visible: true })
        .first()
        .click();
    const dialog = aliasDialog(page);
    await expect(dialog).toBeVisible();
    await expect(
        dialog.locator("[data-part='artist-alias-editor']"),
    ).not.toHaveAttribute("data-state", "loading");
    return dialog;
}

test("an alias added by an admin finds the artist until it is removed", async ({
    page,
    request,
}) => {
    const artist = await findArtist(request, "Loose Artist");
    const alias = `Wandering Minstrel ${uniqueSuffix()}`;
    const dialog = await openAliasDialog(page, artist.id);

    await dialog.locator("[data-field='alias-name'] input").fill(alias);
    await dialog.locator("button[data-item='add-alias']").click();

    const added = dialog
        .locator("[data-part='alias']")
        .filter({ hasText: alias });
    await expect(added).toHaveAttribute("data-source", "admin");
    await expect(added.locator("[data-part='alias-source']")).toHaveText(
        "Added by an admin",
    );
    await expect(dialog.locator("[data-part='alert']")).toContainText(
        "was added",
    );

    await dialog
        .locator("[data-field='alias-name'] input")
        .fill(alias.toUpperCase());
    await dialog.locator("button[data-item='add-alias']").click();
    await expect(dialog.locator("[data-part='field-error']")).toHaveText(
        "The artist already has this name or alias.",
    );

    await page.keyboard.press("Escape");
    await page.goto(
        `/library/artists?q=${encodeURIComponent(alias.toLowerCase())}`,
    );
    const tiles = page.locator(
        "[data-part='tile'][data-kind='artist'] [data-part='tile-title']",
    );
    await expect(tiles).toHaveText(["Loose Artist"]);

    const reopened = await openAliasDialog(page, artist.id);
    await reopened
        .locator("[data-part='alias']")
        .filter({ hasText: alias })
        .locator("button[data-item='remove-alias']")
        .click();
    await expect(
        reopened.locator("[data-part='alias']").filter({ hasText: alias }),
    ).toHaveCount(0);

    await page.keyboard.press("Escape");
    await page.goto(
        `/library/artists?q=${encodeURIComponent(alias.toLowerCase())}`,
    );
    await expect(
        page.locator("[data-part='tile'][data-kind='artist']"),
    ).toHaveCount(0);
});

test("taking aliases from MusicBrainz says when the online lookup is off", async ({
    page,
    request,
}) => {
    const settings = await request.get("/api/v1/settings/artist-pictures", {
        headers: await adminHeaders(request),
    });
    test.skip(
        (await settings.json()).onlineLookupEnabled === true,
        "The online lookup is switched on in this instance",
    );
    const requests: string[] = [];
    page.on("request", (sent) => {
        if (sent.url().includes("/musicbrainz-imports")) {
            requests.push(sent.url());
        }
    });
    const artist = await findArtist(request, "E2E Artist");
    const dialog = await openAliasDialog(page, artist.id);

    await dialog.locator("button[data-item='import-aliases']").click();

    await expect(
        dialog.locator("[data-part='alert'][data-state='error']"),
    ).toContainText("Online lookup is off.");
    expect(requests).toHaveLength(1);
});
