using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoUpdate",
                table: "UserPlugins",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "InstalledVersion",
                table: "UserPlugins",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InstalledVersionId",
                table: "UserPlugins",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedAt",
                table: "UserPlugins",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PinnedVersion",
                table: "UserPlugins",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LatestVersionId",
                table: "Plugins",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VersionCount",
                table: "Plugins",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModelChangedAt",
                table: "ChatSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelOverride",
                table: "ChatSessions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    QuietHoursStart = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    QuietHoursEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "UTC"),
                    EnabledTypes = table.Column<string>(type: "jsonb", nullable: false),
                    MatrixNotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    EmailNotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EmailDigestFrequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PluginUsageEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginUsageEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PluginUsageEvents_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PluginUsageEvents_Plugins_PluginId",
                        column: x => x.PluginId,
                        principalTable: "Plugins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PluginUsageEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PluginVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Changelog = table.Column<string>(type: "text", nullable: true),
                    IsBreakingChange = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MinCompatibility = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MaxCompatibility = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DownloadUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Checksum = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    OpenApiSpecCache = table.Column<string>(type: "text", nullable: true),
                    BaseUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    IsDeprecated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeprecatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeprecationEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeprecationMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SuccessorVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsLatest = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PluginVersions_PluginVersions_SuccessorVersionId",
                        column: x => x.SuccessorVersionId,
                        principalTable: "PluginVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PluginVersions_Plugins_PluginId",
                        column: x => x.PluginId,
                        principalTable: "Plugins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    ScheduledFor = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CronExpression = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsRecurring = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextExecutionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RetryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SourceService = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledNotifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPlugins_InstalledVersionId",
                table: "UserPlugins",
                column: "InstalledVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_UserId",
                table: "NotificationPreferences",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PluginUsageEvents_AgentId",
                table: "PluginUsageEvents",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_PluginUsageEvents_EventType_OccurredAt",
                table: "PluginUsageEvents",
                columns: new[] { "EventType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PluginUsageEvents_PluginId_OccurredAt",
                table: "PluginUsageEvents",
                columns: new[] { "PluginId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PluginUsageEvents_UserId",
                table: "PluginUsageEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PluginVersions_IsDeprecated",
                table: "PluginVersions",
                column: "IsDeprecated");

            migrationBuilder.CreateIndex(
                name: "IX_PluginVersions_PluginId_IsLatest",
                table: "PluginVersions",
                columns: new[] { "PluginId", "IsLatest" });

            migrationBuilder.CreateIndex(
                name: "IX_PluginVersions_PluginId_Version",
                table: "PluginVersions",
                columns: new[] { "PluginId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PluginVersions_SuccessorVersionId",
                table: "PluginVersions",
                column: "SuccessorVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledNotifications_ScheduledFor",
                table: "ScheduledNotifications",
                column: "ScheduledFor");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledNotifications_Status",
                table: "ScheduledNotifications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledNotifications_Status_ScheduledFor",
                table: "ScheduledNotifications",
                columns: new[] { "Status", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledNotifications_UserId",
                table: "ScheduledNotifications",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserPlugins_PluginVersions_InstalledVersionId",
                table: "UserPlugins",
                column: "InstalledVersionId",
                principalTable: "PluginVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPlugins_PluginVersions_InstalledVersionId",
                table: "UserPlugins");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropTable(
                name: "PluginUsageEvents");

            migrationBuilder.DropTable(
                name: "PluginVersions");

            migrationBuilder.DropTable(
                name: "ScheduledNotifications");

            migrationBuilder.DropIndex(
                name: "IX_UserPlugins_InstalledVersionId",
                table: "UserPlugins");

            migrationBuilder.DropColumn(
                name: "AutoUpdate",
                table: "UserPlugins");

            migrationBuilder.DropColumn(
                name: "InstalledVersion",
                table: "UserPlugins");

            migrationBuilder.DropColumn(
                name: "InstalledVersionId",
                table: "UserPlugins");

            migrationBuilder.DropColumn(
                name: "LastUpdatedAt",
                table: "UserPlugins");

            migrationBuilder.DropColumn(
                name: "PinnedVersion",
                table: "UserPlugins");

            migrationBuilder.DropColumn(
                name: "LatestVersionId",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "VersionCount",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "ModelChangedAt",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "ModelOverride",
                table: "ChatSessions");
        }
    }
}
