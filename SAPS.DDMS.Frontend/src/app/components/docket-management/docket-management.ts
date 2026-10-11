// src/app/components/docket-management/docket-management.ts
import { Component, OnInit, inject, ChangeDetectorRef, ChangeDetectionStrategy, OnDestroy, ElementRef, ViewChild, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import * as L from 'leaflet';

interface AddressSearchResult {
  displayName: string;
  latitude: number;
  longitude: number;
}

type AiReviewInputType = 'text' | 'textarea' | 'number' | 'datetime-local';

interface AiExtractionReviewField {
  key: string;
  label: string;
  section: string;
  inputType: AiReviewInputType;
  value: string | number;
  originalType: 'string' | 'number' | 'boolean' | 'object' | 'null';
  readOnly: boolean;
  error: string;
}

interface RelatedCaseReviewItem {
  relatedDocketId: number;
  score: number;
  reason: string;
  included: boolean;
  scoreError: string;
  reasonError: string;
}
import { ToastrService } from 'ngx-toastr';
import { CaseReadinessIntelligence, DocketService } from '../../services/docket';
import { IncidentService } from '../../services/incident';
import { AuthService } from '../../services/auth';
import { InvestigationNoteService } from '../../services/investigation-note';
import { PersonService } from '../../services/person';
import {
  DEFAULT_INCIDENT_LOCATION_TYPES,
  DEFAULT_COURT_OUTCOMES,
  DEFAULT_DOCKET_STATUSES,
  DEFAULT_RUNTIME_POLICIES,
  ReferenceDataService,
  RuntimePolicies
} from '../../services/reference-data';
import { Docket, CreateDocketDto, UpdateDocketDto, AllocateDocketDto, ReassignDocketDto } from '../../models/docket.model';
import { InvestigationNote } from '../../models/investigation-note.model';
import {
  formatDocketDisplayName,
  getDocketFileStem,
  getDocketOwnerName
} from '../../shared/docket-file-name';
import { DialogService } from '../../shared/dialog.service';
import {
  DOCKET_ACTION_ROLES,
  hasAnyRole,
  ROUTE_ROLES,
  ROLES
} from '../../security/role-policy';

@Component({
  selector: 'app-docket-management',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterModule],
  templateUrl: './docket-management.html',
  styleUrls: ['./docket-management.css', './docket-ai-review.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DocketManagement implements OnInit, OnDestroy {
  @ViewChild('locationMap') locationMapElement?: ElementRef<HTMLDivElement>;
  private docketService = inject(DocketService);
  private incidentService = inject(IncidentService);
  private authService = inject(AuthService);
  private noteService = inject(InvestigationNoteService);
  private personService = inject(PersonService);
  private referenceData = inject(ReferenceDataService);
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private toastr = inject(ToastrService);
  private cdr = inject(ChangeDetectorRef);
  private dialog = inject(DialogService);

  /**
   * Dismiss an open row-actions menu when the user continues elsewhere on the
   * page.  Native <details> menus otherwise remain open until their trigger is
   * clicked again, which can obscure the rows below.
   */
  @HostListener('document:click', ['$event'])
  closeDocketActionsOnOutsideClick(event: MouseEvent): void {
    const target = event.target;
    if (target instanceof Element && target.closest('.actions-menu')) {
      return;
    }

    document.querySelectorAll<HTMLDetailsElement>('.actions-menu[open]')
      .forEach(menu => menu.open = false);
  }

  @HostListener('document:keydown.escape')
  closeDocketActionsOnEscape(): void {
    document.querySelectorAll<HTMLDetailsElement>('.actions-menu[open]')
      .forEach(menu => menu.open = false);
  }
  
  dockets: Docket[] = [];
  filteredDockets: Docket[] = [];
  selectedDocket: Docket | null = null;
  availableOfficers: any[] = [];
  incidentTypes: any[] = [];
  incidentLocationTypes = [...DEFAULT_INCIDENT_LOCATION_TYPES].sort((a, b) => a.localeCompare(b));
  readonly caseTypes = ['Cybercrime', 'Domestic Violence', 'Financial Crime', 'Missing Person', 'Normal', 'Organised Crime', 'Urgent / Priority', 'Other'];
  readonly sensitivityLevels = [
    { value: 'NORMAL', label: 'Normal' },
    { value: 'RESTRICTED', label: 'Restricted' },
    { value: 'HIGHLY_SENSITIVE', label: 'Highly Sensitive' }
  ] as const;
  private locationMap?: L.Map;
  private locationMarker?: L.CircleMarker;
  isMapLoading = false;
  mapLoadError = '';
  isResolvingLocation = false;
  locationLookupError = '';
  addressSearchQuery = '';
  addressSearchResults: AddressSearchResult[] = [];
  isSearchingAddress = false;
  addressSearchError = '';
  private addressSearchRequest?: AbortController;
  docketNotes: InvestigationNote[] = [];
  docketNotesError = '';
  isLoading = false;
  error: string = '';
  searchTerm = '';
  selectedStatus = 'ALL';
  dateFrom = '';
  dateTo = '';
  showCreateModal = false;
  showAllocateModal = false;
  isReassigning = false;
  showDetailsModal = false;
  showEditModal = false;
  showStatusModal = false;
  showInspectionModal = false;
  showProsecutorPreviewModal = false;
  showBundleModal = false;
  showBundleViewModal = false;
  showBundleEditModal = false;
  prosecutorPreview: any = null;
  prosecutorPreviewError = '';
  prosecutorBundles: any[] = [];
  selectedBundleDetails: any = null;
  selectedBundleForPrivacy: any = null;
  selectedBundleItemHistory: any = null;
  bundleItemPrivacyHistory: any[] = [];
  bundleViewTab: 'notes' | 'evidence' = 'notes';
  bundleTitle = '';
  bundleNotes = '';
  shareBundleCandidate: any = null;
  sharePreview: any = null;
  eligibleProsecutors: any[] = [];
  selectedProsecutorUserID: number | null = null;
  shareNote = '';
  isSharingBundle = false;
  shareWorkflowError = '';
  isLoadingSharePreview = false;
  aiSummary = '';
  aiSummaryConfirmed = false;
  aiExtractionText = '';
  aiExtractionFields: AiExtractionReviewField[] = [];
  showAiTechnicalData = false;
  readonly aiExtractionSectionOrder = ['Case Identification', 'Incident Details', 'Narrative', 'Case Information', 'Additional Extracted Data'];
  relatedCases: any[] = [];
  relatedCasesReviewText = '';
  relatedCaseReviewItems: RelatedCaseReviewItem[] = [];
  showRelatedCasesTechnicalData = false;
  relatedCasesConfirmed = false;
  readinessIntelligence: CaseReadinessIntelligence | null = null;
  readinessLoading = false;
  privacyHistory: any[] = [];
  
  // UC 3.4 — Archive Docket
  showArchiveModal = false;

  // UC 8.1 — Record Court Outcome
  showCourtOutcomeModal = false;
  courtOutcomeForm!: FormGroup;
  courtOutcomeOptions = [...DEFAULT_COURT_OUTCOMES].sort((a, b) => a.localeCompare(b));

  statusOptions = ['ALL', ...[...DEFAULT_DOCKET_STATUSES].sort((a, b) => a.localeCompare(b))];
  transitionOptions = this.buildTransitionOptions(DEFAULT_DOCKET_STATUSES);
  runtimePolicies: RuntimePolicies = { ...DEFAULT_RUNTIME_POLICIES };
  
  createForm: FormGroup;
  allocateForm: FormGroup;
  editForm: FormGroup;
  statusForm: FormGroup;
  inspectionForm: FormGroup;
  bundleEditForm: FormGroup;
  
  private destroy$ = new Subject<void>();
  
  today = this.toLocalDateInputValue(new Date());
  currentUser = this.authService.getCurrentUser();
  private roleName = this.currentUser?.roleName;
  canCreateDocket = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.create);
  canAllocateDocket = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.allocate);
  canAcceptAssignment = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.acceptAssignment);
  canCompleteInspection = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.completeInspection);
  canUpdateDocket = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.updateMetadata);
  canUpdateStatus = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.changeStatus);
  canPreviewForProsecutor = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.prosecutorPreview);
  canManageBundles = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.viewBundles);
  canCreateBundle = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.createBundle);
  canEditBundles = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.editBundle);
  canExportBundle = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.exportBundle);
  canShareBundle = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.shareBundle);
  canDownloadSharedBundle = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.downloadSharedBundle);
  canPrivacyFlag = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.setPrivacy);
  canViewPrivacyHistory = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.viewPrivacyHistory);
  canRunAi = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.runAi);
  canVerifyAi = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.verifyAiExtraction);
  canAccessNotes = hasAnyRole(this.roleName, ROUTE_ROLES.investigationNotes);
  canAccessEvidence = hasAnyRole(this.roleName, ROUTE_ROLES.evidence);
  canArchiveDocket = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.archive);
  canRecordCourtOutcome = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.recordCourtOutcome);
  canCloseOrReopenDocket = hasAnyRole(this.roleName, DOCKET_ACTION_ROLES.closeOrReopen);

  get isDocketAdministrator(): boolean {
    return this.roleName === ROLES.docketAdministrator;
  }

  get canCreateBundlePrivacyFlag(): boolean {
    return this.roleName === ROLES.investigatingOfficer;
  }

  get canManageBundlePrivacyFlag(): boolean {
    return this.roleName === ROLES.dscSupervisor;
  }

  get canReadBundlePrivacyFlagHistory(): boolean {
    return this.roleName === ROLES.dscSupervisor || this.roleName === ROLES.docketAdministrator;
  }
  
  constructor() {
    this.createForm = this.fb.group({
      obReferenceNumber: [{ value: 'Generated automatically', disabled: true }],
      ficReferenceNumber: [{ value: 'Generated automatically', disabled: true }],
      caseType: ['Normal', [Validators.required]],
      sensitivityLevel: ['NORMAL', [Validators.required]],
      otherCaseType: [''],
      docketSummary: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(1000)]],
      modusOperandi: ['', [Validators.maxLength(1000)]],
      incident: this.fb.group({
        occurrenceDate: ['', [Validators.required]],
        locationType: ['', [Validators.required]],
        description: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(1000)]],
        incidentTypeID: [null, [Validators.required]],
        otherIncidentType: [''],
        otherLocationType: [''],
        latitude: [''],
        longitude: [''],
        areaName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(120)]]
      }),
      complainantFullName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100), Validators.pattern(/^[A-Za-zÀ-ž' -]+$/)]],
      complainantContactNumber: ['', [Validators.pattern(/^(?:0\d{9}|\+27\d{9})$/)]]
    });
    
    this.allocateForm = this.fb.group({
      investigatingOfficerUserID: ['', [Validators.required]],
      reason: ['']
    });

    this.editForm = this.fb.group({
      docketSummary: ['', [Validators.required]],
      modusOperandi: [''],
      sensitivityLevel: ['NORMAL', [Validators.required]],
      reasonForUpdate: ['', [Validators.required, Validators.minLength(20)]]
    });

    this.statusForm = this.fb.group({
      status: ['', [Validators.required]],
      reason: ['', [Validators.required, Validators.minLength(10)]]
    });

    this.inspectionForm = this.fb.group({
      notes: ['', [Validators.required, Validators.minLength(10)]]
    });

    this.bundleEditForm = this.fb.group({
      title: ['', [Validators.required, Validators.minLength(3)]],
      notes: [''],
      aiSummaryText: [''],
      includeNotes: [true],
      includeEvidence: [true],
      reason: ['', [Validators.required, Validators.minLength(3)]]
    });

    // UC 8.1 — Court Outcome form
    this.courtOutcomeForm = this.fb.group({
      outcome:          ['', [Validators.required]],
      courtDate:        ['', [Validators.required]],
      presidingOfficer: ['', [Validators.required, Validators.minLength(3)]],
      sentence:         [''],
      notes:            ['']
    });
  }
  
  ngOnInit(): void {
    this.loadOperationalLookups();
    this.loadDockets();
    this.loadIncidentTypes();
    this.loadAvailableOfficers();

    if (this.router.url.split('?')[0] === '/dockets/create') {
      this.openCreateModal();
    }

    this.route.params
      .pipe(takeUntil(this.destroy$))
      .subscribe(params => {
        const id = Number(params['id']);
        if (id) {
          this.loadDocketDetails(id);
        }
      });
  }
  
  ngOnDestroy(): void {
    this.locationMap?.remove();
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadOperationalLookups(): void {
    this.referenceData.getOperationalLookups()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: lookups => {
          const liveStatuses = this.normaliseStatuses(lookups.docketStatuses);
          this.statusOptions = ['ALL', ...liveStatuses.sort((a, b) => a.localeCompare(b))];
          this.transitionOptions = this.buildTransitionOptions(liveStatuses);
          this.courtOutcomeOptions = this.normaliseStrings(lookups.courtOutcomes, DEFAULT_COURT_OUTCOMES);
          this.incidentLocationTypes = this.normaliseStrings(
            lookups.incidentLocationTypes,
            DEFAULT_INCIDENT_LOCATION_TYPES
          );
          this.runtimePolicies = {
            ...DEFAULT_RUNTIME_POLICIES,
            ...(lookups.runtimePolicies || {})
          };
          if (!this.statusOptions.includes(this.selectedStatus)) {
            this.selectedStatus = 'ALL';
          }
          this.filterDockets();
          this.cdr.markForCheck();
        },
        error: () => {
          this.statusOptions = ['ALL', ...[...DEFAULT_DOCKET_STATUSES].sort((a, b) => a.localeCompare(b))];
          this.transitionOptions = this.buildTransitionOptions(DEFAULT_DOCKET_STATUSES);
          this.courtOutcomeOptions = [...DEFAULT_COURT_OUTCOMES].sort((a, b) => a.localeCompare(b));
          this.incidentLocationTypes = [...DEFAULT_INCIDENT_LOCATION_TYPES].sort((a, b) => a.localeCompare(b));
          this.runtimePolicies = { ...DEFAULT_RUNTIME_POLICIES };
          this.filterDockets();
          this.cdr.markForCheck();
        }
      });
  }
  
  loadDockets(): void {
    this.isLoading = true;
    this.error = '';
    this.cdr.markForCheck();
    
    this.docketService.getDockets()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (dockets) => {
          this.dockets = dockets || [];
          this.filterDockets();
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: (err) => {
          console.error('Failed to load dockets', err);
          this.error = err.error?.message || 'Failed to load dockets';
          this.toastr.error(this.error);
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }
  
  loadDocketDetails(id: number): void {
    this.docketService.getDocketById(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (docket) => {
          this.selectedDocket = docket;
          this.loadDocketNotes(docket.investigationID);
          this.showDetailsModal = true;
          this.cdr.markForCheck();
        },
        error: (err) => {
          console.error('Failed to load docket details', err);
          this.toastr.error(err.error?.message || 'Failed to load docket details');
        }
      });
  }
  
  loadIncidentTypes(): void {
    this.incidentService.getIncidentTypes()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (types) => {
          this.incidentTypes = (Array.isArray(types) ? types : [])
            .sort((left, right) => String(left?.typeName || '').localeCompare(String(right?.typeName || '')));
          this.cdr.markForCheck();
        },
        error: (err) => console.error('Failed to load incident types', err)
      });
  }
  
  loadAvailableOfficers(): void {
    // Only DSC Supervisors can access this endpoint
    if (!this.canAllocateDocket) {
      console.log('User does not have permission to view available officers');
      this.availableOfficers = [];
      return;
    }
    
    this.docketService.getAvailableOfficers()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (officers) => {
          this.availableOfficers = officers || [];
          this.cdr.markForCheck();
        },
        error: (err) => {
          // Silently handle 403 Forbidden - user just doesn't have permission
          if (err.status === 403) {
            console.log('Access denied: User cannot view available officers');
            this.availableOfficers = [];
          } else {
            console.error('Failed to load officers', err);
          }
          this.cdr.markForCheck();
        }
      });
  }
  
  filterDockets(): void {
    let filtered = this.dockets;
    
    if (this.selectedStatus !== 'ALL') {
      filtered = filtered.filter(d => d.status === this.selectedStatus);
    }

    if (this.searchTerm && this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(d => 
        d.docketNumber?.toLowerCase().includes(term) ||
        d.obReferenceNumber?.toLowerCase().includes(term) ||
        d.ficReferenceNumber?.toLowerCase().includes(term) ||
        d.createdByName?.toLowerCase().includes(term) ||
        d.crimeCategory?.toLowerCase().includes(term) ||
        d.suspectName?.toLowerCase().includes(term) ||
        d.stationName?.toLowerCase().includes(term) ||
        d.jurisdiction?.toLowerCase().includes(term) ||
        d.complainantFullName?.toLowerCase().includes(term) ||
        d.caseType?.toLowerCase().includes(term) ||
        new Date(d.dateCreated).toISOString().slice(0, 10).includes(term) ||
        d.incident?.incidentType?.toLowerCase().includes(term) ||
        d.incident?.areaName?.toLowerCase().includes(term)
      );
    }
    
    this.filteredDockets = filtered;
    this.cdr.markForCheck();
  }
  
  onStatusChange(): void {
    this.filterDockets();
  }
  
  onSearch(): void {
    this.filterDockets();
  }

  getDocketSubjectLabel(docket: Docket | null | undefined): string {
    return docket ? getDocketOwnerName(docket) : 'Unassigned case file';
  }

  isCurrentOfficerAssignment(docket: Docket): boolean {
    return this.currentUser?.roleName === ROLES.investigatingOfficer
      && docket.assignedOfficerUserID === this.currentUser.userID;
  }

  getDocketDisplayName(docket: Docket | null | undefined): string {
    return formatDocketDisplayName(docket);
  }

  getOfficerWorkloadLabel(officer: any): string {
    const activeCount = Number(officer?.activeDocketCount ?? 0);
    const maxCount = Number(officer?.maximumActiveDockets ?? this.runtimePolicies.maxActiveDocketsPerOfficer);
    const suffix = this.isOfficerAtCapacity(officer) ? 'at capacity' : 'available';
    return `${activeCount}/${maxCount} active dockets • ${suffix}`;
  }

  isOfficerAtCapacity(officer: any): boolean {
    const activeCount = Number(officer?.activeDocketCount ?? 0);
    const maxCount = Number(officer?.maximumActiveDockets ?? this.runtimePolicies.maxActiveDocketsPerOfficer);
    return officer?.atCapacity === true || (maxCount > 0 && activeCount >= maxCount);
  }

  getSelectedOfficer(): any | null {
    const selectedOfficerId = Number(this.allocateForm.get('investigatingOfficerUserID')?.value);
    if (!selectedOfficerId) return null;
    return this.availableOfficers.find(officer => Number(officer.id) === selectedOfficerId) ?? null;
  }

  openNotesForDocket(docket: Docket): void {
    this.router.navigate(['/investigation', docket.docketID]);
  }

  openEvidenceForDocket(docket: Docket): void {
    this.showDetailsModal = false;
    this.selectedDocket = null;
    this.router.navigate(['/evidence'], { queryParams: { docketId: docket.docketID } });
  }

  loadDocketNotes(investigationId?: number): void {
    if (!investigationId) {
      this.docketNotes = [];
      this.docketNotesError = '';
      this.cdr.markForCheck();
      return;
    }

    this.noteService.getInvestigationNotes(investigationId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: notes => {
          this.docketNotes = notes || [];
          this.docketNotesError = '';
          this.cdr.markForCheck();
        },
        error: err => {
          this.docketNotes = [];
          this.docketNotesError = err.error?.message || 'Investigation notes could not be loaded for this docket.';
          this.cdr.markForCheck();
        }
      });
  }
  
  openCreateModal(): void {
    if (!this.canCreateDocket) {
      this.toastr.error('Only CSC Officers may create new dockets');
      return;
    }
    this.createForm.reset();
    this.createForm.patchValue({
      caseType: 'Normal',
      sensitivityLevel: 'NORMAL',
      incident: {
        locationType: ''
      }
    });
    this.configureOtherValidators();
    this.mapLoadError = '';
    this.isMapLoading = true;
    this.showCreateModal = true;
    this.cdr.detectChanges();
    this.initialiseLocationMap();
  }

  onClassificationChange(): void {
    this.configureOtherValidators();
    const incidentTypeID = this.createForm.get('incident.incidentTypeID')?.value;
    const incidentTypeName = this.incidentTypes.find(type => String(type.incidentTypeID) === String(incidentTypeID))?.typeName || '';
    const classification = `${this.createForm.get('caseType')?.value || ''} ${incidentTypeName}`;
    if (/minor|child|sexual|rape/i.test(classification)) {
      this.createForm.get('sensitivityLevel')?.setValue('HIGHLY_SENSITIVE');
    } else if (/domestic violence|missing person/i.test(classification) && this.createForm.get('sensitivityLevel')?.value === 'NORMAL') {
      this.createForm.get('sensitivityLevel')?.setValue('RESTRICTED');
    }
    this.cdr.markForCheck();
  }

  private configureOtherValidators(): void {
    const controls = [
      { selector: this.createForm.get('caseType')?.value === 'Other', control: this.createForm.get('otherCaseType') },
      { selector: this.createForm.get('incident.incidentTypeID')?.value === 'OTHER', control: this.createForm.get('incident.otherIncidentType') },
      { selector: this.createForm.get('incident.locationType')?.value === 'Other', control: this.createForm.get('incident.otherLocationType') }
    ];
    controls.forEach(({ selector, control }) => {
      control?.setValidators(selector
        ? [Validators.required, Validators.minLength(2), Validators.maxLength(100)]
        : []);
      if (!selector) control?.setValue('', { emitEvent: false });
      control?.updateValueAndValidity({ emitEvent: false });
    });
  }

  private initialiseLocationMap(): void {
    if (!this.locationMapElement || this.locationMap) return;
    try {
      const initial: L.LatLngExpression = [-25.7479, 28.2293];
      this.locationMap = L.map(this.locationMapElement.nativeElement, {
        center: initial,
        zoom: 11,
        zoomControl: true
      });
      const tiles = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/{z}/{y}/{x}', {
        maxZoom: 19,
        attribution: 'Tiles &copy; Esri'
      });
      tiles.once('load', () => {
        this.isMapLoading = false;
        this.mapLoadError = '';
        this.cdr.markForCheck();
      });
      tiles.once('tileerror', () => {
        this.isMapLoading = false;
        this.mapLoadError = 'The map background could not be loaded. Check the network connection and try opening Create Docket again.';
        this.cdr.markForCheck();
      });
      tiles.addTo(this.locationMap);
      this.locationMap.on('click', event => this.selectMapLocation(event.latlng.lat, event.latlng.lng));
      requestAnimationFrame(() => this.locationMap?.invalidateSize());
    } catch {
      this.isMapLoading = false;
      this.mapLoadError = 'The interactive map could not be started. Close Create Docket and try again.';
      this.locationMap?.remove();
      this.locationMap = undefined;
      this.cdr.markForCheck();
    }
  }

  useCurrentLocation(): void {
    if (!navigator.geolocation) {
      this.locationLookupError = 'Your browser does not support location services. Select the incident location on the map.';
      return;
    }
    this.isResolvingLocation = true;
    navigator.geolocation.getCurrentPosition(
      position => {
        const { latitude, longitude } = position.coords;
        this.locationMap?.setView([latitude, longitude], 16);
        void this.selectMapLocation(latitude, longitude);
      },
      () => {
        this.isResolvingLocation = false;
        this.locationLookupError = 'Your location could not be accessed. Allow location permission or select the incident location on the map.';
        this.cdr.markForCheck();
      },
      { enableHighAccuracy: true, timeout: 10000 }
    );
  }

  async searchAddress(): Promise<void> {
    const query = this.addressSearchQuery.trim();
    this.addressSearchError = '';
    this.addressSearchResults = [];
    if (query.length < 3) {
      this.addressSearchError = 'Enter at least 3 characters to search for an address.';
      this.cdr.markForCheck();
      return;
    }

    this.addressSearchRequest?.abort();
    this.addressSearchRequest = new AbortController();
    this.isSearchingAddress = true;
    this.cdr.markForCheck();
    try {
      const params = new URLSearchParams({
        format: 'jsonv2',
        q: query,
        countrycodes: 'za',
        addressdetails: '1',
        limit: '5'
      });
      const response = await fetch(`https://nominatim.openstreetmap.org/search?${params}`, {
        headers: { 'Accept-Language': 'en' },
        signal: this.addressSearchRequest.signal
      });
      if (!response.ok) throw new Error('Address search failed');
      const payload = await response.json();
      this.addressSearchResults = Array.isArray(payload)
        ? payload
            .map(item => ({
              displayName: String(item?.display_name || '').trim(),
              latitude: Number(item?.lat),
              longitude: Number(item?.lon)
            }))
            .filter(item => item.displayName && Number.isFinite(item.latitude) && Number.isFinite(item.longitude))
        : [];
      if (!this.addressSearchResults.length) {
        this.addressSearchError = 'No matching South African address was found. Refine the search or select the location on the map.';
      }
    } catch (error) {
      if ((error as Error)?.name !== 'AbortError') {
        this.addressSearchError = 'Addresses cannot be searched because the map service is unavailable. Select the location directly on the map.';
      }
    } finally {
      this.isSearchingAddress = false;
      this.cdr.markForCheck();
    }
  }

  chooseAddress(result: AddressSearchResult): void {
    this.addressSearchQuery = result.displayName;
    this.addressSearchResults = [];
    this.addressSearchError = '';
    this.locationMap?.setView([result.latitude, result.longitude], 17);
    void this.selectMapLocation(result.latitude, result.longitude, result.displayName);
  }

  async selectMapLocation(latitude: number, longitude: number, knownAreaName?: string): Promise<void> {
    this.isResolvingLocation = true;
    this.locationLookupError = '';
    this.locationMarker?.remove();
    this.locationMarker = L.circleMarker([latitude, longitude], {
      radius: 9,
      color: '#ffffff',
      weight: 3,
      fillColor: '#d62828',
      fillOpacity: 1
    }).addTo(this.locationMap!);
    this.createForm.get('incident')?.patchValue({
      latitude: latitude.toFixed(6),
      longitude: longitude.toFixed(6),
      areaName: `Selected location (${latitude.toFixed(4)}, ${longitude.toFixed(4)})`
    });
    if (knownAreaName) {
      this.createForm.get('incident.areaName')?.setValue(knownAreaName.slice(0, 120));
      this.isResolvingLocation = false;
      this.cdr.markForCheck();
      return;
    }
    try {
      const response = await fetch(`https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=${latitude}&lon=${longitude}`, {
        headers: { 'Accept-Language': 'en' }
      });
      if (!response.ok) throw new Error('Reverse geocoding failed');
      const result = await response.json();
      const address = result.address || {};
      const areaName = address.suburb || address.town || address.city || address.village || address.county || result.display_name;
      this.createForm.get('incident.areaName')?.setValue((areaName || '').slice(0, 120));
    } catch {
      this.locationLookupError = 'The map point was saved, but the nearby area name could not be identified because the map lookup service is unavailable.';
    } finally {
      this.isResolvingLocation = false;
      this.cdr.markForCheck();
    }
  }
  
  closeCreateModal(): void {
    this.addressSearchRequest?.abort();
    this.addressSearchQuery = '';
    this.addressSearchResults = [];
    this.addressSearchError = '';
    this.locationMap?.remove();
    this.locationMap = undefined;
    this.locationMarker = undefined;
    this.locationLookupError = '';
    this.mapLoadError = '';
    this.isMapLoading = false;
    this.showCreateModal = false;
    if (this.router.url.split('?')[0] === '/dockets/create') {
      this.router.navigate(['/dockets']);
    }
    this.cdr.markForCheck();
  }

  openEditModal(docket: Docket): void {
    if (!this.canUpdateDocketRecord(docket)) {
      this.toastr.error('This docket is read-only or you do not have permission to update it');
      return;
    }

    this.selectedDocket = docket;
    this.editForm.reset({
      docketSummary: docket.docketSummary || '',
      modusOperandi: docket.modusOperandi || '',
      sensitivityLevel: docket.sensitivityLevel || 'NORMAL',
      reasonForUpdate: ''
    });
    this.showDetailsModal = false;
    this.showEditModal = true;
    this.cdr.markForCheck();
  }

  closeEditModal(): void {
    this.showEditModal = false;
    this.selectedDocket = null;
    this.cdr.markForCheck();
  }

  updateDocket(): void {
    if (this.editForm.invalid || !this.selectedDocket) {
      this.editForm.markAllAsTouched();
      this.toastr.error('Please complete the update details and reason');
      this.cdr.markForCheck();
      return;
    }

    this.isLoading = true;
    this.cdr.markForCheck();

    const updateData: UpdateDocketDto = {
      docketSummary: this.editForm.value.docketSummary,
      modusOperandi: this.editForm.value.modusOperandi || '',
      sensitivityLevel: this.editForm.value.sensitivityLevel,
      reasonForUpdate: this.editForm.value.reasonForUpdate
    };

    this.docketService.updateDocket(this.selectedDocket.docketID, updateData)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toastr.success('Docket updated successfully');
          this.closeEditModal();
          this.loadDockets();
        },
        error: (err) => {
          console.error('Failed to update docket', err);
          this.toastr.error(err.error?.message || 'Failed to update docket');
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }
  
  createDocket(): void {
    if (!this.canCreateDocket) {
      this.toastr.error('Only CSC Officers may create new dockets');
      return;
    }
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      this.toastr.error('The docket cannot be created because one or more required fields are missing or invalid. Review the highlighted fields.');
      this.cdr.markForCheck();
      return;
    }
    
    this.isLoading = true;
    this.cdr.markForCheck();
    
    const formValue = this.createForm.getRawValue();
    const caseType = formValue.caseType === 'Other' ? formValue.otherCaseType.trim() : formValue.caseType;
    const locationType = formValue.incident.locationType === 'Other'
      ? formValue.incident.otherLocationType.trim()
      : formValue.incident.locationType;
    const customIncidentType = formValue.incident.incidentTypeID === 'OTHER'
      ? formValue.incident.otherIncidentType.trim()
      : undefined;
    const createDto: CreateDocketDto = {
      ...formValue,
      caseType,
      incident: {
        ...formValue.incident,
        locationType,
        customIncidentType,
        incidentTypeID: formValue.incident.incidentTypeID === null || formValue.incident.incidentTypeID === ''
          ? undefined
          : formValue.incident.incidentTypeID === 'OTHER' ? undefined : Number(formValue.incident.incidentTypeID)
      }
    };

    this.docketService.createDocket(createDto)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toastr.success('Docket created successfully');
          this.loadDockets();
          this.closeCreateModal();
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: (err) => {
          console.error('Failed to create docket', err);
          this.toastr.error(err.error?.message || 'Failed to create docket');
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }
  
  openAllocateModal(docket: Docket): void {
    if (!this.canAllocateDocket) {
      this.toastr.error('This docket cannot be allocated because your role is not authorised. Only a DSC Supervisor or authorised System Administrator may allocate it.');
      return;
    }
    
    this.selectedDocket = docket;
    this.isReassigning = false;
    this.allocateForm.reset();
    // Refresh the workload list whenever allocation is opened so an officer
    // who has just reached their maximum cannot be selected from stale data.
    this.loadAvailableOfficers();
    this.showAllocateModal = true;
    this.cdr.markForCheck();
  }

  openReassignModal(docket: Docket): void {
    if (!this.canReassignDocket(docket)) {
      this.toastr.error('This docket cannot be reassigned. Rejected assignments may not be reallocated.');
      return;
    }
    this.selectedDocket = docket;
    this.isReassigning = true;
    this.allocateForm.reset();
    this.allocateForm.get('reason')?.setValidators([Validators.required, Validators.minLength(10), Validators.maxLength(500)]);
    this.allocateForm.get('reason')?.updateValueAndValidity();
    this.loadAvailableOfficers();
    this.showAllocateModal = true;
    this.cdr.markForCheck();
  }
  
  closeAllocateModal(): void {
    this.showAllocateModal = false;
    this.isReassigning = false;
    this.selectedDocket = null;
    this.allocateForm.reset();
    this.allocateForm.get('reason')?.clearValidators();
    this.allocateForm.get('reason')?.updateValueAndValidity({ emitEvent: false });
    this.cdr.markForCheck();
  }
  
  allocateDocket(): void {
    if (!this.canAllocateDocket) {
      this.toastr.error('This docket cannot be allocated because your role is not authorised. Only a DSC Supervisor or authorised System Administrator may allocate it.');
      return;
    }
    if (this.allocateForm.invalid || !this.selectedDocket) {
      this.toastr.error('Please select an investigating officer');
      return;
    }

    const selectedOfficer = this.getSelectedOfficer();
    if (!selectedOfficer) {
      this.toastr.error('Select a valid investigating officer before allocating the docket');
      return;
    }

    if (this.isOfficerAtCapacity(selectedOfficer)) {
      this.toastr.error(
        `${selectedOfficer.fullName} cannot receive more dockets because they already have ${selectedOfficer.activeDocketCount} active dockets, which is the configured maximum.`
      );
      return;
    }
    
    this.isLoading = true;
    this.cdr.markForCheck();
    
    const allocation: AllocateDocketDto = {
      investigatingOfficerUserID: this.allocateForm.value.investigatingOfficerUserID
    };
    
    const request$ = this.isReassigning
      ? this.docketService.reassignDocket(this.selectedDocket.docketID, {
          ...allocation,
          reason: this.allocateForm.value.reason
        } as ReassignDocketDto)
      : this.docketService.allocateDocket(this.selectedDocket.docketID, allocation);

    request$
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          const officerName = selectedOfficer.fullName
            || [selectedOfficer.firstName, selectedOfficer.lastName].filter(Boolean).join(' ')
            || 'the selected Investigating Officer';
          this.toastr.success(
            response?.message || `Docket ${this.isReassigning ? 'reassigned' : 'allocated'} to ${officerName}. It is awaiting the officer's response.`,
            this.isReassigning ? 'Reassignment recorded' : 'Allocation recorded'
          );
          this.loadDockets();
          this.loadAvailableOfficers();
          this.closeAllocateModal();
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: (err) => {
          console.error('Failed to allocate docket', err);
          const errorMsg = err.status === 403 
            ? 'This docket cannot be allocated because your role is not authorised to allocate dockets.'
            : (err.error?.message || 'This docket could not be allocated because the server rejected the assignment. The officer may be at capacity or unavailable.');
          this.toastr.error(errorMsg);
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }

  acceptAssignment(docket: Docket): void {
    if (!this.canAcceptAssignment) {
      this.toastr.error('Only the assigned Investigating Officer can accept this assignment');
      return;
    }
    this.isLoading = true;
    this.cdr.markForCheck();

    this.docketService.acceptAssignment(docket.docketID)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toastr.success('Assignment accepted. The 24-hour inspection timer is now active.');
          this.loadDockets();
        },
        error: (err) => {
          this.toastr.error(err.error?.message || 'Failed to accept assignment');
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }

  async rejectAssignment(docket: Docket): Promise<void> {
    if (!this.canAcceptAssignment || docket.status !== 'ALLOCATED') {
      this.toastr.error('This assignment cannot be rejected because it is not currently allocated to your role.');
      return;
    }
    const reason = await this.dialog.open({
      title: 'Cannot Accept Assignment',
      message: `Explain why you cannot accept ${docket.docketNumber}. The allocating supervisor will be notified and the docket will return for allocation.`,
      inputLabel: 'Reason', inputType: 'textarea', required: true, maxLength: 500,
      confirmText: 'Submit rejection', tone: 'danger'
    });
    if (!reason || !reason.trim()) {
      this.toastr.error('Provide a reason so the DSC Supervisor knows why this docket must be reallocated.');
      return;
    }
    this.docketService.rejectAssignment(docket.docketID, reason.trim())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: response => {
          this.toastr.success(response?.message || 'Assignment rejection recorded and the supervisor was notified.');
          this.loadDockets();
        },
        error: err => this.toastr.error(err.error?.message || err.error || 'The assignment rejection could not be recorded.')
      });
  }

  async reviewAllocation(docket: Docket): Promise<void> {
    if (!this.canAllocateDocket) {
      this.toastr.error('You do not have permission to review allocations');
      return;
    }
    const rawDecision = await this.dialog.open({
      title: 'Review Docket Allocation',
      message: `Choose the allocation decision for ${docket.docketNumber}.`,
      inputLabel: 'Decision', inputType: 'select', required: true,
      options: [
        { value: 'APPROVE', label: 'Approve current allocation' },
        { value: 'CHANGE', label: 'Change investigating officer' },
        { value: 'REJECT', label: 'Reject allocation' }
      ], confirmText: 'Continue'
    });
    if (!rawDecision) return;
    const decision = rawDecision.trim().toUpperCase();
    if (!['APPROVE', 'CHANGE', 'REJECT'].includes(decision)) {
      this.toastr.error('Decision must be Approve, Change or Reject');
      return;
    }

    let replacementOfficerID: number | undefined;
    if (decision === 'CHANGE') {
      const rawOfficerID = await this.dialog.open({
        title: 'Select Replacement Officer',
        message: 'Select an available Investigating Officer. Officers at maximum capacity cannot be selected.',
        inputLabel: 'Investigating Officer', inputType: 'select', required: true,
        options: this.availableOfficers.map(officer => ({
          value: String(officer.id),
          label: `${officer.fullName || `${officer.firstName} ${officer.lastName}`} — ${this.getOfficerWorkloadLabel(officer)}`,
          disabled: this.isOfficerAtCapacity(officer)
        })), confirmText: 'Select Officer'
      });
      replacementOfficerID = Number(rawOfficerID);
      if (!replacementOfficerID) {
        this.toastr.error('Select a valid replacement Investigating Officer');
        return;
      }
      const replacementOfficer = this.availableOfficers.find(officer => Number(officer.id) === replacementOfficerID);
      if (this.isOfficerAtCapacity(replacementOfficer)) {
        this.toastr.error(
          `${replacementOfficer.fullName} cannot receive more dockets because they already have ${replacementOfficer.activeDocketCount} active dockets, which is the configured maximum.`
        );
        return;
      }
    }

    const reason = await this.dialog.open({
      title: 'Confirm Allocation Review',
      message: `Record why this allocation is being ${decision.toLowerCase()}d. This reason will be retained in the audit trail.`,
      inputLabel: 'Review reason', inputType: 'textarea', required: true, minLength: 10, maxLength: 500,
      initialValue: 'Allocation reviewed against station workload', confirmText: 'Record Decision'
    });
    if (!reason || reason.trim().length < 10) {
      this.toastr.error('A review reason of at least 10 characters is required');
      return;
    }

    this.docketService.reviewAllocation(docket.docketID, decision, reason.trim(), replacementOfficerID)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toastr.success(`Allocation ${decision.toLowerCase()} decision recorded`);
          this.loadDockets();
        },
        error: err => this.toastr.error(
          err.error?.message
          || (typeof err.error === 'string' ? err.error : '')
          || 'The allocation review could not be recorded. Confirm that a different DSC Supervisor from the docket station is completing the review.'
        )
      });
  }

  openInspectionModal(docket: Docket): void {
    if (!this.canCompleteInspection) {
      this.toastr.error('You do not have permission to complete docket inspections');
      return;
    }
    this.selectedDocket = docket;
    this.inspectionForm.reset({ notes: '' });
    this.showInspectionModal = true;
    this.cdr.markForCheck();
  }

  closeInspectionModal(): void {
    this.showInspectionModal = false;
    this.selectedDocket = null;
    this.cdr.markForCheck();
  }

  completeInspection(): void {
    if (!this.canCompleteInspection) {
      this.toastr.error('You do not have permission to complete docket inspections');
      return;
    }
    if (this.inspectionForm.invalid || !this.selectedDocket) {
      this.inspectionForm.markAllAsTouched();
      this.toastr.error('Inspection notes are required');
      return;
    }

    this.isLoading = true;
    this.docketService.completeInspection(this.selectedDocket.docketID, this.inspectionForm.value.notes)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toastr.success('24-hour inspection marked as completed.');
          this.closeInspectionModal();
          this.loadDockets();
        },
        error: (err) => {
          this.toastr.error(err.error?.message || 'Failed to complete inspection');
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }

  openStatusModal(docket: Docket): void {
    if (!this.canUpdateStatus) {
      this.toastr.error('You do not have permission to change docket status');
      return;
    }
    this.selectedDocket = docket;
    this.statusForm.reset({ status: '', reason: '' });
    this.showStatusModal = true;
    this.cdr.markForCheck();
  }

  closeStatusModal(): void {
    this.showStatusModal = false;
    this.selectedDocket = null;
    this.cdr.markForCheck();
  }

  transitionStatus(): void {
    if (!this.canUpdateStatus) {
      this.toastr.error('You do not have permission to change docket status');
      return;
    }
    if (this.statusForm.invalid || !this.selectedDocket) {
      this.statusForm.markAllAsTouched();
      this.toastr.error('Select a valid status and provide a reason');
      return;
    }

    const newStatus = this.statusForm.value.status;
    if (!this.isValidTransition(this.selectedDocket.status, newStatus)) {
      this.toastr.error(`Cannot transition from ${this.selectedDocket.status} to ${newStatus}`);
      return;
    }

    this.isLoading = true;
    this.docketService.transitionStatus(this.selectedDocket.docketID, newStatus, this.statusForm.value.reason)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toastr.success(`Docket status updated to ${newStatus}.`);
          this.closeStatusModal();
          this.loadDockets();
        },
        error: (err) => {
          this.toastr.error(err.error?.message || 'Failed to update docket status');
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }

  openProsecutorPreview(docket: Docket): void {
    if (!this.canPreviewForProsecutor) {
      this.toastr.error('You do not have permission to prepare the prosecutor preview');
      return;
    }
    this.selectedDocket = docket;
    this.prosecutorPreview = null;
    this.prosecutorPreviewError = '';
    this.showProsecutorPreviewModal = true;
    this.docketService.getProsecutorPreview(docket.docketID)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (preview) => {
          this.prosecutorPreview = preview;
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.prosecutorPreviewError = err.error?.message || 'The prosecutor preview could not be loaded.';
          this.cdr.markForCheck();
        }
      });
  }

  closeProsecutorPreview(): void {
    this.showProsecutorPreviewModal = false;
    this.prosecutorPreview = null;
    this.prosecutorPreviewError = '';
    this.selectedDocket = null;
    this.cdr.markForCheck();
  }
  
  closeDetailsModal(): void {
    this.showDetailsModal = false;
    this.selectedDocket = null;
    this.docketNotes = [];
    this.docketNotesError = '';
    this.router.navigate(['/dockets']);
    this.cdr.markForCheck();
  }
  
  getStatusBadgeClass(status: string): string {
    const statusMap: Record<string, string> = {
      'OPEN': 'status-open',
      'ALLOCATED': 'status-allocated',
      'UNDER_INVESTIGATION': 'status-investigation',
      'PENDING_PROSECUTOR': 'status-pending',
      'COURT_OUTCOME_RECORDED': 'status-pending',
      'CLOSED': 'status-closed',
      'ARCHIVED': 'status-closed',
      'REOPENED': 'status-reopened'
    };
    return statusMap[status] || 'status-default';
  }

  isAwaitingOfficerResponse(docket: Docket): boolean {
    return docket.status === 'ALLOCATED'
      && !docket.assignmentAcceptedAt
      && docket.allocationReviewStatus !== 'Officer Rejected';
  }

  canInitialAllocate(docket: Docket): boolean {
    return this.canAllocateDocket && docket.status === 'OPEN' &&
      !['Officer Rejected', 'Rejected'].includes(docket.allocationReviewStatus || '');
  }

  canReassignDocket(docket: Docket): boolean {
    return this.canAllocateDocket && ['ALLOCATED', 'UNDER_INVESTIGATION'].includes(docket.status) &&
      !['Officer Rejected', 'Rejected'].includes(docket.allocationReviewStatus || '');
  }

  getAllocationDisplayStatus(docket: Docket): string {
    if (docket.allocationReviewStatus === 'Officer Rejected') return 'Rejected by officer';
    if (docket.assignmentAcceptedAt) return 'Accepted';
    if (this.isAwaitingOfficerResponse(docket)) return 'Awaiting response';
    return docket.allocationReviewStatus || 'Not allocated';
  }

  getInspectionStatus(docket: Docket): string {
    if (docket.inspectionCompletedAt) return 'Completed';
    if (!docket.inspectionDueAt) return 'Not started';
    const dueAt = new Date(docket.inspectionDueAt).getTime();
    const reminderWindow = this.runtimePolicies.inspectionReminderHours * 60 * 60 * 1000;
    if (dueAt < Date.now()) return 'Overdue';
    if (dueAt - Date.now() <= reminderWindow) return 'Due Soon';
    return 'On Track';
  }

  getInspectionClass(docket: Docket): string {
    const status = this.getInspectionStatus(docket);
    if (status === 'Completed') return 'status-complete';
    if (status === 'Overdue') return 'status-breach';
    if (status === 'Due Soon') return 'status-pending';
    if (status === 'On Track') return 'status-allocated';
    return 'status-default';
  }

  isHighPriorityByAge(docket: Docket): boolean {
    if (['CLOSED', 'ARCHIVED'].includes(docket.status)) return false;
    return this.getDocketAgeDays(docket) >= this.runtimePolicies.docketPriorityHighDays;
  }

  getDocketAgeDays(docket: Docket): number {
    const createdAt = new Date(docket.dateCreated).getTime();
    const millisecondsPerDay = 24 * 60 * 60 * 1000;
    return Math.max(0, Math.floor((Date.now() - createdAt) / millisecondsPerDay));
  }

  getArchiveAgeDays(docket: Docket): number {
    const closedAt = docket.docketClosedAt ? new Date(docket.docketClosedAt).getTime() : null;
    if (!closedAt) return 0;
    const millisecondsPerDay = 24 * 60 * 60 * 1000;
    return Math.max(0, Math.floor((Date.now() - closedAt) / millisecondsPerDay));
  }

  canArchiveByPolicy(docket: Docket): boolean {
    return docket.status === 'CLOSED' && this.getArchiveAgeDays(docket) >= this.runtimePolicies.archiveEligibleDays;
  }

  getArchiveEligibilityText(docket: Docket): string {
    if (docket.status === 'ARCHIVED') return 'This docket has already been archived';
    if (docket.status !== 'CLOSED') return 'Archive unavailable because the docket must be closed first';
    if (this.canArchiveByPolicy(docket)) {
      return `Archive review eligible (${this.getArchiveAgeDays(docket)} days closed)`;
    }

    const remainingDays = Math.max(0, this.runtimePolicies.archiveEligibleDays - this.getArchiveAgeDays(docket));
    return `Archive after ${remainingDays} more day${remainingDays === 1 ? '' : 's'}`;
  }

  canUpdateDocketRecord(docket: Docket): boolean {
    if (!this.canUpdateDocket || ['CLOSED', 'ARCHIVED'].includes(docket.status)) return false;
    if (this.roleName !== ROLES.cscOfficer) return true;
    return !!docket.createdByUserID && docket.createdByUserID === this.currentUser?.userID;
  }

  hasSecondaryDocketActions(docket: Docket): boolean {
    if (this.isDocketAdministrator) {
      return false;
    }

    return this.canInitialAllocate(docket)
      || this.canReassignDocket(docket)
      || (this.canAcceptAssignment && this.isCurrentOfficerAssignment(docket) && docket.status === 'ALLOCATED' && !docket.assignmentAcceptedAt)
      || (this.canCompleteInspection && !!docket.assignmentAcceptedAt && !docket.inspectionCompletedAt)
      || this.canManageBundles
      || this.canAccessNotes
      || this.canAccessEvidence
      || (this.canCloseOrReopenDocket && docket.status === 'COURT_OUTCOME_RECORDED')
      || (this.canCloseOrReopenDocket && ['CLOSED', 'ARCHIVED'].includes(docket.status))
      || (this.canRecordCourtOutcome && docket.status === 'PENDING_PROSECUTOR');
  }

  isValidTransition(from: string, to: string): boolean {
    const allowed: Record<string, string[]> = {
      UNDER_INVESTIGATION: ['PENDING_PROSECUTOR'],
      PENDING_PROSECUTOR: ['UNDER_INVESTIGATION'],
      COURT_OUTCOME_RECORDED: ['UNDER_INVESTIGATION'],
      REOPENED: ['UNDER_INVESTIGATION', 'PENDING_PROSECUTOR']
    };
    return (allowed[from] || []).includes(to);
  }

  hasManualTransition(docket: Docket): boolean {
    return this.canUpdateStatus && this.transitionOptions.some(status => this.isValidTransition(docket.status, status));
  }

  private buildTransitionOptions(statuses: string[]): string[] {
    return statuses.filter(status => !['OPEN', 'ALLOCATED', 'CLOSED', 'ARCHIVED'].includes(status));
  }

  private normaliseStatuses(statuses: string[] | null | undefined): string[] {
    return this.normaliseStrings(statuses, DEFAULT_DOCKET_STATUSES);
  }

  private normaliseStrings(values: string[] | null | undefined, fallback: string[]): string[] {
    const normalised = Array.from(new Set(
      (values || [])
        .map(value => value?.trim())
        .filter((value): value is string => !!value)
    ));

    return (normalised.length ? normalised : [...fallback]).sort((a, b) => a.localeCompare(b));
  }

  viewDocketDetails(docket: Docket): void {
    this.router.navigate(['/dockets', docket.docketID]);
  }
  
  retry(): void {
    this.loadDockets();
  }

  openBundleModal(docket: Docket): void {
    if (!this.canManageBundles) {
      this.toastr.error('You do not have permission to view prosecutor bundles');
      return;
    }
    this.selectedDocket = docket;
    this.showBundleModal = true;
    this.bundleTitle = `${this.getDocketSubjectLabel(docket)} Prosecutor Bundle`;
    this.bundleNotes = '';
    this.aiSummary = '';
    this.aiSummaryConfirmed = false;
    this.aiExtractionText = '';
    this.relatedCases = [];
    this.relatedCasesReviewText = '';
    this.relatedCaseReviewItems = [];
    this.showRelatedCasesTechnicalData = false;
    this.relatedCasesConfirmed = false;
    this.readinessIntelligence = null;
    this.readinessLoading = false;
    this.selectedBundleForPrivacy = null;
    this.privacyHistory = [];
    this.loadProsecutorBundles();
    if (this.canViewPrivacyHistory) this.loadPrivacyHistory();
    this.cdr.markForCheck();
  }

  closeBundleModal(options?: { preserveConfirmationState?: boolean }): void {
    const preserveConfirmationState = options?.preserveConfirmationState ?? false;
    this.showBundleModal = false;
    this.selectedDocket = null;
    this.prosecutorBundles = [];
    this.selectedBundleForPrivacy = null;
    this.aiSummary = '';
    if (!preserveConfirmationState) {
      this.aiSummaryConfirmed = false;
    }
    this.aiExtractionText = '';
    this.relatedCases = [];
    this.relatedCasesReviewText = '';
    this.relatedCaseReviewItems = [];
    this.showRelatedCasesTechnicalData = false;
    this.readinessIntelligence = null;
    this.readinessLoading = false;
    if (!preserveConfirmationState) {
      this.relatedCasesConfirmed = false;
    }
    this.showBundleViewModal = false;
    this.showBundleEditModal = false;
    this.selectedBundleDetails = null;
    this.cdr.markForCheck();
  }

  private closeDocketActionViews(): void {
    this.showDetailsModal = false;
    this.showBundleModal = false;
    this.showProsecutorPreviewModal = false;
    this.selectedBundleForPrivacy = null;
    this.prosecutorBundles = [];
    this.showBundleViewModal = false;
    this.showBundleEditModal = false;
    this.selectedBundleDetails = null;
    this.docketNotes = [];
    this.docketNotesError = '';
    this.aiSummary = '';
    this.aiSummaryConfirmed = false;
    this.aiExtractionText = '';
    this.relatedCases = [];
    this.relatedCasesReviewText = '';
    this.relatedCaseReviewItems = [];
    this.showRelatedCasesTechnicalData = false;
    this.relatedCasesConfirmed = false;
    this.cdr.markForCheck();
  }

  loadProsecutorBundles(): void {
    if (!this.selectedDocket) return;
    this.docketService.getProsecutorBundles(this.selectedDocket.docketID)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: bundles => {
          this.prosecutorBundles = bundles || [];
          this.cdr.markForCheck();
        },
        error: err => this.toastr.error(err.error?.message || 'Failed to load prosecutor bundles')
      });
  }

  createProsecutorBundle(): void {
    if (!this.selectedDocket || !this.canCreateBundle) {
      this.toastr.error('You do not have permission to create prosecutor bundles');
      return;
    }
    this.docketService.createProsecutorBundle(this.selectedDocket.docketID, {
      title: this.bundleTitle.trim() || `${this.getDocketSubjectLabel(this.selectedDocket)} Prosecutor Bundle`,
      notes: this.bundleNotes
    }).pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toastr.success('Prosecutor bundle created');
          this.closeBundleModal();
          this.loadProsecutorBundles();
          this.loadDockets();
        },
        error: err => this.toastr.error(err.error?.message || 'Failed to create bundle')
      });
  }

  updateBundle(bundle: any): void {
    this.openBundleEdit(bundle);
  }

  openBundleView(bundle: any): void {
    const bundleId = bundle.bundleID || bundle.bundleId;
    this.docketService.getProsecutorBundle(bundleId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: details => {
          this.selectedBundleDetails = details;
          this.bundleViewTab = this.getBundleItems(details, 'C-clip').length ? 'notes' : 'evidence';
          this.showBundleViewModal = true;
          this.cdr.markForCheck();
        },
        error: err => this.toastr.error(err.error?.message || 'Failed to load bundle details')
      });
  }

  closeBundleViewModal(): void {
    this.showBundleViewModal = false;
    this.selectedBundleDetails = null;
    this.cdr.markForCheck();
  }

  openBundleEdit(bundle: any): void {
    if (!this.canEditBundles) {
      this.toastr.error('You do not have permission to edit prosecutor bundles');
      return;
    }
    if (bundle.status !== 'Draft') {
      this.toastr.error('Only draft bundles can be edited. This bundle has already been submitted for approval.');
      return;
    }
    const bundleId = bundle.bundleID || bundle.bundleId;
    this.docketService.getProsecutorBundle(bundleId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: details => {
          this.selectedBundleDetails = details;
          this.bundleEditForm.reset({
            title: details.title || '',
            notes: details.notes || '',
            aiSummaryText: details.aiSummaryText || '',
            includeNotes: this.getBundleItems(details, 'C-clip').length > 0,
            includeEvidence: this.getBundleItems(details, 'B-clip').length > 0,
            reason: ''
          });
          this.showBundleEditModal = true;
          this.cdr.markForCheck();
        },
        error: err => this.toastr.error(err.error?.message || 'Failed to load bundle edit form')
      });
  }

  closeBundleEditModal(): void {
    this.showBundleEditModal = false;
    this.selectedBundleDetails = null;
    this.bundleEditForm.reset({
      title: '',
      notes: '',
      aiSummaryText: '',
      includeNotes: true,
      includeEvidence: true,
      reason: ''
    });
    this.cdr.markForCheck();
  }

  saveBundleChanges(): void {
    if (!this.selectedBundleDetails) return;
    if (this.bundleEditForm.invalid) {
      this.bundleEditForm.markAllAsTouched();
      this.toastr.error('Please complete the edit bundle form.');
      return;
    }
    const formValue = this.bundleEditForm.getRawValue();
    if (!formValue.includeNotes && !formValue.includeEvidence) {
      this.toastr.error('A bundle must include at least one item.');
      return;
    }

    this.docketService.updateProsecutorBundle(this.selectedBundleDetails.bundleID || this.selectedBundleDetails.bundleId, {
      title: formValue.title.trim(),
      notes: formValue.notes?.trim() || '',
      aiSummaryText: formValue.aiSummaryText?.trim() || '',
      includeNotes: !!formValue.includeNotes,
      includeEvidence: !!formValue.includeEvidence,
      reason: formValue.reason.trim()
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toastr.success('Bundle updated successfully.');
        this.closeBundleEditModal();
        this.loadProsecutorBundles();
        this.loadDockets();
      },
      error: err => this.toastr.error(err.error?.message || 'Failed to update bundle')
    });
  }

  manageBundlePrivacy(bundle: any): void {
    if (!this.canPrivacyFlag) {
      this.toastr.error('You do not have permission to view bundle privacy flags');
      return;
    }
    const bundleId = bundle.bundleID || bundle.bundleId;
    this.docketService.getProsecutorBundle(bundleId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: details => {
          this.selectedBundleForPrivacy = details;
          this.cdr.markForCheck();
        },
        error: err => this.toastr.error(err.error?.message || 'Failed to load bundle privacy fields')
      });
  }

  async toggleBundleItemPrivacy(item: any): Promise<void> {
    if (!this.selectedBundleForPrivacy) return;
    const bundleId = this.selectedBundleForPrivacy.bundleID || this.selectedBundleForPrivacy.bundleId;
    const itemId = item.itemID || item.itemId;
    const makePrivate = !item.isPrivate;

    if (makePrivate && !this.canCreateBundlePrivacyFlag) {
      this.toastr.error('Only an Investigating Officer can create a new privacy flag.');
      return;
    }

    if (!makePrivate && !this.canManageBundlePrivacyFlag) {
      this.toastr.error('Only a DSC Supervisor can manage or remove an active privacy flag.');
      return;
    }

    const promptLabel = makePrivate
      ? 'Flag reason for this bundle item'
      : 'Reason for removing this privacy flag';
    const defaultReason = makePrivate
      ? 'Contains sensitive information'
      : 'Disclosure authorised after privacy review';
    const reason = await this.dialog.open({
      title: makePrivate ? 'Flag Bundle Item as Private' : 'Remove Bundle Privacy Flag',
      message: makePrivate ? 'The selected item will be redacted from prosecutor previews and exports.' : 'The selected item will become visible in prosecutor disclosures.',
      inputLabel: promptLabel, inputType: 'textarea', required: true, minLength: 3, maxLength: 300,
      initialValue: defaultReason, confirmText: makePrivate ? 'Apply Privacy Flag' : 'Remove Privacy Flag', tone: makePrivate ? 'danger' : 'primary'
    });
    if (!reason || !reason.trim()) {
      this.toastr.error('A privacy flag reason is required');
      return;
    }
    if (reason.trim().length > 300) {
      this.toastr.error('Privacy flag reason must not exceed 300 characters');
      return;
    }

    this.docketService.setBundleItemPrivacyFlag(bundleId, itemId, makePrivate, reason.trim())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toastr.success(makePrivate ? 'Privacy flag applied successfully' : 'Privacy flag removed successfully');
          this.manageBundlePrivacy(this.selectedBundleForPrivacy);
          this.loadPrivacyHistory();
          this.loadDockets();
        },
        error: err => this.toastr.error(err.error?.message || 'Failed to update the bundle privacy field')
      });
  }

  viewBundleItemPrivacyHistory(item: any): void {
    if (!this.canReadBundlePrivacyFlagHistory) {
      this.toastr.error('You do not have permission to view privacy flag history.');
      return;
    }

    const itemId = item.itemID || item.itemId;
    this.selectedBundleItemHistory = item;
    this.bundleItemPrivacyHistory = [];
    this.docketService.getBundleItemPrivacyFlagHistory(itemId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: response => {
          this.selectedBundleItemHistory = {
            ...item,
            name: response?.itemName || item.name,
            itemID: response?.itemId || itemId
          };
          this.bundleItemPrivacyHistory = response?.history || [];
          this.cdr.markForCheck();
        },
        error: err => {
          this.bundleItemPrivacyHistory = [];
          this.toastr.error(err.error?.message || 'Failed to load privacy flag history');
          this.cdr.markForCheck();
        }
      });
  }

  getBundlePrivacyActionLabel(item: any): string {
    return item?.isPrivate ? 'Manage Flag' : 'Flag as Private';
  }

  canActOnBundlePrivacyItem(item: any): boolean {
    return item?.isPrivate ? this.canManageBundlePrivacyFlag : this.canCreateBundlePrivacyFlag;
  }

  exportBundle(bundle: any, format: 'PDF' | 'ZIP' = 'PDF'): void {
    if (!this.canExportBundle) {
      this.toastr.error('You do not have permission to export prosecutor bundles');
      return;
    }
    const bundleId = bundle.bundleID || bundle.bundleId;
    this.docketService.exportProsecutorBundle(bundleId, format)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.docketService.downloadProsecutorBundle(bundleId, format)
            .pipe(takeUntil(this.destroy$))
            .subscribe({
              next: blob => {
                const objectUrl = URL.createObjectURL(blob);
                const anchor = document.createElement('a');
                const safeTitle = getDocketFileStem(this.selectedDocket || {
                  docketID: Number(bundle.fk_DocketID ?? bundle.fK_DocketID ?? bundle.docketID ?? 0),
                  docketNumber: String(bundle.docketNumber ?? `Docket ${bundleId}`),
                  complainantFullName: String(bundle.complainantFullName ?? '')
                } as Docket);
                anchor.href = objectUrl;
                anchor.download = `${safeTitle} - Prosecutor Bundle.${format.toLowerCase()}`;
                anchor.style.display = 'none';
                document.body.appendChild(anchor);
                anchor.click();
                anchor.remove();
                URL.revokeObjectURL(objectUrl);
                this.toastr.success(`Prosecutor bundle exported as ${format}`);
                this.closeDocketActionViews();
                this.loadProsecutorBundles();
                this.loadDockets();
              },
              error: err => this.toastr.error(err.error?.message || 'Bundle was exported but could not be downloaded')
            });
        },
        error: err => this.toastr.error(err.error?.message || 'Failed to export bundle')
      });
  }

  openShareBundle(bundle: any): void {
    if (!this.selectedDocket || !this.canShareBundle || bundle?.status !== 'Draft') {
      this.toastr.error('Only a DSC Supervisor can share a Draft prosecutor bundle.');
      return;
    }
    const bundleId = bundle.bundleID || bundle.bundleId;
    this.shareBundleCandidate = bundle;
    this.showBundleModal = false;
    this.sharePreview = null;
    this.eligibleProsecutors = [];
    this.selectedProsecutorUserID = null;
    this.shareNote = '';
    this.shareWorkflowError = '';
    this.isLoadingSharePreview = true;
    this.cdr.markForCheck();

    this.docketService.previewBundleShare(bundleId).pipe(takeUntil(this.destroy$)).subscribe({
      next: preview => {
        this.sharePreview = preview;
        this.isLoadingSharePreview = false;
        this.cdr.markForCheck();
      },
      error: err => {
        this.isLoadingSharePreview = false;
        this.shareWorkflowError = this.getApiMessage(err, 'The disclosure preview could not be prepared. Confirm that the API has been restarted with the latest prosecutor-bundle endpoints.');
        this.toastr.error(this.shareWorkflowError);
        this.cdr.markForCheck();
      }
    });

    this.docketService.getEligibleProsecutors(this.selectedDocket.docketID)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: prosecutors => {
          this.eligibleProsecutors = Array.isArray(prosecutors) ? prosecutors : [];
          this.cdr.markForCheck();
        },
        error: err => {
          this.shareWorkflowError = this.getApiMessage(err, 'The prosecutor list could not be loaded. Confirm that an active Prosecutor exists at this docket station.');
          this.toastr.error(this.shareWorkflowError);
          this.cdr.markForCheck();
        }
      });
  }

  closeShareBundle(): void {
    this.shareBundleCandidate = null;
    this.sharePreview = null;
    this.eligibleProsecutors = [];
    this.selectedProsecutorUserID = null;
    this.shareNote = '';
    this.isSharingBundle = false;
    this.shareWorkflowError = '';
    this.isLoadingSharePreview = false;
    this.showBundleModal = !!this.selectedDocket;
    this.cdr.markForCheck();
  }

  private getApiMessage(error: any, fallback: string): string {
    const payload = error?.error;
    if (typeof payload === 'string' && payload.trim()) return payload;
    if (typeof payload?.message === 'string' && payload.message.trim()) return payload.message;
    if (typeof payload?.title === 'string' && payload.title.trim()) return `${payload.title} ${fallback}`;
    return fallback;
  }

  confirmShareBundle(): void {
    if (!this.shareBundleCandidate || !this.selectedProsecutorUserID || this.isSharingBundle) {
      this.toastr.error('Select the prosecutor who must receive this bundle.');
      return;
    }
    this.isSharingBundle = true;
    const bundleId = this.shareBundleCandidate.bundleID || this.shareBundleCandidate.bundleId;
    this.docketService.shareProsecutorBundle(bundleId, this.selectedProsecutorUserID, this.shareNote.trim())
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: result => {
          this.toastr.success(result?.message || `Prosecutor bundle has been shared successfully with ${result?.recipientName || 'the selected prosecutor'}.`);
          this.closeShareBundle();
          this.loadProsecutorBundles();
          this.loadDockets();
        },
        error: err => {
          this.isSharingBundle = false;
          this.shareWorkflowError = this.getApiMessage(err, 'The bundle could not be shared. Check the selected prosecutor and bundle status.');
          this.toastr.error(this.shareWorkflowError);
          this.cdr.markForCheck();
        }
      });
  }

  downloadSharedBundle(bundle: any, format: 'PDF' | 'ZIP' = 'PDF'): void {
    if (!this.canDownloadSharedBundle || bundle?.status !== 'Shared') {
      this.toastr.error('This bundle cannot be downloaded because it has not been shared with you.');
      return;
    }
    const bundleId = bundle.bundleID || bundle.bundleId;
    this.docketService.downloadProsecutorBundle(bundleId, format).pipe(takeUntil(this.destroy$)).subscribe({
      next: blob => {
        const objectUrl = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = objectUrl;
        anchor.download = `${getDocketFileStem(this.selectedDocket!)} - Prosecutor Bundle.${format.toLowerCase()}`;
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
        URL.revokeObjectURL(objectUrl);
        this.toastr.success(`Shared prosecutor bundle downloaded as ${format}.`);
      },
      error: err => this.toastr.error(err.error?.message || 'The shared bundle could not be downloaded.')
    });
  }

  async setPrivacyFlag(isPrivate: boolean): Promise<void> {
    if (!this.selectedDocket || !this.canPrivacyFlag) {
      this.toastr.error('You do not have permission to change docket privacy');
      return;
    }
    const reason = await this.dialog.open({
      title: isPrivate ? 'Apply Docket Privacy Flag' : 'Remove Docket Privacy Flag',
      message: isPrivate ? 'Sensitive docket information will be redacted from prosecutor disclosures.' : 'Confirm that disclosure has been authorised before removing this protection.',
      inputLabel: 'Reason', inputType: 'textarea', required: true, minLength: 3, maxLength: 300,
      initialValue: 'Privacy review completed', confirmText: isPrivate ? 'Apply Flag' : 'Remove Flag', tone: isPrivate ? 'danger' : 'primary'
    });
    if (!reason || reason.trim().length < 3) {
      this.toastr.error('A reason is required');
      return;
    }
    this.docketService.setPrivacyFlag(this.selectedDocket.docketID, isPrivate, reason.trim())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toastr.success(isPrivate ? 'Privacy flag applied' : 'Privacy flag removed');
          this.loadPrivacyHistory();
          this.closeBundleModal();
          this.loadDockets();
        },
        error: err => this.toastr.error(err.error?.message || 'Failed to update privacy flag')
      });
  }

  loadPrivacyHistory(): void {
    if (!this.selectedDocket || !this.canViewPrivacyHistory) return;
    this.docketService.getPrivacyFlagHistory(this.selectedDocket.docketID)
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: history => this.privacyHistory = history || [], error: () => this.privacyHistory = [] });
  }

  getBundleItems(bundle: any, section: 'B-clip' | 'C-clip'): any[] {
    return (bundle?.manifest || [])
      .filter((item: any) => item.section === section)
      .sort((left: any, right: any) => Number(left.order) - Number(right.order));
  }

  generateAiSummary(): void {
    if (!this.selectedDocket || !this.canRunAi) return;
    this.docketService.generateAiSummary(this.selectedDocket.docketID)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: result => {
          this.aiSummary = result.summary || '';
          this.aiSummaryConfirmed = false;
          this.toastr.info('AI summary generated. Review, edit and confirm it before use.');
          this.cdr.markForCheck();
        },
        error: err => this.toastr.error(err.error?.message || 'Failed to generate AI summary')
      });
  }

  confirmAiSummary(): void {
    if (!this.selectedDocket || this.aiSummary.trim().length < 10) {
      this.toastr.error('Review and provide a summary of at least 10 characters');
      return;
    }

    this.docketService.confirmAiSummary(this.selectedDocket.docketID, this.aiSummary.trim())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.aiSummaryConfirmed = true;
          this.toastr.success('Reviewed AI summary confirmed and audited');
          this.closeBundleModal({ preserveConfirmationState: true });
          this.cdr.markForCheck();
        },
        error: err => this.toastr.error(err.error?.message || 'Failed to confirm the reviewed summary')
      });
  }

  extractAiData(): void {
    if (!this.selectedDocket) return;
    this.docketService.extractAiData(this.selectedDocket.docketID)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: result => {
          const extraction = result?.extraction;
          if (!extraction || Array.isArray(extraction) || typeof extraction !== 'object') {
            this.aiExtractionText = '';
            this.aiExtractionFields = [];
            this.toastr.error('The extracted data cannot be reviewed because the AI service returned an invalid response');
            this.cdr.markForCheck();
            return;
          }
          this.aiExtractionText = JSON.stringify(extraction, null, 2);
          this.aiExtractionFields = this.buildAiExtractionFields(extraction as Record<string, unknown>);
          this.showAiTechnicalData = false;
          this.toastr.info('Review and correct the extracted fields before verifying');
          this.cdr.markForCheck();
        },
        error: err => this.toastr.error(err.error?.message || 'Failed to extract docket data')
      });
  }

  verifyAiExtraction(): void {
    if (!this.selectedDocket || !this.aiExtractionFields.length) {
      this.toastr.error('Generate and review the extracted data first');
      return;
    }

    const verifiedValues = this.buildVerifiedExtraction();
    if (!verifiedValues) {
      this.toastr.error('Correct the highlighted extracted fields before verification');
      this.cdr.markForCheck();
      return;
    }

    this.docketService.verifyAiExtraction(this.selectedDocket.docketID, {
      ...verifiedValues,
      verifiedAt: new Date().toISOString()
    }).pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toastr.success('Extracted data verified and saved');
          this.aiExtractionText = '';
          this.aiExtractionFields = [];
          this.showAiTechnicalData = false;
          this.closeBundleModal();
          this.cdr.markForCheck();
        },
        error: err => this.toastr.error(err.error?.message || 'Failed to verify extracted data')
      });
  }

  getAiExtractionFields(section: string): AiExtractionReviewField[] {
    return this.aiExtractionFields.filter(field => field.section === section);
  }

  get aiTechnicalJson(): string {
    const values = this.buildVerifiedExtraction(false) || {};
    return JSON.stringify(values, null, 2);
  }

  private buildAiExtractionFields(extraction: Record<string, unknown>): AiExtractionReviewField[] {
    const configuration: Record<string, Partial<AiExtractionReviewField>> = {
      docketNumber: { label: 'Docket Number', section: 'Case Identification', readOnly: true },
      obReferenceNumber: { label: 'OB Reference Number', section: 'Case Identification', readOnly: true },
      status: { label: 'Docket Status', section: 'Case Identification' },
      station: { label: 'Police Station', section: 'Case Identification' },
      caseType: { label: 'Case Type', section: 'Incident Details' },
      incidentType: { label: 'Crime / Incident Type', section: 'Incident Details' },
      occurrenceDate: { label: 'Occurrence Date', section: 'Incident Details', inputType: 'datetime-local' },
      area: { label: 'Area', section: 'Incident Details' },
      locationType: { label: 'Location Type', section: 'Incident Details' },
      incidentDescription: { label: 'Incident Description', section: 'Narrative', inputType: 'textarea' },
      docketSummary: { label: 'Docket Summary', section: 'Narrative', inputType: 'textarea' },
      modusOperandi: { label: 'Modus Operandi', section: 'Narrative', inputType: 'textarea' },
      evidenceItemCount: { label: 'Evidence Items', section: 'Case Information', inputType: 'number' },
      noteCount: { label: 'Investigation Notes', section: 'Case Information', inputType: 'number' }
    };
    const configuredOrder = Object.keys(configuration);

    return Object.entries(extraction)
      .sort(([left], [right]) => {
        const leftIndex = configuredOrder.indexOf(left);
        const rightIndex = configuredOrder.indexOf(right);
        return (leftIndex < 0 ? Number.MAX_SAFE_INTEGER : leftIndex) - (rightIndex < 0 ? Number.MAX_SAFE_INTEGER : rightIndex);
      })
      .map(([key, rawValue]) => {
        const config = configuration[key] || {};
        const originalType = rawValue === null ? 'null' : Array.isArray(rawValue) || typeof rawValue === 'object'
          ? 'object'
          : typeof rawValue as 'string' | 'number' | 'boolean';
        let value: string | number = originalType === 'object' ? JSON.stringify(rawValue, null, 2) : (rawValue as string | number ?? '');
        if (config.inputType === 'datetime-local' && typeof value === 'string') {
          value = value.match(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/)?.[0] || value;
        }
        return {
          key,
          label: config.label || this.humanizeAiFieldName(key),
          section: config.section || 'Additional Extracted Data',
          inputType: config.inputType || (originalType === 'number' ? 'number' : originalType === 'object' ? 'textarea' : 'text'),
          value,
          originalType,
          readOnly: config.readOnly || false,
          error: ''
        };
      });
  }

  private buildVerifiedExtraction(validate = true): Record<string, unknown> | null {
    const values: Record<string, unknown> = {};
    let invalid = false;
    for (const field of this.aiExtractionFields) {
      field.error = '';
      const stringValue = String(field.value ?? '').trim();
      if (validate && !stringValue && field.originalType !== 'null') {
        field.error = `${field.label} cannot be blank.`;
        invalid = true;
        continue;
      }
      if (field.inputType === 'number') {
        const numericValue = Number(field.value);
        if (validate && (!Number.isSafeInteger(numericValue) || numericValue < 0)) {
          field.error = `${field.label} must be a whole number of zero or more.`;
          invalid = true;
          continue;
        }
        values[field.key] = numericValue;
      } else if (field.originalType === 'boolean') {
        values[field.key] = stringValue.toLowerCase() === 'true';
      } else if (field.originalType === 'object') {
        try {
          values[field.key] = JSON.parse(stringValue);
        } catch {
          if (validate) {
            field.error = `${field.label} contains invalid structured data.`;
            invalid = true;
          }
          values[field.key] = stringValue;
        }
      } else if (field.originalType === 'null' && !stringValue) {
        values[field.key] = null;
      } else {
        values[field.key] = stringValue;
      }
    }
    return invalid ? null : values;
  }

  private humanizeAiFieldName(key: string): string {
    return key
      .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
      .replace(/[_-]+/g, ' ')
      .replace(/^./, character => character.toUpperCase());
  }

  suggestRelatedCases(): void {
    if (!this.selectedDocket) return;
    this.docketService.suggestRelatedCases(this.selectedDocket.docketID)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: suggestions => {
          this.relatedCases = Array.isArray(suggestions) ? suggestions : [];
          this.relatedCaseReviewItems = this.relatedCases
            .filter(item => item && Number.isInteger(Number(item.relatedDocketId)))
            .map(item => ({
              relatedDocketId: Number(item.relatedDocketId),
              score: Number(item.score),
              reason: String(item.reason || ''),
              included: true,
              scoreError: '',
              reasonError: ''
            }));
          this.relatedCasesReviewText = this.relatedCaseReviewItems.length ? JSON.stringify(this.relatedCases, null, 2) : '';
          this.showRelatedCasesTechnicalData = false;
          this.relatedCasesConfirmed = false;
          if (this.relatedCaseReviewItems.length) {
            this.toastr.info('Review and correct the related-case suggestions before confirming them');
          } else {
            this.toastr.info('No related cases met the similarity threshold');
          }
          this.cdr.markForCheck();
        },
        error: err => this.toastr.error(err.error?.message || 'Failed to load related case suggestions')
      });
  }

  generateReadinessIntelligence(): void {
    if (!this.selectedDocket || this.readinessLoading) return;
    this.readinessLoading = true;
    this.docketService.generateReadinessIntelligence(this.selectedDocket.docketID)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: intelligence => {
          this.readinessIntelligence = intelligence;
          this.readinessLoading = false;
          this.toastr.success('Explainable case-readiness assessment generated and audited');
          this.cdr.markForCheck();
        },
        error: error => {
          this.readinessLoading = false;
          this.toastr.error(error.error?.message || 'Case-readiness intelligence could not be generated');
          this.cdr.markForCheck();
        }
      });
  }

  confirmRelatedCases(): void {
    if (!this.selectedDocket || !this.relatedCaseReviewItems.length) {
      this.toastr.error('Generate and review related-case suggestions first');
      return;
    }

    const included = this.relatedCaseReviewItems.filter(item => item.included);
    let invalid = !included.length;
    for (const item of this.relatedCaseReviewItems) {
      item.scoreError = '';
      item.reasonError = '';
      if (!item.included) continue;
      if (!Number.isFinite(Number(item.score)) || Number(item.score) < 0 || Number(item.score) > 1) {
        item.scoreError = 'Similarity score must be between 0 and 1.';
        invalid = true;
      }
      if (item.reason.trim().length < 3 || item.reason.trim().length > 500) {
        item.reasonError = 'Reason must contain between 3 and 500 characters.';
        invalid = true;
      }
      if (item.relatedDocketId === this.selectedDocket.docketID) {
        item.reasonError = 'A docket cannot be related to itself.';
        invalid = true;
      }
    }
    if (invalid) {
      this.toastr.error(included.length ? 'Correct the highlighted related-case fields before confirming' : 'Select at least one related case to confirm');
      this.cdr.markForCheck();
      return;
    }

    const reviewed = included.map(item => ({
      relatedDocketId: item.relatedDocketId,
      score: Number(item.score),
      reason: item.reason.trim()
    }));

    this.docketService.confirmRelatedCases(this.selectedDocket.docketID, reviewed)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.relatedCases = reviewed;
          this.relatedCasesConfirmed = true;
          this.toastr.success('Reviewed related cases confirmed and audited');
          this.closeBundleModal({ preserveConfirmationState: true });
          this.cdr.markForCheck();
        },
        error: err => this.toastr.error(err.error?.message || 'Failed to confirm the reviewed related cases')
      });
  }

  get relatedCasesTechnicalJson(): string {
    return JSON.stringify(this.relatedCaseReviewItems.filter(item => item.included).map(item => ({
      relatedDocketId: item.relatedDocketId,
      score: Number(item.score),
      reason: item.reason.trim()
    })), null, 2);
  }

  getRelatedCaseScorePercent(item: RelatedCaseReviewItem): number {
    const score = Number(item.score);
    return Number.isFinite(score) ? Math.round(Math.max(0, Math.min(1, score)) * 100) : 0;
  }

  async closeCase(docket: Docket): Promise<void> {
    if (!this.canCloseOrReopenDocket) {
      this.toastr.error('You do not have permission to close this case');
      return;
    }
    const reason = await this.dialog.open({
      title: 'Close Docket',
      message: `You are about to close docket ${docket.docketNumber}. Closed dockets cannot be edited unless an authorised supervisor reopens them.`,
      inputLabel: 'Reason for closing', inputType: 'textarea', required: true, minLength: 10, maxLength: 500,
      initialValue: 'Court outcome completed', confirmText: 'Close Docket', tone: 'danger'
    });
    if (!reason || reason.trim().length < 10) {
      this.toastr.error('A reason of at least 10 characters is required');
      return;
    }
    this.docketService.closeCase(docket.docketID, reason.trim())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toastr.success('Case closed');
          this.closeDocketActionViews();
          this.loadDockets();
        },
        error: err => this.toastr.error(err.error?.message || 'Failed to close case')
      });
  }

  async reopenCase(docket: Docket): Promise<void> {
    if (!this.canCloseOrReopenDocket) {
      this.toastr.error('You do not have permission to reopen this case');
      return;
    }
    const reason = await this.dialog.open({
      title: 'Reopen Docket',
      message: `Reopening docket ${docket.docketNumber} restores it to active operational processing.`,
      inputLabel: 'Reason for reopening', inputType: 'textarea', required: true, minLength: 10, maxLength: 500,
      initialValue: 'New information received', confirmText: 'Reopen Docket'
    });
    if (!reason || reason.trim().length < 10) {
      this.toastr.error('A reason of at least 10 characters is required');
      return;
    }
    this.docketService.reopenCase(docket.docketID, reason.trim())
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toastr.success('Case reopened');
          this.closeDocketActionViews();
          this.loadDockets();
        },
        error: err => this.toastr.error(err.error?.message || 'Failed to reopen case')
      });
  }

  // UC 3.4 — Archive Docket
  openArchiveModal(docket: Docket): void {
    if (!this.canArchiveDocket) {
      this.toastr.error('You do not have permission to archive dockets');
      return;
    }
    if (!this.canArchiveByPolicy(docket)) {
      this.toastr.warning(this.getArchiveEligibilityText(docket));
      return;
    }
    this.selectedDocket = docket;
    this.showArchiveModal = true;
    this.cdr.markForCheck();
  }

  closeArchiveModal(): void {
    this.showArchiveModal = false;
    this.selectedDocket = null;
    this.cdr.markForCheck();
  }

  confirmArchiveDocket(): void {
    if (!this.selectedDocket || !this.canArchiveDocket) {
      this.toastr.error('You do not have permission to archive dockets');
      return;
    }
    if (!this.canArchiveByPolicy(this.selectedDocket)) {
      this.toastr.warning(this.getArchiveEligibilityText(this.selectedDocket));
      return;
    }
    this.isLoading = true;
    this.cdr.markForCheck();
    this.docketService.archiveDocket(this.selectedDocket.docketID)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toastr.success('Docket archived successfully');
          this.closeArchiveModal();
          this.loadDockets();
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.toastr.error(err.error?.message || 'Failed to archive docket');
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }

  // UC 8.1 — Record Court Outcome
  openCourtOutcomeModal(docket: Docket): void {
    if (!this.canRecordCourtOutcome) {
      this.toastr.error('You do not have permission to record court outcomes');
      return;
    }
    this.selectedDocket = docket;
    this.courtOutcomeForm.reset();
    this.showCourtOutcomeModal = true;
    this.cdr.markForCheck();
  }

  closeCourtOutcomeModal(): void {
    this.showCourtOutcomeModal = false;
    this.selectedDocket = null;
    this.cdr.markForCheck();
  }

  submitCourtOutcome(): void {
    if (!this.canRecordCourtOutcome) {
      this.toastr.error('You do not have permission to record court outcomes');
      return;
    }
    if (this.courtOutcomeForm.invalid || !this.selectedDocket) {
      this.toastr.error('Please complete all required fields');
      return;
    }
    const v = this.courtOutcomeForm.value;
    if (v.courtDate < this.toLocalDateInputValue(new Date(this.selectedDocket.dateCreated)) || v.courtDate > this.today) {
      this.toastr.error('Court date must be between the docket creation date and today');
      return;
    }
    if (v.outcome === 'Guilty' && !v.sentence?.trim()) {
      this.toastr.error('Sentence details are required for a Guilty outcome');
      return;
    }
    this.isLoading = true;
    this.cdr.markForCheck();
    this.docketService.recordCourtOutcome(this.selectedDocket.docketID, {
      outcome: v.outcome,
      courtDate: v.courtDate,
      presidingOfficer: v.presidingOfficer,
      sentence: v.sentence || '',
      notes: v.notes || ''
    }).pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toastr.success('Court outcome recorded successfully');
          this.closeCourtOutcomeModal();
          this.loadDockets();
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.toastr.error(err.error?.message || 'Failed to record court outcome');
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }

  private toLocalDateInputValue(value: Date): string {
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
