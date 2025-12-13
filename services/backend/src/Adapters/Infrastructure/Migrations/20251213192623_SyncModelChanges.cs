using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentPlugins_Agents_AgentId1",
                table: "AgentPlugins");

            migrationBuilder.DropForeignKey(
                name: "FK_AgentPlugins_Plugins_PluginId1",
                table: "AgentPlugins");

            migrationBuilder.DropIndex(
                name: "IX_AgentPlugins_AgentId1",
                table: "AgentPlugins");

            migrationBuilder.DropIndex(
                name: "IX_AgentPlugins_PluginId1",
                table: "AgentPlugins");

            migrationBuilder.DropColumn(
                name: "AgentId1",
                table: "AgentPlugins");

            migrationBuilder.DropColumn(
                name: "PluginId1",
                table: "AgentPlugins");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AgentId1",
                table: "AgentPlugins",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PluginId1",
                table: "AgentPlugins",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_AgentPlugins_AgentId1",
                table: "AgentPlugins",
                column: "AgentId1");

            migrationBuilder.CreateIndex(
                name: "IX_AgentPlugins_PluginId1",
                table: "AgentPlugins",
                column: "PluginId1");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentPlugins_Agents_AgentId1",
                table: "AgentPlugins",
                column: "AgentId1",
                principalTable: "Agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AgentPlugins_Plugins_PluginId1",
                table: "AgentPlugins",
                column: "PluginId1",
                principalTable: "Plugins",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
