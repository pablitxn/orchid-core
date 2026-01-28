using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPluginVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create PluginVersions table
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
                        name: "FK_PluginVersions_Plugins_PluginId",
                        column: x => x.PluginId,
                        principalTable: "Plugins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PluginVersions_PluginVersions_SuccessorVersionId",
                        column: x => x.SuccessorVersionId,
                        principalTable: "PluginVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // Add indexes for PluginVersions
            migrationBuilder.CreateIndex(
                name: "IX_PluginVersions_PluginId_Version",
                table: "PluginVersions",
                columns: new[] { "PluginId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PluginVersions_PluginId_IsLatest",
                table: "PluginVersions",
                columns: new[] { "PluginId", "IsLatest" });

            migrationBuilder.CreateIndex(
                name: "IX_PluginVersions_IsDeprecated",
                table: "PluginVersions",
                column: "IsDeprecated");

            migrationBuilder.CreateIndex(
                name: "IX_PluginVersions_SuccessorVersionId",
                table: "PluginVersions",
                column: "SuccessorVersionId");

            // Add new columns to Plugins table
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

            // Add new columns to UserPlugins table
            migrationBuilder.AddColumn<Guid>(
                name: "InstalledVersionId",
                table: "UserPlugins",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstalledVersion",
                table: "UserPlugins",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedAt",
                table: "UserPlugins",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoUpdate",
                table: "UserPlugins",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PinnedVersion",
                table: "UserPlugins",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Add FK and index for UserPlugins -> PluginVersions
            migrationBuilder.CreateIndex(
                name: "IX_UserPlugins_InstalledVersionId",
                table: "UserPlugins",
                column: "InstalledVersionId");

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
            // Remove FK from UserPlugins
            migrationBuilder.DropForeignKey(
                name: "FK_UserPlugins_PluginVersions_InstalledVersionId",
                table: "UserPlugins");

            // Remove index from UserPlugins
            migrationBuilder.DropIndex(
                name: "IX_UserPlugins_InstalledVersionId",
                table: "UserPlugins");

            // Remove new columns from UserPlugins
            migrationBuilder.DropColumn(
                name: "InstalledVersionId",
                table: "UserPlugins");

            migrationBuilder.DropColumn(
                name: "InstalledVersion",
                table: "UserPlugins");

            migrationBuilder.DropColumn(
                name: "LastUpdatedAt",
                table: "UserPlugins");

            migrationBuilder.DropColumn(
                name: "AutoUpdate",
                table: "UserPlugins");

            migrationBuilder.DropColumn(
                name: "PinnedVersion",
                table: "UserPlugins");

            // Remove new columns from Plugins
            migrationBuilder.DropColumn(
                name: "LatestVersionId",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "VersionCount",
                table: "Plugins");

            // Drop PluginVersions table
            migrationBuilder.DropTable(
                name: "PluginVersions");
        }
    }
}
