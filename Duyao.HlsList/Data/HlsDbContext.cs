using Duyao.HlsList.Entity;
using Microsoft.EntityFrameworkCore;

namespace Duyao.HlsList.Data;

public class HlsDbContext : DbContext
{
    public HlsDbContext(DbContextOptions<HlsDbContext> options) : base(options)
    {
    }

    public DbSet<HlsInfo> HlsInfos => Set<HlsInfo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new HlsInfoConfig());
    }
}
