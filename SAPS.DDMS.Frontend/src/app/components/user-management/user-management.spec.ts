import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { NEVER, of } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { UserManagement } from './user-management';
import { UserService } from '../../services/user';
import { AuthService } from '../../services/auth';

describe('UserManagement — account administration', () => {
  let component: UserManagement;
  let fixture: ComponentFixture<UserManagement>;
  let userService: any;
  const mockUsers: any[] = [
    { userID: 1, firstName: 'Sipho', lastName: 'Mokoena', personnelNumber: 'SAPS001', rank: 'Constable', emailAddress: 's.mokoena@saps.gov.za', roleName: 'CSC Officer', stationName: 'JHB', isActivated: true, isActive: true, isLocked: false, accountStatus: 'Active', dateCreated: new Date() },
    { userID: 2, firstName: 'Zanele', lastName: 'Mahlangu', personnelNumber: 'SAPS002', rank: 'Sergeant', emailAddress: 'z.mahlangu@saps.gov.za', roleName: 'Docket Administrator', stationName: 'JHB', isActivated: true, isActive: false, isLocked: true, accountStatus: 'Suspended', dateCreated: new Date() }
  ];

  beforeEach(async () => {
    userService = {
      getAllUsers: vi.fn().mockReturnValue(of(mockUsers)),
      createUser: vi.fn().mockReturnValue(of({ success: true })),
      getRoles: vi.fn().mockReturnValue(of([{ id: 1, name: 'CSC Officer' }])),
      getStations: vi.fn().mockReturnValue(of([{ id: 1, name: 'JHB Central', province: 'Gauteng', region: 'Central' }])),
      getCities: vi.fn().mockReturnValue(of([{ id: 1, province: 'Gauteng', name: 'Central' }]))
    };
    await TestBed.configureTestingModule({
      imports: [RouterTestingModule, UserManagement],
      providers: [
        { provide: UserService, useValue: userService },
        { provide: AuthService, useValue: { getCurrentUser: vi.fn().mockReturnValue({ roleName: 'System Administrator' }) } },
        { provide: ActivatedRoute, useValue: { snapshot: { params: {} } } },
        { provide: ToastrService, useValue: { success: vi.fn(), error: vi.fn(), warning: vi.fn() } }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(UserManagement);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads users for a system administrator', () => {
    expect(userService.getAllUsers).toHaveBeenCalled();
    expect(component.filteredUsers).toHaveLength(2);
  });

  it('always offers all nine South African provinces', () => {
    expect(component.stationProvinces).toEqual([
      'Eastern Cape', 'Free State', 'Gauteng', 'KwaZulu-Natal', 'Limpopo',
      'Mpumalanga', 'North West', 'Northern Cape', 'Western Cape'
    ]);
  });

  it('validates the create form', () => {
    component.createForm.reset();
    expect(component.createForm.invalid).toBe(true);
    component.createForm.patchValue({ firstName: 'John', lastName: 'Smith', personnelNumber: 'SAPS99999', rank: 'Constable', emailAddress: 'j.smith@saps.gov.za', phoneNumber: '0821234567', roleID: 1, stationID: 1 });
    expect(component.createForm.valid).toBe(true);
  });

  it('rejects whitespace-only identity and rank values', () => {
    component.createForm.patchValue({ firstName: '   ', lastName: '   ', personnelNumber: 'ADMIN99', rank: '   ' });

    expect(component.createForm.get('firstName')?.hasError('required')).toBe(true);
    expect(component.createForm.get('lastName')?.hasError('required')).toBe(true);
    expect(component.createForm.get('rank')?.hasError('required')).toBe(true);
  });

  it('returns account status badges', () => {
    expect(component.getStatusBadgeClass(mockUsers[0])).toBe('status-active');
    expect(['status-locked', 'status-inactive']).toContain(component.getStatusBadgeClass(mockUsers[1]));
  });

  it('shows pending activation instead of a stale Active account status', () => {
    const pendingUser = { ...mockUsers[0], isActivated: false, accountStatus: 'Active' };
    expect(component.getStatusText(pendingUser)).toBe('Pending Activation');
    expect(component.getStatusBadgeClass(pendingUser)).toBe('status-pending');
  });

  it('opens and closes create modal', () => {
    component.openCreateModal();
    expect(component.showCreateModal).toBe(true);
    component.closeCreateModal();
    expect(component.showCreateModal).toBe(false);
  });

  it('closes the create modal after a successful user creation', () => {
    component.openCreateModal();
    component.createForm.patchValue({ firstName: 'John', lastName: 'Smith', personnelNumber: 'SAPS99999', rank: 'Constable', emailAddress: 'j.smith@saps.gov.za', phoneNumber: '0821234567', roleID: 1, stationID: 1 });

    component.createUser();

    expect(userService.createUser).toHaveBeenCalled();
    expect(userService.createUser.mock.calls[0][0].personnelNumber).toBeUndefined();
    expect(component.showCreateModal).toBe(false);
  });

  it('blocks duplicate user creation while the first request is pending', () => {
    userService.createUser.mockReturnValue(NEVER);
    component.createForm.patchValue({ firstName: 'John', lastName: 'Smith', personnelNumber: 'SAPS99999', rank: 'Constable', emailAddress: 'j.smith@saps.gov.za', phoneNumber: '0821234567', roleID: 1, stationID: 1 });

    component.createUser();
    component.createUser();

    expect(userService.createUser).toHaveBeenCalledTimes(1);
  });
});
