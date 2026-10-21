import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
import vuetify from 'vite-plugin-vuetify'

export default defineConfig({
    base: '/portal/',
    plugins: [
        vue(),
        vuetify({ autoImport: true, styles: process.env.NODE_ENV === 'test' ? 'none' : true })
    ],
    test: {
        globals: true,
        environment: 'jsdom',
        execArgv: ['--experimental-require-module'],
        server: {
            deps: {
                inline: ['vuetify']
            }
        }
    }
})
