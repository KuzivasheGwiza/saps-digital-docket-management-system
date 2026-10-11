import { TestBed } from '@angular/core/testing';

import { InvestigationNoteService } from './investigation-note';

describe('InvestigationNoteService', () => {
  let service: InvestigationNoteService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(InvestigationNoteService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
