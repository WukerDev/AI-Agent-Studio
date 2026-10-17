import { useAuth } from './auth';

export class AuthViewModel {
    private authStore = useAuth();

    public get isAuthenticated(): boolean {
        return this.authStore.isAuthenticated.value;
    }

    public get username(): string {
        return this.authStore.username.value;
    }

    public get token(): string {
        return this.authStore.token.value;
    }

    public login(): void {
        this.authStore.login();
    }

    public logout(): void {
        this.authStore.logout();
    }
}