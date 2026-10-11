import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';

import { AuthService } from './auth';

describe('AuthService session validation', () => {
  const user = {
    userID: 1,
    firstName: 'System',
    lastName: 'Administrator',
    fullName: 'System Administrator',
    personnelNumber: 'ADMIN001',
    rank: 'Administrator',
    emailAddress: 'admin@example.test',
    phoneNumber: '0123456789',
    roleName: 'System Administrator',
    stationName: 'Head Office',
    isActivated: true,
    isActive: true,
    isLocked: false,
    dateCreated: new Date()
  };

  function tokenWithExpiry(exp: number): string {
    const payload = btoa(JSON.stringify({ exp }))
      .replace(/=/g, '')
      .replace(/\+/g, '-')
      .replace(/\//g, '_');
    return `header.${payload}.signature`;
  }

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient()] });
  });

  it('restores a complete user only when the stored token is still valid', () => {
    localStorage.setItem('accessToken', tokenWithExpiry(Math.floor(Date.now() / 1000) + 3600));
    localStorage.setItem('currentUser', JSON.stringify(user));

    const service = TestBed.inject(AuthService);

    expect(service.isLoggedIn()).toBe(true);
    expect(service.getCurrentUser()?.personnelNumber).toBe('ADMIN001');
  });

  it('clears an expired session instead of allowing protected routes to mount', () => {
    localStorage.setItem('accessToken', tokenWithExpiry(Math.floor(Date.now() / 1000) - 1));
    localStorage.setItem('currentUser', JSON.stringify(user));

    const service = TestBed.inject(AuthService);

    expect(service.isLoggedIn()).toBe(false);
    expect(service.getCurrentUser()).toBeNull();
    expect(localStorage.getItem('accessToken')).toBeNull();
    expect(localStorage.getItem('currentUser')).toBeNull();
  });

  it('clears a malformed token and user record', () => {
    localStorage.setItem('accessToken', 'not-a-jwt');
    localStorage.setItem('currentUser', JSON.stringify(user));

    const service = TestBed.inject(AuthService);

    expect(service.getToken()).toBeNull();
    expect(service.getCurrentUser()).toBeNull();
  });
});
