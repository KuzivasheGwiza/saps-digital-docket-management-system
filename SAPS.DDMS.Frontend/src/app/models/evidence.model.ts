export interface Evidence {
    evidenceID: number;
    evidenceReferenceNumber: string;
    description: string;
    evidenceType: string;
    quantity: number;
    dateCollected: Date;
    collectionMethod: string;
    stateOfItem: string;
    evidenceLocation: string;
    evidenceLocationID: number;
    signatureMethod?: string;
    payloadHash?: string;
    imageUrl?: string;
    fileContentType?: string;
}

export interface CreateEvidenceDto {
    description: string;
    evidenceTypeID: number;
    quantity: number;
    dateCollected: Date;
    collectionMethod: string;
    stateOfItem: string;
    evidenceLocationID: number;
    docketID: number;
    signatureMethod?: string;
    evidenceFile?: File;
}

export interface UpdateEvidenceDto {
    description: string;
    quantity: number;
    collectionMethod: string;
    stateOfItem: string;
    evidenceLocationID: number;
    signatureMethod?: string;
    payloadHash?: string;
    reasonForUpdate: string;
}
