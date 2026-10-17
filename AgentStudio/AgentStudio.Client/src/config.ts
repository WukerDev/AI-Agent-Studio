import { defineStore } from 'pinia';

export const useConfigStore = defineStore('config', {
    state: () => ({
        backendUrl: 'http://localhost:5000',
        eventsUrl: 'http://localhost:5000/events',
        grpcUrl: 'http://localhost:5000',
        authUrl: 'http://localhost:8080',
        authRealm: 'agent-studio',
        authClientId: 'vue-client'
    }),
    getters: {
        getBackendUrl: (state) => state.backendUrl,
        getEventsUrl: (state) => state.eventsUrl,
        getGrpcUrl: (state) => state.grpcUrl,
        getAuthUrl: (state) => state.authUrl,
        getAuthRealm: (state) => state.authRealm,
        getAuthClientId: (state) => state.authClientId,
    },
    actions: {
        updateConfig(newConfig: Partial<typeof this.$state>) {
            this.$patch(newConfig);
        }
    }
});