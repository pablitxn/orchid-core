using System.Net.Http.Headers;
using System.Text;
using Application.Interfaces;
using Core.Application.Interfaces;
using Google.Cloud.Storage.V1;
using Infrastructure.Ai.AgentFramework;
using Infrastructure.Ai.AgentFramework.Services;
using Infrastructure.Auth;
using Infrastructure.Cache;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Providers;
using Infrastructure.Storage;
using Infrastructure.Telemetry.Ai.Langfuse;
using Infrastructure.Telemetry.Ai.NoOpTelemetryClient;
using Infrastructure.VectorStore;
using Infrastructure.Transformers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using ActionCostRepository = Infrastructure.Persistence.ActionCostRepository;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static Task<IServiceCollection> AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null
    )
    {
        // ─── 1. Validate configuration ─────────────────────────────────────────────
        // Chat completion port is registered in AgentFrameworkServiceCollectionExtensions

        var connectionString = configuration.GetConnectionString("DefaultConnection")!;

        #region Database Configuration

        // ─── DbContext with pgvector support ─────────────────────────────────────────
        // 1. Register your DbContext with pgvector support
        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.UseVector() // enable EF Core pgvector mapping
            );

            // Enable sensitive data logging and detailed errors ONLY in Development
            // Security: This prevents leaking sensitive data (like SQL parameters) in production logs
            var env = environment ?? serviceProvider.GetService<IHostEnvironment>();
            if (env?.IsDevelopment() == true)
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        }, ServiceLifetime.Scoped);

        #endregion

        #region Repository Registration

        // ─── Repositories ─────────────────────────────────────────────────────────────
        // 2. Register your repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<ISubscriptionPlanRepository, SubscriptionPlanRepository>();
        services.AddScoped<IActionCostRepository, ActionCostRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IAgentRatingRepository, AgentRatingRepository>();
        services.AddScoped<IPersonalityTemplateRepository, PersonalityTemplateRepository>();
        services.AddScoped<IPluginRepository, PluginRepository>();
        services.AddScoped<IUserPluginRepository, UserPluginRepository>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IKnowledgeBaseFileRepository, KnowledgeBaseFileRepository>();
        services.AddScoped<IMediaCenterAssetRepository, MediaCenterAssetRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
        services.AddScoped<ICreditConsumptionRepository, CreditConsumptionRepository>();
        services.AddScoped<IMessageCostRepository, MessageCostRepository>();
        services.AddScoped<IUserBillingPreferenceRepository, UserBillingPreferenceRepository>();
        services.AddScoped<ILoginHistoryRepository, LoginHistoryRepository>();
        services.AddScoped<IUserWorkflowRepository, UserWorkflowRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IMessageTransformer, LegacyToV2Transformer>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        // services.AddScoped<IAudioJobRepository, AudioJobRepository>();
        
        // ─── Credit and Notification Repositories ────────────────────────────────────
        services.AddScoped<ICostConfigurationRepository, CostConfigurationRepository>();
        services.AddScoped<IUserCreditLimitRepository, UserCreditLimitRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IFeatureFlagRepository, FeatureFlagRepository>();
        services.AddScoped<IServiceAccountRepository, ServiceAccountRepository>();

        // ─── Unit of Work pattern ────────────────────────────────────────────────────
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        // ...

        #endregion

        #region Credit Tracking Services

        // ─── Credit tracking and validation service ──────────────────────────────────
        services.AddScoped<ICreditTrackingService, Services.CreditTrackingService>();
        services.AddScoped<ICostRegistry, Services.CostRegistryService>();
        services.AddScoped<ICreditLimitService, Services.CreditLimitService>();
        services.AddScoped<INotificationService, Services.NotificationService>();
        services.AddScoped<ICreditValidationService, Services.CreditValidationService>();
        services.AddScoped<IAuditService, Services.AuditService>();

        #endregion

        #region AI & Embeddings

        // ─── Embedding generator: OpenAI embeddings ───────────────────────────────────
        services.AddSingleton<IEmbeddingGeneratorPort, OpenAIEmbeddingGenerator>();

        #endregion

        #region Storage Services

        // ─── File storage provider selection ─────────────────────────────────────────
        // Default to "memory" for cloud deployment (no local filesystem access)
        // Use "local" only for development, "gcp" for production with GCS bucket
        services.AddTransient<IFileStorageService>(provider =>
        {
            var env = provider.GetRequiredService<IHostEnvironment>();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var selected = configuration["FileStorage:Provider"]?.ToLowerInvariant() ?? "memory";

            IFileStorageService inner = selected switch
            {
                "gcp" =>
                    new GCPStorageService(
                        StorageClient.Create(),
                        configuration["FileStorage:Bucket"] ?? "default-bucket",
                        loggerFactory.CreateLogger<GCPStorageService>()),
                "local" =>
                    new LocalFileStorageService(
                        configuration["FileStorage:Path"] ?? Path.Combine(env.ContentRootPath, "data")),
                _ => new InMemoryFileStorageService()
            };

            var root = configuration["FileStorage:Root"] ?? "";
            return new TypedFileStorageService(inner, root);
        });


        #endregion

        #region Document Services

        // ─── Document text extraction ─────────────────────────────────────────────────
        services.AddTransient<IDocumentTextExtractor, DocumentTextExtractor>();

        #endregion

        #region Search & External Services

        // ─── Web search provider for semantic kernel plugins ─────────────────────────
        services.AddTransient<ISearchProvider, BingSearchProvider>();

        #endregion

        #region Plugin Discovery & Seeding

        // ─── Tool discovery service ─────────────────────────────────────────────────
        services.AddScoped<IPluginDiscoveryService, ToolDiscoveryService>();

        // ─── Plugin seeder (runs on startup) ─────────────────────────────────────────
        services.AddHostedService<Persistence.Seeders.PluginSeeder>();

        // ─── Database seeding (runs in background on startup) ───────────────────────
        services.AddSingleton<ISeedingStatusService, Persistence.Seeders.SeedingStatusService>();
        services.AddSingleton<Persistence.Seeders.SeedingHealthCheck>();
        services.AddHostedService<Persistence.Seeders.DatabaseSeedingService>();

        #endregion

        #region Caching Services

        // ─── Cache provider (memory or Redis) ────────────────────────────────────────
        var redisConfig = configuration["Redis:Configuration"];
        if (!string.IsNullOrWhiteSpace(redisConfig))
        {
            services.AddSingleton<RedisCacheService>(sp =>
                new RedisCacheService(redisConfig, sp.GetRequiredService<ILogger<RedisCacheService>>()));
            services.AddSingleton<ICacheStore>(sp => sp.GetRequiredService<RedisCacheService>());
            services.AddHostedService(sp => sp.GetRequiredService<RedisCacheService>());

            // Register Redis connection multiplexer for rate limiting and distributed locking
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
                StackExchange.Redis.ConnectionMultiplexer.Connect(redisConfig));
            services.AddSingleton<IRateLimitService, Services.RedisRateLimitService>();
            services.AddSingleton<IDistributedLockService, Services.RedisDistributedLockService>();
        }
        else
        {
            services.AddMemoryCache();
            services.AddSingleton<ICacheStore, InMemoryCacheService>();

            // Use in-memory rate limiting when Redis is not available
            services.AddSingleton<IRateLimitService, Services.InMemoryRateLimitService>();

            // Use in-memory distributed lock for single-instance development
            services.AddSingleton<IDistributedLockService, Services.InMemoryDistributedLockService>();
        }

        #endregion

        #region Vector Store Configuration

        // ─── Npgsql data-source with pgvector support ────────────────────────────────
        // 6. Npgsql data-source with pgvector support
        // Skip vector initialization if configured or targeting a test database
        var skipVectorInit = configuration.GetValue<bool>("SkipVectorInitialization")
                             || (connectionString != null!
                                 && connectionString.IndexOf("_test", StringComparison.OrdinalIgnoreCase) >= 0);
        if (!skipVectorInit)
        {
            try
            {
                var dsBuilder = new NpgsqlDataSourceBuilder(connectionString);
                dsBuilder.UseVector(); // registers pgvector handler
                var dataSource = dsBuilder.Build();
                services.AddSingleton(dataSource);
                services.AddScoped<IVectorStorePort, PgVectorStoreAdapter>();
            }
            catch (Exception ex)
            {
                // Consider using a logger instead of console
                Console.WriteLine($"Failed to initialize pgvector: {ex.Message}");
                throw new InvalidOperationException(
                    "Failed to initialize pgvector extension. Ensure PostgreSQL is properly configured with pgvector.",
                    ex
                );
            }
        }

        #endregion

        #region Background Tasks

        // ─── Background tasks ─────────────────────────────────────────────────────────
        //services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
        //services.AddHostedService<QueuedHostedService>();

        #endregion

        #region File Storage (Legacy)

        // ─── File storage ─────────────────────────────────────────────────────────────
        // We'll read a local path from config for dev environment
        //var localStoragePath = configuration.GetValue<string>("LocalStorage:Path")
        //?? "LocalAudioFiles";
        //services.AddTransient<IFileStorageService>(provider =>
        //new LocalFileStorageService(localStoragePath)
        //);

        #endregion

        #region Authentication Services

        // ─── Password hasher, validator and JWT token service for authentication ─────
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IPasswordValidator, PasswordValidator>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        // ─── JWT blacklist service for token invalidation on logout ─────────────────
        services.AddSingleton<IJwtBlacklistService, Services.RedisJwtBlacklistService>();

        #endregion

        #region Utility Services

        // ─── Utility services ─────────────────────────────────────────────────────────
        services.AddSingleton<ITokenCounter, Services.SimpleTokenCounter>();

        #endregion

        return Task.FromResult(services);
    }

    /// <summary>
    ///     Registers Langfuse telemetry: HTTP client and MediatR pipeline behavior.
    /// </summary>
    public static IServiceCollection AddLangfuseTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        #region Langfuse Configuration Check

        // ─── Register Langfuse settings ──────────────────────────────────────────────
        services.Configure<LangfuseSettings>(configuration.GetSection("Langfuse"));

        // ─── Check Langfuse configuration ────────────────────────────────────────────
        var publicKey = configuration["Langfuse:PublicKey"];
        var secretKey = configuration["Langfuse:SecretKey"];
        var baseUrl = configuration["Langfuse:BaseUrl"];
        if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(secretKey) ||
            string.IsNullOrWhiteSpace(baseUrl))
        {
            // Langfuse not configured; register a no-op telemetry client so behaviors can resolve ITelemetryClient
            if (services.All(sd => sd.ServiceType != typeof(ITelemetryClient)))
            {
                services.AddSingleton<ITelemetryClient, NoOpTelemetryClient>();
            }

            return services;
        }

        #endregion

        #region Langfuse HTTP Client

        // ─── Configure Langfuse HTTP client ──────────────────────────────────────────
        var basicAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{publicKey}:{secretKey}"));
        services.AddHttpClient<ITelemetryClient, LangfuseClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", basicAuth);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        });

        #endregion

        #region Langfuse Health Check

        // ─── Register Langfuse health check ──────────────────────────────────────────
        services.AddHttpClient<LangfuseHealthCheck>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(5); // Short timeout for health checks
        });

        #endregion

        #region Telemetry Pipeline Behaviors

        // ─── Register telemetry pipeline behaviors ───────────────────────────────────
        services.AddTransient(typeof(IPipelineBehavior<,>),
            typeof(LangfuseTelemetryBehavior<,>));

        #endregion

        return services;
    }

    /// <summary>
    ///     Adds Langfuse health check to the existing health checks configuration.
    /// </summary>
    public static IHealthChecksBuilder AddLangfuseHealthCheck(
        this IHealthChecksBuilder builder,
        IConfiguration configuration)
    {
        var baseUrl = configuration["Langfuse:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            builder.AddTypeActivatedCheck<LangfuseHealthCheck>(
                "langfuse",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
                tags: ["telemetry", "langfuse", "external"]);
        }

        return builder;
    }
}