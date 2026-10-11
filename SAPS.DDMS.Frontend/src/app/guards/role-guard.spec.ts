import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, CanActivateFn, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { AuthService } from '../services/auth';
import { ALL_ROLES, ROUTE_ROLES, ROLES } from '../security/role-policy';
import { roleGuard } from './role-guard';
import { ToastrService } from 'ngx-toastr';

describe('roleGuard', () => {
  let currentRole: string | null;
  let router: Router;

  const executeGuard: CanActivateFn = (...guardParameters) =>
    TestBed.runInInjectionContext(() => roleGuard(...guardParameters));

  beforeEach(() => {
    currentRole = null;
    TestBed.configureTestingModule({
      imports: [RouterTestingModule],
      providers: [{
        provide: AuthService,
        useValue: {
          getCurrentUser: vi.fn(() => currentRole ? { userID: 1, roleName: currentRole } : null)
        }
      }, {
        provide: ToastrService,
        useValue: { warning: vi.fn() }
      }]
    });
    router = TestBed.inject(Router);
  });

  function guardFor(roles: readonly string[], deniedRoles: readonly string[] = []): boolean | UrlTree {
    const route = { data: { roles, deniedRoles } } as unknown as ActivatedRouteSnapshot;
    const state = { url: '/protected' } as RouterStateSnapshot;
    return executeGuard(route, state) as boolean | UrlTree;
  }

  it.each(ROUTE_ROLES.dockets)('allows %s into the docket route', role => {
    currentRole = role;
    expect(guardFor(ROUTE_ROLES.dockets)).toBe(true);
  });

  it('blocks a System Administrator from docket routes even when permissions are present', () => {
    currentRole = ROLES.systemAdministrator;
    const result = guardFor(ROUTE_ROLES.dockets, [ROLES.systemAdministrator]) as UrlTree;
    expect(router.serializeUrl(result)).toBe('/dashboard');
  });

  it('redirects an unauthenticated visitor to login', () => {
    const result = guardFor(ROUTE_ROLES.dockets) as UrlTree;
    expect(router.serializeUrl(result)).toBe('/login');
  });

  it('redirects a role without access to the dashboard', () => {
    currentRole = ROLES.prosecutor;
    const result = guardFor(ROUTE_ROLES.evidence) as UrlTree;
    expect(router.serializeUrl(result)).toBe('/dashboard');
  });

  it('allows Records Clerks into evidence but not investigation notes', () => {
    currentRole = ROLES.recordsClerk;
    expect(guardFor(ROUTE_ROLES.evidence)).toBe(true);
    expect(router.serializeUrl(guardFor(ROUTE_ROLES.investigationNotes) as UrlTree)).toBe('/dashboard');
  });
});
