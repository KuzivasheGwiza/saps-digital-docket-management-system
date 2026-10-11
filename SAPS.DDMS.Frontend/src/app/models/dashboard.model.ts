// src/app/models/dashboard.model.ts
export interface DashboardStats {
    totalDockets: number;
    openDockets: number;
    allocatedDockets: number;
    underInvestigation: number;
    closedDockets: number;
    inspectionCompleted: number;
    inspectionPending: number;
    inspectionOverdue: number;
    inspectionCompliancePercent: number;
    statusDistribution: StatusCount[];
    monthlyTrends: MonthlyTrend[];
    officerWorkload: OfficerWorkload[];
    recentDockets: DashboardDocket[];
    inspectionDockets: DashboardDocket[];
}

export interface DashboardDocket {
    docketID: number;
    docketNumber: string;
    obReferenceNumber: string;
    status: string;
    dateCreated: Date;
    assignmentAcceptedAt?: Date;
    inspectionDueAt?: Date;
    inspectionCompletedAt?: Date;
}

export interface StatusCount {
    status: string;
    count: number;
}

export interface MonthlyTrend {
    month: string;
    count: number;
}

export interface OfficerWorkload {
    officerName: string;
    assignedCases: number;
    completedCases: number;
}
