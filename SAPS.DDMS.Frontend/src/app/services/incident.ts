// src/app/services/incident.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '../config/api.config';

@Injectable({ providedIn: 'root' })
export class IncidentService {
    private http = inject(HttpClient);
    
    getIncidentTypes(): Observable<any[]> {
        return this.http.get<any[]>(`${API_URL}/incident/types`);
    }
    
    getGeoLocations(): Observable<any[]> {
        return this.http.get<any[]>(`${API_URL}/incident/locations`);
    }
    
    getIncidentById(id: number): Observable<any> {
        return this.http.get<any>(`${API_URL}/incident/${id}`);
    }
}