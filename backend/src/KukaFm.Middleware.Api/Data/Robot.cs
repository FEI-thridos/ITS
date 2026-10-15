using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KukaFm.Middleware.Api.Data;

public class Robot
{
    public long Id { get; set; }
    public required string RobotCode { get; set; }
    public string? RobotType { get; set; }
    public short Status { get; set; }
    public string? StatusName { get; set; }
    public decimal? BatteryLevel { get; set; }
    public string? MapCode { get; set; }
    public string? NodeCode { get; set; }
    public string? MissionCode { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class RobotConfiguration : IEntityTypeConfiguration<Robot>
{
    public void Configure(EntityTypeBuilder<Robot> builder)
    {
        builder.ToTable("robot");
        builder.HasIndex(r => r.RobotCode).IsUnique();
        builder.Property(r => r.RobotCode).HasMaxLength(32);
        builder.Property(r => r.RobotType).HasMaxLength(32);
        builder.Property(r => r.StatusName).HasMaxLength(32);
        builder.Property(r => r.BatteryLevel).HasPrecision(5, 2);
        builder.Property(r => r.MapCode).HasMaxLength(32);
        builder.Property(r => r.NodeCode).HasMaxLength(64);
        builder.Property(r => r.MissionCode).HasMaxLength(32);
    }
}
