using Microsoft.EntityFrameworkCore;
using AgentStudio.Service.Data.Entities;

namespace AgentStudio.Service.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<AgentNodeEntity> AgentNodes => Set<AgentNodeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AgentNodeEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OwnerId).IsRequired();
            entity.Property(e => e.FlowId).IsRequired();
            entity.Property(e => e.McpEnabledTools).HasColumnType("jsonb");
        });
    }
}