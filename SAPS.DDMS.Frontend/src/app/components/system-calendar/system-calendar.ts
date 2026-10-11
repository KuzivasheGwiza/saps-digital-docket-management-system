import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';

interface SystemCalendarEvent {
  id: string;
  title: string;
  date: string;
  category: 'Administration' | 'Backup' | 'Maintenance' | 'Security';
  description: string;
}

interface SystemCalendarDay {
  date: Date;
  inMonth: boolean;
  events: SystemCalendarEvent[];
}

@Component({
  selector: 'app-system-calendar',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './system-calendar.html',
  styleUrls: ['./system-calendar.css']
})
export class SystemCalendar implements OnInit {
  private readonly storageKey = 'saps-ddms-system-calendar-events';
  readonly weekdays = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  readonly categories: SystemCalendarEvent['category'][] = ['Administration', 'Backup', 'Maintenance', 'Security'];

  currentMonth = new Date();
  days: SystemCalendarDay[] = [];
  events: SystemCalendarEvent[] = [];
  showEditor = false;
  editingId: string | null = null;
  pendingDelete: SystemCalendarEvent | null = null;
  form = this.emptyForm();

  constructor(private toastr: ToastrService) {}

  ngOnInit(): void {
    this.loadEvents();
    this.buildCalendar();
  }

  get monthLabel(): string {
    return this.currentMonth.toLocaleString(undefined, { month: 'long', year: 'numeric' });
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

  openCreate(date?: Date): void {
    this.editingId = null;
    this.form = this.emptyForm(date ? this.toDateInput(date) : this.toDateInput(new Date()));
    this.showEditor = true;
  }

  openEdit(event: SystemCalendarEvent): void {
    this.editingId = event.id;
    this.form = { title: event.title, date: event.date, category: event.category, description: event.description };
    this.showEditor = true;
  }

  closeEditor(): void {
    this.showEditor = false;
    this.editingId = null;
    this.form = this.emptyForm();
  }

  saveEvent(): void {
    const title = this.form.title.trim();
    const description = this.form.description.trim();
    const eventDate = this.form.date;
    if (!title || !this.form.date) {
      this.toastr.warning('Enter an event title and date before saving.', 'Event not saved');
      return;
    }

    if (this.editingId) {
      this.events = this.events.map(item => item.id === this.editingId
        ? { ...item, title, date: this.form.date, category: this.form.category, description }
        : item);
      this.toastr.success('System calendar event updated successfully.');
    } else {
      this.events.push({
        id: globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random()}`,
        title,
        date: this.form.date,
        category: this.form.category,
        description
      });
      this.toastr.success('System calendar event added successfully.');
    }

    this.persistEvents();
    this.closeEditor();
    const savedDate = this.parseDate(eventDate);
    if (savedDate) this.currentMonth = new Date(savedDate.getFullYear(), savedDate.getMonth(), 1);
    this.buildCalendar();
  }

  requestDelete(event: SystemCalendarEvent): void {
    this.pendingDelete = event;
  }

  confirmDelete(): void {
    if (!this.pendingDelete) return;
    this.events = this.events.filter(item => item.id !== this.pendingDelete!.id);
    this.persistEvents();
    this.buildCalendar();
    this.pendingDelete = null;
    this.toastr.success('System calendar event removed.');
  }

  private loadEvents(): void {
    try {
      const stored = JSON.parse(localStorage.getItem(this.storageKey) || '[]');
      this.events = Array.isArray(stored) ? stored.filter(item => item?.id && item?.title && item?.date) : [];
    } catch {
      this.events = [];
      this.toastr.warning('Saved calendar reminders could not be read and were reset.');
    }
  }

  private persistEvents(): void {
    localStorage.setItem(this.storageKey, JSON.stringify(this.events));
  }

  private buildCalendar(): void {
    const year = this.currentMonth.getFullYear();
    const month = this.currentMonth.getMonth();
    const first = new Date(year, month, 1);
    const start = new Date(year, month, 1 - first.getDay());
    this.days = Array.from({ length: 42 }, (_, offset) => {
      const date = new Date(start.getFullYear(), start.getMonth(), start.getDate() + offset);
      const key = this.toDateInput(date);
      return {
        date,
        inMonth: date.getMonth() === month,
        events: this.events.filter(event => event.date === key).sort((a, b) => a.title.localeCompare(b.title))
      };
    });
  }

  private emptyForm(date = ''): Omit<SystemCalendarEvent, 'id'> {
    return { title: '', date, category: 'Administration', description: '' };
  }

  private toDateInput(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private parseDate(value: string): Date | null {
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
    return match ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3])) : null;
  }
}
