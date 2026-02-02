using System;
using Core.Domain.Entities;
using Domain.Enums;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IConfiguration? configuration = null)
    : DbContext(options)
{
    private readonly string? _conn = configuration?.GetConnectionString("DefaultConnection");
    private readonly IConfiguration? _configuration = configuration;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var cs = _conn
                     ?? Environment.GetEnvironmentVariable("DB_CONN");
            if (!string.IsNullOrEmpty(cs))
            {
                optionsBuilder.UseNpgsql(
                    cs,
                    o => o.UseVector());
            }
        }
    }

    public DbSet<UserEntity> Users { get; set; }
    public DbSet<ProjectEntity> Projects { get; set; }
    public DbSet<AudioFileEntity> AudioFiles { get; set; }
    public DbSet<DocumentEntity> Documents { get; set; }
    public DbSet<SheetChunkEntity> SheetChunks { get; set; }
    public DbSet<RoleEntity> Roles { get; set; }
    public DbSet<UserRoleEntity> UserRoles { get; set; }
    public DbSet<SubscriptionEntity> Subscriptions { get; set; }
    public DbSet<AgentEntity> Agents { get; set; }
    public DbSet<PersonalityTemplateEntity> PersonalityTemplates { get; set; }
    public DbSet<PluginEntity> Plugins { get; set; }
    public DbSet<AgentPluginEntity> AgentPlugins { get; set; }
    public DbSet<UserPluginEntity> UserPlugins { get; set; }
    public DbSet<WorkflowEntity> Workflows { get; set; }
    public DbSet<UserWorkflowEntity> UserWorkflows { get; set; }
    public DbSet<KnowledgeBaseFileEntity> KnowledgeBaseFiles { get; set; }
    public DbSet<MediaCenterAssetEntity> MediaCenterAssets { get; set; }
    public DbSet<TeamEntity> Teams { get; set; }
    public DbSet<TeamAgentEntity> TeamAgents { get; set; }
    public DbSet<ChatSessionEntity> ChatSessions { get; set; }
    public DbSet<SubscriptionPlanEntity> SubscriptionPlans { get; set; }
    public DbSet<ActionCostEntity> ActionCosts { get; set; }
    public DbSet<CreditConsumptionEntity> CreditConsumptions { get; set; }
    public DbSet<MessageCostEntity> MessageCosts { get; set; }
    public DbSet<LoginHistoryEntity> LoginHistories { get; set; }
    public DbSet<UserBillingPreferenceEntity> UserBillingPreferences { get; set; }
    public DbSet<CostConfigurationEntity> CostConfigurations { get; set; }
    public DbSet<UserCreditLimitEntity> UserCreditLimits { get; set; }
    public DbSet<NotificationEntity> Notifications { get; set; }
    public DbSet<AuditLogEntity> AuditLogs { get; set; }
    public DbSet<RefreshTokenEntity> RefreshTokens { get; set; }
    public DbSet<AgentRatingEntity> AgentRatings { get; set; }
    public DbSet<FeatureFlagEntity> FeatureFlags { get; set; }
    public DbSet<ServiceAccountEntity> ServiceAccounts { get; set; }
    public DbSet<MessageV2Entity> MessagesV2 { get; set; }
    public DbSet<MessageArtifactEntity> MessageArtifacts { get; set; }
    public DbSet<ChartArtifactEntity> ChartArtifacts { get; set; }
    public DbSet<TableArtifactEntity> TableArtifacts { get; set; }
    public DbSet<MermaidArtifactEntity> MermaidArtifacts { get; set; }
    public DbSet<CanvasTextArtifactEntity> CanvasTextArtifacts { get; set; }
    public DbSet<ImageArtifactEntity> ImageArtifacts { get; set; }
    public DbSet<VideoArtifactEntity> VideoArtifacts { get; set; }
    public DbSet<ThreeJsArtifactEntity> ThreeJsArtifacts { get; set; }
    public DbSet<CalendarArtifactEntity> CalendarArtifacts { get; set; }
    public DbSet<ReactionEntity> Reactions { get; set; }
    public DbSet<StripeWebhookEventEntity> StripeWebhookEvents { get; set; }
    public DbSet<MatrixUserLinkEntity> MatrixUserLinks { get; set; }
    public DbSet<MatrixLinkCodeEntity> MatrixLinkCodes { get; set; }
    public DbSet<CreditPackageEntity> CreditPackages { get; set; }
    public DbSet<CreditPurchaseEntity> CreditPurchases { get; set; }
    public DbSet<WorkspaceEntity> Workspaces { get; set; }
    public DbSet<WorkspaceMemberEntity> WorkspaceMembers { get; set; }
    public DbSet<WorkspaceSubscriptionEntity> WorkspaceSubscriptions { get; set; }
    public DbSet<PluginReviewEntity> PluginReviews { get; set; }
    public DbSet<PluginReviewFlagEntity> PluginReviewFlags { get; set; }
    public DbSet<PluginReviewHelpfulVoteEntity> PluginReviewHelpfulVotes { get; set; }
    public DbSet<UserSocialIdentityEntity> UserSocialIdentities { get; set; }
    public DbSet<PublisherEntity> Publishers { get; set; }
    public DbSet<PluginSubmissionEntity> PluginSubmissions { get; set; }
    public DbSet<PluginVersionEntity> PluginVersions { get; set; }
    public DbSet<PluginUsageEventEntity> PluginUsageEvents { get; set; }
    public DbSet<NotificationPreferencesEntity> NotificationPreferences { get; set; }
    public DbSet<ScheduledNotificationEntity> ScheduledNotifications { get; set; }
    public DbSet<UserCreditsEntity> UserCredits { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Only configure pgvector extension for PostgreSQL provider
        if (Database.IsNpgsql())
        {
            modelBuilder.HasPostgresExtension("vector");
        }
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserEntity>()
            .HasIndex(u => u.Email)
            .IsUnique();
        modelBuilder.Entity<UserEntity>()
            .Property(u => u.PasswordResetToken);
        modelBuilder.Entity<UserEntity>()
            .Property(u => u.PasswordResetExpiry)
            .HasColumnType("timestamp with time zone");

        // Auth0 user fields configuration
        modelBuilder.Entity<UserEntity>()
            .Property(u => u.Auth0UserId)
            .HasMaxLength(255);
        modelBuilder.Entity<UserEntity>()
            .Property(u => u.Auth0Connection)
            .HasMaxLength(100);
        modelBuilder.Entity<UserEntity>()
            .Property(u => u.Auth0LastSyncedAt)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<UserEntity>()
            .HasIndex(u => u.Auth0UserId)
            .IsUnique()
            .HasFilter("\"Auth0UserId\" IS NOT NULL");

        // Model preference for user-level defaults
        modelBuilder.Entity<UserEntity>()
            .Property(u => u.ModelPreference)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Auth0 migration status fields
        modelBuilder.Entity<UserEntity>()
            .Property(u => u.Auth0MigrationStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(Auth0MigrationStatus.NotMigrated);
        modelBuilder.Entity<UserEntity>()
            .Property(u => u.Auth0MigratedAt)
            .HasColumnType("timestamp with time zone");
        modelBuilder.Entity<UserEntity>()
            .Property(u => u.Auth0MigrationError)
            .HasMaxLength(1000);
        modelBuilder.Entity<UserEntity>()
            .HasIndex(u => u.Auth0MigrationStatus);

        // AllowAutoDegradation preference for automatic model fallback
        modelBuilder.Entity<UserEntity>()
            .Property(u => u.AllowAutoDegradation)
            .HasDefaultValue(true);

        modelBuilder.Entity<ProjectEntity>()
            .HasOne(p => p.AudioFile)
            .WithOne(a => a.Project)
            .HasForeignKey<AudioFileEntity>(a => a.ProjectId);

        // Map uploaded documents
        modelBuilder.Entity<DocumentEntity>(b =>
        {
            b.ToTable("Documents");
            b.HasKey(d => d.Id);
            b.Property(d => d.SessionId).IsRequired();
            b.Property(d => d.DocumentPath).IsRequired();
            b.Property(d => d.FileName).IsRequired();
            b.Property(d => d.ContentType).IsRequired();
            b.Property(d => d.FileSize).IsRequired();
            b.Property(d => d.Enum)
                .HasConversion<string>()
                .HasDefaultValue(DocumentEnum.Attachment)
                .IsRequired();
            b.Property(d => d.IsIndexed).IsRequired();
            b.Property(d => d.ChunkCount).IsRequired();
            b.Property(d => d.CreatedAt).HasColumnType("timestamp with time zone");
            
            // Embedding vector of the document - only configure for PostgreSQL
            if (Database.IsNpgsql())
            {
                b.Property(d => d.Embedding)
                    .HasColumnType("vector(1536)");
            }
            else
            {
                b.Ignore(d => d.Embedding);
            }
        });
        // Map sheet chunks
        modelBuilder.Entity<SheetChunkEntity>(b =>
        {
            b.ToTable("SheetChunks");
            b.HasKey(c => c.Id);
            b.Property(c => c.Id);
            b.Property(c => c.DocumentId);
            b.Property(c => c.SheetName).IsRequired();
            b.Property(c => c.StartRow).IsRequired();
            b.Property(c => c.EndRow).IsRequired();
            b.Property(c => c.Text).IsRequired();
            
            // Embedding vector - only configure for PostgreSQL
            if (Database.IsNpgsql())
            {
                b.Property(c => c.Embedding).HasColumnType("vector(1536)");
            }
            else
            {
                b.Ignore(c => c.Embedding);
            }
            b.HasOne(c => c.Document)
                .WithMany(d => d.SheetChunks)
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RoleEntity>(b =>
        {
            b.ToTable("Roles");
            b.HasKey(r => r.Id);
            b.Property(r => r.Name).IsRequired();
            b.HasIndex(r => r.Name).IsUnique();
        });

        modelBuilder.Entity<UserRoleEntity>(b =>
        {
            b.ToTable("UserRoles");
            b.HasKey(ur => new { ur.UserId, ur.RoleId });
            b.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId);
            b.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId);
        });

        modelBuilder.Entity<SubscriptionEntity>(b =>
        {
            b.ToTable("Subscriptions");
            b.HasKey(s => s.Id);
            b.Property(s => s.UserId).IsRequired();
            b.Property(s => s.Credits).IsRequired();
            b.Property(s => s.SubscriptionPlanId);
            b.Property(s => s.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(s => s.UpdatedAt).HasColumnType("timestamp with time zone");
            b.Property(s => s.ExpiresAt).HasColumnType("timestamp with time zone");
            b.Property(s => s.AutoRenew).HasDefaultValue(true);
            // Configure Version as concurrency token
            b.Property(s => s.Version)
                .IsConcurrencyToken()
                .HasDefaultValue(0);
            // Stripe integration fields
            b.Property(s => s.StripeCustomerId).HasMaxLength(100);
            b.Property(s => s.StripeSubscriptionId).HasMaxLength(100);
            b.Property(s => s.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(SubscriptionStatus.Incomplete);
            b.Property(s => s.CurrentPeriodStart).HasColumnType("timestamp with time zone");
            b.Property(s => s.CurrentPeriodEnd).HasColumnType("timestamp with time zone");
            b.Property(s => s.CancelAtPeriodEnd).HasDefaultValue(false);
            b.Property(s => s.CanceledAt).HasColumnType("timestamp with time zone");
            b.Property(s => s.TrialEndsAt).HasColumnType("timestamp with time zone");
            b.HasIndex(s => s.StripeCustomerId);
            b.HasIndex(s => s.StripeSubscriptionId);
            b.HasOne(s => s.User)
                .WithOne()
                .HasForeignKey<SubscriptionEntity>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(s => s.SubscriptionPlan)
                .WithMany()
                .HasForeignKey(s => s.SubscriptionPlanId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Map UserCredits entity (decoupled from Subscription)
        modelBuilder.Entity<UserCreditsEntity>(b =>
        {
            b.ToTable("UserCredits");
            b.HasKey(uc => uc.Id);
            b.Property(uc => uc.UserId).IsRequired();
            b.Property(uc => uc.AvailableCredits).IsRequired();
            b.Property(uc => uc.TotalCreditsGranted).IsRequired();
            b.Property(uc => uc.TotalCreditsConsumed).IsRequired();
            b.Property(uc => uc.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(uc => uc.UpdatedAt).HasColumnType("timestamp with time zone");
            b.Property(uc => uc.LastConsumedAt).HasColumnType("timestamp with time zone");
            // Configure Version as concurrency token
            b.Property(uc => uc.Version)
                .IsConcurrencyToken()
                .HasDefaultValue(0);
            // Unique index: one credit record per user
            b.HasIndex(uc => uc.UserId).IsUnique();
            b.HasOne(uc => uc.User)
                .WithOne()
                .HasForeignKey<UserCreditsEntity>(uc => uc.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Map Stripe webhook event entity
        modelBuilder.Entity<StripeWebhookEventEntity>(b =>
        {
            b.ToTable("StripeWebhookEvents");
            b.HasKey(e => e.Id);
            b.Property(e => e.StripeEventId).IsRequired().HasMaxLength(100);
            b.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            b.Property(e => e.Payload).IsRequired();
            b.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(WebhookEventStatus.Pending);
            b.Property(e => e.ErrorMessage).HasMaxLength(2000);
            b.Property(e => e.RetryCount).HasDefaultValue(0);
            b.Property(e => e.NextRetryAt).HasColumnType("timestamp with time zone");
            b.Property(e => e.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(e => e.ProcessedAt).HasColumnType("timestamp with time zone");
            b.HasIndex(e => e.StripeEventId).IsUnique();
            b.HasIndex(e => e.Status);
            b.HasIndex(e => e.CreatedAt);
            b.HasIndex(e => e.NextRetryAt);
        });

        // Map subscription plan entity with Stripe Price ID
        modelBuilder.Entity<SubscriptionPlanEntity>(b =>
        {
            b.ToTable("SubscriptionPlans");
            b.HasKey(p => p.Id);
            b.Property(p => p.PlanEnum)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();
            b.Property(p => p.Price).IsRequired();
            b.Property(p => p.Credits).IsRequired();
            b.Property(p => p.PaymentUnit)
                .HasConversion<string>()
                .HasMaxLength(20);
            b.Property(p => p.StripePriceId).HasMaxLength(100);
            b.Property(p => p.IsRecurring).HasDefaultValue(true);
            b.HasIndex(p => p.PlanEnum).IsUnique();
            b.HasIndex(p => p.StripePriceId);
        });
        modelBuilder.Entity<AgentEntity>(b =>
        {
            b.ToTable("Agents");
            b.HasKey(a => a.Id);
            b.Property(a => a.Name).IsRequired();
            b.Property(a => a.Description);
            b.Property(a => a.AvatarUrl);
            b.Property(a => a.Personality);
            b.Property(a => a.PersonalityTemplateId);
            b.Property(a => a.Language);
            b.Property(a => a.Model).HasMaxLength(100);
            b.Property(a => a.FallbackModel).HasMaxLength(100);
            b.Property(a => a.ModelPreference)
                .HasConversion<string>()
                .HasMaxLength(20);
            b.Property(a => a.PluginIds).HasColumnType("uuid[]");
            b.Property(a => a.UserId);
            b.Property(a => a.IsPublic).IsRequired().HasDefaultValue(false);
            b.Property(a => a.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(a => a.UpdatedAt).HasColumnType("timestamp with time zone");
            
            // Soft delete fields
            b.Property(a => a.IsDeleted).IsRequired().HasDefaultValue(false);
            b.Property(a => a.DeletedAt).HasColumnType("timestamp with time zone");
            b.Property(a => a.IsInRecycleBin).IsRequired().HasDefaultValue(false);
            b.Property(a => a.RecycleBinExpiresAt).HasColumnType("timestamp with time zone");
            
            // Configure relationship with PersonalityTemplate
            b.HasOne(a => a.PersonalityTemplate)
                .WithMany(pt => pt.Agents)
                .HasForeignKey(a => a.PersonalityTemplateId)
                .OnDelete(DeleteBehavior.SetNull);
                
            // Configure relationship with User (owner)
            b.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);
                
            // Add indexes for better query performance
            b.HasIndex(a => a.UserId);
            b.HasIndex(a => a.IsPublic);
            b.HasIndex(a => new { a.IsDeleted, a.IsInRecycleBin });
        });
        
        modelBuilder.Entity<PersonalityTemplateEntity>(b =>
        {
            b.ToTable("PersonalityTemplates");
            b.HasKey(pt => pt.Id);
            b.Property(pt => pt.Name).IsRequired().HasMaxLength(100);
            b.Property(pt => pt.Description).IsRequired().HasMaxLength(500);
            b.Property(pt => pt.Prompt).IsRequired();
            b.Property(pt => pt.Category).IsRequired().HasMaxLength(50);
            b.Property(pt => pt.IsSystem).IsRequired();
            b.Property(pt => pt.DisplayOrder).IsRequired();
            b.Property(pt => pt.IsActive).IsRequired();
            b.Property(pt => pt.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(pt => pt.UpdatedAt).HasColumnType("timestamp with time zone");
            
            // Create index for faster queries
            b.HasIndex(pt => pt.IsActive);
            b.HasIndex(pt => pt.Category);
        });

        modelBuilder.Entity<PluginEntity>(b =>
        {
            b.ToTable("Plugins");
            b.HasKey(p => p.Id);
            b.Property(p => p.Name).IsRequired();
            b.Property(p => p.Description);
            b.Property(p => p.SourceUrl);
            b.Property(p => p.IsActive).IsRequired();

            // OpenAPI Plugin Support
            b.Property(p => p.PluginType).IsRequired().HasMaxLength(20).HasDefaultValue("builtin");
            b.Property(p => p.BaseUrl).HasMaxLength(2048);
            b.Property(p => p.OpenApiSpecUrl).HasMaxLength(500);
            b.Property(p => p.OpenApiSpecCache); // No max length - stores full JSON spec
            b.Property(p => p.SpecCachedAt).HasColumnType("timestamp with time zone");
            b.Property(p => p.TimeoutSeconds);

            b.Property(p => p.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(p => p.UpdatedAt).HasColumnType("timestamp with time zone");

            // Marketplace metadata
            b.Property(p => p.Author).HasMaxLength(200);
            b.Property(p => p.Category).HasMaxLength(100);
            b.Property(p => p.Icon).HasMaxLength(500);
            b.Property(p => p.Tags).HasMaxLength(500);
            b.Property(p => p.Rating).IsRequired().HasDefaultValue(0.0);
            b.Property(p => p.ReviewCount).IsRequired().HasDefaultValue(0);
            b.Property(p => p.InstallCount).IsRequired().HasDefaultValue(0);
            b.Property(p => p.IsFeatured).IsRequired().HasDefaultValue(false);
            b.Property(p => p.IsPublic).IsRequired().HasDefaultValue(false);
            b.Property(p => p.PublishedAt).HasColumnType("timestamp with time zone");
            b.Property(p => p.Version).HasMaxLength(50);
            b.Property(p => p.Compatibility).HasMaxLength(200);

            // Publisher association
            b.Property(p => p.PublisherId);
            b.HasOne(p => p.Publisher)
                .WithMany(pub => pub.Plugins)
                .HasForeignKey(p => p.PublisherId)
                .OnDelete(DeleteBehavior.SetNull);

            // Version management
            b.Property(p => p.LatestVersionId);
            b.Property(p => p.VersionCount).IsRequired().HasDefaultValue(0);

            // Indexes for filtering
            b.HasIndex(p => p.PluginType);
            b.HasIndex(p => p.Category);
            b.HasIndex(p => p.Rating);
            b.HasIndex(p => p.InstallCount);
            b.HasIndex(p => p.IsFeatured);
            b.HasIndex(p => p.IsPublic);
            b.HasIndex(p => p.PublisherId);
        });

        modelBuilder.Entity<WorkflowEntity>(b =>
        {
            b.ToTable("Workflows");
            b.HasKey(w => w.Id);
            b.Property(w => w.Name).IsRequired();
            b.Property(w => w.Description);
            b.Property(w => w.PriceCredits).IsRequired();
            b.Property(w => w.Author);
            b.Property(w => w.Category);
            b.Property(w => w.Steps).IsRequired();
            b.Property(w => w.EstimatedTime);
            b.Property(w => w.Rating).IsRequired();
            b.Property(w => w.Runs).IsRequired();
            b.Property(w => w.Icon);
            b.Property(w => w.Tags);
            b.Property(w => w.DetailedDescription);
            b.Property(w => w.Prerequisites);
            b.Property(w => w.InputRequirements);
            b.Property(w => w.OutputFormat);
            b.Property(w => w.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(w => w.UpdatedAt).HasColumnType("timestamp with time zone");
            
            // Add indexes for better query performance
            b.HasIndex(w => w.Category);
            b.HasIndex(w => w.Rating);
            b.HasIndex(w => w.Runs);
        });

        modelBuilder.Entity<UserWorkflowEntity>(b =>
        {
            b.ToTable("UserWorkflows");
            b.HasKey(uw => new { uw.UserId, uw.WorkflowId });
            b.Property(uw => uw.PurchasedAt).HasColumnType("timestamp with time zone");
            b.HasOne(uw => uw.User)
                .WithMany()
                .HasForeignKey(uw => uw.UserId);
            b.HasOne(uw => uw.Workflow)
                .WithMany()
                .HasForeignKey(uw => uw.WorkflowId);
        });

        modelBuilder.Entity<AgentPluginEntity>(b =>
        {
            b.ToTable("AgentPlugins");
            b.HasKey(ap => new { ap.AgentId, ap.PluginId });
            b.HasOne(ap => ap.Agent)
                .WithMany()
                .HasForeignKey(ap => ap.AgentId);
            b.HasOne(ap => ap.Plugin)
                .WithMany()
                .HasForeignKey(ap => ap.PluginId);
        });

        modelBuilder.Entity<UserPluginEntity>(b =>
        {
            b.ToTable("UserPlugins");
            b.HasKey(up => up.Id);
            b.Property(up => up.UserId).IsRequired();
            b.Property(up => up.PluginId).IsRequired();
            b.Property(up => up.PurchasedAt).HasColumnType("timestamp with time zone");
            b.Property(up => up.ExpiresAt).HasColumnType("timestamp with time zone");
            b.Property(up => up.IsActive).IsRequired();
            b.Property(up => up.Settings).HasColumnType("jsonb");

            // Version tracking fields
            b.Property(up => up.InstalledVersionId);
            b.Property(up => up.InstalledVersion).HasMaxLength(50);
            b.Property(up => up.LastUpdatedAt).HasColumnType("timestamp with time zone");
            b.Property(up => up.AutoUpdate).IsRequired().HasDefaultValue(false);
            b.Property(up => up.PinnedVersion).HasMaxLength(50);

            b.HasIndex(up => new { up.UserId, up.PluginId }).IsUnique();
            b.HasIndex(up => up.InstalledVersionId);

            b.HasOne(up => up.User)
                .WithMany()
                .HasForeignKey(up => up.UserId);
            b.HasOne(up => up.Plugin)
                .WithMany()
                .HasForeignKey(up => up.PluginId);
            b.HasOne(up => up.InstalledVersionEntity)
                .WithMany()
                .HasForeignKey(up => up.InstalledVersionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<KnowledgeBaseFileEntity>(b =>
        {
            b.ToTable("KnowledgeBaseFiles");
            b.HasKey(f => f.Id);
            b.Property(f => f.UserId).IsRequired();
            b.Property(f => f.Title).IsRequired();
            b.Property(f => f.Description);
            b.Property(f => f.Tags).HasColumnType("text[]");
            b.Property(f => f.MimeType).IsRequired();
            b.Property(f => f.FileUrl).IsRequired();
            b.Property(f => f.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(f => f.UpdatedAt).HasColumnType("timestamp with time zone");
            b.HasIndex(f => f.MimeType);
            b.HasIndex(f => f.Tags).HasMethod("gin");
            b.HasIndex(f => f.CreatedAt);
        });

        modelBuilder.Entity<MediaCenterAssetEntity>(b =>
        {
            b.ToTable("MediaCenterAssets");
            b.HasKey(a => a.Id);
            b.Property(a => a.MimeType).IsRequired();
            b.Property(a => a.Title).IsRequired();
            b.Property(a => a.Duration);
            b.Property(a => a.FileUrl).IsRequired();
            b.Property(a => a.CreatedAt).HasColumnType("timestamp with time zone");
            b.HasIndex(a => a.MimeType);
            b.HasIndex(a => a.CreatedAt);
            b.HasOne(a => a.KnowledgeBaseFile)
                .WithMany()
                .HasForeignKey(a => a.KnowledgeBaseFileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeamEntity>(b =>
        {
            b.ToTable("Teams");
            b.HasKey(t => t.Id);
            b.Property(t => t.Name).IsRequired();
            b.Property(t => t.Description);
            b.Property(t => t.Policy)
                .HasConversion<int>()
                .HasDefaultValue(TeamInteractionPolicy.Open);
            b.Property(t => t.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(t => t.UpdatedAt).HasColumnType("timestamp with time zone");
        });

        modelBuilder.Entity<TeamAgentEntity>(b =>
        {
            b.ToTable("TeamAgents");
            b.HasKey(ta => new { ta.TeamId, ta.AgentId });
            b.HasOne(ta => ta.Team)
                .WithMany(t => t.TeamAgents)
                .HasForeignKey(ta => ta.TeamId);
            b.HasOne(ta => ta.Agent)
                .WithMany()
                .HasForeignKey(ta => ta.AgentId);
            b.Property(ta => ta.Role).IsRequired();
            b.Property(ta => ta.Order);
        });

        modelBuilder.Entity<ChatSessionEntity>(b =>
        {
            b.ToTable("ChatSessions");
            b.HasKey(s => s.Id);
            b.Property(s => s.SessionId).IsRequired();
            b.Property(s => s.UserId).IsRequired();
            b.Property(s => s.AgentId);
            b.Property(s => s.TeamId);
            b.Property(s => s.InteractionType).IsRequired();
            b.Property(s => s.Title);
            b.Property(s => s.IsArchived).IsRequired();
            b.Property(s => s.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(s => s.UpdatedAt).HasColumnType("timestamp with time zone");
            b.HasIndex(s => new { s.UserId, s.IsArchived });
            b.HasOne(s => s.Agent)
                .WithMany()
                .HasForeignKey(s => s.AgentId);
            b.HasOne(s => s.Team)
                .WithMany()
                .HasForeignKey(s => s.TeamId);
        });
        // Map action cost entity and configure CreatedAt index
        modelBuilder.Entity<ActionCostEntity>(b =>
        {
            b.ToTable("ActionCosts");
            b.HasKey(c => c.Id);
            b.Property(c => c.ActionName)
                .HasColumnName("ActionType") // Map ActionName property to ActionType column
                .IsRequired();
            b.Property(c => c.Credits).IsRequired();
            b.Property(c => c.PaymentUnit).IsRequired();
            b.Property(c => c.CreatedAt).HasColumnType("timestamp with time zone");
            b.HasIndex(c => c.CreatedAt);
        });

        // Map credit consumption entity
        modelBuilder.Entity<CreditConsumptionEntity>(b =>
        {
            b.ToTable("CreditConsumptions");
            b.HasKey(c => c.Id);
            b.Property(c => c.UserId).IsRequired();
            b.Property(c => c.ConsumptionType).IsRequired();
            b.Property(c => c.ResourceId);
            b.Property(c => c.ResourceName).IsRequired();
            b.Property(c => c.CreditsConsumed).IsRequired();
            b.Property(c => c.Metadata);
            b.Property(c => c.ConsumedAt).HasColumnType("timestamp with time zone");
            b.Property(c => c.BalanceAfter).IsRequired();
            b.HasIndex(c => new { c.UserId, c.ConsumedAt });
            b.HasIndex(c => c.ConsumptionType);
            b.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId);
        });

        // Map message cost entity
        modelBuilder.Entity<MessageCostEntity>(b =>
        {
            b.ToTable("MessageCosts");
            b.HasKey(m => m.Id);
            b.Property(m => m.MessageId).IsRequired();
            b.Property(m => m.UserId).IsRequired();
            b.Property(m => m.BillingMethod).IsRequired();
            b.Property(m => m.TokensConsumed);
            b.Property(m => m.CostPerToken);
            b.Property(m => m.FixedRate);
            b.Property(m => m.TotalCredits).IsRequired();
            b.Property(m => m.HasPluginUsage).IsRequired();
            b.Property(m => m.HasWorkflowUsage).IsRequired();
            b.Property(m => m.AdditionalCredits).IsRequired();
            b.Property(m => m.CreatedAt).HasColumnType("timestamp with time zone");
            b.HasIndex(m => new { m.UserId, m.CreatedAt });
            b.HasIndex(m => m.MessageId).IsUnique();
            b.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId);
        });

        // Map user billing preference entity
        modelBuilder.Entity<UserBillingPreferenceEntity>(b =>
        {
            b.ToTable("UserBillingPreferences");
            b.HasKey(p => p.Id);
            b.Property(p => p.UserId).IsRequired();
            b.Property(p => p.MessageBillingMethod).IsRequired();
            b.Property(p => p.TokenRate).IsRequired();
            b.Property(p => p.FixedMessageRate).IsRequired();
            b.Property(p => p.LowCreditThreshold);
            b.Property(p => p.EnableLowCreditAlerts).IsRequired();
            b.Property(p => p.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(p => p.UpdatedAt).HasColumnType("timestamp with time zone");
            b.HasIndex(p => p.UserId).IsUnique();
            b.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId);
        });

        // Map credit consumption entity
        modelBuilder.Entity<CreditConsumptionEntity>(b =>
        {
            b.ToTable("CreditConsumptions");
            b.HasKey(c => c.Id);
            b.Property(c => c.UserId).IsRequired();
            b.Property(c => c.ConsumptionType).IsRequired().HasMaxLength(50);
            b.Property(c => c.ResourceId).IsRequired().HasMaxLength(100);
            b.Property(c => c.ResourceName).IsRequired().HasMaxLength(200);
            b.Property(c => c.CreditsConsumed).IsRequired();
            b.Property(c => c.BalanceAfter).IsRequired();
            b.Property(c => c.ConsumedAt).HasColumnType("timestamp with time zone");
            b.Property(c => c.Metadata).HasColumnType("jsonb");
            b.HasIndex(c => new { c.UserId, c.ConsumedAt });
            b.HasIndex(c => c.ConsumptionType);
            b.HasIndex(c => c.ResourceId);
            b.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId);
        });

        // Map login history entity
        modelBuilder.Entity<LoginHistoryEntity>(b =>
        {
            b.ToTable("LoginHistories");
            b.HasKey(l => l.Id);
            b.Property(l => l.UserId).IsRequired();
            b.Property(l => l.Timestamp).HasColumnType("timestamp with time zone");
            b.Property(l => l.IpAddress).IsRequired().HasMaxLength(45);
            b.Property(l => l.UserAgent).HasMaxLength(500);
            b.Property(l => l.Location).HasMaxLength(200);
            b.Property(l => l.Success).IsRequired();
            b.Property(l => l.FailureReason).HasMaxLength(500);
            b.HasIndex(l => new { l.UserId, l.Timestamp });
            b.HasIndex(l => l.Success);
            b.HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId);
        });

        // Map user credit limit entity
        modelBuilder.Entity<UserCreditLimitEntity>(b =>
        {
            b.ToTable("UserCreditLimits");
            b.HasKey(l => l.Id);
            b.Property(l => l.UserId).IsRequired();
            b.Property(l => l.LimitType).IsRequired().HasMaxLength(50);
            b.Property(l => l.ResourceType).HasMaxLength(100);
            b.Property(l => l.MaxCredits).IsRequired();
            b.Property(l => l.ConsumedCredits).IsRequired();
            b.Property(l => l.PeriodStartDate).HasColumnType("timestamp with time zone");
            b.Property(l => l.PeriodEndDate).HasColumnType("timestamp with time zone");
            b.Property(l => l.IsActive).IsRequired();
            b.Property(l => l.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(l => l.UpdatedAt).HasColumnType("timestamp with time zone");
            // Configure Version as concurrency token
            b.Property(l => l.Version)
                .IsConcurrencyToken()
                .HasDefaultValue(0);
            b.HasIndex(l => new { l.UserId, l.LimitType, l.ResourceType, l.IsActive });
            b.HasIndex(l => l.PeriodEndDate);
            b.HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId);
        });

        // Map refresh token entity
        modelBuilder.Entity<RefreshTokenEntity>(b =>
        {
            b.ToTable("RefreshTokens");
            b.HasKey(r => r.Id);
            b.Property(r => r.UserId).IsRequired();
            b.Property(r => r.Token).IsRequired().HasMaxLength(88); // Base64 of 64 bytes
            b.Property(r => r.ExpiresAt).HasColumnType("timestamp with time zone");
            b.Property(r => r.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(r => r.CreatedByIp).HasMaxLength(45);
            b.Property(r => r.RevokedAt).HasColumnType("timestamp with time zone");
            b.Property(r => r.RevokedByIp).HasMaxLength(45);
            b.Property(r => r.ReplacedByToken).HasMaxLength(88);
            b.Property(r => r.RevokeReason).HasMaxLength(200);
            b.HasIndex(r => r.Token).IsUnique();
            b.HasIndex(r => new { r.UserId, r.ExpiresAt });
            b.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Map agent rating entity
        modelBuilder.Entity<AgentRatingEntity>(b =>
        {
            b.ToTable("AgentRatings");
            b.HasKey(r => r.Id);
            b.Property(r => r.AgentId).IsRequired();
            b.Property(r => r.UserId).IsRequired();
            b.Property(r => r.Rating).IsRequired();
            b.Property(r => r.Comment).HasMaxLength(1000);
            b.Property(r => r.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(r => r.UpdatedAt).HasColumnType("timestamp with time zone");
            // Unique constraint: one rating per user per agent
            b.HasIndex(r => new { r.AgentId, r.UserId }).IsUnique();
            // Index for efficient average calculation queries
            b.HasIndex(r => r.AgentId);
            b.HasOne(r => r.Agent)
                .WithMany()
                .HasForeignKey(r => r.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Map plugin review entity
        modelBuilder.Entity<PluginReviewEntity>(b =>
        {
            b.ToTable("PluginReviews");
            b.HasKey(r => r.Id);
            b.Property(r => r.PluginId).IsRequired();
            b.Property(r => r.UserId).IsRequired();
            b.Property(r => r.Rating).IsRequired();
            b.Property(r => r.Title).HasMaxLength(100);
            b.Property(r => r.Body).HasMaxLength(2000);
            b.Property(r => r.HelpfulCount).IsRequired().HasDefaultValue(0);
            b.Property(r => r.IsVerified).IsRequired().HasDefaultValue(false);
            b.Property(r => r.IsDeleted).IsRequired().HasDefaultValue(false);
            b.Property(r => r.DeletedAt).HasColumnType("timestamp with time zone");
            b.Property(r => r.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(r => r.UpdatedAt).HasColumnType("timestamp with time zone");
            // Unique constraint: one review per user per plugin
            b.HasIndex(r => new { r.PluginId, r.UserId }).IsUnique();
            // Index for efficient queries by plugin
            b.HasIndex(r => r.PluginId);
            // Index for soft delete filtering
            b.HasIndex(r => r.IsDeleted);
            b.HasOne(r => r.Plugin)
                .WithMany()
                .HasForeignKey(r => r.PluginId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Map plugin review flag entity
        modelBuilder.Entity<PluginReviewFlagEntity>(b =>
        {
            b.ToTable("PluginReviewFlags");
            b.HasKey(f => f.Id);
            b.Property(f => f.ReviewId).IsRequired();
            b.Property(f => f.UserId).IsRequired();
            b.Property(f => f.Reason).IsRequired().HasMaxLength(100);
            b.Property(f => f.Details).HasMaxLength(1000);
            b.Property(f => f.CreatedAt).HasColumnType("timestamp with time zone");
            // Unique constraint: one flag per user per review
            b.HasIndex(f => new { f.ReviewId, f.UserId }).IsUnique();
            b.HasOne(f => f.Review)
                .WithMany()
                .HasForeignKey(f => f.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Map plugin review helpful vote entity
        modelBuilder.Entity<PluginReviewHelpfulVoteEntity>(b =>
        {
            b.ToTable("PluginReviewHelpfulVotes");
            b.HasKey(v => v.Id);
            b.Property(v => v.ReviewId).IsRequired();
            b.Property(v => v.UserId).IsRequired();
            b.Property(v => v.CreatedAt).HasColumnType("timestamp with time zone");
            // Unique constraint: one vote per user per review
            b.HasIndex(v => new { v.ReviewId, v.UserId }).IsUnique();
            b.HasOne(v => v.Review)
                .WithMany()
                .HasForeignKey(v => v.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(v => v.User)
                .WithMany()
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FeatureFlagEntity>(b =>
        {
            b.ToTable("FeatureFlags");
            b.HasKey(f => f.Id);
            b.Property(f => f.Key).IsRequired().HasMaxLength(100);
            b.Property(f => f.Description).HasMaxLength(500);
            b.Property(f => f.IsEnabled).IsRequired();
            b.Property(f => f.AllowedPlans).HasMaxLength(200);
            b.Property(f => f.RolloutPercentage).IsRequired().HasDefaultValue(100);
            b.Property(f => f.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(f => f.UpdatedAt).HasColumnType("timestamp with time zone");
            b.HasIndex(f => f.Key).IsUnique();
            b.HasIndex(f => f.IsEnabled);
        });

        modelBuilder.Entity<ServiceAccountEntity>(b =>
        {
            b.ToTable("ServiceAccounts");
            b.HasKey(sa => sa.Id);
            b.Property(sa => sa.ClientId).IsRequired().HasMaxLength(100);
            b.Property(sa => sa.ClientSecretHash).IsRequired();
            b.Property(sa => sa.Name).IsRequired().HasMaxLength(200);
            b.Property(sa => sa.Scopes).HasColumnType("text[]");
            b.Property(sa => sa.IsActive).IsRequired().HasDefaultValue(true);
            b.Property(sa => sa.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(sa => sa.LastUsedAt).HasColumnType("timestamp with time zone");
            b.HasIndex(sa => sa.ClientId).IsUnique();
        });

        // Map MessagesV2 entity for Chat V2 API
        modelBuilder.Entity<MessageV2Entity>(b =>
        {
            b.ToTable("MessagesV2");
            b.HasKey(m => m.Id);
            b.Property(m => m.SessionId).IsRequired().HasMaxLength(128);
            b.Property(m => m.ChatSessionId).IsRequired();
            b.Property(m => m.Role).IsRequired().HasMaxLength(20);
            b.Property(m => m.Content);
            b.Property(m => m.ToolCallsJson).HasColumnType("jsonb");
            b.Property(m => m.ArtifactsJson).HasColumnType("jsonb");
            b.Property(m => m.ReactionsJson).HasColumnType("jsonb");
            b.Property(m => m.Timestamp).IsRequired();
            b.Property(m => m.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(m => m.UpdatedAt).HasColumnType("timestamp with time zone");

            // Indexes for efficient pagination queries
            b.HasIndex(m => new { m.SessionId, m.Timestamp });
            b.HasIndex(m => m.ChatSessionId);

            // Relationship with ChatSession
            b.HasOne(m => m.ChatSession)
                .WithMany()
                .HasForeignKey(m => m.ChatSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Map MessageArtifacts entity (base table for all artifact types)
        modelBuilder.Entity<MessageArtifactEntity>(b =>
        {
            b.ToTable("MessageArtifacts");
            b.HasKey(a => a.Id);
            b.Property(a => a.MessageId).IsRequired();
            b.Property(a => a.Type).IsRequired().HasMaxLength(50);
            b.Property(a => a.Title).HasMaxLength(200);
            b.Property(a => a.Order).IsRequired().HasDefaultValue(0);
            b.Property(a => a.CreatedAt).HasColumnType("timestamp with time zone");

            // Index for efficient queries by message
            b.HasIndex(a => a.MessageId);

            // Relationship with Message
            b.HasOne(a => a.Message)
                .WithMany(m => m.Artifacts)
                .HasForeignKey(a => a.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Map ChartArtifacts entity
        modelBuilder.Entity<ChartArtifactEntity>(b =>
        {
            b.ToTable("ChartArtifacts");
            b.HasKey(c => c.ArtifactId);
            b.Property(c => c.ChartType).IsRequired().HasMaxLength(20);
            b.Property(c => c.Data).HasColumnType("jsonb");

            // 1:1 relationship with MessageArtifact
            b.HasOne(c => c.Artifact)
                .WithOne(a => a.ChartArtifact)
                .HasForeignKey<ChartArtifactEntity>(c => c.ArtifactId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Map TableArtifacts entity
        modelBuilder.Entity<TableArtifactEntity>(b =>
        {
            b.ToTable("TableArtifacts");
            b.HasKey(t => t.ArtifactId);
            b.Property(t => t.Data).HasColumnType("jsonb");

            // 1:1 relationship with MessageArtifact
            b.HasOne(t => t.Artifact)
                .WithOne(a => a.TableArtifact)
                .HasForeignKey<TableArtifactEntity>(t => t.ArtifactId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Map MermaidArtifacts entity
        modelBuilder.Entity<MermaidArtifactEntity>(b =>
        {
            b.ToTable("MermaidArtifacts");
            b.HasKey(m => m.ArtifactId);
            b.Property(m => m.Content).IsRequired();

            // 1:1 relationship with MessageArtifact
            b.HasOne(m => m.Artifact)
                .WithOne(a => a.MermaidArtifact)
                .HasForeignKey<MermaidArtifactEntity>(m => m.ArtifactId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Map CanvasTextArtifacts entity
        modelBuilder.Entity<CanvasTextArtifactEntity>(b =>
        {
            b.ToTable("CanvasTextArtifacts");
            b.HasKey(c => c.ArtifactId);
            b.Property(c => c.Content).IsRequired();

            // 1:1 relationship with MessageArtifact
            b.HasOne(c => c.Artifact)
                .WithOne(a => a.CanvasTextArtifact)
                .HasForeignKey<CanvasTextArtifactEntity>(c => c.ArtifactId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Map ImageArtifacts entity
        modelBuilder.Entity<ImageArtifactEntity>(b =>
        {
            b.ToTable("ImageArtifacts");
            b.HasKey(i => i.ArtifactId);
            b.Property(i => i.Src).IsRequired();
            b.Property(i => i.Alt).HasMaxLength(200);

            // 1:1 relationship with MessageArtifact
            b.HasOne(i => i.Artifact)
                .WithOne(a => a.ImageArtifact)
                .HasForeignKey<ImageArtifactEntity>(i => i.ArtifactId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Map VideoArtifacts entity
        modelBuilder.Entity<VideoArtifactEntity>(b =>
        {
            b.ToTable("VideoArtifacts");
            b.HasKey(v => v.ArtifactId);
            b.Property(v => v.Src).IsRequired();

            // 1:1 relationship with MessageArtifact
            b.HasOne(v => v.Artifact)
                .WithOne(a => a.VideoArtifact)
                .HasForeignKey<VideoArtifactEntity>(v => v.ArtifactId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Map ThreeJsArtifacts entity
        modelBuilder.Entity<ThreeJsArtifactEntity>(b =>
        {
            b.ToTable("ThreeJsArtifacts");
            b.HasKey(t => t.ArtifactId);
            b.Property(t => t.SceneData).HasColumnType("jsonb");

            // 1:1 relationship with MessageArtifact
            b.HasOne(t => t.Artifact)
                .WithOne(a => a.ThreeJsArtifact)
                .HasForeignKey<ThreeJsArtifactEntity>(t => t.ArtifactId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Map CalendarArtifacts entity
        modelBuilder.Entity<CalendarArtifactEntity>(b =>
        {
            b.ToTable("CalendarArtifacts");
            b.HasKey(c => c.ArtifactId);

            // Events is now stored as string, configure as jsonb for PostgreSQL
            if (Database.IsNpgsql())
            {
                b.Property(c => c.Events).HasColumnType("jsonb");
            }

            // 1:1 relationship with MessageArtifact
            b.HasOne(c => c.Artifact)
                .WithOne(a => a.CalendarArtifact)
                .HasForeignKey<CalendarArtifactEntity>(c => c.ArtifactId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Map Reactions entity (polymorphic: targets messages or artifacts)
        modelBuilder.Entity<ReactionEntity>(b =>
        {
            b.ToTable("Reactions");
            b.HasKey(r => r.Id);
            b.Property(r => r.TargetType).IsRequired().HasMaxLength(20);
            b.Property(r => r.TargetId).IsRequired();
            b.Property(r => r.UserId).IsRequired();
            b.Property(r => r.Emoji).IsRequired().HasMaxLength(100);
            b.Property(r => r.Type).IsRequired().HasMaxLength(20).HasDefaultValue("emoji");
            b.Property(r => r.CreatedAt).HasColumnType("timestamp with time zone");

            // Unique constraint: one reaction per user per target per emoji
            b.HasIndex(r => new { r.TargetId, r.UserId, r.Emoji }).IsUnique();

            // Index for efficient queries by target
            b.HasIndex(r => new { r.TargetType, r.TargetId });

            // Relationship with User
            b.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Note: Polymorphic relationships to Message and Artifact are handled
            // manually in application code since EF Core doesn't natively support
            // polymorphic FKs. The TargetType discriminator indicates which entity
            // the TargetId references ('message' or 'artifact').
        });

        // Map Matrix user link entity
        modelBuilder.Entity<MatrixUserLinkEntity>(b =>
        {
            b.ToTable("MatrixUserLinks");
            b.HasKey(l => l.Id);
            b.Property(l => l.UserId).IsRequired();
            b.Property(l => l.MatrixUserId).IsRequired().HasMaxLength(255);
            b.Property(l => l.LinkedAt).HasColumnType("timestamp with time zone");
            b.Property(l => l.IsActive).IsRequired().HasDefaultValue(true);

            // Unique constraint: one Matrix user ID can only be linked once
            b.HasIndex(l => l.MatrixUserId).IsUnique();

            // Index for efficient lookup by user
            b.HasIndex(l => new { l.UserId, l.IsActive });

            // Relationship with User
            b.HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Map Matrix link code entity
        modelBuilder.Entity<MatrixLinkCodeEntity>(b =>
        {
            b.ToTable("MatrixLinkCodes");
            b.HasKey(c => c.Id);
            b.Property(c => c.UserId).IsRequired();
            b.Property(c => c.Code).IsRequired().HasMaxLength(20);
            b.Property(c => c.ExpiresAt).HasColumnType("timestamp with time zone");
            b.Property(c => c.UsedAt).HasColumnType("timestamp with time zone");

            // Unique constraint: codes must be unique
            b.HasIndex(c => c.Code).IsUnique();

            // Index for efficient lookup by user
            b.HasIndex(c => c.UserId);

            // Index for cleanup of expired codes
            b.HasIndex(c => c.ExpiresAt);

            // Relationship with User
            b.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Map CreditPackage entity
        modelBuilder.Entity<CreditPackageEntity>(b =>
        {
            b.ToTable("CreditPackages");
            b.HasKey(p => p.Id);
            b.Property(p => p.Name).IsRequired().HasMaxLength(100);
            b.Property(p => p.Credits).IsRequired();
            b.Property(p => p.PriceInCents).IsRequired();
            b.Property(p => p.Currency).IsRequired().HasMaxLength(3).HasDefaultValue("usd");
            b.Property(p => p.StripePriceId).HasMaxLength(100);
            b.Property(p => p.IsActive).IsRequired().HasDefaultValue(true);
            b.Property(p => p.ExpirationDays);
            b.Property(p => p.DisplayOrder).IsRequired().HasDefaultValue(0);
            b.Property(p => p.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(p => p.UpdatedAt).HasColumnType("timestamp with time zone");

            b.HasIndex(p => p.StripePriceId);
            b.HasIndex(p => p.IsActive);
        });

        // Map CreditPurchase entity
        modelBuilder.Entity<CreditPurchaseEntity>(b =>
        {
            b.ToTable("CreditPurchases");
            b.HasKey(p => p.Id);
            b.Property(p => p.UserId).IsRequired();
            b.Property(p => p.CreditPackageId).IsRequired();
            b.Property(p => p.CreditsGranted).IsRequired();
            b.Property(p => p.PricePaidCents).IsRequired();
            b.Property(p => p.Currency).IsRequired().HasMaxLength(3).HasDefaultValue("usd");
            b.Property(p => p.StripeCheckoutSessionId).HasMaxLength(100);
            b.Property(p => p.StripePaymentIntentId).HasMaxLength(100);
            b.Property(p => p.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(CreditPurchaseStatus.Pending);
            b.Property(p => p.PurchasedAt).HasColumnType("timestamp with time zone");
            b.Property(p => p.CreditsExpiresAt).HasColumnType("timestamp with time zone");

            b.HasIndex(p => p.UserId);
            b.HasIndex(p => p.StripeCheckoutSessionId);
            b.HasIndex(p => p.StripePaymentIntentId);
            b.HasIndex(p => p.Status);

            // Relationships
            b.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(p => p.CreditPackage)
                .WithMany()
                .HasForeignKey(p => p.CreditPackageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Map Workspace entity
        modelBuilder.Entity<WorkspaceEntity>(b =>
        {
            b.ToTable("Workspaces");
            b.HasKey(w => w.Id);
            b.Property(w => w.Name).IsRequired().HasMaxLength(200);
            b.Property(w => w.Description).HasMaxLength(1000);
            b.Property(w => w.AvatarUrl).HasMaxLength(500);
            b.Property(w => w.Slug).IsRequired().HasMaxLength(100);
            b.Property(w => w.OwnerId).IsRequired();
            b.Property(w => w.IsPersonal).IsRequired().HasDefaultValue(false);
            b.Property(w => w.DeletedAt).HasColumnType("timestamp with time zone");
            b.Property(w => w.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(w => w.UpdatedAt).HasColumnType("timestamp with time zone");

            // Unique slug constraint
            b.HasIndex(w => w.Slug).IsUnique();

            // Index for user's workspaces query
            b.HasIndex(w => w.OwnerId);

            // Soft delete filter
            b.HasIndex(w => w.DeletedAt);

            // Relationship with Owner
            b.HasOne(w => w.Owner)
                .WithMany()
                .HasForeignKey(w => w.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Map WorkspaceMember entity
        modelBuilder.Entity<WorkspaceMemberEntity>(b =>
        {
            b.ToTable("WorkspaceMembers");
            b.HasKey(m => m.Id);
            b.Property(m => m.WorkspaceId).IsRequired();
            b.Property(m => m.UserId).IsRequired();
            b.Property(m => m.Role)
                .HasConversion<int>()
                .IsRequired();
            b.Property(m => m.JoinedAt).HasColumnType("timestamp with time zone");
            b.Property(m => m.LeftAt).HasColumnType("timestamp with time zone");

            // Unique constraint: user can only be member once per workspace
            b.HasIndex(m => new { m.WorkspaceId, m.UserId }).IsUnique();

            // Index for user's memberships query
            b.HasIndex(m => m.UserId);

            // Relationships
            b.HasOne(m => m.Workspace)
                .WithMany(w => w.Members)
                .HasForeignKey(m => m.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(m => m.InvitedBy)
                .WithMany()
                .HasForeignKey(m => m.InvitedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Map WorkspaceSubscription entity
        modelBuilder.Entity<WorkspaceSubscriptionEntity>(b =>
        {
            b.ToTable("WorkspaceSubscriptions");
            b.HasKey(s => s.Id);
            b.Property(s => s.WorkspaceId).IsRequired();
            b.Property(s => s.Credits).IsRequired();
            b.Property(s => s.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(s => s.UpdatedAt).HasColumnType("timestamp with time zone");
            b.Property(s => s.ExpiresAt).HasColumnType("timestamp with time zone");
            b.Property(s => s.AutoRenew).HasDefaultValue(true);
            b.Property(s => s.Version)
                .IsConcurrencyToken()
                .HasDefaultValue(0);
            b.Property(s => s.StripeCustomerId).HasMaxLength(100);
            b.Property(s => s.StripeSubscriptionId).HasMaxLength(100);
            b.Property(s => s.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(SubscriptionStatus.Incomplete);
            b.Property(s => s.CurrentPeriodStart).HasColumnType("timestamp with time zone");
            b.Property(s => s.CurrentPeriodEnd).HasColumnType("timestamp with time zone");
            b.Property(s => s.CancelAtPeriodEnd).HasDefaultValue(false);
            b.Property(s => s.CanceledAt).HasColumnType("timestamp with time zone");
            b.Property(s => s.TrialEndsAt).HasColumnType("timestamp with time zone");

            b.HasIndex(s => s.StripeCustomerId);
            b.HasIndex(s => s.StripeSubscriptionId);

            b.HasOne(s => s.Workspace)
                .WithOne()
                .HasForeignKey<WorkspaceSubscriptionEntity>(s => s.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(s => s.SubscriptionPlan)
                .WithMany()
                .HasForeignKey(s => s.SubscriptionPlanId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Map UserSocialIdentity entity
        modelBuilder.Entity<UserSocialIdentityEntity>(b =>
        {
            b.ToTable("UserSocialIdentities");
            b.HasKey(i => i.Id);
            b.Property(i => i.UserId).IsRequired();
            b.Property(i => i.Provider).IsRequired().HasMaxLength(50);
            b.Property(i => i.ProviderUserId).IsRequired().HasMaxLength(255);
            b.Property(i => i.Email).HasMaxLength(255);
            b.Property(i => i.DisplayName).HasMaxLength(200);
            b.Property(i => i.AvatarUrl).HasMaxLength(500);
            b.Property(i => i.IsPrimary).IsRequired();
            b.Property(i => i.LinkedAt).HasColumnType("timestamp with time zone");
            b.Property(i => i.LastUsedAt).HasColumnType("timestamp with time zone");
            b.Property(i => i.UnlinkedAt).HasColumnType("timestamp with time zone");

            // Unique constraint: one provider per user (for active identities)
            b.HasIndex(i => new { i.UserId, i.Provider })
                .IsUnique()
                .HasFilter("\"UnlinkedAt\" IS NULL");

            // Unique constraint: provider user ID (for active identities)
            b.HasIndex(i => new { i.Provider, i.ProviderUserId })
                .IsUnique()
                .HasFilter("\"UnlinkedAt\" IS NULL");

            // Index for user lookups
            b.HasIndex(i => i.UserId);

            // Relationship with User
            b.HasOne(i => i.User)
                .WithMany()
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Map Publisher entity
        modelBuilder.Entity<PublisherEntity>(b =>
        {
            b.ToTable("Publishers");
            b.HasKey(p => p.Id);
            b.Property(p => p.UserId).IsRequired();
            b.Property(p => p.Name).IsRequired().HasMaxLength(200);
            b.Property(p => p.Bio).HasMaxLength(2000);
            b.Property(p => p.WebsiteUrl).HasMaxLength(500);
            b.Property(p => p.LogoUrl).HasMaxLength(500);
            b.Property(p => p.SupportEmail).HasMaxLength(255);
            b.Property(p => p.IsVerified).IsRequired().HasDefaultValue(false);
            b.Property(p => p.VerifiedAt).HasColumnType("timestamp with time zone");
            b.Property(p => p.Status)
                .HasConversion<int>()
                .IsRequired()
                .HasDefaultValue(PublisherStatus.Pending);
            b.Property(p => p.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(p => p.UpdatedAt).HasColumnType("timestamp with time zone");

            // Unique constraint: one publisher per user
            b.HasIndex(p => p.UserId).IsUnique();

            // Relationship with User
            b.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Map PluginSubmission entity
        modelBuilder.Entity<PluginSubmissionEntity>(b =>
        {
            b.ToTable("PluginSubmissions");
            b.HasKey(s => s.Id);
            b.Property(s => s.PublisherId).IsRequired();
            b.Property(s => s.PluginId);
            b.Property(s => s.Name).IsRequired().HasMaxLength(200);
            b.Property(s => s.Slug).IsRequired().HasMaxLength(100);
            b.Property(s => s.ShortDescription).IsRequired().HasMaxLength(500);
            b.Property(s => s.FullDescription);
            b.Property(s => s.Category).IsRequired().HasMaxLength(100);
            b.Property(s => s.Tags).HasMaxLength(500);
            b.Property(s => s.Version).IsRequired().HasMaxLength(50);
            b.Property(s => s.ManifestUrl).HasMaxLength(500);
            b.Property(s => s.RepositoryUrl).HasMaxLength(500);
            b.Property(s => s.Compatibility).HasMaxLength(500);
            b.Property(s => s.Status)
                .HasConversion<int>()
                .IsRequired()
                .HasDefaultValue(SubmissionStatus.Pending);
            b.Property(s => s.ReviewedBy);
            b.Property(s => s.ReviewNotes).HasMaxLength(2000);
            b.Property(s => s.ReviewedAt).HasColumnType("timestamp with time zone");
            b.Property(s => s.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(s => s.UpdatedAt).HasColumnType("timestamp with time zone");

            // Indexes
            b.HasIndex(s => s.PublisherId);
            b.HasIndex(s => s.PluginId);
            b.HasIndex(s => s.Status);
            b.HasIndex(s => s.Slug);

            // Relationships
            b.HasOne(s => s.Publisher)
                .WithMany()
                .HasForeignKey(s => s.PublisherId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(s => s.Plugin)
                .WithMany()
                .HasForeignKey(s => s.PluginId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Map PluginVersion entity
        modelBuilder.Entity<PluginVersionEntity>(b =>
        {
            b.ToTable("PluginVersions");
            b.HasKey(v => v.Id);
            b.Property(v => v.PluginId).IsRequired();
            b.Property(v => v.Version).IsRequired().HasMaxLength(50);
            b.Property(v => v.Changelog);
            b.Property(v => v.IsBreakingChange).IsRequired().HasDefaultValue(false);
            b.Property(v => v.MinCompatibility).HasMaxLength(100);
            b.Property(v => v.MaxCompatibility).HasMaxLength(100);
            b.Property(v => v.DownloadUrl).HasMaxLength(2048);
            b.Property(v => v.Checksum).HasMaxLength(100);
            b.Property(v => v.FileSizeBytes);
            b.Property(v => v.OpenApiSpecCache);
            b.Property(v => v.BaseUrl).HasMaxLength(2048);

            // Deprecation fields
            b.Property(v => v.IsDeprecated).IsRequired().HasDefaultValue(false);
            b.Property(v => v.DeprecatedAt).HasColumnType("timestamp with time zone");
            b.Property(v => v.DeprecationEndsAt).HasColumnType("timestamp with time zone");
            b.Property(v => v.DeprecationMessage).HasMaxLength(500);
            b.Property(v => v.SuccessorVersionId);

            // Status fields
            b.Property(v => v.IsActive).IsRequired().HasDefaultValue(true);
            b.Property(v => v.IsLatest).IsRequired().HasDefaultValue(false);

            // Audit fields
            b.Property(v => v.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(v => v.PublishedAt).HasColumnType("timestamp with time zone");
            b.Property(v => v.PublishedBy);

            // Unique constraint: one version string per plugin
            b.HasIndex(v => new { v.PluginId, v.Version }).IsUnique();
            b.HasIndex(v => new { v.PluginId, v.IsLatest });
            b.HasIndex(v => v.IsDeprecated);

            // Relationships
            b.HasOne(v => v.Plugin)
                .WithMany(p => p.Versions)
                .HasForeignKey(v => v.PluginId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(v => v.SuccessorVersion)
                .WithMany()
                .HasForeignKey(v => v.SuccessorVersionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Map PluginUsageEvent entity for analytics
        modelBuilder.Entity<PluginUsageEventEntity>(b =>
        {
            b.ToTable("PluginUsageEvents");
            b.HasKey(e => e.Id);
            b.Property(e => e.PluginId).IsRequired();
            b.Property(e => e.UserId);
            b.Property(e => e.AgentId);
            b.Property(e => e.EventType).IsRequired().HasMaxLength(50);
            b.Property(e => e.Metadata).HasColumnType("jsonb");
            b.Property(e => e.ErrorMessage).HasMaxLength(1000);
            b.Property(e => e.DurationMs);
            b.Property(e => e.OccurredAt).HasColumnType("timestamp with time zone");

            // Indexes for efficient analytics queries
            b.HasIndex(e => new { e.PluginId, e.OccurredAt });
            b.HasIndex(e => new { e.EventType, e.OccurredAt });
            b.HasIndex(e => e.UserId);

            // Relationships
            b.HasOne(e => e.Plugin)
                .WithMany()
                .HasForeignKey(e => e.PluginId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasOne(e => e.Agent)
                .WithMany()
                .HasForeignKey(e => e.AgentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // NotificationPreferencesEntity configuration
        modelBuilder.Entity<NotificationPreferencesEntity>(b =>
        {
            b.ToTable("NotificationPreferences");
            b.HasKey(p => p.Id);
            b.Property(p => p.UserId).IsRequired();
            b.Property(p => p.NotificationsEnabled).IsRequired().HasDefaultValue(true);
            b.Property(p => p.QuietHoursStart);
            b.Property(p => p.QuietHoursEnd);
            b.Property(p => p.Timezone).HasMaxLength(100).HasDefaultValue("UTC");
            b.Property(p => p.EnabledTypes).HasColumnType("jsonb");
            b.Property(p => p.MatrixNotificationsEnabled).IsRequired().HasDefaultValue(true);
            b.Property(p => p.EmailNotificationsEnabled).IsRequired().HasDefaultValue(false);
            b.Property(p => p.EmailDigestFrequency).HasMaxLength(20);
            b.Property(p => p.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(p => p.UpdatedAt).HasColumnType("timestamp with time zone");
            b.HasIndex(p => p.UserId).IsUnique();
            b.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ScheduledNotificationEntity configuration
        modelBuilder.Entity<ScheduledNotificationEntity>(b =>
        {
            b.ToTable("ScheduledNotifications");
            b.HasKey(n => n.Id);
            b.Property(n => n.UserId).IsRequired();
            b.Property(n => n.Type).IsRequired().HasMaxLength(50);
            b.Property(n => n.Title).IsRequired().HasMaxLength(200);
            b.Property(n => n.Message).IsRequired();
            b.Property(n => n.Priority).IsRequired().HasDefaultValue(1);
            b.Property(n => n.Metadata).HasColumnType("jsonb");
            b.Property(n => n.ScheduledFor).HasColumnType("timestamp with time zone");
            b.Property(n => n.CronExpression).HasMaxLength(100);
            b.Property(n => n.IsRecurring).IsRequired().HasDefaultValue(false);
            b.Property(n => n.LastExecutedAt).HasColumnType("timestamp with time zone");
            b.Property(n => n.NextExecutionAt).HasColumnType("timestamp with time zone");
            b.Property(n => n.Status)
                .HasConversion<int>()
                .IsRequired()
                .HasDefaultValue(ScheduledNotificationStatus.Pending);
            b.Property(n => n.RetryCount).IsRequired().HasDefaultValue(0);
            b.Property(n => n.LastError).HasMaxLength(1000);
            b.Property(n => n.SourceService).HasMaxLength(100);
            b.Property(n => n.SourceId).HasMaxLength(100);
            b.Property(n => n.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(n => n.UpdatedAt).HasColumnType("timestamp with time zone");
            b.HasIndex(n => n.UserId);
            b.HasIndex(n => n.ScheduledFor);
            b.HasIndex(n => n.Status);
            b.HasIndex(n => new { n.Status, n.ScheduledFor });
            b.HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}