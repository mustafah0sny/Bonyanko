using BonyankopAPI.Interfaces;
using BonyankopAPI.Services;
using BonyankopAPI.Repositories;

namespace BonyankopAPI.Extensions
{
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Register repositories
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IProviderProfileRepository, ProviderProfileRepository>();
            services.AddScoped<IPortfolioItemRepository, PortfolioItemRepository>();
            services.AddScoped<IDiagnosticRepository, DiagnosticRepository>();
            services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();
            services.AddScoped<IQuoteRepository, QuoteRepository>();
            services.AddScoped<IProjectRepository, ProjectRepository>();
            services.AddScoped<IRatingRepository, RatingRepository>();
            services.AddScoped<IAuditLogRepository, AuditLogRepository>();
            services.AddScoped<ISystemSettingsRepository, SystemSettingsRepository>();

            // Register custom services
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<IAiDiagnosticService, AiDiagnosticService>();

            // CrackVision AI structural-damage-detection model
            services.Configure<CrackVisionOptions>(
                configuration.GetSection(CrackVisionOptions.SectionName));

            var crackVisionOptions = configuration
                .GetSection(CrackVisionOptions.SectionName)
                .Get<CrackVisionOptions>() ?? new CrackVisionOptions();

            services.AddHttpClient<ICrackVisionClient, CrackVisionClient>(client =>
            {
                client.BaseAddress = new Uri(crackVisionOptions.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(crackVisionOptions.TimeoutSeconds);
            });

            return services;
        }
    }
}
