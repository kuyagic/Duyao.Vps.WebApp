using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duyao.VpnList.Migrations
{
    /// <inheritdoc />
    public partial class AddIsStar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "is_star",
                table: "vpn_info",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_star",
                table: "vpn_info");
        }
    }
}
