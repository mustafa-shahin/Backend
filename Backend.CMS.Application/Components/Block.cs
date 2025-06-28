using System.Text.Json.Serialization;

namespace Backend.CMS.Application.Components
{
    public class Block
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("columns")]
        public int? Columns { get; set; }

        [JsonPropertyName("priority")]
        public int? Priority { get; set; }

        [JsonPropertyName("components")]
        public List<BaseComponent> Components { get; set; } = [];

        [JsonPropertyName("backgroundColor")]
        public string? BackgroundColor { get; set; }

        [JsonPropertyName("borderWidth")]
        public BorderWidth BorderWidth { get; set; } = BorderWidth.None;

        [JsonPropertyName("borderStyle")]
        public BorderStyle BorderStyle { get; set; } = BorderStyle.None;

        [JsonPropertyName("borderRadius")]
        public BorderRadius BorderRadius { get; set; } = BorderRadius.None;

        [JsonPropertyName("borderColor")]
        public string? BorderColor { get; set; }

        [JsonPropertyName("verticalAlignment")]
        public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;

        [JsonPropertyName("padding")]
        public ResponsiveAttribute<Padding>? Padding { get; set; }

        [JsonPropertyName("margin")]
        public ResponsiveAttribute<Padding>? Margin { get; set; }

        [JsonPropertyName("customCss")]
        public string? CustomCss { get; set; }

        [JsonPropertyName("customAttributes")]
        public Dictionary<string, object> CustomAttributes { get; set; } = new();

        [JsonPropertyName("isVisible")]
        public bool IsVisible { get; set; } = true;

        [JsonPropertyName("deviceVisibility")]
        public DeviceType DeviceVisibility { get; set; } = DeviceType.Both;
    }
}