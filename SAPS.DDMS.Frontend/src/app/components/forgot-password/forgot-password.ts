import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../services/auth';
import { getApiErrorMessage } from '../../validation/api-error';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './forgot-password.html',
  styleUrls: ['./forgot-password.css']
})
export class ForgotPassword {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private toastr = inject(ToastrService);
  private router = inject(Router);
  private cdr = inject(ChangeDetectorRef);

  isLoading = false;
  message = '';
  errorMessage = '';

  forgotForm = this.fb.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(100)]]
  });

  onSubmit(): void {
    if (this.isLoading) return;
    const emailControl = this.forgotForm.get('email');
    emailControl?.setValue(emailControl.value?.trim().toLowerCase() || '', { emitEvent: false });
    if (this.forgotForm.invalid) {
      this.forgotForm.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    this.message = '';
    this.errorMessage = '';

    this.authService.forgotPassword(this.forgotForm.value.email ?? '').subscribe({
      next: (response) => {
        this.message = response.message || 'If the account exists, a one-time PIN has been sent to its registered contact channel.';
        this.toastr.success(this.message, 'Success');
        this.isLoading = false;
        this.cdr.detectChanges();
        const email = this.forgotForm.value.email ?? '';
        setTimeout(() => this.router.navigate(['/reset-password'], { queryParams: { email } }), 800);
      },
      error: (error) => {
        this.errorMessage = getApiErrorMessage(error, 'A reset PIN could not be issued');
        this.toastr.error(this.errorMessage, 'Error');
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }
}
