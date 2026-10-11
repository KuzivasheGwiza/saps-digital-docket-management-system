import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { InvestigationNoteService } from '../../services/investigation-note';
import { DocketService } from '../../services/docket';
import { AuthService } from '../../services/auth';
import { InvestigationNote, CreateInvestigationNoteDto, UpdateInvestigationNoteDto } from '../../models/investigation-note.model';
import { Docket } from '../../models/docket.model';
import { hasAnyRole, INVESTIGATION_NOTE_ACTION_ROLES } from '../../security/role-policy';
import { trimmedTextValidator } from '../../validation/input.validators';
import { getApiErrorMessage } from '../../validation/api-error';
import { DialogService } from '../../shared/dialog.service';

const MAX_NOTE_ATTACHMENT_BYTES = 10 * 1024 * 1024;
const ALLOWED_NOTE_ATTACHMENT_TYPES = new Set(['application/pdf', 'image/jpeg', 'image/png']);
import { formatDocketDisplayName } from '../../shared/docket-file-name';

@Component({
  selector: 'app-investigation-notes',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterModule],
  templateUrl: './investigation-notes.html',
  styleUrls: ['./investigation-notes.css']
})
export class InvestigationNotes implements OnInit {
  private noteService = inject(InvestigationNoteService);
  private docketService = inject(DocketService);
  private authService = inject(AuthService);
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private toastr = inject(ToastrService);
  private cdr = inject(ChangeDetectorRef);
  private dialog = inject(DialogService);
  
  docketId: number = 0;
  investigationId: number = 0;
  docket: Docket | null = null;
  notes: InvestigationNote[] = [];
  selectedNote: InvestigationNote | null = null;
  isLoading = false;
  showCreateModal = false;
  showEditModal = false;
  showVersionModal = false;
  selectedFile: File | null = null;
  fileValidationMessage = '';
  noteVersions: any[] = [];

  showArchiveRequestModal = false;
  archiveRequestNote: any = null;
  archiveRequestReason = '';
  isSubmittingArchive = false;
  archiveRequests: any[] = [];
  reviewNotes = '';
  
  createForm: FormGroup;
  editForm: FormGroup;
  
  currentUser = this.authService.getCurrentUser();
  canCreateNote = hasAnyRole(this.currentUser?.roleName, INVESTIGATION_NOTE_ACTION_ROLES.create);
  canViewVersions = hasAnyRole(this.currentUser?.roleName, INVESTIGATION_NOTE_ACTION_ROLES.viewVersions);
  canRequestArchive = hasAnyRole(this.currentUser?.roleName, INVESTIGATION_NOTE_ACTION_ROLES.requestArchive);
  canReviewArchive = hasAnyRole(this.currentUser?.roleName, INVESTIGATION_NOTE_ACTION_ROLES.reviewArchive);
  
  constructor() {
    this.createForm = this.fb.group({
      description: ['', [trimmedTextValidator(10, 256)]]
    });
    
    this.editForm = this.fb.group({
      description: ['', [trimmedTextValidator(10, 256)]],
      reason: ['', [trimmedTextValidator(10, 256)]]
    });
  }
  
  ngOnInit(): void {
    this.route.params.subscribe(params => {
      const docketId = Number(params['id']);
      if (Number.isSafeInteger(docketId) && docketId > 0) {
        this.docketId = docketId;
        this.loadDocket();
      } else {
        this.toastr.error('Investigation notes cannot be loaded because the docket address contains an invalid identifier.');
      }
    });
    if (this.canReviewArchive) {
      this.loadArchiveRequests();
    }
  }
  
  loadDocket(): void {
    this.docketService.getDocketById(this.docketId).subscribe({
      next: (docket) => {
        if (!docket || !Number(docket.docketID)) {
          this.toastr.error('Investigation notes cannot be loaded because the docket response is empty or invalid.');
          this.cdr.detectChanges();
          return;
        }
        this.docket = docket;
        if (Number.isSafeInteger(Number(docket.investigationID)) && Number(docket.investigationID) > 0) {
          this.investigationId = Number(docket.investigationID);
          this.loadNotes();
        } else {
          this.toastr.error('This docket is not linked to an investigation');
        }
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Failed to load docket', error);
        this.toastr.error(getApiErrorMessage(error, 'Docket information could not be loaded'));
        this.cdr.detectChanges();
      }
    });
  }

  getDocketDisplayName(): string {
    return formatDocketDisplayName(this.docket);
  }
  
  loadNotes(): void {
    this.isLoading = true;
    this.noteService.getInvestigationNotes(this.investigationId).subscribe({
      next: (notes) => {
        this.notes = Array.isArray(notes) ? notes.filter(note => !!note && Number(note.noteID) > 0) : [];
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Failed to load notes', error);
        this.toastr.error(getApiErrorMessage(error, 'Investigation notes could not be loaded'));
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  downloadAttachment(attachment: { attachmentID: number; fileName: string }): void {
    if (!Number.isSafeInteger(Number(attachment?.attachmentID)) || Number(attachment.attachmentID) <= 0) {
      this.toastr.error('The attachment cannot be downloaded because its identifier is invalid.');
      return;
    }
    this.noteService.downloadAttachment(attachment.attachmentID).subscribe({
      next: blob => {
        if (!(blob instanceof Blob) || blob.size === 0) {
          this.toastr.error('The attachment cannot be downloaded because the server returned an empty file.');
          return;
        }
        const objectUrl = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = objectUrl;
        const suppliedName = typeof attachment.fileName === 'string' ? attachment.fileName : '';
        anchor.download = suppliedName.replace(/[^a-zA-Z0-9._-]/g, '_') || `attachment-${attachment.attachmentID}`;
        anchor.style.display = 'none';
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
        URL.revokeObjectURL(objectUrl);
        this.cdr.detectChanges();
      },
      error: err => {
        this.toastr.error(getApiErrorMessage(err, 'The attachment could not be downloaded'));
        this.cdr.detectChanges();
      }
    });
  }

  canEditNoteRecord(note: InvestigationNote): boolean {
    if (this.isNoteReadOnly(note)) return false;
    if (hasAnyRole(this.currentUser?.roleName, INVESTIGATION_NOTE_ACTION_ROLES.editAny)) return true;
    return hasAnyRole(this.currentUser?.roleName, INVESTIGATION_NOTE_ACTION_ROLES.editOwn) &&
      !!note.authorUserID && note.authorUserID === this.currentUser?.userID;
  }

  canRequestArchiveNote(note: InvestigationNote): boolean {
    return this.canRequestArchive && !this.isNoteReadOnly(note);
  }

  private isNoteReadOnly(note: InvestigationNote): boolean {
    return ['ArchivePending', 'Archived'].includes(note.status || '') ||
      ['CLOSED', 'ARCHIVED'].includes(this.docket?.status || '');
  }
  
  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = null;
    this.fileValidationMessage = '';
    const file = input.files?.[0];
    if (!file) return;
    if (!ALLOWED_NOTE_ATTACHMENT_TYPES.has(file.type.toLowerCase())) {
      this.fileValidationMessage = 'Select a PDF, JPG or PNG attachment. Other file types are blocked for safety.';
      input.value = '';
      return;
    }
    if (file.size > MAX_NOTE_ATTACHMENT_BYTES) {
      this.fileValidationMessage = 'The attachment cannot exceed 10 MB.';
      input.value = '';
      return;
    }
    this.selectedFile = file;
  }
  
  openCreateModal(): void {
    if (!this.canCreateNote) {
      this.toastr.error('A note cannot be added because your role is not authorised for investigation notes.');
      return;
    }
    this.createForm.reset();
    this.selectedFile = null;
    this.fileValidationMessage = '';
    this.showCreateModal = true;
  }
  
  closeCreateModal(): void {
    this.showCreateModal = false;
    this.selectedFile = null;
    this.fileValidationMessage = '';
  }
  
  createNote(): void {
    if (!this.canCreateNote) {
      this.toastr.error('A note cannot be added because your role is not authorised for investigation notes.');
      return;
    }
    if (this.isLoading) return;
    this.trimControl(this.createForm, 'description');
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      this.toastr.error('The note cannot be saved because it must contain between 10 and 256 meaningful characters.');
      return;
    }
    if (!Number.isSafeInteger(this.investigationId) || this.investigationId <= 0) {
      this.toastr.error('The note cannot be saved because this docket is not linked to a valid investigation.');
      return;
    }
    
    this.isLoading = true;
    const formData = new FormData();
    formData.append('investigationID', this.investigationId.toString());
    formData.append('description', this.createForm.value.description);
    if (this.selectedFile) {
      formData.append('attachment', this.selectedFile);
    }
    
    this.noteService.createNote(formData).subscribe({
      next: () => {
        this.toastr.success('Investigation note saved successfully');
        this.closeCreateModal();
        this.isLoading = false;
        this.loadNotes();
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Failed to create note', error);
        this.toastr.error(getApiErrorMessage(error, 'The investigation note could not be saved'));
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }
  
  openEditModal(note: InvestigationNote): void {
    if (!this.canEditNoteRecord(note)) {
      this.toastr.error('This note is read-only or you do not have permission to edit it');
      return;
    }
    this.selectedNote = note;
    this.editForm.patchValue({
      description: note.description,
      reason: ''
    });
    this.showEditModal = true;
  }
  
  closeEditModal(): void {
    this.showEditModal = false;
    this.selectedNote = null;
  }
  
  updateNote(): void {
    if (this.isLoading) return;
    this.trimControl(this.editForm, 'description');
    this.trimControl(this.editForm, 'reason');
    if (this.editForm.invalid || !this.selectedNote || !this.canEditNoteRecord(this.selectedNote)) {
      this.editForm.markAllAsTouched();
      this.toastr.error('The note cannot be updated because valid content, a reason and permission to edit this note are required.');
      return;
    }
    
    this.isLoading = true;
    const updateData: UpdateInvestigationNoteDto = {
      description: this.editForm.value.description,
      reason: this.editForm.value.reason
    };
    
    this.noteService.updateNote(this.selectedNote.noteID, updateData).subscribe({
      next: () => {
        this.toastr.success('Note updated successfully. Version history preserved.');
        this.closeEditModal();
        this.isLoading = false;
        this.loadNotes();
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Failed to update note', error);
        this.toastr.error(getApiErrorMessage(error, 'The investigation note could not be updated'));
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }
  
  viewVersions(note: InvestigationNote): void {
    if (!this.canViewVersions) {
      this.toastr.error('You do not have permission to view note history');
      return;
    }
    this.selectedNote = note;
    this.noteService.getNoteVersions(note.noteID).subscribe({
      next: (versions) => {
        this.noteVersions = Array.isArray(versions) ? versions : [];
        this.showVersionModal = true;
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Failed to load versions', error);
        this.toastr.error('Failed to load version history');
        this.cdr.detectChanges();
      }
    });
  }

  openArchiveRequestModal(note: InvestigationNote): void {
    if (!this.canRequestArchiveNote(note)) {
      this.toastr.error('This note cannot be submitted for archive review');
      return;
    }
    this.archiveRequestNote = note;
    this.archiveRequestReason = '';
    this.showArchiveRequestModal = true;
  }

  closeArchiveRequestModal(): void {
    this.showArchiveRequestModal = false;
    this.archiveRequestNote = null;
    this.archiveRequestReason = '';
  }

  submitArchiveRequest(): void {
    if (!this.archiveRequestNote || !this.canRequestArchiveNote(this.archiveRequestNote)) {
      this.toastr.error('This note cannot be submitted for archive review');
      return;
    }
    if (!this.archiveRequestReason.trim() || this.archiveRequestReason.trim().length < 10) {
      this.toastr.error('Please provide a reason of at least 10 characters');
      return;
    }
    this.isSubmittingArchive = true;

    this.noteService.requestArchiveNote(this.archiveRequestNote.noteID, this.archiveRequestReason.trim())
      .subscribe({
        next: () => {
          this.toastr.success('Archive request submitted. Awaiting DSC Supervisor approval.');
          this.closeArchiveRequestModal();
          this.loadNotes();
          this.isSubmittingArchive = false;
          this.cdr.detectChanges();
        },
        error: (err) => {
          this.toastr.error(err.error?.message || 'Failed to submit archive request');
          this.isSubmittingArchive = false;
          this.cdr.detectChanges();
        }
      });
  }

  loadArchiveRequests(): void {
    this.noteService.getArchiveRequests().subscribe({
      next: requests => {
        this.archiveRequests = requests || [];
        this.cdr.detectChanges();
      },
      error: () => {
        this.archiveRequests = [];
        this.cdr.detectChanges();
      }
    });
  }

  async reviewArchiveRequest(request: any, decision: 'Approved' | 'Rejected'): Promise<void> {
    const rejectionReason = decision === 'Rejected'
      ? (await this.dialog.open({
          title: 'Reject Note Archive Request',
          message: 'Explain why this investigation note must remain active. The reason will be recorded in the review history.',
          inputLabel: 'Rejection reason', inputType: 'textarea', required: true, minLength: 3, maxLength: 500,
          initialValue: 'Archive request rejected', confirmText: 'Reject Request', tone: 'danger'
        }) || '')
      : ((await this.dialog.confirm({
          title: 'Approve Note Archive Request',
          message: 'The investigation note will be archived and retained in its version history.',
          confirmText: 'Approve & Archive'
        })) ? '' : '__cancelled__');
    if (rejectionReason === '__cancelled__') return;
    if (decision === 'Rejected' && rejectionReason.trim().length < 3) {
      this.toastr.error('A rejection reason is required');
      return;
    }

    this.noteService.reviewArchiveRequest(request.archiveRequestID, decision, rejectionReason)
      .subscribe({
        next: () => {
          this.toastr.success(decision === 'Approved' ? 'Note archived' : 'Archive request rejected');
          this.loadArchiveRequests();
          this.loadNotes();
          this.cdr.detectChanges();
        },
        error: err => {
          this.toastr.error(err.error?.message || 'Failed to review archive request');
          this.cdr.detectChanges();
        }
      });
  }

  
  closeVersionModal(): void {
    this.showVersionModal = false;
    this.noteVersions = [];
    this.selectedNote = null;
  }
  
  formatDate(date: Date): string {
    const parsed = new Date(date);
    return Number.isNaN(parsed.getTime()) ? 'Unavailable' : parsed.toLocaleString();
  }
  
  getFileIcon(fileName: string): string {
    const ext = typeof fileName === 'string' ? fileName.split('.').pop()?.toLowerCase() : '';
    switch(ext) {
      case 'pdf': return 'fas fa-file-pdf';
      case 'jpg':
      case 'jpeg':
      case 'png':
      case 'gif': return 'fas fa-file-image';
      case 'doc':
      case 'docx': return 'fas fa-file-word';
      default: return 'fas fa-file';
    }
  }

  private trimControl(form: FormGroup, controlName: string): void {
    const control = form.get(controlName);
    if (typeof control?.value === 'string') {
      control.setValue(control.value.trim(), { emitEvent: false });
    }
  }
}
