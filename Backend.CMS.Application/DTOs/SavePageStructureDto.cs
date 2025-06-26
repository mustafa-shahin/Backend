namespace Backend.CMS.Application.DTOs
{
    public class SavePageStructureDto
    {
        public int PageId { get; set; }

        /// <summary>
        /// Page content containing all components and their configurations
        /// </summary>
        public Dictionary<string, object> Content { get; set; } = new();

        /// <summary>
        /// Layout configuration for the page
        /// </summary>
        public Dictionary<string, object> Layout { get; set; } = new();

        /// <summary>
        /// Page-level settings
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new();

        /// <summary>
        /// Custom CSS styles
        /// </summary>
        public Dictionary<string, object> Styles { get; set; } = new();

        /// <summary>
        /// Optional change description
        /// </summary>
        public string? ChangeDescription { get; set; }

        /// <summary>
        /// Whether to create a new version
        /// </summary>
        public bool CreateVersion { get; set; } = true;
    }

}