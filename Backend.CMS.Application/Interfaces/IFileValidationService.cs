using Backend.CMS.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Application.Interfaces
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
