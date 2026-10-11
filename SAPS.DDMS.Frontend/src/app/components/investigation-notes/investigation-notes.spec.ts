import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { of } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { InvestigationNotes } from './investigation-notes';
import { InvestigationNoteService } from '../../services/investigation-note';
import { DocketService } from '../../services/docket';
import { AuthService } from '../../services/auth';

describe('InvestigationNotes — note and archive workflows', () => {
  let component: InvestigationNotes;
  let fixture: ComponentFixture<InvestigationNotes>;
  let noteService: any;
  const mockNote: any = { noteID: 1, description: 'Detailed investigation note', entryDate: '2026-05-25', investigationID: 1, attachments: [] };

  beforeEach(async () => {
    noteService = {
      getInvestigationNotes: vi.fn().mockReturnValue(of([mockNote])),
      createNote: vi.fn().mockReturnValue(of({ success: true })),
      requestArchiveNote: vi.fn().mockReturnValue(of({ success: true })),
      getArchiveRequests: vi.fn().mockReturnValue(of([]))
    };
    await TestBed.configureTestingModule({
      imports: [RouterTestingModule, InvestigationNotes],
      providers: [
        { provide: InvestigationNoteService, useValue: noteService },
        { provide: DocketService, useValue: { getDocketById: vi.fn().mockReturnValue(of({ docketID: 1, docketNumber: 'CAS-001', investigationID: 1 })) } },
        { provide: AuthService, useValue: { getCurrentUser: vi.fn().mockReturnValue({ roleName: 'Investigating Officer' }) } },
        { provide: ActivatedRoute, useValue: { params: of({ id: '1' }) } },
        { provide: ToastrService, useValue: { success: vi.fn(), error: vi.fn(), warning: vi.fn() } }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(InvestigationNotes);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads the docket and notes', () => {
    expect(component.docketId).toBe(1);
    expect(component.notes).toHaveLength(1);
  });

  it('requires a meaningful note description', () => {
    component.createForm.patchValue({ description: 'short' });
    expect(component.createForm.invalid).toBe(true);
    component.createForm.patchValue({ description: 'This is a valid investigation note.' });
    expect(component.createForm.valid).toBe(true);
  });

  it('rejects whitespace-only and overlong notes', () => {
    component.createForm.patchValue({ description: '          ' });
    expect(component.createForm.get('description')?.hasError('required')).toBe(true);

    component.createForm.patchValue({ description: 'x'.repeat(257) });
    expect(component.createForm.get('description')?.hasError('maxlength')).toBe(true);
  });

  it('rejects unsafe note attachments before upload', () => {
    const input: any = {
      files: [new File(['payload'], 'payload.exe', { type: 'application/octet-stream' })],
      value: 'payload.exe'
    };

    component.onFileSelected({ target: input } as any);

    expect(component.selectedFile).toBeNull();
    expect(component.fileValidationMessage).toContain('PDF');
  });

  it('saves a note successfully when no attachment is selected', () => {
    component.createForm.patchValue({ description: 'This note is saved without an attachment.' });
    component.createNote();

    expect(noteService.createNote).toHaveBeenCalledTimes(1);
    const submitted = noteService.createNote.mock.calls[0][0] as FormData;
    expect(submitted.has('attachment')).toBe(false);
  });

  it('opens and closes archive request state', () => {
    const archiveButton = (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[aria-label="Request note archive"]');
    expect(archiveButton).toBeTruthy();
    archiveButton!.click();
    expect(component.showArchiveRequestModal).toBe(true);
    fixture.detectChanges();
    expect(component.showVersionModal).toBe(false);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Submit Request');
    component.closeArchiveRequestModal();
    expect(component.showArchiveRequestModal).toBe(false);
    expect(component.archiveRequestNote).toBeNull();
  });

  it('rejects an empty archive reason', () => {
    component.archiveRequestNote = mockNote;
    component.archiveRequestReason = '';
    component.submitArchiveRequest();
    expect(component.isSubmittingArchive).toBe(false);
  });
});
