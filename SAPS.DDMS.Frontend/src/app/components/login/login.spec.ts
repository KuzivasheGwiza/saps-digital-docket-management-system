import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { ToastrService } from 'ngx-toastr';
import { of, Subject, throwError } from 'rxjs';

import { Login } from './login';
import { AuthService } from '../../services/auth';

describe('Login', () => {
  let component: Login;
  let fixture: ComponentFixture<Login>;
  let authService: { login: ReturnType<typeof vi.fn>; verifyOtp: ReturnType<typeof vi.fn> };
  let toastr: { success: ReturnType<typeof vi.fn>; error: ReturnType<typeof vi.fn>; info: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    authService = {
      login: vi.fn(),
      verifyOtp: vi.fn()
    };
    toastr = {
      success: vi.fn(),
      error: vi.fn(),
      info: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [RouterTestingModule, Login],
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: ToastrService, useValue: toastr }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(Login);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('shows both required messages and does not call login after a blank submission', () => {
    component.onSubmit();
    fixture.detectChanges();

    const messages = [...fixture.nativeElement.querySelectorAll('.invalid-feedback')]
      .map((element: HTMLElement) => element.textContent?.trim());
    expect(messages).toContain('Personnel number or email address is required.');
    expect(messages).toContain('Password is required.');
    expect(component.loginForm.get('personnelNumber')?.touched).toBe(true);
    expect(component.loginForm.get('password')?.touched).toBe(true);
    expect(authService.login).not.toHaveBeenCalled();
  });

  it('does not show required messages before sign in is attempted', () => {
    component.loginForm.get('personnelNumber')?.markAsTouched();
    component.loginForm.get('password')?.markAsTouched();
    fixture.detectChanges();

    const messages = [...fixture.nativeElement.querySelectorAll('.invalid-feedback')]
      .map((element: HTMLElement) => element.textContent?.trim())
      .filter(Boolean);

    expect(messages).not.toContain('Personnel number or email address is required.');
    expect(messages).not.toContain('Password is required.');
  });

  it('treats whitespace-only login details as empty', () => {
    component.loginForm.patchValue({ personnelNumber: '   ', password: '   ' });

    component.onSubmit();

    expect(component.loginForm.invalid).toBe(true);
    expect(authService.login).not.toHaveBeenCalled();
  });

  it('shows and clears the Caps Lock warning', async () => {
    const passwordInput = fixture.nativeElement.querySelector('#login-password') as HTMLInputElement;
    const capsLockEvent = new KeyboardEvent('keydown', { key: 'A', bubbles: true });
    Object.defineProperty(capsLockEvent, 'getModifierState', {
      value: (modifier: string) => modifier === 'CapsLock'
    });

    passwordInput.dispatchEvent(capsLockEvent);
    await fixture.whenStable();

    const warning = fixture.nativeElement.querySelector('#caps-lock-warning') as HTMLElement;
    expect(warning.textContent?.trim()).toBe('Caps Lock is on.');
    expect(warning.getAttribute('role')).toBe('status');

    passwordInput.dispatchEvent(new FocusEvent('blur', { bubbles: true }));
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('#caps-lock-warning')).toBeNull();
  });

  it('shows a safe incorrect-credentials message for an invalid login', () => {
    authService.login.mockReturnValue(throwError(() => ({
      status: 401,
      error: { message: 'Invalid personnel number or password' }
    })));
    component.loginForm.patchValue({ personnelNumber: 'UNKNOWN001', password: 'WrongPassword!' });

    component.onSubmit();

    expect(component.errorMessage).toBe(
      'Sign-in was denied because the personnel number/email address or password is incorrect. Please check your details and try again.'
    );
    expect(toastr.error).not.toHaveBeenCalled();
  });

  it('shows an actionable message when an account is locked', () => {
    authService.login.mockReturnValue(throwError(() => ({
      status: 401,
      error: { message: 'Account is locked due to failed attempts' }
    })));
    component.loginForm.patchValue({ personnelNumber: 'ADMIN001', password: 'WrongPassword!' });

    component.onSubmit();

    expect(component.errorMessage).toBe(
      'Your account is locked. Contact a system administrator to unlock it.'
    );
  });

  it('trims the personnel number but preserves the password when submitting', () => {
    authService.login.mockReturnValue(of({
      success: true,
      requiresOtp: true,
      pendingPersonnelNumber: 'ADMIN001',
      deliveryHint: 'Enter the PIN.'
    } as any));
    component.loginForm.patchValue({ personnelNumber: '  ADMIN001  ', password: ' Pass word! ' });

    component.onSubmit();

    expect(authService.login).toHaveBeenCalledWith({
      personnelNumber: 'ADMIN001',
      password: ' Pass word! '
    });
  });

  it('handles an empty authentication response without throwing', () => {
    authService.login.mockReturnValue(of(null as any));
    component.loginForm.patchValue({ personnelNumber: 'ADMIN001', password: 'StrongPass12!' });

    expect(() => component.onSubmit()).not.toThrow();
    expect(component.errorMessage).toContain('invalid response');
  });

  it('blocks a second sign-in submission while one is pending', () => {
    authService.login.mockReturnValue(new Subject());
    component.loginForm.patchValue({ personnelNumber: 'ADMIN001', password: 'StrongPass12!' });

    component.onSubmit();
    component.onSubmit();

    expect(authService.login).toHaveBeenCalledTimes(1);
  });
});
