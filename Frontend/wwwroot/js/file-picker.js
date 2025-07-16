

window.triggerFileInput = (element) => {
    if (element) {
        element.click();
    }
};

window.copyToClipboard = async (text) => {
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

            const successful = document.execCommand('copy');
            document.body.removeChild(textArea);
            return successful;
        }
    } catch (error) {
        console.error('Failed to copy to clipboard:', error);
        return false;
    }
};

window.downloadFileWithAuth = async (url, filename) => {
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
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);

        // Clean up the blob URL
        window.URL.revokeObjectURL(downloadUrl);

        return true;
    } catch (error) {
        console.error('Download failed:', error);
        // Fallback to opening in new tab
        window.open(url, '_blank');
        return false;
    }
};

// File drag and drop helpers
window.fileDragDrop = {
    setup: (element, dotNetRef, methodName) => {
        if (!element) return;

        const preventDefault = (e) => {
            e.preventDefault();
            e.stopPropagation();
        };

        const handleDrop = (e) => {
            preventDefault(e);
            element.classList.remove('drag-over');

            const files = Array.from(e.dataTransfer.files);
            if (files.length > 0) {
                dotNetRef.invokeMethodAsync(methodName, files);
            }
        };

        const handleDragOver = (e) => {
            preventDefault(e);
            element.classList.add('drag-over');
        };

        const handleDragLeave = (e) => {
            preventDefault(e);
            element.classList.remove('drag-over');
        };

        element.addEventListener('dragenter', preventDefault);
        element.addEventListener('dragover', handleDragOver);
        element.addEventListener('dragleave', handleDragLeave);
        element.addEventListener('drop', handleDrop);

        return {
            dispose: () => {
                element.removeEventListener('dragenter', preventDefault);
                element.removeEventListener('dragover', handleDragOver);
                element.removeEventListener('dragleave', handleDragLeave);
                element.removeEventListener('drop', handleDrop);
            }
        };
    }
};

// Escape key listener for modals
window.addEscapeKeyListener = (dotNetRef) => {
    const handleEscape = (e) => {
        if (e.key === 'Escape') {
            dotNetRef.invokeMethodAsync('HandleEscapeKey');
        }
    };

    document.addEventListener('keydown', handleEscape);

    // Store reference for cleanup
    window.currentEscapeHandler = handleEscape;
};

window.removeEscapeKeyListener = () => {
    if (window.currentEscapeHandler) {
        document.removeEventListener('keydown', window.currentEscapeHandler);
        window.currentEscapeHandler = null;
    }
};

// Video streaming utilities
window.videoStreamingUtils = {
    setupVideoElement: (videoElement, primaryUrl, fallbackUrl) => {
        if (!videoElement) return;

        const handleError = () => {
            console.warn('Primary video source failed, trying fallback...');
            if (fallbackUrl && fallbackUrl !== primaryUrl) {
                videoElement.src = fallbackUrl;
            }
        };

        videoElement.addEventListener('error', handleError);
        videoElement.src = primaryUrl;

        return {
            dispose: () => {
                videoElement.removeEventListener('error', handleError);
            }
        };
    },

    setupAudioElement: (audioElement, primaryUrl, fallbackUrl) => {
        if (!audioElement) return;

        const handleError = () => {
            console.warn('Primary audio source failed, trying fallback...');
            if (fallbackUrl && fallbackUrl !== primaryUrl) {
                audioElement.src = fallbackUrl;
            }
        };

        audioElement.addEventListener('error', handleError);
        audioElement.src = primaryUrl;

        return {
            dispose: () => {
                audioElement.removeEventListener('error', handleError);
            }
        };
    }
};