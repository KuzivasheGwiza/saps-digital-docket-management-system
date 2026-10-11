import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';  // ✅ Already imported, but ensure it's there
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../services/auth';
import { strongPasswordValidator } from '../../validation/input.validators';
import { getApiErrorMessage } from '../../validation/api-error';

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './change-password.html',
  styleUrls: ['./change-password.css']
})
export class ChangePassword {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private toastr = inject(ToastrService);
  private cdr = inject(ChangeDetectorRef);
  
  passwordForm: FormGroup;
  isLoading = false;
  showCurrentPassword = false;
  showNewPassword = false;
  showConfirmPassword = false;
  
  constructor() {
    this.passwordForm = this.fb.group({
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, Validators.minLength(12), Validators.maxLength(128), strongPasswordValidator]],
      confirmNewPassword: ['', [Validators.required]]
    }, { validator: this.passwordMatchValidator });
  }
  
  passwordMatchValidator(g: FormGroup) {
    return g.get('newPassword')?.value === g.get('confirmNewPassword')?.value
      ? null : { mismatch: true };
  }
  
  onSubmit(): void {
    if (this.isLoading) return;
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      this.toastr.error('The password cannot be changed because all fields, a matching confirmation and a strong new password are required.', 'Validation error');
      return;
    }
    
    this.isLoading = true;
    
    this.authService.changePassword(
      this.passwordForm.value.currentPassword,
      this.passwordForm.value.newPassword,
      this.passwordForm.value.confirmNewPassword
    ).subscribe({
      next: () => {
        this.toastr.success('Password changed successfully', 'Success');
        this.isLoading = false;
        this.cdr.detectChanges();
        this.router.navigate(['/dashboard']);
      },
      error: (error) => {
        this.toastr.error(getApiErrorMessage(error, 'The password could not be changed'), 'Error');
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }
  
  toggleCurrentPassword(): void {
    this.showCurrentPassword = !this.showCurrentPassword;
  }
  
  toggleNewPassword(): void {
    this.showNewPassword = !this.showNewPassword;
  }
  
  toggleConfirmPassword(): void {
    this.showConfirmPassword = !this.showConfirmPassword;
  }
  
  goBack(): void {
    this.router.navigate(['/dashboard']);
  }
}
