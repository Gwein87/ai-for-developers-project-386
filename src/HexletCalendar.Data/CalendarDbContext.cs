using Microsoft.EntityFrameworkCore;

namespace HexletCalendar.Data;

public sealed class CalendarDbContext(DbContextOptions<CalendarDbContext> options) : DbContext(options)
{
    public DbSet<EventTypeEntity> EventTypes => Set<EventTypeEntity>();
    public DbSet<BookingEntity> Bookings => Set<BookingEntity>();

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
        modelBuilder.Entity<BookingEntity>(entity =>
        {
            entity.ToTable("Bookings", table => table.HasCheckConstraint("CK_Bookings_Interval", "EndAt > StartAt"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedOnAdd();
            entity.HasOne<EventTypeEntity>().WithMany().HasForeignKey(item => item.EventTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(item => item.EventTypeName).IsRequired().HasMaxLength(100);
            entity.Property(item => item.GuestName).IsRequired().HasMaxLength(100);
            entity.Property(item => item.GuestEmail).IsRequired().HasMaxLength(254);
            entity.Property(item => item.IdempotencyKey).IsRequired().HasMaxLength(36);
            entity.HasIndex(item => item.IdempotencyKey).IsUnique();
            entity.HasIndex(item => new { item.StartAt, item.EndAt });
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

public sealed class BookingEntity
{
    public int Id { get; set; }
    public int EventTypeId { get; set; }
    public required string EventTypeName { get; set; }
    public long StartAt { get; set; }
    public long EndAt { get; set; }
    public required string GuestName { get; set; }
    public required string GuestEmail { get; set; }
    public required string IdempotencyKey { get; set; }
}
