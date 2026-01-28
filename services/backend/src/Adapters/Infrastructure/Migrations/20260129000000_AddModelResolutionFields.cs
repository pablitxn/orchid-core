using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModelResolutionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add FallbackModel to Agents table
            migrationBuilder.AddColumn<string>(
                name: "FallbackModel",
                table: "Agents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Add ModelPreference enum to Agents table (stored as string)
            migrationBuilder.AddColumn<string>(
                name: "ModelPreference",
                table: "Agents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Add ModelPreference to Users table for user-level defaults
            migrationBuilder.AddColumn<string>(
                name: "ModelPreference",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FallbackModel",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "ModelPreference",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "ModelPreference",
                table: "Users");
        }
    }
}
