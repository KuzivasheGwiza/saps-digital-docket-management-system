export const ROLES = {
  systemAdministrator: 'System Administrator',
  dscSupervisor: 'DSC Supervisor',
  investigatingOfficer: 'Investigating Officer',
  cscOfficer: 'CSC Officer',
  docketAdministrator: 'Docket Administrator',
  recordsClerk: 'Records Clerk',
  prosecutor: 'Prosecutor'
} as const;

export type SeededRole = typeof ROLES[keyof typeof ROLES];

export const PERMISSIONS = {
  dashboardView: 'Dashboard.View',
  docketsView: 'Dockets.View',
  personsView: 'Persons.View',
  evidenceView: 'Evidence.View',
  calendarView: 'Calendar.View',
  reportsView: 'Reports.View',
  stationsView: 'Stations.View',
  usersManage: 'Users.Manage',
  rolesManage: 'Roles.Manage',
  faqManage: 'Faq.Manage',
  configurationManage: 'Configuration.Manage',
  backupManage: 'Backup.Manage',
  auditView: 'Audit.View',
  aiLogView: 'AI.Log.View',
  smsManage: 'SMS.Manage',
  dataExchangeManage: 'DataExchange.Manage'
} as const;

export const ALL_ROLES: readonly SeededRole[] = Object.values(ROLES);

export const ROUTE_ROLES = {
  dockets: [
    ROLES.dscSupervisor,
    ROLES.investigatingOfficer,
    ROLES.cscOfficer,
    ROLES.docketAdministrator,
    ROLES.recordsClerk,
    ROLES.prosecutor
  ],
  createDocket: [ROLES.cscOfficer],
  calendar: [
    ROLES.dscSupervisor,
    ROLES.investigatingOfficer,
    ROLES.cscOfficer,
    ROLES.docketAdministrator,
    ROLES.recordsClerk
  ],
  evidence: [
    ROLES.investigatingOfficer,
    ROLES.dscSupervisor,
    ROLES.recordsClerk
  ],
  investigationNotes: [
    ROLES.investigatingOfficer,
    ROLES.dscSupervisor,
    ROLES.docketAdministrator
  ],
  persons: [
    ROLES.systemAdministrator,
    ROLES.cscOfficer,
    ROLES.investigatingOfficer,
    ROLES.dscSupervisor,
    ROLES.docketAdministrator
  ],
  reports: [ROLES.dscSupervisor, ROLES.docketAdministrator],
  operationalAdministration: [ROLES.systemAdministrator, ROLES.dscSupervisor]
} as const;

export const DOCKET_ACTION_ROLES = {
  create: ROUTE_ROLES.createDocket,
  allocate: [ROLES.dscSupervisor],
  acceptAssignment: [ROLES.investigatingOfficer],
  completeInspection: [ROLES.dscSupervisor],
  updateMetadata: [ROLES.cscOfficer, ROLES.docketAdministrator],
  changeStatus: [
    ROLES.investigatingOfficer,
    ROLES.dscSupervisor,
    ROLES.docketAdministrator
  ],
  prosecutorPreview: [ROLES.dscSupervisor],
  viewBundles: [
    ROLES.investigatingOfficer,
    ROLES.dscSupervisor,
    ROLES.prosecutor
  ],
  createBundle: [ROLES.investigatingOfficer],
  editBundle: [ROLES.investigatingOfficer],
  shareBundle: [ROLES.dscSupervisor],
  exportBundle: [ROLES.dscSupervisor],
  downloadSharedBundle: [ROLES.dscSupervisor, ROLES.prosecutor],
  setPrivacy: [ROLES.dscSupervisor, ROLES.investigatingOfficer],
  viewPrivacyHistory: [ROLES.dscSupervisor, ROLES.docketAdministrator],
  runAi: [
    ROLES.investigatingOfficer,
    ROLES.docketAdministrator,
    ROLES.dscSupervisor
  ],
  verifyAiExtraction: [ROLES.investigatingOfficer, ROLES.docketAdministrator],
  archive: [ROLES.docketAdministrator],
  recordCourtOutcome: [ROLES.dscSupervisor],
  closeOrReopen: [ROLES.dscSupervisor, ROLES.docketAdministrator]
} as const;

export const PERSON_ACTION_ROLES = {
  createOrEdit: [
    ROLES.systemAdministrator,
    ROLES.cscOfficer,
    ROLES.investigatingOfficer,
    ROLES.dscSupervisor,
    ROLES.docketAdministrator
  ],
  linkToDocket: [
    ROLES.systemAdministrator,
    ROLES.cscOfficer,
    ROLES.investigatingOfficer,
    ROLES.dscSupervisor
  ],
  archive: [
    ROLES.systemAdministrator,
    ROLES.investigatingOfficer,
    ROLES.dscSupervisor,
    ROLES.docketAdministrator
  ]
} as const;

export const EVIDENCE_ACTION_ROLES = {
  create: [ROLES.investigatingOfficer],
  edit: [ROLES.investigatingOfficer],
  transfer: [ROLES.investigatingOfficer, ROLES.recordsClerk],
  viewCustodyHistory: [
    ROLES.investigatingOfficer,
    ROLES.recordsClerk,
    ROLES.dscSupervisor
  ]
} as const;

export const INVESTIGATION_NOTE_ACTION_ROLES = {
  create: [ROLES.investigatingOfficer],
  editAny: [ROLES.docketAdministrator],
  editOwn: [ROLES.investigatingOfficer],
  viewVersions: ROUTE_ROLES.investigationNotes,
  requestArchive: [ROLES.investigatingOfficer],
  reviewArchive: [ROLES.dscSupervisor]
} as const;

export const REPORT_ROLES_BY_KEY: Readonly<Record<string, readonly SeededRole[]>> = {
  'users-list': [ROLES.dscSupervisor],
  'dockets-list': [ROLES.dscSupervisor, ROLES.docketAdministrator],
  'evidence-list': [ROLES.dscSupervisor],
  'station-workload': [ROLES.dscSupervisor],
  'custody-summary': [ROLES.dscSupervisor],
  'docket-criteria': [ROLES.dscSupervisor],
  'status-management': [ROLES.dscSupervisor],
  'hotspot-analysis': [ROLES.dscSupervisor],
  'clearance-rate': [ROLES.dscSupervisor],
  'crime-trend': [ROLES.dscSupervisor],
  'response-time': [ROLES.dscSupervisor],
  'case-aging': [ROLES.dscSupervisor]
};

export function hasAnyRole(
  roleName: string | null | undefined,
  allowedRoles: readonly string[]
): boolean {
  return !!roleName && allowedRoles.includes(roleName);
}

export function hasPermission(
  permissions: readonly string[] | null | undefined,
  permission: string
): boolean {
  return !!permissions?.some(item => item.toLowerCase() === permission.toLowerCase());
}
