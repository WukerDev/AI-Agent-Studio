import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import { setupRouter } from './router'
import 'vuetify/styles'
import { createVuetify, type ThemeDefinition } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import '@mdi/font/css/materialdesignicons.css'
import './style.css'
import App from './App.vue'
import pl from './locales/pl.json'
import en from './locales/en.json'
import { useAuth } from './auth'

const agentStudioDarkTheme: ThemeDefinition = {
    dark: true,
    colors: {
        background: '#0B1120',
        surface: '#172136',
        primary: '#E63946',
        secondary: '#1D2A45',
        error: '#FF5252',
        info: '#2196F3',
        success: '#4CAF50',
        warning: '#FB8C00',
    }
}

const agentStudioLightTheme: ThemeDefinition = {
    dark: false,
    colors: {
        background: '#F8FAFC',
        surface: '#FFFFFF',
        primary: '#E63946',
        secondary: '#E2E8F0',
        error: '#EF4444',
        info: '#3B82F6',
        success: '#10B981',
        warning: '#F59E0B',
    }
}

export const i18n = createI18n({
    legacy: false,
    locale: 'pl',
    fallbackLocale: 'en',
    messages: {
        pl,
        en
    }
})

const vuetify = createVuetify({
    components,
    directives,
    theme: {
        defaultTheme: 'agentStudioDarkTheme',
        themes: {
            agentStudioDarkTheme,
            agentStudioLightTheme
        }
    }
})

function mountApp() {
    const app = createApp(App)
    const pinia = createPinia()
    const router = setupRouter()

    app.use(pinia)
    app.use(i18n)
    app.use(router)
    app.use(vuetify)
    app.mount('#app')
}

const { initAuth } = useAuth()

initAuth()
    .then(() => {
        mountApp()
    })
    .catch((err) => {
        console.warn('[AgentStudio] Keycloak IAM unavailable or offline, continuing in guest mode:', err)
        mountApp()
    })
