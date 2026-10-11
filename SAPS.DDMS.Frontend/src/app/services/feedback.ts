import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '../config/api.config';

export interface FeedbackItem {
  feedbackID: number;
  fk_UserID: number;
  userName: string;
  subject: string;
  message: string;
  response?: string;
  status: string;
  createdAt: string;
  respondedAt?: string;
}

@Injectable({ providedIn: 'root' })
export class FeedbackService {
  private http = inject(HttpClient);

  getMine(): Observable<FeedbackItem[]> {
    return this.http.get<FeedbackItem[]>(`${API_URL}/feedback/my`);
  }

  getAll(): Observable<FeedbackItem[]> {
    return this.http.get<FeedbackItem[]>(`${API_URL}/feedback`);
  }

  submit(subject: string, message: string): Observable<FeedbackItem> {
    return this.http.post<FeedbackItem>(`${API_URL}/feedback`, { subject, message });
  }

  respond(id: number, response: string): Observable<any> {
    return this.http.patch(`${API_URL}/feedback/${id}/respond`, { response });
  }
}
