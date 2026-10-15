using Microsoft.EntityFrameworkCore;

namespace KukaFm.Middleware.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Robot> Robots => Set<Robot>();
    public DbSet<RobotStatusHistory> RobotStatusHistory => Set<RobotStatusHistory>();
    public DbSet<Mission> Missions => Set<Mission>();
    public DbSet<MissionStatusHistory> MissionStatusHistory => Set<MissionStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
