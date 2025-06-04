using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Domain.Entities
{
    public class PagePermission
    {
        public Guid PageId { get; set; }
        public Page Page { get; set; } = null!;
        public Guid RoleId { get; set; }
        public Role Role { get; set; } = null!;
        public bool CanView { get; set; }
        public bool CanEdit { get; set; }
    }
}
