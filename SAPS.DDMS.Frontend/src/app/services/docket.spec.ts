import { TestBed } from '@angular/core/testing';

import { DocketService } from './docket';

describe('DocketService', () => {
  let service: DocketService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(DocketService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
