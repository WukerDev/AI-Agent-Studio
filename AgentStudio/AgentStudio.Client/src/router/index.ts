import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import WorkflowCanvas from '../components/workflow/WorkflowCanvas.vue'

const SettingsView = { template: '<div class="pa-6 text-h5">Ekran ustawień projektu</div>' }

const routes: RouteRecordRaw[] = [
    {
        path: '/',
        name: 'Editor',
        component: WorkflowCanvas
    },
    {
        path: '/settings',
        name: 'Settings',
        component: SettingsView
    }
]

const router = createRouter({
    history: createWebHistory(),
    routes
})

export default router