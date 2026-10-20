import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useNotificationStore } from './notifications'

describe('Notifications Store (src/notifications.ts)', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
    })

    it('should initialize with empty notifications and hidden snackbar', () => {
        const store = useNotificationStore()
        expect(store.notifications).toHaveLength(0)
        expect(store.currentNotification).toBeNull()
        expect(store.isVisible).toBe(false)
    })

    it('should add success notification and make it visible', () => {
        const store = useNotificationStore()
        store.success('Operation succeeded!')

        expect(store.notifications).toHaveLength(1)
        expect(store.currentNotification?.message).toBe('Operation succeeded!')
        expect(store.currentNotification?.type).toBe('success')
        expect(store.isVisible).toBe(true)
    })

    it('should add error notification', () => {
        const store = useNotificationStore()
        store.error('Connection failed!')

        expect(store.currentNotification?.type).toBe('error')
        expect(store.currentNotification?.message).toBe('Connection failed!')
    })

    it('should dismiss notification', () => {
        const store = useNotificationStore()
        store.info('Info message')
        expect(store.isVisible).toBe(true)

        store.dismiss()
        expect(store.isVisible).toBe(false)
    })
})
