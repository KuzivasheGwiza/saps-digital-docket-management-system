// src/app/components/dashboard/dashboard.ts
import {
  Component,
  OnInit,
  AfterViewInit,
  OnDestroy,
  ElementRef,
  ViewChild,
  inject,
  NgZone,
  ChangeDetectorRef
} from '@angular/core';

import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

import { AuthService } from '../../services/auth';
import { DashboardService } from '../../services/dashboard';

import { UserResponse } from '../../models/user.model';
import { DashboardDocket, DashboardStats } from '../../models/dashboard.model';

import { Chart, registerables } from 'chart.js';

Chart.register(...registerables);

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './dashboard.html',
  styleUrls: ['./dashboard.css']
})
export class Dashboard implements OnInit, AfterViewInit, OnDestroy {

  @ViewChild('statusChart') statusChartRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('trendChart') trendChartRef!: ElementRef<HTMLCanvasElement>;

  private authService = inject(AuthService);
  private dashboardService = inject(DashboardService);
  private zone = inject(NgZone);
  private cdr = inject(ChangeDetectorRef);

  currentUser: UserResponse | null = null;
  recentDockets: DashboardDocket[] = [];
  inspectionDockets: DashboardDocket[] = [];
  stats: DashboardStats | null = null;
  isLoading = true;

  private statusChart: Chart<'bar', number[], string> | null = null;
  private trendChart: Chart<'bar', number[], string> | null = null;
  private chartsReady = false;

  ngOnInit(): void {
    this.currentUser = this.authService.getCurrentUser();
    if (!this.isSystemAdministrator) {
      this.loadDashboardData();
    } else {
      this.isLoading = false;
    }
  }

  get isSystemAdministrator(): boolean {
    return this.currentUser?.roleName === 'System Administrator';
  }

  ngAfterViewInit(): void {
    this.chartsReady = true;
    this.renderChartsWhenReady();
  }

  // Count-up animation for stat numbers
  animateCountUp(elementId: string, target: number, duration = 800): void {
    const el = document.getElementById(elementId);
    if (!el) return;
    const start = Date.now();
    const tick = () => {
      const elapsed = Date.now() - start;
      const progress = Math.min(elapsed / duration, 1);
      const ease = 1 - Math.pow(1 - progress, 3); // cubic ease-out
      el.textContent = Math.round(ease * target).toString();
      if (progress < 1) requestAnimationFrame(tick);
    };
    requestAnimationFrame(tick);
  }

  // Animate all stats after load
  animateStats(): void {
    if (!this.stats) return;
    setTimeout(() => {
      this.animateCountUp('stat-total',       this.stats!.totalDockets,          900);
      this.animateCountUp('stat-open',        this.stats!.openDockets,            800);
      this.animateCountUp('stat-allocated',   this.stats!.allocatedDockets,       700);
      this.animateCountUp('stat-investigation',this.stats!.underInvestigation,    600);
    }, 200);
  }

  ngOnDestroy(): void {
    this.statusChart?.destroy();
    this.trendChart?.destroy();
  }

  loadDashboardData(): void {
    this.isLoading = true;

    this.dashboardService.getDashboardStats().subscribe({
      next: (stats) => {
        this.stats = stats;
        this.recentDockets = stats.recentDockets || [];
        this.inspectionDockets = stats.inspectionDockets || [];
        this.isLoading = false;
        this.cdr.detectChanges();
        this.renderChartsWhenReady();
      },
      error: (error) => {
        console.error('Failed to load dashboard stats', error);
        this.isLoading = false;
      }
    });
  }

  initCharts(): void {
    if (!this.stats) return;

    this.initStatusChart();
    this.initTrendChart();
  }

  private renderChartsWhenReady(): void {
    if (!this.stats || !this.chartsReady) return;

    requestAnimationFrame(() => {
      this.zone.runOutsideAngular(() => this.initCharts());
    });
  }

  private initStatusChart(): void {
    if (!this.statusChartRef?.nativeElement || !this.stats) return;

    const ctx = this.statusChartRef.nativeElement.getContext('2d');
    if (!ctx) return;

    this.statusChart?.destroy();

    this.statusChart = new Chart<'bar', number[], string>(ctx, {
      type: 'bar',
      data: {
        labels: this.stats.statusDistribution.map(s => s.status),
        datasets: [
          {
            data: this.stats.statusDistribution.map(s => s.count),
            backgroundColor: [
              '#001F3F',
              '#005BAA',
              '#0074D9',
              '#FFB81C',
              '#D99A00',
              '#E6F2FF'
            ],
            borderColor: '#001F3F',
            borderWidth: 1,
            borderRadius: 6,
            maxBarThickness: 44
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            display: false
          }
        },
        scales: {
          x: {
            grid: { display: false }
          },
          y: {
            beginAtZero: true,
            ticks: { precision: 0 }
          }
        }
      }
    });
  }

  private initTrendChart(): void {
    if (!this.trendChartRef?.nativeElement || !this.stats) return;

    const ctx = this.trendChartRef.nativeElement.getContext('2d');
    if (!ctx) return;

    this.trendChart?.destroy();

    this.trendChart = new Chart<'bar', number[], string>(ctx, {
      type: 'bar',
      data: {
        labels: this.stats.monthlyTrends.map(t => t.month),
        datasets: [
          {
            label: 'Dockets Created',
            data: this.stats.monthlyTrends.map(t => t.count),
            borderColor: '#005BAA',
            backgroundColor: 'rgba(0, 91, 170, 0.78)',
            borderWidth: 1,
            borderRadius: 6,
            maxBarThickness: 48
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            position: 'top'
          }
        },
        scales: {
          y: {
            beginAtZero: true,
            ticks: { precision: 0 }
          },
          x: {
            grid: { display: false }
          }
        }
      }
    });
  }

  getRoleBadgeClass(role: string): string {
    const roleMap: Record<string, string> = {
      'System Administrator': 'role-admin',
      'DSC Supervisor': 'role-supervisor',
      'Investigating Officer': 'role-io',
      'CSC Officer': 'role-csc',
      'Docket Administrator': 'role-admin',
      'Records Clerk': 'role-clerk'
    };
    return roleMap[role] || 'role-default';
  }

  getStatusBadgeClass(status: string): string {
    const statusMap: Record<string, string> = {
      'OPEN': 'status-open',
      'ALLOCATED': 'status-allocated',
      'UNDER_INVESTIGATION': 'status-investigation',
      'PENDING_PROSECUTOR': 'status-pending',
      'CLOSED': 'status-closed'
    };
    return statusMap[status] || 'status-default';
  }

  getInspectionCompliance(): { completed: number; overdue: number; pending: number; percent: number } {
    if (this.stats) {
      return {
        completed: this.stats.inspectionCompleted || 0,
        overdue: this.stats.inspectionOverdue || 0,
        pending: this.stats.inspectionPending || 0,
        percent: this.stats.inspectionCompliancePercent || 0
      };
    }

    const tracked = this.inspectionDockets.filter(d => !!d.inspectionDueAt || !!d.assignmentAcceptedAt);
    const completed = tracked.filter(d => !!d.inspectionCompletedAt).length;
    const overdue = tracked.filter(d => !d.inspectionCompletedAt && d.inspectionDueAt && new Date(d.inspectionDueAt).getTime() < Date.now()).length;
    const pending = Math.max(tracked.length - completed - overdue, 0);
    const percent = tracked.length ? Math.round((completed / tracked.length) * 100) : 0;
    return { completed, overdue, pending, percent };
  }

  getInspectionStatus(docket: DashboardDocket): string {
    if (docket.inspectionCompletedAt) return 'Completed';
    if (!docket.inspectionDueAt) return 'Awaiting acceptance';
    return new Date(docket.inspectionDueAt).getTime() < Date.now() ? 'Overdue' : 'Pending';
  }
}
