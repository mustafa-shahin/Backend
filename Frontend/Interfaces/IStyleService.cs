using Backend.CMS.Domain.Enums;
using Frontend.Enums;

namespace Frontend.Interfaces
{
    public interface IStyleService
    {
        // Theme-related methods
        string GetThemeIcon(bool isDarkMode);
        string GetThemeToggleLabel(bool isDarkMode);

        // Button-related methods
        string GetButtonIcon(bool isLoading, string defaultIcon = "fas fa-save");
        string GetButtonClass(string variant = "primary", string size = "medium");

        // Status and badge methods
        string GetStatusBadgeClass(PageStatus status);
        string GetStatusText(PageStatus status);
        string GetUserRoleBadgeClass(UserRole role);
        string GetUserRoleText(UserRole role);

        // Notification methods
        string GetNotificationClass(NotificationType type);
        string GetNotificationIcon(NotificationType type);
        string GetNotificationIconContainerClass(NotificationType type);
        string GetNotificationTitleClass(NotificationType type);
        string GetNotificationMessageClass(NotificationType type);
        string GetNotificationCloseButtonClass(NotificationType type);
        string GetNotificationProgressBarClass(NotificationType type);

        // Form methods
        string GetFormInputClass(bool hasError = false);
        string GetFormLabelClass();


        // Icon and color methods
        string GetIconColorClass(string iconType);
        string GetIconSizeClass(string size);
        string GetColorVariant(string color, string variant = "500");

        // Layout methods
        string GetSidebarClass(bool isCollapsed, bool isMobileOpen);
        string GetSidebarItemClass(bool isActive, bool isDisabled);
        string GetSidebarIconClass(bool isActive, bool isDisabled, bool isCollapsed);

        string GetModalDialogSizeClass(string size);
        string GetModalBodyClass(string size);

        string GetViewModeToggleClass(bool isActive);
        string GetLoadingSpinnerClass(string size = "medium");
        string GetEmptyStateClass();
        string GetEmptyStateIconClass();
        string GetFormGridClass(int columns = 1);
        public string GetTableActionButtonClass(string variant = "primary");
    }
}