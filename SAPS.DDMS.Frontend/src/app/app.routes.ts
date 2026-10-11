import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { authGuard } from './guards/auth-guard';
import { roleGuard } from './guards/role-guard';
import { AuthService } from './services/auth';
import { ALL_ROLES, PERMISSIONS, ROUTE_ROLES, ROLES } from './security/role-policy';

export const guestOnlyGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);
  return authService.isLoggedIn() ? router.createUrlTree(['/dashboard']) : true;
};

// Each screen is lazy loaded. This keeps the sign-in and application shell small,
// while the browser downloads only the page the user actually opens.
export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./components/login/login').then(m => m.Login), canActivate: [guestOnlyGuard] },
  // Activation intentionally remains reachable while another user is signed in.
  // The activation component clears that session before submitting the token.
  { path: 'activate', loadComponent: () => import('./components/activate-account/activate-account').then(m => m.ActivateAccount) },
  { path: 'forgot-password', loadComponent: () => import('./components/forgot-password/forgot-password').then(m => m.ForgotPassword), canActivate: [guestOnlyGuard] },
  // Reset links are account-specific and must remain reachable even if another
  // user is already signed in on the same browser.
  { path: 'reset-password', loadComponent: () => import('./components/reset-password/reset-password').then(m => m.ResetPassword) },
  { path: 'change-password', loadComponent: () => import('./components/change-password/change-password').then(m => m.ChangePassword), canActivate: [authGuard] },
  { path: 'profile', loadComponent: () => import('./components/profile/profile').then(m => m.Profile), canActivate: [authGuard] },
  { path: 'help', loadComponent: () => import('./components/help/help').then(m => m.Help), canActivate: [authGuard] },
  { path: 'feedback', loadComponent: () => import('./components/feedback/feedback').then(m => m.Feedback), canActivate: [authGuard] },
  { path: 'dashboard', loadComponent: () => import('./components/dashboard/dashboard').then(m => m.Dashboard), canActivate: [authGuard, roleGuard], data: { roles: ALL_ROLES, permission: PERMISSIONS.dashboardView } },
  { path: 'users', loadComponent: () => import('./components/user-management/user-management').then(m => m.UserManagement), canActivate: [authGuard, roleGuard], data: { roles: [ROLES.systemAdministrator], permission: PERMISSIONS.usersManage } },
  { path: 'users/:id', loadComponent: () => import('./components/user-management/user-management').then(m => m.UserManagement), canActivate: [authGuard, roleGuard], data: { roles: [ROLES.systemAdministrator], permission: PERMISSIONS.usersManage } },
  { path: 'roles', loadComponent: () => import('./components/role-management/role-management').then(m => m.RoleManagement), canActivate: [authGuard, roleGuard], data: { roles: [ROLES.systemAdministrator], permission: PERMISSIONS.rolesManage } },
  { path: 'data-exchange', loadComponent: () => import('./components/data-exchange/data-exchange').then(m => m.DataExchange), canActivate: [authGuard, roleGuard], data: { roles: [ROLES.systemAdministrator], permission: PERMISSIONS.dataExchangeManage } },
  // Every authenticated user may read published FAQs.  Management controls
  // remain permission-protected inside the page and by the API.
  { path: 'faq-management', loadComponent: () => import('./components/faq-management/faq-management').then(m => m.FaqManagement), canActivate: [authGuard, roleGuard], data: { roles: ALL_ROLES } },
  { path: 'system-config', loadComponent: () => import('./components/system-config/system-config').then(m => m.SystemConfig), canActivate: [authGuard, roleGuard], data: { roles: [ROLES.systemAdministrator], permission: PERMISSIONS.configurationManage } },
  { path: 'station-tree', loadComponent: () => import('./components/station-tree/station-tree').then(m => m.StationTree), canActivate: [authGuard, roleGuard], data: { roles: ALL_ROLES, permission: PERMISSIONS.stationsView } },
  { path: 'backup', loadComponent: () => import('./components/backup/backup').then(m => m.Backup), canActivate: [authGuard, roleGuard], data: { roles: [ROLES.systemAdministrator], permission: PERMISSIONS.backupManage } },
  { path: 'audit-log', loadComponent: () => import('./components/audit-log/audit-log').then(m => m.AuditLog), canActivate: [authGuard, roleGuard], data: { roles: [ROLES.systemAdministrator], permission: PERMISSIONS.auditView } },
  { path: 'ai-log', loadComponent: () => import('./components/ai-log/ai-log').then(m => m.AiLog), canActivate: [authGuard, roleGuard], data: { roles: ROUTE_ROLES.operationalAdministration, permission: PERMISSIONS.aiLogView } },
  { path: 'sms-queue', loadComponent: () => import('./components/sms-queue/sms-queue').then(m => m.SmsQueue), canActivate: [authGuard, roleGuard], data: { roles: ROUTE_ROLES.operationalAdministration, permission: PERMISSIONS.smsManage } },
  { path: 'reports', loadComponent: () => import('./components/reports/reports').then(m => m.Reports), canActivate: [authGuard, roleGuard], data: { roles: ROUTE_ROLES.reports, permission: PERMISSIONS.reportsView } },
  { path: 'evidence-register', loadComponent: () => import('./components/reports/reports').then(m => m.Reports), canActivate: [authGuard, roleGuard], data: { roles: [ROLES.dscSupervisor], permission: PERMISSIONS.reportsView, defaultReport: 'evidence-list' } },
  { path: 'calendar', loadComponent: () => import('./components/docket-calendar/docket-calendar').then(m => m.DocketCalendar), canActivate: [authGuard, roleGuard], data: { roles: ROUTE_ROLES.calendar, permission: PERMISSIONS.calendarView } },
  { path: 'system-calendar', loadComponent: () => import('./components/system-calendar/system-calendar').then(m => m.SystemCalendar), canActivate: [authGuard, roleGuard], data: { roles: [ROLES.systemAdministrator] } },
  { path: 'dockets', loadComponent: () => import('./components/docket-management/docket-management').then(m => m.DocketManagement), canActivate: [authGuard, roleGuard], data: { roles: ROUTE_ROLES.dockets, deniedRoles: [ROLES.systemAdministrator], permission: PERMISSIONS.docketsView } },
  { path: 'dockets/create', loadComponent: () => import('./components/docket-management/docket-management').then(m => m.DocketManagement), canActivate: [authGuard, roleGuard], data: { roles: ROUTE_ROLES.createDocket, deniedRoles: [ROLES.systemAdministrator] } },
  { path: 'dockets/:id', loadComponent: () => import('./components/docket-management/docket-management').then(m => m.DocketManagement), canActivate: [authGuard, roleGuard], data: { roles: ROUTE_ROLES.dockets, deniedRoles: [ROLES.systemAdministrator], permission: PERMISSIONS.docketsView } },
  { path: 'evidence', loadComponent: () => import('./components/evidence-management/evidence-management').then(m => m.EvidenceManagement), canActivate: [authGuard, roleGuard], data: { roles: ROUTE_ROLES.evidence, permission: PERMISSIONS.evidenceView } },
  { path: 'investigation', redirectTo: '/dockets', pathMatch: 'full' },
  { path: 'investigation/:id', loadComponent: () => import('./components/investigation-notes/investigation-notes').then(m => m.InvestigationNotes), canActivate: [authGuard, roleGuard], data: { roles: ROUTE_ROLES.investigationNotes } },
  { path: 'persons', loadComponent: () => import('./components/person-management/person-management').then(m => m.PersonManagement), canActivate: [authGuard, roleGuard], data: { roles: ROUTE_ROLES.persons, permission: PERMISSIONS.personsView } },
  { path: 'persons/create', loadComponent: () => import('./components/person-management/person-management').then(m => m.PersonManagement), canActivate: [authGuard, roleGuard], data: { roles: ROUTE_ROLES.persons, permission: PERMISSIONS.personsView } },
  { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
  { path: '**', redirectTo: '/dashboard' }
];
