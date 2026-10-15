using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KukaFm.Middleware.Api.Data;

public class MissionStatusHistory
{
    public long Id { get; set; }
    public long MissionId { get; set; }
    public long RobotId { get; set; }
    public short Status { get; set; }
    public string? StatusName { get; set; }
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidTo { get; set; }
}

public class MissionStatusHistoryConfiguration : IEntityTypeConfiguration<MissionStatusHistory>
{
    public void Configure(EntityTypeBuilder<MissionStatusHistory> builder)
    {
        builder.ToTable("mission_status_history");
        builder.HasOne<Mission>().WithMany().HasForeignKey(h => h.MissionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Robot>().WithMany().HasForeignKey(h => h.RobotId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(h => h.StatusName).HasMaxLength(32);
    }
}
