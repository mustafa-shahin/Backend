// File utility functions for frontend
window.triggerFileInput = (element) => {
    if (element) {
        element.click();
    }
};

window.fileUtils = {
    triggerFileInput: (element) => {
        if (element) {
            element.click();
        }
    },
    
    formatFileSize: (bytes) => {
        if (bytes === 0) return '0 Bytes';
        
        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    },
    
    validateImageFile: (file, maxSize = 10 * 1024 * 1024) => {
        const errors = [];
        
        if (!file.type.startsWith('image/')) {
            errors.push('File must be an image');
        }
        
        if (file.size > maxSize) {
            errors.push(`File size must be less than ${fileUtils.formatFileSize(maxSize)}`);
        }
        
        return {
            isValid: errors.length === 0,
            errors: errors
        };
    }
};