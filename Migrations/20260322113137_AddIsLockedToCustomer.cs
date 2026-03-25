using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTKPM_FE.Migrations
{
    public partial class AddIsLockedToCustomer : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "tbl_customer",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLocked",
                table: "tbl_customer");
        }
    }
}
