// loading feedback without blocking operations
let loadingSteps = [
  "Starting application...",
  "Loading Blazor WebAssembly...",
  "Initializing components...",
  "Connecting to services...",
  "Almost ready...",
];
let currentStep = 0;

function updateLoadingStatus(message, progress) {
  const statusElement = document.getElementById("loading-status");
  const progressElement = document.getElementById("loading-progress");

  if (statusElement && message) {
    statusElement.textContent = message;
  }

  if (progressElement && typeof progress === "number") {
    progressElement.style.width = progress + "%";
  }
}

function advanceLoadingStep() {
  if (currentStep < loadingSteps.length) {
    updateLoadingStatus(loadingSteps[currentStep], (currentStep + 1) * 20);
    currentStep++;
  }
}

// Progress simulation
let progressInterval = setInterval(() => {
  advanceLoadingStep();
  if (currentStep >= loadingSteps.length) {
    clearInterval(progressInterval);
  }
}, 800);

// Blazor event handlers (non-blocking)
window.addEventListener("DOMContentLoaded", function () {
  console.log("DOM loaded, initializing Blazor...");
});

// Error handling
window.addEventListener("error", function (e) {
  console.error("Global error:", e.error);
  if (window.fileHelpers) {
    window.fileHelpers.showNotification(
      "An unexpected error occurred",
      "error",
      8000
    );
  }
});

// Global promise rejection handler
window.addEventListener("unhandledrejection", function (e) {
  console.error("Unhandled promise rejection:", e.reason);
  if (window.fileHelpers) {
    window.fileHelpers.showNotification(
      "An unexpected error occurred",
      "error",
      8000
    );
  }
});

// ===== FILE INPUT TRIGGER =====
window.triggerFileInput = function (element) {
  try {
    if (element && typeof element.click === "function") {
      element.click();
      return true;
    }
    return false;
  } catch (error) {
    console.error("Failed to trigger file input:", error);
    return false;
  }
};

// ===== Essential Utilities Only =====

// Copy to clipboard
window.copyToClipboard = function (text) {
  if (navigator.clipboard && navigator.clipboard.writeText) {
    return navigator.clipboard
      .writeText(text)
      .then(() => {
        return true;
      })
      .catch(() => {
        return fallbackCopyToClipboard(text);
      });
  } else {
    return Promise.resolve(fallbackCopyToClipboard(text));
  }
};

function fallbackCopyToClipboard(text) {
  try {
    const textArea = document.createElement("textarea");
    textArea.value = text;
    textArea.style.position = "fixed";
    textArea.style.left = "-999999px";
    textArea.style.top = "-999999px";
    document.body.appendChild(textArea);
    textArea.focus();
    textArea.select();
    const result = document.execCommand("copy");
    document.body.removeChild(textArea);
    return result;
  } catch (err) {
    console.error("Fallback copy failed:", err);
    return false;
  }
}

// Theme detection
window.getSystemTheme = function () {
  try {
    return window.matchMedia("(prefers-color-scheme: dark)").matches
      ? "dark"
      : "light";
  } catch {
    return "light";
  }
};
// Local storage helpers with error handling
window.setLocalStorage = function (key, value) {
  try {
    localStorage.setItem(key, JSON.stringify(value));
    return true;
  } catch (error) {
    console.warn("Failed to set localStorage:", error);
    return false;
  }
};
window.getLocalStorage = function (key) {
  try {
    const item = localStorage.getItem(key);
    return item ? JSON.parse(item) : null;
  } catch (error) {
    console.warn("Failed to get localStorage:", error);
    return null;
  }
};

window.removeLocalStorage = function (key) {
  try {
    localStorage.removeItem(key);
    return true;
  } catch (error) {
    console.warn("Failed to remove localStorage:", error);
    return false;
  }
};

// Focus element safely
window.focusElement = function (selector) {
  try {
    const element = document.querySelector(selector);
    if (element && typeof element.focus === "function") {
      // Use requestAnimationFrame to ensure DOM is ready
      requestAnimationFrame(() => {
        try {
          element.focus();
        } catch (err) {
          console.warn("Focus failed on animation frame:", err);
        }
      });
    }
  } catch (err) {
    console.warn("Focus element failed:", err);
  }
};
// ===== Modal/Dialog Utilities =====

// Global variables for escape key handling
let currentEscapeHandler = null;
let escapeKeyListener = null;

// Add escape key listener for modals with error handling
window.addEscapeKeyListener = function (dotNetRef) {
  try {
    // Remove existing listener first
    removeEscapeKeyListener();

    // Store the .NET reference
    currentEscapeHandler = dotNetRef;

    // Create the event handler
    escapeKeyListener = function (event) {
      if (event.key === "Escape" && currentEscapeHandler) {
        try {
          currentEscapeHandler.invokeMethodAsync("HandleEscapeKey");
        } catch (err) {
          console.warn("Failed to invoke escape key handler:", err);
        }
      }
    };

    // Add the listener
    document.addEventListener("keydown", escapeKeyListener);
  } catch (err) {
    console.error("Failed to add escape key listener:", err);
  }
};

// Remove escape key listener
window.removeEscapeKeyListener = function () {
  try {
    if (escapeKeyListener) {
      document.removeEventListener("keydown", escapeKeyListener);
      escapeKeyListener = null;
    }
    currentEscapeHandler = null;
  } catch (err) {
    console.error("Failed to remove escape key listener:", err);
  }
};

// ===== End Modal/Dialog Utilities =====

// Clean up loading when Blazor starts
window.addEventListener("blazor:started", function () {
  clearInterval(progressInterval);
  updateLoadingStatus("Ready!", 100);

  setTimeout(() => {
    const appElement = document.getElementById("app");
    if (appElement && appElement.children.length > 1) {
      // Remove loading screen when app is ready
      const loadingScreen = appElement.firstElementChild;
      if (loadingScreen) {
        loadingScreen.style.opacity = "0";
        loadingScreen.style.transition = "opacity 0.3s ease-out";
        setTimeout(() => {
          if (loadingScreen.parentNode) {
            loadingScreen.parentNode.removeChild(loadingScreen);
          }
        }, 300);
      }
    }
  }, 500);

  // Initialize file helpers
  if (window.fileHelpers) {
    console.log("File helpers initialized successfully");
  }
});

// Debug helpers for development
if (
  window.location.hostname === "localhost" ||
  window.location.hostname === "127.0.0.1"
) {
  window.debugFileService = {
    testDownload: (fileId) => {
      console.log("Testing download for file:", fileId);
      return window.downloadFileWithAuth(
        `/api/file/${fileId}/download`,
        `test-file-${fileId}`
      );
    },
    testNotification: (type = "info") => {
      return window.fileHelpers.showNotification(
        `Test ${type} notification`,
        type,
        3000
      );
    },
    resetStates: () => {
      return window.resetDialogStates();
    },
  };

  console.log(
    "%c🎨 CMS Designer %c- Development Mode",
    "background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 8px 12px; border-radius: 4px; font-weight: bold;",
    "color: #667eea; font-weight: normal;"
  );
}

window.fileHelpers = {
  /**
   * Downloads a file with authentication support
   * @param {string} url - Download URL
   * @param {string} filename - Optional filename
   */
  downloadFileWithAuth: function (url, filename) {
    try {
      // Create a temporary link element
      const link = document.createElement("a");
      link.href = url;
      link.download = filename || "download";
      link.style.display = "none";

      // Add to DOM, click, and remove
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);

      console.log(`File download initiated: ${filename || "download"}`);
      this.showNotification(
        `Download started: ${filename || "file"}`,
        "success",
        3000
      );
    } catch (error) {
      console.error("Error downloading file:", error);
      this.showNotification("Download failed", "error");
    }
  },

  /**
   * Downloads file using fetch API with proper error handling
   * @param {string} url - Download URL
   * @param {string} filename - Filename
   * @param {Object} options - Additional options
   */
  downloadFileWithFetch: async function (url, filename, options = {}) {
    try {
      const response = await fetch(url, {
        method: "GET",
        headers: {
          Authorization: this.getAuthHeader(),
          ...options.headers,
        },
      });

      if (!response.ok) {
        throw new Error(
          `Download failed: ${response.status} ${response.statusText}`
        );
      }

      const blob = await response.blob();
      const downloadUrl = window.URL.createObjectURL(blob);

      const link = document.createElement("a");
      link.href = downloadUrl;
      link.download = filename;
      link.click();

      // Cleanup
      window.URL.revokeObjectURL(downloadUrl);

      console.log(`File downloaded successfully: ${filename}`);
      this.showNotification(`Downloaded: ${filename}`, "success");
    } catch (error) {
      console.error("Error downloading file:", error);
      this.showNotification(`Download failed: ${error.message}`, "error");
    }
  },

  /**
   * Gets the authorization header for authenticated requests
   * @returns {string} Authorization header value
   */
  getAuthHeader: function () {
    const token =
      localStorage.getItem("authToken") || sessionStorage.getItem("authToken");
    return token ? `Bearer ${token}` : "";
  },

  /**
   * Validates file before upload
   * @param {File} file - File to validate
   * @param {Object} options - Validation options
   * @returns {Object} Validation result
   */
  validateFile: function (file, options = {}) {
    const result = {
      isValid: true,
      errors: [],
      warnings: [],
    };

    // Size validation
    const maxSize = options.maxSize || 10 * 1024 * 1024; // 10MB default
    if (file.size > maxSize) {
      result.isValid = false;
      result.errors.push(
        `File size (${this.formatFileSize(
          file.size
        )}) exceeds maximum allowed size (${this.formatFileSize(maxSize)})`
      );
    }

    // Type validation
    const allowedTypes = options.allowedTypes || [];
    if (allowedTypes.length > 0) {
      const fileExtension = "." + file.name.split(".").pop().toLowerCase();
      const isAllowed =
        allowedTypes.includes(fileExtension) ||
        allowedTypes.includes(file.type);

      if (!isAllowed) {
        result.isValid = false;
        result.errors.push(
          `File type not allowed. Allowed types: ${allowedTypes.join(", ")}`
        );
      }
    }

    // Name validation
    if (file.name.length > 255) {
      result.warnings.push("Filename is very long and may be truncated");
    }

    // Special character validation
    const invalidChars = /[<>:"/\\|?*]/;
    if (invalidChars.test(file.name)) {
      result.warnings.push(
        "Filename contains special characters that may cause issues"
      );
    }

    return result;
  },

  /**
   * Formats file size in human-readable format
   * @param {number} bytes - Size in bytes
   * @returns {string} Formatted size
   */
  formatFileSize: function (bytes) {
    if (bytes === 0) return "0 B";

    const sizes = ["B", "KB", "MB", "GB", "TB"];
    const i = Math.floor(Math.log(bytes) / Math.log(1024));

    return Math.round((bytes / Math.pow(1024, i)) * 100) / 100 + " " + sizes[i];
  },

  /**
   * Formats duration in human-readable format
   * @param {number} seconds - Duration in seconds
   * @returns {string} Formatted duration
   */
  formatDuration: function (seconds) {
    if (!seconds || seconds < 0) return "0:00";

    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const secs = Math.floor(seconds % 60);

    if (hours > 0) {
      return `${hours}:${minutes.toString().padStart(2, "0")}:${secs
        .toString()
        .padStart(2, "0")}`;
    }
    return `${minutes}:${secs.toString().padStart(2, "0")}`;
  },

  /**
   * Shows a notification to the user
   * @param {string} message - Notification message
   * @param {string} type - Notification type (success, error, warning, info)
   * @param {number} duration - Display duration in milliseconds
   */
  showNotification: function (message, type = "info", duration = 5000) {
    // Try to use existing notification system
    if (
      window.blazorNotifications &&
      typeof window.blazorNotifications.show === "function"
    ) {
      window.blazorNotifications.show(message, type, duration);
      return;
    }

    // Fallback to console
    console.log(`[${type.toUpperCase()}] ${message}`);

    // Simple toast notification fallback
    this.createToastNotification(message, type, duration);
  },

  /**
   * Creates a simple toast notification
   * @param {string} message - Message text
   * @param {string} type - Notification type
   * @param {number} duration - Display duration
   */
  createToastNotification: function (message, type, duration) {
    // Create toast container if it doesn't exist
    let container = document.getElementById("toast-container");
    if (!container) {
      container = document.createElement("div");
      container.id = "toast-container";
      container.style.cssText = `
        position: fixed;
        top: 20px;
        right: 20px;
        z-index: 10000;
        pointer-events: none;
        `;
      document.body.appendChild(container);
    }

    // Create toast element
    const toast = document.createElement("div");
    toast.style.cssText = `
        background: ${this.getToastColor(type)};
        color: white;
        padding: 12px 20px;
        margin-bottom: 10px;
        border-radius: 8px;
        box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        pointer-events: auto;
        cursor: pointer;
        max-width: 350px;
        opacity: 0;
        transform: translateX(100%);
        transition: all 0.3s ease;
        font-size: 14px;
        font-weight: 500;
        `;
    toast.innerHTML = `
        <div style="display: flex; align-items: center; justify-content: space-between;">
            <span>${message}</span>
            <button style="background: transparent; border: none; color: white; cursor: pointer; margin-left: 12px; padding: 0; font-size: 16px;" onclick="this.parentElement.parentElement.click()">
                ✕
            </button>
        </div>
        `;

    // Add to container
    container.appendChild(toast);

    // Animate in
    setTimeout(() => {
      toast.style.opacity = "1";
      toast.style.transform = "translateX(0)";
    }, 10);

    // Remove after duration
    const removeToast = () => {
      toast.style.opacity = "0";
      toast.style.transform = "translateX(100%)";
      setTimeout(() => {
        if (toast.parentNode) {
          toast.parentNode.removeChild(toast);
        }
      }, 300);
    };

    setTimeout(removeToast, duration);

    // Click to dismiss
    toast.addEventListener("click", removeToast);
  },

  /**
   * Gets color for toast notification based on type
   * @param {string} type - Notification type
   * @returns {string} CSS color value
   */
  getToastColor: function (type) {
    const colors = {
      success: "#10b981",
      error: "#ef4444",
      warning: "#f59e0b",
      info: "#3b82f6",
    };
    return colors[type] || colors.info;
  },

  /**
   * Copies text to clipboard with error handling
   * @param {string} text - Text to copy
   * @returns {Promise < boolean >} Success status
   */
  copyToClipboard: async function (text) {
    try {
      if (navigator.clipboard && window.isSecureContext) {
        await navigator.clipboard.writeText(text);
        this.showNotification("Copied to clipboard", "success", 2000);
        return true;
      } else {
        // Fallback for older browsers
        const textArea = document.createElement("textarea");
        textArea.value = text;
        textArea.style.position = "fixed";
        textArea.style.opacity = "0";
        textArea.style.pointerEvents = "none";
        document.body.appendChild(textArea);
        textArea.focus();
        textArea.select();

        try {
          const successful = document.execCommand("copy");
          document.body.removeChild(textArea);

          if (successful) {
            this.showNotification("Copied to clipboard", "success", 2000);
            return true;
          }
        } catch (err) {
          document.body.removeChild(textArea);
          throw err;
        }
      }
    } catch (error) {
      console.error("Failed to copy to clipboard:", error);
      this.showNotification("Failed to copy to clipboard", "error");
      return false;
    }
  },

  /**
   * Resets all dialog states - useful for preventing cross-dialog interference
   */
  resetDialogStates: function () {
    try {
      // Clear any modal backdrop click handlers
      const backdrops = document.querySelectorAll(".modal-backdrop");
      backdrops.forEach((backdrop) => {
        backdrop.onclick = null;
      });

      // Reset any file input states
      const fileInputs = document.querySelectorAll('input[type="file"]');
      fileInputs.forEach((input) => {
        input.value = "";
      });

      console.log("Dialog states reset successfully");
    } catch (error) {
      console.error("Error resetting dialog states:", error);
    }
  },

  /**
   * Debounces a function call
   * @param {Function} func - Function to debounce
   * @param {number} delay - Delay in milliseconds
   * @returns {Function} Debounced function
   */
  debounce: function (func, delay) {
    let timeoutId;
    return function (...args) {
      clearTimeout(timeoutId);
      timeoutId = setTimeout(() => func.apply(this, args), delay);
    };
  },
};

// Global functions for Blazor interop
window.downloadFileWithAuth = window.fileHelpers.downloadFileWithAuth.bind(
  window.fileHelpers
);
window.validateFile = window.fileHelpers.validateFile.bind(window.fileHelpers);
window.formatFileSize = window.fileHelpers.formatFileSize.bind(
  window.fileHelpers
);
window.formatDuration = window.fileHelpers.formatDuration.bind(
  window.fileHelpers
);
window.copyToClipboard = window.fileHelpers.copyToClipboard.bind(
  window.fileHelpers
);
window.resetDialogStates = window.fileHelpers.resetDialogStates.bind(
  window.fileHelpers
);
console.log("File helpers loaded successfully");

// copy to clipboard with better error handling
window.copyToClipboard = async (text) => {
  try {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(text);
      return true;
    } else {
      // Fallback for older browsers
      return fallbackCopyToClipboard(text);
    }
  } catch (error) {
    console.error("Failed to copy to clipboard:", error);
    return fallbackCopyToClipboard(text);
  }
};

function fallbackCopyToClipboard(text) {
  try {
    const textArea = document.createElement("textarea");
    textArea.value = text;
    textArea.style.position = "fixed";
    textArea.style.left = "-999999px";
    textArea.style.top = "-999999px";
    document.body.appendChild(textArea);
    textArea.focus();
    textArea.select();

    const result = document.execCommand("copy");
    document.body.removeChild(textArea);
    return result;
  } catch (err) {
    console.error("Fallback copy failed:", err);
    return false;
  }
}

// Prevent modal backdrop issues
window.preventModalClose = (event) => {
  event.stopPropagation();
};

// File preview helpers
window.previewFile = (url, contentType) => {
  try {
    if (contentType.startsWith("image/")) {
      // For images, open in new tab
      window.open(url, "_blank");
    } else if (
      contentType.startsWith("video/") ||
      contentType.startsWith("audio/")
    ) {
      // For media files, open in new tab
      window.open(url, "_blank");
    } else {
      // For other files, try to download
      window.downloadFileWithAuth(url, "preview");
    }
  } catch (error) {
    console.error("Preview failed:", error);
    // Fallback to download
    window.downloadFileWithAuth(url, "preview");
  }
};

// Utility to get file extension from filename
window.getFileExtension = (filename) => {
  return filename.slice(((filename.lastIndexOf(".") - 1) >>> 0) + 2);
};

// Utility to format file size
window.formatFileSize = (bytes) => {
  if (bytes === 0) return "0 Bytes";

  const k = 1024;
  const sizes = ["Bytes", "KB", "MB", "GB", "TB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));

  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i];
};

window.showNotification = (message, type = "info", duration = 5000) => {
  const notification = document.createElement("div");
  notification.className = `fixed top-4 right-4 z-50 p-4 rounded-lg shadow-lg transition-all duration-300 transform translate-x-full`;

  // Set notification style based on type
  switch (type) {
    case "success":
      notification.className += " bg-green-500 text-white";
      break;
    case "error":
      notification.className += " bg-red-500 text-white";
      break;
    case "warning":
      notification.className += " bg-yellow-500 text-black";
      break;
    default:
      notification.className += " bg-blue-500 text-white";
  }

  notification.innerHTML = `
        <div class="flex items-center space-x-2">
            <span>${message}</span>
            <button onclick="this.parentElement.parentElement.remove()" class="ml-2 text-current opacity-70 hover:opacity-100">
                <i class="fas fa-times"></i>
            </button>
        </div>
        `;

  document.body.appendChild(notification);

  // Animate in
  setTimeout(() => {
    notification.classList.remove("translate-x-full");
  }, 100);

  // Auto remove after duration
  setTimeout(() => {
    notification.classList.add("translate-x-full");
    setTimeout(() => {
      if (notification.parentNode) {
        notification.parentNode.removeChild(notification);
      }
    }, 300);
  }, duration);
};

// Debug helpers for development
if (
  window.location.hostname === "localhost" ||
  window.location.hostname === "127.0.0.1"
) {
  window.debugFileService = {
    testDownload: (fileId) => {
      console.log("Testing download for file:", fileId);
      return window.downloadFileWithAuth(
        `/api/file/${fileId}/download`,
        `test-file-${fileId}`
      );
    },
    testPreview: (fileId) => {
      console.log("Testing preview for file:", fileId);
      return window.previewFile(
        `/api/file/${fileId}/download`,
        "application/octet-stream"
      );
    },
  };
}
