import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import WorkflowCanvas from '../components/workflow/WorkflowCanvas.vue'

const routes: RouteRecordRaw[] = [
    {
        path: '/',
        name: 'Editor',
        component: WorkflowCanvas
    },
]

export function setupRouter() {
    const router = createRouter({
        history: createWebHistory(import.meta.env.BASE_URL),
        routes
    })

    return router
}
