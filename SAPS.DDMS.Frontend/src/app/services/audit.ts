import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '../config/api.config';

export interface AuditLogFilters {
  query?: string;
  action?: string;
  userId?: number | null;
  from?: string;
  to?: string;
}

@Injectable({ providedIn: 'root' })
export class AuditService {
  private http = inject(HttpClient);

  getLogs(filters: AuditLogFilters = {}): Observable<any[]> {
    return this.http.get<any[]>(`${API_URL}/audit/logs`, {
      // Audit snapshots are wide, so the initial page is deliberately small.
      // Server-side filters and CSV export still cover the complete result set.
      params: this.buildParams(filters).set('pageSize', '25')
    });
  }

  exportLogs(filters: AuditLogFilters = {}): Observable<Blob> {
    return this.http.get(`${API_URL}/audit/export`, {
      params: this.buildParams(filters),
      responseType: 'blob'
    });
  }

  private buildParams(filters: AuditLogFilters): HttpParams {
    let params = new HttpParams();
    if (filters.query?.trim()) params = params.set('q', filters.query.trim());
    if (filters.action?.trim()) params = params.set('action', filters.action.trim());
    if (filters.userId != null) params = params.set('userId', filters.userId.toString());
    if (filters.from?.trim()) params = params.set('from', filters.from.trim());
    if (filters.to?.trim()) params = params.set('to', filters.to.trim());
    return params;
  }
}
