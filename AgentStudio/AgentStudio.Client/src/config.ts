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
    const isBrowser = typeof window !== 'undefined';
    const isDevVite = isBrowser && (window.location.port === '5173' || window.location.port === '4173');
    
    const defaultGatewayUrl = isBrowser && !isDevVite
        ? window.location.origin
        : (import.meta.env.VITE_GATEWAY_URL || 'http://localhost:5000');

    const defaultAuthUrl = import.meta.env.VITE_AUTH_URL || (
        isBrowser && !isDevVite
            ? window.location.origin
            : 'http://localhost:8080'
    );

    const state = reactive<AppConfig>({
        backendUrl: defaultGatewayUrl,
        eventsUrl: `${defaultGatewayUrl}/events`,
        grpcUrl: defaultGatewayUrl,
        authUrl: defaultAuthUrl,
        authRealm: import.meta.env.VITE_AUTH_REALM || 'agent-studio',
        authClientId: import.meta.env.VITE_AUTH_CLIENT_ID || 'portal'
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
