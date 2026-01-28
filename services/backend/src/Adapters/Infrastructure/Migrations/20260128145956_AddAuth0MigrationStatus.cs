using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuth0MigrationStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Auth0Connection",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Auth0LastSyncedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Auth0MigratedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Auth0MigrationError",
                table: "Users",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Auth0MigrationStatus",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotMigrated");

            migrationBuilder.AddColumn<string>(
                name: "Auth0UserId",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelPreference",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Settings",
                table: "UserPlugins",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Author",
                table: "Plugins",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Plugins",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Compatibility",
                table: "Plugins",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "Plugins",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InstallCount",
                table: "Plugins",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "Plugins",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Plugins",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Plugins",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Rating",
                table: "Plugins",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "ReviewCount",
                table: "Plugins",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Plugins",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Version",
                table: "Plugins",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FallbackModel",
                table: "Agents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelPreference",
                table: "Agents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Auth0MigrationStatus",
                table: "Users",
                column: "Auth0MigrationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Auth0UserId",
                table: "Users",
                column: "Auth0UserId",
                unique: true,
                filter: "\"Auth0UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_Category",
                table: "Plugins",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_InstallCount",
                table: "Plugins",
                column: "InstallCount");

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_IsFeatured",
                table: "Plugins",
                column: "IsFeatured");

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_IsPublic",
                table: "Plugins",
                column: "IsPublic");

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_Rating",
                table: "Plugins",
                column: "Rating");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Auth0MigrationStatus",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Auth0UserId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Plugins_Category",
                table: "Plugins");

            migrationBuilder.DropIndex(
                name: "IX_Plugins_InstallCount",
                table: "Plugins");

            migrationBuilder.DropIndex(
                name: "IX_Plugins_IsFeatured",
                table: "Plugins");

            migrationBuilder.DropIndex(
                name: "IX_Plugins_IsPublic",
                table: "Plugins");

            migrationBuilder.DropIndex(
                name: "IX_Plugins_Rating",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "Auth0Connection",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Auth0LastSyncedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Auth0MigratedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Auth0MigrationError",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Auth0MigrationStatus",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Auth0UserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ModelPreference",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Settings",
                table: "UserPlugins");

            migrationBuilder.DropColumn(
                name: "Author",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "Compatibility",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "InstallCount",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "ReviewCount",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "FallbackModel",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "ModelPreference",
                table: "Agents");
        }
    }
}
