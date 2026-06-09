using Microsoft.EntityFrameworkCore;
using WorkflowDashboard.Api.Models;

namespace WorkflowDashboard.Api.Data;

public class WorkflowDbContext : DbContext
{
    public WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : base(options) { }

    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<InputRequest> InputRequests => Set<InputRequest>();
    public DbSet<Command> Commands => Set<Command>();
    public DbSet<WorkflowEvent> Events => Set<WorkflowEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Feature>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.Status).HasDefaultValue("backlog");
            e.Property(f => f.Priority).HasDefaultValue(0);
        });

        modelBuilder.Entity<Workflow>(e =>
        {
            e.HasKey(w => w.Id);
            e.Property(w => w.Status).HasDefaultValue("pending");
            e.HasOne(w => w.Feature)
                .WithMany(f => f.Workflows)
                .HasForeignKey(w => w.FeatureId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Agent>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Status).HasDefaultValue("idle");
            e.HasOne(a => a.Workflow)
                .WithMany(w => w.Agents)
                .HasForeignKey(a => a.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InputRequest>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Status).HasDefaultValue("pending");
            e.HasOne(i => i.Workflow)
                .WithMany(w => w.InputRequests)
                .HasForeignKey(i => i.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Agent)
                .WithMany()
                .HasForeignKey(i => i.AgentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Command>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Status).HasDefaultValue("pending");
            e.HasOne(c => c.Workflow)
                .WithMany(w => w.Commands)
                .HasForeignKey(c => c.WorkflowId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WorkflowEvent>(e =>
        {
            e.HasKey(ev => ev.Id);
            e.Property(ev => ev.Id).ValueGeneratedOnAdd();
        });
    }
}
