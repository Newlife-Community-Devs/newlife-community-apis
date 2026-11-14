using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Newlife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hubtel_callback_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    client_reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    processing_result = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hubtel_callback_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    order_token = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    buyer_full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    buyer_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    buyer_phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    hubtel_sales_invoice_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    hubtel_transaction_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    hubtel_refund_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    payment_method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    refunded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    requires_review = table.Column<bool>(type: "boolean", nullable: false),
                    review_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    hold_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_orders_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_items_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_order_items_ticket_types_ticket_type_id",
                        column: x => x.ticket_type_id,
                        principalTable: "ticket_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_payment_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_reference = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    hubtel_checkout_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    checkout_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_payment_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_payment_attempts_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tickets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    holder_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    checked_in_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    checked_in_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tickets", x => x.id);
                    table.ForeignKey(
                        name: "FK_tickets_AspNetUsers_checked_in_by_user_id",
                        column: x => x.checked_in_by_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tickets_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tickets_order_items_order_item_id",
                        column: x => x.order_item_id,
                        principalTable: "order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tickets_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tickets_ticket_types_ticket_type_id",
                        column: x => x.ticket_type_id,
                        principalTable: "ticket_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hubtel_callback_logs_client_reference",
                table: "hubtel_callback_logs",
                column: "client_reference");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_order_id_ticket_type_id",
                table: "order_items",
                columns: new[] { "order_id", "ticket_type_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_items_ticket_type_id",
                table: "order_items",
                column: "ticket_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_payment_attempts_client_reference",
                table: "order_payment_attempts",
                column: "client_reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_payment_attempts_order_id",
                table: "order_payment_attempts",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_event_id_status",
                table: "orders",
                columns: new[] { "event_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_order_number",
                table: "orders",
                column: "order_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_order_token",
                table: "orders",
                column: "order_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_status_created_at",
                table: "orders",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_hubtel_transaction_id",
                table: "orders",
                column: "hubtel_transaction_id",
                unique: true,
                filter: "hubtel_transaction_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_checked_in_by_user_id",
                table: "tickets",
                column: "checked_in_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_code",
                table: "tickets",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tickets_event_id_status",
                table: "tickets",
                columns: new[] { "event_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_tickets_order_id",
                table: "tickets",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_order_item_id",
                table: "tickets",
                column: "order_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_ticket_type_id",
                table: "tickets",
                column: "ticket_type_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hubtel_callback_logs");

            migrationBuilder.DropTable(
                name: "order_payment_attempts");

            migrationBuilder.DropTable(
                name: "tickets");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "orders");
        }
    }
}
