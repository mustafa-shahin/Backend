using Backend.CMS.Domain.Enums;
using Frontend.Enums;
using Frontend.Interfaces;

namespace Frontend.Services
{
    public class StyleService : IStyleService
    {
        private readonly Dictionary<string, string> _themeVariables = [];

        public StyleService()
        {
            InitializeThemeVariables();
        }

        private void InitializeThemeVariables()
        {
            _themeVariables["primary"] = "blue";
            _themeVariables["secondary"] = "gray";
            _themeVariables["success"] = "green";
            _themeVariables["warning"] = "yellow";
            _themeVariables["danger"] = "red";
            _themeVariables["info"] = "blue";
        }

        #region Theme Methods
        public string GetThemeIcon(bool isDarkMode)
        {
            return isDarkMode
                ? "fas fa-sun text-yellow-500 group-hover:text-yellow-400 transition-all duration-300"
                : "fas fa-moon text-gray-600 dark:text-gray-400 group-hover:text-blue-500 transition-all duration-300";
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
            var baseClass = "inline-flex items-center justify-center font-medium rounded-lg transition-all duration-200 " +
                   "focus:outline-none focus:ring-2 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed " +
                   "transform hover:scale-[1.02] active:scale-[0.98]"; ;
            var sizeClass = GetButtonSizeClass(size);
            var variantClass = GetButtonVariantClass(variant);

            return $"{baseClass} {sizeClass} {variantClass}";
        }


        private string GetButtonSizeClass(string size)
        {
            return size.ToLower() switch
            {
                "xs" => "px-2 py-1 text-xs",
                "small" => "px-3 py-1.5 text-sm",
                "medium" => "px-4 py-2 text-sm",
                "large" => "px-6 py-3 text-base",
                "xl" => "px-8 py-4 text-lg",
                _ => "px-4 py-2 text-sm"
            };
        }

        private string GetButtonVariantClass(string variant)
        {
            return variant.ToLower() switch
            {
                "primary" => "text-white bg-gradient-to-r from-blue-600 to-blue-700 hover:from-blue-700 hover:to-blue-800 " +
                           "focus:ring-blue-500 shadow-md hover:shadow-lg dark:shadow-blue-500/25",

                "secondary" => "text-gray-700 bg-white border border-gray-300 hover:bg-gray-50 focus:ring-blue-500 " +
                             "dark:bg-gray-800 dark:text-gray-300 dark:border-gray-600 dark:hover:bg-gray-700 shadow-sm",

                "danger" => "text-white bg-gradient-to-r from-red-600 to-red-700 hover:from-red-700 hover:to-red-800 " +
                          "focus:ring-red-500 shadow-md hover:shadow-lg dark:shadow-red-500/25",

                "success" => "text-white bg-gradient-to-r from-green-600 to-green-700 hover:from-green-700 hover:to-green-800 " +
                           "focus:ring-green-500 shadow-md hover:shadow-lg dark:shadow-green-500/25",

                "warning" => "text-white bg-gradient-to-r from-yellow-600 to-yellow-700 hover:from-yellow-700 hover:to-yellow-800 " +
                           "focus:ring-yellow-500 shadow-md hover:shadow-lg dark:shadow-yellow-500/25",

                "info" => "text-white bg-gradient-to-r from-blue-500 to-cyan-600 hover:from-blue-600 hover:to-cyan-700 " +
                        "focus:ring-blue-500 shadow-md hover:shadow-lg dark:shadow-blue-500/25",

                "ghost" => "text-gray-700 hover:bg-gray-100 focus:ring-blue-500 dark:text-gray-300 dark:hover:bg-gray-700",

                "outline" => "text-blue-600 border border-blue-600 hover:bg-blue-50 focus:ring-blue-500 " +
                           "dark:text-blue-400 dark:border-blue-400 dark:hover:bg-blue-900/20",

                _ => "text-white bg-gradient-to-r from-blue-600 to-blue-700 hover:from-blue-700 hover:to-blue-800 " +
                   "focus:ring-blue-500 shadow-md hover:shadow-lg"
            };
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
            var baseClass = "rounded-xl p-0 shadow-lg border backdrop-blur-sm max-w-sm transition-all duration-300";
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
                NotificationType.Success => "text-green-800 dark:text-green-200 font-medium",
                NotificationType.Error => "text-red-800 dark:text-red-200 font-medium",
                NotificationType.Warning => "text-yellow-800 dark:text-yellow-200 font-medium",
                NotificationType.Info => "text-blue-800 dark:text-blue-200 font-medium",
                _ => "text-gray-800 dark:text-gray-200 font-medium"
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
                NotificationType.Success => "text-green-600 dark:text-green-400 hover:bg-green-100 dark:hover:bg-green-800 rounded-md p-1 transition-colors",
                NotificationType.Error => "text-red-600 dark:text-red-400 hover:bg-red-100 dark:hover:bg-red-800 rounded-md p-1 transition-colors",
                NotificationType.Warning => "text-yellow-600 dark:text-yellow-400 hover:bg-yellow-100 dark:hover:bg-yellow-800 rounded-md p-1 transition-colors",
                NotificationType.Info => "text-blue-600 dark:text-blue-400 hover:bg-blue-100 dark:hover:bg-blue-800 rounded-md p-1 transition-colors",
                _ => "text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-md p-1 transition-colors"
            };
        }

        public string GetNotificationProgressBarClass(NotificationType type)
        {
            return type switch
            {
                NotificationType.Success => "bg-green-500 dark:bg-green-400 h-1 rounded-full transition-all duration-100 ease-linear",
                NotificationType.Error => "bg-red-500 dark:bg-red-400 h-1 rounded-full transition-all duration-100 ease-linear",
                NotificationType.Warning => "bg-yellow-500 dark:bg-yellow-400 h-1 rounded-full transition-all duration-100 ease-linear",
                NotificationType.Info => "bg-blue-500 dark:bg-blue-400 h-1 rounded-full transition-all duration-100 ease-linear",
                _ => "bg-gray-500 dark:bg-gray-400 h-1 rounded-full transition-all duration-100 ease-linear"
            };
        }
        #endregion

        #region Form Methods
        public string GetFormInputClass(bool hasError = false)
        {
            var baseClass = "block w-full px-3 py-2 border rounded-lg shadow-sm placeholder-gray-400 " +
                           "focus:outline-none focus:ring-2 focus:ring-offset-0 dark:bg-gray-700 " +
                           "dark:placeholder-gray-400 dark:text-white sm:text-sm transition-all duration-200";

            if (hasError)
            {
                return $"{baseClass} border-red-300 focus:border-red-500 focus:ring-red-500 dark:border-red-600";
            }

            return $"{baseClass} border-gray-300 focus:border-blue-500 focus:ring-blue-500 " +
                   "dark:border-gray-600 dark:focus:border-blue-500 dark:focus:ring-blue-500";
        }

        public string GetFormGridClass(int columns = 1)
        {
            return columns switch
            {
                1 => "grid grid-cols-1 gap-6",
                2 => "grid grid-cols-1 md:grid-cols-2 gap-6",
                3 => "grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6",
                4 => "grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6",
                _ => "grid grid-cols-1 gap-6"
            };
        }
        #endregion
       

        public string GetTableActionButtonClass(string variant = "primary")
        {
            return variant.ToLower() switch
            {
                "edit" => "text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-300 transition-colors p-1 rounded",
                "delete" => "text-red-600 hover:text-red-800 dark:text-red-400 dark:hover:text-red-300 transition-colors p-1 rounded",
                "view" => "text-green-600 hover:text-green-800 dark:text-green-400 dark:hover:text-green-300 transition-colors p-1 rounded",
                _ => "text-gray-600 hover:text-gray-800 dark:text-gray-400 dark:hover:text-gray-300 transition-colors p-1 rounded"
            };
        }

        #region Icon and Color Methods
        public string GetIconColorClass(string iconType)
        {
            return iconType.ToLower() switch
            {
                "warning" => "text-yellow-500 dark:text-yellow-400",
                "danger" => "text-red-500 dark:text-red-400",
                "error" => "text-red-500 dark:text-red-400",
                "info" => "text-blue-500 dark:text-blue-400",
                "success" => "text-green-500 dark:text-green-400",
                "primary" => "text-blue-500 dark:text-blue-400",
                "secondary" => "text-gray-500 dark:text-gray-400",
                _ => "text-gray-500 dark:text-gray-400"
            };
        }

        public string GetIconSizeClass(string size)
        {
            return size.ToLower() switch
            {
                "xs" => "text-xs",
                "small" => "text-sm",
                "medium" => "text-base",
                "large" => "text-lg",
                "xl" => "text-xl",
                "2xl" => "text-2xl",
                "3xl" => "text-3xl",
                "4xl" => "text-4xl",
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
            var baseClass = "left-0 top-0 h-full bg-white dark:bg-gray-800 border-r border-gray-200 dark:border-gray-700 " +
                           "flex flex-col transition-all duration-300 z-30 sidebar-transition";
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


        public string GetModalDialogSizeClass(string size)
        {
            return size.ToLower() switch
            {
                "xs" => "w-full max-w-xs",
                "small" => "w-full max-w-md",
                "medium" => "w-full max-w-lg",
                "large" => "w-full max-w-2xl",
                "xlarge" => "w-full max-w-4xl",
                "2xlarge" => "w-full max-w-6xl",
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
                "2xlarge" => "max-h-[75vh] overflow-y-auto",
                "full" => "max-h-[80vh] overflow-y-auto",
                _ => "max-h-96 overflow-y-auto"
            };

            return $"{baseClass} {heightClass}";
        }


        public string GetViewModeToggleClass(bool isActive)
        {
            return isActive
                ? GetButtonClass("primary", "small")
                : GetButtonClass("outline", "small");
        }


        public string GetLoadingSpinnerClass(string size = "medium")
        {
            var sizeClass = size.ToLower() switch
            {
                "small" => "w-4 h-4",
                "medium" => "w-8 h-8",
                "large" => "w-12 h-12",
                _ => "w-8 h-8"
            };

            return $"animate-spin rounded-full {sizeClass} border-2 border-gray-300 border-t-blue-600 dark:border-gray-600 dark:border-t-blue-400";
        }

        public string GetFileIcon(FileType fileType)
        {
            return fileType switch
            {
                FileType.Image => "fas fa-image",
                FileType.Video => "fas fa-video",
                FileType.Audio => "fas fa-music",
                FileType.Document => "fas fa-file-alt",
                FileType.Archive => "fas fa-file-archive",
                _ => "fas fa-file"
            };
        }
        public string GetFileIconColor(FileType fileType)
        {
            return fileType switch
            {
                FileType.Image => "text-green-600 dark:text-green-400",
                FileType.Video => "text-blue-600 dark:text-blue-400",
                FileType.Audio => "text-purple-600 dark:text-purple-400",
                FileType.Document => "text-red-600 dark:text-red-400",
                FileType.Archive => "text-yellow-600 dark:text-yellow-400",
                _ => "text-gray-600 dark:text-gray-400"
            };
        }
        public string GetFileTypeColor(FileType fileType)
        {
            return fileType switch
            {
                FileType.Image => "text-green-600 dark:text-green-400",
                FileType.Video => "text-blue-600 dark:text-blue-400",
                FileType.Audio => "text-purple-600 dark:text-purple-400",
                FileType.Document => "text-red-600 dark:text-red-400",
                FileType.Archive => "text-yellow-600 dark:text-yellow-400",
                _ => "text-gray-600 dark:text-gray-400"
            };
        }
        public string GetFolderTypeBadgeClass(FolderType folderType)
        {
            return folderType switch
            {
                FolderType.Images => "bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300",
                FolderType.Documents => "bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300",
                FolderType.Videos => "bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300",
                FolderType.Audio => "bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-300",
                FolderType.UserAvatars => "bg-indigo-100 text-indigo-800 dark:bg-indigo-900/30 dark:text-indigo-300",
                FolderType.CompanyAssets => "bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300",
                FolderType.Temporary => "bg-orange-100 text-orange-800 dark:bg-orange-900/30 dark:text-orange-300",
                _ => "bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300"
            };
        }
        public string GetFolderTypeText(FolderType folderType)
        {
            return folderType switch
            {
                FolderType.Images => "Images",
                FolderType.Documents => "Documents",
                FolderType.Videos => "Videos",
                FolderType.Audio => "Audio",
                FolderType.UserAvatars => "Avatars",
                FolderType.CompanyAssets => "Assets",
                FolderType.Temporary => "Temporary",
                _ => "General"
            };
        }

        public string GetDocumentIcon(string extension)
        {
            return extension.ToLower() switch
            {
                ".pdf" => "fas fa-file-pdf",
                ".doc" or ".docx" => "fas fa-file-word",
                ".xls" or ".xlsx" => "fas fa-file-excel",
                ".ppt" or ".pptx" => "fas fa-file-powerpoint",
                ".txt" => "fas fa-file-alt",
                ".zip" or ".rar" or ".7z" => "fas fa-file-archive",
                _ => "fas fa-file"
            };
        }

        public string GetDocumentColor(string extension)
        {
            return extension.ToLower() switch
            {
                ".pdf" => "text-red-600 dark:text-red-400",
                ".doc" or ".docx" => "text-blue-600 dark:text-blue-400",
                ".xls" or ".xlsx" => "text-green-600 dark:text-green-400",
                ".ppt" or ".pptx" => "text-orange-600 dark:text-orange-400",
                ".txt" => "text-gray-600 dark:text-gray-400",
                ".zip" or ".rar" or ".7z" => "text-yellow-600 dark:text-yellow-400",
                _ => "text-gray-600 dark:text-gray-400"
            };
        }
        public string GetFileIcon(string contentType)
        {
            return contentType.ToLower() switch
            {
                var ct when ct.StartsWith("image/") => "fas fa-image",
                var ct when ct.StartsWith("video/") => "fas fa-video",
                var ct when ct.StartsWith("audio/") => "fas fa-music",
                var ct when ct.Contains("pdf") => "fas fa-file-pdf",
                var ct when ct.Contains("word") => "fas fa-file-word",
                var ct when ct.Contains("excel") => "fas fa-file-excel",
                var ct when ct.Contains("powerpoint") => "fas fa-file-powerpoint",
                var ct when ct.Contains("zip") || ct.Contains("rar") || ct.Contains("7z") => "fas fa-file-archive",
                _ => "fas fa-file"
            };
        }
        public string GetDialogSizeClass(string size)
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
        public string GetBodyClass(string size)
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
        public string GetViewToggleClass(bool isActive, bool isFirst)
        {
            var baseClass = "px-4 py-2 text-sm font-medium transition-colors duration-200";
            var positionClass = isFirst ? "rounded-l-lg border border-r-0" : "rounded-r-lg border";
            var stateClass = isActive
                ? "bg-green-600 text-white border-green-600 hover:bg-green-700"
                : "bg-white dark:bg-gray-700 text-gray-700 dark:text-gray-300 border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-600";

            return $"{baseClass} {positionClass} {stateClass}";
        }
        public string GetPrimaryButtonClass(FileType fileType)
        {
            return fileType switch
            {
                FileType.Image => "bg-green-600 hover:bg-green-700 focus:ring-green-500",
                FileType.Video => "bg-blue-600 hover:bg-blue-700 focus:ring-blue-500",
                FileType.Audio => "bg-purple-600 hover:bg-purple-700 focus:ring-purple-500",
                FileType.Document => "bg-red-600 hover:bg-red-700 focus:ring-red-500",
                _ => "bg-gray-600 hover:bg-gray-700 focus:ring-gray-500"
            };
        }
        public string GetCountBadgeClass(FileType fileType)
        {
            return fileType switch
            {
                FileType.Image => "bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300",
                FileType.Video => "bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300",
                FileType.Audio => "bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-300",
                FileType.Document => "bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-300",
                _ => "bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300"
            };
        }


        public long GetMaxFileSize(FileType fileType)
        {
            return fileType switch
            {
                FileType.Image => 10 * 1024 * 1024,   // 10MB
                FileType.Video => 100 * 1024 * 1024,  // 100MB
                FileType.Audio => 50 * 1024 * 1024,   // 50MB
                FileType.Document => 50 * 1024 * 1024,// 50MB
                _ => 10 * 1024 * 1024
            };
        }

        public string GetFileTypeIcon(FileType fileType)
        {
            return fileType switch
            {
                FileType.Image => "fas fa-images",
                FileType.Video => "fas fa-video",
                FileType.Audio => "fas fa-music",
                FileType.Document => "fas fa-file-alt",
                _ => "fas fa-file"
            };
        }

        public string GetHeaderGradient(FileType fileType)
        {
            return fileType switch
            {
                FileType.Image => "from-green-500 to-green-600",
                FileType.Video => "from-blue-500 to-blue-600",
                FileType.Audio => "from-purple-500 to-purple-600",
                FileType.Document => "from-red-500 to-red-600",
                _ => "from-gray-500 to-gray-600"
            };
        }
    }
}