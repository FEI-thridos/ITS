using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KukaFm.Middleware.Api.Data;

public class Mission
{
    public long Id { get; set; }
    public required string MissionCode { get; set; }
    public required string RequestId { get; set; }
    public required string MissionType { get; set; }
    public required string StartNode { get; set; }
    public required string TargetNode { get; set; }
    public required string ContainerCode { get; set; }
    public short Priority { get; set; }
    public long RobotId { get; set; }
    public Robot Robot { get; set; } = null!;
    public short Status { get; set; }
    public string? StatusName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public class MissionConfiguration : IEntityTypeConfiguration<Mission>
{
    public void Configure(EntityTypeBuilder<Mission> builder)
    {
        builder.ToTable("mission");
        builder.HasIndex(m => m.MissionCode).IsUnique();
        builder.HasIndex(m => m.RequestId).IsUnique();
        builder.HasOne(m => m.Robot).WithMany().HasForeignKey(m => m.RobotId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(m => m.MissionCode).HasMaxLength(32);
        builder.Property(m => m.RequestId).HasMaxLength(32);
        builder.Property(m => m.MissionType).HasMaxLength(20);
        builder.Property(m => m.StartNode).HasMaxLength(64);
        builder.Property(m => m.TargetNode).HasMaxLength(64);
        builder.Property(m => m.ContainerCode).HasMaxLength(64);
        builder.Property(m => m.StatusName).HasMaxLength(32);
    }
}
