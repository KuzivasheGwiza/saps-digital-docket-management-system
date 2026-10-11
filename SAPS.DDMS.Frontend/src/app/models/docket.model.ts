export interface Incident {
    incidentID: number;
    occurrenceDate: Date;
    locationType: string;
    description: string;
    incidentType: string;
    areaName?: string;
    latitude?: string;
    longitude?: string;
}

export interface Docket {
    docketID: number;
    investigationID?: number;
    docketNumber: string;
    obReferenceNumber: string;
    ficReferenceNumber?: string;
    docketSummary: string;
    modusOperandi?: string;
    priorityLevel: string;
    sensitivityLevel: 'NORMAL' | 'RESTRICTED' | 'HIGHLY_SENSITIVE';
    caseType?: string;
    complainantFullName?: string;
    complainantContactNumber?: string;
    status: string;
    dateCreated: Date;
    dateAllocated?: Date;
    assignmentAcceptedAt?: Date;
    inspectionDueAt?: Date;
    inspectionCompletedAt?: Date;
    allocationReviewStatus?: string;
    allocationReviewNotes?: string;
    crimeCategory?: string;
    stationName?: string;
    jurisdiction?: string;
    suspectName?: string;
    privacyFlag?: boolean;
    docketArchivedAt?: Date;
    docketClosedAt?: Date;
    reopenedAt?: Date;
    incident: Incident;
    createdByUserID?: number;
    assignedOfficerUserID?: number;
    createdByName: string;
    assignedOfficerName?: string;
}

export interface CreateIncidentDto {
    occurrenceDate: Date;
    locationType: string;
    description: string;
    incidentTypeID?: number;
    customIncidentType?: string;
    latitude?: string;
    longitude?: string;
    areaName?: string;
}

export interface CreateDocketDto {
    obReferenceNumber?: string;
    ficReferenceNumber?: string;
    docketSummary: string;
    modusOperandi?: string;
    incident: CreateIncidentDto;
    complainantFullName: string;
    complainantContactNumber: string;
    caseType: string;
    sensitivityLevel: 'NORMAL' | 'RESTRICTED' | 'HIGHLY_SENSITIVE';
}

export interface UpdateDocketDto {
    docketSummary: string;
    modusOperandi: string;
    sensitivityLevel: 'NORMAL' | 'RESTRICTED' | 'HIGHLY_SENSITIVE';
    reasonForUpdate: string;
}

export interface AllocateDocketDto {
    investigatingOfficerUserID: number;
}

export interface ReassignDocketDto extends AllocateDocketDto {
    reason: string;
}
