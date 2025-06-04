namespace Backend.CMS.API.Telemetry
{
    public interface ITelemetryInitializer
    {
        void Initialize(ITelemetry telemetry);
    }

    public interface ITelemetry
    {
        TelemetryContext Context { get; }
    }

    public class TelemetryContext
    {
        public Dictionary<string, string> Properties { get; set; } = new();
    }
}