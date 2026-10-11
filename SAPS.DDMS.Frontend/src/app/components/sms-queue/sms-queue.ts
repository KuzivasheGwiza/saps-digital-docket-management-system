import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterModule } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { API_URL } from '../../config/api.config';
import { DialogService } from '../../shared/dialog.service';

interface SmsQueueItem {
  smsMessageID: number;
  userName?: string;
  recipientNumber: string;
  message: string;
  purpose: string;
  createdAt: string;
  sentAt?: string;
  status: string;
  providerReference?: string;
}

@Component({
  selector: 'app-sms-queue',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './sms-queue.html',
  styleUrls: ['./sms-queue.css']
})
export class SmsQueue implements OnInit {
  private http = inject(HttpClient);
  private toastr = inject(ToastrService);
  private cdr = inject(ChangeDetectorRef);
  private dialog = inject(DialogService);

  items: SmsQueueItem[] = [];
  isLoading = false;
  statusFilter = '';
  errorMessage = '';
  private updatingIds = new Set<number>();

  ngOnInit(): void {
    this.loadQueue();
  }

  loadQueue(): void {
    this.isLoading = true;
    this.errorMessage = '';
    const suffix = this.statusFilter ? `?status=${encodeURIComponent(this.statusFilter)}` : '';
    this.http.get<SmsQueueItem[]>(`${API_URL}/sms-queue${suffix}`).subscribe({
      next: items => {
        this.items = items || [];
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: err => {
        this.errorMessage = err.error?.message || 'SMS queue could not be loaded.';
        this.toastr.error(this.errorMessage);
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  markDispatched(item: SmsQueueItem): void {
    if (this.isUpdating(item)) return;
    this.updatingIds.add(item.smsMessageID);
    this.http.patch(`${API_URL}/sms-queue/${item.smsMessageID}/dispatch`, {}).subscribe({
      next: () => {
        this.updatingIds.delete(item.smsMessageID);
        this.toastr.success('SMS marked as dispatched');
        this.cdr.detectChanges();
        this.loadQueue();
      },
      error: err => {
        this.updatingIds.delete(item.smsMessageID);
        this.toastr.error(err.error?.message || 'SMS could not be updated');
        this.cdr.detectChanges();
      }
    });
  }

  async cancel(item: SmsQueueItem): Promise<void> {
    if (this.isUpdating(item)) return;
    const confirmed = await this.dialog.confirm({
      title: 'Cancel Queued SMS',
      message: `Cancel the operational SMS to ${item.recipientNumber}? It will not be sent and this action will be recorded.`,
      confirmText: 'Cancel SMS', tone: 'danger'
    });
    if (!confirmed) return;
    this.updatingIds.add(item.smsMessageID);
    this.http.patch(`${API_URL}/sms-queue/${item.smsMessageID}/cancel`, {}).subscribe({
      next: () => {
        this.updatingIds.delete(item.smsMessageID);
        this.toastr.success('SMS cancelled');
        this.cdr.detectChanges();
        this.loadQueue();
      },
      error: err => {
        this.updatingIds.delete(item.smsMessageID);
        this.toastr.error(err.error?.message || 'SMS could not be cancelled');
        this.cdr.detectChanges();
      }
    });
  }

  canDispatch(item: SmsQueueItem): boolean {
    return item.status.startsWith('Queued') && !!item.recipientNumber;
  }

  isUpdating(item: SmsQueueItem): boolean {
    return this.updatingIds.has(item.smsMessageID);
  }

  statusClass(item: SmsQueueItem): string {
    if (item.status.startsWith('Queued')) return 'queued';
    if (item.status.startsWith('Sent')) return 'sent';
    if (item.status === 'Cancelled') return 'cancelled';
    return 'neutral';
  }
}
