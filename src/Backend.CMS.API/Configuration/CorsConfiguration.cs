namespace Backend.CMS.API.Configuration
{
    public static class CorsConfiguration
    {
        public static void ConfigureCors(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("Production", builder =>
                {
                    builder
                        .WithOrigins(configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
                        .WithHeaders("Authorization", "Content-Type", "X-Tenant-Id")
                        .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                        .AllowCredentials();
                });
            });
        }
    }
}
