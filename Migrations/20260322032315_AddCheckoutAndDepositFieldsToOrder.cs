using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTKPM_FE.Migrations
{
    public partial class AddCheckoutAndDepositFieldsToOrder : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "tbl_order",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "DepositAmount",
                table: "tbl_order",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "tbl_order",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "tbl_order",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeposited",
                table: "tbl_order",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "tbl_order",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "tbl_order",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "tbl_order");

            migrationBuilder.DropColumn(
                name: "DepositAmount",
                table: "tbl_order");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "tbl_order");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "tbl_order");

            migrationBuilder.DropColumn(
                name: "IsDeposited",
                table: "tbl_order");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "tbl_order");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "tbl_order");
        }
    }
}
