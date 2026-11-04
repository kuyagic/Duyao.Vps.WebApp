using Duyao.HlsList.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Duyao.HlsList.Data;

public class HlsInfoConfig : IEntityTypeConfiguration<HlsInfo>
{
    public void Configure(EntityTypeBuilder<HlsInfo> builder)
    {
        builder.ToTable("hls_info");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(h => h.Guid)
            .HasColumnName("guid")
            .HasMaxLength(36)
            .IsRequired();

        builder.HasIndex(h => h.Guid)
            .IsUnique()
            .HasDatabaseName("ix_hls_info_guid");

        builder.Property(h => h.Title)
            .HasColumnName("title")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(h => h.Desc)
            .HasColumnName("desc")
            .HasMaxLength(1024);

        builder.Property(h => h.Img)
            .HasColumnName("img")
            .HasMaxLength(512);

        builder.Property(h => h.KeyData)
            .HasColumnName("key_data")
            .HasMaxLength(256);

        builder.Property(h => h.EntryHost)
            .HasColumnName("entry_host")
            .HasMaxLength(256);

        builder.Property(h => h.EntryPath)
            .HasColumnName("entry_path")
            .HasMaxLength(1024);

        builder.Property(h => h.CreateAt)
            .HasColumnName("create_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(h => h.UpdateAt)
            .HasColumnName("update_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(h => h.IsValid)
            .HasColumnName("is_valid")
            .HasDefaultValue(1);

        builder.HasIndex(h => h.Title).HasDatabaseName("ix_hls_info_title");

    }
}
