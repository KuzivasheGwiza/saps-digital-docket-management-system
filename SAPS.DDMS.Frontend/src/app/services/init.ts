import { Injectable, inject } from '@angular/core';
import { AuthService } from './auth';
import { UserService } from './user';
import { firstValueFrom } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class InitService {
  private authService = inject(AuthService);
  private userService = inject(UserService);
  
  async initialize(): Promise<void> {
    // Public token pages must boot independently of any cached signed-in user.
    // In particular, an administrator's session must never redirect activation.
    if (this.isPublicTokenPage()) return;
    if (!this.authService.isLoggedIn()) return;

    try {
      const freshUser = await firstValueFrom(this.userService.getProfile());
      if (freshUser) {
        this.authService.setCurrentUser(freshUser);
      }
    } catch (error: any) {
      // A 401 is handled centrally by the auth interceptor. For a temporary
      // server/network failure, keep the still-valid cached session available.
      if (error?.status !== 401) {
        console.warn('Could not refresh the signed-in user profile.', error);
      }
    }
  }

  private isPublicTokenPage(): boolean {
    if (typeof window === 'undefined') return false;
    return ['/activate', '/reset-password'].includes(window.location.pathname.replace(/\/$/, ''));
  }
}
