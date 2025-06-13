using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Application.Interfaces
{
    public interface IDownloadTokenService
    {
        Task<string> GenerateDownloadTokenAsync(int fileId, int? userId = null, TimeSpan? expirationTime = null);
        Task<(bool IsValid, int FileId, int? UserId)> ValidateTokenAsync(string token);
    }
}
