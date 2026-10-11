import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { NEVER, of } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { EvidenceManagement } from './evidence-management';
import { EvidenceService } from '../../services/evidence';
import { DocketService } from '../../services/docket';
import { AuthService } from '../../services/auth';
import { ReferenceDataService } from '../../services/reference-data';

describe('EvidenceManagement — custody workflows', () => {
  let component: EvidenceManagement;
  let fixture: ComponentFixture<EvidenceManagement>;
  let evidenceService: any;

  beforeEach(async () => {
    evidenceService = {
      getEvidenceTypes: vi.fn().mockReturnValue(of([{ evidenceTypeID: 1, name: 'Document' }])),
      getEvidenceLocations: vi.fn().mockReturnValue(of([{ evidenceLocationID: 1, locationName: 'Evidence Store' }])),
      getEvidenceByDocket: vi.fn().mockReturnValue(of([])),
      createEvidence: vi.fn().mockReturnValue(NEVER),
      updateEvidence: vi.fn().mockReturnValue(NEVER),
      getChainOfCustody: vi.fn().mockReturnValue(of([])),
      recordTransfer: vi.fn().mockReturnValue(of({ success: true }))
    };
    await TestBed.configureTestingModule({
      imports: [RouterTestingModule, EvidenceManagement],
      providers: [
        { provide: EvidenceService, useValue: evidenceService },
        { provide: DocketService, useValue: { getDockets: vi.fn().mockReturnValue(of([])) } },
        { provide: AuthService, useValue: { getCurrentUser: vi.fn().mockReturnValue({ roleName: 'Investigating Officer' }) } },
        {
          provide: ReferenceDataService,
          useValue: {
            getOperationalLookups: vi.fn().mockReturnValue(of({
              docketStatuses: ['OPEN'],
              provinces: ['Gauteng'],
              courtOutcomes: ['Guilty'],
              evidenceCollectionMethods: ['Photographed'],
              evidenceItemStates: ['Sealed'],
              runtimePolicies: { evidenceRetentionYears: 5 }
            }))
          }
        },
        { provide: ActivatedRoute, useValue: { queryParams: of({}) } },
        { provide: ToastrService, useValue: { success: vi.fn(), error: vi.fn(), warning: vi.fn() } }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(EvidenceManagement);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads reference data', () => {
    expect(component).toBeTruthy();
    expect(evidenceService.getEvidenceTypes).toHaveBeenCalled();
    expect(evidenceService.getEvidenceLocations).toHaveBeenCalled();
    expect(component.evidenceRetentionYears).toBe(5);
  });

  it('opens a transfer with the immutable current location', () => {
    const evidence: any = { evidenceID: 1, description: 'Test evidence', evidenceLocationID: 7 };
    component.openTransferModal(evidence);
    expect(component.selectedEvidence).toBe(evidence);
    expect(component.showTransferModal).toBe(true);
    expect(component.transferForm.value.fromLocationID).toBe(7);
  });

  it('loads chain-of-custody history by evidence ID', () => {
    const evidence: any = { evidenceID: 3, description: 'Test evidence' };
    component.loadCustodyHistory(evidence);
    expect(evidenceService.getChainOfCustody).toHaveBeenCalledWith(3);
    expect(component.showDetailsModal).toBe(true);
  });

  it('calculates evidence retention review dates from the configured rule', () => {
    const reviewDate = component.getRetentionReviewDate({
      evidenceID: 1,
      evidenceReferenceNumber: 'EV-1',
      description: 'Document',
      evidenceType: 'Document',
      quantity: 1,
      dateCollected: new Date('2020-07-25T00:00:00Z'),
      collectionMethod: '',
      stateOfItem: '',
      evidenceLocation: 'Evidence Store',
      evidenceLocationID: 1
    });

    expect(reviewDate?.toISOString().slice(0, 10)).toBe('2025-07-25');
  });

  it('rejects blank descriptions, fractional quantities and future collection dates', () => {
    component.createForm.patchValue({
      description: '   ',
      quantity: 1.5,
      dateCollected: new Date(Date.now() + 60_000).toISOString()
    });

    expect(component.createForm.get('description')?.hasError('required')).toBe(true);
    expect(component.createForm.get('quantity')?.hasError('positiveInteger')).toBe(true);
    expect(component.createForm.get('dateCollected')?.hasError('futureDate')).toBe(true);
  });

  it('blocks duplicate evidence submissions while the first request is pending', () => {
    component.createForm.patchValue({
      description: 'Sealed document',
      evidenceTypeID: 1,
      quantity: 1,
      dateCollected: new Date(Date.now() - 60_000).toISOString(),
      collectionMethod: 'Photographed',
      stateOfItem: 'Sealed',
      evidenceLocationID: 1,
      signatureMethod: 'Ink',
      docketID: 10
    });

    component.createEvidence();
    component.createEvidence();

    expect(evidenceService.createEvidence).toHaveBeenCalledTimes(1);
  });

  it('confirms successful evidence registration and refreshes the list', () => {
    const toastr = TestBed.inject(ToastrService) as any;
    evidenceService.createEvidence.mockReturnValueOnce(of({ success: true }));
    component.selectedDocketId = 10;
    component.showCreateModal = true;
    component.createForm.patchValue({
      description: 'Sealed documentary evidence',
      evidenceTypeID: 1,
      quantity: 1,
      dateCollected: new Date(Date.now() - 60_000).toISOString(),
      collectionMethod: 'Photographed',
      stateOfItem: 'Sealed',
      evidenceLocationID: 1,
      signatureMethod: 'Ink',
      docketID: 10
    });

    component.createEvidence();

    expect(toastr.success).toHaveBeenCalledWith('Evidence has been registered successfully');
    expect(component.showCreateModal).toBe(false);
    expect(evidenceService.getEvidenceByDocket).toHaveBeenCalledWith(10);
  });

  it('accepts PDFs and videos and rejects unsupported or oversized evidence files before upload', () => {
    const pdf = new File(['%PDF-1.4'], 'statement.pdf', { type: 'application/pdf' });
    component.onFileSelected({ target: { files: [pdf], value: 'statement.pdf' } } as any);
    expect(component.selectedEvidenceFile).toBe(pdf);
    expect(component.fileValidationMessage).toBe('');

    const video = new File(['video'], 'scene.mp4', { type: 'video/mp4' });
    component.onFileSelected({ target: { files: [video], value: 'scene.mp4' } } as any);
    expect(component.selectedEvidenceFile).toBe(video);
    expect(component.fileValidationMessage).toBe('');

    const invalidTypeInput: any = {
      files: [new File(['not an image'], 'payload.exe', { type: 'application/octet-stream' })],
      value: 'payload.exe'
    };
    component.onFileSelected({ target: invalidTypeInput } as any);
    expect(component.selectedEvidenceFile).toBeNull();
    expect(component.fileValidationMessage).toContain('JPEG');

    const oversizedInput: any = {
      files: [new File([new Uint8Array(50 * 1024 * 1024 + 1)], 'large.mp4', { type: 'video/mp4' })],
      value: 'large.png'
    };
    component.onFileSelected({ target: oversizedInput } as any);
    expect(component.selectedEvidenceFile).toBeNull();
    expect(component.fileValidationMessage).toContain('50 MB');
  });
});
