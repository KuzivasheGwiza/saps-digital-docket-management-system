import { Component, OnInit, inject, ChangeDetectorRef, ChangeDetectionStrategy, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';
import { UserService } from '../../services/user';
import { SOUTH_AFRICAN_CONTACT_PATTERN } from '../../validation/contact.validators';
import { AuthService } from '../../services/auth';
import { UserResponse, CreateUserDto, UpdateUserDto, Role, Station } from '../../models/user.model';
import { positiveIntegerValidator, trimmedTextValidator } from '../../validation/input.validators';
import { getApiErrorMessage } from '../../validation/api-error';
import { DialogService } from '../../shared/dialog.service';

const SOUTH_AFRICAN_PROVINCES = [
  'Eastern Cape', 'Free State', 'Gauteng', 'KwaZulu-Natal', 'Limpopo',
  'Mpumalanga', 'North West', 'Northern Cape', 'Western Cape'
] as const;

@Component({
  selector: 'app-user-management',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterModule],
  templateUrl: './user-management.html',
  styleUrls: ['./user-management.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserManagement implements OnInit, OnDestroy {
  private userService = inject(UserService);
  private authService = inject(AuthService);
  private route = inject(ActivatedRoute);
  private fb = inject(FormBuilder);
  private toastr = inject(ToastrService);
  private cdr = inject(ChangeDetectorRef);
  private dialog = inject(DialogService);
  
  users: UserResponse[] = [];
  filteredUsers: UserResponse[] = [];
  roles: Role[] = [];
  stations: Station[] = [];
  cities: Array<{ id: number; province: string; name: string }> = [];
  createProvince = '';
  createCity = '';
  createStationSearch = '';
  createRoleSearch = '';
  selectedTab = 'All';
  searchTerm = '';
  isLoading = false;
  error: string = '';
  showCreateModal = false;
  showEditModal = false;
  showDetailsModal = false;
  showRoleHistoryModal = false;
  selectedUser: UserResponse | null = null;
  roleHistory: any[] = [];
  activationLink: string = '';
  private destroy$ = new Subject<void>();
  
  tabs = ['All'];
  
  createForm: FormGroup;
  editForm: FormGroup;
  
  currentUser = this.authService.getCurrentUser();
  isSystemAdmin = this.currentUser?.roleName === 'System Administrator';
  
  constructor() {
    this.createForm = this.fb.group({
      firstName: ['', [trimmedTextValidator(2, 50), Validators.pattern(/^[A-Za-zÀ-ž' .-]+$/)]],
      lastName: ['', [trimmedTextValidator(2, 50), Validators.pattern(/^[A-Za-zÀ-ž' .-]+$/)]],
      rank: ['', [trimmedTextValidator(2, 50)]],
      stationID: [null, [Validators.required, positiveIntegerValidator]],
      emailAddress: ['', [Validators.required, Validators.email, Validators.maxLength(50)]],
      phoneNumber: ['', [Validators.pattern(SOUTH_AFRICAN_CONTACT_PATTERN)]],
      roleID: [null, [Validators.required, positiveIntegerValidator]],
      reason: ['']
    });
    
    this.editForm = this.fb.group({
      firstName: ['', [trimmedTextValidator(2, 50), Validators.pattern(/^[A-Za-zÀ-ž' .-]+$/)]],
      lastName: ['', [trimmedTextValidator(2, 50), Validators.pattern(/^[A-Za-zÀ-ž' .-]+$/)]],
      rank: ['', [trimmedTextValidator(2, 50)]],
      stationID: [null, [Validators.required, positiveIntegerValidator]],
      emailAddress: ['', [Validators.required, Validators.email, Validators.maxLength(50)]],
      phoneNumber: ['', [Validators.pattern(SOUTH_AFRICAN_CONTACT_PATTERN)]],
      roleID: [null, [Validators.required, positiveIntegerValidator]],
      reason: ['', [trimmedTextValidator(3, 500)]]
    });
  }
  
  ngOnInit(): void {
    this.loadData();
  }
  
  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
  
  loadData(): void {
    if (!this.isSystemAdmin) {
      this.error = 'You do not have permission to manage users';
      this.toastr.error(this.error);
      return;
    }
    
    this.isLoading = true;
    this.error = '';
    this.cdr.markForCheck();
    
    this.userService.getAllUsers()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (users) => {
          this.users = Array.isArray(users) ? users.filter(user => !!user && Number(user.userID) > 0) : [];
          this.filterUsers();
          this.openRouteUserDetails();
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.error = err.error?.message || 'Failed to load users';
          this.toastr.error(this.error);
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
    
    this.userService.getRoles()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (roles) => {
          this.roles = (Array.isArray(roles) ? roles.filter(role => !!role && Number(role.id) > 0 && !!role.name) : [])
            .sort((left, right) => left.name.localeCompare(right.name));
          this.tabs = ['All', ...this.roles.map(role => role.name)];
          if (!this.tabs.includes(this.selectedTab)) {
            this.selectedTab = 'All';
          }
          this.filterUsers();
          this.cdr.markForCheck();
        },
        error: () => {
          this.toastr.error('Failed to load roles');
        }
      });
    
    this.userService.getStations()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (stations) => {
          this.stations = (Array.isArray(stations) ? stations.filter(station => !!station && Number(station.id) > 0 && !!station.name) : [])
            .sort((left, right) => left.name.localeCompare(right.name));
          this.cdr.markForCheck();
        },
        error: () => {
          this.toastr.error('Failed to load stations');
        }
      });

    this.userService.getCities()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: cities => {
          this.cities = (Array.isArray(cities) ? cities : [])
            .filter(city => !!city?.province && !!city?.name)
            .sort((left, right) => left.province.localeCompare(right.province) || left.name.localeCompare(right.name));
          this.cdr.markForCheck();
        },
        error: () => this.toastr.error('Failed to load cities')
      });
  }
  
  filterUsers(): void {
    let filtered = this.users;
    
    if (this.selectedTab !== 'All') {
      filtered = filtered.filter(u => u.roleName === this.selectedTab);
    }
    
    if (this.searchTerm && this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      filtered = filtered.filter(u => 
        (u.firstName?.toLowerCase().includes(term) || false) ||
        (u.lastName?.toLowerCase().includes(term) || false) ||
        (u.personnelNumber?.toLowerCase().includes(term) || false)
      );
    }
    
    this.filteredUsers = filtered;
    this.cdr.markForCheck();
  }
  
  onTabChange(tab: string): void {
    this.selectedTab = tab;
    this.filterUsers();
  }
  
  onSearch(): void {
    this.filterUsers();
  }
  
  openCreateModal(): void {
    const selectedRole = this.selectedTab !== 'All'
      ? this.roles.find(role => role.name === this.selectedTab)
      : null;

    this.createForm.reset({
      firstName: '',
      lastName: '',
      rank: '',
      stationID: '',
      emailAddress: '',
      phoneNumber: '',
      roleID: selectedRole?.id ?? null
    });
    this.createProvince = '';
    this.createCity = '';
    this.createStationSearch = '';
    this.createRoleSearch = selectedRole?.name || '';
    this.showCreateModal = true;
    this.activationLink = '';
    this.cdr.markForCheck();
  }

  get stationProvinces(): string[] {
    return [...SOUTH_AFRICAN_PROVINCES];
  }

  get createCities(): string[] {
    if (!this.createProvince) return [];
    return [...new Set([
      ...this.cities
        .filter(city => this.isSameProvince(city.province, this.createProvince))
        .map(city => city.name.trim()),
      ...this.stations
      .filter(station => this.isSameProvince(station.province, this.createProvince))
      .map(station => station.region?.trim())
    ].filter(Boolean))].sort((left, right) => left.localeCompare(right));
  }

  get createStations(): Station[] {
    return this.stations.filter(station =>
      this.isSameProvince(station.province, this.createProvince) && station.region === this.createCity
    );
  }

  private isSameProvince(left: string | null | undefined, right: string): boolean {
    const normalise = (value: string | null | undefined) => (value || '').toLowerCase().replace(/[^a-z]/g, '');
    return normalise(left) === normalise(right);
  }

  onCreateProvinceChange(): void {
    this.createCity = '';
    this.createStationSearch = '';
    this.createForm.get('stationID')?.setValue(null);
  }

  onCreateCityChange(): void {
    this.createStationSearch = '';
    this.createForm.get('stationID')?.setValue(null);
  }

  onCreateStationSearch(value: string): void {
    this.createStationSearch = value;
    const station = this.createStations.find(item => item.name.localeCompare(value.trim(), undefined, { sensitivity: 'accent' }) === 0);
    this.createForm.get('stationID')?.setValue(station?.id ?? null);
    this.createForm.get('stationID')?.markAsTouched();
  }

  onCreateRoleSearch(value: string): void {
    this.createRoleSearch = value;
    const role = this.roles.find(item => item.name.localeCompare(value.trim(), undefined, { sensitivity: 'accent' }) === 0);
    this.createForm.get('roleID')?.setValue(role?.id ?? null);
    this.createForm.get('roleID')?.markAsTouched();
  }
  
  closeCreateModal(): void {
    this.showCreateModal = false;
    this.cdr.markForCheck();
  }
  
  createUser(): void {
    if (!this.isSystemAdmin) {
      this.toastr.error('A user cannot be created because only a System Administrator may manage user accounts.');
      return;
    }
    if (this.isLoading) return;
    this.trimFormControls(this.createForm, ['firstName', 'lastName', 'rank', 'emailAddress']);
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      this.toastr.error('Please fill all required fields correctly');
      return;
    }
    
    this.isLoading = true;
    this.cdr.markForCheck();
    
    const userData: CreateUserDto = {
      firstName: this.createForm.value.firstName.trim(),
      lastName: this.createForm.value.lastName.trim(),
      rank: this.createForm.value.rank.trim(),
      stationID: Number(this.createForm.value.stationID),
      emailAddress: this.createForm.value.emailAddress.trim().toLowerCase(),
      phoneNumber: this.createForm.value.phoneNumber || '',
      roleID: Number(this.createForm.value.roleID)
    };
    
    this.userService.createUser(userData)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          if (response?.user?.activationLink) {
            this.activationLink = response.user.activationLink;
            this.toastr.warning('User created. Copy the activation link from the confirmation banner.');
          } else {
            this.toastr.success(response?.message || 'User created successfully! An activation email has been sent.');
          }
          this.closeCreateModal();
          this.isLoading = false;
          this.loadData();
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.toastr.error(getApiErrorMessage(err, 'The user account could not be created'));
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }

  copyActivationLink(): void {
    if (!this.activationLink) return;
    const clipboard = globalThis.navigator?.clipboard;
    if (!clipboard?.writeText) {
      this.toastr.error('Automatic copy is unavailable in this browser. Select and copy the activation link manually.');
      return;
    }
    clipboard.writeText(this.activationLink).then(
      () => this.toastr.success('Activation link copied.'),
      () => this.toastr.error('Could not copy the link. Select and copy it manually.')
    );
  }

  clearActivationLink(): void {
    this.activationLink = '';
    this.cdr.markForCheck();
  }

  resendActivation(user: UserResponse): void {
    this.isLoading = true;
    this.activationLink = '';
    this.cdr.markForCheck();

    this.userService.resendActivationEmail(user.userID)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          if (response.user?.activationLink) {
            this.activationLink = response.user.activationLink;
            this.toastr.warning('Activation email was not sent. Use the activation link shown.');
          } else {
            this.toastr.success(response.message || 'Activation email resent successfully.');
          }
          this.loadData();
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.toastr.error(err.error?.message || 'Failed to resend activation email');
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }

  private openRouteUserDetails(): void {
    const userId = Number(this.route.snapshot.params['id']);
    if (!userId) {
      return;
    }

    const user = this.users.find(u => u.userID === userId);
    if (user) {
      this.openDetailsModal(user);
    }
  }

  openDetailsModal(user: UserResponse): void {
    this.selectedUser = user;
    this.showDetailsModal = true;
    this.cdr.markForCheck();
  }

  closeDetailsModal(): void {
    this.showDetailsModal = false;
    this.selectedUser = null;
    this.cdr.markForCheck();
  }
  
  openEditModal(user: UserResponse): void {
    this.selectedUser = user;
    this.editForm.patchValue({
      firstName: user.firstName,
      lastName: user.lastName,
      rank: user.rank,
      stationID: this.stations.find(s => s.name === user.stationName)?.id || '',
      emailAddress: user.emailAddress,
      phoneNumber: user.phoneNumber || '',
      roleID: this.roles.find(r => r.name === user.roleName)?.id || '',
      reason: ''
    });
    this.showEditModal = true;
    this.cdr.markForCheck();
  }
  
  closeEditModal(): void {
    this.showEditModal = false;
    this.selectedUser = null;
    this.cdr.markForCheck();
  }
  
  updateUser(): void {
    if (!this.isSystemAdmin) {
      this.toastr.error('The user cannot be updated because only a System Administrator may manage user accounts.');
      return;
    }
    if (this.isLoading) return;
    this.trimFormControls(this.editForm, ['firstName', 'lastName', 'rank', 'emailAddress', 'reason']);
    if (this.editForm.invalid || !this.selectedUser) {
      this.editForm.markAllAsTouched();
      this.toastr.error('Please fill all required fields');
      return;
    }
    
    this.isLoading = true;
    this.cdr.markForCheck();
    
    const updateData: UpdateUserDto = {
      firstName: this.editForm.value.firstName.trim(),
      lastName: this.editForm.value.lastName.trim(),
      rank: this.editForm.value.rank.trim(),
      stationID: Number(this.editForm.value.stationID),
      emailAddress: this.editForm.value.emailAddress.trim().toLowerCase(),
      phoneNumber: this.editForm.value.phoneNumber || '',
      roleID: Number(this.editForm.value.roleID),
      reason: this.editForm.value.reason || 'User account updated'
    };
    
    this.userService.updateUser(this.selectedUser.userID, updateData)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.toastr.success('User updated successfully');
          this.loadData();
          this.closeEditModal();
          this.isLoading = false;
          this.cdr.markForCheck();
        },
        error: (err) => {
          const errorMessage = getApiErrorMessage(err, 'The user account could not be updated');
          this.toastr.error(errorMessage);
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }
  
  getStatusBadgeClass(user: UserResponse): string {
    if (user.accountStatus === 'Suspended') return 'status-locked';
    if (user.accountStatus === 'Deactivated') return 'status-inactive';
    if (!user.isActivated) return 'status-pending';
    if (user.isLocked) return 'status-locked';
    if (!user.isActive) return 'status-inactive';
    return 'status-active';
  }
  
  getStatusText(user: UserResponse): string {
    if (user.accountStatus === 'Suspended') return 'Suspended';
    if (user.accountStatus === 'Deactivated') return 'Deactivated';
    if (!user.isActivated) return 'Pending Activation';
    if (user.isLocked) return 'Locked';
    if (!user.isActive) return 'Inactive';
    return user.accountStatus || 'Active';
  }

  async performUserAction(user: UserResponse, action: 'deactivate' | 'reactivate' | 'suspend' | 'unlock'): Promise<void> {
    if (!this.isSystemAdmin) {
      this.toastr.error('The account action cannot be completed because only a System Administrator may manage user accounts.');
      return;
    }
    if (this.isLoading) return;
    const reason = await this.dialog.open({
      title: `${action.charAt(0).toUpperCase() + action.slice(1)} User Account`,
      message: `You are about to ${action} ${user.firstName} ${user.lastName}. This administrative action will be audited.`,
      inputLabel: 'Reason', inputType: 'textarea', required: true, minLength: 3, maxLength: 500,
      initialValue: `${action} user account`, confirmText: `${action.charAt(0).toUpperCase() + action.slice(1)} Account`,
      tone: action === 'deactivate' || action === 'suspend' ? 'danger' : 'primary'
    });
    if (!reason || reason.trim().length < 3) {
      this.toastr.error('A reason is required');
      return;
    }

    const request = {
      deactivate: () => this.userService.deactivateUser(user.userID, reason.trim()),
      reactivate: () => this.userService.reactivateUser(user.userID, reason.trim()),
      suspend: () => this.userService.suspendUser(user.userID, reason.trim()),
      unlock: () => this.userService.unlockUser(user.userID, reason.trim())
    }[action]();

    this.isLoading = true;
    this.cdr.markForCheck();
    request.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toastr.success(`User ${action} action completed`);
        this.closeDetailsModal();
        this.closeRoleHistoryModal();
        this.loadData();
        this.isLoading = false;
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.toastr.error(getApiErrorMessage(err, `The ${action} account action could not be completed`));
        this.isLoading = false;
        this.cdr.markForCheck();
      }
    });
  }

  openRoleHistory(user: UserResponse): void {
    this.selectedUser = user;
    this.roleHistory = [];
    this.showRoleHistoryModal = true;
    this.userService.getRoleHistory(user.userID)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: history => {
          this.roleHistory = history || [];
          this.cdr.markForCheck();
        },
        error: err => {
          this.toastr.error(err.error?.message || 'Failed to load role history');
          this.cdr.markForCheck();
        }
      });
  }

  closeRoleHistoryModal(): void {
    this.showRoleHistoryModal = false;
    this.roleHistory = [];
    this.selectedUser = null;
    this.cdr.markForCheck();
  }
  
  retry(): void {
    this.loadData();
  }

  private trimFormControls(form: FormGroup, controlNames: string[]): void {
    for (const name of controlNames) {
      const control = form.get(name);
      if (typeof control?.value === 'string') {
        control.setValue(control.value.trim(), { emitEvent: false });
      }
    }
  }
}
