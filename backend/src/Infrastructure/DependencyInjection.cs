using System.Text;
using Backend.Application.Common.Interfaces;
using Backend.Infrastructure.Adapters;
using Backend.Infrastructure.Ai;
using Backend.Infrastructure.BackgroundServices;
using Backend.Infrastructure.Caching;
using Backend.Infrastructure.Data;
using Backend.Infrastructure.Data.Interceptors;
using Backend.Infrastructure.Identity;
using Backend.Infrastructure.Services;
using Backend.Infrastructure.Weather;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjection
{
    public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(Services.Database);
        Guard.Against.Null(connectionString, message: $"Connection string '{Services.Database}' not found.");

        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString);
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        builder.Services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        builder.Services.AddScoped<ApplicationDbContextInitialiser>();

        // BCrypt password hasher — must be registered BEFORE AddIdentityCore (Identity uses TryAdd*).
        builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, BCryptPasswordHasher>();

        // Identity — user management only, no built-in API endpoints
        builder.Services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        // JWT settings
        var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT settings ('Jwt') are not configured.");
        builder.Services.AddSingleton(jwtSettings);

        // JWT bearer authentication
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                    ClockSkew = TimeSpan.Zero,
                    // Prevent .NET from remapping "sub" → ClaimTypes.NameIdentifier
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                };

                // Allow JWT via query param for SSE endpoints (browser EventSource can't set headers).
                // Also used by debug.html's SSE inspector.
                options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        if (ctx.Request.Path.StartsWithSegments("/api/v1/sse") &&
                            ctx.Request.Query.TryGetValue("access_token", out var token) &&
                            !string.IsNullOrWhiteSpace(token))
                        {
                            ctx.Token = token;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthorizationBuilder();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddTransient<IIdentityService, IdentityService>();
        builder.Services.AddScoped<IAuthService, AuthService>();

        // Infrastructure capabilities (technical writers consumed by Application handlers)
        builder.Services.AddScoped<INotificationService, NotificationService>();
        builder.Services.AddScoped<IAuditService, AuditService>();

        // Push notifications via SSE (no external service required)
        builder.Services.AddScoped<IPushNotificationService, LocalPushNotificationService>();
        builder.Services.AddScoped<VoiceActionDeduplicationService>();
        builder.Services.AddScoped<IVoiceActionService, VoiceActionService>();
        builder.Services.AddScoped<IProactiveContextService, ProactiveContextService>();
        builder.Services.AddScoped<IFamilyItemService, FamilyItemService>();
        builder.Services.AddScoped<IAIMemoryService, AIMemoryService>();
        builder.Services.AddScoped<IChatSynchronizationService, ChatSynchronizationService>();
        builder.Services.AddScoped<IHealthMonitoringService, HealthMonitoringService>();
        builder.Services.AddScoped<IHealthRiskScoringService, HealthRiskScoringService>();
        builder.Services.AddScoped<IPredictiveHealthRiskService, PredictiveHealthRiskService>();
        builder.Services.AddScoped<IFollowUpIntelligenceService, FollowUpIntelligenceService>();
        builder.Services.AddScoped<IAiPriorityEngine, AiPriorityEngine>();
        builder.Services.AddScoped<IAiDecisionEngine, AiDecisionEngine>();

        // SSE: singleton so connections survive request lifetimes
        builder.Services.AddSingleton<ISseConnectionManager, SseConnectionManager>();

        // AI Test Mode — singleton state + always-on worker (worker self-gates on IsEnabled)
        builder.Services.AddSingleton<AiTestModeService>();
        builder.Services.AddHostedService<AiTestModeWorker>();

        // Voice action in-memory queue: singleton channel shared across scopes
        builder.Services.AddSingleton<IVoiceActionQueue, VoiceActionQueue>();

        // AI client: vLLM (OpenAI-compatible) when configured, null client otherwise.
        builder.Services.Configure<LLMOptions>(
            builder.Configuration.GetSection(LLMOptions.SectionName));

        var llm = builder.Configuration.GetSection(LLMOptions.SectionName).Get<LLMOptions>();
        var llmConfigured = !string.IsNullOrWhiteSpace(llm?.BaseUrl)
            && !string.IsNullOrWhiteSpace(llm?.Token)
            && !string.IsNullOrWhiteSpace(llm?.Model);

        if (llmConfigured)
        {
            builder.Services.AddHttpClient<IAiClient, LLMClient>();
            builder.Services.AddHttpClient<IChildProfileExtractor, GemmaChildProfileExtractor>();
        }
        else
        {
            builder.Services.AddSingleton<IAiClient, NullAiClient>();
            builder.Services.AddSingleton<IChildProfileExtractor, RuleBasedChildProfileExtractor>();
        }

        // Weather provider: Open-Meteo (no API key) by default. Override via Weather:Provider.
        //   "Stub"          -> deterministic in-memory provider (used by tests)
        //   "OpenMeteo"     -> https://open-meteo.com (default in production)
        //   "OpenWeatherMap"-> requires Weather:ApiKey
        var weatherProvider = builder.Configuration["Weather:Provider"] ?? "OpenMeteo";
        if (string.Equals(weatherProvider, "OpenWeatherMap", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddScoped<IWeatherProvider, OpenWeatherMapProvider>();
        }
        else if (string.Equals(weatherProvider, "Stub", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddScoped<IWeatherProvider, StubWeatherProvider>();
        }
        else
        {
            builder.Services.AddScoped<IWeatherProvider, OpenMeteoProvider>();
        }

        // Adapters — WeatherAdapter is the real impl; CachedWeatherAdapter wraps it as the public IWeatherAdapter.
        builder.Services.AddScoped<WeatherAdapter>();
        builder.Services.AddScoped<IWeatherAdapter, CachedWeatherAdapter>();
        builder.Services.AddScoped<IIntentParser, IntentParser>();
        builder.Services.AddHttpClient("weather");

        // Distributed cache: Redis when configured, in-memory fallback otherwise.
        var redisConnectionString = builder.Configuration.GetConnectionString(Services.Redis);

        var healthChecks = builder.Services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>("database", tags: ["ready"]);

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var options = ConfigurationOptions.Parse(redisConnectionString);
                options.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(options);
            });

            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "carenestai:";
            });

            healthChecks.AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);
        }
        else
        {
            builder.Services.AddDistributedMemoryCache();
        }

        // Background workers — bind options + register hosted services. Tests disable via config.
        builder.Services.Configure<BackgroundWorkerOptions>(
            builder.Configuration.GetSection(BackgroundWorkerOptions.SectionName));

        var workerOptions = builder.Configuration
            .GetSection(BackgroundWorkerOptions.SectionName)
            .Get<BackgroundWorkerOptions>() ?? new BackgroundWorkerOptions();

        if (workerOptions.Enabled)
        {
            builder.Services.AddHostedService<WeatherSyncWorker>();
            builder.Services.AddHostedService<RoutineCalendarSyncWorker>();
            builder.Services.AddHostedService<ProactiveAnalysisWorker>();
            builder.Services.AddHostedService<NotificationRetryWorker>();
            builder.Services.AddHostedService<UpcomingEventRecommendationWorker>();
            builder.Services.AddHostedService<MedicationSchedulerService>();
            builder.Services.AddHostedService<NotificationDispatcherService>();
            builder.Services.AddHostedService<EscalationEngineService>();
            builder.Services.AddHostedService<MorningBriefingService>();
            // ProactiveVoiceWorker removed — AQI/weather alerts now handled by AiDecisionEngine via ContinuousContextAnalysisWorker
            builder.Services.AddHostedService<LeaveHomeChecklistWorker>();
            builder.Services.AddHostedService<HealthAdvisoryWorker>();
            builder.Services.AddHostedService<ScheduledReminderWorker>();
            builder.Services.AddHostedService<FollowUpDispatchWorker>();
            builder.Services.AddHostedService<ContinuousContextAnalysisWorker>();
            builder.Services.AddHostedService<EventReminderDispatchWorker>();
        }
    }
}
