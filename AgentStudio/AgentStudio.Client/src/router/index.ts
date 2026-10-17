import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuth } from '../auth'

const routes: RouteRecordRaw[] = [
    {
        path: '/',
        name: 'Editor',
        component: () => import('../components/workflow/WorkflowCanvas.vue')
    },
]

export function setupRouter() {
    const router = createRouter({
        history: createWebHistory(import.meta.env.BASE_URL),
        routes
    })

    router.beforeEach((_to, _from, next) => {
        const authStore = useAuth()

        if (!authStore.isAuthenticated) {
            authStore.login()
            return
        }

        next()
    })

    return router
}