using Duyao.VpnList.Entity;
using Microsoft.EntityFrameworkCore;

namespace Duyao.VpnList.Data;

public class VpnDbContext : DbContext
{
    public VpnDbContext(DbContextOptions<VpnDbContext> options) : base(options)
    {
    }

    public DbSet<VpnInfo> VpnInfos => Set<VpnInfo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new VpnInfoConfig());
    }
}
