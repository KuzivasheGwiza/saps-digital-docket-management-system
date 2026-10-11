// src/app/components/person-management/person-management.ts
import { Component, OnInit, inject, ChangeDetectorRef, ChangeDetectionStrategy, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { Subject, of } from 'rxjs';
import { debounceTime, distinctUntilChanged, switchMap, catchError, takeUntil } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';
import { PersonService } from '../../services/person';
import { DocketService } from '../../services/docket';
import { AuthService } from '../../services/auth';
import { Person, CreatePersonDto, UpdatePersonDto, LinkPersonToDocketDto } from '../../models/person.model';
import { hasAnyRole, PERSON_ACTION_ROLES, ROLES } from '../../security/role-policy';
import { Docket } from '../../models/docket.model';
import { SOUTH_AFRICAN_CONTACT_PATTERN } from '../../validation/contact.validators';
import {
  notFutureDateValidator,
  optionalTrimmedMaxLengthValidator,
  positiveIntegerValidator,
  southAfricanIdBirthDateValidator,
  trimmedTextValidator
} from '../../validation/input.validators';
import { getApiErrorMessage } from '../../validation/api-error';
import { formatDocketDisplayName } from '../../shared/docket-file-name';
import { DialogService } from '../../shared/dialog.service';

@Component({
  selector: 'app-person-management',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterModule],
  templateUrl: './person-management.html',
  styleUrls: ['./person-management.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PersonManagement implements OnInit, OnDestroy {
  private personService = inject(PersonService);
  private docketService = inject(DocketService);
  private dialog = inject(DialogService);
  private authService = inject(AuthService);
  private fb = inject(FormBuilder);
  private toastr = inject(ToastrService);
  private cdr = inject(ChangeDetectorRef);
  private router = inject(Router);
  
  persons: Person[] = [];
  filteredPersons: Person[] = [];
  dockets: Docket[] = [];
  personRoles: any[] = [];
  selectedPerson: Person | null = null;
  recentRegisteredPerson: Person | null = null;
  searchTerm = '';
  isLoading = false;
  error: string = '';
  showCreateModal = false;
  showEditModal = false;
  showDetailsModal = false;
  showLinkModal = false;
  
  createForm: FormGroup;
  editForm: FormGroup;
  linkForm: FormGroup;
  
  private destroy$ = new Subject<void>();
  private searchSubject = new Subject<string>();
  
  currentUser = this.authService.getCurrentUser();
  canCreateOrEditPerson = hasAnyRole(this.currentUser?.roleName, PERSON_ACTION_ROLES.createOrEdit);
  canLinkPerson = hasAnyRole(this.currentUser?.roleName, PERSON_ACTION_ROLES.linkToDocket);
  canArchivePerson = hasAnyRole(this.currentUser?.roleName, PERSON_ACTION_ROLES.archive);
  isCscContactOnly = this.currentUser?.roleName === ROLES.cscOfficer;
  readonly todayDate = this.toLocalDateInputValue(new Date());
  personRoleSearch = '';

  getDocketDisplayName(docket: Docket): string {
    return formatDocketDisplayName(docket);
  }
  
  constructor() {
    this.createForm = this.fb.group({
      firstName: ['', [trimmedTextValidator(2, 100), Validators.pattern(/^[A-Za-zÀ-ž' .-]+$/)]],
      lastName: ['', [trimmedTextValidator(2, 100), Validators.pattern(/^[A-Za-zÀ-ž' .-]+$/)]],
      idNumber: ['', [Validators.required, Validators.pattern(/^\d{13}$/)]],
      dateOfBirth: ['', [Validators.required, notFutureDateValidator]],
      phoneNumber: ['', [Validators.required, Validators.pattern(SOUTH_AFRICAN_CONTACT_PATTERN)]],
      emailAddress: ['', [Validators.required, Validators.email, Validators.maxLength(100)]],
      physicalAddress: ['', [trimmedTextValidator(5, 500)]],
      personRoleID: ['', [Validators.required, positiveIntegerValidator]]
    }, { validators: southAfricanIdBirthDateValidator });
    
    this.editForm = this.fb.group({
      firstName: ['', [trimmedTextValidator(2, 100), Validators.pattern(/^[A-Za-zÀ-ž' .-]+$/)]],
      lastName: ['', [trimmedTextValidator(2, 100), Validators.pattern(/^[A-Za-zÀ-ž' .-]+$/)]],
      phoneNumber: ['', [Validators.pattern(SOUTH_AFRICAN_CONTACT_PATTERN)]],
      emailAddress: ['', [Validators.email, Validators.maxLength(100)]],
      physicalAddress: ['', [optionalTrimmedMaxLengthValidator(500)]]
    });
    
    this.linkForm = this.fb.group({
      docketID: ['', [Validators.required, positiveIntegerValidator]],
      relationshipRole: ['', [trimmedTextValidator(2, 50)]]
    });
  }
  
  ngOnInit(): void {
    // Show the authorised person register immediately; search refines it.
    this.loadPersons();
    this.loadDockets();
    this.loadPersonRoles();
    this.setupSearch();
    if (this.router.url.split('?')[0] === '/persons/create') {
      this.openCreateModal();
    }
  }
  
  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
  
  private setupSearch(): void {
    this.searchSubject.pipe(
      debounceTime(500),
      distinctUntilChanged(),
      switchMap(term => {
        const trimmedTerm = term?.trim() || '';
        this.isLoading = true;
        this.error = '';
        this.cdr.markForCheck();
        const request = trimmedTerm.length >= 2
          ? this.personService.searchPersons(trimmedTerm)
          : this.personService.getAllPersons();
        return request.pipe(
          catchError(err => {
            console.error('Search error:', err);
            this.error = err.error?.message || 'Failed to search persons';
            this.toastr.error(this.error);
            return of([]);
          })
        );
      }),
      takeUntil(this.destroy$)
    ).subscribe(results => {
      this.filteredPersons = Array.isArray(results) ? results : [];
      if (!this.searchTerm.trim()) this.persons = [...this.filteredPersons];
      this.isLoading = false;
      this.cdr.markForCheck();
    });
  }
  
  onSearchInput(): void {
    const term = this.searchTerm?.trim() || '';
    this.searchSubject.next(term.length >= 2 ? term : '');
  }
  
  loadPersons(): void {
    this.isLoading = true;
    this.error = '';
    this.cdr.markForCheck();

    this.personService.getAllPersons()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: persons => {
          this.persons = Array.isArray(persons) ? persons : [];
          this.filteredPersons = [...this.persons];
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: err => {
          this.error = getApiErrorMessage(err, 'The person register could not be loaded.');
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }
  
  loadDockets(): void {
    this.docketService.getDockets()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (dockets) => {
          this.dockets = Array.isArray(dockets) ? dockets : [];
          this.cdr.markForCheck();
        },
        error: (err) => console.error('Failed to load dockets', err)
      });
  }
  
  loadPersonRoles(): void {
    this.personService.getPersonRoles()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (roles) => {
          this.personRoles = (Array.isArray(roles) ? roles : [])
            .sort((left, right) => String(left?.roleName || '').localeCompare(String(right?.roleName || '')));
          this.cdr.markForCheck();
        },
        error: (err) => console.error('Failed to load person roles', err)
      });
  }
  
  openCreateModal(): void {
    if (!this.canCreateOrEditPerson) {
      this.toastr.error('You do not have permission to register persons');
      return;
    }
    this.createForm.reset({
      firstName: '',
      lastName: '',
      idNumber: '',
      dateOfBirth: '',
      phoneNumber: '',
      emailAddress: '',
      physicalAddress: '',
      personRoleID: ''
    });
    this.personRoleSearch = '';
    this.showCreateModal = true;
    this.cdr.markForCheck();
  }
  
  closeCreateModal(): void {
    this.showCreateModal = false;
    if (this.router.url.split('?')[0] === '/persons/create') {
      this.router.navigate(['/persons']);
    }
    this.cdr.markForCheck();
  }
  
  createPerson(): void {
    if (!this.canCreateOrEditPerson) {
      this.toastr.error('You do not have permission to register persons');
      return;
    }
    if (this.isLoading) return;
    this.trimFormControls(this.createForm, ['firstName', 'lastName', 'idNumber', 'phoneNumber', 'emailAddress', 'physicalAddress']);
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      this.toastr.error('Please fill required fields correctly');
      this.cdr.markForCheck();
      return;
    }
    
    this.isLoading = true;
    this.cdr.markForCheck();
    const formValue = this.createForm.value;
    const personData: CreatePersonDto = {
      firstName: formValue.firstName.trim(),
      lastName: formValue.lastName.trim(),
      idNumber: formValue.idNumber.trim(),
      dateOfBirth: formValue.dateOfBirth,
      phoneNumber: formValue.phoneNumber.trim(),
      emailAddress: formValue.emailAddress.trim().toLowerCase(),
      physicalAddress: formValue.physicalAddress.trim(),
      personRoleID: Number(formValue.personRoleID)
    };
    
    this.personService.createPerson(personData)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          this.toastr.success('Person created successfully');
          this.searchTerm = '';
          this.isLoading = false;
          this.closeCreateModal();
          this.loadPersons();

          const personId = this.getCreatedPersonId(result);
          if (personId) {
            this.loadRecentRegisteredPerson(personId);
          } else {
            this.recentRegisteredPerson = {
              ...personData,
              personID: 0,
              fullName: `${personData.firstName} ${personData.lastName}`,
              createdAt: new Date()
            } as Person;
            this.cdr.markForCheck();
          }
        },
        error: (err) => {
          console.error('Failed to create person', err);
          const errorMessage = getApiErrorMessage(err, 'Person could not be registered');
          this.toastr.error(errorMessage);
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }

  onPersonRoleSearch(value: string): void {
    this.personRoleSearch = value;
    const selectedRole = this.personRoles.find(role =>
      String(role?.roleName || '').localeCompare(value.trim(), undefined, { sensitivity: 'accent' }) === 0
    );
    this.createForm.get('personRoleID')?.setValue(selectedRole?.personRoleID ?? null);
    this.createForm.get('personRoleID')?.markAsTouched();
  }
  
  openEditModal(person: Person): void {
    if (!this.canCreateOrEditPerson) {
      this.toastr.error('You do not have permission to edit person records');
      return;
    }
    if (person.isArchived) {
      this.toastr.warning('Archived person records are read-only.');
      return;
    }
    this.selectedPerson = person;
    this.editForm.patchValue({
      firstName: person.firstName,
      lastName: person.lastName,
      phoneNumber: person.phoneNumber || '',
      emailAddress: person.emailAddress || '',
      physicalAddress: person.physicalAddress || ''
    });
    this.showEditModal = true;
    this.cdr.markForCheck();
  }
  
  closeEditModal(): void {
    this.showEditModal = false;
    this.selectedPerson = null;
    this.cdr.markForCheck();
  }
  
  updatePerson(): void {
    if (!this.canCreateOrEditPerson) {
      this.toastr.error('You do not have permission to edit person records');
      return;
    }
    if (this.isLoading) return;
    this.trimFormControls(this.editForm, ['firstName', 'lastName', 'emailAddress', 'physicalAddress']);
    if (this.editForm.invalid || !this.selectedPerson) {
      this.editForm.markAllAsTouched();
      this.toastr.error('Person details cannot be updated because one or more highlighted fields are invalid.');
      return;
    }
    
    this.isLoading = true;
    this.cdr.markForCheck();
    const formValue = this.editForm.getRawValue();
    const updateData: UpdatePersonDto = {
      firstName: formValue.firstName.trim(),
      lastName: formValue.lastName.trim(),
      phoneNumber: formValue.phoneNumber?.trim() || '',
      emailAddress: formValue.emailAddress?.trim().toLowerCase() || '',
      physicalAddress: formValue.physicalAddress?.trim() || ''
    };
    
    this.personService.updatePerson(this.selectedPerson.personID, updateData)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toastr.success('Person details updated successfully');
          this.loadPersons();
          this.closeEditModal();
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: (err) => {
          console.error('Failed to update person', err);
          const errorMessage = getApiErrorMessage(err, 'Person details could not be updated');
          this.toastr.error(errorMessage);
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }
  
  viewPersonDetails(person: Person): void {
    this.openPersonDetailsById(person.personID);
  }

  viewRecentRegisteredPerson(): void {
    if (!this.recentRegisteredPerson) return;

    if (this.recentRegisteredPerson.personID) {
      this.openPersonDetailsById(this.recentRegisteredPerson.personID);
      return;
    }

    this.selectedPerson = this.recentRegisteredPerson;
    this.showDetailsModal = true;
    this.cdr.markForCheck();
  }

  clearRecentRegisteredPerson(): void {
    this.recentRegisteredPerson = null;
    this.cdr.markForCheck();
  }

  private getCreatedPersonId(result: any): number | null {
    const personId = result?.personId ?? result?.personID ?? result?.PersonID ?? result?.data?.personID ?? result?.data?.personId;
    const numericId = Number(personId);
    return Number.isSafeInteger(numericId) && numericId > 0 ? numericId : null;
  }

  private loadRecentRegisteredPerson(personId: number): void {
    this.isLoading = true;
    this.cdr.markForCheck();

    this.personService.getPersonById(personId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (person) => {
          this.recentRegisteredPerson = person;
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: (err) => {
          console.error('Failed to load registered person', err);
          this.toastr.warning('Person saved, but the registered details could not be loaded');
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }

  private openPersonDetailsById(personId: number): void {
    this.isLoading = true;
    this.error = '';
    this.cdr.markForCheck();

    this.personService.getPersonById(personId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (person) => {
          this.selectedPerson = person;
          this.showDetailsModal = true;
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: (err) => {
          console.error('Failed to load person details', err);
          const errorMessage = err.error?.message || 'Failed to load person details';
          this.toastr.error(errorMessage);
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }
  
  closeDetailsModal(): void {
    this.showDetailsModal = false;
    this.selectedPerson = null;
    this.cdr.markForCheck();
  }
  
  openLinkModal(person: Person): void {
    if (!this.canLinkPerson) {
      this.toastr.error('You do not have permission to link persons to dockets');
      return;
    }
    if (person.isArchived) {
      this.toastr.warning('Archived person records cannot be linked to dockets.');
      return;
    }
    this.selectedPerson = person;
    const defaultRole = this.currentUser?.roleName === 'CSC Officer'
      ? 'Complainant'
      : (person.personRole || '');
    this.linkForm.reset({ docketID: '', relationshipRole: defaultRole });
    this.showLinkModal = true;
    this.cdr.markForCheck();
  }
  
  closeLinkModal(): void {
    this.showLinkModal = false;
    this.selectedPerson = null;
    this.cdr.markForCheck();
  }
  
  linkPersonToDocket(): void {
    if (!this.canLinkPerson) {
      this.toastr.error('You do not have permission to link persons to dockets');
      return;
    }
    if (this.isLoading) return;
    if (this.linkForm.invalid || !this.selectedPerson) {
      this.linkForm.markAllAsTouched();
      this.toastr.error('The person cannot be linked because a valid docket and relationship are required.');
      return;
    }
    
    this.isLoading = true;
    this.cdr.markForCheck();
    const linkData: LinkPersonToDocketDto = {
      personID: this.selectedPerson.personID,
      docketID: Number(this.linkForm.value.docketID),
      relationshipRole: this.linkForm.value.relationshipRole.trim()
    };
    
    this.personService.linkPersonToDocket(linkData)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toastr.success(`${this.selectedPerson?.firstName} ${this.selectedPerson?.lastName} has been linked to the docket`);
          this.closeLinkModal();
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: (err) => {
          console.error('Failed to link person', err);
          const errorMessage = getApiErrorMessage(err, 'The person could not be linked to the docket');
          this.toastr.error(errorMessage);
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }

  async archivePerson(person: Person): Promise<void> {
    if (!this.canArchivePerson) {
      this.toastr.error('You do not have permission to archive person records');
      return;
    }
    if (this.isLoading) return;
    if (person.isArchived) {
      this.toastr.info('This person record is already archived.');
      return;
    }
    const reason = await this.dialog.open({
      title: 'Archive Person Record',
      message: `Archive ${person.firstName} ${person.lastName}? The record remains available for audit and historical docket references.`,
      inputLabel: 'Reason for archiving', inputType: 'textarea', required: true, minLength: 10, maxLength: 500,
      initialValue: 'Person record no longer active', confirmText: 'Archive Person', tone: 'danger'
    });
    if (!reason || reason.trim().length < 10) {
      this.toastr.error('A reason of at least 10 characters is required');
      return;
    }

    this.isLoading = true;
    this.personService.archivePerson(person.personID, reason.trim())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toastr.success('Person archived');
          this.closeDetailsModal();
          this.retry();
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: err => {
          this.toastr.error(err.error?.message || 'Failed to archive person');
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }
  
  retry(): void {
    if (this.searchTerm && this.searchTerm.length >= 2) {
      this.searchSubject.next(this.searchTerm);
    } else {
      this.loadPersons();
    }
  }
  
  formatDate(date: Date): string {
    if (!date) return '-';
    const parsed = new Date(date);
    return Number.isNaN(parsed.getTime()) ? '-' : parsed.toLocaleDateString();
  }

  private toLocalDateInputValue(date: Date): string {
    const localTime = date.getTime() - date.getTimezoneOffset() * 60_000;
    return new Date(localTime).toISOString().slice(0, 10);
  }

  private trimFormControls(form: FormGroup, controlNames: string[]): void {
    for (const name of controlNames) {
      const control = form.get(name);
      if (typeof control?.value === 'string') {
        control.setValue(control.value.trim(), { emitEvent: false });
      }
    }
  }
}
