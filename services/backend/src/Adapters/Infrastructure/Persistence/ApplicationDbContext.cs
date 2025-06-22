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
            b.HasOne(s => s.User)
                .WithOne()
                .HasForeignKey<SubscriptionEntity>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(s => s.SubscriptionPlan)
                .WithMany()
                .HasForeignKey(s => s.SubscriptionPlanId)
                .OnDelete(DeleteBehavior.SetNull);
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
            b.Property(p => p.CreatedAt).HasColumnType("timestamp with time zone");
            b.Property(p => p.UpdatedAt).HasColumnType("timestamp with time zone");
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
            b.HasOne<AgentEntity>()
                .WithMany()
                .HasForeignKey(ap => ap.AgentId);
            b.HasOne<PluginEntity>()
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
            b.HasIndex(up => new { up.UserId, up.PluginId }).IsUnique();
            b.HasOne(up => up.User)
                .WithMany()
                .HasForeignKey(up => up.UserId);
            b.HasOne(up => up.Plugin)
                .WithMany()
                .HasForeignKey(up => up.PluginId);
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
    }
}