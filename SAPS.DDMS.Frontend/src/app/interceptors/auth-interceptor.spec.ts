import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, HttpInterceptorFn, HttpRequest, HttpResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { authInterceptor } from './auth-interceptor';
import { AuthService } from '../services/auth';

describe('authInterceptor', () => {
  let authService: { getToken: ReturnType<typeof vi.fn>; clearSession: ReturnType<typeof vi.fn> };
  let router: { url: string; navigate: ReturnType<typeof vi.fn> };

  const intercept: HttpInterceptorFn = (req, next) =>
    TestBed.runInInjectionContext(() => authInterceptor(req, next));

  beforeEach(() => {
    authService = {
      getToken: vi.fn().mockReturnValue('valid-token'),
      clearSession: vi.fn()
    };
    router = {
      url: '/dockets',
      navigate: vi.fn().mockResolvedValue(true)
    };
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router }
      ]
    });
  });

  it('adds the bearer token once', () => {
    const next = vi.fn().mockImplementation((request: HttpRequest<unknown>) => {
      expect(request.headers.get('Authorization')).toBe('Bearer valid-token');
      return of(new HttpResponse({ status: 200 }));
    });

    intercept(new HttpRequest('GET', '/api/docket'), next).subscribe();

    expect(next).toHaveBeenCalledTimes(1);
  });

  it('clears a rejected session and redirects instead of retrying the same token', () => {
    const next = vi.fn().mockReturnValue(throwError(() => new HttpErrorResponse({ status: 401 })));

    intercept(new HttpRequest('GET', '/api/docket'), next).subscribe({ error: () => {} });

    expect(next).toHaveBeenCalledTimes(1);
    expect(authService.clearSession).toHaveBeenCalledTimes(1);
    expect(router.navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { returnUrl: '/dockets' }
    });
  });

  it('does not redirect when the login endpoint rejects bad credentials', () => {
    const next = vi.fn().mockReturnValue(throwError(() => new HttpErrorResponse({ status: 401 })));

    intercept(new HttpRequest('POST', '/api/auth/login', null), next).subscribe({ error: () => {} });

    expect(authService.clearSession).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
