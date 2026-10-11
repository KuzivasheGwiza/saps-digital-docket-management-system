using System.ComponentModel.DataAnnotations;

namespace SAPS.DDMS.API.Models.DTOs
{
    public class EvidenceDtos
    {
        public class CreateEvidenceDto
        {
            [Required, StringLength(2000, MinimumLength = 3)]
            public string? Description { get; set; }

            [Range(1, int.MaxValue, ErrorMessage = "Select a valid evidence type.")]
            public int EvidenceTypeID { get; set; }

            [Range(1, 1_000_000, ErrorMessage = "Quantity must be a whole number greater than zero.")]
            public int Quantity { get; set; }

            public DateTime DateCollected { get; set; }

            [Required, StringLength(256, MinimumLength = 2)]
            public string? CollectionMethod { get; set; }

            [Required, StringLength(256, MinimumLength = 2)]
            public string? StateOfItem { get; set; }

            [Range(1, int.MaxValue, ErrorMessage = "Select a valid evidence location.")]
            public int EvidenceLocationID { get; set; }

            [Range(1, int.MaxValue, ErrorMessage = "Select a valid docket.")]
            public int DocketID { get; set; }

            [Required, StringLength(50, MinimumLength = 2)]
            public string? SignatureMethod { get; set; }

            [StringLength(256)]
            public string? PayloadHash { get; set; }

            public IFormFile? EvidenceFile { get; set; }
        }

        public class UpdateEvidenceDto
        {
            [Required, StringLength(2000, MinimumLength = 3)]
            public string? Description { get; set; }

            [Range(1, 1_000_000, ErrorMessage = "Quantity must be a whole number greater than zero.")]
            public int Quantity { get; set; }

            [Required, StringLength(256, MinimumLength = 2)]
            public string? CollectionMethod { get; set; }

            [Required, StringLength(256, MinimumLength = 2)]
            public string? StateOfItem { get; set; }

            [Range(1, int.MaxValue, ErrorMessage = "Select a valid evidence location.")]
            public int EvidenceLocationID { get; set; }

            [Required, StringLength(50, MinimumLength = 2)]
            public string? SignatureMethod { get; set; }

            [StringLength(256)]
            public string? PayloadHash { get; set; }

            [Required, StringLength(500, MinimumLength = 10)]
            public string? ReasonForUpdate { get; set; }
        }

        public class EvidenceResponseDto
        {
            public int EvidenceID { get; set; }
            public string? EvidenceReferenceNumber { get; set; }
            public string? Description { get; set; }
            public string? EvidenceType { get; set; }
            public int Quantity { get; set; }
            public DateTime DateCollected { get; set; }
            public string? CollectionMethod { get; set; }
            public string? StateOfItem { get; set; }
            public string? EvidenceLocation { get; set; }
            public int EvidenceLocationID { get; set; }
            public string? SignatureMethod { get; set; }
            public string? PayloadHash { get; set; }
            public string? FileHash { get; set; }
            public string? ImageUrl { get; set; }
        }
    }
}
