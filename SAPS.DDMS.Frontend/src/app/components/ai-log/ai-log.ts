import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { AiService } from '../../services/ai';
import { ToastrService } from 'ngx-toastr';

@Component({
  selector: 'app-ai-log',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './ai-log.html',
  styleUrls: ['./ai-log.css']
})
export class AiLog implements OnInit {
  private aiService = inject(AiService);
  private toastr = inject(ToastrService);
  private cdr = inject(ChangeDetectorRef);
  logs: any[] = [];
  isLoading = false;
  errorMessage = '';

  ngOnInit(): void {
    this.loadLogs();
  }

  loadLogs(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.aiService.getLogs().subscribe({
      next: logs => {
        this.logs = logs || [];
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: error => {
        this.logs = [];
        this.errorMessage = error.status === 403
          ? 'You do not have permission to view the AI interaction log.'
          : error.error?.message || 'AI interaction log could not be loaded.';
        this.toastr.error(this.errorMessage);
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }
}
