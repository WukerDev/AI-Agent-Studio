import { createApp } from 'vue'
import { createI18n } from 'vue-i18n'
import router from './router'
import 'vuetify/styles'
import { createVuetify, type ThemeDefinition } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import '@mdi/font/css/materialdesignicons.css'
import './style.css'
import App from './App.vue'
import pl from './locales/pl.json'

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

const i18n = createI18n({
    legacy: false,
    locale: 'pl',
    fallbackLocale: 'pl',
    messages: {
        pl
    }
})

const vuetify = createVuetify({
    components,
    directives,
    theme: {
        defaultTheme: 'agentStudioDarkTheme',
        themes: {
            agentStudioDarkTheme,
        }
    }
})

const app = createApp(App)

app.use(i18n)
app.use(router)
app.use(vuetify)

app.mount('#app')