import { ref } from 'vue';
import { createSharedComposable } from '@vueuse/core';
import Keycloak from 'keycloak-js';
import { useConfig } from './config.ts';

let keycloakInstance: Keycloak | null = null;

export function ensureCryptoRandomUuid(): void {
    if (typeof globalThis.crypto === 'undefined' || typeof globalThis.crypto.randomUUID === 'function') {
        return;
    }

    if (typeof globalThis.crypto.getRandomValues !== 'function') {
        return;
    }

    const fallbackRandomUUID = (): `${string}-${string}-${string}-${string}-${string}` => {
        const bytes = new Uint8Array(16);
        globalThis.crypto.getRandomValues(bytes);
        bytes[6] = (bytes[6]! & 0x0f) | 0x40;
        bytes[8] = (bytes[8]! & 0x3f) | 0x80;

        const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
        return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}` as `${string}-${string}-${string}-${string}-${string}`;
    };

    try {
        Object.defineProperty(globalThis.crypto, 'randomUUID', {
            value: fallbackRandomUUID,
            configurable: true,
            writable: true
        });
    } catch {
        if (typeof Crypto !== 'undefined' && Crypto.prototype) {
            Object.defineProperty(Crypto.prototype, 'randomUUID', {
                value: fallbackRandomUUID,
                configurable: true,
                writable: true
            });
        }
    }
}

export function resolvePkceMethod(): 'S256' | false {
    const hasSubtleCrypto =
        typeof globalThis.crypto !== 'undefined' &&
        typeof globalThis.crypto.subtle !== 'undefined' &&
        typeof globalThis.crypto.subtle.digest === 'function';

    return hasSubtleCrypto ? 'S256' : false;
}

export const useAuth = createSharedComposable(() => {
    const isAuthenticated = ref(false);
    const token = ref('');
    const username = ref('');

    async function setTokenData() {
        if (keycloakInstance?.token) {
            token.value = keycloakInstance.token;
        }
        if (keycloakInstance?.authenticated) {
            const tokenUsername = (keycloakInstance.tokenParsed as { preferred_username?: string } | undefined)?.preferred_username;
            if (tokenUsername) {
                username.value = tokenUsername;
            }
            try {
                const profile = await keycloakInstance.loadUserProfile();
                if (profile.username) {
                    username.value = profile.username;
                }
            } catch (profileError) {
                console.warn('[AgentStudio] Could not load Keycloak user profile, using token claims:', profileError);
            }
        }
    }

    async function initAuth() {
        ensureCryptoRandomUuid();

        const config = useConfig();
        keycloakInstance = new Keycloak({
            url: config.getAuthUrl,
            realm: config.getAuthRealm,
            clientId: config.getAuthClientId
        });

        try {
            const pkceMethod = resolvePkceMethod();
            const authenticated = await keycloakInstance.init({
                onLoad: 'login-required',
                checkLoginIframe: false,
                pkceMethod
            });

            isAuthenticated.value = authenticated;

            if (authenticated) {
                await setTokenData();

                setInterval(() => {
                    keycloakInstance?.updateToken(70).then((refreshed) => {
                        if (refreshed) {
                            setTokenData();
                        }
                    }).catch(() => {
                        logout();
                    });
                }, 60000);
            }
        } catch (error) {
            console.error('[AgentStudio] Keycloak auth initialization failed:', error);
            throw new Error('AUTH_INIT_ERROR');
        }
    }

    function getPortalRedirectUri(): string | undefined {
        if (typeof window === 'undefined') {
            return undefined;
        }
        return `${window.location.origin}/portal/`;
    }

    function login() {
        ensureCryptoRandomUuid();
        const redirectUri = getPortalRedirectUri();
        keycloakInstance?.login(redirectUri ? { redirectUri } : undefined);
    }

    function logout() {
        const redirectUri = getPortalRedirectUri();
        const wasAuthenticated = isAuthenticated.value;
        isAuthenticated.value = false;
        token.value = '';
        username.value = '';

        if (keycloakInstance && wasAuthenticated) {
            keycloakInstance.logout(redirectUri ? { redirectUri } : undefined);
        } else {
            login();
        }
    }

    return {
        isAuthenticated,
        token,
        username,
        initAuth,
        setTokenData,
        login,
        logout
    };
});