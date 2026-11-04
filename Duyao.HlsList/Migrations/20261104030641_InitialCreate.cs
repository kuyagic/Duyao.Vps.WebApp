using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Duyao.HlsList.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hls_info",
                columns: table => new
                {
                    id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    desc = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    img = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    key_data = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    entry_host = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    entry_path = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    create_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    update_at = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    is_valid = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hls_info", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_hls_info_title",
                table: "hls_info",
                column: "title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hls_info");
        }
    }
}
