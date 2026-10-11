import { TestBed } from '@angular/core/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { of } from 'rxjs';
import { AppComponent } from './app';
import { AuthService } from './services/auth';
import { NotificationService } from './services/notification';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RouterTestingModule, AppComponent],
      providers: [
        { provide: AuthService, useValue: {
          isLoggedIn: vi.fn().mockReturnValue(false),
          getToken: vi.fn().mockReturnValue(null),
          getCurrentUser: vi.fn().mockReturnValue(null),
          logout: vi.fn(),
          currentUser$: of(null)
        } },
        { provide: NotificationService, useValue: {
          getNotifications: vi.fn().mockReturnValue(of([])),
          markRead: vi.fn().mockReturnValue(of({}))
        } }
      ]
    }).compileComponents();
  });

  it('creates the application shell', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeTruthy();
  }, 15_000);

  it('renders the router outlet', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('router-outlet')).toBeTruthy();
  });

  it('hides operational docket, person and evidence navigation from System Administrators', () => {
    const auth = TestBed.inject(AuthService) as any;
    auth.getCurrentUser.mockReturnValue({
      roleName: 'System Administrator',
      permissions: ['Dockets.View', 'Persons.View', 'Evidence.View']
    });
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;

    expect(app.canAccessDockets()).toBe(false);
    expect(app.canAccessPersons()).toBe(false);
    expect(app.canAccessEvidence()).toBe(false);
    expect(app.canAccessAdministration()).toBe(true);
  });
});
