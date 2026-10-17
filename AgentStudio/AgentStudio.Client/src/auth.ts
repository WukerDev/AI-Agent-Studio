import { defineStore } from 'pinia';
import Keycloak from 'keycloak-js';
import { useConfigStore } from './config';

let keycloakInstance: Keycloak | null = null;

export const useAuthStore = defineStore('auth', {
    state: () => ({
        isAuthenticated: false,
        token: '',
        username: ''
    }),
    actions: {
        async initAuth() {
            const configStore = useConfigStore();

            keycloakInstance = new Keycloak({
                url: configStore.getAuthUrl,
                realm: configStore.getAuthRealm,
                clientId: configStore.getAuthClientId
            });

            try {
                const authenticated = await keycloakInstance.init({
                    onLoad: 'login-required',
                    checkLoginIframe: false
                });

                this.isAuthenticated = authenticated;

                if (authenticated) {
                    await this.setTokenData();

                    setInterval(() => {
                        keycloakInstance?.updateToken(70).then((refreshed) => {
                            if (refreshed) {
                                this.setTokenData();
                            }
                        }).catch(() => {
                            this.logout();
                        });
                    }, 60000);
                }
            } catch (error) {
                throw new Error('AUTH_INIT_ERROR');
            }
        },
        async setTokenData() {
            if (keycloakInstance?.token) {
                this.token = keycloakInstance.token;
            }
            if (keycloakInstance?.authenticated) {
                const profile = await keycloakInstance.loadUserProfile();
                if (profile.username) {
                    this.username = profile.username;
                }
            }
        },
        login() {
            if (keycloakInstance) {
                keycloakInstance.login();
            }
        },
        logout() {
            if (keycloakInstance) {
                keycloakInstance.logout();
            }
            this.isAuthenticated = false;
            this.token = '';
            this.username = '';
        }
    }
});