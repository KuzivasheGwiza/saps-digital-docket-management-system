import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_URL } from '../config/api.config';

export interface SystemSetting {
  settingID: number;
  settingKey: string;
  settingValue: string;
  description: string;
  dataType: string;
  updatedAt: Date;
  updatedBy: string;
}

@Injectable({ providedIn: 'root' })
export class SystemConfigService {
  private http = inject(HttpClient);

  getSettings(): Observable<SystemSetting[]> {
    return this.http.get<SystemSetting[]>(`${API_URL}/system-config`);
  }

  getSettingsMap(): Observable<Record<string, string>> {
    return this.getSettings().pipe(
      map(settings => settings.reduce<Record<string, string>>((result, setting) => {
        result[setting.settingKey] = setting.settingValue;
        return result;
      }, {}))
    );
  }
}
