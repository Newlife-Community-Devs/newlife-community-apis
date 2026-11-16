using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Newlife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "stock_reserved",
                table: "ticket_types",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "stock_reserved",
                table: "ticket_types");
        }
    }
}
