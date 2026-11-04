using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Duyao.HlsList.Entity;

/// <summary>
/// HLS 视频记录，字段参考 record.json
/// </summary>
[Table("hls_info")]
public class HlsInfo
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>
    /// 对外暴露的 varchar guid，/player/{id} 使用
    /// </summary>
    [Column("guid")]
    [MaxLength(36)]
    public string Guid { get; set; } = "";

    [Column("title")]
    [MaxLength(256)]
    public string Title { get; set; } = "";

    [Column("desc")]
    [MaxLength(1024)]
    public string? Desc { get; set; }

    [Column("img")]
    [MaxLength(512)]
    public string? Img { get; set; }

    [Column("key_data")]
    [MaxLength(256)]
    public string? KeyData { get; set; }

    [Column("entry_host")]
    [MaxLength(256)]
    public string? EntryHost { get; set; }

    [Column("entry_path")]
    [MaxLength(1024)]
    public string? EntryPath { get; set; }

    [Column("create_at")]
    public DateTime CreateAt { get; set; } = DateTime.UtcNow;

    [Column("update_at")]
    public DateTime UpdateAt { get; set; } = DateTime.UtcNow;

    [Column("is_valid")]
    public int IsValid { get; set; } = 1;
}
