import { routes } from '../app.routes';
import {
  DOCKET_ACTION_ROLES,
  EVIDENCE_ACTION_ROLES,
  INVESTIGATION_NOTE_ACTION_ROLES,
  PERSON_ACTION_ROLES,
  REPORT_ROLES_BY_KEY,
  ROUTE_ROLES,
  ROLES
} from './role-policy';

describe('role route and action matrix', () => {
  function getRouteRoles(path: string): readonly string[] {
    const match = routes.find(route => route.path === path);
    return (match?.data as { roles?: readonly string[] } | undefined)?.roles ?? [];
  }

  it('keeps sensitive route access aligned to the intended roles', () => {
    expect(getRouteRoles('users')).toEqual([ROLES.systemAdministrator]);
    expect(getRouteRoles('users/:id')).toEqual([ROLES.systemAdministrator]);
    expect(getRouteRoles('roles')).toEqual([ROLES.systemAdministrator]);
    expect(getRouteRoles('data-exchange')).toEqual([ROLES.systemAdministrator]);
    expect(getRouteRoles('faq-management')).toEqual(Object.values(ROLES));
    expect(getRouteRoles('system-config')).toEqual([ROLES.systemAdministrator]);
    expect(getRouteRoles('backup')).toEqual([ROLES.systemAdministrator]);
    expect(getRouteRoles('audit-log')).toEqual([ROLES.systemAdministrator]);
    expect(getRouteRoles('ai-log')).toEqual([ROLES.systemAdministrator, ROLES.dscSupervisor]);
    expect(getRouteRoles('sms-queue')).toEqual([ROLES.systemAdministrator, ROLES.dscSupervisor]);
    expect(getRouteRoles('reports')).toEqual(ROUTE_ROLES.reports);
    expect(getRouteRoles('calendar')).toEqual(ROUTE_ROLES.calendar);
    expect(getRouteRoles('dockets/create')).toEqual([ROLES.cscOfficer]);
    expect(getRouteRoles('evidence')).toEqual(ROUTE_ROLES.evidence);
    expect(getRouteRoles('persons')).toEqual(ROUTE_ROLES.persons);
    expect(getRouteRoles('persons/create')).toEqual(ROUTE_ROLES.persons);
    expect(getRouteRoles('investigation/:id')).toEqual(ROUTE_ROLES.investigationNotes);
  });

  it('keeps docket action access aligned for every operational role', () => {
    expect(DOCKET_ACTION_ROLES.create).toEqual([ROLES.cscOfficer]);
    expect(DOCKET_ACTION_ROLES.allocate).toEqual([ROLES.dscSupervisor]);
    expect(DOCKET_ACTION_ROLES.acceptAssignment).toEqual([ROLES.investigatingOfficer]);
    expect(DOCKET_ACTION_ROLES.completeInspection).toEqual([ROLES.dscSupervisor]);
    expect(DOCKET_ACTION_ROLES.updateMetadata).toEqual([
      ROLES.cscOfficer,
      ROLES.docketAdministrator
    ]);
    expect(DOCKET_ACTION_ROLES.changeStatus).toEqual([
      ROLES.investigatingOfficer,
      ROLES.dscSupervisor,
      ROLES.docketAdministrator
    ]);
    expect(DOCKET_ACTION_ROLES.prosecutorPreview).toEqual([ROLES.dscSupervisor]);
    expect(DOCKET_ACTION_ROLES.viewBundles).toEqual([
      ROLES.investigatingOfficer,
      ROLES.dscSupervisor,
      ROLES.prosecutor
    ]);
    expect(DOCKET_ACTION_ROLES.createBundle).toEqual([
      ROLES.investigatingOfficer
    ]);
    expect(DOCKET_ACTION_ROLES.editBundle).toEqual([
      ROLES.investigatingOfficer
    ]);
    expect(DOCKET_ACTION_ROLES.shareBundle).toEqual([ROLES.dscSupervisor]);
    expect(DOCKET_ACTION_ROLES.exportBundle).toEqual([ROLES.dscSupervisor]);
    expect(DOCKET_ACTION_ROLES.downloadSharedBundle).toEqual([ROLES.dscSupervisor, ROLES.prosecutor]);
    expect(DOCKET_ACTION_ROLES.setPrivacy).toEqual([
      ROLES.dscSupervisor,
      ROLES.investigatingOfficer
    ]);
    expect(DOCKET_ACTION_ROLES.viewPrivacyHistory).toEqual([
      ROLES.dscSupervisor,
      ROLES.docketAdministrator
    ]);
    expect(DOCKET_ACTION_ROLES.runAi).toEqual([
      ROLES.investigatingOfficer,
      ROLES.docketAdministrator,
      ROLES.dscSupervisor
    ]);
    expect(DOCKET_ACTION_ROLES.verifyAiExtraction).toEqual([
      ROLES.investigatingOfficer,
      ROLES.docketAdministrator
    ]);
    expect(DOCKET_ACTION_ROLES.archive).toEqual([
      ROLES.docketAdministrator
    ]);
    expect(DOCKET_ACTION_ROLES.recordCourtOutcome).toEqual([
      ROLES.dscSupervisor
    ]);
    expect(DOCKET_ACTION_ROLES.closeOrReopen).toEqual([
      ROLES.dscSupervisor,
      ROLES.docketAdministrator
    ]);
  });

  it('keeps person, evidence, and investigation-note actions aligned to the responsible roles', () => {
    expect(PERSON_ACTION_ROLES.createOrEdit).toEqual([
      ROLES.systemAdministrator,
      ROLES.cscOfficer,
      ROLES.investigatingOfficer,
      ROLES.dscSupervisor,
      ROLES.docketAdministrator
    ]);
    expect(PERSON_ACTION_ROLES.linkToDocket).toEqual([
      ROLES.systemAdministrator,
      ROLES.cscOfficer,
      ROLES.investigatingOfficer,
      ROLES.dscSupervisor
    ]);
    expect(PERSON_ACTION_ROLES.archive).toEqual([
      ROLES.systemAdministrator,
      ROLES.investigatingOfficer,
      ROLES.dscSupervisor,
      ROLES.docketAdministrator
    ]);

    expect(EVIDENCE_ACTION_ROLES.create).toEqual([
      ROLES.investigatingOfficer
    ]);
    expect(EVIDENCE_ACTION_ROLES.edit).toEqual([
      ROLES.investigatingOfficer
    ]);
    expect(EVIDENCE_ACTION_ROLES.transfer).toEqual([
      ROLES.investigatingOfficer,
      ROLES.recordsClerk
    ]);
    expect(EVIDENCE_ACTION_ROLES.viewCustodyHistory).toEqual([
      ROLES.investigatingOfficer,
      ROLES.recordsClerk,
      ROLES.dscSupervisor
    ]);

    expect(INVESTIGATION_NOTE_ACTION_ROLES.create).toEqual([
      ROLES.investigatingOfficer
    ]);
    expect(INVESTIGATION_NOTE_ACTION_ROLES.editAny).toEqual([
      ROLES.docketAdministrator
    ]);
    expect(INVESTIGATION_NOTE_ACTION_ROLES.editOwn).toEqual([
      ROLES.investigatingOfficer
    ]);
    expect(INVESTIGATION_NOTE_ACTION_ROLES.viewVersions).toEqual(
      ROUTE_ROLES.investigationNotes
    );
    expect(INVESTIGATION_NOTE_ACTION_ROLES.requestArchive).toEqual([
      ROLES.investigatingOfficer
    ]);
    expect(INVESTIGATION_NOTE_ACTION_ROLES.reviewArchive).toEqual([
      ROLES.dscSupervisor
    ]);
  });

  it('keeps every report key aligned to the intended consuming roles', () => {
    expect(REPORT_ROLES_BY_KEY['dockets-list']).toEqual([ROLES.dscSupervisor, ROLES.docketAdministrator]);
    for (const [key, roles] of Object.entries(REPORT_ROLES_BY_KEY)) {
      if (key !== 'dockets-list') expect(roles).toEqual([ROLES.dscSupervisor]);
    }
  });
});
