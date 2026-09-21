using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MerchantAdmin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryReturnedToOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InventoryReturned",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Stock_NonNegative",
                table: "Products",
                sql: "[Stock] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Stock_NonNegative",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "InventoryReturned",
                table: "Orders");
        }
    }
}
