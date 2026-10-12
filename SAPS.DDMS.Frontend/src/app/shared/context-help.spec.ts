import { TestBed } from '@angular/core/testing';
import { NavigationEnd, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { AuthService } from '../services/auth';
import { ContextHelp } from './context-help';

describe('ContextHelp', () => {
  let navigation: Subject<NavigationEnd>;
  let router: { url: string; events: Subject<NavigationEnd> };

  beforeEach(async () => {
    navigation = new Subject<NavigationEnd>();
    router = { url: '/dockets', events: navigation };
    await TestBed.configureTestingModule({
      imports: [ContextHelp],
      providers: [
        { provide: Router, useValue: router },
        { provide: AuthService, useValue: { getCurrentUser: () => ({ roleName: 'CSC Officer' }) } }
      ]
    }).compileComponents();
  });

  it('opens current-page guidance, closes with Escape and returns focus', () => {
    const fixture = TestBed.createComponent(ContextHelp);
    fixture.detectChanges();
    const trigger = fixture.nativeElement.querySelector('.floating-help-button') as HTMLButtonElement;
    trigger.focus();
    trigger.click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#context-help-title').textContent).toContain('Docket Management');
    expect(document.activeElement?.getAttribute('aria-label')).toBe('Close page help');

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.context-help-panel')).toBeNull();
    expect(document.activeElement).toBe(trigger);
    fixture.destroy();
  });

  it('keeps the trigger fixed to the viewport', () => {
    const fixture = TestBed.createComponent(ContextHelp);
    fixture.detectChanges();
    const trigger = fixture.nativeElement.querySelector('.floating-help-button') as HTMLButtonElement;
    expect(getComputedStyle(trigger).position).toBe('fixed');
    fixture.destroy();
  });

  it('switches guidance and resets scroll on navigation and modal changes', async () => {
    const fixture = TestBed.createComponent(ContextHelp);
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('.floating-help-button') as HTMLButtonElement).click();
    fixture.detectChanges();
    const scroll = fixture.nativeElement.querySelector('.context-help-scroll') as HTMLElement;
    scroll.scrollTop = 100;

    router.url = '/reports';
    navigation.next(new NavigationEnd(1, '/reports', '/reports'));
    await Promise.resolve();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#context-help-title').textContent).toContain('Report Management');
    expect(scroll.scrollTop).toBe(0);

    const modal = document.createElement('div');
    modal.className = 'modal show';
    modal.innerHTML = '<div class="modal-header"><h3>Register Evidence</h3></div>';
    document.body.appendChild(modal);
    await Promise.resolve();
    await Promise.resolve();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('#context-help-title').textContent).toContain('Register Evidence');
    modal.remove();
    fixture.destroy();
  });
});
