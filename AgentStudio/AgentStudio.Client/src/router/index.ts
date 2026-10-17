import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '../auth'

const routes: RouteRecordRaw[] = [
    {
        path: '/',
        name: 'Editor',
        component: () => import('../components/workflow/WorkflowCanvas.vue')
    },
]

const router = createRouter({
    history: createWebHistory(import.meta.env.BASE_URL),
    routes
})
//@ts-ignore
router.beforeEach((to, from, next) => {
    const authStore = useAuthStore()

    if (!authStore.isAuthenticated) {
        authStore.login()
        return
    }

    next()
})

export default router