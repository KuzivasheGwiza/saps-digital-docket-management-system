import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject } from 'rxjs';
import { tap } from 'rxjs/operators';
import { API_URL } from '../config/api.config';
import { LoginRequest, AuthResponse, UserResponse } from '../models/user.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
    private readonly activityKey = 'lastUserActivityAt';
    private readonly timeoutKey = 'sessionTimeoutMinutes';
    private readonly defaultTimeoutMinutes = 60;
    private http = inject(HttpClient);
    private currentUserSubject = new BehaviorSubject<UserResponse | null>(null);
    public currentUser$ = this.currentUserSubject.asObservable();
    
    constructor() {
        this.restoreSession();
    }
    
    login(loginRequest: LoginRequest): Observable<AuthResponse> {
        return this.http.post<AuthResponse>(`${API_URL}/auth/login`, loginRequest)
            .pipe(tap(response => {
                if (response.requiresOtp) {
                    localStorage.removeItem('accessToken');
                    localStorage.removeItem('currentUser');
                    this.currentUserSubject.next(null);
                    return;
                }

                const token = response.token || response.accessToken || response.data?.token || response.data?.accessToken;
                const user = response.user || response.data?.user;
                const authSuccess = response.success !== false && !!token && !!user;

                if (authSuccess && token && user) {
                    localStorage.setItem('accessToken', token);
                    this.storeSessionPolicy(response.sessionTimeoutMinutes ?? response.data?.sessionTimeoutMinutes);
                    this.setCurrentUser(user);
                } else {
                    this.clearSession();
                }
            }));
    }

    verifyOtp(personnelNumber: string, otpCode: string): Observable<AuthResponse> {
        return this.http.post<AuthResponse>(`${API_URL}/auth/verify-otp`, { personnelNumber, otpCode })
            .pipe(tap(response => {
                const token = response.token || response.accessToken || response.data?.token || response.data?.accessToken;
                const user = response.user || response.data?.user;
                if (response.success !== false && token && user) {
                    localStorage.setItem('accessToken', token);
                    this.storeSessionPolicy(response.sessionTimeoutMinutes ?? response.data?.sessionTimeoutMinutes);
                    this.setCurrentUser(user);
                }
            }));
    }
    
    logout(): void {
        const hadToken = !!this.getToken();
        if (hadToken) {
            this.http.post(`${API_URL}/auth/logout`, {}).subscribe({ error: () => {} });
        }
        this.clearSession();
    }

    clearSession(): void {
        localStorage.removeItem('accessToken');
        localStorage.removeItem('currentUser');
        localStorage.removeItem(this.activityKey);
        localStorage.removeItem(this.timeoutKey);
        this.currentUserSubject.next(null);
    }

    setCurrentUser(user: UserResponse): void {
        localStorage.setItem('currentUser', JSON.stringify(user));
        this.recordActivity(true);
        this.currentUserSubject.next(user);
    }

    recordActivity(force = false): void {
        if (!localStorage.getItem('accessToken')) return;
        const now = Date.now();
        const previous = Number(localStorage.getItem(this.activityKey) || 0);
        if (force || now - previous >= 5_000) {
            localStorage.setItem(this.activityKey, String(now));
        }
    }

    hasTimedOutFromInactivity(): boolean {
        if (!localStorage.getItem('accessToken')) return false;
        const lastActivity = Number(localStorage.getItem(this.activityKey) || 0);
        const configured = Number(localStorage.getItem(this.timeoutKey));
        const timeoutMinutes = Number.isFinite(configured) && configured > 0
            ? configured
            : this.defaultTimeoutMinutes;
        return lastActivity > 0 && Date.now() - lastActivity >= timeoutMinutes * 60_000;
    }
    
    getToken(): string | null {
        const token = localStorage.getItem('accessToken');
        if (!token) return null;

        if (!this.isTokenUsable(token)) {
            this.clearSession();
            return null;
        }

        return token;
    }
    
    isLoggedIn(): boolean {
        return !!this.getToken() && !!this.currentUserSubject.value;
    }
    
    getCurrentUser(): UserResponse | null {
        return this.currentUserSubject.value;
    }
    
    hasRole(role: string): boolean {
        const user = this.getCurrentUser();
        return user?.roleName === role;
    }
    
    activateAccount(userId: number, token: string, password: string): Observable<any> {
        return this.http.post(`${API_URL}/auth/activate`, { userId, token, password });
    }
    
    changePassword(currentPassword: string, newPassword: string, confirmNewPassword: string): Observable<any> {
        return this.http.post(`${API_URL}/auth/change-password`, { currentPassword, newPassword, confirmNewPassword });
    }
    
    forgotPassword(email: string): Observable<any> {
        return this.http.post(`${API_URL}/auth/forgot-password`, { email });
    }
    
    resetPassword(token: string, newPassword: string, confirmPassword: string): Observable<any> {
        return this.http.post(`${API_URL}/auth/reset-password`, { token, newPassword, confirmPassword });
    }

    private restoreSession(): void {
        const storedUser = localStorage.getItem('currentUser');
        const storedToken = localStorage.getItem('accessToken');
        if (!storedUser || !storedToken || !this.isTokenUsable(storedToken)) {
            this.clearSession();
            return;
        }

        try {
            const user = JSON.parse(storedUser) as UserResponse;
            if (!user || !user.userID || !user.roleName) {
                throw new Error('Stored user is incomplete');
            }
            this.currentUserSubject.next(user);
            if (!localStorage.getItem(this.activityKey)) this.recordActivity(true);
        } catch {
            this.clearSession();
        }
    }

    private storeSessionPolicy(value: number | undefined): void {
        const timeout = Number.isFinite(Number(value)) && Number(value) > 0
            ? Math.min(120, Math.floor(Number(value)))
            : this.defaultTimeoutMinutes;
        localStorage.setItem(this.timeoutKey, String(timeout));
        localStorage.setItem(this.activityKey, String(Date.now()));
    }

    private isTokenUsable(token: string): boolean {
        try {
            const parts = token.split('.');
            if (parts.length !== 3) return false;

            const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
            const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=');
            const payload = JSON.parse(atob(padded)) as { exp?: number; nbf?: number };
            const now = Math.floor(Date.now() / 1000);

            if (typeof payload.exp !== 'number' || payload.exp <= now) return false;
            if (typeof payload.nbf === 'number' && payload.nbf > now + 30) return false;
            return true;
        } catch {
            return false;
        }
    }
}
