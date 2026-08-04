using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Duyao.VpnList.Entity;

[Table("vpn_info")]
public class VpnInfo
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("ip")]
    [Required]
    [MaxLength(64)]
    public string Ip { get; set; } = "";

    [Column("port")]
    [Required]
    [MaxLength(8)]
    public string Port { get; set; } = "";

    [Column("host_name")]
    [MaxLength(256)]
    public string? HostName { get; set; }

    [Column("country")]
    [MaxLength(64)]
    public string? Country { get; set; }

    [Column("asn_org")]
    [MaxLength(256)]
    public string? AsnOrg { get; set; }

    [Column("ext_1")]
    [MaxLength(256)]
    public string? Ext1 { get; set; }

    [Column("ext_2")]
    [MaxLength(256)]
    public string? Ext2 { get; set; }

    [Column("create_at")]
    public DateTime CreateAt { get; set; } = DateTime.UtcNow;

    [Column("update_at")]
    public DateTime UpdateAt { get; set; } = DateTime.UtcNow;

    [Column("is_valid")]
    public int IsValid { get; set; } = 1;

    [Column("is_star")]
    public int IsStar { get; set; } = 0;
}
