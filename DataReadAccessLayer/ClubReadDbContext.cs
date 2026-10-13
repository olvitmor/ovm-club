using DataReadAccessLayer.Models;
using Microsoft.EntityFrameworkCore;

namespace DataReadAccessLayer;

public partial class ClubReadDbContext : DbContext
{
    public ClubReadDbContext(DbContextOptions<ClubReadDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Class> Classes { get; set; }

    public virtual DbSet<Enrollment> Enrollments { get; set; }

    public virtual DbSet<Pass> Passes { get; set; }

    public virtual DbSet<Room> Rooms { get; set; }

    public virtual DbSet<WaitlistEntry> WaitlistEntries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Class>(entity =>
        {
            entity.ToTable("classes");

            entity.HasIndex(e => e.RoomId, "IX_classes_room_id");

            entity.HasIndex(e => new { e.StartsAt, e.Id }, "IX_classes_starts_at_id_not_cancelled").HasFilter("(NOT is_cancelled)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Capacity).HasColumnName("capacity");
            entity.Property(e => e.DurationMinutes).HasColumnName("duration_minutes");
            entity.Property(e => e.IsCancelled).HasColumnName("is_cancelled");
            entity.Property(e => e.RoomId).HasColumnName("room_id");
            entity.Property(e => e.StartsAt).HasColumnName("starts_at");
            entity.Property(e => e.Title).HasColumnName("title");
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.ToTable("enrollments");

            entity.HasIndex(e => e.ClassId, "IX_enrollments_class_id");

            entity.HasIndex(e => e.ClassId, "IX_enrollments_class_id_active").HasFilter("(status = 'active'::text)");

            entity.HasIndex(e => e.UserId, "IX_enrollments_user_id");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CancelledAt).HasColumnName("cancelled_at");
            entity.Property(e => e.ClassId).HasColumnName("class_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.RefundedVisits).HasColumnName("refunded_visits");
            entity.Property(e => e.Seats).HasColumnName("seats");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.UserId).HasColumnName("user_id");
        });

        modelBuilder.Entity<Pass>(entity =>
        {
            entity.ToTable("passes");

            entity.HasIndex(e => e.UserId, "IX_passes_user_id");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.RemainingVisits).HasColumnName("remaining_visits");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ValidUntil).HasColumnName("valid_until");
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.ToTable("rooms");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        modelBuilder.Entity<WaitlistEntry>(entity =>
        {
            entity.ToTable("waitlist_entries");

            entity.HasIndex(e => new { e.ClassId, e.CreatedAt, e.Id }, "IX_waitlist_entries_class_id_queue_waiting").HasFilter("(status = 'waiting'::text)");

            entity.HasIndex(e => e.EnrollmentId, "IX_waitlist_entries_enrollment_id");

            entity.HasIndex(e => new { e.UserId, e.ClassId }, "IX_waitlist_entries_user_id_class_id_waiting")
                .IsUnique()
                .HasFilter("(status = 'waiting'::text)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CancelledAt).HasColumnName("cancelled_at");
            entity.Property(e => e.ClassId).HasColumnName("class_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.EnrollmentId).HasColumnName("enrollment_id");
            entity.Property(e => e.Seats).HasColumnName("seats");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.UserId).HasColumnName("user_id");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
