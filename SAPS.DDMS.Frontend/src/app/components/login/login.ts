// src/app/components/login/login.ts
import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  AbstractControl,
  FormsModule,
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  ValidationErrors,
  ValidatorFn,
  Validators
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../services/auth';
import { finalize } from 'rxjs/operators';

const trimmedRequiredValidator: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = control.value;
  return typeof value === 'string' && value.trim().length > 0 ? null : { required: true };
};

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink],
  templateUrl: './login.html',
  styleUrls: ['./login.css']
})
export class Login implements OnInit {
  loginForm: FormGroup;
  isLoading = false;
  errorMessage = '';
  showPassword = false;
  capsLockOn = false;
  submitted = false;
  otpRequired = false;
  pendingPersonnelNumber = '';
  deliveryHint = '';
  
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toastr = inject(ToastrService);
  private cdr = inject(ChangeDetectorRef);
  
  constructor() {
    this.loginForm = this.fb.group({
      personnelNumber: ['', [trimmedRequiredValidator]],
      password: ['', [trimmedRequiredValidator]],
      otpCode: ['', [Validators.pattern(/^\d{6}$/)]]
    });
  }

  ngOnInit(): void {
    this.loginForm.reset({
      personnelNumber: '',
      password: '',
      otpCode: ''
    });
    this.submitted = false;
  }
  
  onSubmit(): void {
    if (this.isLoading) return;
    if (this.otpRequired) {
      this.verifyOtp();
      return;
    }

    this.submitted = true;
    this.errorMessage = '';
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }
    
    this.isLoading = true;
    this.errorMessage = '';
    this.cdr.detectChanges();
    
    const credentials = {
      personnelNumber: this.loginForm.value.personnelNumber?.trim(),
      password: this.loginForm.value.password
    };
    
    this.authService.login(credentials)
      .pipe(finalize(() => {
        this.isLoading = false;
        this.cdr.detectChanges();
      }))
      .subscribe({
        next: (response) => {
          if (response?.requiresOtp) {
            this.otpRequired = true;
            this.submitted = false;
            this.pendingPersonnelNumber = response.pendingPersonnelNumber || credentials.personnelNumber;
            this.deliveryHint = response.deliveryHint || 'Enter the one-time PIN to complete login.';
            this.loginForm.get('otpCode')?.setValidators([Validators.required, Validators.pattern(/^\d{6}$/)]);
            this.loginForm.get('otpCode')?.updateValueAndValidity();
            this.toastr.info('One-time PIN required', 'Two-factor authentication');
            this.cdr.detectChanges();
            return;
          }

          const token = response?.token || response?.accessToken || response?.data?.token || response?.data?.accessToken;
          if (response?.success !== false && token) {
            this.toastr.success('Login successful!', 'Welcome');
            this.navigateAfterLogin();
          } else {
            this.errorMessage = response?.message || 'Sign-in could not be completed because the authentication service returned an invalid response. Please try again.';
            this.cdr.detectChanges();
          }
        },
        error: (error) => {
          this.errorMessage = this.getLoginErrorMessage(error);
          this.cdr.detectChanges();
        }
      });
  }

  verifyOtp(): void {
    if (this.isLoading) return;
    const otpCode = this.loginForm.value.otpCode?.trim();
    if (!/^\d{6}$/.test(otpCode || '')) {
      this.errorMessage = 'Enter the 6-digit one-time PIN.';
      this.loginForm.get('otpCode')?.markAsTouched();
      return;
    }

    this.isLoading = true;
    this.errorMessage = '';
    this.cdr.detectChanges();

    this.authService.verifyOtp(this.pendingPersonnelNumber, otpCode)
      .pipe(finalize(() => {
        this.isLoading = false;
        this.cdr.detectChanges();
      }))
      .subscribe({
        next: (response) => {
          const token = response?.token || response?.accessToken || response?.data?.token || response?.data?.accessToken;
          if (response?.success !== false && token) {
            this.toastr.success('Login successful!', 'Welcome');
            this.navigateAfterLogin();
          } else {
            this.errorMessage = response?.message || 'The one-time PIN could not be verified because the authentication service returned an invalid response.';
          }
        },
        error: (error) => {
          this.errorMessage = error.error?.message || 'OTP verification failed.';
        }
      });
  }

  restartLogin(): void {
    this.otpRequired = false;
    this.submitted = false;
    this.capsLockOn = false;
    this.errorMessage = '';
    this.pendingPersonnelNumber = '';
    this.deliveryHint = '';
    this.loginForm.get('otpCode')?.clearValidators();
    this.loginForm.get('otpCode')?.setValue('');
    this.loginForm.get('otpCode')?.updateValueAndValidity();
  }
  
  togglePasswordVisibility(): void {
    this.showPassword = !this.showPassword;
  }

  isFieldInvalid(controlName: 'personnelNumber' | 'password' | 'otpCode'): boolean {
    const control = this.loginForm.get(controlName);
    if (!control || !control.invalid) {
      return false;
    }

    return controlName === 'otpCode'
      ? control.touched || this.submitted
      : this.submitted;
  }

  clearLoginError(): void {
    this.errorMessage = '';
  }

  updateCapsLockState(event: KeyboardEvent): void {
    this.capsLockOn = event.getModifierState('CapsLock');
  }

  onPasswordBlur(): void {
    this.capsLockOn = false;
  }

  private getLoginErrorMessage(error: any): string {
    const serverMessage = typeof error?.error?.message === 'string'
      ? error.error.message.trim()
      : '';

    if (error?.status === 401) {
      const normalisedMessage = serverMessage.toLowerCase();
      if (normalisedMessage.includes('not activated')) {
        return 'Your account has not been activated. Use your activation link or contact a system administrator.';
      }
      if (normalisedMessage.includes('locked')) {
        return 'Your account is locked. Contact a system administrator to unlock it.';
      }
      if (normalisedMessage.includes('suspended')) {
        return 'Your account is suspended. Contact a system administrator for assistance.';
      }
      if (normalisedMessage.includes('deactivated') || normalisedMessage.includes('not eligible')) {
        return 'Your account is not permitted to sign in. Contact a system administrator for assistance.';
      }
      return 'Sign-in was denied because the personnel number/email address or password is incorrect. Please check your details and try again.';
    }

    if (error?.status === 400) {
      return serverMessage || 'Sign-in could not continue because one or more login details are invalid. Please correct the highlighted fields.';
    }
    if (error?.status === 429) {
      return 'Too many sign-in attempts. Please wait a moment before trying again.';
    }
    if (error?.status === 0) {
      return 'The login service could not be reached. Check your connection and try again.';
    }
    if (error?.status >= 500) {
      return 'The login service is temporarily unavailable. Please try again shortly.';
    }
    return serverMessage || 'Sign-in could not be completed because the authentication service rejected the request. Please try again.';
  }

  private navigateAfterLogin(): void {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    const safeReturnUrl = returnUrl?.startsWith('/') && !returnUrl.startsWith('//')
      ? returnUrl
      : '/dashboard';
    void this.router.navigateByUrl(safeReturnUrl);
  }
}
