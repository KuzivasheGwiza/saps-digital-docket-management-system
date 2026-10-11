import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import { DocketService } from '../../services/docket';
import { Docket } from '../../models/docket.model';

interface CalendarDay {
  date: Date;
  inMonth: boolean;
  dockets: Docket[];
}

@Component({
  selector: 'app-docket-calendar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './docket-calendar.html',
  styleUrls: ['./docket-calendar.css']
})
export class DocketCalendar implements OnInit {
  private docketService = inject(DocketService);
  private cdr = inject(ChangeDetectorRef);

  dockets: Docket[] = [];
  days: CalendarDay[] = [];
  currentMonth = new Date();
  isLoading = false;
  errorMessage = '';
  expandedDayKey: string | null = null;
  weekdays = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

  ngOnInit(): void {
    this.loadDockets();
  }

  loadDockets(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.docketService.getDockets().subscribe({
      next: dockets => {
        this.dockets = dockets || [];
        this.isLoading = false;
        this.buildCalendar();
        this.cdr.detectChanges();
      },
      error: err => {
        this.errorMessage = err.error?.message || 'Calendar could not be loaded.';
        this.isLoading = false;
        this.buildCalendar();
        this.cdr.detectChanges();
      }
    });
  }

  previousMonth(): void {
    this.currentMonth = new Date(this.currentMonth.getFullYear(), this.currentMonth.getMonth() - 1, 1);
    this.buildCalendar();
  }

  nextMonth(): void {
    this.currentMonth = new Date(this.currentMonth.getFullYear(), this.currentMonth.getMonth() + 1, 1);
    this.buildCalendar();
  }

  today(): void {
    this.currentMonth = new Date();
    this.buildCalendar();
  }

  showLatestActivity(): void {
    const eventDates = this.dockets.flatMap(docket => [
      docket.dateCreated,
      docket.dateAllocated,
      docket.inspectionDueAt
    ]).filter(Boolean).map(value => new Date(value!)).filter(value => !Number.isNaN(value.getTime()));

    if (!eventDates.length) return;
    const latest = eventDates.reduce((current, candidate) => candidate > current ? candidate : current);
    this.currentMonth = new Date(latest.getFullYear(), latest.getMonth(), 1);
    this.buildCalendar();
  }

  get monthEventCount(): number {
    return this.days
      .filter(day => day.inMonth)
      .reduce((total, day) => total + day.dockets.length, 0);
  }

  get monthLabel(): string {
    return this.currentMonth.toLocaleString(undefined, { month: 'long', year: 'numeric' });
  }

  isDayExpanded(day: CalendarDay): boolean {
    return this.expandedDayKey === this.dayKey(day.date);
  }

  toggleDay(day: CalendarDay): void {
    const key = this.dayKey(day.date);
    this.expandedDayKey = this.expandedDayKey === key ? null : key;
  }

  private buildCalendar(): void {
    this.expandedDayKey = null;
    const year = this.currentMonth.getFullYear();
    const month = this.currentMonth.getMonth();
    const first = new Date(year, month, 1);
    const start = new Date(year, month, 1 - first.getDay());
    const days: CalendarDay[] = [];

    for (let offset = 0; offset < 42; offset++) {
      const date = new Date(start.getFullYear(), start.getMonth(), start.getDate() + offset);
      days.push({
        date,
        inMonth: date.getMonth() === month,
        dockets: this.docketsForDate(date)
      });
    }

    this.days = days;
    this.cdr.detectChanges();
  }

  private docketsForDate(date: Date): Docket[] {
    return this.dockets.filter(docket => {
      const created = new Date(docket.dateCreated);
      const allocated = docket.dateAllocated ? new Date(docket.dateAllocated) : null;
      const due = docket.inspectionDueAt ? new Date(docket.inspectionDueAt) : null;
      return this.sameDay(created, date) || this.sameDay(allocated, date) || this.sameDay(due, date);
    });
  }

  private sameDay(candidate: Date | null, date: Date): boolean {
    if (!candidate || Number.isNaN(candidate.getTime())) return false;
    return candidate.getFullYear() === date.getFullYear()
      && candidate.getMonth() === date.getMonth()
      && candidate.getDate() === date.getDate();
  }

  private dayKey(date: Date): string {
    return `${date.getFullYear()}-${date.getMonth()}-${date.getDate()}`;
  }
}
