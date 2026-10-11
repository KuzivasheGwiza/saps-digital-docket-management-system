import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from '../config/api.config';

export interface Faq {
  faqID: number;
  question: string;
  answer: string;
  createdAt: Date;
  isDeleted: boolean;
  isPublished: boolean;
}

export interface CreateFaqDto { question: string; answer: string; isPublished: boolean; }
export interface UpdateFaqDto { question: string; answer: string; isPublished: boolean; }

@Injectable({ providedIn: 'root' })
export class FaqService {
  private http = inject(HttpClient);

  getFaqs(): Observable<Faq[]> {
    return this.http.get<Faq[]>(`${API_URL}/faq`);
  }

  createFaq(dto: CreateFaqDto): Observable<any> {
    return this.http.post(`${API_URL}/faq`, dto);
  }

  updateFaq(id: number, dto: UpdateFaqDto): Observable<any> {
    return this.http.put(`${API_URL}/faq/${id}`, dto);
  }

  deleteFaq(id: number): Observable<any> {
    return this.http.delete(`${API_URL}/faq/${id}`);
  }
}
