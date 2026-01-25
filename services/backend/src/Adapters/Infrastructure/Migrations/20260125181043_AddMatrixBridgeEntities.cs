using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMatrixBridgeEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAt",
                table: "StripeWebhookEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MatrixLinkCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatrixLinkCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatrixLinkCodes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatrixUserLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatrixUserId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatrixUserLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatrixUserLinks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StripeWebhookEvents_NextRetryAt",
                table: "StripeWebhookEvents",
                column: "NextRetryAt");

            migrationBuilder.CreateIndex(
                name: "IX_MatrixLinkCodes_Code",
                table: "MatrixLinkCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatrixLinkCodes_ExpiresAt",
                table: "MatrixLinkCodes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_MatrixLinkCodes_UserId",
                table: "MatrixLinkCodes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MatrixUserLinks_MatrixUserId",
                table: "MatrixUserLinks",
                column: "MatrixUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatrixUserLinks_UserId_IsActive",
                table: "MatrixUserLinks",
                columns: new[] { "UserId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatrixLinkCodes");

            migrationBuilder.DropTable(
                name: "MatrixUserLinks");

            migrationBuilder.DropIndex(
                name: "IX_StripeWebhookEvents_NextRetryAt",
                table: "StripeWebhookEvents");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "StripeWebhookEvents");
        }
    }
}
