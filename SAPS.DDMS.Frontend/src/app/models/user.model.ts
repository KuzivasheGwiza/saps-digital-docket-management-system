// src/app/models/user.model.ts
export interface User {
    userID: number;
    firstName: string;
    lastName: string;
    fullName: string;
    personnelNumber: string;
    rank: string;
    emailAddress: string;
    phoneNumber: string;
    roleName: string;
    stationName: string;
    accountStatus?: string;
    notificationPreferences?: string;
    isActivated: boolean;
    isActive: boolean;
    isLocked: boolean;
    dateCreated: Date;
    lastLoginDate?: Date;
    activationEmailSent?: boolean;
    activationLink?: string;
    permissions?: string[];
}

export interface CreateUserDto {
    firstName: string;
    lastName: string;
    personnelNumber?: string;
    rank: string;
    stationID: number;
    emailAddress: string;
    phoneNumber: string;
    roleID: number;
}

export interface UpdateUserDto {
    firstName: string;
    lastName: string;
    rank: string;
    stationID: number;
    emailAddress: string;
    phoneNumber: string;
    roleID: number;
    reason?: string;
}

export interface Role {
    id: number;
    name: string;
    description: string;
}

export interface Station {
    id: number;
    name: string;
    province: string;
    region: string;
}

export interface LoginRequest {
    /** The API keeps the legacy property name, but accepts a personnel number or email address. */
    personnelNumber: string;
    password: string;
}

export interface AuthResponse {
    success: boolean;
    message?: string;
    token?: string;
    accessToken?: string;
    expiration?: Date;
    sessionTimeoutMinutes?: number;
    requiresOtp?: boolean;
    pendingPersonnelNumber?: string;
    otpExpiresAt?: Date;
    deliveryHint?: string;
    user?: UserResponse;
    data?: {
        token?: string;
        accessToken?: string;
        user?: UserResponse;
        sessionTimeoutMinutes?: number;
    };
}

export interface UserResponse {
    userID: number;
    firstName: string;
    lastName: string;
    fullName: string;
    personnelNumber: string;
    rank: string;
    emailAddress: string;
    phoneNumber: string;
    roleName: string;
    stationName: string;
    accountStatus?: string;
    notificationPreferences?: string;
    isActivated: boolean;
    isActive: boolean;
    isLocked: boolean;
    dateCreated: Date;
    lastLoginDate?: Date;
    activationEmailSent?: boolean;
    activationLink?: string;
    permissions?: string[];
}
