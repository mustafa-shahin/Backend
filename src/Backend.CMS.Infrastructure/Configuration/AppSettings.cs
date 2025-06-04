using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.CMS.Infrastructure.Configuration
{
    public class AppSettings
    {
        public DatabaseSettings Database { get; set; } = new();
        public SecuritySettings Security { get; set; } = new();
        public CacheSettings Cache { get; set; } = new();
        public FileStorageSettings FileStorage { get; set; } = new();
        public EmailSettings Email { get; set; } = new();
        public LoggingSettings Logging { get; set; } = new();
    }

    public class SecuritySettings
    {
        public string JwtSecret { get; set; } = string.Empty;
        public int JwtExpirationMinutes { get; set; } = 60;
        public int RefreshTokenExpirationDays { get; set; } = 7;
        public int MaxFailedLoginAttempts { get; set; } = 5;
        public int LockoutDurationMinutes { get; set; } = 15;
        public bool RequireEmailVerification { get; set; } = true;
        public bool EnableTwoFactor { get; set; } = false;
    }
}
