<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { ProblemCodeObject, RoleObject, type User } from '@/api/generated/models'
import { problemCodeOf } from '@/api/problems'
import AdminNav from '@/components/admin/AdminNav.vue'
import UserDeleteDialog from '@/components/admin/UserDeleteDialog.vue'
import UserEditDialog from '@/components/admin/UserEditDialog.vue'
import UserList, { type UserListItem } from '@/components/admin/UserList.vue'
import UserPinResetDialog from '@/components/admin/UserPinResetDialog.vue'
import BaseAlert from '@/components/base/BaseAlert.vue'
import BaseButton from '@/components/base/BaseButton.vue'
import { useSessionStore } from '@/stores/session'
import { useUsersStore } from '@/stores/users'

/** Admin page: create, edit and delete profiles and remove their PINs. */
const { t } = useI18n()
const router = useRouter()
const session = useSessionStore()
const usersStore = useUsersStore()

const editOpen = ref(false)
const editing = ref<UserListItem | null>(null)
const deleteOpen = ref(false)
const deleting = ref<UserListItem | null>(null)
const deleteBusy = ref(false)
const resetPinOpen = ref(false)
const resettingPin = ref<UserListItem | null>(null)
const resetPinBusy = ref(false)
const message = ref<string | null>(null)
const notice = ref<string | null>(null)

function toItem(user: User): UserListItem | null {
    if (!user.id || !user.name || !user.initials || !user.color) {
        return null
    }

    return {
        id: user.id,
        name: user.name,
        initials: user.initials,
        color: user.color,
        isAdmin: user.role === RoleObject.Admin,
        isBuiltIn: !!user.isBuiltIn,
        hasPin: !!user.hasPin,
        createdAt: user.createdAt ?? null,
    }
}

const items = computed(() => usersStore.users.map(toItem).filter((item) => item !== null))

function openCreate(): void {
    message.value = null
    notice.value = null
    editing.value = null
    editOpen.value = true
}

function openEdit(user: UserListItem): void {
    message.value = null
    notice.value = null
    editing.value = user
    editOpen.value = true
}

function openResetPin(user: UserListItem): void {
    message.value = null
    notice.value = null
    resettingPin.value = user
    resetPinOpen.value = true
}

function openDelete(user: UserListItem): void {
    message.value = null
    notice.value = null
    deleting.value = user
    deleteOpen.value = true
}

async function onSaved(user: User): Promise<void> {
    await usersStore.load()
    // Name, initials, color or role of the own profile changed.
    if (user.id === session.profile?.id) {
        await session.refresh()
        if (!session.isAdmin) {
            await router.replace({ name: 'library' })
        }
    }
}

async function confirmDelete(): Promise<void> {
    const user = deleting.value
    if (!user || deleteBusy.value) {
        return
    }

    deleteBusy.value = true
    try {
        await usersStore.remove(user.id)
        deleteOpen.value = false
        if (user.id === session.profile?.id) {
            // The own profile is gone together with its session.
            await router.replace({ name: 'profiles' })
            session.clear()
            return
        }
    } catch (failure) {
        deleteOpen.value = false
        switch (problemCodeOf(failure)) {
            case ProblemCodeObject.UserNot_found:
                message.value = t('adminUsers.errors.notFound')
                break
            case ProblemCodeObject.UserBuilt_in_admin:
                message.value = t('adminUsers.builtInNotDeletable')
                break
            default:
                message.value = t('adminUsers.errors.deleteFailed')
        }
    } finally {
        deleteBusy.value = false
    }

    await usersStore.load()
}

async function confirmResetPin(): Promise<void> {
    const user = resettingPin.value
    if (!user || resetPinBusy.value) {
        return
    }

    resetPinBusy.value = true
    try {
        await usersStore.resetPin(user.id)
        resetPinOpen.value = false
        notice.value = t('adminUsers.pinReset', { name: user.name })
        if (user.id === session.profile?.id) {
            await session.refresh()
        }
    } catch (failure) {
        resetPinOpen.value = false
        switch (problemCodeOf(failure)) {
            case ProblemCodeObject.UserNot_found:
                message.value = t('adminUsers.errors.notFound')
                break
            case ProblemCodeObject.PinManaged_by_environment:
                message.value = t('adminUsers.errors.pinManaged')
                break
            default:
                message.value = t('adminUsers.errors.resetPinFailed')
        }
    } finally {
        resetPinBusy.value = false
    }

    await usersStore.load()
}

onMounted(() => {
    void usersStore.load()
})
</script>

<template>
    <div
        data-part="view"
        data-view="admin-users"
        :aria-busy="usersStore.status === 'loading' ? 'true' : undefined"
    >
        <AdminNav />
        <div data-part="view-actions">
            <BaseButton
                variant="primary"
                icon="add"
                :label="t('adminUsers.add')"
                @click="openCreate"
            />
        </div>
        <template v-if="usersStore.status === 'error'">
            <BaseAlert tone="error" :message="t('adminUsers.loadError')" />
            <div data-part="view-actions">
                <BaseButton
                    data-item="retry"
                    :label="t('library.retry')"
                    @click="usersStore.load()"
                />
            </div>
        </template>
        <BaseAlert v-if="message" tone="error" :message="message" />
        <BaseAlert v-if="notice" tone="success" :message="notice" />
        <UserList :users="items" @edit="openEdit" @delete="openDelete" @reset-pin="openResetPin" />
        <UserEditDialog v-model:open="editOpen" :user="editing" @saved="onSaved" />
        <UserDeleteDialog
            v-model:open="deleteOpen"
            :user="deleting"
            :busy="deleteBusy"
            @confirm="confirmDelete"
        />
        <UserPinResetDialog
            v-model:open="resetPinOpen"
            :user="resettingPin"
            :busy="resetPinBusy"
            @confirm="confirmResetPin"
        />
    </div>
</template>
