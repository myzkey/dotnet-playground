using Microsoft.EntityFrameworkCore;
using TaskApi.Domain.Entities;

namespace TaskApi.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var task = modelBuilder.Entity<TaskItem>();
        task.ToTable("Tasks", table =>
            table.HasCheckConstraint("CK_Tasks_Title_NotBlank", "length(btrim(\"Title\")) > 0"));
        task.HasKey(item => item.Id);
        task.Property(item => item.Title).IsRequired().HasMaxLength(200);

        // NpgsqlはOffset=0のDateTimeOffsetをtimestamptzへマッピングする。
        // PostgreSQLは時点を保存し、元のタイムゾーン名やオフセットは保存しない。
        task.Property(item => item.CreatedAt).HasColumnType("timestamp with time zone");
        task.Property(item => item.UpdatedAt).HasColumnType("timestamp with time zone");
    }
}
