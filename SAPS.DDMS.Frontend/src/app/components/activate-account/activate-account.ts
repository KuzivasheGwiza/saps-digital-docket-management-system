import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../services/auth';
import { strongPasswordValidator } from '../../validation/input.validators';
import { getApiErrorMessage } from '../../validation/api-error';

@Component({
  selector: 'app-activate-account',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './activate-account.html',
  styleUrls: ['./activate-account.css']
})
export class ActivateAccount {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private toastr = inject(ToastrService);
  private cdr = inject(ChangeDetectorRef);
  
  activateForm: FormGroup;
  isLoading = false;
  token = '';
  userId = 0;
  showPassword = false;
  showConfirmPassword = false;
  
  constructor() {
    // Activation links are account-specific and must not inherit the session
    // of an administrator (or any other user) already using this browser.
    this.authService.clearSession();

    this.activateForm = this.fb.group({
      password: ['', [Validators.required, Validators.minLength(12), Validators.maxLength(128), strongPasswordValidator]],
      confirmPassword: ['', [Validators.required]]
    }, { validator: this.passwordMatchValidator });
    
    this.token = typeof this.route.snapshot.queryParams['token'] === 'string'
      ? this.route.snapshot.queryParams['token'].trim()
      : '';
    const suppliedUserId = Number(this.route.snapshot.queryParams['userId']);
    this.userId = Number.isSafeInteger(suppliedUserId) && suppliedUserId > 0 ? suppliedUserId : 0;
  }
  
  passwordMatchValidator(g: FormGroup) {
    return g.get('password')?.value === g.get('confirmPassword')?.value
      ? null : { mismatch: true };
  }
  
  onSubmit(): void {
    if (this.isLoading) return;
    if (this.activateForm.invalid || !this.hasValidActivationLink) {
      this.activateForm.markAllAsTouched();
      return;
    }
    
    this.isLoading = true;
    
    this.authService.activateAccount(
      this.userId, 
      this.token, 
      this.activateForm.value.password
    ).subscribe({
      next: (response) => {
        this.toastr.success('Account activated! Please log in.', 'Success');
        this.isLoading = false;
        this.cdr.detectChanges();
        this.router.navigate(['/login']);
      },
      error: (error) => {
        this.toastr.error(getApiErrorMessage(error, 'The account could not be activated'), 'Error');
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  get hasValidActivationLink(): boolean {
    return this.userId > 0
      && this.token.length >= 20
      && this.token.length <= 512
      && /^[A-Za-z0-9._~-]+$/.test(this.token);
  }
  
  togglePasswordVisibility(): void {
    this.showPassword = !this.showPassword;
  }
  
  toggleConfirmPasswordVisibility(): void {
    this.showConfirmPassword = !this.showConfirmPassword;
  }
}
