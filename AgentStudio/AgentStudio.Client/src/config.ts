import { reactive, computed } from 'vue';
import { createSharedComposable } from '@vueuse/core';

export interface AppConfig {
    backendUrl: string;
    eventsUrl: string;
    grpcUrl: string;
    authUrl: string;
    authRealm: string;
    authClientId: string;
}

export const useConfig = createSharedComposable(() => {
    const state = reactive<AppConfig>({
        backendUrl: 'http://localhost:5000',
        eventsUrl: 'http://localhost:5000/events',
        grpcUrl: 'http://localhost:5000',
        authUrl: 'http://localhost:8080',
        authRealm: 'agent-studio',
        authClientId: 'portal'
    });

    function updateConfig(newConfig: Partial<AppConfig>) {
        Object.assign(state, newConfig);
    }

    return reactive({
        ...state,
        getBackendUrl: computed(() => state.backendUrl),
        getEventsUrl: computed(() => state.eventsUrl),
        getGrpcUrl: computed(() => state.grpcUrl),
        getAuthUrl: computed(() => state.authUrl),
        getAuthRealm: computed(() => state.authRealm),
        getAuthClientId: computed(() => state.authClientId),
        updateConfig
    });
});