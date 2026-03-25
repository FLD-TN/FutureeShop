using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTKPM_FE.Migrations
{
    public partial class AddOrderReasonFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "tbl_order",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "tbl_order",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "tbl_order");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "tbl_order");
        }
    }
}
