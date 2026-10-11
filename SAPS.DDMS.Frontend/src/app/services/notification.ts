import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '../config/api.config';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private http = inject(HttpClient);

  getNotifications(unreadOnly = false): Observable<any[]> {
    return this.http.get<any[]>(`${API_URL}/notification`, { params: { unreadOnly } as any });
  }

  getUnreadCount(): Observable<{ count: number }> {
    return this.http.get<{ count: number }>(`${API_URL}/notification/unread-count`);
  }

  markRead(id: number): Observable<any> {
    return this.http.patch(`${API_URL}/notification/${id}/read`, {});
  }
}
