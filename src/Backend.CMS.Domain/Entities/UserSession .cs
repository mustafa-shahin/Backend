using Backend.CMS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Domain.Entities
{
    public class UserSession : BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public string RefreshToken { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; }
    }
}
