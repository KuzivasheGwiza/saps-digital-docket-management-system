import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '../config/api.config';
import { InvestigationNote, UpdateInvestigationNoteDto } from '../models/investigation-note.model';

@Injectable({ providedIn: 'root' })
export class InvestigationNoteService {
    private http = inject(HttpClient);

    getInvestigationNotes(investigationId: number): Observable<InvestigationNote[]> {
        return this.http.get<InvestigationNote[]>(`${API_URL}/investigation/${investigationId}/notes`);
    }

    createNote(formData: FormData): Observable<any> {
        return this.http.post(`${API_URL}/investigation/notes`, formData);
    }

    updateNote(noteId: number, note: UpdateInvestigationNoteDto): Observable<any> {
        return this.http.put(`${API_URL}/investigation/notes/${noteId}`, note);
    }

    getNoteVersions(noteId: number): Observable<any[]> {
        return this.http.get<any[]>(`${API_URL}/investigation/notes/${noteId}/versions`);
    }

    requestArchiveNote(noteId: number, reason: string): Observable<any> {
        return this.http.post(`${API_URL}/investigation/notes/${noteId}/archive-request`, { reason });
    }

    getArchiveRequests(): Observable<any[]> {
        return this.http.get<any[]>(`${API_URL}/investigation/notes/archive-requests`);
    }

    reviewArchiveRequest(requestId: number, decision: string, rejectionReason?: string): Observable<any> {
        return this.http.patch(`${API_URL}/investigation/notes/archive-requests/${requestId}/review`, { decision, rejectionReason });
    }

    downloadAttachment(attachmentId: number): Observable<Blob> {
        return this.http.get(`${API_URL}/files/note-attachments/${attachmentId}`, { responseType: 'blob' });
    }
}
