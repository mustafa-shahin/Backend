using Backend.CMS.API.Filters;
using Backend.CMS.API.Middleware;
using Backend.CMS.Application.Common.Behaviors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Backend.CMS.API.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationBehaviors(this IServiceCollection services)
        {
            // Add MediatR pipeline behaviors
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ErrorHandlingBehavior<,>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditingBehavior<,>));

            return services;
        }

        public static IServiceCollection AddApiFilters(this IServiceCollection services)
        {
            services.AddScoped<ApiExceptionFilter>();

            return services;
        }

        public static IServiceCollection AddCustomControllers(this IServiceCollection services)
        {
            services.AddControllers(options =>
            {
                options.Filters.Add<ApiExceptionFilter>();
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                // Customize model validation error responses
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(e => e.Value?.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? []
                        );

                    var errorResponse = new ApiErrorResponse
                    {
                        Message = "Validation failed",
                        StatusCode = 400,
                        Details = "One or more validation errors occurred",
                        ValidationErrors = errors,
                        TraceId = context.HttpContext.TraceIdentifier,
                        Timestamp = DateTime.UtcNow,
                        Path = context.HttpContext.Request.Path,
                        Method = context.HttpContext.Request.Method
                    };

                    return new BadRequestObjectResult(errorResponse);
                };
            });

            return services;
        }
    }
}