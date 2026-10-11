import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { InitService } from './init';
import { AuthService } from './auth';
import { UserService } from './user';

describe('InitService', () => {
  let authService: { isLoggedIn: ReturnType<typeof vi.fn>; setCurrentUser: ReturnType<typeof vi.fn> };
  let userService: { getProfile: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    authService = {
      isLoggedIn: vi.fn().mockReturnValue(true),
      setCurrentUser: vi.fn()
    };
    userService = {
      getProfile: vi.fn().mockReturnValue(of({ userID: 7, roleName: 'Investigating Officer' }))
    };
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: UserService, useValue: userService }
      ]
    });
  });

  it('refreshes every signed-in role through the authenticated profile endpoint', async () => {
    const service = TestBed.inject(InitService);

    await service.initialize();

    expect(userService.getProfile).toHaveBeenCalledTimes(1);
    expect(authService.setCurrentUser).toHaveBeenCalledWith({
      userID: 7,
      roleName: 'Investigating Officer'
    });
  });

  it('does not call the API when no valid session exists', async () => {
    authService.isLoggedIn.mockReturnValue(false);
    const service = TestBed.inject(InitService);

    await service.initialize();

    expect(userService.getProfile).not.toHaveBeenCalled();
  });
});
