using FoundryBilling.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoundryBilling.Api.Data;

public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<FoundryHub> FoundryHubs => Set<FoundryHub>();

    public DbSet<FoundryProject> FoundryProjects => Set<FoundryProject>();

    public DbSet<ModelDeployment> ModelDeployments => Set<ModelDeployment>();

    public DbSet<UsageMetricSlice> UsageMetricSlices => Set<UsageMetricSlice>();

    public DbSet<DailyUsageRollup> DailyUsageRollups => Set<DailyUsageRollup>();

    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
    }
}

internal sealed class FoundryHubConfiguration : IEntityTypeConfiguration<FoundryHub>
{
    public void Configure(EntityTypeBuilder<FoundryHub> builder)
    {
        builder.ToTable("FoundryHubs");

        builder.HasKey(hub => hub.Id);

        builder.Property(hub => hub.AzureResourceId).IsRequired();
        builder.Property(hub => hub.Name).IsRequired();
        builder.Property(hub => hub.SubscriptionId).IsRequired();
        builder.Property(hub => hub.ResourceGroup).IsRequired();
        builder.Property(hub => hub.Region).IsRequired();

        builder.HasIndex(hub => hub.AzureResourceId).IsUnique();

        builder.HasMany(hub => hub.Projects)
            .WithOne(project => project.Hub)
            .HasForeignKey(project => project.HubId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(hub => hub.Deployments)
            .WithOne(deployment => deployment.Hub)
            .HasForeignKey(deployment => deployment.HubId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FoundryProjectConfiguration : IEntityTypeConfiguration<FoundryProject>
{
    public void Configure(EntityTypeBuilder<FoundryProject> builder)
    {
        builder.ToTable("FoundryProjects");

        builder.HasKey(project => project.Id);

        builder.Property(project => project.AzureResourceId).IsRequired();
        builder.Property(project => project.Name).IsRequired();
    }
}

internal sealed class ModelDeploymentConfiguration : IEntityTypeConfiguration<ModelDeployment>
{
    public void Configure(EntityTypeBuilder<ModelDeployment> builder)
    {
        builder.ToTable("ModelDeployments");

        builder.HasKey(deployment => deployment.Id);

        builder.Property(deployment => deployment.AzureResourceId).IsRequired();
        builder.Property(deployment => deployment.DeploymentName).IsRequired();
        builder.Property(deployment => deployment.ModelName).IsRequired();
        builder.Property(deployment => deployment.Capacity).IsRequired();

        builder.HasIndex(deployment => deployment.AzureResourceId).IsUnique();

        builder.HasMany(deployment => deployment.UsageMetricSlices)
            .WithOne(slice => slice.Deployment)
            .HasForeignKey(slice => slice.DeploymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(deployment => deployment.DailyUsageRollups)
            .WithOne(rollup => rollup.Deployment)
            .HasForeignKey(rollup => rollup.DeploymentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UsageMetricSliceConfiguration : IEntityTypeConfiguration<UsageMetricSlice>
{
    public void Configure(EntityTypeBuilder<UsageMetricSlice> builder)
    {
        builder.ToTable("UsageMetricSlices");

        builder.HasKey(slice => slice.Id);

        builder.Property(slice => slice.IntervalMinutes)
            .HasDefaultValue(60);

        builder.HasIndex(slice => new
        {
            slice.Timestamp,
            slice.DeploymentId
        });
    }
}

internal sealed class DailyUsageRollupConfiguration : IEntityTypeConfiguration<DailyUsageRollup>
{
    public void Configure(EntityTypeBuilder<DailyUsageRollup> builder)
    {
        builder.ToTable("DailyUsageRollups");

        builder.HasKey(rollup => rollup.Id);

        builder.Property(rollup => rollup.Date)
            .HasColumnType("date");

        builder.HasIndex(rollup => new
        {
            rollup.Date,
            rollup.DeploymentId
        });
    }
}

internal sealed class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> builder)
    {
        builder.ToTable("SyncRuns");

        builder.HasKey(run => run.Id);

        builder.Property(run => run.StartedAt).IsRequired();
        builder.Property(run => run.Status).IsRequired();

        builder.HasIndex(run => run.StartedAt)
            .IsDescending();
    }
}
