using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccessLayer.Configurations;

public class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("idempotency_keys");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Operation).HasColumnName("operation").IsRequired();
        builder.Property(x => x.Key).HasColumnName("key").IsRequired();
        builder.Property(x => x.RequestHash).HasColumnName("request_hash").IsRequired();
        builder.Property(x => x.ResponseBody).HasColumnName("response_body").HasColumnType("jsonb");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId);

        builder
            .HasIndex(x => new { x.UserId, x.Operation, x.Key })
            .IsUnique();

        builder.HasIndex(x => x.CreatedAt);
    }
}
