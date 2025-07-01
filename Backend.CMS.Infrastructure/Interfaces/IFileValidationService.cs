using Backend.CMS.Domain.Enums;

namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IFileValidationService
    {
        bool IsAllowedFileType(string fileName, string contentType);
        bool IsAllowedFileSize(long fileSize);
        Task<bool> IsSafeFileAsync(Stream fileStream, string fileName);
        bool IsImageFile(string fileName, string contentType);
        bool IsVideoFile(string fileName, string contentType);
        bool IsDocumentFile(string fileName, string contentType);
        FileType GetFileType(string fileName, string contentType);
        List<string> GetAllowedExtensions();
        long GetMaxFileSize();
    }
}
