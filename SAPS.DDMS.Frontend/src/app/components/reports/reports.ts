import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpParams } from '@angular/common/http';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { API_URL } from '../../config/api.config';
import { AuthService } from '../../services/auth';
import { hasAnyRole, REPORT_ROLES_BY_KEY } from '../../security/role-policy';
import { DEFAULT_DOCKET_STATUSES } from '../../services/reference-data';

interface ReportDefinition {
  key: string;
  label: string;
}

interface ReportResponse {
  key: string;
  title: string;
  category?: string;
  generatedAt: string;
  generatedBy: string;
  generatedWhere?: string;
  criteria?: Record<string, string>;
  columns: string[];
  rows: Record<string, unknown>[];
  totals: Record<string, unknown>;
  chart?: Record<string, unknown>[];
  chartTitle?: string;
  landscape?: boolean;
}

interface TransactionSection {
  label: string;
  rows: Record<string, unknown>[];
  subtotal: number;
}

interface TransactionGroup {
  label: string;
  sections: TransactionSection[];
  subtotal: number;
}

interface StationSummary {
  province: string;
  station: string;
  total: number;
  statuses: Record<string, number>;
}

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './reports.html',
  styleUrls: ['./reports.css']
})
export class Reports implements OnInit {
  private http = inject(HttpClient);
  private authService = inject(AuthService);
  private cdr = inject(ChangeDetectorRef);
  private route = inject(ActivatedRoute);
  currentUser = this.authService.getCurrentUser();

  private readonly reportDefinitions: ReportDefinition[] = [
    { key: 'users-list', label: 'User List' },
    { key: 'dockets-list', label: 'Docket List' },
    { key: 'evidence-list', label: 'Evidence List' },
    { key: 'station-workload', label: 'Station Workload' },
    { key: 'custody-summary', label: 'Custody Transaction Report' },
    { key: 'docket-criteria', label: 'Docket Criteria Report' },
    { key: 'status-management', label: 'Status Management' },
    { key: 'hotspot-analysis', label: 'Hotspot Analysis' },
    { key: 'clearance-rate', label: 'Clearance Rate' },
    { key: 'crime-trend', label: 'Crime Transaction Report' },
    { key: 'response-time', label: 'Response Time' },
    { key: 'case-aging', label: 'Workload and Case Aging' }
  ];
  reports: ReportDefinition[] = [];
  selectedKey = 'dockets-list';
  report: ReportResponse | null = null;
  isLoading = false;
  isDownloading = false;
  errorMessage = '';
  dedicatedRegister = false;

  fromDate = '';
  toDate = '';
  status = '';
  crimeCategory = '';
  statusOptions = DEFAULT_DOCKET_STATUSES;

  ngOnInit(): void {
    this.dedicatedRegister = this.route.snapshot.data['defaultReport'] === 'evidence-list';
    const user = this.currentUser = this.authService.getCurrentUser();
    this.reports = this.reportDefinitions.filter(report =>
      hasAnyRole(user?.roleName, REPORT_ROLES_BY_KEY[report.key] || [])
    ).sort((left, right) => left.label.localeCompare(right.label));
    if (this.dedicatedRegister) {
      this.reports = this.reports.filter(report => report.key === 'evidence-list');
    }
    this.selectedKey = this.dedicatedRegister ? 'evidence-list' : (this.reports[0]?.key || '');
    if (!this.selectedKey) {
      this.errorMessage = 'No reports are available for your role.';
      return;
    }

    this.loadReport();
  }

  selectReport(key: string): void {
    if (!key || key === this.selectedKey) return;
    this.selectedKey = key;
    this.crimeCategory = '';
    this.loadReport();
  }

  loadReport(): void {
    if (this.hasInvalidDateRange()) {
      this.errorMessage = 'The From date must be on or before the To date.';
      this.isLoading = false;
      return;
    }
    this.isLoading = true;
    this.errorMessage = '';
    this.report = null;

    this.http.get<ReportResponse>(`${API_URL}/reports/${this.selectedKey}`, { params: this.buildParams() })
      .subscribe({
        next: report => {
          this.report = report;
          this.isLoading = false;
          this.cdr.detectChanges();
        },
        error: err => {
          this.errorMessage = err.error?.message || 'Report could not be loaded.';
          this.isLoading = false;
          this.cdr.detectChanges();
        }
      });
  }

  downloadPdf(): void {
    if (this.hasInvalidDateRange()) {
      this.errorMessage = 'The From date must be on or before the To date.';
      return;
    }
    if (!this.report || this.isDownloading) {
      return;
    }

    this.errorMessage = '';
    this.isDownloading = true;

    this.http.get(`${API_URL}/reports/${this.selectedKey}/pdf`, {
      params: this.buildParams(),
      responseType: 'blob'
    }).subscribe({
      next: blob => {
        const pdfBlob = blob.type === 'application/pdf'
          ? blob
          : new Blob([blob], { type: 'application/pdf' });
        const objectUrl = window.URL.createObjectURL(pdfBlob);
        const anchor = document.createElement('a');
        anchor.href = objectUrl;
        anchor.download = `${this.selectedReport.label.replace(/\s+/g, '-')}.pdf`;
        anchor.click();
        window.setTimeout(() => window.URL.revokeObjectURL(objectUrl), 0);
        this.isDownloading = false;
        this.cdr.detectChanges();
      },
      error: err => {
        console.error('Report PDF download failed', err);
        this.errorMessage = 'The PDF could not be downloaded. Please try again.';
        this.isDownloading = false;
        this.cdr.detectChanges();
      }
    });
  }

  get selectedReport(): ReportDefinition {
    return this.reports.find(report => report.key === this.selectedKey) ?? this.reports[0];
  }

  get totalPairs(): { key: string; value: unknown }[] {
    return Object.entries(this.report?.totals || {})
      .filter(([key]) => this.selectedKey !== 'station-workload' || !/station/i.test(key))
      .map(([key, value]) => ({ key, value }));
  }

  get activeFilterChips(): string[] {
    const chips: string[] = [];
    if (this.supportsDateCriteria()) {
      if (this.fromDate) chips.push(`From ${this.fromDate}`);
      if (this.toDate) chips.push(`To ${this.toDate}`);
    }
    if (this.supportsStatusCriteria() && this.status) chips.push(`Status: ${this.status}`);
    if (this.supportsCrimeCategoryFilter() && this.crimeCategory) {
      chips.push(`Crime: ${this.crimeCategory}`);
    }
    return chips;
  }

  get isTransactional(): boolean {
    return this.selectedKey === 'station-workload';
  }

  get transactionDetailColumns(): string[] {
    const excluded = this.selectedKey === 'station-workload'
      ? ['Province', 'Station', 'StationTotal']
      : ['Month', 'CrimeCategory'];
    const columns = (this.report?.columns || []).filter(column => !excluded.includes(column));
    return columns.length ? columns : ['Area', 'Count'];
  }

  get transactionPrimaryLabel(): string {
    return this.selectedKey === 'station-workload' ? 'Province' : 'Month';
  }

  get transactionSecondaryLabel(): string {
    return this.selectedKey === 'station-workload' ? 'Station' : 'Crime type';
  }

  get transactionUnit(): string {
    return this.selectedKey === 'station-workload' ? 'dockets' : 'cases';
  }

  get transactionalGroups(): TransactionGroup[] {
    if (!this.report || !this.isTransactional) return [];
    const primaryKey = this.selectedKey === 'station-workload' ? 'Province' : 'Month';
    const secondaryKey = this.selectedKey === 'station-workload' ? 'Station' : 'CrimeCategory';
    const valueKey = 'Count';

    return this.groupRows(this.report.rows, primaryKey).map(primary => {
      const sections = this.groupRows(primary.rows, secondaryKey).map(secondary => ({
        label: secondary.label,
        rows: secondary.rows,
        subtotal: secondary.rows.reduce((sum, row) => sum + (Number(row[valueKey]) || 0), 0)
      }));
      return {
        label: primary.label,
        sections,
        subtotal: sections.reduce((sum, section) => sum + section.subtotal, 0)
      };
    });
  }

  get transactionGrandTotal(): number {
    return this.transactionalGroups.reduce((sum, group) => sum + group.subtotal, 0);
  }

  get displayedRows(): Record<string, unknown>[] {
    const rows = this.report?.rows || [];
    if (this.selectedKey !== 'hotspot-analysis' || !this.crimeCategory) return rows;
    return rows.filter(row => this.formatValue(row['CrimeCategory']) === this.crimeCategory);
  }

  get displayedColumns(): string[] {
    const columns = this.report?.columns || [];
    return this.selectedKey === 'hotspot-analysis'
      ? columns.filter(column => column !== 'CrimeCategory')
      : columns;
  }

  get crimeCategories(): string[] {
    if (this.selectedKey !== 'hotspot-analysis') return [];
    return [...new Set((this.report?.rows || []).map(row => this.formatValue(row['CrimeCategory'])).filter(value => value !== '-'))]
      .sort((a, b) => a.localeCompare(b));
  }

  get stationSummaries(): StationSummary[] {
    if (this.selectedKey !== 'station-workload') return [];
    const summaries = new Map<string, StationSummary>();
    for (const row of this.report?.rows || []) {
      const station = this.formatValue(row['Station']);
      const province = this.formatValue(row['Province']);
      const status = this.formatValue(row['Status']);
      const count = Number(row['Count']) || 0;
      const key = `${province}\u0000${station}`;
      const summary = summaries.get(key) || { province, station, total: 0, statuses: {} };
      summary.total += count;
      summary.statuses[status] = (summary.statuses[status] || 0) + count;
      summaries.set(key, summary);
    }
    return [...summaries.values()].sort((a, b) => b.total - a.total || a.station.localeCompare(b.station));
  }

  get stationStatuses(): string[] {
    return [...new Set(this.stationSummaries.flatMap(row => Object.keys(row.statuses)))].sort();
  }

  get busiestStation(): StationSummary | null {
    return this.stationSummaries[0] || null;
  }

  get averageStationWorkload(): number {
    if (!this.stationSummaries.length) return 0;
    return Math.round(this.stationSummaries.reduce((sum, row) => sum + row.total, 0) / this.stationSummaries.length);
  }

  get displayedChart(): Record<string, unknown>[] {
    if (this.selectedKey === 'crime-trend') {
      const monthlyTotals = new Map<string, number>();
      for (const row of this.report?.rows || []) {
        const month = this.formatValue(row['Month']);
        monthlyTotals.set(month, (monthlyTotals.get(month) || 0) + (Number(row['Count']) || 0));
      }
      return [...monthlyTotals].sort(([left], [right]) => left.localeCompare(right))
        .map(([Month, Count]) => ({ Month, Count }));
    }
    if (this.selectedKey !== 'hotspot-analysis') return this.report?.chart || [];
    const areas = new Map<string, number>();
    for (const row of this.displayedRows) {
      const area = this.formatValue(row['Area']);
      areas.set(area, (areas.get(area) || 0) + (Number(row['Count']) || 0));
    }
    return [...areas].map(([Area, Count]) => ({ Area, Count })).sort((a, b) => Number(b.Count) - Number(a.Count));
  }

  get maxChartCount(): number {
    const values = this.displayedChart.map(row => this.chartValue(row));
    return Math.max(...values, 1);
  }

  chartWidth(row: Record<string, unknown>): string {
    const count = this.chartValue(row);
    return `${Math.max(6, (count / this.maxChartCount) * 100)}%`;
  }

  chartLabel(row: Record<string, unknown>): string {
    const keys = ['Status', 'Area', 'Station', 'Month', 'Officer', 'CrimeCategory'];
    const key = keys.find(candidate => row[candidate] !== undefined);
    return this.formatValue(key ? row[key] : Object.values(row)[0]);
  }

  chartValue(row: Record<string, unknown>): number {
    for (const key of ['Count', 'ClearanceRate', 'OldestDays']) {
      if (row[key] !== undefined) return Number(row[key]) || 0;
    }
    return 0;
  }

  supportsDateCriteria(): boolean {
    return ['docket-criteria', 'hotspot-analysis', 'clearance-rate', 'crime-trend', 'response-time', 'case-aging'].includes(this.selectedKey);
  }

  supportsStatusCriteria(): boolean {
    return this.selectedKey === 'docket-criteria';
  }

  supportsCrimeCategoryFilter(): boolean {
    return this.selectedKey === 'hotspot-analysis';
  }

  heatColour(row: Record<string, unknown>): string {
    const intensity = Math.max(.18, this.chartValue(row) / this.maxChartCount);
    return `rgba(180, 35, 24, ${Math.min(.9, intensity).toFixed(2)})`;
  }

  downloadCsv(): void {
    if (!this.report) return;
    const rows: Record<string, unknown>[] = this.selectedKey === 'station-workload'
      ? this.stationSummaries.map(item => ({ Province: item.province, Station: item.station, ...item.statuses, Total: item.total }))
      : this.displayedRows;
    const columns = this.selectedKey === 'station-workload'
      ? ['Province', 'Station', ...this.stationStatuses, 'Total']
      : this.displayedColumns;
    const escape = (value: unknown) => `"${this.formatValue(value).replace(/"/g, '""')}"`;
    const csv = [columns.map(escape).join(','), ...rows.map(row => columns.map(column => escape(row[column])).join(','))].join('\r\n');
    const objectUrl = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const anchor = document.createElement('a');
    anchor.href = objectUrl;
    anchor.download = `${this.selectedReport.label.replace(/\s+/g, '-')}.csv`;
    anchor.click();
    URL.revokeObjectURL(objectUrl);
  }

  hasInvalidDateRange(): boolean {
    return !!this.fromDate && !!this.toDate && this.fromDate > this.toDate;
  }

  formatValue(value: unknown): string {
    if (value === null || value === undefined || value === '') return '-';
    if (typeof value === 'boolean') return value ? 'Yes' : 'No';
    if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(value)) {
      return new Date(value).toLocaleString();
    }
    return String(value);
  }

  private buildParams(): HttpParams {
    let params = new HttpParams();
    if (this.supportsDateCriteria()) {
      if (this.fromDate) params = params.set('from', this.fromDate);
      if (this.toDate) params = params.set('to', this.toDate);
    }
    if (this.supportsStatusCriteria() && this.status) params = params.set('status', this.status);
    if (this.supportsCrimeCategoryFilter() && this.crimeCategory) {
      params = params.set('crimeCategory', this.crimeCategory);
    }
    return params;
  }

  private groupRows(rows: Record<string, unknown>[], key: string): Array<{ label: string; rows: Record<string, unknown>[] }> {
    const groups = new Map<string, Record<string, unknown>[]>();
    for (const row of rows) {
      const label = this.formatValue(row[key]);
      groups.set(label, [...(groups.get(label) ?? []), row]);
    }
    return Array.from(groups, ([label, groupedRows]) => ({ label, rows: groupedRows }));
  }
}
