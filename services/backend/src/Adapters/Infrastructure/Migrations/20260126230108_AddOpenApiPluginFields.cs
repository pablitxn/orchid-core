using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenApiPluginFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseUrl",
                table: "Plugins",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenApiSpecCache",
                table: "Plugins",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpenApiSpecUrl",
                table: "Plugins",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PluginType",
                table: "Plugins",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "builtin");

            migrationBuilder.AddColumn<DateTime>(
                name: "SpecCachedAt",
                table: "Plugins",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeoutSeconds",
                table: "Plugins",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CreditPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Credits = table.Column<int>(type: "integer", nullable: false),
                    PriceInCents = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "usd"),
                    StripePriceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ExpirationDays = table.Column<int>(type: "integer", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditPackages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CreditPurchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditPackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditsGranted = table.Column<int>(type: "integer", nullable: false),
                    PricePaidCents = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "usd"),
                    StripeCheckoutSessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StripePaymentIntentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    PurchasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreditsExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditPurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditPurchases_CreditPackages_CreditPackageId",
                        column: x => x.CreditPackageId,
                        principalTable: "CreditPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditPurchases_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_PluginType",
                table: "Plugins",
                column: "PluginType");

            migrationBuilder.CreateIndex(
                name: "IX_CreditPackages_IsActive",
                table: "CreditPackages",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CreditPackages_StripePriceId",
                table: "CreditPackages",
                column: "StripePriceId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_CreditPackageId",
                table: "CreditPurchases",
                column: "CreditPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_Status",
                table: "CreditPurchases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_StripeCheckoutSessionId",
                table: "CreditPurchases",
                column: "StripeCheckoutSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_StripePaymentIntentId",
                table: "CreditPurchases",
                column: "StripePaymentIntentId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditPurchases_UserId",
                table: "CreditPurchases",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditPurchases");

            migrationBuilder.DropTable(
                name: "CreditPackages");

            migrationBuilder.DropIndex(
                name: "IX_Plugins_PluginType",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "BaseUrl",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "OpenApiSpecCache",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "OpenApiSpecUrl",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "PluginType",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "SpecCachedAt",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "TimeoutSeconds",
                table: "Plugins");
        }
    }
}
