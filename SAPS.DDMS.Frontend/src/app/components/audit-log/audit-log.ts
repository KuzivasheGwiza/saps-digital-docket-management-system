import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AuditLogFilters, AuditService } from '../../services/audit';

interface AuditUserOption {
  id: number;
  name: string;
}

@Component({
  selector: 'app-audit-log',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './audit-log.html',
  styleUrls: ['./audit-log.css']
})
export class AuditLog implements OnInit {
  private auditService = inject(AuditService);
  private cdr = inject(ChangeDetectorRef);
  logs: any[] = [];
  searchTerm = '';
  actionType = '';
  selectedUserId: number | null = null;
  fromDate = '';
  toDate = '';
  actionOptions: string[] = [];
  userOptions: AuditUserOption[] = [];
  isLoading = false;
  isExporting = false;
  errorMessage = '';
  dateRangeError = '';

  ngOnInit(): void {
    this.loadLogs();
  }

  loadLogs(): void {
    const filters = this.buildFilters();
    if (!filters) return;

    this.isLoading = true;
    this.errorMessage = '';
    this.auditService.getLogs(filters).subscribe({
      next: logs => {
        this.logs = logs || [];
        this.captureFilterOptions(this.logs);
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.logs = [];
        this.errorMessage = 'Audit records could not be loaded. Please try again.';
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  exportLogs(): void {
    const filters = this.buildFilters();
    if (!filters) return;

    this.isExporting = true;
    this.errorMessage = '';
    this.auditService.exportLogs(filters).subscribe({
      next: blob => {
        const objectUrl = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = objectUrl;
        anchor.download = `saps-ddms-audit-${new Date().toISOString().slice(0, 10)}.csv`;
        anchor.style.display = 'none';
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
        URL.revokeObjectURL(objectUrl);
        this.isExporting = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.errorMessage = 'The filtered audit export could not be created. Please try again.';
        this.isExporting = false;
        this.cdr.detectChanges();
      }
    });
  }

  resetFilters(): void {
    this.searchTerm = '';
    this.actionType = '';
    this.selectedUserId = null;
    this.fromDate = '';
    this.toDate = '';
    this.dateRangeError = '';
    this.loadLogs();
  }

  getPayloadSummary(log: any): string {
    const details = this.readAuditDetails(log);
    if (details.newValue?.payloadHash) {
      return `Payload hash: ${details.newValue.payloadHash}`;
    }
    if (details.oldValue?.payloadHash) {
      return `Previous payload hash: ${details.oldValue.payloadHash}`;
    }
    if (details.newValue) return 'Updated record details available';
    if (details.oldValue) return 'Previous record snapshot available';
    return '-';
  }

  private readAuditDetails(log: any): { oldValue: any | null; newValue: any | null } {
    return {
      oldValue: this.parseJsonValue(log?.oldValue),
      newValue: this.parseJsonValue(log?.newValue)
    };
  }

  private parseJsonValue(value: unknown): any | null {
    if (typeof value !== 'string' || !value.trim()) return null;
    try {
      return JSON.parse(value);
    } catch {
      return value;
    }
  }

  private buildFilters(): AuditLogFilters | null {
    this.dateRangeError = '';
    if (this.fromDate && this.toDate && this.fromDate > this.toDate) {
      this.dateRangeError = 'The start date must be on or before the end date.';
      return null;
    }

    return {
      query: this.searchTerm,
      action: this.actionType,
      userId: this.selectedUserId,
      from: this.toIsoBoundary(this.fromDate, false),
      to: this.toIsoBoundary(this.toDate, true)
    };
  }

  private toIsoBoundary(value: string, endOfDay: boolean): string | undefined {
    if (!value) return undefined;
    const time = endOfDay ? '23:59:59.999' : '00:00:00.000';
    return new Date(`${value}T${time}`).toISOString();
  }

  private captureFilterOptions(logs: any[]): void {
    const actions = new Set(this.actionOptions);
    const users = new Map(this.userOptions.map(user => [user.id, user]));

    for (const log of logs) {
      if (typeof log?.actionType === 'string' && log.actionType.trim()) {
        actions.add(log.actionType.trim());
      }

      const id = Number(log?.fK_UserID);
      if (Number.isInteger(id) && id > 0) {
        users.set(id, {
          id,
          name: log.userName?.trim() || `User #${id}`
        });
      }
    }

    this.actionOptions = [...actions].sort((a, b) => a.localeCompare(b));
    this.userOptions = [...users.values()].sort((a, b) => a.name.localeCompare(b.name));
  }
}
