import { resolveContextHelp } from './context-help-content';
import { ROLES } from '../security/role-policy';

describe('context help content', () => {
  it('covers every page route with a specific guide', () => {
    const paths = [
      '/login', '/activate', '/forgot-password', '/reset-password', '/change-password',
      '/profile', '/help', '/feedback', '/users', '/users/42', '/roles',
      '/data-exchange', '/faq-management', '/system-config', '/station-tree',
      '/backup', '/audit-log', '/ai-log', '/sms-queue', '/reports',
      '/evidence-register', '/calendar', '/system-calendar', '/dockets',
      '/dockets/create', '/dockets/42', '/evidence', '/investigation/42',
      '/persons', '/persons/create'
    ];
    for (const path of paths) {
      const result = resolveContextHelp(path, ROLES.dscSupervisor);
      expect(result.title, path).toBeTruthy();
      expect(result.purpose, path).toBeTruthy();
      expect(result.steps.length, path).toBeGreaterThan(0);
      expect(result.title, path).not.toBe('Dashboard');
    }
  });

  it('uses form guidance instead of list guidance when a modal is open', () => {
    expect(resolveContextHelp('/dockets', ROLES.cscOfficer, 'Create New Docket').title).toBe('Create Docket');
    expect(resolveContextHelp('/dockets', ROLES.cscOfficer, 'Update Docket').title).toBe('Update Docket');
    expect(resolveContextHelp('/evidence', ROLES.investigatingOfficer, 'Register Evidence').title).toBe('Register Evidence');
    expect(resolveContextHelp('/persons', ROLES.cscOfficer, 'Person Details').title).toBe('Person Details');
    expect(resolveContextHelp('/system-calendar', ROLES.systemAdministrator, 'Add system event').title).toBe('Add System Event');
  });

  it('limits docket list instructions to the current role', () => {
    const supervisor = resolveContextHelp('/dockets', ROLES.dscSupervisor);
    const investigator = resolveContextHelp('/dockets', ROLES.investigatingOfficer);
    const administrator = resolveContextHelp('/dashboard', ROLES.systemAdministrator);
    expect(supervisor.steps.join(' ')).toContain('Allocate');
    expect(investigator.steps.join(' ')).toContain('Accept assignment');
    expect(investigator.steps.join(' ')).not.toContain('Reassign');
    expect(administrator.controls.join(' ')).toContain('Dockets are not available');
  });

  it('updates the guide when the route changes', () => {
    const create = resolveContextHelp('/dockets/create', ROLES.cscOfficer);
    const evidence = resolveContextHelp('/evidence', ROLES.investigatingOfficer);
    expect(create.title).toBe('Create Docket');
    expect(evidence.title).toBe('Evidence Management');
    expect(evidence.steps.join(' ')).not.toContain('Occurrence Date');
  });
});
