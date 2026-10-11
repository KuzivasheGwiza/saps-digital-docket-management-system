// src/app/components/evidence-management/evidence-management.ts
import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { EvidenceService } from '../../services/evidence';
import { DocketService } from '../../services/docket';
import { AuthService } from '../../services/auth';
import {
  DEFAULT_EVIDENCE_COLLECTION_METHODS,
  DEFAULT_EVIDENCE_ITEM_STATES,
  DEFAULT_RUNTIME_POLICIES,
  ReferenceDataService
} from '../../services/reference-data';
import { EVIDENCE_ACTION_ROLES, hasAnyRole } from '../../security/role-policy';
import { Evidence, CreateEvidenceDto, UpdateEvidenceDto } from '../../models/evidence.model';
import { Docket } from '../../models/docket.model';
import {
  notFutureDateValidator,
  optionalTrimmedMaxLengthValidator,
  positiveIntegerValidator,
  trimmedTextValidator
} from '../../validation/input.validators';
import { getApiErrorMessage } from '../../validation/api-error';
import { formatDocketDisplayName, getDocketOwnerName } from '../../shared/docket-file-name';

const MAX_EVIDENCE_FILE_BYTES = 50 * 1024 * 1024;
const ALLOWED_EVIDENCE_FILE_TYPES = new Set([
  'image/jpeg', 'image/png', 'image/gif', 'application/pdf',
  'video/mp4', 'video/webm', 'video/quicktime', 'video/x-msvideo'
]);

@Component({
  selector: 'app-evidence-management',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterModule],
  templateUrl: './evidence-management.html',
  styleUrls: ['./evidence-management.css']
})
export class EvidenceManagement implements OnInit, OnDestroy {
  private evidenceService = inject(EvidenceService);
  private docketService = inject(DocketService);
  private authService = inject(AuthService);
  private referenceData = inject(ReferenceDataService);
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private toastr = inject(ToastrService);
  private cdr = inject(ChangeDetectorRef);
  
  evidenceList: Evidence[] = [];
  dockets: Docket[] = [];
  evidenceTypes: any[] = [];
  evidenceLocations: any[] = [];
  selectedDocketId: number = 0;
  selectedEvidence: Evidence | null = null;
  evidenceCollectionMethods = [...DEFAULT_EVIDENCE_COLLECTION_METHODS].sort((a, b) => a.localeCompare(b));
  evidenceItemStates = [...DEFAULT_EVIDENCE_ITEM_STATES].sort((a, b) => a.localeCompare(b));
  isLoading = false;
  showCreateModal = false;
  showEditModal = false;
  showDetailsModal = false;
  showTransferModal = false;
  selectedEvidenceFile: File | null = null;
  imagePreviewUrl: string | null = null;
  fileValidationMessage = '';
  custodyHistory: any[] = [];
  evidenceRetentionYears = DEFAULT_RUNTIME_POLICIES.evidenceRetentionYears;
  private evidenceImageUrls: string[] = [];
  
  createForm: FormGroup;
  editForm: FormGroup;
  transferForm: FormGroup;
  
  currentUser = this.authService.getCurrentUser();
  canCreateEvidence = hasAnyRole(this.currentUser?.roleName, EVIDENCE_ACTION_ROLES.create);
  canEditEvidence = hasAnyRole(this.currentUser?.roleName, EVIDENCE_ACTION_ROLES.edit);
  canTransferEvidence = hasAnyRole(this.currentUser?.roleName, EVIDENCE_ACTION_ROLES.transfer);
  canViewCustodyHistory = hasAnyRole(this.currentUser?.roleName, EVIDENCE_ACTION_ROLES.viewCustodyHistory);
  currentUserDisplayName = this.currentUser?.fullName || [this.currentUser?.firstName, this.currentUser?.lastName].filter(Boolean).join(' ') || 'Signed-in user';
  get maxCollectedDateTime(): string {
    return this.toLocalDateTimeInputValue(new Date());
  }
  
  constructor() {
    this.createForm = this.fb.group({
      description: ['', [trimmedTextValidator(3, 2000)]],
      evidenceTypeID: [null, [Validators.required, positiveIntegerValidator]],
      quantity: [1, [Validators.required, positiveIntegerValidator, Validators.max(1_000_000)]],
      dateCollected: ['', [Validators.required, notFutureDateValidator]],
      collectionMethod: ['', [trimmedTextValidator(2, 256)]],
      stateOfItem: ['', [trimmedTextValidator(2, 256)]],
      evidenceLocationID: [null, [Validators.required, positiveIntegerValidator]],
      signatureMethod: ['Ink', [trimmedTextValidator(2, 50)]],
      docketID: [null, [Validators.required, positiveIntegerValidator]]
    });
    
    this.editForm = this.fb.group({
      description: ['', [trimmedTextValidator(3, 2000)]],
      quantity: [1, [Validators.required, positiveIntegerValidator, Validators.max(1_000_000)]],
      collectionMethod: ['', [trimmedTextValidator(2, 256)]],
      stateOfItem: ['', [trimmedTextValidator(2, 256)]],
      evidenceLocationID: [null, [Validators.required, positiveIntegerValidator]],
      signatureMethod: ['Ink', [trimmedTextValidator(2, 50)]],
      payloadHash: ['', [optionalTrimmedMaxLengthValidator(256)]],
      reasonForUpdate: ['', [trimmedTextValidator(10, 500)]]
    });

    this.transferForm = this.fb.group({
      fromLocationID: [null, [Validators.required, positiveIntegerValidator]],
      toLocationID: [null, [Validators.required, positiveIntegerValidator]],
      receivingOfficerUserID: [null, [Validators.required, positiveIntegerValidator]],
      signatureMethod: ['Ink', [trimmedTextValidator(2, 50)]],
      payloadHash: ['', [optionalTrimmedMaxLengthValidator(256)]],
      transferReason: ['', [trimmedTextValidator(10, 500)]]
    });
  }
  
  ngOnInit(): void {
    this.loadOperationalLookups();
    this.loadEvidenceTypes();
    this.loadEvidenceLocations();
    this.loadDockets();
    
    this.route.queryParams.subscribe(params => {
      if (params['docketId']) {
        const docketId = Number(params['docketId']);
        if (Number.isSafeInteger(docketId) && docketId > 0) {
          this.selectedDocketId = docketId;
          this.loadEvidence();
        } else {
          this.toastr.error('Evidence cannot be loaded because the docket address contains an invalid identifier.');
        }
      }
    });
  }

  ngOnDestroy(): void {
    this.revokeEvidenceImageUrls();
  }

  loadOperationalLookups(): void {
    this.referenceData.getOperationalLookups().subscribe({
      next: lookups => {
        this.evidenceRetentionYears = this.normaliseRetentionYears(lookups.runtimePolicies?.evidenceRetentionYears);
        this.evidenceCollectionMethods = this.normaliseStrings(
          lookups.evidenceCollectionMethods,
          DEFAULT_EVIDENCE_COLLECTION_METHODS
        );
        this.evidenceItemStates = this.normaliseStrings(
          lookups.evidenceItemStates,
          DEFAULT_EVIDENCE_ITEM_STATES
        );
        this.cdr.detectChanges();
      },
      error: () => {
        this.evidenceRetentionYears = DEFAULT_RUNTIME_POLICIES.evidenceRetentionYears;
        this.evidenceCollectionMethods = [...DEFAULT_EVIDENCE_COLLECTION_METHODS].sort((a, b) => a.localeCompare(b));
        this.evidenceItemStates = [...DEFAULT_EVIDENCE_ITEM_STATES].sort((a, b) => a.localeCompare(b));
        this.cdr.detectChanges();
      }
    });
  }
  
  loadEvidenceTypes(): void {
    this.evidenceService.getEvidenceTypes().subscribe({
      next: (types) => {
        this.evidenceTypes = (Array.isArray(types) ? types : [])
          .sort((left, right) => String(left?.name || '').localeCompare(String(right?.name || '')));
        this.cdr.detectChanges();
      },
      error: (error) => console.error('Failed to load evidence types', error)
    });
  }
  
  loadEvidenceLocations(): void {
    this.evidenceService.getEvidenceLocations().subscribe({
      next: (locations) => {
        this.evidenceLocations = (Array.isArray(locations) ? locations : [])
          .sort((left, right) => String(left?.locationName || '').localeCompare(String(right?.locationName || '')));
        this.cdr.detectChanges();
      },
      error: (error) => console.error('Failed to load evidence locations', error)
    });
  }
  
  loadDockets(): void {
    this.docketService.getDockets().subscribe({
      next: (dockets) => {
        this.dockets = Array.isArray(dockets) ? dockets : [];
        this.cdr.detectChanges();
      },
      error: (error) => console.error('Failed to load dockets', error)
    });
  }
  
  loadEvidence(): void {
    if (!this.selectedDocketId) return;
    
    this.isLoading = true;
    this.evidenceService.getEvidenceByDocket(this.selectedDocketId).subscribe({
      next: (evidence) => {
        this.revokeEvidenceImageUrls();
        this.evidenceList = Array.isArray(evidence) ? evidence.filter(item => !!item && Number(item.evidenceID) > 0) : [];
        this.loadProtectedEvidenceImages();
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Failed to load evidence', error);
        this.toastr.error('Failed to load evidence');
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  private loadProtectedEvidenceImages(): void {
    for (const item of this.evidenceList) {
      if (!item.imageUrl) continue;
      item.imageUrl = undefined;
      this.evidenceService.getEvidenceImage(item.evidenceID).subscribe({
        next: blob => {
          if (!(blob instanceof Blob) || blob.size === 0) return;
          const objectUrl = URL.createObjectURL(blob);
          this.evidenceImageUrls.push(objectUrl);
          item.imageUrl = objectUrl;
          item.fileContentType = blob.type;
          this.cdr.detectChanges();
        },
        error: () => {
          item.imageUrl = undefined;
          this.cdr.detectChanges();
        }
      });
    }
  }

  private revokeEvidenceImageUrls(): void {
    this.evidenceImageUrls.forEach(url => URL.revokeObjectURL(url));
    this.evidenceImageUrls = [];
  }
  
  onDocketChange(): void {
    this.loadEvidence();
  }

  private normaliseStrings(values: string[] | null | undefined, fallback: string[]): string[] {
    const normalised = Array.from(new Set(
      (Array.isArray(values) ? values : [])
        .map(value => value?.trim())
        .filter((value): value is string => !!value)
    ));

    return (normalised.length ? normalised : [...fallback]).sort((left, right) => left.localeCompare(right));
  }
  
  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedEvidenceFile = null;
    this.imagePreviewUrl = null;
    this.fileValidationMessage = '';
    const file = input.files?.[0];
    if (!file) return;

    if (!ALLOWED_EVIDENCE_FILE_TYPES.has(file.type.toLowerCase())) {
      this.fileValidationMessage = 'Select a JPEG, PNG, GIF, PDF, MP4, WebM, MOV or AVI evidence file. Other file types are blocked for safety.';
      input.value = '';
      this.cdr.detectChanges();
      return;
    }
    if (file.size > MAX_EVIDENCE_FILE_BYTES) {
      this.fileValidationMessage = 'The evidence file cannot exceed 50 MB.';
      input.value = '';
      this.cdr.detectChanges();
      return;
    }

    this.selectedEvidenceFile = file;
    if (file.type === 'application/pdf' || file.type.startsWith('video/')) {
      this.cdr.detectChanges();
      return;
    }
    const reader = new FileReader();
    reader.onload = () => {
      this.imagePreviewUrl = typeof reader.result === 'string' ? reader.result : null;
      this.cdr.detectChanges();
    };
    reader.onerror = () => {
      this.selectedEvidenceFile = null;
      this.imagePreviewUrl = null;
      this.fileValidationMessage = 'The selected evidence file could not be read. Choose another file.';
      input.value = '';
      this.cdr.detectChanges();
    };
    reader.readAsDataURL(file);
  }
  
  openCreateModal(): void {
    if (!this.canCreateEvidence) {
      this.toastr.error('Evidence cannot be registered because your role is not authorised for evidence capture. An Investigating Officer must perform this action.');
      return;
    }
    this.createForm.reset();
    this.createForm.patchValue({ quantity: 1, docketID: this.selectedDocketId || null, collectionMethod: '', stateOfItem: '', signatureMethod: 'Ink' });
    this.selectedEvidenceFile = null;
    this.imagePreviewUrl = null;
    this.fileValidationMessage = '';
    this.showCreateModal = true;
  }
  
  closeCreateModal(): void {
    this.showCreateModal = false;
    this.selectedEvidenceFile = null;
    this.imagePreviewUrl = null;
    this.fileValidationMessage = '';
  }
  
  createEvidence(): void {
    if (!this.canCreateEvidence) {
      this.toastr.error('Evidence cannot be registered because your role is not authorised for evidence capture. An Investigating Officer must perform this action.');
      return;
    }
    if (this.isLoading) return;
    this.trimFormControls(this.createForm, ['description', 'collectionMethod', 'stateOfItem', 'signatureMethod']);
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      this.toastr.error('Evidence cannot be registered because its description, type, quantity, date, collection method, item state, storage location and docket are required.');
      return;
    }
    
    this.isLoading = true;
    const value = this.createForm.getRawValue();
    const formData = new FormData();
    formData.append('description', value.description);
    formData.append('evidenceTypeID', String(Number(value.evidenceTypeID)));
    formData.append('quantity', String(Number(value.quantity)));
    formData.append('dateCollected', value.dateCollected);
    formData.append('collectionMethod', value.collectionMethod);
    formData.append('stateOfItem', value.stateOfItem);
    formData.append('evidenceLocationID', String(Number(value.evidenceLocationID)));
    formData.append('signatureMethod', value.signatureMethod);
    formData.append('docketID', String(Number(value.docketID)));
    if (this.selectedEvidenceFile) {
      formData.append('evidenceFile', this.selectedEvidenceFile);
    }
    
    this.evidenceService.createEvidence(formData).subscribe({
      next: () => {
        this.toastr.success('Evidence has been registered successfully');
        this.closeCreateModal();
        this.isLoading = false;
        this.loadEvidence();
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Failed to create evidence', error);
        this.toastr.error(getApiErrorMessage(error, 'Evidence could not be registered'));
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  isPdfEvidence(evidence: Evidence): boolean {
    return evidence.fileContentType === 'application/pdf';
  }

  isVideoEvidence(evidence: Evidence): boolean {
    return evidence.fileContentType?.startsWith('video/') === true;
  }
  
  openEditModal(evidence: Evidence): void {
    if (!this.canEditEvidence) {
      this.toastr.error('You do not have permission to edit evidence');
      return;
    }
    this.selectedEvidence = evidence;
    this.editForm.patchValue({
      description: evidence.description,
      quantity: evidence.quantity,
      collectionMethod: evidence.collectionMethod,
      stateOfItem: evidence.stateOfItem,
      evidenceLocationID: evidence.evidenceLocationID,
      signatureMethod: evidence.signatureMethod || 'Ink',
      payloadHash: evidence.payloadHash || '',
      reasonForUpdate: ''
    });
    this.showEditModal = true;
  }
  
  closeEditModal(): void {
    this.showEditModal = false;
    this.selectedEvidence = null;
  }
  
  updateEvidence(): void {
    if (!this.canEditEvidence) {
      this.toastr.error('You do not have permission to edit evidence');
      return;
    }
    if (this.isLoading) return;
    this.trimFormControls(this.editForm, ['description', 'collectionMethod', 'stateOfItem', 'signatureMethod', 'payloadHash', 'reasonForUpdate']);
    if (this.editForm.invalid || !this.selectedEvidence) {
      this.editForm.markAllAsTouched();
      this.toastr.error('Evidence cannot be updated because valid details and a reason of at least 10 characters are required.');
      return;
    }
    
    this.isLoading = true;
    const formValue = this.editForm.getRawValue();
    const updateData: UpdateEvidenceDto = {
      description: formValue.description,
      quantity: formValue.quantity,
      collectionMethod: formValue.collectionMethod,
      stateOfItem: formValue.stateOfItem,
      evidenceLocationID: formValue.evidenceLocationID,
      signatureMethod: formValue.signatureMethod,
      payloadHash: formValue.payloadHash,
      reasonForUpdate: formValue.reasonForUpdate
    };
    
    this.evidenceService.updateEvidence(this.selectedEvidence.evidenceID, updateData).subscribe({
      next: () => {
        this.toastr.success('Evidence updated successfully');
        this.closeEditModal();
        this.isLoading = false;
        this.loadEvidence();
        this.cdr.detectChanges();
      },
      error: (error) => {
        console.error('Failed to update evidence', error);
        this.toastr.error(getApiErrorMessage(error, 'Evidence could not be updated'));
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }
  
  viewEvidenceDetails(evidence: Evidence): void {
    this.selectedEvidence = evidence;
    this.showDetailsModal = true;
  }
  
  closeDetailsModal(): void {
    this.showDetailsModal = false;
    this.selectedEvidence = null;
    this.custodyHistory = [];
  }

  openTransferModal(evidence: Evidence): void {
    if (!this.canTransferEvidence) {
      this.toastr.error('You do not have permission to transfer evidence');
      return;
    }
    this.selectedEvidence = evidence;
    const currentLocation = evidence.evidenceLocationID || '';
    this.transferForm.reset({
      fromLocationID: currentLocation,
      toLocationID: '',
      receivingOfficerUserID: this.currentUser?.userID || '',
      signatureMethod: 'Ink',
      payloadHash: evidence.payloadHash || '',
      transferReason: ''
    });
    this.showTransferModal = true;
  }

  closeTransferModal(): void {
    this.showTransferModal = false;
    this.selectedEvidence = null;
  }

  recordTransfer(): void {
    if (!this.canTransferEvidence) {
      this.toastr.error('You do not have permission to transfer evidence');
      return;
    }
    if (this.isLoading) return;
    this.trimFormControls(this.transferForm, ['signatureMethod', 'payloadHash', 'transferReason']);
    if (this.transferForm.invalid || !this.selectedEvidence) {
      this.transferForm.markAllAsTouched();
      this.toastr.error('The transfer cannot be recorded because the source, destination, receiving officer, signature and a reason of at least 10 characters are required.');
      return;
    }

    const formValue = this.transferForm.getRawValue();
    if (Number(formValue.fromLocationID) === Number(formValue.toLocationID)) {
      this.toastr.error('The transfer cannot be recorded because the destination is the same as the current storage location.');
      return;
    }

    if (!Number(formValue.receivingOfficerUserID)) {
      this.toastr.error('The transfer cannot be recorded because a valid receiving officer is required for chain of custody.');
      return;
    }

    this.isLoading = true;
    this.evidenceService.recordTransfer(this.selectedEvidence.evidenceID, {
      fromLocationID: Number(formValue.fromLocationID),
      toLocationID: Number(formValue.toLocationID),
      receivingOfficerUserID: Number(formValue.receivingOfficerUserID),
      signatureMethod: formValue.signatureMethod,
      payloadHash: formValue.payloadHash,
      transferReason: formValue.transferReason?.trim()
    }).subscribe({
      next: () => {
        this.toastr.success('Evidence transfer recorded in chain of custody');
        this.closeTransferModal();
        this.isLoading = false;
        this.loadEvidence();
        this.cdr.detectChanges();
      },
      error: (error) => {
        this.toastr.error(getApiErrorMessage(error, 'The evidence transfer could not be recorded'));
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  loadCustodyHistory(evidence: Evidence): void {
    if (!this.canViewCustodyHistory) {
      this.toastr.error('You do not have permission to view chain-of-custody history');
      return;
    }
    this.selectedEvidence = evidence;
    this.evidenceService.getChainOfCustody(evidence.evidenceID).subscribe({
      next: history => {
        this.custodyHistory = Array.isArray(history) ? history : [];
        this.showDetailsModal = true;
        this.cdr.detectChanges();
      },
      error: () => {
        this.custodyHistory = [];
        this.showDetailsModal = true;
        this.toastr.warning('No chain-of-custody history returned by the API');
        this.cdr.detectChanges();
      }
    });
  }
  
  getEvidenceTypeName(typeId: number): string {
    return this.evidenceTypes.find(t => Number(t?.evidenceTypeID) === Number(typeId))?.name || '';
  }

  getLocationName(locationId: number | string | null | undefined): string {
    if (!locationId) return '';
    return this.evidenceLocations.find(loc => Number(loc?.evidenceLocationID) === Number(locationId))?.locationName || '';
  }

  getRetentionReviewDate(evidence: Evidence): Date | null {
    const collectedAt = new Date(evidence.dateCollected);
    if (Number.isNaN(collectedAt.getTime())) return null;

    return new Date(
      collectedAt.getFullYear() + this.evidenceRetentionYears,
      collectedAt.getMonth(),
      collectedAt.getDate(),
      collectedAt.getHours(),
      collectedAt.getMinutes(),
      collectedAt.getSeconds()
    );
  }

  getRetentionStatus(evidence: Evidence): string {
    const reviewDateValue = this.getRetentionReviewDate(evidence);
    if (!reviewDateValue) return 'Collection date unavailable';

    const reviewDate = reviewDateValue.getTime();
    if (reviewDate < Date.now()) return 'Review Due';

    const reviewWindowDays = 30;
    const reviewWindow = reviewWindowDays * 24 * 60 * 60 * 1000;
    if (reviewDate - Date.now() <= reviewWindow) return 'Review Soon';

    return 'Within Retention';
  }

  get selectedDocket(): Docket | null {
    return this.dockets.find(docket => Number(docket?.docketID) === Number(this.selectedDocketId)) ?? null;
  }

  getDocketSubjectLabel(docket: Docket | null | undefined): string {
    return docket ? getDocketOwnerName(docket) : 'Unassigned case file';
  }

  getDocketDisplayName(docket: Docket | null | undefined): string {
    return formatDocketDisplayName(docket);
  }

  private normaliseRetentionYears(value: unknown): number {
    const years = Number(value);
    return Number.isSafeInteger(years) && years >= 1 && years <= 100
      ? years
      : DEFAULT_RUNTIME_POLICIES.evidenceRetentionYears;
  }

  private trimFormControls(form: FormGroup, controlNames: string[]): void {
    for (const name of controlNames) {
      const control = form.get(name);
      if (typeof control?.value === 'string') {
        control.setValue(control.value.trim(), { emitEvent: false });
      }
    }
  }

  private toLocalDateTimeInputValue(date: Date): string {
    const localTime = date.getTime() - date.getTimezoneOffset() * 60_000;
    return new Date(localTime).toISOString().slice(0, 16);
  }
}
