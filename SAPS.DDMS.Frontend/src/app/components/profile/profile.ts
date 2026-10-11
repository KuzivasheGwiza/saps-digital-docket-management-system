// src/app/components/profile/profile.ts
import { ChangeDetectorRef, Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../services/auth';
import { UserService } from '../../services/user';
import { UserResponse } from '../../models/user.model';
import { SOUTH_AFRICAN_CONTACT_PATTERN } from '../../validation/contact.validators';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, ReactiveFormsModule],
  templateUrl: './profile.html',
  styleUrls: ['./profile.css']
})
export class Profile implements OnInit, OnDestroy {
  @ViewChild('cameraVideo') cameraVideo?: ElementRef<HTMLVideoElement>;
  private authService = inject(AuthService);
  private userService = inject(UserService);
  private toastr = inject(ToastrService);
  private fb = inject(FormBuilder);
  private cdr = inject(ChangeDetectorRef);

  user: UserResponse | null = null;
  isLoading = true;
  isSaving = false;
  isEditing = false;
  isUploadingPhoto = false;
  profilePhotoUrl = '';
  photoValidationMessage = '';
  showCamera = false;
  showPhotoMenu = false;
  isStartingCamera = false;
  private cameraStream: MediaStream | null = null;

  // UC 2.5 — Update Profile form (editable fields only)
  profileForm: FormGroup;

  constructor() {
    this.profileForm = this.fb.group({
      phoneNumber: ['', [Validators.pattern(SOUTH_AFRICAN_CONTACT_PATTERN)]],
      notificationPreferences: ['InApp']
    });
  }

  ngOnInit(): void {
    this.loadProfile();
    this.loadProfilePhoto();
  }

  ngOnDestroy(): void {
    this.stopCamera();
    this.revokeProfilePhoto();
  }

  async openCamera(): Promise<void> {
    this.showPhotoMenu = false;
    this.photoValidationMessage = '';
    if (!navigator.mediaDevices?.getUserMedia) {
      this.photoValidationMessage = 'This browser cannot access a camera. Use Choose photo instead.';
      return;
    }

    this.isStartingCamera = true;
    try {
      this.cameraStream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'user', width: { ideal: 1280 }, height: { ideal: 720 } },
        audio: false
      });
      this.showCamera = true;
      this.isStartingCamera = false;
      this.cdr.detectChanges();
      const video = this.cameraVideo?.nativeElement;
      if (video) {
        video.srcObject = this.cameraStream;
        await video.play();
      }
    } catch (error) {
      this.isStartingCamera = false;
      this.stopCamera();
      const denied = error instanceof DOMException && ['NotAllowedError', 'SecurityError'].includes(error.name);
      this.photoValidationMessage = denied
        ? 'Camera access was not granted. Allow camera permission in the browser, then try again.'
        : 'The camera could not be opened. Check that it is connected and not being used by another application.';
      this.cdr.detectChanges();
    }
  }

  closeCamera(): void {
    this.stopCamera();
    this.showCamera = false;
    this.cdr.detectChanges();
  }

  capturePhoto(): void {
    const video = this.cameraVideo?.nativeElement;
    if (!video || !video.videoWidth || !video.videoHeight) {
      this.photoValidationMessage = 'The camera is still starting. Wait for the preview, then capture the photo.';
      return;
    }

    const canvas = document.createElement('canvas');
    const maximumWidth = 1280;
    const scale = Math.min(1, maximumWidth / video.videoWidth);
    canvas.width = Math.round(video.videoWidth * scale);
    canvas.height = Math.round(video.videoHeight * scale);
    const context = canvas.getContext('2d');
    if (!context) {
      this.photoValidationMessage = 'The captured photo could not be prepared for upload.';
      return;
    }
    context.drawImage(video, 0, 0, canvas.width, canvas.height);
    canvas.toBlob(blob => {
      if (!blob) {
        this.photoValidationMessage = 'The captured photo could not be prepared for upload.';
        this.cdr.detectChanges();
        return;
      }
      const photo = new File([blob], `profile-photo-${Date.now()}.jpg`, { type: 'image/jpeg' });
      this.closeCamera();
      this.uploadProfilePhoto(photo);
    }, 'image/jpeg', 0.88);
  }

  loadProfilePhoto(): void {
    this.userService.getProfilePhoto().subscribe({
      next: blob => {
        if (!(blob instanceof Blob) || blob.size === 0) return;
        this.revokeProfilePhoto();
        this.profilePhotoUrl = URL.createObjectURL(blob);
        this.cdr.detectChanges();
      },
      error: () => { /* An absent photo uses the standard fallback avatar. */ }
    });
  }

  onProfilePhotoSelected(event: Event): void {
    this.showPhotoMenu = false;
    const input = event.target as HTMLInputElement;
    const photo = input.files?.[0];
    this.photoValidationMessage = '';
    if (!photo) return;
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(photo.type.toLowerCase())) {
      this.photoValidationMessage = 'Select a JPEG, PNG or WebP image.';
      input.value = '';
      return;
    }
    if (photo.size > 2 * 1024 * 1024) {
      this.photoValidationMessage = 'The profile photo cannot exceed 2 MB.';
      input.value = '';
      return;
    }

    this.uploadProfilePhoto(photo, input);
  }

  togglePhotoMenu(): void {
    if (this.isUploadingPhoto || this.isStartingCamera) return;
    this.showPhotoMenu = !this.showPhotoMenu;
  }

  @HostListener('document:click', ['$event'])
  closePhotoMenuOutside(event: MouseEvent): void {
    const target = event.target as Element | null;
    if (this.showPhotoMenu && !target?.closest('.photo-picker')) this.showPhotoMenu = false;
  }

  @HostListener('document:keydown.escape')
  closePhotoMenuOnEscape(): void {
    this.showPhotoMenu = false;
  }

  private uploadProfilePhoto(photo: File, input?: HTMLInputElement): void {
    this.isUploadingPhoto = true;
    this.userService.uploadProfilePhoto(photo).subscribe({
      next: response => {
        this.isUploadingPhoto = false;
        if (input) input.value = '';
        this.toastr.success(response?.message || 'Profile photo updated successfully.');
        this.loadProfilePhoto();
        this.cdr.detectChanges();
      },
      error: error => {
        this.isUploadingPhoto = false;
        this.photoValidationMessage = error?.error?.message || 'The profile photo could not be uploaded.';
        if (input) input.value = '';
        this.cdr.detectChanges();
      }
    });
  }

  private stopCamera(): void {
    this.cameraStream?.getTracks().forEach(track => track.stop());
    this.cameraStream = null;
    if (this.cameraVideo?.nativeElement) this.cameraVideo.nativeElement.srcObject = null;
  }

  private revokeProfilePhoto(): void {
    if (this.profilePhotoUrl) URL.revokeObjectURL(this.profilePhotoUrl);
    this.profilePhotoUrl = '';
  }

  loadProfile(): void {
    this.isLoading = true;
    const currentUser = this.authService.getCurrentUser();

    if (currentUser) {
      this.userService.getProfile().subscribe({
        next: (user) => {
          this.user = user;
          // Pre-populate editable fields
          this.profileForm.patchValue({
            phoneNumber: user.phoneNumber || '',
            notificationPreferences: user.notificationPreferences || 'InApp'
          });
          this.isLoading = false;
          this.cdr.detectChanges();
        },
        error: (error) => {
          console.error('Failed to load profile', error);
          this.toastr.error('Failed to load profile');
          this.isLoading = false;
          this.cdr.detectChanges();
        }
      });
    } else {
      this.isLoading = false;
      this.cdr.detectChanges();
    }
  }

  // UC 2.5 — Toggle edit mode
  toggleEdit(): void {
    this.isEditing = !this.isEditing;
    if (!this.isEditing && this.user) {
      // Reset to current values on cancel
      this.profileForm.patchValue({
        phoneNumber: this.user.phoneNumber || '',
        notificationPreferences: this.user.notificationPreferences || 'InApp'
      });
    }
  }

  // UC 2.5 — Save profile changes
  saveProfile(): void {
    if (this.profileForm.invalid) {
      this.toastr.error('The profile cannot be saved because the phone number must be 10 digits starting with 0, or +27 followed by 9 digits.');
      return;
    }

    this.isSaving = true;
    const payload = {
      phoneNumber: this.profileForm.value.phoneNumber || '',
      notificationPreferences: this.profileForm.value.notificationPreferences || 'InApp'
    };

    this.userService.updateMe(payload).subscribe({
      next: () => {
        if (this.user) {
        this.user = { ...this.user, phoneNumber: payload.phoneNumber, notificationPreferences: payload.notificationPreferences };
      }
      this.isEditing = false;
      this.isSaving = false;
      this.toastr.success('Profile updated successfully');
      this.cdr.detectChanges();
    },
    error: (err) => {
      console.error('Failed to update profile', err);
      this.toastr.error(err.error?.message || 'Failed to update profile');
      this.isSaving = false;
      this.cdr.detectChanges();
    }
  });
  }

  getStatusBadgeClass(): string {
    if (!this.user) return '';
    if (!this.user.isActivated) return 'status-pending';
    if (this.user.isLocked) return 'status-locked';
    if (!this.user.isActive) return 'status-inactive';
    return 'status-active';
  }

  getStatusText(): string {
    if (!this.user) return '';
    if (!this.user.isActivated) return 'Pending Activation';
    if (this.user.isLocked) return 'Locked';
    if (!this.user.isActive) return 'Inactive';
    return 'Active';
  }
}
