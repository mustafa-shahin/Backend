namespace Backend.CMS.Application.DTOs
{
    public class OpeningHourDto
    {
        public Guid Id { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public TimeOnly OpenTime { get; set; }
        public TimeOnly CloseTime { get; set; }
        public bool IsClosed { get; set; }
        public bool IsOpen24Hours { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateOpeningHourDto
    {
        public DayOfWeek DayOfWeek { get; set; }
        public TimeOnly OpenTime { get; set; }
        public TimeOnly CloseTime { get; set; }
        public bool IsClosed { get; set; }
        public bool IsOpen24Hours { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateOpeningHourDto
    {
        public Guid? Id { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public TimeOnly OpenTime { get; set; }
        public TimeOnly CloseTime { get; set; }
        public bool IsClosed { get; set; }
        public bool IsOpen24Hours { get; set; }
        public string? Notes { get; set; }
    }
}