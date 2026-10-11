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
    [Authorize(Policy = PermissionCatalog.RolesManage)]
    public class RolesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _audit;

        public RolesController(ApplicationDbContext db, IAuditService audit)
        {
            _db = db;
            _audit = audit;
        }

        [HttpGet]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _db.Roles
                .AsNoTracking()
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                .OrderBy(r => r.Name)
                .ToListAsync();

            var result = roles.Select(r =>
            {
                var effectiveNames = r.Permissions is null
                    ? PermissionCatalog.DefaultsForRole(r.Name)
                    : r.RolePermissions
                        .Where(rp => string.Equals(rp.AccessLevel, "Allow", StringComparison.OrdinalIgnoreCase))
                        .Select(rp => rp.Permission?.Name)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Cast<string>()
                        .ToArray();

                return new
                {
                    Id = r.RoleID,
                    r.Name,
                    r.Description,
                    PermissionText = r.Permissions,
                    Permissions = effectiveNames.Select(name => new
                    {
                        PermissionID = r.RolePermissions
                            .FirstOrDefault(rp => rp.Permission?.Name == name)?.PermissionID ?? 0,
                        Name = name,
                        AccessLevel = "Allow"
                    })
                };
            }).ToList();

            return Ok(result);
        }

        [HttpGet("catalog")]
        public IActionResult GetPermissionCatalog()
        {
            return Ok(PermissionCatalog.All);
        }

        public class RoleUpsertDto
        {
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public List<string> Permissions { get; set; } = new();
        }

        [HttpPost]
        public async Task<IActionResult> CreateRole([FromBody] RoleUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { success = false, message = "Role name is required." });

            dto.Permissions ??= new List<string>();

            if (await _db.Roles.AnyAsync(r => r.Name == dto.Name.Trim()))
                return BadRequest(new { success = false, message = "Role name already exists." });

            EnsureBaselinePermission(dto.Permissions);
            var invalidPermissions = GetInvalidPermissions(dto.Permissions);
            if (invalidPermissions.Count > 0)
                return BadRequest(new { success = false, message = $"Unknown permissions: {string.Join(", ", invalidPermissions)}" });

            var role = new Role
            {
                Name = dto.Name.Trim(),
                Description = dto.Description?.Trim(),
                Permissions = string.Join(",", dto.Permissions.Select(p => p.Trim()).Where(p => p.Length > 0).Distinct())
            };

            await _db.Roles.AddAsync(role);
            await _db.SaveChangesAsync();
            await SyncRolePermissions(role.RoleID, dto.Permissions);
            await _audit.LogActionAsync("CREATE_ROLE", "Role", role.RoleID, null, role, GetCurrentUserId());

            return Ok(new { success = true, message = "Role created.", roleId = role.RoleID });
        }

        [HttpPut("{id:int}/permissions")]
        public async Task<IActionResult> UpdateRolePermissions(int id, [FromBody] RoleUpsertDto dto)
        {
            var role = await _db.Roles.FindAsync(id);
            if (role == null)
                return NotFound(new { success = false, message = "Role not found." });

            dto.Permissions ??= new List<string>();

            EnsureBaselinePermission(dto.Permissions);
            var invalidPermissions = GetInvalidPermissions(dto.Permissions);
            if (invalidPermissions.Count > 0)
                return BadRequest(new { success = false, message = $"Unknown permissions: {string.Join(", ", invalidPermissions)}" });

            var old = new { role.Name, role.Description, role.Permissions };
            role.Name = string.IsNullOrWhiteSpace(dto.Name) ? role.Name : dto.Name.Trim();
            role.Description = dto.Description?.Trim();
            role.Permissions = string.Join(",", dto.Permissions.Select(p => p.Trim()).Where(p => p.Length > 0).Distinct());
            await _db.SaveChangesAsync();

            await SyncRolePermissions(id, dto.Permissions);
            await _audit.LogActionAsync("UPDATE_ROLE_PERMISSIONS", "Role", id, old, new { role.Name, role.Description, role.Permissions }, GetCurrentUserId());

            return Ok(new { success = true, message = "Role permissions updated." });
        }

        private async Task SyncRolePermissions(int roleId, IEnumerable<string> permissionNames)
        {
            var normalizedNames = permissionNames
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var current = await _db.RolePermissions.Where(rp => rp.RoleID == roleId).ToListAsync();
            _db.RolePermissions.RemoveRange(current);

            foreach (var name in normalizedNames)
            {
                var permission = await _db.Permissions.FirstOrDefaultAsync(p => p.Name == name);
                if (permission == null)
                {
                    permission = new Permissions { Name = name };
                    await _db.Permissions.AddAsync(permission);
                    await _db.SaveChangesAsync();
                }

                await _db.RolePermissions.AddAsync(new RolePermissions
                {
                    RoleID = roleId,
                    PermissionID = permission.PermissionID,
                    AccessLevel = "Allow"
                });
            }

            await _db.SaveChangesAsync();
        }

        private static List<string> GetInvalidPermissions(IEnumerable<string> permissions)
        {
            var allowed = PermissionCatalog.All.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return permissions
                .Select(item => item.Trim())
                .Where(item => item.Length > 0 && !allowed.Contains(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void EnsureBaselinePermission(ICollection<string> permissions)
        {
            if (!permissions.Contains(PermissionCatalog.DashboardView, StringComparer.OrdinalIgnoreCase))
                permissions.Add(PermissionCatalog.DashboardView);
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
