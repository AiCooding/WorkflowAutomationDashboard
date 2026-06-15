using Microsoft.EntityFrameworkCore;
using WorkflowDashboard.Api.Models;

namespace WorkflowDashboard.Api.Data;

public class WorkflowDbContext : DbContext
{
    public WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : base(options) { }

    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<Pipeline> Pipelines => Set<Pipeline>();
    public DbSet<PipelineRun> PipelineRuns => Set<PipelineRun>();
    public DbSet<PipelineStepRun> PipelineStepRuns => Set<PipelineStepRun>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<AgentRunnerSettings> AgentRunnerSettings => Set<AgentRunnerSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Feature>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.Status).HasDefaultValue("backlog");
            e.Property(f => f.Priority).HasDefaultValue(0);
            e.HasOne(f => f.Repository)
                .WithMany()
                .HasForeignKey(f => f.RepositoryId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(f => f.RepositoryId);
        });

        modelBuilder.Entity<Repository>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Path).IsRequired();
            e.Property(r => r.Name).IsRequired();
            e.HasIndex(r => r.Path).IsUnique();
        });

        modelBuilder.Entity<Pipeline>(e =>
        {
            e.HasKey(p => p.Id);
        });

        modelBuilder.Entity<PipelineRun>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Status).HasDefaultValue("pending");
            e.HasOne(r => r.Pipeline)
                .WithMany()
                .HasForeignKey(r => r.PipelineId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Feature)
                .WithMany()
                .HasForeignKey(r => r.FeatureId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(r => r.Repository)
                .WithMany()
                .HasForeignKey(r => r.RepositoryId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => r.RepositoryId);
            e.HasIndex(r => r.Status);
        });

        modelBuilder.Entity<PipelineStepRun>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Status).HasDefaultValue("pending");
            e.HasOne(s => s.PipelineRun)
                .WithMany(r => r.StepRuns)
                .HasForeignKey(s => s.PipelineRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApprovalRequest>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Status).HasDefaultValue("pending");
            e.HasOne(a => a.PipelineRun)
                .WithMany(r => r.ApprovalRequests)
                .HasForeignKey(a => a.PipelineRunId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.StepRun)
                .WithMany()
                .HasForeignKey(a => a.StepRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentRunnerSettings>(e =>
        {
            e.HasKey(s => s.Id);
        });
    }
}
