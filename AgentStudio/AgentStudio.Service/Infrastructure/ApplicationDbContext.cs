using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using AgentStudio.Service.Model;
using System.Text.Json;

namespace AgentStudio.Service.Infrastructure;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<WorkflowEntity> Workflows => Set<WorkflowEntity>();
    public DbSet<AgentNodeEntity> AgentNodes => Set<AgentNodeEntity>();
    public DbSet<WorkflowEdgeEntity> WorkflowEdges => Set<WorkflowEdgeEntity>();
    public DbSet<WorkflowExecutionEntity> WorkflowExecutions => Set<WorkflowExecutionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var stringListComparer = new ValueComparer<List<string>?>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c == null ? null : c.ToList());

        modelBuilder.Entity<WorkflowEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OwnerId).IsRequired();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => new { e.Id, e.OwnerId });
        });

        modelBuilder.Entity<AgentNodeEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OwnerId).IsRequired();
            entity.Property(e => e.FlowId).IsRequired();

            var toolsProp = entity.Property(e => e.McpEnabledTools)
                .HasConversion(
                    v => JsonSerializer.Serialize(v ?? new List<string>(), (JsonSerializerOptions?)null),
                    v => string.IsNullOrWhiteSpace(v)
                        ? new List<string>()
                        : (JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()));

            toolsProp.Metadata.SetValueComparer(stringListComparer);

            if (Database.IsNpgsql())
            {
                toolsProp.HasColumnType("jsonb");
            }

            entity.HasIndex(e => new { e.FlowId, e.OwnerId });
        });

        modelBuilder.Entity<WorkflowEdgeEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FlowId).IsRequired();
            entity.Property(e => e.SourceNodeId).IsRequired();
            entity.Property(e => e.TargetNodeId).IsRequired();
            entity.HasIndex(e => new { e.FlowId, e.OwnerId });
        });

        modelBuilder.Entity<WorkflowExecutionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FlowId).IsRequired();
            entity.Property(e => e.OwnerId).IsRequired();

            if (Database.IsNpgsql())
            {
                entity.Property(e => e.LogsJson).HasColumnType("jsonb");
            }

            entity.HasIndex(e => new { e.FlowId, e.OwnerId });
        });
    }
}
