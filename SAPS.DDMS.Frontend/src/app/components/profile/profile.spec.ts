import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { of } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { Profile } from './profile';
import { UserService } from '../../services/user';
import { AuthService } from '../../services/auth';

describe('Profile — update profile', () => {
  let component: Profile;
  let fixture: ComponentFixture<Profile>;
  const mockUser: any = {
    userID: 1, firstName: 'Thabo', lastName: 'Dlamini', personnelNumber: 'SAPS001',
    rank: 'Sergeant', emailAddress: 't.dlamini@saps.gov.za', phoneNumber: '0821234567',
    roleName: 'DSC Supervisor', stationName: 'JHB Central', notificationPreferences: 'InApp',
    isActivated: true, isActive: true, isLocked: false, dateCreated: new Date()
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RouterTestingModule, Profile],
      providers: [
        { provide: UserService, useValue: {
          getProfile: vi.fn().mockReturnValue(of(mockUser)),
          getProfilePhoto: vi.fn().mockReturnValue(of(new Blob())),
          uploadProfilePhoto: vi.fn().mockReturnValue(of({ success: true })),
          updateMe: vi.fn().mockReturnValue(of({ success: true }))
        } },
        { provide: AuthService, useValue: { getCurrentUser: vi.fn().mockReturnValue({ userID: 1, roleName: 'DSC Supervisor' }) } },
        { provide: ToastrService, useValue: { success: vi.fn(), error: vi.fn() } }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(Profile);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads the current user', () => {
    expect(component.user?.firstName).toBe('Thabo');
  });

  it('validates phone numbers', () => {
    component.profileForm.patchValue({ phoneNumber: '0821234567' });
    expect(component.profileForm.valid).toBe(true);
    component.profileForm.patchValue({ phoneNumber: '123' });
    expect(component.profileForm.invalid).toBe(true);
  });

  it('toggles edit mode and reports active status', () => {
    component.toggleEdit();
    expect(component.isEditing).toBe(true);
    expect(component.getStatusBadgeClass()).toBe('status-active');
    expect(component.getStatusText()).toBe('Active');
  });
});
