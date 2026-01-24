using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArtifactsAndReactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessagesV2",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ChatSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    ToolCallsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ArtifactsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ReactionsJson = table.Column<string>(type: "jsonb", nullable: true),
                    Timestamp = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessagesV2", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessagesV2_ChatSessions_ChatSessionId",
                        column: x => x.ChatSessionId,
                        principalTable: "ChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Emoji = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "emoji"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageArtifacts_MessagesV2_MessageId",
                        column: x => x.MessageId,
                        principalTable: "MessagesV2",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalendarArtifacts",
                columns: table => new
                {
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Events = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarArtifacts", x => x.ArtifactId);
                    table.ForeignKey(
                        name: "FK_CalendarArtifacts_MessageArtifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "MessageArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CanvasTextArtifacts",
                columns: table => new
                {
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasTextArtifacts", x => x.ArtifactId);
                    table.ForeignKey(
                        name: "FK_CanvasTextArtifacts_MessageArtifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "MessageArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChartArtifacts",
                columns: table => new
                {
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChartType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Data = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartArtifacts", x => x.ArtifactId);
                    table.ForeignKey(
                        name: "FK_ChartArtifacts_MessageArtifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "MessageArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImageArtifacts",
                columns: table => new
                {
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Src = table.Column<string>(type: "text", nullable: false),
                    Alt = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageArtifacts", x => x.ArtifactId);
                    table.ForeignKey(
                        name: "FK_ImageArtifacts_MessageArtifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "MessageArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MermaidArtifacts",
                columns: table => new
                {
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MermaidArtifacts", x => x.ArtifactId);
                    table.ForeignKey(
                        name: "FK_MermaidArtifacts_MessageArtifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "MessageArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TableArtifacts",
                columns: table => new
                {
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Data = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableArtifacts", x => x.ArtifactId);
                    table.ForeignKey(
                        name: "FK_TableArtifacts_MessageArtifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "MessageArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ThreeJsArtifacts",
                columns: table => new
                {
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    SceneData = table.Column<JsonDocument>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThreeJsArtifacts", x => x.ArtifactId);
                    table.ForeignKey(
                        name: "FK_ThreeJsArtifacts_MessageArtifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "MessageArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoArtifacts",
                columns: table => new
                {
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Src = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoArtifacts", x => x.ArtifactId);
                    table.ForeignKey(
                        name: "FK_VideoArtifacts_MessageArtifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "MessageArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageArtifacts_MessageId",
                table: "MessageArtifacts",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessagesV2_ChatSessionId",
                table: "MessagesV2",
                column: "ChatSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_MessagesV2_SessionId_Timestamp",
                table: "MessagesV2",
                columns: new[] { "SessionId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_TargetId_UserId_Emoji",
                table: "Reactions",
                columns: new[] { "TargetId", "UserId", "Emoji" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_TargetType_TargetId",
                table: "Reactions",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_UserId",
                table: "Reactions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarArtifacts");

            migrationBuilder.DropTable(
                name: "CanvasTextArtifacts");

            migrationBuilder.DropTable(
                name: "ChartArtifacts");

            migrationBuilder.DropTable(
                name: "ImageArtifacts");

            migrationBuilder.DropTable(
                name: "MermaidArtifacts");

            migrationBuilder.DropTable(
                name: "Reactions");

            migrationBuilder.DropTable(
                name: "TableArtifacts");

            migrationBuilder.DropTable(
                name: "ThreeJsArtifacts");

            migrationBuilder.DropTable(
                name: "VideoArtifacts");

            migrationBuilder.DropTable(
                name: "MessageArtifacts");

            migrationBuilder.DropTable(
                name: "MessagesV2");
        }
    }
}
