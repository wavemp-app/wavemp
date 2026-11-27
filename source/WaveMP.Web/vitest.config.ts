import { fileURLToPath } from 'node:url'
import { mergeConfig, defineConfig, configDefaults } from 'vitest/config'
import viteConfig from './vite.config.ts'

export default mergeConfig(
    viteConfig,
    defineConfig({
        test: {
            environment: 'jsdom',
            include: ['src/**/__tests__/*.spec.ts', 'scripts/**/__tests__/*.spec.ts'],
            exclude: [...configDefaults.exclude],
            root: fileURLToPath(new URL('./', import.meta.url)),
            // The largest tests (the component overview, long track lists) take two to three
            // seconds alone and more than the default limit of five seconds while other test
            // runs (dotnet test, Playwright) share the machine.
            testTimeout: 20_000,
        },
    }),
)
