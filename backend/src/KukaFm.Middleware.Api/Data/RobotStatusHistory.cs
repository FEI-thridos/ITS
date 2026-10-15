using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KukaFm.Middleware.Api.Data;

public class RobotStatusHistory
{
    public long Id { get; set; }
    public long RobotId { get; set; }
    public short Status { get; set; }
    public string? StatusName { get; set; }
    public decimal? BatteryLevel { get; set; }
    public string? MapCode { get; set; }
    public string? NodeCode { get; set; }
    public string? MissionCode { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidTo { get; set; }
}

public class RobotStatusHistoryConfiguration : IEntityTypeConfiguration<RobotStatusHistory>
{
    public void Configure(EntityTypeBuilder<RobotStatusHistory> builder)
    {
        builder.ToTable("robot_status_history");
        builder.HasOne<Robot>().WithMany().HasForeignKey(h => h.RobotId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(h => h.StatusName).HasMaxLength(32);
        builder.Property(h => h.BatteryLevel).HasPrecision(5, 2);
        builder.Property(h => h.MapCode).HasMaxLength(32);
        builder.Property(h => h.NodeCode).HasMaxLength(64);
        builder.Property(h => h.MissionCode).HasMaxLength(32);
    }
}
