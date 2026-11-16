import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import AdminBackupsView from '../AdminBackupsView.vue'
import type { Backup, Job } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'
import { useBackupsStore } from '@/stores/backups'
import { useBackupSettingsStore } from '@/stores/backupSettings'

const time = new Date('2026-11-16T03:00:00Z')

const job: Job = {
    id: 'database-backup',
    state: 'Idle',
    intervalMinutes: 1440,
    dailyAt: '03:00',
    nextRunAt: new Date('2026-11-17T03:00:00Z'),
    lastRun: {
        id: 'run',
        jobId: 'database-backup',
        status: 'Succeeded',
        trigger: 'Schedule',
        queuedAt: time,
        startedAt: time,
    },
}

const backups: Backup[] = [
    {
        id: 'wavemp-20261116T030000Z-schedule',
        fileName: 'wavemp-20261116T030000Z-schedule.dump',
        createdAt: time,
        sizeBytes: 2_000_000,
        trigger: 'Schedule',
    },
    {
        id: 'wavemp-20261115T120000Z-migration',
        fileName: 'wavemp-20261115T120000Z-migration.dump',
        createdAt: new Date('2026-11-15T12:00:00Z'),
        sizeBytes: 1_000_000,
        trigger: 'Migration',
    },
]

async function mountView(current: { job: Job; backups: Backup[] } | null = { job, backups }) {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push('/admin/backups')
    const store = useBackupsStore()
    const load = vi.spyOn(store, 'load').mockImplementation(async () => {
        store.job = current?.job ?? null
        store.backups = current?.backups ?? []
        store.status = current ? 'ready' : 'error'
    })
    const create = vi.spyOn(store, 'create')
    const remove = vi.spyOn(store, 'remove')
    const settings = useBackupSettingsStore()
    vi.spyOn(settings, 'load').mockImplementation(async () => {
        settings.settings = { scheduleTime: '03:00', retentionCount: 7 }
        settings.status = 'ready'
    })
    const update = vi.spyOn(settings, 'update')
    const wrapper = mount(AdminBackupsView, {
        global: { plugins: [router, createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return { wrapper, load, create, remove, update }
}

describe('AdminBackupsView', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    it('shows the state, the exports and the settings of the backups', async () => {
        const { wrapper } = await mountView()

        expect(wrapper.get('[data-part="view"]').attributes('data-view')).toBe('admin-backups')
        expect(wrapper.get('[data-part="admin-nav-item"][aria-current="page"]').text()).toBe(
            'Backups',
        )
        const sections = wrapper.findAll('section[data-part="section"]')
        expect(sections.map((section) => section.attributes('data-item'))).toEqual([
            'status',
            'backups',
            'settings',
        ])
        const status = wrapper.get('[data-part="backup-status"]')
        expect(status.attributes('data-state')).toBe('idle')
        expect(
            status.get('[data-item="status"] [data-part="badge"]').attributes('data-state'),
        ).toBe('succeeded')
        expect(status.get('[data-item="schedule"] [data-part="stat-value"]').text()).toBe(
            'Daily at 03:00',
        )
        expect(status.get('[data-item="backups"] [data-part="stat-value"]').text()).toBe('2')
        expect(status.get('[data-item="size"] [data-part="stat-value"]').text()).toBe('3 MB')
        const items = wrapper.findAll('[data-part="backup-item"]')
        expect(items.map((item) => item.attributes('data-kind'))).toEqual(['schedule', 'migration'])
        expect(items[1]!.get('[data-part="badge"]').text()).toBe('Before an update')
        expect(items[0]!.get('[data-part="backup-name"]').text()).toBe(
            'wavemp-20261116T030000Z-schedule.dump',
        )
        expect((wrapper.get('input[type="time"]').element as HTMLInputElement).value).toBe('03:00')
        expect((wrapper.get('select').element as HTMLSelectElement).value).toBe('7')
        expect(wrapper.find('[data-part="alert"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('starts an export and loads the state again', async () => {
        const { wrapper, create, load } = await mountView()
        create.mockResolvedValue('started')

        await wrapper.get('[data-part="backup-status"] > [data-part="button"]').trigger('click')
        await flushPromises()

        expect(create).toHaveBeenCalledTimes(1)
        expect(load).toHaveBeenCalledTimes(2)
        wrapper.unmount()
    })

    it('tells when a backup is already running', async () => {
        const { wrapper, create } = await mountView()
        create.mockResolvedValue('alreadyRunning')

        await wrapper.get('[data-part="backup-status"] > [data-part="button"]').trigger('click')
        await flushPromises()

        expect(wrapper.get('[data-part="alert"]').text()).toBe('A backup is already running.')
        wrapper.unmount()
    })

    it('shows the error of the last run and no start while a backup runs', async () => {
        const { wrapper } = await mountView({
            job: {
                ...job,
                state: 'Running',
                lastRun: { ...job.lastRun, status: 'Failed', errorCode: 'backup.failed' },
            },
            backups,
        })

        const status = wrapper.get('[data-part="backup-status"]')
        expect(status.attributes('data-state')).toBe('running')
        expect(
            status.get('[data-item="status"] [data-part="badge"]').attributes('data-state'),
        ).toBe('running')
        expect(status.get('[data-part="button"]').attributes('disabled')).toBeDefined()
        wrapper.unmount()
    })

    it('deletes an export after asking', async () => {
        const { wrapper, remove } = await mountView()
        remove.mockResolvedValue('deleted')

        await wrapper.findAll('[data-part="backup-actions"] button')[1]!.trigger('click')
        expect(wrapper.get('[data-part="dialog-title"]').text()).toBe('Delete export')
        const confirm = wrapper
            .findAll('[data-part="dialog-footer"] button')
            .find((button) => button.text() === 'Delete')
        await confirm!.trigger('click')
        await flushPromises()

        expect(remove).toHaveBeenCalledWith('wavemp-20261115T120000Z-migration')
        expect(wrapper.get('[data-part="alert"]').text()).toBe('The export is deleted.')
        wrapper.unmount()
    })

    it('shows an empty state without exports', async () => {
        const { wrapper } = await mountView({ job, backups: [] })

        expect(wrapper.find('[data-part="backup-list"]').exists()).toBe(false)
        expect(wrapper.get('[data-part="empty-state"]').text()).toBe('There are no exports yet.')
        wrapper.unmount()
    })

    it('saves the time and the number of kept exports', async () => {
        const { wrapper, update } = await mountView()
        update.mockResolvedValue({ scheduleTime: '22:15', retentionCount: 14 })

        await wrapper.get('input[type="time"]').setValue('22:15')
        await wrapper.get('select').setValue('14')
        await wrapper.get('form').trigger('submit')
        await flushPromises()

        expect(update).toHaveBeenCalledWith({ scheduleTime: '22:15', retentionCount: 14 })
        expect(wrapper.get('[data-part="alert"]').text()).toBe('The settings are saved.')
        wrapper.unmount()
    })

    it('rejects a time the server would not accept before sending it', async () => {
        const { wrapper, update } = await mountView()

        await wrapper.get('input[type="time"]').setValue('25:00')
        await wrapper.get('form').trigger('submit')
        await flushPromises()

        expect(update).not.toHaveBeenCalled()
        expect(wrapper.get('[data-field="backup-time"]').attributes('data-state')).toBe('invalid')
        expect(wrapper.get('[data-part="field-error"]').text()).toBe(
            'Enter a time between 00:00 and 23:59.',
        )
        wrapper.unmount()
    })

    it('shows the field errors of the server', async () => {
        const { wrapper, update } = await mountView()
        update.mockRejectedValue({
            responseStatusCode: 400,
            code: 'request.invalid',
            errors: [{ field: 'retentionCount', code: 'out_of_range', min: 1, max: 100 }],
        })

        await wrapper.get('form').trigger('submit')
        await flushPromises()

        expect(wrapper.get('[data-field="backup-retention"]').attributes('data-state')).toBe(
            'invalid',
        )
        expect(wrapper.get('[data-part="field-error"]').text()).toBe(
            'Choose a number between 1 and 100.',
        )
        wrapper.unmount()
    })
})
