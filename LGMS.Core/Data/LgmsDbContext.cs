using LGMS.Models;
using Microsoft.EntityFrameworkCore;

namespace LGMS.Data;

public sealed class LgmsDbContext : DbContext
{
    public LgmsDbContext(DbContextOptions<LgmsDbContext> options) : base(options)
    {
    }

    public DbSet<LocalRepository> LocalRepositories => Set<LocalRepository>();
    public DbSet<GitHubAccount> GitHubAccounts => Set<GitHubAccount>();
    public DbSet<GitOperationLog> GitOperationLogs => Set<GitOperationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<GitHubAccount>(entity =>
        {
            entity.HasIndex(x => new { x.Host, x.Username })
                  .IsUnique();
        });

        modelBuilder.Entity<LocalRepository>(entity =>
        {
            entity.HasIndex(x => x.LocalPath)
                  .IsUnique();
        });

        modelBuilder.Entity<GitOperationLog>(entity =>
        {
            entity.HasIndex(x => x.CreatedAt);
        });
    }
}
