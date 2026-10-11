// src/app/services/person.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { API_URL } from '../config/api.config';
import { Person, CreatePersonDto, UpdatePersonDto, LinkPersonToDocketDto } from '../models/person.model';

@Injectable({ providedIn: 'root' })
export class PersonService {
    private http = inject(HttpClient);

    private normalizeRole(role: any): any {
        return {
            ...role,
            personRoleID: role?.personRoleID ?? role?.personRoleId ?? role?.PersonRoleID,
            roleName: role?.roleName ?? role?.RoleName,
            code: role?.code ?? role?.Code,
            description: role?.description ?? role?.Description,
            isActive: role?.isActive ?? role?.IsActive
        };
    }

    private normalizePerson(person: any): Person {
        return {
            ...person,
            personID: person?.personID ?? person?.personId ?? person?.PersonID,
            fullName: person?.fullName ?? `${person?.firstName ?? ''} ${person?.lastName ?? ''}`.trim(),
            idNumber: person?.idNumber ?? person?.iDNumber ?? person?.IDNumber,
            maskedIdNumber: person?.maskedIdNumber ?? person?.maskedIDNumber ?? person?.MaskedIDNumber,
            createdAt: person?.createdAt ?? person?.CreatedAt
        };
    }
    
    searchPersons(searchTerm: string): Observable<Person[]> {
        // If search term is empty, return empty array immediately to avoid API call
        if (!searchTerm || searchTerm.trim() === '') {
            return of([]);
        }
        
        let params = new HttpParams();
        params = params.set('searchTerm', searchTerm.trim());
        
        return this.http.get<Person[]>(`${API_URL}/person`, { params }).pipe(
            map(persons => (persons || []).map(person => this.normalizePerson(person))),
            catchError(error => {
                console.error('Search persons error:', error);
                // Return empty array on error
                return of([]);
            })
        );
    }
    
    getAllPersons(): Observable<Person[]> {
        // An empty term returns the first authorised page of people (up to 50)
        // in the same scope and alphabetical order as the server-side register.
        return this.http.get<Person[]>(`${API_URL}/person`).pipe(
            map(persons => (persons || []).map(person => this.normalizePerson(person))),
            catchError(() => of([]))
        );
    }
    
    getPersonById(id: number): Observable<Person> {
        return this.http.get<Person>(`${API_URL}/person/${id}`).pipe(
            map(person => this.normalizePerson(person))
        );
    }
    
    createPerson(person: CreatePersonDto): Observable<any> {
        return this.http.post(`${API_URL}/person`, person);
    }
    
    updatePerson(id: number, person: UpdatePersonDto): Observable<any> {
        return this.http.put(`${API_URL}/person/${id}`, person);
    }
    
    linkPersonToDocket(link: LinkPersonToDocketDto): Observable<any> {
        return this.http.post(`${API_URL}/person/link`, link);
    }

    archivePerson(id: number, reason: string): Observable<any> {
        return this.http.patch(`${API_URL}/person/${id}/archive`, { reason });
    }
    
    getPersonRoles(): Observable<any[]> {
        return this.http.get<any[] | PersonRolesApiResponse>(`${API_URL}/person/roles`).pipe(
            map(response => {
                const roles = Array.isArray(response) ? response : (response.data || []);
                return (roles || []).map(role => this.normalizeRole(role));
            }),
            catchError(() => of([]))
        );
    }
}

// Add this interface for the response
export interface PersonApiResponse {
    success: boolean;
    data: Person[];
    message?: string;
}

export interface PersonRolesApiResponse {
    success: boolean;
    data: any[];
    message?: string;
}
