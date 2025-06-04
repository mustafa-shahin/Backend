using System;
using System.Collections.Generic;
using Backend.CMS.Domain.Common;

namespace Backend.CMS.Domain.Entities
{
    public class PageComponent : BaseEntity
    {
        public Guid PageId { get; set; }
        public Page Page { get; set; } = null!;
        public string ComponentType { get; set; } = string.Empty;
        public string ComponentName { get; set; } = string.Empty;
        public int Order { get; set; }
        public string? ContainerName { get; set; }
        public Dictionary<string, object> Settings { get; set; } = [];
        public Dictionary<string, object> Content { get; set; } = [];
        public bool IsActive { get; set; } = true;
    }
}
