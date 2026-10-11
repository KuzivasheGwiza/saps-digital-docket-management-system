// src/app/components/faq-management/faq-management.ts
// UC 1.10 Create FAQ | UC 1.11 Update FAQ | UC 1.12 Delete FAQ
import { Component, OnInit, inject, ChangeDetectorRef, ChangeDetectionStrategy, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';
import { FaqService, Faq } from '../../services/faq';
import { AuthService } from '../../services/auth';
import { trimmedTextValidator } from '../../validation/input.validators';
import { getApiErrorMessage } from '../../validation/api-error';

@Component({
  selector: 'app-faq-management',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterModule],
  templateUrl: './faq-management.html',
  styleUrls: ['./faq-management.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class FaqManagement implements OnInit, OnDestroy {
  private faqService = inject(FaqService);
  private authService = inject(AuthService);
  private fb = inject(FormBuilder);
  private toastr = inject(ToastrService);
  private cdr = inject(ChangeDetectorRef);
  private destroy$ = new Subject<void>();

  faqs: Faq[] = [];
  filteredFaqs: Faq[] = [];
  searchTerm = '';
  isLoading = false;
  showViewModal = false;
  showCreateModal = false;
  showEditModal = false;
  showDeleteModal = false;
  selectedFaq: Faq | null = null;

  createForm: FormGroup;
  editForm: FormGroup;

  currentUser = this.authService.getCurrentUser();
  isSystemAdmin = this.currentUser?.roleName === 'System Administrator';

  constructor() {
    this.createForm = this.fb.group({
      question: ['', [trimmedTextValidator(5, 250)]],
      answer:   ['', [trimmedTextValidator(10, 2000)]],
      isPublished: [false]
    });
    this.editForm = this.fb.group({
      question: ['', [trimmedTextValidator(5, 250)]],
      answer:   ['', [trimmedTextValidator(10, 2000)]],
      isPublished: [false]
    });
  }

  ngOnInit(): void {
    this.loadFaqs();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadFaqs(): void {
    this.isLoading = true;
    this.cdr.detectChanges();
    this.faqService.getFaqs().pipe(takeUntil(this.destroy$)).subscribe({
      next: (faqs) => {
        this.faqs = Array.isArray(faqs)
          ? faqs.filter(faq => !!faq && Number(faq.faqID) > 0).map(faq => ({
              ...faq,
              question: typeof faq.question === 'string' ? faq.question : '',
              answer: typeof faq.answer === 'string' ? faq.answer : ''
            }))
          : [];
        this.filterFaqs();
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.toastr.error(err.error?.message || 'Failed to load FAQs');
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  filterFaqs(): void {
    const term = this.searchTerm.toLowerCase().trim();
    this.filteredFaqs = term
      ? this.faqs.filter(f => f.question.toLowerCase().includes(term) || f.answer.toLowerCase().includes(term))
      : [...this.faqs];
    this.cdr.markForCheck();
  }

  onSearch(): void { this.filterFaqs(); }

  openViewModal(faq: Faq): void {
    this.selectedFaq = faq;
    this.showViewModal = true;
    this.cdr.markForCheck();
  }

  closeViewModal(): void {
    this.showViewModal = false;
    this.selectedFaq = null;
    this.cdr.markForCheck();
  }

  // UC 1.10 — Create FAQ
  openCreateModal(): void {
    this.createForm.reset();
    this.showCreateModal = true;
    this.cdr.markForCheck();
  }

  closeCreateModal(): void {
    this.showCreateModal = false;
    this.cdr.markForCheck();
  }

  createFaq(): void {
    if (!this.isSystemAdmin) {
      this.toastr.error('The FAQ cannot be created because only a System Administrator may manage help content.');
      return;
    }
    if (this.isLoading) return;
    this.trimFaqForm(this.createForm);
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      this.toastr.error('The FAQ cannot be created because the question and answer must contain meaningful text within the allowed lengths.');
      return;
    }
    this.isLoading = true;
    this.cdr.markForCheck();
    this.faqService.createFaq({
      question: this.createForm.value.question.trim(),
      answer:   this.createForm.value.answer.trim(),
      isPublished: !!this.createForm.value.isPublished
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toastr.success('FAQ entry added successfully');
        this.closeCreateModal();
        this.loadFaqs();
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.toastr.error(getApiErrorMessage(err, 'The FAQ could not be created'));
        this.isLoading = false;
        this.cdr.markForCheck();
      }
    });
  }

  // UC 1.11 — Update FAQ
  openEditModal(faq: Faq): void {
    this.selectedFaq = faq;
    this.editForm.patchValue({ question: faq.question, answer: faq.answer, isPublished: faq.isPublished });
    this.showEditModal = true;
    this.cdr.markForCheck();
  }

  closeEditModal(): void {
    this.showEditModal = false;
    this.selectedFaq = null;
    this.cdr.markForCheck();
  }

  updateFaq(): void {
    if (!this.isSystemAdmin) {
      this.toastr.error('The FAQ cannot be updated because only a System Administrator may manage help content.');
      return;
    }
    if (this.isLoading) return;
    this.trimFaqForm(this.editForm);
    if (this.editForm.invalid || !this.selectedFaq) {
      this.editForm.markAllAsTouched();
      this.toastr.error('The FAQ cannot be updated because the question and answer must contain meaningful text within the allowed lengths.');
      return;
    }
    this.isLoading = true;
    this.cdr.markForCheck();
    this.faqService.updateFaq(this.selectedFaq.faqID, {
      question: this.editForm.value.question.trim(),
      answer:   this.editForm.value.answer.trim(),
      isPublished: !!this.editForm.value.isPublished
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toastr.success('FAQ entry updated successfully');
        this.closeEditModal();
        this.loadFaqs();
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.toastr.error(getApiErrorMessage(err, 'The FAQ could not be updated'));
        this.isLoading = false;
        this.cdr.markForCheck();
      }
    });
  }

  // UC 1.12 — Delete FAQ (soft delete)
  openDeleteModal(faq: Faq): void {
    this.selectedFaq = faq;
    this.showDeleteModal = true;
    this.cdr.markForCheck();
  }

  closeDeleteModal(): void {
    this.showDeleteModal = false;
    this.selectedFaq = null;
    this.cdr.markForCheck();
  }

  confirmDeleteFaq(): void {
    if (!this.isSystemAdmin) {
      this.toastr.error('The FAQ cannot be deleted because only a System Administrator may manage help content.');
      return;
    }
    if (this.isLoading) return;
    if (!this.selectedFaq) return;
    this.isLoading = true;
    this.cdr.markForCheck();
    this.faqService.deleteFaq(this.selectedFaq.faqID)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: () => {
          this.toastr.success('FAQ entry deleted successfully');
          this.closeDeleteModal();
          this.loadFaqs();
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.toastr.error(getApiErrorMessage(err, 'The FAQ could not be deleted'));
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }

  private trimFaqForm(form: FormGroup): void {
    for (const name of ['question', 'answer']) {
      const control = form.get(name);
      if (typeof control?.value === 'string') {
        control.setValue(control.value.trim(), { emitEvent: false });
      }
    }
  }
}
