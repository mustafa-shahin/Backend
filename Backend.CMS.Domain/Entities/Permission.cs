using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Backend.CMS.Domain.Common;
using Backend.CMS.Domain.Enums;
using System.Collections.Generic;

namespace Backend.CMS.Domain.Entities
{
    public class Permission : BaseEntity
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string DisplayName { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [Required]
        [StringLength(100)]
        public string Category { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public bool IsSystemPermission { get; set; } = false;

        public int SortOrder { get; set; }

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

        public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
    }

    public class RolePermission : BaseEntity
    {
        [Required]
        public UserRole Role { get; set; }

        public int PermissionId { get; set; }

        [ForeignKey("PermissionId")]
        public Permission Permission { get; set; } = null!;

        public bool IsGranted { get; set; } = true;

        [StringLength(1000)]
        public string? Reason { get; set; }

        public DateTime? ExpiresAt { get; set; }
    }

    public class UserPermission : BaseEntity
    {
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        public int PermissionId { get; set; }

        [ForeignKey("PermissionId")]
        public Permission Permission { get; set; } = null!;

        public bool IsGranted { get; set; } = true;

        [StringLength(1000)]
        public string? Reason { get; set; }

        public DateTime? ExpiresAt { get; set; }
    }
}