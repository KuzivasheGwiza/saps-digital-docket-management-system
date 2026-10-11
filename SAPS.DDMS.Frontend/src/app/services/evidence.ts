// src/app/services/evidence.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '../config/api.config';
import { Evidence, UpdateEvidenceDto } from '../models/evidence.model';

@Injectable({ providedIn: 'root' })
export class EvidenceService {
    private http = inject(HttpClient);
    
    getEvidenceByDocket(docketId: number): Observable<Evidence[]> {
        return this.http.get<Evidence[]>(`${API_URL}/evidence/docket/${docketId}`);
    }
    
    getEvidenceById(id: number): Observable<Evidence> {
        return this.http.get<Evidence>(`${API_URL}/evidence/${id}`);
    }
    
    createEvidence(formData: FormData): Observable<any> {
        return this.http.post(`${API_URL}/evidence`, formData);
    }
    
    updateEvidence(id: number, evidence: UpdateEvidenceDto): Observable<any> {
        return this.http.put(`${API_URL}/evidence/${id}`, evidence);
    }

    recordTransfer(id: number, transfer: any): Observable<any> {
        return this.http.post(`${API_URL}/evidence/${id}/transfers`, transfer);
    }

    getChainOfCustody(id: number): Observable<any[]> {
        return this.http.get<any[]>(`${API_URL}/evidence/${id}/chain-of-custody`);
    }
    
    getEvidenceTypes(): Observable<any[]> {
        return this.http.get<any[]>(`${API_URL}/evidence/types`);
    }
    
    getEvidenceLocations(): Observable<any[]> {
        return this.http.get<any[]>(`${API_URL}/evidence/locations`);
    }

    getEvidenceImage(evidenceId: number): Observable<Blob> {
        return this.http.get(`${API_URL}/files/evidence/${evidenceId}`, { responseType: 'blob' });
    }
}
