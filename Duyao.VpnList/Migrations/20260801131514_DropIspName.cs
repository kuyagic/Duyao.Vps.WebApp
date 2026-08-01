using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duyao.VpnList.Migrations
{
    /// <inheritdoc />
    public partial class DropIspName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "isp_name",
                table: "vpn_info");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "isp_name",
                table: "vpn_info",
                type: "TEXT",
                maxLength: 128,
                nullable: true);
        }
    }
}
