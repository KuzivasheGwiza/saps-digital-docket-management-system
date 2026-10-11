using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.DDMS.API.Data;
using SAPS.DDMS.API.Models;
using SAPS.DDMS.API.Services;

namespace SAPS.DDMS.API.Controllers
{
    [ApiController]
    [Route("api/feedback")]
    [Authorize]
    public class FeedbackController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _audit;

        public FeedbackController(ApplicationDbContext db, IAuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        public class CreateFeedbackDto
        {
            [Required, MaxLength(150)]
            public string Subject { get; set; } = string.Empty;

            [Required, MaxLength(2000)]
            public string Message { get; set; } = string.Empty;
        }

        public class RespondFeedbackDto
        {
            [Required, MinLength(3), MaxLength(2000)]
            public string Response { get; set; } = string.Empty;
        }

        private sealed class FeedbackView
        {
            public int FeedbackID { get; set; }
            public int FK_UserID { get; set; }
            public string UserName { get; set; } = string.Empty;
            public string Subject { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string? Response { get; set; }
            public string Status { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime? RespondedAt { get; set; }
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMyFeedback()
        {
            var userId = GetCurrentUserId();
            var feedback = await BuildQuery()
                .Where(item => item.FK_UserID == userId)
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync();

            await _audit.LogActionAsync("REVIEW_OWN_FEEDBACK", "Feedback", null, null,
                new { Count = feedback.Count }, userId);
            return Ok(feedback);
        }

        [HttpGet]
        [Authorize(Roles = "System Administrator")]
        public async Task<IActionResult> GetAllFeedback()
        {
            var feedback = await BuildQuery()
                .OrderBy(item => item.Status == "Submitted" ? 0 : 1)
                .ThenByDescending(item => item.CreatedAt)
                .Take(500)
                .ToListAsync();
            return Ok(feedback);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitFeedback([FromBody] CreateFeedbackDto dto)
        {
            var subject = dto.Subject?.Trim() ?? string.Empty;
            var message = dto.Message?.Trim() ?? string.Empty;
            if (subject.Length < 3 || message.Length < 10)
                return BadRequest(new { success = false, message = "A subject and message of at least 10 characters are required." });

            var userId = GetCurrentUserId();
            var feedback = new Feedback
            {
                FK_UserID = userId,
                Subject = subject,
                Message = message,
                Status = "Submitted",
                CreatedAt = DateTime.UtcNow
            };
            await _db.Feedback.AddAsync(feedback);
            await _db.SaveChangesAsync();
            await _audit.LogActionAsync("SUBMIT_FEEDBACK", "Feedback", feedback.FeedbackID,
                null, new { feedback.Subject, feedback.Status }, userId);

            return CreatedAtAction(nameof(GetMyFeedback), new { id = feedback.FeedbackID }, feedback);
        }

        [HttpPatch("{id:int}/respond")]
        [Authorize(Roles = "System Administrator")]
        public async Task<IActionResult> Respond(int id, [FromBody] RespondFeedbackDto dto)
        {
            var response = dto.Response?.Trim() ?? string.Empty;
            if (response.Length < 3)
                return BadRequest(new { success = false, message = "A response is required." });

            var feedback = await _db.Feedback.FindAsync(id);
            if (feedback == null) return NotFound(new { success = false, message = "Feedback not found." });

            var responderId = GetCurrentUserId();
            var old = new { feedback.Status, feedback.Response };
            feedback.Response = response;
            feedback.Status = "Responded";
            feedback.RespondedAt = DateTime.UtcNow;
            feedback.RespondedByUserID = responderId;
            await _db.Notifications.AddAsync(new Notification
            {
                FK_UserID = feedback.FK_UserID,
                Title = "Feedback response available",
                Message = $"A response is available for: {feedback.Subject}",
                PriorityLevel = "NORMAL"
            });
            await _db.SaveChangesAsync();
            await _audit.LogActionAsync("RESPOND_FEEDBACK", "Feedback", id, old,
                new { feedback.Status, feedback.Response }, responderId);

            return Ok(new { success = true, message = "Feedback response saved." });
        }

        private IQueryable<FeedbackView> BuildQuery()
        {
            return _db.Feedback
                .Include(item => item.User)
                .Select(item => new FeedbackView
                {
                    FeedbackID = item.FeedbackID,
                    FK_UserID = item.FK_UserID,
                    UserName = item.User == null ? string.Empty : (item.User.FirstName + " " + item.User.LastName).Trim(),
                    Subject = item.Subject,
                    Message = item.Message,
                    Response = item.Response,
                    Status = item.Status,
                    CreatedAt = item.CreatedAt,
                    RespondedAt = item.RespondedAt
                });
        }

        private int GetCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("userId")
                ?? User.FindFirstValue("sub");
            if (!int.TryParse(value, out var userId))
                throw new UnauthorizedAccessException("User id claim not found.");
            return userId;
        }
    }
}
