import { ref } from 'vue';
import { createSharedComposable } from '@vueuse/core';
import Keycloak from 'keycloak-js';
import {useConfig} from "./config.ts";

let keycloakInstance: Keycloak | null = null;

export const useAuth = createSharedComposable(() => {
    const isAuthenticated = ref(false);
    const token = ref('');
    const username = ref('');

    async function setTokenData() {
        if (keycloakInstance?.token) {
            token.value = keycloakInstance.token;
        }
        if (keycloakInstance?.authenticated) {
            const profile = await keycloakInstance.loadUserProfile();
            if (profile.username) {
                username.value = profile.username;
            }
        }
    }

    async function initAuth() {
        const config = useConfig();
        keycloakInstance = new Keycloak({
            url: config.getAuthUrl,
            realm: config.getAuthRealm,
            clientId: config.getAuthClientId
        });

        try {
            const authenticated = await keycloakInstance.init({
                onLoad: 'login-required',
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
            throw new Error('AUTH_INIT_ERROR');
        }
    }

    function login() {
        keycloakInstance?.login();
    }

    function logout() {
        keycloakInstance?.logout();
        isAuthenticated.value = false;
        token.value = '';
        username.value = '';
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