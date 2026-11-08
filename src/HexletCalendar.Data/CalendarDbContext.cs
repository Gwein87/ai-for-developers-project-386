using Microsoft.EntityFrameworkCore;

namespace HexletCalendar.Data;

public sealed class CalendarDbContext(DbContextOptions<CalendarDbContext> options) : DbContext(options)
{
    public DbSet<EventTypeEntity> EventTypes => Set<EventTypeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<EventTypeEntity>(entity =>
        {
            entity.ToTable("EventTypes", table =>
            {
                table.HasCheckConstraint("CK_EventTypes_Name", "length(Name) BETWEEN 1 AND 100");
                table.HasCheckConstraint("CK_EventTypes_Description", "length(Description) BETWEEN 1 AND 2000");
                table.HasCheckConstraint("CK_EventTypes_Duration", "DurationMinutes BETWEEN 15 AND 540 AND DurationMinutes % 15 = 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
            entity.Property(item => item.Name).IsRequired().HasMaxLength(100);
            entity.Property(item => item.Description).IsRequired().HasMaxLength(2000);
        });
    }
}

public sealed class EventTypeEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int DurationMinutes { get; set; }
}
