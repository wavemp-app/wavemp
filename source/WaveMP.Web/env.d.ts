/// <reference types="vite/client" />

/** Version of the web app, set at build time (see resolveAppVersion in vite.config.ts). */
declare const __WAVEMP_VERSION__: string
/**
 * Theme a theme preview server shows instead of the theme of the profile (scripts/theme-preview.ts);
 * empty otherwise. Development only.
 */
declare const __WAVEMP_PREVIEW_THEME__: string
