import {
  ALL_ROLES,
  DOCKET_ACTION_ROLES,
  EVIDENCE_ACTION_ROLES,
  hasAnyRole,
  PERSON_ACTION_ROLES,
  REPORT_ROLES_BY_KEY,
  ROUTE_ROLES,
  ROLES,
  SeededRole
} from './role-policy';

describe('seven-role access policy', () => {
  const expectedRoutes: Record<SeededRole, readonly string[]> = {
    [ROLES.systemAdministrator]: ['persons', 'administration'],
    [ROLES.dscSupervisor]: ['dockets', 'calendar', 'evidence', 'persons', 'reports', 'administration', 'investigationNotes'],
    [ROLES.investigatingOfficer]: ['dockets', 'calendar', 'evidence', 'persons', 'investigationNotes'],
    [ROLES.cscOfficer]: ['dockets', 'calendar', 'persons'],
    [ROLES.docketAdministrator]: ['dockets', 'calendar', 'persons', 'reports', 'investigationNotes'],
    [ROLES.recordsClerk]: ['dockets', 'calendar', 'evidence'],
    [ROLES.prosecutor]: ['dockets']
  };

  const routePolicies: Record<string, readonly string[]> = {
    dockets: ROUTE_ROLES.dockets,
    calendar: ROUTE_ROLES.calendar,
    evidence: ROUTE_ROLES.evidence,
    persons: ROUTE_ROLES.persons,
    reports: ROUTE_ROLES.reports,
    investigationNotes: ROUTE_ROLES.investigationNotes,
    administration: ROUTE_ROLES.operationalAdministration
  };

  it('defines each seeded SAPS role exactly once', () => {
    expect(new Set(ALL_ROLES).size).toBe(7);
    expect(ALL_ROLES).toEqual(expect.arrayContaining(Object.values(ROLES)));
  });

  it.each(ALL_ROLES)('exposes the intended navigation for %s', role => {
    const actual = Object.entries(routePolicies)
      .filter(([, allowed]) => hasAnyRole(role, allowed))
      .map(([route]) => route);
    expect(actual.sort()).toEqual([...expectedRoutes[role]].sort());
  });

  it('keeps docket controls aligned to their API role policies', () => {
    expect(ROUTE_ROLES.dockets).not.toContain(ROLES.systemAdministrator);
    expect(Object.values(DOCKET_ACTION_ROLES).every(
      roles => !(roles as readonly string[]).includes(ROLES.systemAdministrator)
    )).toBe(true);
    expect(DOCKET_ACTION_ROLES.create).toEqual([ROLES.cscOfficer]);
    expect(DOCKET_ACTION_ROLES.acceptAssignment).toEqual([ROLES.investigatingOfficer]);
    expect(DOCKET_ACTION_ROLES.prosecutorPreview).toEqual([ROLES.dscSupervisor]);
    expect(DOCKET_ACTION_ROLES.createBundle).not.toContain(ROLES.prosecutor);
    expect(DOCKET_ACTION_ROLES.exportBundle).toEqual([ROLES.dscSupervisor]);
  });

  it('gives each person action its distinct role set', () => {
    expect(PERSON_ACTION_ROLES.createOrEdit).toContain(ROLES.docketAdministrator);
    expect(PERSON_ACTION_ROLES.linkToDocket).not.toContain(ROLES.docketAdministrator);
    expect(PERSON_ACTION_ROLES.archive).not.toContain(ROLES.cscOfficer);
  });

  it('gives Records Clerks custody controls without evidence-edit privileges', () => {
    expect(EVIDENCE_ACTION_ROLES.transfer).toContain(ROLES.recordsClerk);
    expect(EVIDENCE_ACTION_ROLES.edit).not.toContain(ROLES.recordsClerk);
    expect(EVIDENCE_ACTION_ROLES.transfer).not.toContain(ROLES.docketAdministrator);
  });

  it('limits reports to DSC Supervisors except for the Docket Administrator docket list', () => {
    expect(REPORT_ROLES_BY_KEY['users-list']).toEqual([ROLES.dscSupervisor]);
    expect(REPORT_ROLES_BY_KEY['dockets-list']).toEqual([ROLES.dscSupervisor, ROLES.docketAdministrator]);
  });

  it('fails closed for missing and unknown roles', () => {
    expect(hasAnyRole(undefined, ALL_ROLES)).toBe(false);
    expect(hasAnyRole('Unknown Role', ALL_ROLES)).toBe(false);
  });
});
