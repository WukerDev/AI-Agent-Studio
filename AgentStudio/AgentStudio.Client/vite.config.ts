import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
import vuetify from 'vite-plugin-vuetify'

export default defineConfig({
    plugins: [
        vue(),
        vuetify({ autoImport: true, styles: process.env.NODE_ENV === 'test' ? 'none' : true })
    ],
    test: {
        globals: true,
        environment: 'jsdom',
        server: {
            deps: {
                inline: ['vuetify']
            }
        }
    }
})
