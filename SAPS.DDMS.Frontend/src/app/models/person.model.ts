export interface Person {
    personID: number;
    firstName: string;
    lastName: string;
    fullName: string;
    idNumber?: string;
    maskedIdNumber?: string;
    dateOfBirth?: Date;
    phoneNumber?: string;
    emailAddress?: string;
    physicalAddress?: string;
    personRole?: string;
    createdAt: Date;
    isArchived?: boolean;
    archivedAt?: Date;
    archiveReason?: string;
    linkedDockets?: PersonDocketLink[];
}

export interface PersonDocketLink {
    docketID: number;
    docketNumber: string;
    status: string;
    relationshipRole: string;
    linkedAt: Date;
}

export interface CreatePersonDto {
    firstName: string;
    lastName: string;
    idNumber: string;
    dateOfBirth: Date;
    phoneNumber: string;
    emailAddress: string;
    physicalAddress: string;
    personRoleID: number;
}

export interface UpdatePersonDto {
    firstName: string;
    lastName: string;
    phoneNumber: string;
    emailAddress: string;
    physicalAddress: string;
}

export interface LinkPersonToDocketDto {
    personID: number;
    docketID: number;
    relationshipRole: string;
}
