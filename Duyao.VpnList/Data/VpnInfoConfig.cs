using Duyao.VpnList.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duyao.VpnList.Data;

public class VpnInfoConfig : IEntityTypeConfiguration<VpnInfo>
{
    public void Configure(EntityTypeBuilder<VpnInfo> builder)
    {
        builder.ToTable("vpn_info");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(v => v.Ip)
            .HasColumnName("ip")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(v => v.Port)
            .HasColumnName("port")
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(v => v.HostName)
            .HasColumnName("host_name")
            .HasMaxLength(256);

        builder.Property(v => v.Country)
            .HasColumnName("country")
            .HasMaxLength(64);

        builder.Property(v => v.AsnOrg)
            .HasColumnName("asn_org")
            .HasMaxLength(256);

        builder.Property(v => v.Ext1)
            .HasColumnName("ext_1")
            .HasMaxLength(256);

        builder.Property(v => v.Ext2)
            .HasColumnName("ext_2")
            .HasMaxLength(256);

        builder.Property(v => v.CreateAt)
            .HasColumnName("create_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(v => v.UpdateAt)
            .HasColumnName("update_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(v => v.IsValid)
            .HasColumnName("is_valid")
            .HasDefaultValue(1);

        // 用于 ip+port 联合判定同一条记录
        builder.HasIndex(v => new { v.Ip, v.Port }).HasDatabaseName("ix_vpn_info_ip_port");
    }
}
