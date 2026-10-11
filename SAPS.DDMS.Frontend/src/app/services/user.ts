// src/app/services/user.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { API_URL } from '../config/api.config';
import { UserResponse, CreateUserDto, UpdateUserDto, Role, Station } from '../models/user.model';

@Injectable({ providedIn: 'root' })
export class UserService {
    private http = inject(HttpClient);

    getAllUsers(roleId?: number, isActive?: boolean): Observable<UserResponse[]> {
        let params = new HttpParams();
        if (roleId) params = params.set('roleId', roleId.toString());
        if (isActive !== undefined) params = params.set('isActive', isActive.toString());

        return this.http.get<UserResponse[]>(`${API_URL}/user`, { params }).pipe(
            catchError(error => {
                console.error('Get all users error:', error);
                return throwError(() => error);
            })
        );
    }

    getUserById(id: number): Observable<UserResponse> {
        return this.http.get<UserResponse>(`${API_URL}/user/${id}`).pipe(
            catchError(error => {
                console.error('Get user by id error:', error);
                return throwError(() => error);
            })
        );
    }

    createUser(user: CreateUserDto): Observable<any> {
        console.log('API URL:', `${API_URL}/user`);
        console.log('Create User Payload:', user);

        return this.http.post(`${API_URL}/user`, user).pipe(
            catchError(error => {
                console.error('Create user error details:', error);
                if (error.error) {
                    console.error('Error body:', error.error);
                }
                return throwError(() => error);
            })
        );
    }

    resendActivationEmail(id: number): Observable<any> {
        return this.http.post(`${API_URL}/user/${id}/resend-activation`, {}).pipe(
            catchError(error => {
                console.error('Resend activation error:', error);
                return throwError(() => error);
            })
        );
    }

    updateUser(id: number, user: UpdateUserDto): Observable<any> {
        return this.http.put(`${API_URL}/user/${id}`, user).pipe(
            catchError(error => {
                console.error('Update user error:', error);
                return throwError(() => error);
            })
        );
    }

    deactivateUser(id: number, reason: string): Observable<any> {
        return this.http.patch(`${API_URL}/user/${id}/deactivate`, { reason });
    }

    reactivateUser(id: number, reason: string): Observable<any> {
        return this.http.patch(`${API_URL}/user/${id}/reactivate`, { reason });
    }

    suspendUser(id: number, reason: string): Observable<any> {
        return this.http.patch(`${API_URL}/user/${id}/suspend`, { reason });
    }

    unlockUser(id: number, reason: string): Observable<any> {
        return this.http.patch(`${API_URL}/user/${id}/unlock`, { reason });
    }

    getRoleHistory(id: number): Observable<any[]> {
        return this.http.get<any[]>(`${API_URL}/user/${id}/role-history`);
    }

    getRoles(): Observable<Role[]> {
        return this.http.get<Role[]>(`${API_URL}/user/roles`).pipe(
            catchError(error => {
                console.error('Get roles error:', error);
                return throwError(() => error);
            })
        );
    }

    getStations(): Observable<Station[]> {
        return this.http.get<Station[]>(`${API_URL}/user/stations`).pipe(
            catchError(error => {
                console.error('Get stations error:', error);
                return throwError(() => error);
            })
        );
    }

    getCities(): Observable<Array<{ id: number; province: string; name: string }>> {
        return this.http.get<Array<{ id: number; province: string; name: string }>>(`${API_URL}/user/cities`).pipe(
            catchError(error => {
                console.error('Get cities error:', error);
                return throwError(() => error);
            })
        );
    }

    getProfile(): Observable<UserResponse> {
        return this.http.get<UserResponse>(`${API_URL}/user/profile`).pipe(
            catchError(error => {
                console.error('Get profile error:', error);
                return throwError(() => error);
            })
        );
    }

    getProfilePhoto(): Observable<Blob> {
        return this.http.get(`${API_URL}/user/profile/photo`, { responseType: 'blob' });
    }

    uploadProfilePhoto(photo: File): Observable<any> {
        const formData = new FormData();
        formData.append('photo', photo);
        return this.http.post(`${API_URL}/user/profile/photo`, formData);
    }

    // UC 2.5 - Update Profile (phone number + notification preferences)
    updateMe(payload: { phoneNumber: string; notificationPreferences?: string }): Observable<any> {
        return this.http.patch(`${API_URL}/user/me`, payload).pipe(
            catchError(error => {
                console.error('Update profile error:', error);
                return throwError(() => error);
            })
        );
    }
}
