// Theme previews for theme designers (scripts/theme-preview.ts): a development server per theme
// that shows that theme instead of the theme of the profile, with the appearance "system", so the
// light or dark variant follows the mode of the device (or prefers-color-scheme of the browser that
// takes the screenshots). Several previews can run at once against the same service and profile.

/** Id of the theme a preview server shows; empty outside of a preview and in production builds. */
export const previewThemeId: string = import.meta.env.DEV ? __WAVEMP_PREVIEW_THEME__ : ''
