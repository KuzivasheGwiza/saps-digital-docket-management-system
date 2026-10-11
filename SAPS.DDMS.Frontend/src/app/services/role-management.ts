import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_URL } from '../config/api.config';

export interface AssignedPermission {
  permissionID: number;
  name: string;
  accessLevel: string;
}

export interface AccessRole {
  id: number;
  name: string;
  description?: string;
  permissionText?: string;
  permissions: AssignedPermission[];
}

export interface PermissionDefinition {
  name: string;
  group: string;
  description: string;
}

export interface RoleUpsert {
  name: string;
  description: string;
  permissions: string[];
}

@Injectable({ providedIn: 'root' })
export class RoleManagementService {
  private http = inject(HttpClient);

  getRoles(): Observable<AccessRole[]> {
    return this.http.get<AccessRole[]>(`${API_URL}/roles`);
  }

  getCatalog(): Observable<PermissionDefinition[]> {
    return this.http.get<PermissionDefinition[]>(`${API_URL}/roles/catalog`);
  }

  createRole(role: RoleUpsert): Observable<{ success: boolean; message: string; roleId: number }> {
    return this.http.post<{ success: boolean; message: string; roleId: number }>(`${API_URL}/roles`, role);
  }

  updateRole(id: number, role: RoleUpsert): Observable<{ success: boolean; message: string }> {
    return this.http.put<{ success: boolean; message: string }>(`${API_URL}/roles/${id}/permissions`, role);
  }
}
