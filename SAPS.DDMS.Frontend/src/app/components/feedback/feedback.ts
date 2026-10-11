import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../services/auth';
import { FeedbackItem, FeedbackService } from '../../services/feedback';
import { trimmedTextValidator } from '../../validation/input.validators';
import { getApiErrorMessage } from '../../validation/api-error';
import { DialogService } from '../../shared/dialog.service';

@Component({
  selector: 'app-feedback',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './feedback.html',
  styleUrls: ['./feedback.css']
})
export class Feedback implements OnInit {
  private feedbackService = inject(FeedbackService);
  private authService = inject(AuthService);
  private toastr = inject(ToastrService);
  private fb = inject(FormBuilder);
  private cdr = inject(ChangeDetectorRef);
  private dialog = inject(DialogService);

  items: FeedbackItem[] = [];
  isLoading = false;
  isSubmitting = false;
  respondingFeedbackId: number | null = null;
  isAdmin = this.authService.getCurrentUser()?.roleName === 'System Administrator';

  feedbackForm = this.fb.group({
    subject: ['', [trimmedTextValidator(3, 150)]],
    message: ['', [trimmedTextValidator(10, 2000)]]
  });

  ngOnInit(): void {
    this.loadFeedback();
  }

  loadFeedback(): void {
    this.isLoading = true;
    const request = this.isAdmin ? this.feedbackService.getAll() : this.feedbackService.getMine();
    request.subscribe({
      next: items => {
        this.items = Array.isArray(items) ? items.filter(item => !!item && Number(item.feedbackID) > 0) : [];
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: err => {
        this.items = [];
        this.isLoading = false;
        this.toastr.error(getApiErrorMessage(err, 'Feedback could not be loaded'));
        this.cdr.detectChanges();
      }
    });
  }

  submitFeedback(): void {
    if (this.isSubmitting) return;
    this.trimFeedbackForm();
    if (this.feedbackForm.invalid) {
      this.feedbackForm.markAllAsTouched();
      this.toastr.error('Feedback cannot be submitted because a meaningful subject and message within the allowed lengths are required.');
      return;
    }

    this.isSubmitting = true;
    const subject = this.feedbackForm.value.subject!.trim();
    const message = this.feedbackForm.value.message!.trim();
    this.feedbackService.submit(subject, message).subscribe({
      next: () => {
        this.feedbackForm.reset();
        this.isSubmitting = false;
        this.toastr.success('Feedback submitted');
        this.cdr.detectChanges();
        this.loadFeedback();
      },
      error: err => {
        this.isSubmitting = false;
        this.toastr.error(getApiErrorMessage(err, 'Feedback could not be submitted'));
        this.cdr.detectChanges();
      }
    });
  }

  async respond(item: FeedbackItem): Promise<void> {
    if (!this.isAdmin) {
      this.toastr.error('A response cannot be saved because only a System Administrator may respond to feedback.');
      return;
    }
    if (this.respondingFeedbackId !== null) return;
    const response = await this.dialog.open({
      title: 'Respond to Feedback', message: item.subject,
      inputLabel: 'Official response', inputType: 'textarea', required: true, minLength: 3, maxLength: 2000,
      initialValue: item.response || '', placeholder: 'Write a clear response to the user', confirmText: 'Send Response'
    });
    const normalisedResponse = response?.trim() || '';
    if (normalisedResponse.length < 3 || normalisedResponse.length > 2000) {
      this.toastr.error('The response cannot be saved because it must contain between 3 and 2,000 characters.');
      return;
    }
    this.respondingFeedbackId = item.feedbackID;
    this.feedbackService.respond(item.feedbackID, normalisedResponse).subscribe({
      next: () => {
        this.respondingFeedbackId = null;
        this.toastr.success('Feedback response saved');
        this.loadFeedback();
      },
      error: err => {
        this.respondingFeedbackId = null;
        this.toastr.error(getApiErrorMessage(err, 'The feedback response could not be saved'));
        this.cdr.detectChanges();
      }
    });
  }

  private trimFeedbackForm(): void {
    for (const name of ['subject', 'message']) {
      const control = this.feedbackForm.get(name);
      if (typeof control?.value === 'string') {
        control.setValue(control.value.trim(), { emitEvent: false });
      }
    }
  }
}
