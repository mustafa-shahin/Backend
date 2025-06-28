using System.Text.Json.Serialization;

namespace Backend.CMS.Application.Components
{
    public class ButtonComponent : BaseComponent
    {
        // Frontend Properties
        [JsonPropertyName("buttonText")]
        public string ButtonText { get; set; } = "Button";

        [JsonPropertyName("buttonLinkUrl")]
        public string? ButtonLinkUrl { get; set; }

        [JsonPropertyName("buttonLinkUrlTarget")]
        public TargetType ButtonLinkUrlTarget { get; set; } = TargetType.Self;

        [JsonPropertyName("buttonSize")]
        public ButtonSize ButtonSize { get; set; } = ButtonSize.Medium;

        [JsonPropertyName("buttonType")]
        public ButtonType ButtonType { get; set; } = ButtonType.Solid;

        // Button Colors
        [JsonPropertyName("buttonBackgroundColor")]
        public string? ButtonBackgroundColor { get; set; }

        [JsonPropertyName("buttonTextColor")]
        public string? ButtonTextColor { get; set; }

        [JsonPropertyName("buttonBorderColor")]
        public string? ButtonBorderColor { get; set; }

        // Hover States
        [JsonPropertyName("buttonHoverBackgroundColor")]
        public string? ButtonHoverBackgroundColor { get; set; }

        [JsonPropertyName("buttonHoverTextColor")]
        public string? ButtonHoverTextColor { get; set; }

        // Active States
        [JsonPropertyName("buttonActiveBackgroundColor")]
        public string? ButtonActiveBackgroundColor { get; set; }

        [JsonPropertyName("buttonActiveTextColor")]
        public string? ButtonActiveTextColor { get; set; }

        // Disabled States
        [JsonPropertyName("buttonDisabledBackgroundColor")]
        public string? ButtonDisabledBackgroundColor { get; set; }

        [JsonPropertyName("buttonDisabledTextColor")]
        public string? ButtonDisabledTextColor { get; set; }

        // Focus States
        [JsonPropertyName("buttonFocusBackgroundColor")]
        public string? ButtonFocusBackgroundColor { get; set; }

        [JsonPropertyName("buttonFocusTextColor")]
        public string? ButtonFocusTextColor { get; set; }

        // Border Properties
        [JsonPropertyName("borderWidth")]
        public BorderWidth BorderWidth { get; set; } = BorderWidth.None;

        [JsonPropertyName("borderStyle")]
        public BorderStyle BorderStyle { get; set; } = BorderStyle.None;

        [JsonPropertyName("borderRadius")]
        public BorderRadius BorderRadius { get; set; } = BorderRadius.Regular;

        [JsonPropertyName("borderColor")]
        public string? BorderColor { get; set; }

        // Icon Properties
        [JsonPropertyName("icon")]
        public string? Icon { get; set; }

        [JsonPropertyName("iconPosition")]
        public IconPosition IconPosition { get; set; } = IconPosition.Left;

        [JsonPropertyName("iconSize")]
        public int? IconSize { get; set; }

        // Behavior Properties
        [JsonPropertyName("isDisabled")]
        public bool IsDisabled { get; set; } = false;

        [JsonPropertyName("isLoading")]
        public bool IsLoading { get; set; } = false;

        [JsonPropertyName("loadingText")]
        public string? LoadingText { get; set; }

        [JsonPropertyName("tooltip")]
        public string? Tooltip { get; set; }

        [JsonPropertyName("ariaLabel")]
        public string? AriaLabel { get; set; }

        [JsonPropertyName("confirmationMessage")]
        public string? ConfirmationMessage { get; set; }

        [JsonPropertyName("trackingEvent")]
        public string? TrackingEvent { get; set; }
    }


}