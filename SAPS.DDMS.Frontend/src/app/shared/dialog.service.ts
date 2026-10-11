import { Injectable, signal } from '@angular/core';

export interface AppDialogOption {
  value: string;
  label: string;
  disabled?: boolean;
}

export interface AppDialogRequest {
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  tone?: 'primary' | 'danger';
  inputLabel?: string;
  inputType?: 'text' | 'textarea' | 'select';
  initialValue?: string;
  placeholder?: string;
  minLength?: number;
  maxLength?: number;
  required?: boolean;
  options?: AppDialogOption[];
  resolve: (value: string | null) => void;
}

@Injectable({ providedIn: 'root' })
export class DialogService {
  readonly active = signal<AppDialogRequest | null>(null);

  open(options: Omit<AppDialogRequest, 'resolve'>): Promise<string | null> {
    this.active()?.resolve(null);
    return new Promise(resolve => this.active.set({ ...options, resolve }));
  }

  confirm(options: Omit<AppDialogRequest, 'resolve' | 'inputType'>): Promise<boolean> {
    return this.open({ ...options, inputType: undefined }).then(value => value !== null);
  }

  finish(value: string | null): void {
    const current = this.active();
    this.active.set(null);
    current?.resolve(value);
  }
}
