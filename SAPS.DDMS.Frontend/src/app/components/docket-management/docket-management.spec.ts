import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { of } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { DocketManagement } from './docket-management';
import { DocketService } from '../../services/docket';
import { IncidentService } from '../../services/incident';
import { InvestigationNoteService } from '../../services/investigation-note';
import { PersonService } from '../../services/person';
import { AuthService } from '../../services/auth';
import { ReferenceDataService } from '../../services/reference-data';

describe('DocketManagement — core docket workflows', () => {
  let component: DocketManagement;
  let fixture: ComponentFixture<DocketManagement>;
  let docketService: any;
  const archiveReadyDocket = {
    docketID: 3,
    docketNumber: 'CAS-2026-003',
    status: 'CLOSED',
    obReferenceNumber: 'OB-003',
    docketSummary: 'Closed docket',
    incidentID: 3,
    stationName: 'JHB',
    dateCreated: new Date('2026-01-01T00:00:00Z'),
    docketClosedAt: new Date('2026-03-01T00:00:00Z')
  };

  const mockDockets = [
    { docketID: 1, docketNumber: 'CAS-2026-001', status: 'OPEN', obReferenceNumber: 'OB-001', docketSummary: 'Test docket 1', incidentID: 1, stationName: 'JHB', dateCreated: new Date() },
    { docketID: 2, docketNumber: 'CAS-2026-002', status: 'ALLOCATED', obReferenceNumber: 'OB-002', docketSummary: 'Test docket 2', incidentID: 2, stationName: 'JHB', dateCreated: new Date() },
    archiveReadyDocket
  ];

  beforeEach(async () => {
    docketService = {
      getDockets: vi.fn().mockReturnValue(of(mockDockets)),
      getDocketById: vi.fn().mockReturnValue(of(mockDockets[0])),
      createDocket: vi.fn().mockReturnValue(of({ success: true })),
      getAvailableOfficers: vi.fn().mockReturnValue(of([])),
      getProsecutorBundles: vi.fn().mockReturnValue(of([])),
      getPrivacyFlagHistory: vi.fn().mockReturnValue(of([])),
      exportProsecutorBundle: vi.fn().mockReturnValue(of({ success: true })),
      downloadProsecutorBundle: vi.fn().mockReturnValue(of(new Blob(['bundle'], { type: 'application/pdf' }))),
      confirmAiSummary: vi.fn().mockReturnValue(of({ success: true })),
      extractAiData: vi.fn().mockReturnValue(of({
        extraction: {
          docketNumber: 'SAPS-1-20260823-32862',
          caseType: 'Urgent / Priority',
          occurrenceDate: '2026-08-20T14:30:00Z',
          incidentDescription: 'Original extracted narrative',
          evidenceItemCount: 2
        }
      })),
      verifyAiExtraction: vi.fn().mockReturnValue(of({ success: true })),
      suggestRelatedCases: vi.fn().mockReturnValue(of([
        { relatedDocketId: 2, score: 0.82, reason: 'Matching location and modus operandi' }
      ])),
      confirmRelatedCases: vi.fn().mockReturnValue(of({ success: true })),
      generateReadinessIntelligence: vi.fn().mockReturnValue(of({
        docketId: 1,
        docketNumber: 'CAS-2026-001',
        generatedAtUtc: '2026-08-16T00:00:00Z',
        score: 75,
        readinessBand: 'Moderate',
        slaState: 'On track',
        metrics: {
          evidenceCount: 2,
          hashedEvidenceCount: 2,
          integrityCoverage: 100,
          custodyControlCoverage: 100,
          evidenceTransferCount: 1,
          activeInvestigationNotes: 2,
          assignmentAccepted: true,
          inspectionCompleted: false,
          prosecutorBundleStatus: 'Draft'
        },
        blockers: [],
        recommendations: ['Complete inspection'],
        limitations: 'Decision support only.'
      }))
    };

    await TestBed.configureTestingModule({
      imports: [RouterTestingModule, DocketManagement],
      providers: [
        { provide: DocketService, useValue: docketService },
        { provide: IncidentService, useValue: { getIncidentTypes: vi.fn().mockReturnValue(of([])) } },
        { provide: InvestigationNoteService, useValue: { getInvestigationNotes: vi.fn().mockReturnValue(of([])) } },
        { provide: PersonService, useValue: {} },
        { provide: AuthService, useValue: { getCurrentUser: vi.fn().mockReturnValue({ roleName: 'DSC Supervisor' }) } },
        {
          provide: ReferenceDataService,
          useValue: {
            getOperationalLookups: vi.fn().mockReturnValue(of({
              docketStatuses: ['OPEN', 'ALLOCATED', 'CLOSED'],
              provinces: ['Gauteng'],
              courtOutcomes: ['Guilty', 'Not Guilty'],
              incidentLocationTypes: ['Residential Property', 'Business Premises'],
              runtimePolicies: {
                docketPriorityHighDays: 7,
                archiveEligibleDays: 90,
                maxActiveDocketsPerOfficer: 10,
                inspectionReminderHours: 4,
                inspectionSlaHours: 24
              }
            }))
          }
        },
        { provide: ToastrService, useValue: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DocketManagement);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => vi.unstubAllGlobals());

  it('generates explainable case-readiness intelligence for the selected docket', () => {
    component.selectedDocket = mockDockets[0] as any;

    component.generateReadinessIntelligence();

    expect(docketService.generateReadinessIntelligence).toHaveBeenCalledWith(1);
    expect(component.readinessIntelligence?.score).toBe(75);
    expect(component.readinessIntelligence?.metrics.integrityCoverage).toBe(100);
    expect(component.readinessLoading).toBe(false);
  });

  it('loads dockets on initialisation', () => {
    expect(component).toBeTruthy();
    expect(docketService.getDockets).toHaveBeenCalled();
    expect(component.dockets).toHaveLength(3);
    expect(component.statusOptions).toEqual(['ALL', 'ALLOCATED', 'CLOSED', 'OPEN']);
  });

  it('shows docket-linked Notes and Evidence without the redundant Records action', () => {
    (component as any).roleName = 'DSC Supervisor';
    component.canAccessNotes = true;
    component.canAccessEvidence = true;
    fixture.detectChanges();

    const actions = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.actions-cell button')
    ).map(button => (button.textContent || '').trim());
    expect(actions).toContain('View notes');
    expect(actions).toContain('View evidence');
    expect(actions).not.toContain('Records');
  });

  it('limits Docket Administrator row actions to View and Archive', () => {
    (component as any).roleName = 'Docket Administrator';
    component.canArchiveDocket = true;
    component.canAccessEvidence = false;
    component.filteredDockets = [archiveReadyDocket as any];
    fixture.detectChanges();

    const actionLabels = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.actions-cell button')
    ).map(button => button.textContent?.trim());

    expect(actionLabels).toEqual(['View', 'Archive']);
  });

  it('does not show an empty secondary-actions menu to a view-only role', () => {
    (component as any).roleName = 'Backup Administrator';
    component.canUpdateDocket = false;
    component.canAllocateDocket = false;
    component.canAcceptAssignment = false;
    component.canCompleteInspection = false;
    component.canUpdateStatus = false;
    component.canManageBundles = false;
    component.canAccessNotes = false;
    component.canAccessEvidence = false;
    component.canArchiveDocket = false;
    component.canCloseOrReopenDocket = false;
    component.canRecordCourtOutcome = false;
    component.filteredDockets = [{ ...archiveReadyDocket, status: 'OPEN' } as any];
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('.actions-menu')).toBeNull();
    expect((fixture.nativeElement as HTMLElement).querySelector('.btn-view')).not.toBeNull();
  });

  it('keeps the Docket Administrator docket-details view read-only', () => {
    (component as any).roleName = 'Docket Administrator';
    component.selectedDocket = archiveReadyDocket as any;
    component.showDetailsModal = true;
    fixture.detectChanges();

    const detailFooterLabels = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.modal.show .modal-footer button')
    ).map(button => button.textContent?.trim());

    expect(detailFooterLabels).toEqual(['View Evidence', 'Close']);
  });

  it('filters by docket number', () => {
    component.searchTerm = 'CAS-2026-001';
    component.filterDockets();
    expect(component.filteredDockets.map(d => d.docketNumber)).toEqual(['CAS-2026-001']);
  });

  it('filters by status and restores all results', () => {
    component.selectedStatus = 'ALLOCATED';
    component.filterDockets();
    expect(component.filteredDockets.every(d => d.status === 'ALLOCATED')).toBe(true);

    component.selectedStatus = 'ALL';
    component.searchTerm = '';
    component.filterDockets();
    expect(component.filteredDockets).toHaveLength(mockDockets.length);
  });

  it('validates the create form', () => {
    component.createForm.reset();
    expect(component.createForm.invalid).toBe(true);
    component.createForm.patchValue({
      obReferenceNumber: 'OB-2026-00001',
      caseType: 'Normal',
      sensitivityLevel: 'NORMAL',
      docketSummary: 'Armed robbery at convenience store',
      complainantFullName: 'John Smith',
      incident: {
        occurrenceDate: '2026-05-25',
        locationType: 'Business premises',
        areaName: 'Pretoria Central',
        incidentTypeID: 1,
        description: 'Armed robbery incident description here'
      }
    });
    expect(component.createForm.valid).toBe(true);
  });

  it('omits an unselected optional incident type instead of sending an empty string', () => {
    component.canCreateDocket = true;
    component.createForm.patchValue({
      obReferenceNumber: 'OB-2026-00002',
      caseType: 'Normal',
      docketSummary: 'Vehicle theft docket',
      complainantFullName: 'Jane Smith',
      incident: {
        occurrenceDate: '2026-05-25T08:30',
        locationType: 'Parking area',
        areaName: 'Pretoria Central',
        description: 'Vehicle reported stolen from a secured parking area',
        incidentTypeID: 1
      }
    });

    component.createDocket();

    expect(docketService.createDocket).toHaveBeenCalledWith(
      expect.objectContaining({
        incident: expect.objectContaining({ incidentTypeID: 1 })
      })
    );
  });

  it('requires an explanation whenever Other is selected', () => {
    component.createForm.patchValue({
      caseType: 'Other',
      incident: { incidentTypeID: 'OTHER', locationType: 'Other' }
    });

    component.onClassificationChange();

    expect(component.createForm.get('otherCaseType')?.hasError('required')).toBe(true);
    expect(component.createForm.get('incident.otherIncidentType')?.hasError('required')).toBe(true);
    expect(component.createForm.get('incident.otherLocationType')?.hasError('required')).toBe(true);
  });

  it('submits specified Other values instead of the generic option', () => {
    component.canCreateDocket = true;
    component.createForm.patchValue({
      obReferenceNumber: 'OB-2026-00999',
      caseType: 'Other',
      otherCaseType: 'Priority witness protection',
      docketSummary: 'Priority docket requiring immediate protection',
      complainantFullName: 'Jane Smith',
      incident: {
        occurrenceDate: '2026-05-25T08:30',
        locationType: 'Other',
        otherLocationType: 'Community hall',
        areaName: 'Pretoria Central',
        latitude: '-25.747900',
        longitude: '28.229300',
        description: 'Witness intimidation reported at the community hall',
        incidentTypeID: 'OTHER',
        otherIncidentType: 'Witness intimidation'
      }
    });
    component.onClassificationChange();

    component.createDocket();

    expect(docketService.createDocket).toHaveBeenCalledWith(expect.objectContaining({
      caseType: 'Priority witness protection',
      incident: expect.objectContaining({
        locationType: 'Community hall',
        customIncidentType: 'Witness intimidation',
        incidentTypeID: undefined
      })
    }));
  });

  it('requires at least three characters before searching for an address', async () => {
    const fetchSpy = vi.fn();
    vi.stubGlobal('fetch', fetchSpy);
    component.addressSearchQuery = 'PT';

    await component.searchAddress();

    expect(fetchSpy).not.toHaveBeenCalled();
    expect(component.addressSearchError).toContain('at least 3 characters');
  });

  it('returns valid South African address-search suggestions', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      json: async () => [
        { display_name: 'Church Square, Pretoria, Gauteng, South Africa', lat: '-25.7461', lon: '28.1881' },
        { display_name: '', lat: 'invalid', lon: 'invalid' }
      ]
    }));
    component.addressSearchQuery = 'Church Square Pretoria';

    await component.searchAddress();

    expect(component.addressSearchResults).toEqual([{
      displayName: 'Church Square, Pretoria, Gauteng, South Africa',
      latitude: -25.7461,
      longitude: 28.1881
    }]);
    expect(component.addressSearchError).toBe('');
  });

  it('opens and closes the archive modal', () => {
    (component as any).roleName = 'Docket Administrator';
    component.canArchiveDocket = true;
    component.openArchiveModal(archiveReadyDocket as any);
    expect(component.showArchiveModal).toBe(true);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Confirm Archive');
    component.closeArchiveModal();
    expect(component.showArchiveModal).toBe(false);
    expect(component.selectedDocket).toBeNull();
  });

  it('shows an archive-eligibility message for recently closed dockets', () => {
    const closedTooSoon = {
      ...archiveReadyDocket,
      docketID: 4,
      docketClosedAt: new Date()
    };

    component.openArchiveModal(closedTooSoon as any);

    expect(component.showArchiveModal).toBe(false);
    expect(component.getArchiveEligibilityText(closedTooSoon as any)).toContain('Archive after');
  });

  it('renders the court outcome dialog independently of docket details', () => {
    component.openCourtOutcomeModal(mockDockets[0] as any);
    fixture.detectChanges();
    expect(component.showDetailsModal).toBe(false);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Save Court Outcome');
  });

  it('requires and records human confirmation of an edited AI summary', () => {
    component.selectedDocket = mockDockets[0] as any;
    component.aiSummary = 'Reviewed and corrected investigation summary.';

    component.confirmAiSummary();

    expect(docketService.confirmAiSummary).toHaveBeenCalledWith(
      1,
      'Reviewed and corrected investigation summary.'
    );
    expect(component.aiSummaryConfirmed).toBe(true);
  });

  it('presents AI extraction as labelled review fields instead of editable JSON', () => {
    component.selectedDocket = mockDockets[0] as any;
    component.showBundleModal = true;

    component.extractAiData();
    fixture.detectChanges();

    expect(component.aiExtractionFields.map(field => field.label)).toContain('Incident Description');
    expect(component.aiExtractionFields.find(field => field.key === 'docketNumber')?.readOnly).toBe(true);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Review Extracted Docket Data');
    expect((fixture.nativeElement as HTMLElement).querySelector('#ai-extraction-review')).toBeNull();
  });

  it('sends reviewed AI field values with their original data types', () => {
    component.selectedDocket = mockDockets[0] as any;
    component.extractAiData();
    component.aiExtractionFields.find(field => field.key === 'incidentDescription')!.value = 'Corrected witness narrative';
    component.aiExtractionFields.find(field => field.key === 'evidenceItemCount')!.value = 3;

    component.verifyAiExtraction();

    expect(docketService.verifyAiExtraction).toHaveBeenCalledWith(1, expect.objectContaining({
      docketNumber: 'SAPS-1-20260823-32862',
      incidentDescription: 'Corrected witness narrative',
      evidenceItemCount: 3,
      verifiedAt: expect.any(String)
    }));
    expect(component.aiExtractionFields).toEqual([]);
  });

  it('validates edited related-case suggestions before confirmation', () => {
    component.selectedDocket = mockDockets[0] as any;
    component.suggestRelatedCases();
    component.relatedCaseReviewItems[0].reason = 'Reviewed matching location and modus operandi';

    component.confirmRelatedCases();

    expect(docketService.confirmRelatedCases).toHaveBeenCalledWith(1, [
      { relatedDocketId: 2, score: 0.82, reason: 'Reviewed matching location and modus operandi' }
    ]);
    expect(component.relatedCasesConfirmed).toBe(true);
  });

  it('renders related cases as structured cards and excludes rejected suggestions', () => {
    component.selectedDocket = mockDockets[0] as any;
    component.showBundleModal = true;
    component.suggestRelatedCases();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Review Related Case Suggestions');
    expect((fixture.nativeElement as HTMLElement).querySelector('#related-cases-review')).toBeNull();
    expect(component.getRelatedCaseScorePercent(component.relatedCaseReviewItems[0])).toBe(82);

    component.relatedCaseReviewItems[0].included = false;
    component.confirmRelatedCases();
    expect(docketService.confirmRelatedCases).not.toHaveBeenCalled();
  });

  it('names a new prosecutor bundle after the complainant, not the SAPS docket number', () => {
    const docket = {
      ...mockDockets[0],
      complainantFullName: 'Naledi Khumalo'
    } as any;
    component.canManageBundles = true;

    component.openBundleModal(docket);

    expect(component.bundleTitle).toBe('Naledi Khumalo Prosecutor Bundle');
  });

  it('downloads a prosecutor bundle with a sanitised complainant filename', () => {
    const docket = {
      ...mockDockets[0],
      complainantFullName: 'Naledi / Khumalo'
    } as any;
    const createObjectUrl = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:test');
    const revokeObjectUrl = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const originalCreateElement = document.createElement.bind(document);
    const anchor = originalCreateElement('a');
    const click = vi.spyOn(anchor, 'click').mockImplementation(() => undefined);
    const createElement = vi.spyOn(document, 'createElement').mockImplementation((tagName: string, options?: ElementCreationOptions) =>
      tagName.toLowerCase() === 'a' ? anchor : originalCreateElement(tagName, options)
    );
    component.selectedDocket = docket;
    component.canExportBundle = true;

    component.exportBundle({ bundleID: 44, title: 'SAPS DDMS' });

    expect(anchor.download).toBe('Naledi Khumalo - Prosecutor Bundle.pdf');
    expect(click).toHaveBeenCalledOnce();

    createElement.mockRestore();
    createObjectUrl.mockRestore();
    revokeObjectUrl.mockRestore();
  });
});
