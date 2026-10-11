import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../services/auth';
import {
  DataExchangeDataset,
  DataExchangeFilters,
  DataExchangeFormat,
  DataExchangeService,
  LookupImportApplyResult,
  LookupImportDocument,
  LookupImportPreview,
  LookupImportType
} from '../../services/data-exchange';
import { UserService } from '../../services/user';
import { Role, Station } from '../../models/user.model';
import { hasPermission, PERMISSIONS } from '../../security/role-policy';

interface DatasetOption {
  key: DataExchangeDataset;
  label: string;
  description: string;
}

@Component({
  selector: 'app-data-exchange',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './data-exchange.html',
  styleUrls: ['./data-exchange.css']
})
export class DataExchange implements OnInit {
  private readonly dataExchange = inject(DataExchangeService);
  private readonly userService = inject(UserService);
  private readonly authService = inject(AuthService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly datasets: DatasetOption[] = [
    { key: 'audit', label: 'Audit trail', description: 'Who performed each audited action and when.' },
    { key: 'dockets', label: 'Docket register', description: 'Operational docket status and allocation data.' },
    { key: 'evidence', label: 'Evidence register', description: 'Evidence metadata, storage, collection, and docket linkage.' },
    { key: 'users', label: 'Personnel register', description: 'Authorised users, roles, stations, and account state.' }
  ];
  readonly statuses = [
    'OPEN',
    'ALLOCATED',
    'UNDER_INVESTIGATION',
    'PENDING_PROSECUTOR',
    'CLOSED',
    'ARCHIVED',
    'REOPENED'
  ];
  readonly lookupTypes: { key: LookupImportType; label: string }[] = [
    { key: 'evidence-locations', label: 'Evidence locations' },
    { key: 'evidence-types', label: 'Evidence types' },
    { key: 'incident-types', label: 'Incident types' }
  ];

  selectedDataset: DataExchangeDataset = 'dockets';
  selectedFormat: DataExchangeFormat = 'xlsx';
  fromDate = '';
  toDate = '';
  stationId: number | null = null;
  status = '';
  roleId: number | null = null;
  activeFilter: '' | 'true' | 'false' = '';
  docketId: number | null = null;
  userId: number | null = null;
  actionType = '';

  stations: Station[] = [];
  roles: Role[] = [];
  lookupLoadWarning = '';
  isExporting = false;
  exportError = '';
  exportSuccess = '';

  templateLookupType: LookupImportType = 'incident-types';
  templateImportFormat: 'json' | 'xml' = 'json';
  selectedFileName = '';
  importDocument: LookupImportDocument | null = null;
  importPreview: LookupImportPreview | null = null;
  importResult: LookupImportApplyResult | null = null;
  importError = '';
  isReadingFile = false;
  isPreviewing = false;
  isApplying = false;
  applyConfirmed = false;

  get isAuthorised(): boolean {
    const user = this.authService.getCurrentUser();
    return user?.roleName === 'System Administrator' ||
      hasPermission(user?.permissions, PERMISSIONS.dataExchangeManage);
  }

  get selectedDatasetOption(): DatasetOption {
    return this.datasets.find(option => option.key === this.selectedDataset) ?? this.datasets[0];
  }

  get hasInvalidDateRange(): boolean {
    return !!this.fromDate && !!this.toDate && this.fromDate > this.toDate;
  }

  get canApplyImport(): boolean {
    return !!this.importDocument &&
      !!this.importPreview?.canApply &&
      !!this.importPreview.previewToken &&
      this.applyConfirmed &&
      !this.isApplying;
  }

  ngOnInit(): void {
    if (!this.isAuthorised) return;

    forkJoin({
      stations: this.userService.getStations(),
      roles: this.userService.getRoles()
    }).subscribe({
      next: result => {
        this.stations = result.stations || [];
        this.roles = result.roles || [];
        this.cdr.detectChanges();
      },
      error: () => {
        this.lookupLoadWarning = 'Station and role filters are temporarily unavailable. Other filters still work.';
        this.cdr.detectChanges();
      }
    });
  }

  exportFile(): void {
    if (this.hasInvalidDateRange || this.isExporting) return;
    this.isExporting = true;
    this.exportError = '';
    this.exportSuccess = '';

    this.dataExchange.exportFile(
      this.selectedDataset,
      this.selectedFormat,
      this.activeFilters()
    ).subscribe({
      next: response => {
        const blob = response.body;
        if (!blob || blob.size === 0) {
          this.exportError = 'The export completed without a file.';
          this.isExporting = false;
          return;
        }

        const fallback = `saps-ddms-${this.selectedDataset}-${new Date().toISOString().slice(0, 10)}.${this.selectedFormat}`;
        this.downloadBlob(blob, this.responseFileName(response.headers.get('content-disposition')) || fallback);
        this.exportSuccess = `${this.selectedDatasetOption.label} exported as ${this.selectedFormat.toUpperCase()}.`;
        this.isExporting = false;
        this.cdr.detectChanges();
      },
      error: error => {
        this.exportError = this.apiError(error, 'The data export could not be created.');
        this.isExporting = false;
        this.cdr.detectChanges();
      }
    });
  }

  async selectImportFile(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    this.resetImportState();
    if (!file) return;

    this.selectedFileName = file.name;
    const lowerName = file.name.toLowerCase();
    if (!lowerName.endsWith('.json') && !lowerName.endsWith('.xml')) {
      this.importError = 'Select a .json or .xml lookup file.';
      input.value = '';
      return;
    }
    if (file.size > 1_048_576) {
      this.importError = 'The JSON file may not exceed 1 MB.';
      input.value = '';
      return;
    }

    this.isReadingFile = true;
    try {
      const text = await file.text();
      const parsed = lowerName.endsWith('.xml')
        ? this.parseLookupXml(text)
        : JSON.parse(text) as Partial<LookupImportDocument>;
      if (parsed.schemaVersion !== 1 ||
          !this.isLookupType(parsed.lookupType) ||
          !Array.isArray(parsed.records) ||
          parsed.records.length === 0) {
        throw new Error('Invalid lookup document');
      }
      this.importDocument = {
        schemaVersion: 1,
        lookupType: parsed.lookupType,
        records: parsed.records
      };
    } catch {
      this.importError = 'The file is not a valid SAPS DDMS lookup JSON or XML document.';
      this.importDocument = null;
      input.value = '';
    } finally {
      this.isReadingFile = false;
      this.cdr.detectChanges();
    }
  }

  previewImport(): void {
    if (!this.importDocument || this.isPreviewing) return;
    this.isPreviewing = true;
    this.importError = '';
    this.importResult = null;
    this.importPreview = null;
    this.applyConfirmed = false;

    this.dataExchange.previewLookupImport(this.importDocument).subscribe({
      next: preview => {
        this.importPreview = preview;
        this.isPreviewing = false;
        this.cdr.detectChanges();
      },
      error: error => {
        this.importError = this.apiError(error, 'The lookup import could not be previewed.');
        this.isPreviewing = false;
        this.cdr.detectChanges();
      }
    });
  }

  applyImport(): void {
    const token = this.importPreview?.previewToken;
    if (!this.importDocument || !token || !this.canApplyImport) return;
    this.isApplying = true;
    this.importError = '';
    this.importResult = null;

    this.dataExchange.applyLookupImport(this.importDocument, token).subscribe({
      next: result => {
        this.importResult = result;
        this.isApplying = false;
        this.applyConfirmed = false;
        this.importPreview = null;
        this.cdr.detectChanges();
      },
      error: error => {
        this.importError = this.apiError(error, 'The lookup import could not be applied.');
        this.isApplying = false;
        this.cdr.detectChanges();
      }
    });
  }

  downloadTemplate(): void {
    const records = this.templateLookupType === 'incident-types'
      ? [{ name: 'Example incident type', description: 'Replace or remove this example.' }]
      : this.templateLookupType === 'evidence-types'
        ? [{ name: 'Example type' }]
        : [{
            name: 'Example evidence store',
            physicalAddress: 'Replace with the authorised address',
            storageRequirements: 'Secure, access-controlled storage'
          }];
    const document: LookupImportDocument = {
      schemaVersion: 1,
      lookupType: this.templateLookupType,
      records
    };
    if (this.templateImportFormat === 'xml') {
      const recordsXml = records.map(record => {
        const fields = Object.entries(record)
          .map(([key, value]) => `<${key}>${this.escapeXml(String(value))}</${key}>`)
          .join('');
        return `<record>${fields}</record>`;
      }).join('');
      const xml = `<?xml version="1.0" encoding="UTF-8"?><lookupImport schemaVersion="1" lookupType="${this.templateLookupType}"><records>${recordsXml}</records></lookupImport>`;
      this.downloadBlob(new Blob([xml], { type: 'application/xml' }), `saps-ddms-${this.templateLookupType}-template.xml`);
      return;
    }
    const blob = new Blob([JSON.stringify(document, null, 2)], { type: 'application/json' });
    this.downloadBlob(blob, `saps-ddms-${this.templateLookupType}-template.json`);
  }

  actionClass(action: string): string {
    return `action-${action.toLowerCase()}`;
  }

  private activeFilters(): DataExchangeFilters {
    const filters: DataExchangeFilters = {
      from: this.fromDate,
      to: this.toDate,
      stationId: this.stationId
    };
    if (this.selectedDataset === 'dockets') filters.status = this.status;
    if (this.selectedDataset === 'users') {
      filters.roleId = this.roleId;
      filters.isActive = this.activeFilter === '' ? null : this.activeFilter === 'true';
    }
    if (this.selectedDataset === 'evidence') filters.docketId = this.docketId;
    if (this.selectedDataset === 'audit') {
      filters.userId = this.userId;
      filters.actionType = this.actionType;
    }
    return filters;
  }

  private resetImportState(): void {
    this.selectedFileName = '';
    this.importDocument = null;
    this.importPreview = null;
    this.importResult = null;
    this.importError = '';
    this.applyConfirmed = false;
  }

  private isLookupType(value: unknown): value is LookupImportType {
    return value === 'incident-types' || value === 'evidence-types' || value === 'evidence-locations';
  }

  private parseLookupXml(text: string): Partial<LookupImportDocument> {
    const xml = new DOMParser().parseFromString(text, 'application/xml');
    if (xml.querySelector('parsererror')) throw new Error('Invalid XML');
    const root = xml.documentElement;
    const lookupType = root.getAttribute('lookupType');
    const schemaVersion = Number(root.getAttribute('schemaVersion'));
    const records = Array.from(root.querySelectorAll('records > record')).map(record => ({
      name: record.querySelector('name')?.textContent?.trim() ?? '',
      description: record.querySelector('description')?.textContent?.trim() || undefined,
      physicalAddress: record.querySelector('physicalAddress')?.textContent?.trim() || undefined,
      storageRequirements: record.querySelector('storageRequirements')?.textContent?.trim() || undefined
    }));
    return { schemaVersion: schemaVersion as 1, lookupType: lookupType as LookupImportType, records };
  }

  private escapeXml(value: string): string {
    return value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&apos;');
  }

  private responseFileName(contentDisposition: string | null): string | null {
    if (!contentDisposition) return null;
    const encoded = /filename\*=UTF-8''([^;]+)/i.exec(contentDisposition)?.[1];
    if (encoded) {
      try { return decodeURIComponent(encoded.replace(/^"|"$/g, '')); } catch { return null; }
    }
    return /filename="?([^";]+)"?/i.exec(contentDisposition)?.[1] ?? null;
  }

  private downloadBlob(blob: Blob, fileName: string): void {
    const objectUrl = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = objectUrl;
    anchor.download = fileName.replace(/[^a-zA-Z0-9._-]/g, '_');
    anchor.style.display = 'none';
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(objectUrl);
  }

  private apiError(error: any, fallback: string): string {
    if (typeof error?.error?.message === 'string') return error.error.message;
    const validationErrors = error?.error?.errors;
    if (validationErrors && typeof validationErrors === 'object') {
      const first = Object.values(validationErrors).flat().find(value => typeof value === 'string');
      if (typeof first === 'string') return first;
    }
    return fallback;
  }
}
