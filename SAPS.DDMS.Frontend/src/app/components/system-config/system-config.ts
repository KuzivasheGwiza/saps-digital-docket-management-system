import { Component, OnInit, inject, ChangeDetectorRef, ChangeDetectionStrategy, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { HttpClient } from '@angular/common/http';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../services/auth';
import { API_URL } from '../../config/api.config';

export interface SystemSetting {
  settingID: number;
  settingKey: string;
  settingValue: string;
  description: string;
  dataType: string;
  updatedAt: Date;
  updatedBy: string;
}

@Component({
  selector: 'app-system-config',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterModule],
  templateUrl: './system-config.html',
  styleUrls: ['./system-config.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SystemConfig implements OnInit, OnDestroy {
  private http = inject(HttpClient);
  private authService = inject(AuthService);
  private fb = inject(FormBuilder);
  private toastr = inject(ToastrService);
  private cdr = inject(ChangeDetectorRef);
  private destroy$ = new Subject<void>();

  settings: SystemSetting[] = [];
  isLoading = false;
  editingKey: string | null = null;
  editForm: FormGroup;

  timerSettings = [
    { key: 'SESSION_TIMEOUT_MINUTES',    label: 'Session Timeout (minutes)',        unit: 'minutes', min: 5,  max: 120 },
    { key: 'OTP_EXPIRY_MINUTES',         label: 'OTP Expiry (minutes)',             unit: 'minutes', min: 1,  max: 30 },
    { key: 'PASSWORD_RESET_EXPIRY_HOURS',label: 'Password Reset Link Expiry (hours)',unit: 'hours',  min: 1,  max: 72 },
    { key: 'INSPECTION_SLA_HOURS',       label: '24-hr Inspection SLA (hours)',     unit: 'hours',   min: 12, max: 72 },
    { key: 'MAX_LOGIN_ATTEMPTS',         label: 'Max Failed Login Attempts',         unit: 'attempts',min: 3, max: 10 },
  ];

  businessRuleSettings = [
    { key: 'DOCKET_PRIORITY_HIGH_DAYS',  label: 'High Priority Docket SLA (days)',  unit: 'days',   min: 1,  max: 30 },
    { key: 'EVIDENCE_RETENTION_YEARS',   label: 'Evidence Retention Period (years)', unit: 'years',  min: 1,  max: 30 },
    { key: 'ARCHIVE_ELIGIBLE_DAYS',      label: 'Docket Archive Eligible After (days)',unit: 'days', min: 30, max: 365 },
    { key: 'INSPECTION_REMINDER_HOURS',  label: 'Inspection Reminder Before SLA (hours)',unit:'hours',min:1, max:12 },
  ];

  securitySettings = [
    { key: 'TWO_FACTOR_REQUIRED', label: 'Require OTP for all logins', unit: 'true / false' }
  ];

  currentUser = this.authService.getCurrentUser();

  constructor() {
    this.editForm = this.fb.group({
      settingValue: ['', [Validators.required, Validators.min(1)]]
    });
  }

  ngOnInit(): void { this.loadSettings(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadSettings(): void {
    this.isLoading = true;
    this.cdr.markForCheck();
    this.http.get<SystemSetting[]>(`${API_URL}/system-config`)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (s) => { this.settings = s || []; this.isLoading = false; this.cdr.markForCheck(); },
        error: () => {
          // Defaults keep the settings page usable during a temporary API outage.
          this.settings = [
            ...this.timerSettings.map((t,i) => ({ settingID:i+1, settingKey:t.key, settingValue: t.min.toString(), description:t.label, dataType:'int', updatedAt: new Date(), updatedBy:'System' })),
            ...this.businessRuleSettings.map((b,i) => ({ settingID:i+10, settingKey:b.key, settingValue: b.min.toString(), description:b.label, dataType:'int', updatedAt: new Date(), updatedBy:'System' })),
            ...this.securitySettings.map((s,i) => ({ settingID:i+20, settingKey:s.key, settingValue: 'false', description:s.label, dataType:'bool', updatedAt: new Date(), updatedBy:'System' })),
          ];
          this.isLoading = false;
          this.cdr.markForCheck();
        }
      });
  }

  getSettingValue(key: string): string {
    return this.settings.find(s => s.settingKey === key)?.settingValue || '—';
  }

  startEdit(key: string): void {
    this.editingKey = key;
    this.editForm.patchValue({ settingValue: this.getSettingValue(key) });
    this.cdr.markForCheck();
  }

  cancelEdit(): void { this.editingKey = null; this.cdr.markForCheck(); }

  saveSetting(key: string): void {
    if (this.editForm.invalid) return;
    const value = this.editForm.value.settingValue.toString();
    this.http.patch(`${API_URL}/system-config/${key}`, { value })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          const s = this.settings.find(x => x.settingKey === key);
          if (s) { s.settingValue = value; s.updatedAt = new Date(); s.updatedBy = this.currentUser?.fullName || 'Admin'; }
          this.editingKey = null;
          this.toastr.success(`Setting "${key}" updated to ${value}`);
          this.cdr.markForCheck();
        },
        error: (err) => this.toastr.error(err.error?.message || 'Failed to update setting')
      });
  }
}
