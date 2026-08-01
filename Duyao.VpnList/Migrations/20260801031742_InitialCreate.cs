using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duyao.VpnList.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vpn_info",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ip = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    port = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    host_name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    country = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    isp_name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ext_1 = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ext_2 = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    create_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    update_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    is_valid = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vpn_info", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vpn_info_ip_port",
                table: "vpn_info",
                columns: new[] { "ip", "port" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vpn_info");
        }
    }
}
