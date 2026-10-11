// src/app/services/docket.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '../config/api.config';
import { Docket, CreateDocketDto, UpdateDocketDto, AllocateDocketDto, ReassignDocketDto } from '../models/docket.model';

export interface CaseReadinessIntelligence {
    docketId: number;
    docketNumber: string;
    generatedAtUtc: string;
    score: number;
    readinessBand: 'Low' | 'Moderate' | 'High';
    slaState: string;
    slaDueAt?: string | null;
    metrics: {
        evidenceCount: number;
        hashedEvidenceCount: number;
        integrityCoverage: number;
        custodyControlCoverage: number;
        evidenceTransferCount: number;
        activeInvestigationNotes: number;
        assignmentAccepted: boolean;
        inspectionCompleted: boolean;
        prosecutorBundleStatus: string;
    };
    blockers: string[];
    recommendations: string[];
    limitations: string;
}

@Injectable({ providedIn: 'root' })
export class DocketService {
    private http = inject(HttpClient);

    getDockets(status?: string): Observable<Docket[]> {
        const url = status ? `${API_URL}/docket?status=${status}` : `${API_URL}/docket`;
        return this.http.get<Docket[]>(url);
    }

    getDocketById(id: number): Observable<Docket> {
        return this.http.get<Docket>(`${API_URL}/docket/${id}`);
    }

    createDocket(docket: CreateDocketDto): Observable<any> {
        return this.http.post(`${API_URL}/docket`, docket);
    }

    updateDocket(id: number, docket: UpdateDocketDto): Observable<any> {
        return this.http.put(`${API_URL}/docket/${id}`, docket);
    }

    allocateDocket(id: number, allocation: AllocateDocketDto): Observable<any> {
        return this.http.post(`${API_URL}/docket/${id}/allocate`, allocation);
    }

    reassignDocket(id: number, reassignment: ReassignDocketDto): Observable<any> {
        return this.http.patch(`${API_URL}/docket/${id}/assignment/reassign`, reassignment);
    }

    acceptAssignment(id: number): Observable<any> {
        return this.http.patch(`${API_URL}/docket/${id}/assignment/accept`, {});
    }

    rejectAssignment(id: number, reason: string): Observable<any> {
        return this.http.patch(`${API_URL}/docket/${id}/assignment/reject`, { reason });
    }

    reviewAllocation(id: number, decision: string, reason: string, investigatingOfficerUserID?: number): Observable<any> {
        return this.http.patch(`${API_URL}/docket/${id}/assignment/review`, {
            decision,
            reason,
            investigatingOfficerUserID
        });
    }

    completeInspection(id: number, notes: string): Observable<any> {
        return this.http.patch(`${API_URL}/docket/${id}/inspection/complete`, { notes });
    }

    transitionStatus(id: number, status: string, reason: string): Observable<any> {
        return this.http.patch(`${API_URL}/docket/${id}/status`, { status, reason });
    }

    closeCase(id: number, reason: string): Observable<any> {
        return this.http.patch(`${API_URL}/docket/${id}/close`, { reason });
    }

    reopenCase(id: number, reason: string): Observable<any> {
        return this.http.patch(`${API_URL}/docket/${id}/reopen`, { reason });
    }

    getProsecutorPreview(id: number): Observable<any> {
        return this.http.get(`${API_URL}/docket/${id}/prosecutor-preview`);
    }

    getProsecutorBundles(id: number): Observable<any[]> {
        return this.http.get<any[]>(`${API_URL}/docket/${id}/prosecutor-bundles`);
    }

    getProsecutorBundle(bundleId: number): Observable<any> {
        return this.http.get(`${API_URL}/prosecutor-bundles/${bundleId}`);
    }

    createProsecutorBundle(id: number, payload: { title: string; notes?: string }): Observable<any> {
        return this.http.post(`${API_URL}/docket/${id}/prosecutor-bundles`, payload);
    }

    updateProsecutorBundle(bundleId: number, payload: { title?: string; notes?: string; aiSummaryText?: string; includeNotes?: boolean; includeEvidence?: boolean; reason?: string }): Observable<any> {
        return this.http.put(`${API_URL}/prosecutor-bundles/${bundleId}`, payload);
    }

    getEligibleProsecutors(docketId: number): Observable<any[]> {
        return this.http.get<any[]>(`${API_URL}/docket/${docketId}/eligible-prosecutors`);
    }

    previewBundleShare(bundleId: number): Observable<any> {
        return this.http.get(`${API_URL}/prosecutor-bundles/${bundleId}/share-preview`);
    }

    shareProsecutorBundle(bundleId: number, prosecutorUserID: number, note?: string): Observable<any> {
        return this.http.post(`${API_URL}/prosecutor-bundles/${bundleId}/share`, { prosecutorUserID, note });
    }

    exportProsecutorBundle(bundleId: number, format = 'PDF'): Observable<any> {
        return this.http.post(`${API_URL}/prosecutor-bundles/${bundleId}/export`, { format });
    }

    downloadProsecutorBundle(bundleId: number, format = 'PDF'): Observable<Blob> {
        return this.http.get(`${API_URL}/prosecutor-bundles/${bundleId}/download`, {
            params: { format },
            responseType: 'blob'
        });
    }

    setPrivacyFlag(id: number, isPrivate: boolean, reason: string): Observable<any> {
        return this.http.patch(`${API_URL}/docket/${id}/privacy-flag`, { isPrivate, reason });
    }

    setBundleItemPrivacyFlag(bundleId: number, itemId: number, isPrivate: boolean, reason: string): Observable<any> {
        return this.http.patch(`${API_URL}/prosecutor-bundles/${bundleId}/items/${itemId}/privacy-flag`, {
            isPrivate,
            reason
        });
    }

    getBundleItemPrivacyFlagHistory(itemId: number): Observable<any> {
        return this.http.get<any>(`${API_URL}/docket-privacy-flags/${itemId}/history`);
    }

    getPrivacyFlagHistory(id: number): Observable<any[]> {
        return this.http.get<any[]>(`${API_URL}/docket/${id}/privacy-flag/history`);
    }

    getAvailableOfficers(): Observable<any[]> {
        return this.http.get<any[]>(`${API_URL}/docket/available-officers`);
    }

    // UC 3.4 - Archive Docket
    archiveDocket(id: number): Observable<any> {
        return this.http.patch(`${API_URL}/docket/${id}/archive`, {});
    }

    // UC 8.1 - Record Court Outcome
    recordCourtOutcome(id: number, payload: { outcome: string; courtDate: string; presidingOfficer: string; sentence?: string; notes?: string }): Observable<any> {
        return this.http.post(`${API_URL}/docket/${id}/court-outcome`, payload);
    }

    generateAiSummary(id: number): Observable<any> {
        return this.http.post(`${API_URL}/ai/docket/${id}/summary`, {});
    }

    confirmAiSummary(id: number, summary: string): Observable<any> {
        return this.http.post(`${API_URL}/ai/docket/${id}/summary/confirm`, { summary });
    }

    extractAiData(id: number): Observable<{ extraction: Record<string, unknown>; requiresHumanVerification: boolean }> {
        return this.http.post<{ extraction: Record<string, unknown>; requiresHumanVerification: boolean }>(
            `${API_URL}/ai/docket/${id}/extract`, {}
        );
    }

    verifyAiExtraction(id: number, payload: any): Observable<any> {
        return this.http.post(`${API_URL}/ai/docket/${id}/verify-extractions`, payload);
    }

    suggestRelatedCases(id: number): Observable<any[]> {
        return this.http.post<any[]>(`${API_URL}/ai/docket/${id}/related-cases`, {});
    }

    confirmRelatedCases(id: number, relatedCases: any[]): Observable<any> {
        return this.http.post(`${API_URL}/ai/docket/${id}/related-cases/confirm`, { relatedCases });
    }

    generateReadinessIntelligence(id: number): Observable<CaseReadinessIntelligence> {
        return this.http.post<CaseReadinessIntelligence>(
            `${API_URL}/ai/docket/${id}/readiness-intelligence`, {}
        );
    }
}
