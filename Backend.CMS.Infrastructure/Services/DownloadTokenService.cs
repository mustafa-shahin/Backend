using Backend.CMS.Infrastructure.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace Backend.CMS.Infrastructure.Services
{
    public class DownloadTokenService : IDownloadTokenService
    {
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(5);

        public DownloadTokenService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<string> GenerateDownloadTokenAsync(int fileId, int? userId = null, TimeSpan? expirationTime = null)
        {
            var token = GenerateSecureToken();
            var expiration = expirationTime ?? _defaultExpiration;

            var tokenData = new DownloadTokenData
            {
                FileId = fileId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(expiration)
            };

            _cache.Set($"download_token_{token}", tokenData, expiration);

            return token;
        }

        public async Task<(bool IsValid, int FileId, int? UserId)> ValidateTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
                return (false, 0, null);

            if (_cache.TryGetValue($"download_token_{token}", out DownloadTokenData tokenData))
            {
                if (tokenData.ExpiresAt > DateTime.UtcNow)
                {
                    // Remove token after use for security
                    _cache.Remove($"download_token_{token}");
                    return (true, tokenData.FileId, tokenData.UserId);
                }
            }

            return (false, 0, null);
        }

        private static string GenerateSecureToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
        }

        private class DownloadTokenData
        {
            public int FileId { get; set; }
            public int? UserId { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
        }
    }
}