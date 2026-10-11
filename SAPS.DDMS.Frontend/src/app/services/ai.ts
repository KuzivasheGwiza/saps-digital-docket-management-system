import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '../config/api.config';

@Injectable({ providedIn: 'root' })
export class AiService {
  private http = inject(HttpClient);

  getLogs(docketId?: number): Observable<any[]> {
    const url = docketId ? `${API_URL}/ai/logs?docketId=${docketId}` : `${API_URL}/ai/logs`;
    return this.http.get<any[]>(url);
  }
}
