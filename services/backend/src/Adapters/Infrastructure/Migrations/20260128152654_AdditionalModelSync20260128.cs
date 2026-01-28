using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdditionalModelSync20260128 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowAutoDegradation",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "MfaEnabled",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MfaEnrolledAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MfaLastVerifiedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MfaMethod",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublisherId",
                table: "Plugins",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PluginReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    HelpfulCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PluginReviews_Plugins_PluginId",
                        column: x => x.PluginId,
                        principalTable: "Plugins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PluginReviews_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Publishers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Bio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    WebsiteUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SupportEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Publishers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Publishers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSocialIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderUserId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AvatarUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UnlinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSocialIdentities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSocialIdentities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PluginReviewFlags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginReviewFlags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PluginReviewFlags_PluginReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "PluginReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PluginReviewFlags_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PluginReviewHelpfulVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginReviewHelpfulVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PluginReviewHelpfulVotes_PluginReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "PluginReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PluginReviewHelpfulVotes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PluginSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublisherId = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ShortDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FullDescription = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tags = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ManifestUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RepositoryUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Compatibility = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PluginSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PluginSubmissions_Plugins_PluginId",
                        column: x => x.PluginId,
                        principalTable: "Plugins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PluginSubmissions_Publishers_PublisherId",
                        column: x => x.PublisherId,
                        principalTable: "Publishers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_PublisherId",
                table: "Plugins",
                column: "PublisherId");

            migrationBuilder.CreateIndex(
                name: "IX_PluginReviewFlags_ReviewId_UserId",
                table: "PluginReviewFlags",
                columns: new[] { "ReviewId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PluginReviewFlags_UserId",
                table: "PluginReviewFlags",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PluginReviewHelpfulVotes_ReviewId_UserId",
                table: "PluginReviewHelpfulVotes",
                columns: new[] { "ReviewId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PluginReviewHelpfulVotes_UserId",
                table: "PluginReviewHelpfulVotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PluginReviews_IsDeleted",
                table: "PluginReviews",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_PluginReviews_PluginId",
                table: "PluginReviews",
                column: "PluginId");

            migrationBuilder.CreateIndex(
                name: "IX_PluginReviews_PluginId_UserId",
                table: "PluginReviews",
                columns: new[] { "PluginId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PluginReviews_UserId",
                table: "PluginReviews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PluginSubmissions_PluginId",
                table: "PluginSubmissions",
                column: "PluginId");

            migrationBuilder.CreateIndex(
                name: "IX_PluginSubmissions_PublisherId",
                table: "PluginSubmissions",
                column: "PublisherId");

            migrationBuilder.CreateIndex(
                name: "IX_PluginSubmissions_Slug",
                table: "PluginSubmissions",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "IX_PluginSubmissions_Status",
                table: "PluginSubmissions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Publishers_UserId",
                table: "Publishers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSocialIdentities_Provider_ProviderUserId",
                table: "UserSocialIdentities",
                columns: new[] { "Provider", "ProviderUserId" },
                unique: true,
                filter: "\"UnlinkedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserSocialIdentities_UserId",
                table: "UserSocialIdentities",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSocialIdentities_UserId_Provider",
                table: "UserSocialIdentities",
                columns: new[] { "UserId", "Provider" },
                unique: true,
                filter: "\"UnlinkedAt\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Plugins_Publishers_PublisherId",
                table: "Plugins",
                column: "PublisherId",
                principalTable: "Publishers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Plugins_Publishers_PublisherId",
                table: "Plugins");

            migrationBuilder.DropTable(
                name: "PluginReviewFlags");

            migrationBuilder.DropTable(
                name: "PluginReviewHelpfulVotes");

            migrationBuilder.DropTable(
                name: "PluginSubmissions");

            migrationBuilder.DropTable(
                name: "UserSocialIdentities");

            migrationBuilder.DropTable(
                name: "PluginReviews");

            migrationBuilder.DropTable(
                name: "Publishers");

            migrationBuilder.DropIndex(
                name: "IX_Plugins_PublisherId",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "AllowAutoDegradation",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MfaEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MfaEnrolledAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MfaLastVerifiedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MfaMethod",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PublisherId",
                table: "Plugins");
        }
    }
}
