using Backend.CMS.Domain.Enums;
using Frontend.Enums;
using Frontend.Interfaces;

namespace Frontend.Services
{
    public class StyleService : IStyleService
    {
        #region Theme Methods
        public string GetThemeIcon(bool isDarkMode)
        {
            return isDarkMode
                ? "fas fa-sun text-yellow-500 group-hover:text-yellow-400"
                : "fas fa-moon text-gray-600 dark:text-gray-400 group-hover:text-blue-500";
        }

        public string GetThemeToggleLabel(bool isDarkMode)
        {
            return $"Switch to {(isDarkMode ? "light" : "dark")} mode";
        }
        #endregion

        #region Button Methods
        public string GetButtonIcon(bool isLoading, string defaultIcon = "fas fa-save")
        {
            return isLoading ? "fas fa-spinner fa-spin" : defaultIcon;
        }

        public string GetButtonClass(string variant = "primary", string size = "medium")
        {
            var baseClass = "inline-flex items-center justify-center font-medium rounded-lg transition-all duration-200 focus:outline-none focus:ring-2 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed";

            var sizeClass = size.ToLower() switch
            {
                "small" => "px-3 py-1.5 text-sm",
                "medium" => "px-4 py-2 text-sm",
                "large" => "px-6 py-3 text-base",
                _ => "px-4 py-2 text-sm"
            };

            var variantClass = variant.ToLower() switch
            {
                "primary" => "text-white bg-blue-600 hover:bg-blue-700 focus:ring-blue-500 shadow-sm hover:shadow-md transform hover:scale-[1.02]",
                "secondary" => "text-gray-700 bg-white border border-gray-300 hover:bg-gray-50 focus:ring-blue-500 dark:bg-gray-800 dark:text-gray-300 dark:border-gray-600 dark:hover:bg-gray-700",
                "danger" => "text-white bg-red-600 hover:bg-red-700 focus:ring-red-500 shadow-sm hover:shadow-md",
                "success" => "text-white bg-green-600 hover:bg-green-700 focus:ring-green-500 shadow-sm hover:shadow-md",
                "warning" => "text-white bg-yellow-600 hover:bg-yellow-700 focus:ring-yellow-500 shadow-sm hover:shadow-md",
                "ghost" => "text-gray-700 hover:bg-gray-100 focus:ring-blue-500 dark:text-gray-300 dark:hover:bg-gray-700",
                _ => "text-white bg-blue-600 hover:bg-blue-700 focus:ring-blue-500 shadow-sm hover:shadow-md"
            };

            return $"{baseClass} {sizeClass} {variantClass}";
        }
        #endregion

        #region Status and Badge Methods
        public string GetStatusBadgeClass(PageStatus status)
        {
            var baseClass = "inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium";

            return status switch
            {
                PageStatus.Published => $"{baseClass} bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300",
                PageStatus.Draft => $"{baseClass} bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-300",
                PageStatus.Scheduled => $"{baseClass} bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300",
                PageStatus.Archived => $"{baseClass} bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300",
                _ => $"{baseClass} bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300"
            };
        }

        public string GetStatusText(PageStatus status)
        {
            return status switch
            {
                PageStatus.Published => "Published",
                PageStatus.Draft => "Draft",
                PageStatus.Scheduled => "Scheduled",
                PageStatus.Archived => "Archived",
                _ => status.ToString()
            };
        }

        public string GetUserRoleBadgeClass(UserRole role)
        {
            var baseClass = "inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium";

            return role switch
            {
                UserRole.Admin => $"{baseClass} bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300",
                UserRole.Dev => $"{baseClass} bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-300",
                UserRole.Customer => $"{baseClass} bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300",
                _ => $"{baseClass} bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300"
            };
        }

        public string GetUserRoleText(UserRole role)
        {
            return role switch
            {
                UserRole.Admin => "Administrator",
                UserRole.Dev => "Developer",
                UserRole.Customer => "Customer",
                _ => role.ToString()
            };
        }
        #endregion

        #region Notification Methods
        public string GetNotificationClass(NotificationType type)
        {
            var baseClass = "rounded-xl p-0 shadow-lg border backdrop-blur-sm max-w-sm";

            return type switch
            {
                NotificationType.Success => $"{baseClass} bg-green-50/95 dark:bg-green-900/90 border-green-200 dark:border-green-800",
                NotificationType.Error => $"{baseClass} bg-red-50/95 dark:bg-red-900/90 border-red-200 dark:border-red-800",
                NotificationType.Warning => $"{baseClass} bg-yellow-50/95 dark:bg-yellow-900/90 border-yellow-200 dark:border-yellow-800",
                NotificationType.Info => $"{baseClass} bg-blue-50/95 dark:bg-blue-900/90 border-blue-200 dark:border-blue-800",
                _ => $"{baseClass} bg-gray-50/95 dark:bg-gray-900/90 border-gray-200 dark:border-gray-800"
            };
        }

        public string GetNotificationIconContainerClass(NotificationType type)
        {
            var baseClass = "w-8 h-8 rounded-full flex items-center justify-center";

            return type switch
            {
                NotificationType.Success => $"{baseClass} bg-green-100 dark:bg-green-800",
                NotificationType.Error => $"{baseClass} bg-red-100 dark:bg-red-800",
                NotificationType.Warning => $"{baseClass} bg-yellow-100 dark:bg-yellow-800",
                NotificationType.Info => $"{baseClass} bg-blue-100 dark:bg-blue-800",
                _ => $"{baseClass} bg-gray-100 dark:bg-gray-800"
            };
        }

        public string GetNotificationIcon(NotificationType type)
        {
            var baseClass = "w-4 h-4";

            return type switch
            {
                NotificationType.Success => $"{baseClass} text-green-600 dark:text-green-300 fas fa-check",
                NotificationType.Error => $"{baseClass} text-red-600 dark:text-red-300 fas fa-exclamation-circle",
                NotificationType.Warning => $"{baseClass} text-yellow-600 dark:text-yellow-300 fas fa-exclamation-triangle",
                NotificationType.Info => $"{baseClass} text-blue-600 dark:text-blue-300 fas fa-info-circle",
                _ => $"{baseClass} text-gray-600 dark:text-gray-300 fas fa-bell"
            };
        }

        public string GetNotificationTitleClass(NotificationType type)
        {
            return type switch
            {
                NotificationType.Success => "text-green-800 dark:text-green-200",
                NotificationType.Error => "text-red-800 dark:text-red-200",
                NotificationType.Warning => "text-yellow-800 dark:text-yellow-200",
                NotificationType.Info => "text-blue-800 dark:text-blue-200",
                _ => "text-gray-800 dark:text-gray-200"
            };
        }

        public string GetNotificationMessageClass(NotificationType type)
        {
            return type switch
            {
                NotificationType.Success => "text-green-700 dark:text-green-300",
                NotificationType.Error => "text-red-700 dark:text-red-300",
                NotificationType.Warning => "text-yellow-700 dark:text-yellow-300",
                NotificationType.Info => "text-blue-700 dark:text-blue-300",
                _ => "text-gray-700 dark:text-gray-300"
            };
        }

        public string GetNotificationCloseButtonClass(NotificationType type)
        {
            return type switch
            {
                NotificationType.Success => "text-green-600 dark:text-green-400 hover:bg-green-100 dark:hover:bg-green-800",
                NotificationType.Error => "text-red-600 dark:text-red-400 hover:bg-red-100 dark:hover:bg-red-800",
                NotificationType.Warning => "text-yellow-600 dark:text-yellow-400 hover:bg-yellow-100 dark:hover:bg-yellow-800",
                NotificationType.Info => "text-blue-600 dark:text-blue-400 hover:bg-blue-100 dark:hover:bg-blue-800",
                _ => "text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800"
            };
        }

        public string GetNotificationProgressBarClass(NotificationType type)
        {
            return type switch
            {
                NotificationType.Success => "bg-green-500 dark:bg-green-400",
                NotificationType.Error => "bg-red-500 dark:bg-red-400",
                NotificationType.Warning => "bg-yellow-500 dark:bg-yellow-400",
                NotificationType.Info => "bg-blue-500 dark:bg-blue-400",
                _ => "bg-gray-500 dark:bg-gray-400"
            };
        }
        #endregion

        #region Form Methods
        public string GetFormInputClass(bool hasError = false)
        {
            var baseClass = "block w-full px-3 py-2 border rounded-lg shadow-sm placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-offset-0 dark:bg-gray-700 dark:placeholder-gray-400 dark:text-white sm:text-sm transition-colors duration-200";

            if (hasError)
            {
                return $"{baseClass} border-red-300 focus:border-red-500 focus:ring-red-500 dark:border-red-600";
            }

            return $"{baseClass} border-gray-300 focus:border-blue-500 focus:ring-blue-500 dark:border-gray-600 dark:focus:border-blue-500 dark:focus:ring-blue-500";
        }

        public string GetFormLabelClass()
        {
            return "block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1";
        }

        public string GetValidationMessageClass()
        {
            return "text-red-600 dark:text-red-400 text-sm mt-1";
        }
        #endregion

        #region Table Methods
        public string GetTableContainerClass()
        {
            return "bg-white dark:bg-gray-800 shadow-sm rounded-lg overflow-hidden border border-gray-200 dark:border-gray-700";
        }

        public string GetTableHeaderClass()
        {
            return "px-6 py-3 text-left text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider";
        }

        public string GetTableCellClass()
        {
            return "px-6 py-4 whitespace-nowrap text-sm text-gray-900 dark:text-gray-100";
        }

        public string GetTableRowClass()
        {
            return "hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors duration-150";
        }
        #endregion

        #region Icon and Color Methods
        public string GetIconColorClass(string iconType)
        {
            return iconType.ToLower() switch
            {
                "warning" => "text-yellow-500",
                "danger" => "text-red-500",
                "error" => "text-red-500",
                "info" => "text-blue-500",
                "success" => "text-green-500",
                "primary" => "text-blue-500",
                "secondary" => "text-gray-500",
                _ => "text-gray-500"
            };
        }

        public string GetIconSizeClass(string size)
        {
            return size.ToLower() switch
            {
                "small" => "text-sm",
                "medium" => "text-base",
                "large" => "text-lg",
                "xl" => "text-xl",
                "2xl" => "text-2xl",
                "3xl" => "text-3xl",
                _ => "text-base"
            };
        }

        public string GetColorVariant(string color, string variant = "500")
        {
            return $"text-{color}-{variant} dark:text-{color}-{variant}";
        }
        #endregion

        #region Layout Methods
        public string GetSidebarClass(bool isCollapsed, bool isMobileOpen)
        {
            var baseClass = "left-0 top-0 h-full bg-white dark:bg-gray-800 border-r border-gray-200 dark:border-gray-700 flex flex-col transition-all duration-300 z-30";
            var widthClass = isCollapsed ? "w-16" : "w-64";
            var mobileClass = isMobileOpen ? "translate-x-0" : "-translate-x-full lg:translate-x-0";

            return $"{baseClass} {widthClass} {mobileClass}";
        }

        public string GetSidebarItemClass(bool isActive, bool isDisabled)
        {
            var baseClass = "relative flex items-center px-3 py-2.5 rounded-lg transition-all duration-200 group";

            if (isDisabled)
            {
                return $"{baseClass} text-gray-400 dark:text-gray-600 cursor-not-allowed opacity-60";
            }

            var stateClass = isActive
                ? "bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300 shadow-sm"
                : "text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 hover:text-gray-900 dark:hover:text-gray-100";

            return $"{baseClass} {stateClass}";
        }

        public string GetSidebarIconClass(bool isActive, bool isDisabled, bool isCollapsed)
        {
            var sizeClass = isCollapsed ? "text-lg" : "text-base";

            if (isDisabled)
            {
                return $"{sizeClass} text-gray-400 dark:text-gray-600";
            }

            if (isActive)
            {
                return $"{sizeClass} text-blue-600 dark:text-blue-300";
            }

            return $"{sizeClass} text-gray-500 dark:text-gray-400 group-hover:text-gray-700 dark:group-hover:text-gray-200";
        }
        #endregion

        #region Modal Methods
        public string GetModalBackdropClass()
        {
            return "fixed inset-0 bg-black/50 backdrop-blur-sm z-40";
        }

        public string GetModalDialogSizeClass(string size)
        {
            return size.ToLower() switch
            {
                "small" => "w-full max-w-md",
                "medium" => "w-full max-w-lg",
                "large" => "w-full max-w-2xl",
                "xlarge" => "w-full max-w-4xl",
                "full" => "w-full max-w-7xl mx-4",
                _ => "w-full max-w-lg"
            };
        }

        public string GetModalBodyClass(string size)
        {
            var baseClass = "p-6";
            var heightClass = size.ToLower() switch
            {
                "xlarge" => "max-h-[70vh] overflow-y-auto",
                "full" => "max-h-[75vh] overflow-y-auto",
                _ => "max-h-96 overflow-y-auto"
            };

            return $"{baseClass} {heightClass}";
        }
        #endregion
    }
}