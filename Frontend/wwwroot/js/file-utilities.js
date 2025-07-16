// wwwroot/js/file-utilities.js

/**
 * File picker and upload utilities
 */
window.fileUtilities = {
    /**
     * Trigger file input dialog
     */
    triggerFileInput: function (element) {
        if (element && element.click) {
            element.click();
        }
    },

    /**
     * Copy text to clipboard
     */
    copyToClipboard: async function (text) {
        try {
            if (navigator.clipboard && window.isSecureContext) {
                await navigator.clipboard.writeText(text);
                return true;
            } else {
                // Fallback for older browsers
                const textArea = document.createElement('textarea');
                textArea.value = text;
                textArea.style.position = 'fixed';
                textArea.style.left = '-999999px';
                textArea.style.top = '-999999px';
                document.body.appendChild(textArea);
                textArea.focus();
                textArea.select();
                const result = document.execCommand('copy');
                textArea.remove();
                return result;
            }
        } catch (error) {
            console.error('Failed to copy to clipboard:', error);
            return false;
        }
    },

    /**
     * Download file with authentication
     */
    downloadFileWithAuth: async function (url, filename) {
        try {
            const response = await fetch(url, {
                method: 'GET',
                credentials: 'include',
                headers: {
                    'Accept': '*/*'
                }
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const blob = await response.blob();
            const downloadUrl = window.URL.createObjectURL(blob);

            const link = document.createElement('a');
            link.href = downloadUrl;
            link.download = filename || 'download';
            link.style.display = 'none';

            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);

            // Clean up
            setTimeout(() => window.URL.revokeObjectURL(downloadUrl), 100);

            return true;
        } catch (error) {
            console.error('Download failed:', error);
            // Fallback to direct navigation
            window.open(url, '_blank');
            return false;
        }
    },

    /**
     * Validate file before upload
     */
    validateFile: function (file, options = {}) {
        const errors = [];

        // Check file size
        if (options.maxSize && file.size > options.maxSize) {
            errors.push(`File size exceeds ${this.formatFileSize(options.maxSize)}`);
        }

        // Check file type
        if (options.allowedTypes && options.allowedTypes.length > 0) {
            const fileExtension = file.name.split('.').pop().toLowerCase();
            const isAllowed = options.allowedTypes.some(type =>
                type.toLowerCase() === fileExtension ||
                file.type.startsWith(type)
            );

            if (!isAllowed) {
                errors.push(`File type ${fileExtension} is not allowed`);
            }
        }

        // Check file name
        if (options.maxNameLength && file.name.length > options.maxNameLength) {
            errors.push(`File name is too long (max ${options.maxNameLength} characters)`);
        }

        return {
            isValid: errors.length === 0,
            errors: errors
        };
    },

    /**
     * Format file size for display
     */
    formatFileSize: function (bytes) {
        if (bytes === 0) return '0 Bytes';

        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));

        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    },

    /**
     * Create file preview
     */
    createFilePreview: function (file, previewElement) {
        if (!file || !previewElement) return;

        const reader = new FileReader();

        reader.onload = function (e) {
            if (file.type.startsWith('image/')) {
                previewElement.innerHTML = `
                    <img src="${e.target.result}" 
                         alt="Preview" 
                         class="w-full h-full object-cover rounded" />
                `;
            } else {
                previewElement.innerHTML = `
                    <div class="flex flex-col items-center justify-center h-full p-4">
                        <i class="fas fa-file text-4xl text-gray-400 mb-2"></i>
                        <span class="text-sm text-gray-600 text-center">${file.name}</span>
                    </div>
                `;
            }
        };

        if (file.type.startsWith('image/')) {
            reader.readAsDataURL(file);
        } else {
            // For non-image files, show file icon
            reader.onload();
        }
    },

    /**
     * Drag and drop utilities
     */
    dragAndDrop: {
        setupDropZone: function (element, onDrop, onDragOver, onDragLeave) {
            if (!element) return;

            element.addEventListener('dragover', function (e) {
                e.preventDefault();
                e.stopPropagation();
                if (onDragOver) onDragOver(e);
            });

            element.addEventListener('dragenter', function (e) {
                e.preventDefault();
                e.stopPropagation();
            });

            element.addEventListener('dragleave', function (e) {
                e.preventDefault();
                e.stopPropagation();
                if (onDragLeave) onDragLeave(e);
            });

            element.addEventListener('drop', function (e) {
                e.preventDefault();
                e.stopPropagation();

                const files = Array.from(e.dataTransfer.files);
                if (onDrop) onDrop(files, e);
            });
        }
    }
};

/**
 * Video streaming utilities
 */
window.videoStreamingUtils = {
    /**
     * Setup video element with multiple sources and error handling
     */
    setupVideoElement: function (videoElement, primaryUrl, fallbackUrl) {
        if (!videoElement) return;

        // Clear existing sources
        while (videoElement.firstChild) {
            videoElement.removeChild(videoElement.firstChild);
        }

        // Add primary source
        if (primaryUrl) {
            const primarySource = document.createElement('source');
            primarySource.src = primaryUrl;
            primarySource.type = 'video/mp4'; // Default, could be dynamic
            videoElement.appendChild(primarySource);
        }

        // Add fallback source
        if (fallbackUrl && fallbackUrl !== primaryUrl) {
            const fallbackSource = document.createElement('source');
            fallbackSource.src = fallbackUrl;
            fallbackSource.type = 'video/mp4'; // Default, could be dynamic
            videoElement.appendChild(fallbackSource);
        }

        // Add error handling
        videoElement.addEventListener('error', function (e) {
            console.error('Video error:', e);
        });

        // Load the video
        videoElement.load();
    },

    /**
     * Setup audio element with multiple sources and error handling
     */
    setupAudioElement: function (audioElement, primaryUrl, fallbackUrl) {
        if (!audioElement) return;

        // Clear existing sources
        while (audioElement.firstChild) {
            audioElement.removeChild(audioElement.firstChild);
        }

        // Add primary source
        if (primaryUrl) {
            const primarySource = document.createElement('source');
            primarySource.src = primaryUrl;
            primarySource.type = 'audio/mpeg'; // Default, could be dynamic
            audioElement.appendChild(primarySource);
        }

        // Add fallback source
        if (fallbackUrl && fallbackUrl !== primaryUrl) {
            const fallbackSource = document.createElement('source');
            fallbackSource.src = fallbackUrl;
            fallbackSource.type = 'audio/mpeg'; // Default, could be dynamic
            audioElement.appendChild(fallbackSource);
        }

        // Add error handling
        audioElement.addEventListener('error', function (e) {
            console.error('Audio error:', e);
        });

        // Load the audio
        audioElement.load();
    }
};

/**
 * Escape key handler for dialogs
 */
window.escapeKeyHandlers = [];

window.addEscapeKeyListener = function (dotNetObjectRef) {
    const handler = function (event) {
        if (event.key === 'Escape') {
            dotNetObjectRef.invokeMethodAsync('HandleEscapeKey');
        }
    };

    document.addEventListener('keydown', handler);
    window.escapeKeyHandlers.push(handler);
};

window.removeEscapeKeyListener = function () {
    window.escapeKeyHandlers.forEach(handler => {
        document.removeEventListener('keydown', handler);
    });
    window.escapeKeyHandlers = [];
};

/**
 * Global aliases for backward compatibility
 */
window.triggerFileInput = window.fileUtilities.triggerFileInput;
window.copyToClipboard = window.fileUtilities.copyToClipboard;
window.downloadFileWithAuth = window.fileUtilities.downloadFileWithAuth;