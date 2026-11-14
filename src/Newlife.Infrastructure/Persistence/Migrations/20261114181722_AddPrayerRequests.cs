using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Newlife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrayerRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "prayer_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    submitter_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    is_anonymous = table.Column<bool>(type: "boolean", nullable: false),
                    is_answered = table.Column<bool>(type: "boolean", nullable: false),
                    answered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_flagged = table.Column<bool>(type: "boolean", nullable: false),
                    flag_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prayer_requests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_prayer_requests_deleted_at",
                table: "prayer_requests",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "ix_prayer_requests_is_answered",
                table: "prayer_requests",
                column: "is_answered");

            migrationBuilder.CreateIndex(
                name: "ix_prayer_requests_wall",
                table: "prayer_requests",
                columns: new[] { "is_flagged", "deleted_at", "created_at" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prayer_requests");
        }
    }
}
