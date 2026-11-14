using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Newlife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSermons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sermon_episodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    feed_guid = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description_html = table.Column<string>(type: "text", nullable: true),
                    summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    audio_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    artwork_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sermon_episodes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sermon_episodes_feed_guid",
                table: "sermon_episodes",
                column: "feed_guid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sermon_episodes_visible_published",
                table: "sermon_episodes",
                columns: new[] { "is_visible", "published_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sermon_episodes");
        }
    }
}
