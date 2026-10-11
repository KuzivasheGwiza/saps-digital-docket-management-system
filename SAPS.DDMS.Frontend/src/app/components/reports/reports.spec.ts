import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { RouterTestingModule } from '@angular/router/testing';
import { of } from 'rxjs';
import { Reports } from './reports';
import { AuthService } from '../../services/auth';

describe('Reports criteria', () => {
  let component: Reports;
  let http: { get: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    http = { get: vi.fn().mockReturnValue(of({
      key: 'dockets-list', title: 'Report', generatedAt: new Date().toISOString(),
      generatedBy: 'Supervisor', columns: [], rows: [], totals: {}
    })) };

    await TestBed.configureTestingModule({
      imports: [RouterTestingModule, Reports],
      providers: [
        { provide: HttpClient, useValue: http },
        { provide: AuthService, useValue: { getCurrentUser: () => ({ roleName: 'DSC Supervisor' }) } }
      ]
    }).compileComponents();

    component = TestBed.createComponent(Reports).componentInstance;
    component.reports = [
      { key: 'hotspot-analysis', label: 'Hotspot Analysis' },
      { key: 'case-aging', label: 'Workload and Case Aging' }
    ];
  });

  it('sends the selected hotspot crime category to screen and PDF requests', () => {
    component.selectedKey = 'hotspot-analysis';
    component.fromDate = '2026-01-01';
    component.toDate = '2026-01-31';
    component.crimeCategory = 'Vehicle Theft';

    component.loadReport();

    const options = http.get.mock.calls[0][1];
    expect(options.params.get('crimeCategory')).toBe('Vehicle Theft');
    expect(options.params.get('from')).toBe('2026-01-01');
    expect(options.params.get('to')).toBe('2026-01-31');
  });

  it('sends the Case Aging date range to the backend', () => {
    component.selectedKey = 'case-aging';
    component.fromDate = '2026-02-01';
    component.toDate = '2026-02-28';

    component.loadReport();

    const options = http.get.mock.calls[0][1];
    expect(options.params.get('from')).toBe('2026-02-01');
    expect(options.params.get('to')).toBe('2026-02-28');
  });

  it('renders station workload as a two-level transactional report with subtotals', () => {
    component.selectedKey = 'station-workload';
    component.report = {
      key: 'station-workload', title: 'Station Workload', generatedAt: new Date().toISOString(),
      generatedBy: 'Supervisor', columns: ['Province', 'Station', 'Status', 'Count', 'StationTotal'],
      rows: [
        { Province: 'Gauteng', Station: 'Pretoria Central', Status: 'OPEN', Count: 2, StationTotal: 3 },
        { Province: 'Gauteng', Station: 'Pretoria Central', Status: 'CLOSED', Count: 1, StationTotal: 3 },
        { Province: 'Gauteng', Station: 'Sunnyside', Status: 'OPEN', Count: 4, StationTotal: 4 }
      ], totals: { 'Grand Total': 7 }
    };

    expect(component.isTransactional).toBe(true);
    expect(component.transactionalGroups).toHaveLength(1);
    expect(component.transactionalGroups[0].sections).toHaveLength(2);
    expect(component.transactionalGroups[0].subtotal).toBe(7);
    expect(component.transactionGrandTotal).toBe(7);
  });

  it('collapses crime-trend categories into one useful bar per month', () => {
    component.selectedKey = 'crime-trend';
    component.report = {
      key: 'crime-trend', title: 'Crime Trend', generatedAt: new Date().toISOString(),
      generatedBy: 'Supervisor', columns: ['Month', 'CrimeCategory', 'Count'],
      rows: [
        { Month: '2026-01', CrimeCategory: 'Assault', Count: 2 },
        { Month: '2026-01', CrimeCategory: 'Theft', Count: 3 },
        { Month: '2026-02', CrimeCategory: 'Assault', Count: 4 }
      ], totals: {}
    };

    expect(component.displayedChart).toEqual([
      { Month: '2026-01', Count: 5 },
      { Month: '2026-02', Count: 4 }
    ]);
  });

  it('uses docket counts to size the status distribution graph', () => {
    component.selectedKey = 'status-management';
    component.report = {
      key: 'status-management', title: 'Docket Status Distribution', generatedAt: new Date().toISOString(),
      generatedBy: 'Supervisor', columns: ['Status', 'Count', 'Percent'], rows: [], totals: {},
      chart: [{ Status: 'OPEN', Count: 8 }, { Status: 'CLOSED', Count: 2 }]
    };

    expect(component.chartWidth(component.displayedChart[0])).toBe('100%');
    expect(component.chartWidth(component.displayedChart[1])).toBe('25%');
  });
});
