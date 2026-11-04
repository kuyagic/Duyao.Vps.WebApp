using Microsoft.EntityFrameworkCore.Migrations;
using Newtonsoft.Json.Linq;

#nullable disable

namespace Duyao.HlsList.Migrations
{
    /// <inheritdoc />
    public partial class AddGuidAndSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "guid",
                table: "hls_info",
                type: "TEXT",
                maxLength: 36,
                nullable: false,
                defaultValue: "");

            // 为存量行生成随机 guid（在唯一索引创建前执行）
            migrationBuilder.Sql(
                "UPDATE hls_info SET guid = lower(hex(randomblob(4)))||'-'||lower(hex(randomblob(2)))||'-'||" +
                "lower(hex(randomblob(2)))||'-'||lower(hex(randomblob(2)))||'-'||lower(hex(randomblob(6))) " +
                "WHERE guid = '';");

            migrationBuilder.CreateIndex(
                name: "ix_hls_info_guid",
                table: "hls_info",
                column: "guid",
                unique: true);

            SeedOldData(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_hls_info_guid",
                table: "hls_info");

            migrationBuilder.DropColumn(
                name: "guid",
                table: "hls_info");
        }

        /// <summary>
        /// 导入 _olddata/hls-key 下的 json+key 作为初始数据
        /// </summary>
        private static void SeedOldData(MigrationBuilder migrationBuilder)
        {
            const string prefix = "Duyao.HlsList._olddata.hls_key.";
            var asm = typeof(AddGuidAndSeed).Assembly;

            foreach (var name in asm.GetManifestResourceNames()
                         .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                                     && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(n => n))
            {
                JObject json;
                using (var rs = asm.GetManifestResourceStream(name))
                using (var reader = new StreamReader(rs!))
                {
                    json = JObject.Parse(reader.ReadToEnd());
                }

                var title = json.Value<string>("title") ?? "";
                var entryUrl = json.Value<string>("entry_url") ?? "";
                if (string.IsNullOrEmpty(entryUrl))
                {
                    continue;
                }

                var uri = new Uri(entryUrl);

                // key 文件内容转 hex 存入 key_data
                var keyResource = name.Substring(0, name.Length - 5) + ".key";
                var keyData = "";
                var keyStream = asm.GetManifestResourceStream(keyResource);
                if (keyStream != null)
                {
                    using var ms = new MemoryStream();
                    keyStream.CopyTo(ms);
                    keyData = Convert.ToHexString(ms.ToArray()).ToLowerInvariant();
                }

                var guid = Guid.NewGuid().ToString("D");

                migrationBuilder.Sql(
                    "INSERT INTO hls_info (guid, title, desc, img, key_data, entry_host, entry_path, create_at, update_at, is_valid) " +
                    $"VALUES ({Q(guid)}, {Q(title)}, {Q(json.Value<string>("desc"))}, {Q(json.Value<string>("img"))}, " +
                    $"{Q(keyData)}, {Q(uri.Host)}, {Q(uri.PathAndQuery)}, datetime('now'), datetime('now'), 1);");
            }
        }

        private static string Q(string? s)
        {
            return s == null ? "NULL" : "'" + s.Replace("'", "''") + "'";
        }
    }
}
