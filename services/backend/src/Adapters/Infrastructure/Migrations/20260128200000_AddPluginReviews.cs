using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPluginReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create PluginReviews table
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

            // Create PluginReviewFlags table
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

            // Create PluginReviewHelpfulVotes table
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

            // Create indexes for PluginReviews
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
                name: "IX_PluginReviews_IsDeleted",
                table: "PluginReviews",
                column: "IsDeleted");

            // Create indexes for PluginReviewFlags
            migrationBuilder.CreateIndex(
                name: "IX_PluginReviewFlags_ReviewId_UserId",
                table: "PluginReviewFlags",
                columns: new[] { "ReviewId", "UserId" },
                unique: true);

            // Create indexes for PluginReviewHelpfulVotes
            migrationBuilder.CreateIndex(
                name: "IX_PluginReviewHelpfulVotes_ReviewId_UserId",
                table: "PluginReviewHelpfulVotes",
                columns: new[] { "ReviewId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PluginReviewHelpfulVotes");
            migrationBuilder.DropTable(name: "PluginReviewFlags");
            migrationBuilder.DropTable(name: "PluginReviews");
        }
    }
}
