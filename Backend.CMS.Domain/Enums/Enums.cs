namespace Backend.CMS.Domain.Enums
{
    public enum PageStatus
    {
        Draft = 0,
        Published = 1,
        Archived = 2,
        Scheduled = 3
    }

    public enum ComponentType
    {
        Text = 0,
        Image = 1,
        Button = 2,
        Container = 3,
        Grid = 4,
        Card = 5,
        List = 6,
        Form = 7,
        Video = 8,
        Map = 9,
        Gallery = 10,
        Slider = 11,
        Navigation = 12,
        Footer = 13,
        Header = 14,
        Sidebar = 15
    }
    public enum DeploymentStatus
    {
        Pending = 0,
        InProgress = 1,
        Completed = 2,
        Failed = 3,
        RolledBack = 4,
        Cancelled = 5
    }

    public enum SyncStatus
    {
        Pending = 0,
        InProgress = 1,
        Completed = 2,
        Failed = 3,
        ConflictDetected = 4,
        ManualReviewRequired = 5
    }

    public enum ConflictResolutionStrategy
    {
        UseLocal = 0,
        UseMaster = 1,
        Merge = 2,
        Skip = 3,
        ManualReview = 4
    }

    public enum JobStatus
    {
        Scheduled = 0,
        InProgress = 1,
        Completed = 2,
        Failed = 3,
        Cancelled = 4,
        PartiallyCompleted = 5
    }

    public enum JobType
    {
        Deployment = 0,
        TemplateSync = 1,
        Rollback = 2,
        Maintenance = 3
    }

    public enum JobPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    public enum ProposalStatus
    {
        Pending = 0,
        UnderReview = 1,
        Approved = 2,
        Rejected = 3,
        Scheduled = 4,
        Executed = 5,
        Cancelled = 6
    }
    public enum UserRole
    {
        Customer = 0,
        Admin = 1,
        Dev = 2
    }
    public enum FileType
    {
        Document = 0,
        Image = 1,
        Video = 2,
        Audio = 3,
        Archive = 4,
        Other = 5
    }

    public enum FolderType
    {
        General = 0,
        Images = 1,
        Documents = 2,
        Videos = 3,
        Audio = 4,
        UserAvatars = 5,
        CompanyAssets = 6,
        Temporary = 7
    }

    public enum FileAccessType
    {
        View = 0,
        Download = 1,
        Preview = 2,
        Edit = 3
    }
    public enum ProductStatus
    {
        Draft = 0,
        Active = 1,
        Archived = 2
    }

    public enum ProductType
    {
        Physical = 0,
        Digital = 1,
        Service = 2,
        GiftCard = 3
    }
}
