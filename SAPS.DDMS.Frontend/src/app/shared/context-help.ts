import { CommonModule } from '@angular/common';
import { AfterViewInit, ChangeDetectorRef, Component, ElementRef, HostListener, NgZone, OnDestroy, ViewChild, inject } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter, Subject, takeUntil } from 'rxjs';
import { AuthService } from '../services/auth';
import { ContextHelpGuide, resolveContextHelp } from './context-help-content';

@Component({
  selector: 'app-context-help',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './context-help.html',
  styleUrls: ['./context-help.css']
})
export class ContextHelp implements AfterViewInit, OnDestroy {
  private router = inject(Router);
  private auth = inject(AuthService);
  private cdr = inject(ChangeDetectorRef);
  private zone = inject(NgZone);
  private destroy$ = new Subject<void>();
  private observer?: MutationObserver;
  private refreshPending = false;
  private contextKey = '';
  private returnFocus?: HTMLElement;

  @ViewChild('helpButton') helpButton?: ElementRef<HTMLButtonElement>;
  @ViewChild('helpPanel') helpPanel?: ElementRef<HTMLElement>;
  @ViewChild('closeButton') closeButton?: ElementRef<HTMLButtonElement>;
  @ViewChild('panelScroll') panelScroll?: ElementRef<HTMLElement>;

  isOpen = false;
  guide: ContextHelpGuide = resolveContextHelp(this.router.url, this.auth.getCurrentUser()?.roleName || '');

  constructor() {
    this.router.events.pipe(filter(event => event instanceof NavigationEnd), takeUntil(this.destroy$))
      .subscribe(() => {
        this.refresh();
        this.scheduleRefresh();
      });
  }

  ngAfterViewInit(): void {
    // Most create, edit and detail views in DDMS are modals rather than routes.
    // Watch their headings so the same button follows the visible task.
    this.observer = new MutationObserver(() => this.scheduleRefresh());
    this.observer.observe(document.body, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ['class']
    });
    this.scheduleRefresh();
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
    this.destroy$.next();
    this.destroy$.complete();
  }

  open(): void {
    this.refresh();
    this.returnFocus = document.activeElement instanceof HTMLElement && document.activeElement !== document.body
      ? document.activeElement : this.helpButton?.nativeElement;
    this.isOpen = true;
    this.cdr.detectChanges();
    if (this.panelScroll) this.panelScroll.nativeElement.scrollTop = 0;
    this.closeButton?.nativeElement.focus();
  }

  close(): void {
    if (!this.isOpen) return;
    this.isOpen = false;
    this.cdr.detectChanges();
    const target = this.returnFocus?.isConnected ? this.returnFocus : this.helpButton?.nativeElement;
    target?.focus();
    this.returnFocus = undefined;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.close();
  }

  onPanelKeydown(event: KeyboardEvent): void {
    if (event.key !== 'Tab' || !this.helpPanel) return;
    const focusable = Array.from(this.helpPanel.nativeElement.querySelectorAll<HTMLElement>(
      'button:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])'
    ));
    if (!focusable.length) return;
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  private scheduleRefresh(): void {
    if (this.refreshPending) return;
    this.refreshPending = true;
    queueMicrotask(() => this.zone.run(() => {
      this.refreshPending = false;
      this.refresh();
    }));
  }

  private refresh(): void {
    const modalTitle = this.visibleTaskTitle();
    const role = this.auth.getCurrentUser()?.roleName || '';
    const key = `${this.router.url}|${role}|${modalTitle}`;
    if (key === this.contextKey) return;
    this.contextKey = key;
    this.guide = resolveContextHelp(this.router.url, role, modalTitle);
    if (this.isOpen && this.panelScroll) this.panelScroll.nativeElement.scrollTop = 0;
    this.cdr.detectChanges();
  }

  private visibleTaskTitle(): string {
    const activePage = document.querySelector('router-outlet')?.nextElementSibling ?? document;
    const headings = activePage.querySelectorAll<HTMLElement>(
      '.modal.show .modal-header h3, .modal.show .modal-header h2, ' +
      '.modal-backdrop [role="dialog"] h2, .modal-backdrop [role="alertdialog"] h2, .camera-dialog h2'
    );
    return headings.item(headings.length - 1)?.textContent?.trim() || '';
  }
}
