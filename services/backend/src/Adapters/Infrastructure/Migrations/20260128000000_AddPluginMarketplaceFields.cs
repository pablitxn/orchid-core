using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPluginMarketplaceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "Icon",
                table: "Plugins",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Plugins",
                type: "character varying(500)",
                maxLength: 500,
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

            migrationBuilder.AddColumn<string>(
                name: "Version",
                table: "Plugins",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Compatibility",
                table: "Plugins",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_Category",
                table: "Plugins",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_Rating",
                table: "Plugins",
                column: "Rating");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Plugins_Category",
                table: "Plugins");

            migrationBuilder.DropIndex(
                name: "IX_Plugins_Rating",
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

            migrationBuilder.DropColumn(
                name: "Author",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "ReviewCount",
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
                name: "Version",
                table: "Plugins");

            migrationBuilder.DropColumn(
                name: "Compatibility",
                table: "Plugins");
        }
    }
}
