import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_URL } from '../config/api.config';

export interface RuntimePolicies {
  docketPriorityHighDays: number;
  archiveEligibleDays: number;
  evidenceRetentionYears: number;
  inspectionReminderHours: number;
  inspectionSlaHours: number;
  maxActiveDocketsPerOfficer: number;
}

export interface OperationalLookups {
  docketStatuses: string[];
  provinces: string[];
  courtOutcomes: string[];
  incidentLocationTypes?: string[];
  evidenceCollectionMethods?: string[];
  evidenceItemStates?: string[];
  runtimePolicies?: Partial<RuntimePolicies>;
}

export const DEFAULT_PROVINCES = [
  'Eastern Cape',
  'Free State',
  'Gauteng',
  'KwaZulu-Natal',
  'Limpopo',
  'Mpumalanga',
  'North West',
  'Northern Cape',
  'Western Cape'
];

export const DEFAULT_DOCKET_STATUSES = [
  'OPEN',
  'ALLOCATED',
  'UNDER_INVESTIGATION',
  'PENDING_PROSECUTOR',
  'COURT_OUTCOME_RECORDED',
  'CLOSED',
  'ARCHIVED',
  'REOPENED'
];

export const DEFAULT_COURT_OUTCOMES = [
  'Guilty',
  'Not Guilty',
  'Case Withdrawn',
  'Case Struck Off'
];

export const DEFAULT_INCIDENT_LOCATION_TYPES = [
  'Residential Property',
  'Business Premises',
  'Street / Road',
  'Highway',
  'Public Transport Hub',
  'School / Campus',
  'Hospital / Clinic',
  'Government Facility',
  'Open Field / Rural Area',
  'Shopping Centre',
  'Informal Settlement',
  'Police Station',
  'Correctional Facility',
  'Other'
];

export const DEFAULT_EVIDENCE_COLLECTION_METHODS = [
  'Photographed',
  'Bagged and Tagged',
  'Fingerprint Lift',
  'DNA Swab',
  'Digital Extraction',
  'Scene Seizure',
  'Voluntary Handover',
  'Search and Seizure',
  'CCTV Retrieval',
  'Forensic Collection'
];

export const DEFAULT_EVIDENCE_ITEM_STATES = [
  'Sealed',
  'Unsealed',
  'Intact',
  'Damaged',
  'Wet',
  'Dry',
  'Contaminated',
  'Fragile',
  'Operational',
  'Inoperable'
];

export const DEFAULT_RUNTIME_POLICIES: RuntimePolicies = {
  docketPriorityHighDays: 7,
  archiveEligibleDays: 90,
  evidenceRetentionYears: 7,
  inspectionReminderHours: 4,
  inspectionSlaHours: 24,
  maxActiveDocketsPerOfficer: 10
};

@Injectable({ providedIn: 'root' })
export class ReferenceDataService {
  private http = inject(HttpClient);

  getOperationalLookups(): Observable<OperationalLookups> {
    return this.http.get<OperationalLookups>(`${API_URL}/reference-data/operational`);
  }
}
