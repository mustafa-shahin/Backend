

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
        public MonitoringSettings Monitoring { get; set; } = new();
        public TenantSettings Tenant { get; set; } = new();
    }

    public class DatabaseSettings
    {
        public string DefaultConnection { get; set; } = string.Empty;
        public string MasterConnection { get; set; } = string.Empty;
        public int CommandTimeout { get; set; } = 30;
        public int MaxRetryCount { get; set; } = 3;
        public int MaxRetryDelay { get; set; } = 30;
        public bool EnableSensitiveDataLogging { get; set; } = false;
        public bool EnableDetailedErrors { get; set; } = false;
        public Dictionary<string, string> TenantConnections { get; set; } = new();
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
        public PasswordPolicySettings PasswordPolicy { get; set; } = new();
        public RateLimitSettings RateLimit { get; set; } = new();
        public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
        public SecurityHeaderSettings Headers { get; set; } = new();
    }

    public class PasswordPolicySettings
    {
        public int MinLength { get; set; } = 8;
        public int MaxLength { get; set; } = 128;
        public bool RequireUppercase { get; set; } = true;
        public bool RequireLowercase { get; set; } = true;
        public bool RequireDigit { get; set; } = true;
        public bool RequireSpecialCharacter { get; set; } = true;
        public int MinUniqueCharacters { get; set; } = 4;
        public int PasswordHistoryLimit { get; set; } = 5;
        public int PasswordExpirationDays { get; set; } = 90;
    }

    public class RateLimitSettings
    {
        public int RequestsPerMinute { get; set; } = 100;
        public int BurstLimit { get; set; } = 20;
        public int WindowSizeMinutes { get; set; } = 1;
        public string[] ExemptedIPs { get; set; } = Array.Empty<string>();
        public Dictionary<string, int> EndpointLimits { get; set; } = new();
    }

    public class SecurityHeaderSettings
    {
        public string ContentSecurityPolicy { get; set; } = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' data:;";
        public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";
        public bool EnableHsts { get; set; } = true;
        public int HstsMaxAge { get; set; } = 31536000;
        public bool HstsIncludeSubdomains { get; set; } = true;
    }

    public class CacheSettings
    {
        public string Type { get; set; } = "Memory"; // Memory, Redis, Hybrid
        public string? RedisConnectionString { get; set; }
        public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan SlidingExpiration { get; set; } = TimeSpan.FromMinutes(10);
        public int MaxMemoryCacheSize { get; set; } = 100; // MB
        public Dictionary<string, TimeSpan> CacheProfiles { get; set; } = new();
        public bool EnableCompression { get; set; } = true;
        public string KeyPrefix { get; set; } = "cms:";
    }

    public class FileStorageSettings
    {
        public string Provider { get; set; } = "Local"; // Local, Azure, AWS, GCP
        public string BasePath { get; set; } = "wwwroot/uploads";
        public long MaxFileSize { get; set; } = 10 * 1024 * 1024; // 10MB
        public string[] AllowedExtensions { get; set; } = { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".doc", ".docx" };
        public string[] BlockedExtensions { get; set; } = { ".exe", ".bat", ".cmd", ".scr" };
        public bool EnableAntiVirus { get; set; } = false;
        public ImageProcessingSettings ImageProcessing { get; set; } = new();
        public CloudStorageSettings CloudStorage { get; set; } = new();
    }

    public class ImageProcessingSettings
    {
        public bool EnableThumbnails { get; set; } = true;
        public int[] ThumbnailSizes { get; set; } = { 150, 300, 600 };
        public int MaxImageWidth { get; set; } = 2048;
        public int MaxImageHeight { get; set; } = 2048;
        public int JpegQuality { get; set; } = 85;
        public bool PreserveExif { get; set; } = false;
    }

    public class CloudStorageSettings
    {
        public string? AzureConnectionString { get; set; }
        public string? AzureContainerName { get; set; }
        public string? AwsAccessKey { get; set; }
        public string? AwsSecretKey { get; set; }
        public string? AwsBucketName { get; set; }
        public string? AwsRegion { get; set; }
    }

    public class EmailSettings
    {
        public string Provider { get; set; } = "SMTP"; // SMTP, SendGrid, AWS, Azure
        public SmtpSettings Smtp { get; set; } = new();
        public string? SendGridApiKey { get; set; }
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = string.Empty;
        public string[] AdminEmails { get; set; } = Array.Empty<string>();
        public TemplateSettings Templates { get; set; } = new();
    }

    public class SmtpSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class TemplateSettings
    {
        public string BasePath { get; set; } = "EmailTemplates";
        public string DefaultLocale { get; set; } = "en";
        public Dictionary<string, string> Templates { get; set; } = new();
    }

    public class LoggingSettings
    {
        public string MinimumLevel { get; set; } = "Information";
        public bool EnableStructuredLogging { get; set; } = true;
        public bool EnableFileLogging { get; set; } = true;
        public string LogFilePath { get; set; } = "Logs/cms-.log";
        public int MaxLogFileSize { get; set; } = 100; // MB
        public int MaxLogFiles { get; set; } = 10;
        public SeqSettings? Seq { get; set; }
        public ElasticsearchSettings? Elasticsearch { get; set; }
        public Dictionary<string, string> LogLevels { get; set; } = new();
    }

    public class SeqSettings
    {
        public string ServerUrl { get; set; } = string.Empty;
        public string? ApiKey { get; set; }
    }

    public class ElasticsearchSettings
    {
        public string Url { get; set; } = string.Empty;
        public string IndexName { get; set; } = "cms-logs";
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public class MonitoringSettings
    {
        public bool EnableHealthChecks { get; set; } = true;
        public bool EnableMetrics { get; set; } = true;
        public bool EnableTracing { get; set; } = true;
        public ApplicationInsightsSettings? ApplicationInsights { get; set; }
        public PrometheusSettings? Prometheus { get; set; }
        public int HealthCheckTimeoutSeconds { get; set; } = 10;
        public string[] CriticalHealthChecks { get; set; } = { "database", "cache" };
    }

    public class ApplicationInsightsSettings
    {
        public string InstrumentationKey { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public bool EnableDependencyTracking { get; set; } = true;
        public bool EnablePerformanceCounters { get; set; } = true;
    }

    public class PrometheusSettings
    {
        public bool Enabled { get; set; } = false;
        public string Endpoint { get; set; } = "/metrics";
        public string[] MetricPrefixes { get; set; } = { "cms_" };
    }

    public class TenantSettings
    {
        public string DefaultTenant { get; set; } = "demo";
        public bool EnableTenantIsolation { get; set; } = true;
        public bool EnableCrossTenantAccess { get; set; } = false;
        public int MaxTenantsPerInstance { get; set; } = 100;
        public TenantResolutionSettings Resolution { get; set; } = new();
        public Dictionary<string, object> DefaultSettings { get; set; } = new();
    }

    public class TenantResolutionSettings
    {
        public string Strategy { get; set; } = "Subdomain"; // Subdomain, Header, Path, Domain
        public string HeaderName { get; set; } = "X-Tenant-Id";
        public string PathPrefix { get; set; } = "/tenant/";
        public bool EnableFallback { get; set; } = true;
        public string FallbackTenant { get; set; } = "demo";
    }
}