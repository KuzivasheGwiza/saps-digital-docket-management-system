import { CommonModule } from '@angular/common';
import { Component, HostListener, inject, OnDestroy, OnInit } from '@angular/core';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { filter, interval, Subject, takeUntil } from 'rxjs';
import { AuthService } from './services/auth';
import { NotificationService } from './services/notification';
import { hasAnyRole, hasPermission, PERMISSIONS, ROUTE_ROLES, ROLES } from './security/role-policy';
import { AppDialog } from './shared/app-dialog';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterModule, AppDialog],
  templateUrl: './app.html',
  styleUrls: ['./app.css']
})
export class AppComponent implements OnInit, OnDestroy {
  public authService = inject(AuthService);
  private notificationService = inject(NotificationService);
  private router = inject(Router);
  private destroy$ = new Subject<void>();

  notifications: any[] = [];
  unreadCount = 0;
  isNotificationOpen = false;
  isDropdownOpen = false;
  isAdministrationOpen = false;
  isMobileMenuOpen = false;
  private nativeSelectSearch: { element: HTMLSelectElement | null; term: string; resetTimer: ReturnType<typeof setTimeout> | null } = {
    element: null,
    term: '',
    resetTimer: null
  };

  ngOnInit(): void {
    if (this.authService.isLoggedIn()) {
      this.loadNotifications();
    }

    // Keeps the Alerts badge current when an allocation or rejection is made
    // while the recipient is already signed in on another screen.
    interval(30_000)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        if (this.authService.hasTimedOutFromInactivity()) {
          this.authService.logout();
          void this.router.navigate(['/login'], { queryParams: { reason: 'inactivity' } });
          return;
        }
        if (this.authService.isLoggedIn()) this.loadNotifications();
      });

    this.router.events
      .pipe(
        filter(event => event instanceof NavigationEnd),
        takeUntil(this.destroy$)
      )
      .subscribe(() => this.closeNavigation());
  }

  @HostListener('document:click')
  @HostListener('document:keydown')
  @HostListener('document:touchstart')
  @HostListener('document:mousemove')
  recordUserActivity(): void {
    this.authService.recordActivity();
  }

  ngOnDestroy(): void {
    if (this.nativeSelectSearch.resetTimer) clearTimeout(this.nativeSelectSearch.resetTimer);
    this.destroy$.next();
    this.destroy$.complete();
  }

  /**
   * Every remaining compact native dropdown supports a case-insensitive
   * contains search: focus it and type part of an option's name. Search-input
   * dropdowns are used where users need to see and filter many options.
   */
  @HostListener('document:keydown', ['$event'])
  searchFocusedDropdown(event: KeyboardEvent): void {
    const select = event.target instanceof HTMLSelectElement ? event.target : null;
    if (!select || select.disabled || event.ctrlKey || event.metaKey || event.altKey || event.key.length !== 1) return;

    if (this.nativeSelectSearch.element !== select) {
      if (this.nativeSelectSearch.resetTimer) clearTimeout(this.nativeSelectSearch.resetTimer);
      this.nativeSelectSearch = { element: select, term: '', resetTimer: null };
      select.setAttribute('aria-description', 'Type to search the available options.');
    }

    this.nativeSelectSearch.term += event.key.toLocaleLowerCase();
    const term = this.nativeSelectSearch.term.trim();
    const matchingIndex = Array.from(select.options).findIndex(option =>
      !option.disabled && option.text.toLocaleLowerCase().includes(term)
    );
    if (matchingIndex >= 0) {
      event.preventDefault();
      select.selectedIndex = matchingIndex;
      select.dispatchEvent(new Event('change', { bubbles: true }));
    }

    if (this.nativeSelectSearch.resetTimer) clearTimeout(this.nativeSelectSearch.resetTimer);
    this.nativeSelectSearch.resetTimer = setTimeout(() => {
      this.nativeSelectSearch.term = '';
      this.nativeSelectSearch.resetTimer = null;
    }, 700);
  }

  get currentUser() {
    return this.authService.getCurrentUser();
  }

  shouldShowShell(): boolean {
    if (!this.authService.isLoggedIn()) return false;

    const path = this.router.url.split(/[?#]/)[0];
    return !['/', '/login', '/activate', '/forgot-password', '/reset-password'].includes(path);
  }

  hasRole(role: string): boolean {
    return this.currentUser?.roleName === role;
  }

  hasAccess(permission: string, fallbackRoles: readonly string[] = []): boolean {
    return hasPermission(this.currentUser?.permissions, permission) ||
      hasAnyRole(this.currentUser?.roleName, fallbackRoles);
  }

  canAccessDockets(): boolean {
    if (this.hasRole(ROLES.systemAdministrator)) return false;
    return this.hasAccess(PERMISSIONS.docketsView, ROUTE_ROLES.dockets);
  }

  canAccessCalendar(): boolean {
    if (this.hasRole(ROLES.systemAdministrator)) return true;
    return this.hasAccess(PERMISSIONS.calendarView, ROUTE_ROLES.calendar);
  }

  getCalendarRoute(): string {
    return this.hasRole(ROLES.systemAdministrator) ? '/system-calendar' : '/calendar';
  }

  canAccessEvidence(): boolean {
    if (this.hasRole(ROLES.systemAdministrator)) return false;
    return this.hasAccess(PERMISSIONS.evidenceView, ROUTE_ROLES.evidence);
  }

  canAccessPersons(): boolean {
    if (this.hasRole(ROLES.systemAdministrator)) return false;
    return this.hasAccess(PERMISSIONS.personsView, ROUTE_ROLES.persons);
  }

  canAccessReports(): boolean {
    return this.hasAccess(PERMISSIONS.reportsView, ROUTE_ROLES.reports);
  }

  canAccessEvidenceRegister(): boolean {
    return this.hasRole(ROLES.dscSupervisor) && this.canAccessReports();
  }

  canAccessStations(): boolean {
    return this.hasAccess(PERMISSIONS.stationsView, Object.values(ROLES));
  }

  canAccessAdministration(): boolean {
    return hasAnyRole(this.currentUser?.roleName, ROUTE_ROLES.operationalAdministration) || [
      PERMISSIONS.usersManage,
      PERMISSIONS.rolesManage,
      PERMISSIONS.faqManage,
      PERMISSIONS.configurationManage,
      PERMISSIONS.backupManage,
      PERMISSIONS.auditView,
      PERMISSIONS.aiLogView,
      PERMISSIONS.smsManage,
      PERMISSIONS.dataExchangeManage
    ].some(permission => hasPermission(this.currentUser?.permissions, permission));
  }

  canManageUsers(): boolean { return this.hasAccess(PERMISSIONS.usersManage, [ROLES.systemAdministrator]); }
  canManageRoles(): boolean { return this.hasAccess(PERMISSIONS.rolesManage, [ROLES.systemAdministrator]); }
  canManageFaq(): boolean { return this.hasAccess(PERMISSIONS.faqManage, [ROLES.systemAdministrator]); }
  canManageConfiguration(): boolean { return this.hasAccess(PERMISSIONS.configurationManage, [ROLES.systemAdministrator]); }
  canManageBackup(): boolean { return this.hasAccess(PERMISSIONS.backupManage, [ROLES.systemAdministrator]); }
  canViewAudit(): boolean { return this.hasAccess(PERMISSIONS.auditView, [ROLES.systemAdministrator]); }
  canViewAiLog(): boolean { return this.hasAccess(PERMISSIONS.aiLogView, ROUTE_ROLES.operationalAdministration); }
  canManageSms(): boolean { return this.hasAccess(PERMISSIONS.smsManage, ROUTE_ROLES.operationalAdministration); }
  canManageDataExchange(): boolean { return this.hasAccess(PERMISSIONS.dataExchangeManage, [ROLES.systemAdministrator]); }

  isAdministrationRoute(): boolean {
    const path = this.router.url.split(/[?#]/)[0];
    return [
      '/users',
      '/roles',
      '/faq-management',
      '/system-config',
      '/backup',
      '/audit-log',
      '/ai-log',
      '/sms-queue',
      '/data-exchange'
    ].some(route => path === route || path.startsWith(`${route}/`));
  }

  getHelpTopic(): string {
    const url = this.router.url.split(/[?#]/)[0];
    if (url === '/dockets/create') return 'create-docket';
    if (/^\/dockets\/[^/]+$/.test(url)) return 'docket-details';
    if (url === '/persons/create') return 'register-person';
    if (/^\/users\/[^/]+$/.test(url)) return 'user-details';
    if (url === '/evidence-register') return 'evidence-register';
    if (url === '/system-calendar') return 'system-calendar';
    if (url === '/change-password') return 'change-password';
    if (url.includes('/users')) return 'users';
    if (url.includes('/roles')) return 'roles';
    if (url.includes('/faq-management')) return 'faq';
    if (url.includes('/reports')) return 'reports';
    if (url.includes('/calendar')) return 'calendar';
    if (url.includes('/sms-queue')) return 'sms';
    if (url.includes('/data-exchange')) return 'data-exchange';
    if (url.includes('/dockets')) return 'dockets';
    if (url.includes('/evidence')) return 'evidence';
    if (url.includes('/investigation')) return 'investigation';
    if (url.includes('/persons')) return 'persons';
    if (url.includes('/feedback')) return 'feedback';
    if (url.includes('/station-tree')) return 'stations';
    if (url.includes('/ai-log')) return 'ai';
    if (url.includes('/system-config')) return 'configuration';
    if (url.includes('/backup')) return 'backup';
    if (url.includes('/audit-log')) return 'audit';
    if (url.includes('/profile')) return 'profile';
    return 'dashboard';
  }

  getBreadcrumbLabel(): string {
    const url = this.router.url.split(/[?#]/)[0];

    if (url === '/dockets/create') return 'Create Docket';
    if (/^\/dockets\/[^/]+$/.test(url)) return 'Docket Details';
    if (url === '/persons/create') return 'Register Person';
    if (url.startsWith('/investigation/')) return 'Investigation Notes';
    if (url.startsWith('/users/')) return 'User Details';
    if (url === '/evidence-register') return 'Evidence Register';

    const labels: Array<[string, string]> = [
      ['/faq-management', 'FAQ Management'],
      ['/roles', 'Roles & Permissions'],
      ['/data-exchange', 'Data Exchange'],
      ['/system-config', 'System Configuration'],
      ['/station-tree', 'Station Hierarchy'],
      ['/audit-log', 'Audit Trail'],
      ['/ai-log', 'AI Interaction Log'],
      ['/sms-queue', 'SMS Queue'],
      ['/change-password', 'Change Password'],
      ['/profile', 'My Profile'],
      ['/reports', 'Reports'],
      ['/system-calendar', 'System Calendar'],
      ['/calendar', 'Docket Calendar'],
      ['/dockets', 'Dockets'],
      ['/evidence', 'Evidence'],
      ['/persons', 'Persons'],
      ['/backup', 'Backup & Restore'],
      ['/users', 'User Management'],
      ['/help', 'Help'],
      ['/feedback', 'Feedback'],
      ['/dashboard', 'Dashboard']
    ];

    return labels.find(([path]) => url.startsWith(path))?.[1] ?? 'Dashboard';
  }

  getUserInitials(): string {
    const first = this.currentUser?.firstName?.trim().charAt(0) || '';
    const last = this.currentUser?.lastName?.trim().charAt(0) || '';
    return `${first}${last}`.toUpperCase() || 'U';
  }

  logout(): void {
    this.closeNavigation();
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  toggleMobileMenu(): void {
    this.isMobileMenuOpen = !this.isMobileMenuOpen;
    if (!this.isMobileMenuOpen) {
      this.isAdministrationOpen = false;
      this.isDropdownOpen = false;
      this.isNotificationOpen = false;
    }
  }

  toggleAdministration(): void {
    this.isAdministrationOpen = !this.isAdministrationOpen;
    this.isDropdownOpen = false;
    this.isNotificationOpen = false;
  }

  toggleDropdown(): void {
    this.isDropdownOpen = !this.isDropdownOpen;
    this.isAdministrationOpen = false;
    this.isNotificationOpen = false;
  }

  toggleNotifications(): void {
    this.isNotificationOpen = !this.isNotificationOpen;
    this.isAdministrationOpen = false;
    this.isDropdownOpen = false;
    if (this.isNotificationOpen) {
      this.loadNotifications();
    }
  }

  closeNavigation(): void {
    this.isMobileMenuOpen = false;
    this.isAdministrationOpen = false;
    this.isDropdownOpen = false;
    this.isNotificationOpen = false;
  }

  loadNotifications(): void {
    this.notificationService.getNotifications().subscribe({
      next: items => {
        this.notifications = items || [];
        this.unreadCount = this.notifications.filter(item => !item.isRead).length;
      },
      error: () => {
        this.notifications = [];
        this.unreadCount = 0;
      }
    });
  }

  markNotificationRead(item: any): void {
    if (item.isRead) return;
    this.notificationService.markRead(item.notificationID).subscribe({
      next: () => this.loadNotifications(),
      error: () => {}
    });
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as Element | null;
    if (target && !target.closest('.nav-container')) {
      this.closeNavigation();
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeNavigation();
  }

  @HostListener('window:resize')
  onResize(): void {
    if (window.innerWidth > 1100) {
      this.isMobileMenuOpen = false;
    }
  }
}
