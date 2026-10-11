import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { ToastrModule } from 'ngx-toastr';
import { AuthService } from '../../services/auth';

import { ActivateAccount } from './activate-account';

describe('ActivateAccount', () => {
  let component: ActivateAccount;
  let fixture: ComponentFixture<ActivateAccount>;
  const authService = {
    clearSession: vi.fn(),
    activateAccount: vi.fn()
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RouterTestingModule, ToastrModule.forRoot(), ActivateAccount],
      providers: [{ provide: AuthService, useValue: authService }]
    }).compileComponents();

    fixture = TestBed.createComponent(ActivateAccount);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('clears any existing user session before processing an activation link', () => {
    expect(authService.clearSession).toHaveBeenCalled();
  });
});
