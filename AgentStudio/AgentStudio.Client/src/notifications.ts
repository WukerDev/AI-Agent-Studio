import { defineStore } from 'pinia'
import { ref } from 'vue'

export type NotificationType = 'success' | 'error' | 'info' | 'warning'

export interface AppNotification {
    id: string
    message: string
    type: NotificationType
    timeout: number
    timestamp: number
}

export const useNotificationStore = defineStore('notifications', () => {
    const notifications = ref<AppNotification[]>([])
    const currentNotification = ref<AppNotification | null>(null)
    const isVisible = ref(false)

    function notify(message: string, type: NotificationType = 'info', timeout = 4000) {
        const item: AppNotification = {
            id: `notif_${Date.now()}_${Math.random().toString(36).substring(2, 6)}`,
            message,
            type,
            timeout,
            timestamp: Date.now()
        }

        notifications.value.push(item)
        currentNotification.value = item
        isVisible.value = true
    }

    function success(message: string, timeout = 3500) {
        notify(message, 'success', timeout)
    }

    function error(message: string, timeout = 5000) {
        notify(message, 'error', timeout)
    }

    function info(message: string, timeout = 3500) {
        notify(message, 'info', timeout)
    }

    function warning(message: string, timeout = 4000) {
        notify(message, 'warning', timeout)
    }

    function dismiss() {
        isVisible.value = false
    }

    return {
        notifications,
        currentNotification,
        isVisible,
        notify,
        success,
        error,
        info,
        warning,
        dismiss
    }
})
