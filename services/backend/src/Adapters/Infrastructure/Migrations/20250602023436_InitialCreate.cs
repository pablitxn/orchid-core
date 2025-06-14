using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "ActionCosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "text", nullable: false),
                    Credits = table.Column<decimal>(type: "numeric", nullable: false),
                    PaymentUnit = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionCosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CostConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CostType = table.Column<string>(type: "text", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceName = table.Column<string>(type: "text", nullable: true),
                    CreditCost = table.Column<int>(type: "integer", nullable: false),
                    CostPer1kTokens = table.Column<decimal>(type: "numeric", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<string>(type: "text", nullable: false),
                    DocumentPath = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Enum = table.Column<string>(type: "text", nullable: false, defaultValue: "Attachment"),
                    Embedding = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    IsIndexed = table.Column<bool>(type: "boolean", nullable: false),
                    ChunkCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeBaseFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    MimeType = table.Column<string>(type: "text", nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBaseFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    ActionUrl = table.Column<string>(type: "text", nullable: true),
                    ActionText = table.Column<string>(type: "text", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersonalityTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalityTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plugins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SourceUrl = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PriceCredits = table.Column<int>(type: "integer", nullable: false),
                    IsSubscriptionBased = table.Column<bool>(type: "boolean", nullable: false),
                    SystemName = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TargetLanguage = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanEnum = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    Credits = table.Column<int>(type: "integer", nullable: false),
                    PaymentUnit = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Policy = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    AvatarUrl = table.Column<string>(type: "text", nullable: true),
                    Credits = table.Column<int>(type: "integer", nullable: true),
                    SubscriptionActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    PasswordResetToken = table.Column<string>(type: "text", nullable: true),
                    PasswordResetExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PasswordChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    PriceCredits = table.Column<int>(type: "integer", nullable: false),
                    Author = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: true),
                    Steps = table.Column<int>(type: "integer", nullable: false),
                    EstimatedTime = table.Column<string>(type: "text", nullable: true),
                    Rating = table.Column<double>(type: "double precision", nullable: false),
                    Runs = table.Column<int>(type: "integer", nullable: false),
                    Icon = table.Column<string>(type: "text", nullable: true),
                    Tags = table.Column<string>(type: "text", nullable: true),
                    DetailedDescription = table.Column<string>(type: "text", nullable: true),
                    Prerequisites = table.Column<string>(type: "text", nullable: true),
                    InputRequirements = table.Column<string>(type: "text", nullable: true),
                    OutputFormat = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SheetChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SheetName = table.Column<string>(type: "text", nullable: false),
                    StartRow = table.Column<int>(type: "integer", nullable: false),
                    EndRow = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1536)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SheetChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SheetChunks_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaCenterAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgeBaseFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    MimeType = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    FileUrl = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaCenterAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaCenterAssets_KnowledgeBaseFiles_KnowledgeBaseFileId",
                        column: x => x.KnowledgeBaseFileId,
                        principalTable: "KnowledgeBaseFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AudioFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioFiles_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Agents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    AvatarUrl = table.Column<string>(type: "text", nullable: true),
                    Personality = table.Column<string>(type: "text", nullable: true),
                    PersonalityTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Language = table.Column<string>(type: "text", nullable: true),
                    PluginIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsInRecycleBin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    RecycleBinExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Agents_PersonalityTemplates_PersonalityTemplateId",
                        column: x => x.PersonalityTemplateId,
                        principalTable: "PersonalityTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Agents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CreditConsumptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumptionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", maxLength: 100, nullable: false),
                    ResourceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreditsConsumed = table.Column<int>(type: "integer", nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BalanceAfter = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditConsumptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditConsumptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoginHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoginHistories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageCosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingMethod = table.Column<string>(type: "text", nullable: false),
                    TokensConsumed = table.Column<int>(type: "integer", nullable: true),
                    CostPerToken = table.Column<decimal>(type: "numeric", nullable: true),
                    FixedRate = table.Column<decimal>(type: "numeric", nullable: true),
                    TotalCredits = table.Column<int>(type: "integer", nullable: false),
                    HasPluginUsage = table.Column<bool>(type: "boolean", nullable: false),
                    HasWorkflowUsage = table.Column<bool>(type: "boolean", nullable: false),
                    AdditionalCredits = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageCosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageCosts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Credits = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AutoRenew = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SubscriptionPlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserBillingPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageBillingMethod = table.Column<string>(type: "text", nullable: false),
                    TokenRate = table.Column<decimal>(type: "numeric", nullable: false),
                    FixedMessageRate = table.Column<int>(type: "integer", nullable: false),
                    LowCreditThreshold = table.Column<int>(type: "integer", nullable: true),
                    EnableLowCreditAlerts = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBillingPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserBillingPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserCreditLimits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LimitType = table.Column<string>(type: "text", nullable: false),
                    MaxCredits = table.Column<int>(type: "integer", nullable: false),
                    ConsumedCredits = table.Column<int>(type: "integer", nullable: false),
                    PeriodStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ResourceType = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCreditLimits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCreditLimits_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPlugins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPlugins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPlugins_Plugins_PluginId",
                        column: x => x.PluginId,
                        principalTable: "Plugins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPlugins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserWorkflows",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWorkflows", x => new { x.UserId, x.WorkflowId });
                    table.ForeignKey(
                        name: "FK_UserWorkflows_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserWorkflows_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentPlugins",
                columns: table => new
                {
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId1 = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId1 = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentPlugins", x => new { x.AgentId, x.PluginId });
                    table.ForeignKey(
                        name: "FK_AgentPlugins_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentPlugins_Agents_AgentId1",
                        column: x => x.AgentId1,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentPlugins_Plugins_PluginId",
                        column: x => x.PluginId,
                        principalTable: "Plugins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentPlugins_Plugins_PluginId1",
                        column: x => x.PluginId1,
                        principalTable: "Plugins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    InteractionType = table.Column<int>(type: "integer", nullable: false),
                    SessionId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatSessions_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChatSessions_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TeamAgents",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamAgents", x => new { x.TeamId, x.AgentId });
                    table.ForeignKey(
                        name: "FK_TeamAgents_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamAgents_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionCosts_CreatedAt",
                table: "ActionCosts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AgentPlugins_AgentId1",
                table: "AgentPlugins",
                column: "AgentId1");

            migrationBuilder.CreateIndex(
                name: "IX_AgentPlugins_PluginId",
                table: "AgentPlugins",
                column: "PluginId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentPlugins_PluginId1",
                table: "AgentPlugins",
                column: "PluginId1");

            migrationBuilder.CreateIndex(
                name: "IX_Agents_IsDeleted_IsInRecycleBin",
                table: "Agents",
                columns: new[] { "IsDeleted", "IsInRecycleBin" });

            migrationBuilder.CreateIndex(
                name: "IX_Agents_IsPublic",
                table: "Agents",
                column: "IsPublic");

            migrationBuilder.CreateIndex(
                name: "IX_Agents_PersonalityTemplateId",
                table: "Agents",
                column: "PersonalityTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Agents_UserId",
                table: "Agents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioFiles_ProjectId",
                table: "AudioFiles",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_AgentId",
                table: "ChatSessions",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_TeamId",
                table: "ChatSessions",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_UserId_IsArchived",
                table: "ChatSessions",
                columns: new[] { "UserId", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditConsumptions_ConsumptionType",
                table: "CreditConsumptions",
                column: "ConsumptionType");

            migrationBuilder.CreateIndex(
                name: "IX_CreditConsumptions_ResourceId",
                table: "CreditConsumptions",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditConsumptions_UserId_ConsumedAt",
                table: "CreditConsumptions",
                columns: new[] { "UserId", "ConsumedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseFiles_CreatedAt",
                table: "KnowledgeBaseFiles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseFiles_MimeType",
                table: "KnowledgeBaseFiles",
                column: "MimeType");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseFiles_Tags",
                table: "KnowledgeBaseFiles",
                column: "Tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_LoginHistories_Success",
                table: "LoginHistories",
                column: "Success");

            migrationBuilder.CreateIndex(
                name: "IX_LoginHistories_UserId_Timestamp",
                table: "LoginHistories",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaCenterAssets_CreatedAt",
                table: "MediaCenterAssets",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MediaCenterAssets_KnowledgeBaseFileId",
                table: "MediaCenterAssets",
                column: "KnowledgeBaseFileId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaCenterAssets_MimeType",
                table: "MediaCenterAssets",
                column: "MimeType");

            migrationBuilder.CreateIndex(
                name: "IX_MessageCosts_MessageId",
                table: "MessageCosts",
                column: "MessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageCosts_UserId_CreatedAt",
                table: "MessageCosts",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalityTemplates_Category",
                table: "PersonalityTemplates",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalityTemplates_IsActive",
                table: "PersonalityTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SheetChunks_DocumentId",
                table: "SheetChunks",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_SubscriptionPlanId",
                table: "Subscriptions",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_UserId",
                table: "Subscriptions",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamAgents_AgentId",
                table: "TeamAgents",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBillingPreferences_UserId",
                table: "UserBillingPreferences",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCreditLimits_UserId",
                table: "UserCreditLimits",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPlugins_PluginId",
                table: "UserPlugins",
                column: "PluginId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPlugins_UserId_PluginId",
                table: "UserPlugins",
                columns: new[] { "UserId", "PluginId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWorkflows_WorkflowId",
                table: "UserWorkflows",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_Category",
                table: "Workflows",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_Rating",
                table: "Workflows",
                column: "Rating");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_Runs",
                table: "Workflows",
                column: "Runs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionCosts");

            migrationBuilder.DropTable(
                name: "AgentPlugins");

            migrationBuilder.DropTable(
                name: "AudioFiles");

            migrationBuilder.DropTable(
                name: "ChatSessions");

            migrationBuilder.DropTable(
                name: "CostConfigurations");

            migrationBuilder.DropTable(
                name: "CreditConsumptions");

            migrationBuilder.DropTable(
                name: "LoginHistories");

            migrationBuilder.DropTable(
                name: "MediaCenterAssets");

            migrationBuilder.DropTable(
                name: "MessageCosts");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "SheetChunks");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "TeamAgents");

            migrationBuilder.DropTable(
                name: "UserBillingPreferences");

            migrationBuilder.DropTable(
                name: "UserCreditLimits");

            migrationBuilder.DropTable(
                name: "UserPlugins");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "UserWorkflows");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "KnowledgeBaseFiles");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropTable(
                name: "Agents");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "Plugins");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Workflows");

            migrationBuilder.DropTable(
                name: "PersonalityTemplates");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
