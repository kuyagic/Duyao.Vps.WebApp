using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duyao.VpnList.Migrations
{
    /// <inheritdoc />
    public partial class AddAsnOrg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "asn_org",
                table: "vpn_info",
                type: "TEXT",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "asn_org",
                table: "vpn_info");
        }
    }
}
