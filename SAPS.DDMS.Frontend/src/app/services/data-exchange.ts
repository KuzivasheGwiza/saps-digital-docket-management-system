import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_URL } from '../config/api.config';

export type DataExchangeDataset = 'dockets' | 'users' | 'evidence' | 'audit';
export type DataExchangeFormat = 'xlsx' | 'json' | 'xml' | 'doc';
export type LookupImportType = 'evidence-types' | 'incident-types' | 'evidence-locations';

export interface DataExchangeFilters {
  from?: string;
  to?: string;
  stationId?: number | null;
  status?: string;
  roleId?: number | null;
  isActive?: boolean | null;
  docketId?: number | null;
  userId?: number | null;
  actionType?: string;
}

export interface LookupImportRecord {
  name: string;
  description?: string;
  physicalAddress?: string;
  storageRequirements?: string;
}

export interface LookupImportDocument {
  schemaVersion: 1;
  lookupType: LookupImportType;
  records: LookupImportRecord[];
  previewToken?: string;
  confirmApply?: boolean;
}

export interface LookupImportPreviewRow {
  name: string;
  action: 'Create' | 'Update' | 'Unchanged';
  details: string;
}

export interface LookupImportPreview {
  schemaVersion: number;
  lookupType: LookupImportType;
  contentHash: string;
  totalRecords: number;
  createCount: number;
  updateCount: number;
  unchangedCount: number;
  canApply: boolean;
  previewToken?: string;
  previewExpiresAtUtc?: string;
  errors: string[];
  rows: LookupImportPreviewRow[];
}

export interface LookupImportApplyResult {
  success: boolean;
  message: string;
  lookupType: LookupImportType;
  created: number;
  updated: number;
  unchanged: number;
  contentHash: string;
}

@Injectable({ providedIn: 'root' })
export class DataExchangeService {
  private readonly http = inject(HttpClient);

  exportFile(
    dataset: DataExchangeDataset,
    format: DataExchangeFormat,
    filters: DataExchangeFilters
  ): Observable<HttpResponse<Blob>> {
    let params = new HttpParams().set('format', format);
    for (const [key, rawValue] of Object.entries(filters)) {
      const value = typeof rawValue === 'string' ? rawValue.trim() : rawValue;
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }

    return this.http.get(`${API_URL}/data-exchange/export/${dataset}`, {
      params,
      observe: 'response',
      responseType: 'blob'
    });
  }

  previewLookupImport(document: LookupImportDocument): Observable<LookupImportPreview> {
    return this.http.post<LookupImportPreview>(
      `${API_URL}/data-exchange/lookups/import/preview`,
      this.withoutImportConfirmation(document)
    );
  }

  applyLookupImport(
    document: LookupImportDocument,
    previewToken: string
  ): Observable<LookupImportApplyResult> {
    return this.http.post<LookupImportApplyResult>(
      `${API_URL}/data-exchange/lookups/import/apply`,
      {
        ...this.withoutImportConfirmation(document),
        previewToken,
        confirmApply: true
      }
    );
  }

  private withoutImportConfirmation(document: LookupImportDocument): LookupImportDocument {
    return {
      schemaVersion: 1,
      lookupType: document.lookupType,
      records: document.records.map(record => ({ ...record }))
    };
  }
}
