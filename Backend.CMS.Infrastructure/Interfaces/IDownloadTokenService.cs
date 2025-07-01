namespace Backend.CMS.Infrastructure.Interfaces
{
    public interface IDownloadTokenService
    {
        Task<string> GenerateDownloadTokenAsync(int fileId, int? userId = null, TimeSpan? expirationTime = null);
        Task<(bool IsValid, int FileId, int? UserId)> ValidateTokenAsync(string token);
    }
}
