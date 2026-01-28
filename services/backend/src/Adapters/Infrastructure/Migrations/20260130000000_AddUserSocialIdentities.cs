using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSocialIdentities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
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

            // Index for user lookups
            migrationBuilder.CreateIndex(
                name: "IX_UserSocialIdentities_UserId",
                table: "UserSocialIdentities",
                column: "UserId");

            // Unique constraint: one provider per user (for active identities)
            migrationBuilder.CreateIndex(
                name: "IX_UserSocialIdentities_UserId_Provider",
                table: "UserSocialIdentities",
                columns: new[] { "UserId", "Provider" },
                unique: true,
                filter: "\"UnlinkedAt\" IS NULL");

            // Unique constraint: provider user ID (for active identities)
            migrationBuilder.CreateIndex(
                name: "IX_UserSocialIdentities_Provider_ProviderUserId",
                table: "UserSocialIdentities",
                columns: new[] { "Provider", "ProviderUserId" },
                unique: true,
                filter: "\"UnlinkedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSocialIdentities");
        }
    }
}
