using System.Text.Json.Serialization;

namespace Backend.CMS.Application.Components
{
    public abstract class BaseComponent
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("type")]
        public string Type => GetType().Name;

        [JsonPropertyName("showOn")]
        public DeviceType ShowOn { get; set; } = DeviceType.Both;

        [JsonPropertyName("padding")]
        public ResponsiveAttribute<Padding>? Padding { get; set; }

        [JsonPropertyName("horizontalAlignment")]
        public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;

        [JsonPropertyName("verticalAlignment")]
        public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;

        [JsonPropertyName("activeFrom")]
        public string? ActiveFrom { get; set; }

        [JsonPropertyName("activeUntil")]
        public string? ActiveUntil { get; set; }

        [JsonPropertyName("font")]
        public Font? Font { get; set; }

        [JsonPropertyName("customCss")]
        public string? CustomCss { get; set; }

        [JsonPropertyName("customAttributes")]
        public Dictionary<string, object> CustomAttributes { get; set; } = new();
    }

    public class ResponsiveAttribute<T>
    {
        [JsonPropertyName("desktop")]
        public T? Desktop { get; set; }

        [JsonPropertyName("tablet")]
        public T? Tablet { get; set; }

        [JsonPropertyName("mobile")]
        public T? Mobile { get; set; }
    }

    public class Padding
    {
        [JsonPropertyName("top")]
        public int Top { get; set; }

        [JsonPropertyName("right")]
        public int Right { get; set; }

        [JsonPropertyName("bottom")]
        public int Bottom { get; set; }

        [JsonPropertyName("left")]
        public int Left { get; set; }
    }

    public class Font
    {
        [JsonPropertyName("family")]
        public string? Family { get; set; }

        [JsonPropertyName("size")]
        public int? Size { get; set; }

        [JsonPropertyName("weight")]
        public FontWeight? Weight { get; set; }

        [JsonPropertyName("style")]
        public FontStyle? Style { get; set; }

        [JsonPropertyName("color")]
        public string? Color { get; set; }
    }
}