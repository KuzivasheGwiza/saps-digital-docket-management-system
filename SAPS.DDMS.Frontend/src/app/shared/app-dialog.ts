import { CommonModule } from '@angular/common';
import { Component, ElementRef, ViewChild, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogService } from './dialog.service';

@Component({
  selector: 'app-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="dialog-backdrop" *ngIf="dialog.active() as request" (click)="cancel()">
      <section class="dialog-card" role="dialog" aria-modal="true" aria-labelledby="app-dialog-title" (click)="$event.stopPropagation()">
        <header><span class="dialog-icon"><i class="fas" [class.fa-triangle-exclamation]="request.tone === 'danger'" [class.fa-circle-check]="request.tone !== 'danger'"></i></span><div><h2 id="app-dialog-title">{{ request.title }}</h2><p>{{ request.message }}</p></div></header>
        <div class="dialog-field" *ngIf="request.inputType">
          <label for="app-dialog-value">{{ request.inputLabel || 'Reason' }}<span *ngIf="request.required"> *</span></label>
          <textarea #field id="app-dialog-value" *ngIf="request.inputType === 'textarea'" [(ngModel)]="value" [maxlength]="request.maxLength || 2000" [placeholder]="request.placeholder || ''" rows="4"></textarea>
          <input #field id="app-dialog-value" *ngIf="request.inputType === 'text'" [(ngModel)]="value" [maxlength]="request.maxLength || 500" [placeholder]="request.placeholder || ''">
          <select #field id="app-dialog-value" *ngIf="request.inputType === 'select'" [(ngModel)]="value">
            <option value="">Select an option</option><option *ngFor="let option of request.options || []" [value]="option.value" [disabled]="option.disabled">{{ option.label }}</option>
          </select>
          <div class="dialog-meta"><span class="dialog-error" *ngIf="error">{{ error }}</span><span *ngIf="request.maxLength && request.inputType !== 'select'">{{ value.length }}/{{ request.maxLength }}</span></div>
        </div>
        <footer><button type="button" class="dialog-cancel" (click)="cancel()">{{ request.cancelText || 'Cancel' }}</button><button type="button" [class.dialog-danger]="request.tone === 'danger'" class="dialog-confirm" (click)="submit()">{{ request.confirmText || 'Confirm' }}</button></footer>
      </section>
    </div>`,
  styles: [`
    .dialog-backdrop{position:fixed;inset:0;z-index:10000;background:rgba(2,18,36,.62);backdrop-filter:blur(4px);display:grid;place-items:center;padding:20px}.dialog-card{width:min(540px,100%);background:#fff;border-radius:16px;box-shadow:0 28px 80px rgba(0,18,38,.35);overflow:hidden;color:#092744}.dialog-card header{display:flex;gap:14px;padding:24px 24px 18px;border-bottom:1px solid #e4ebf3}.dialog-icon{width:42px;height:42px;border-radius:50%;display:grid;place-items:center;background:#e8f3ff;color:#075ca8;flex:none}.dialog-card h2{font-size:20px;margin:0 0 7px}.dialog-card p{margin:0;color:#52677d;line-height:1.5;white-space:pre-line}.dialog-field{padding:20px 24px 4px}.dialog-field label{font-weight:700;display:block;margin-bottom:8px}.dialog-field textarea,.dialog-field input,.dialog-field select{width:100%;border:1px solid #b8c8da;border-radius:10px;padding:12px 14px;font:inherit;box-sizing:border-box;background:#fff;color:#092744}.dialog-field textarea:focus,.dialog-field input:focus,.dialog-field select:focus{outline:0;border-color:#0878d1;box-shadow:0 0 0 3px rgba(8,120,209,.14)}.dialog-meta{min-height:24px;display:flex;justify-content:space-between;gap:12px;padding-top:6px;color:#718197;font-size:12px}.dialog-error{color:#b42318;font-weight:600}.dialog-card footer{display:flex;justify-content:flex-end;gap:10px;padding:18px 24px 22px}.dialog-card button{border-radius:9px;padding:11px 18px;font-weight:700;cursor:pointer}.dialog-cancel{background:#fff;border:1px solid #bdcad8;color:#334e68}.dialog-confirm{border:1px solid #075ca8;background:#075ca8;color:#fff}.dialog-confirm.dialog-danger{background:#b42318;border-color:#b42318}@media(max-width:520px){.dialog-card header{padding:20px}.dialog-card footer{padding:16px 20px}.dialog-field{padding:18px 20px 2px}.dialog-card footer button{flex:1}}
  `]
})
export class AppDialog {
  readonly dialog = inject(DialogService);
  @ViewChild('field') field?: ElementRef<HTMLElement>;
  value = '';
  error = '';

  constructor() {
    effect(() => {
      const request = this.dialog.active();
      this.value = request?.initialValue || '';
      this.error = '';
      if (request) setTimeout(() => this.field?.nativeElement.focus());
    });
  }

  cancel(): void { this.dialog.finish(null); }
  submit(): void {
    const request = this.dialog.active();
    if (!request) return;
    const value = request.inputType ? this.value.trim() : 'confirmed';
    const minimum = request.minLength || (request.required ? 1 : 0);
    if (value.length < minimum) { this.error = `Enter at least ${minimum} characters.`; return; }
    if (request.maxLength && value.length > request.maxLength) { this.error = `Enter no more than ${request.maxLength} characters.`; return; }
    this.dialog.finish(value);
  }
}
