import { useAuthStore } from './auth';

export class AuthViewModel {
    private authStore = useAuthStore();

    public get isAuthenticated(): boolean {
        return this.authStore.isAuthenticated;
    }   

    public get username(): string {
        return this.authStore.username;
    }

    public login(): void {
        this.authStore.login();
    }

    public logout(): void {
        this.authStore.logout();
    }
}