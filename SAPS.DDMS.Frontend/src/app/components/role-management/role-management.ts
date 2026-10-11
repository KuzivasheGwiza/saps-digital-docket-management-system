import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin, Subject, takeUntil } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import {
  AccessRole,
  PermissionDefinition,
  RoleManagementService,
  RoleUpsert
} from '../../services/role-management';
import { PERMISSIONS } from '../../security/role-policy';

@Component({
  selector: 'app-role-management',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './role-management.html',
  styleUrls: ['./role-management.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RoleManagement implements OnInit, OnDestroy {
  private service = inject(RoleManagementService);
  private toastr = inject(ToastrService);
  private cdr = inject(ChangeDetectorRef);
  private destroy$ = new Subject<void>();

  roles: AccessRole[] = [];
  catalog: PermissionDefinition[] = [];
  selectedRoleId: number | null = null;
  roleName = '';
  description = '';
  selectedPermissions = new Set<string>();
  isLoading = true;
  isSaving = false;
  loadError = '';

  ngOnInit(): void {
    this.load();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get groupedCatalog(): Array<{ group: string; permissions: PermissionDefinition[] }> {
    const groups = new Map<string, PermissionDefinition[]>();
    for (const permission of this.catalog) {
      const items = groups.get(permission.group) ?? [];
      items.push(permission);
      groups.set(permission.group, items);
    }
    return Array.from(groups, ([group, permissions]) => ({ group, permissions }));
  }

  get selectedCount(): number {
    return this.selectedPermissions.size;
  }

  load(preferredRoleId?: number): void {
    this.isLoading = true;
    this.loadError = '';
    forkJoin({ roles: this.service.getRoles(), catalog: this.service.getCatalog() })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: ({ roles, catalog }) => {
          this.roles = roles;
          this.catalog = catalog;
          this.isLoading = false;
          const preferred = roles.find(role => role.id === preferredRoleId);
          if (preferred) this.selectRole(preferred);
          else if (this.selectedRoleId !== null) {
            const existing = roles.find(role => role.id === this.selectedRoleId);
            if (existing) this.selectRole(existing);
          }
          this.cdr.markForCheck();
        },
        error: error => {
          this.isLoading = false;
          this.loadError = this.errorMessage(error, 'Roles and permissions could not be loaded.');
          this.cdr.markForCheck();
        }
      });
  }

  selectRole(role: AccessRole): void {
    this.selectedRoleId = role.id;
    this.roleName = role.name;
    this.description = role.description ?? '';
    this.selectedPermissions = new Set(role.permissions.map(permission => permission.name));
    this.selectedPermissions.add(PERMISSIONS.dashboardView);
    this.cdr.markForCheck();
  }

  startNewRole(): void {
    this.selectedRoleId = null;
    this.roleName = '';
    this.description = '';
    this.selectedPermissions = new Set<string>([PERMISSIONS.dashboardView]);
    this.cdr.markForCheck();
  }

  isSelected(permission: string): boolean {
    return this.selectedPermissions.has(permission);
  }

  togglePermission(permission: string, checked: boolean): void {
    if (permission === PERMISSIONS.dashboardView) return;
    const next = new Set(this.selectedPermissions);
    if (checked) next.add(permission);
    else next.delete(permission);
    this.selectedPermissions = next;
  }

  toggleGroup(group: string, checked: boolean): void {
    const next = new Set(this.selectedPermissions);
    for (const permission of this.catalog.filter(item => item.group === group)) {
      if (checked) next.add(permission.name);
      else next.delete(permission.name);
    }
    next.add(PERMISSIONS.dashboardView);
    this.selectedPermissions = next;
    this.cdr.markForCheck();
  }

  isGroupSelected(group: string): boolean {
    const items = this.catalog.filter(item => item.group === group);
    return items.length > 0 && items.every(item => this.selectedPermissions.has(item.name));
  }

  isBaselinePermission(permission: string): boolean {
    return permission === PERMISSIONS.dashboardView;
  }

  save(): void {
    const name = this.roleName.trim();
    if (!name) {
      this.toastr.error('Role name is required.');
      return;
    }
    if (this.selectedPermissions.size === 0 && name !== 'System Administrator') {
      this.toastr.warning('Select at least one permission so this role can access the system.');
      return;
    }

    const payload: RoleUpsert = {
      name,
      description: this.description.trim(),
      permissions: Array.from(this.selectedPermissions).sort()
    };
    this.isSaving = true;
    const request = this.selectedRoleId === null
      ? this.service.createRole(payload)
      : this.service.updateRole(this.selectedRoleId, payload);

    request.pipe(takeUntil(this.destroy$)).subscribe({
      next: response => {
        let savedId: number | undefined = this.selectedRoleId ?? undefined;
        if (savedId === undefined && 'roleId' in response && typeof response.roleId === 'number') {
          savedId = response.roleId;
        }
        this.isSaving = false;
        this.toastr.success(response.message || 'Role permissions saved.');
        this.load(savedId);
      },
      error: error => {
        this.isSaving = false;
        this.toastr.error(this.errorMessage(error, 'The role could not be saved.'));
        this.cdr.markForCheck();
      }
    });
  }

  private errorMessage(error: any, fallback: string): string {
    return error?.error?.message || error?.message || fallback;
  }
}
