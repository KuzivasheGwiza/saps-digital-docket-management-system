namespace SAPS.DDMS.API.Models.DTOs
{
    public class InvestigationNoteDtos
    {
        public class CreateInvestigationNoteDto
        {
            public int InvestigationID { get; set; } 
            public string Description { get; set; } = string.Empty;
            // A note can be recorded without media; an attachment is optional.
            public IFormFile? Attachment { get; set; }
        }

        public class UpdateInvestigationNoteDto
        {
            public string Description { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
        }

        public class InvestigationNoteResponseDto
        {
            public int NoteID { get; set; }
            public string Description { get; set; } = string.Empty;
            public DateTime EntryDate { get; set; }
            public string Status { get; set; } = string.Empty;
            public string AuthorName { get; set; } = string.Empty;
            public int AuthorUserID { get; set; }
            public List<NoteAttachmentDto> Attachments { get; set; }
        }

        public class NoteAttachmentDto
        {
            public int AttachmentID { get; set; }
            public string FileName { get; set; }
            public long FileSize { get; set; }
            public string DownloadUrl { get; set; }
        }
    }
}
