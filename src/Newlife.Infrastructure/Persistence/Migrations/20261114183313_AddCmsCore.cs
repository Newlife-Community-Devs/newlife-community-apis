using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Newlife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCmsCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "announcements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    is_pinned = table.Column<bool>(type: "boolean", nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    publish_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "media_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    alt_text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_assets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "site_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    group = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    venue = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    banner_media_id = table.Column<Guid>(type: "uuid", nullable: true),
                    banner_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    is_featured = table.Column<bool>(type: "boolean", nullable: false),
                    ticketing_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_events_media_assets_banner_media_id",
                        column: x => x.banner_media_id,
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ticket_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    max_per_order = table.Column<int>(type: "integer", nullable: false),
                    sales_start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sales_end_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ticket_types", x => x.id);
                    table.ForeignKey(
                        name: "FK_ticket_types_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_announcements_is_published_is_pinned_publish_at",
                table: "announcements",
                columns: new[] { "is_published", "is_pinned", "publish_at" });

            migrationBuilder.CreateIndex(
                name: "IX_events_banner_media_id",
                table: "events",
                column: "banner_media_id");

            migrationBuilder.CreateIndex(
                name: "IX_events_is_published_start_at",
                table: "events",
                columns: new[] { "is_published", "start_at" });

            migrationBuilder.CreateIndex(
                name: "IX_events_slug",
                table: "events",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_storage_key",
                table: "media_assets",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_site_settings_key",
                table: "site_settings",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ticket_types_event_id_is_active",
                table: "ticket_types",
                columns: new[] { "event_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcements");

            migrationBuilder.DropTable(
                name: "site_settings");

            migrationBuilder.DropTable(
                name: "ticket_types");

            migrationBuilder.DropTable(
                name: "events");

            migrationBuilder.DropTable(
                name: "media_assets");
        }
    }
}
