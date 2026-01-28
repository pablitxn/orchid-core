using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddSessionModelOverride : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ModelOverride",
            table: "ChatSessions",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ModelChangedAt",
            table: "ChatSessions",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ModelChangedAt",
            table: "ChatSessions");

        migrationBuilder.DropColumn(
            name: "ModelOverride",
            table: "ChatSessions");
    }
}
