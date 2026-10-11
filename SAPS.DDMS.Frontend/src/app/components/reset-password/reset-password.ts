import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../services/auth';
import { getApiErrorMessage } from '../../validation/api-error';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './reset-password.html',
  styleUrls: ['./reset-password.css']
})
export class ResetPassword {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private authService = inject(AuthService);
  private toastr = inject(ToastrService);
  private cdr = inject(ChangeDetectorRef);

  isLoading = false;
  errorMessage = '';
  token = this.route.snapshot.queryParamMap.get('token') ?? '';
  email = this.route.snapshot.queryParamMap.get('email') ?? '';

  resetForm: FormGroup = this.fb.group({
    otpCode: [this.token, [Validators.required, Validators.pattern(/^\d{6}$|^[A-Za-z0-9._~-]{20,}$/)]],
    newPassword: ['', [Validators.required, Validators.minLength(12), Validators.maxLength(128), this.passwordComplexityValidator()]],
    confirmPassword: ['', [Validators.required]]
  }, { validators: this.passwordMatchValidator });

  constructor() {
    // Reset links must work independently of whichever user was already signed in.
    this.authService.clearSession();
  }

  passwordMatchValidator(group: FormGroup) {
    return group.get('newPassword')?.value === group.get('confirmPassword')?.value
      ? null
      : { mismatch: true };
  }

  passwordComplexityValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const value = `${control.value ?? ''}`;
      if (!value) return null;

      const meetsRule = value.length >= 12
        && /[A-Z]/.test(value)
        && /[a-z]/.test(value)
        && /\d/.test(value)
        && /[^A-Za-z0-9]/.test(value);

      return meetsRule ? null : { complexity: true };
    };
  }

  onSubmit(): void {
    if (this.isLoading) return;
    if (this.resetForm.invalid) {
      this.resetForm.markAllAsTouched();
      this.errorMessage = 'The password cannot be reset because a valid 6-digit one-time PIN and matching secure passwords are required.';
      return;
    }

    this.isLoading = true;
    this.errorMessage = '';

    this.authService.resetPassword(
      this.resetForm.value.otpCode.trim(),
      this.resetForm.value.newPassword,
      this.resetForm.value.confirmPassword
    ).subscribe({
      next: () => {
        this.toastr.success('Password reset successfully. Please log in.', 'Success');
        this.isLoading = false;
        this.cdr.detectChanges();
        this.router.navigate(['/login']);
      },
      error: (error) => {
        this.errorMessage = getApiErrorMessage(error, 'The password could not be reset because the one-time PIN may be invalid, expired, or already used');
        this.toastr.error(this.errorMessage, 'Error');
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }
}
