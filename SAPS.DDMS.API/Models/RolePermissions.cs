using System.ComponentModel.DataAnnotations.Schema;
using SAPS.DDMS.API.Models;
namespace SAPS.DDMS.API.Models
{
    public class RolePermissions
    {
        [ForeignKey("Role")]
        public int RoleID { get; set; }
        public virtual Role? Role { get; set; }
        [ForeignKey("Permission")]
        public int PermissionID { get; set; }
        public virtual Permissions? Permission { get; set; }

        public string AccessLevel { get; set; } = string.Empty;
    }
}
