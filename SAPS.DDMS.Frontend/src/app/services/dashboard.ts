import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '../config/api.config';
import { DashboardStats } from '../models/dashboard.model';

@Injectable({ providedIn: 'root' })
export class DashboardService {
    private http = inject(HttpClient);
    
    getDashboardStats(): Observable<DashboardStats> {
        return this.http.get<DashboardStats>(`${API_URL}/dashboard/stats`);
    }
}
