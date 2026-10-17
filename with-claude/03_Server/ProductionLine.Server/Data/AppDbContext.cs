using Microsoft.EntityFrameworkCore;
namespace ProductionLine.Server.Data;
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<UserLineAccessEntity> UserLineAccesses => Set<UserLineAccessEntity>();
    public DbSet<LineEntity> Lines => Set<LineEntity>();
    public DbSet<InspectionEntity> Inspections => Set<InspectionEntity>();
    public DbSet<IssueEntity> Issues => Set<IssueEntity>();
    public DbSet<IssueHistoryEntity> IssueHistories => Set<IssueHistoryEntity>();
    public DbSet<IssueMessageEntity> IssueMessages => Set<IssueMessageEntity>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<UserLineAccessEntity>().HasKey(x => new { x.UserId, x.LineId });
        modelBuilder.Entity<UserLineAccessEntity>().HasOne<UserEntity>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UserLineAccessEntity>().HasOne<LineEntity>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InspectionEntity>().HasOne<LineEntity>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<IssueEntity>().HasOne<LineEntity>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<IssueEntity>().HasOne<UserEntity>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<IssueHistoryEntity>().HasOne<IssueEntity>().WithMany().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<IssueHistoryEntity>().HasOne<UserEntity>().WithMany().HasForeignKey(x => x.ChangedBy).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<IssueMessageEntity>()
            .HasOne<IssueEntity>().WithMany().HasForeignKey(x => x.IssueId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<IssueMessageEntity>()
            .HasOne<UserEntity>().WithMany().HasForeignKey(x => x.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<IssueMessageEntity>()
            .HasIndex(x => new { x.IssueId, x.Id });
        modelBuilder.Entity<IssueMessageEntity>().Property(x => x.Message)
            .IsRequired().HasMaxLength(2000);
        modelBuilder.Entity<IssueMessageEntity>().ToTable(table =>
            table.HasCheckConstraint("CK_IssueMessages_MessageLength", "length(\"Message\") BETWEEN 1 AND 2000"));
        modelBuilder.Entity<IssueMessageEntity>().Property(x => x.CreatedAt)
            .HasConversion(value => value.UtcDateTime.Ticks,
                value => new DateTimeOffset(value, TimeSpan.Zero));
    }
}
