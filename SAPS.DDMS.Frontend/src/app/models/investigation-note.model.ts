export interface InvestigationNote {
    noteID: number;
    description: string;
    entryDate: Date;
    status?: string;
    authorUserID?: number;
    authorName: string;
    attachments: NoteAttachment[] | null | undefined;
}

export interface NoteAttachment {
    attachmentID: number;
    fileName: string;
    fileSize: number;
    downloadUrl: string;
}

export interface CreateInvestigationNoteDto {
    investigationID: number;
    description: string;
    attachment?: File;
}

export interface UpdateInvestigationNoteDto {
    description: string;
    reason: string;
}
