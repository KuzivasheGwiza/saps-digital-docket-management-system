using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Services;
using SAPS.DDMS.API.Security;
using System.Security.Claims;

namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FaqController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _auditService;

        public FaqController(ApplicationDbContext db, IAuditService auditService)
        {
            _db = db;
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<IActionResult> GetFaqs([FromQuery] string? search)
        {
            var query = _db.Faqs.Where(f => !f.IsDeleted);
            if (!User.IsInRole("System Administrator"))
                query = query.Where(f => f.IsPublished);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(f => f.Question.Contains(term) || f.Answer.Contains(term));
            }

            var faqs = await query.OrderBy(f => f.Question).ToListAsync();
            return Ok(faqs);
        }

        public class FaqDto
        {
            public string Question { get; set; } = string.Empty;
            public string Answer { get; set; } = string.Empty;
            public bool IsPublished { get; set; }
        }

        [HttpPost]
        [Authorize(Policy = PermissionCatalog.FaqManage)]
        public async Task<IActionResult> CreateFaq([FromBody] FaqDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Question) || string.IsNullOrWhiteSpace(dto.Answer))
                return BadRequest(new { success = false, message = "Question and answer are required." });

            var normalizedQuestion = dto.Question.Trim();
            var duplicateExists = await _db.Faqs.AnyAsync(f =>
                !f.IsDeleted && f.Question.ToLower() == normalizedQuestion.ToLower());
            if (duplicateExists)
                return Conflict(new { success = false, message = "An active FAQ with this question already exists." });

            var userId = GetCurrentUserId();
            var faq = new Faq
            {
                Question = normalizedQuestion,
                Answer = dto.Answer.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedByUserID = userId,
                IsDeleted = false,
                IsPublished = dto.IsPublished,
                PublishedAt = dto.IsPublished ? DateTime.UtcNow : null,
                PublishedByUserID = dto.IsPublished ? userId : null
            };

            await _db.Faqs.AddAsync(faq);
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("CREATE_FAQ", "Faq", faq.FaqID, null, faq, userId);

            return CreatedAtAction(nameof(GetFaqs), new { id = faq.FaqID }, faq);
        }

        [HttpPut("{id}")]
        [Authorize(Policy = PermissionCatalog.FaqManage)]
        public async Task<IActionResult> UpdateFaq(int id, [FromBody] FaqDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Question) || string.IsNullOrWhiteSpace(dto.Answer))
                return BadRequest(new { success = false, message = "Question and answer are required." });

            var faq = await _db.Faqs.FirstOrDefaultAsync(f => f.FaqID == id && !f.IsDeleted);
            if (faq == null) return NotFound(new { success = false, message = "FAQ not found." });

            var normalizedQuestion = dto.Question.Trim();
            var duplicateExists = await _db.Faqs.AnyAsync(f =>
                f.FaqID != id && !f.IsDeleted && f.Question.ToLower() == normalizedQuestion.ToLower());
            if (duplicateExists)
                return Conflict(new { success = false, message = "An active FAQ with this question already exists." });

            var userId = GetCurrentUserId();
            var old = new { faq.Question, faq.Answer, faq.IsPublished };
            faq.Question = normalizedQuestion;
            faq.Answer = dto.Answer.Trim();
            if (dto.IsPublished && !faq.IsPublished)
            {
                faq.PublishedAt = DateTime.UtcNow;
                faq.PublishedByUserID = userId;
            }
            else if (!dto.IsPublished)
            {
                faq.PublishedAt = null;
                faq.PublishedByUserID = null;
            }
            faq.IsPublished = dto.IsPublished;
            faq.UpdatedAt = DateTime.UtcNow;
            faq.UpdatedByUserID = userId;
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("UPDATE_FAQ", "Faq", id, old, new { faq.Question, faq.Answer, faq.IsPublished }, userId);

            return Ok(new { success = true, message = "FAQ updated." });
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = PermissionCatalog.FaqManage)]
        public async Task<IActionResult> DeleteFaq(int id)
        {
            var faq = await _db.Faqs.FirstOrDefaultAsync(f => f.FaqID == id && !f.IsDeleted);
            if (faq == null) return NotFound(new { success = false, message = "FAQ not found." });

            var userId = GetCurrentUserId();
            faq.IsDeleted = true;
            faq.DeletedAt = DateTime.UtcNow;
            faq.DeletedByUserID = userId;
            await _db.SaveChangesAsync();
            await _auditService.LogActionAsync("DELETE_FAQ", "Faq", id, faq, new { faq.IsDeleted }, userId);

            return Ok(new { success = true, message = "FAQ deleted." });
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
            return userIdClaim != null ? int.Parse(userIdClaim) : 0;
        }
    }
}
