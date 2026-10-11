import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DataExchangeService, LookupImportDocument, LookupImportPreview } from './data-exchange';

describe('DataExchangeService', () => {
  let service: DataExchangeService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(DataExchangeService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('exports the selected dataset with only populated parameters', () => {
    service.exportFile('dockets', 'xlsx', {
      from: '2026-01-01',
      to: '',
      stationId: 4,
      roleId: null,
      status: ' OPEN '
    }).subscribe();

    const request = http.expectOne(req => req.url === '/api/data-exchange/export/dockets');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('format')).toBe('xlsx');
    expect(request.request.params.get('from')).toBe('2026-01-01');
    expect(request.request.params.get('stationId')).toBe('4');
    expect(request.request.params.get('status')).toBe('OPEN');
    expect(request.request.params.has('to')).toBe(false);
    expect(request.request.params.has('roleId')).toBe(false);
    request.flush(new Blob(['xlsx']));
  });

  it('requests Word and XML export formats from the same parameterised endpoint', () => {
    service.exportFile('users', 'doc', {}).subscribe();
    let request = http.expectOne(req => req.url === '/api/data-exchange/export/users');
    expect(request.request.params.get('format')).toBe('doc');
    request.flush(new Blob(['word']));

    service.exportFile('audit', 'xml', { actionType: ' LOGIN ' }).subscribe();
    request = http.expectOne(req => req.url === '/api/data-exchange/export/audit');
    expect(request.request.params.get('format')).toBe('xml');
    expect(request.request.params.get('actionType')).toBe('LOGIN');
    request.flush(new Blob(['xml']));
  });

  it('previews a lookup document without forwarding confirmation fields', () => {
    const document: LookupImportDocument = {
      schemaVersion: 1,
      lookupType: 'incident-types',
      records: [{ name: 'Cybercrime', description: 'Online offences' }],
      previewToken: 'stale-token',
      confirmApply: true
    };

    service.previewLookupImport(document).subscribe();
    const request = http.expectOne('/api/data-exchange/lookups/import/preview');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.previewToken).toBeUndefined();
    expect(request.request.body.confirmApply).toBeUndefined();
    request.flush({
      schemaVersion: 1,
      lookupType: 'incident-types',
      contentHash: 'HASH',
      totalRecords: 1,
      createCount: 1,
      updateCount: 0,
      unchangedCount: 0,
      canApply: true,
      previewToken: 'preview-token',
      errors: [],
      rows: []
    } satisfies LookupImportPreview);
  });

  it('requires the server preview token when applying reviewed lookups', () => {
    const document: LookupImportDocument = {
      schemaVersion: 1,
      lookupType: 'evidence-types',
      records: [{ name: 'DNA' }]
    };

    service.applyLookupImport(document, 'preview-token').subscribe();
    const request = http.expectOne('/api/data-exchange/lookups/import/apply');
    expect(request.request.method).toBe('POST');
    expect(request.request.body.previewToken).toBe('preview-token');
    expect(request.request.body.confirmApply).toBe(true);
    request.flush({
      success: true,
      message: 'Applied',
      lookupType: 'evidence-types',
      created: 1,
      updated: 0,
      unchanged: 0,
      contentHash: 'HASH'
    });
  });
});
