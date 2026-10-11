// backup.ts — Complexity 10a: Backup/restore from front end
import { Component, OnInit, inject, ChangeDetectorRef, ChangeDetectionStrategy, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { ToastrService } from 'ngx-toastr';
import { AuthService } from '../../services/auth';
import { API_URL } from '../../config/api.config';

export interface BackupRecord {
  backupID: number;
  fileName: string;
  createdAt: Date;
  createdBy: string;
  sizeMB: number;
  status: string;
  notes?: string;
}

@Component({
  selector: 'app-backup',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './backup.html',
  styleUrls: ['./backup.css'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Backup implements OnInit, OnDestroy {
  private http = inject(HttpClient);
  private authService = inject(AuthService);
  private toastr = inject(ToastrService);
  private cdr = inject(ChangeDetectorRef);
  private router = inject(Router);
  private destroy$ = new Subject<void>();

  backups: BackupRecord[] = [];
  isLoading = false;
  isCreating = false;
  isRestoring = false;
  selectedBackup: BackupRecord | null = null;
  showRestoreModal = false;
  currentUser = this.authService.getCurrentUser();

  ngOnInit(): void { this.loadBackups(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadBackups(): void {
    this.isLoading = true;
    this.cdr.markForCheck();
    this.http.get<BackupRecord[]>(`${API_URL}/backup`)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (b) => { this.backups = b || []; this.isLoading = false; this.cdr.markForCheck(); },
        error: () => {
          this.backups = [];
          this.isLoading = false;
          this.toastr.error('Backup records could not be loaded');
          this.cdr.markForCheck();
        }
      });
  }

  // Complexity 10a — Create backup from front end
  createBackup(): void {
    this.isCreating = true;
    this.cdr.markForCheck();
    this.http.post<BackupRecord>(`${API_URL}/backup/create`, {})
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (b) => {
          this.backups.unshift(b);
          this.isCreating = false;
          this.toastr.success('Database backup created successfully');
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.isCreating = false;
          this.toastr.error(err.error?.message || 'Database backup could not be created');
          this.cdr.markForCheck();
        }
      });
  }

  // Complexity 10a — Restore from selected backup
  openRestoreModal(backup: BackupRecord): void { this.selectedBackup = backup; this.showRestoreModal = true; this.cdr.markForCheck(); }
  closeRestoreModal(): void { this.showRestoreModal = false; this.selectedBackup = null; this.cdr.markForCheck(); }

  confirmRestore(): void {
    if (!this.selectedBackup) return;
    this.isRestoring = true;
    this.cdr.markForCheck();
    this.http.post(`${API_URL}/backup/restore/${this.selectedBackup.backupID}`, {})
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response: any) => {
          this.toastr.success(response?.message || 'Backup restored successfully. Please sign in again.');
          this.closeRestoreModal();
          this.isRestoring = false;
          this.authService.clearSession();
          this.cdr.markForCheck();
          void this.router.navigate(['/login']);
        },
        error: (err) => { this.toastr.error(err.error?.message || 'Restore could not be initiated'); this.isRestoring = false; this.cdr.markForCheck(); }
      });
  }

  downloadBackup(backup: BackupRecord): void {
    this.http.get(`${API_URL}/backup/download/${backup.backupID}`, {
      observe: 'response',
      responseType: 'blob'
    }).pipe(takeUntil(this.destroy$))
      .subscribe({
        next: response => {
          const blob = response.body;
          if (!blob) {
            this.toastr.error('The backup download was empty');
            return;
          }

          const safeName = (backup.fileName || `saps-ddms-backup-${backup.backupID}.bak`)
            .replace(/[^a-zA-Z0-9._-]/g, '_');
          const objectUrl = URL.createObjectURL(blob);
          const anchor = document.createElement('a');
          anchor.href = objectUrl;
          anchor.download = safeName;
          anchor.style.display = 'none';
          document.body.appendChild(anchor);
          anchor.click();
          anchor.remove();
          URL.revokeObjectURL(objectUrl);
        },
        error: err => this.toastr.error(err.error?.message || 'Backup could not be downloaded')
      });
  }
}
