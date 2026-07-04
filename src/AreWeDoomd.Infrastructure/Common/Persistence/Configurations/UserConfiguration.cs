using AreWeDoomd.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AreWeDoomd.Infrastructure.Common.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Username)
            .IsRequired()
            .HasMaxLength(24);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(254);

        builder.Property(x => x.PasswordHash)
            .IsRequired();

        builder.Property(x => x.UserType)
            .IsRequired()
            .HasConversion<int>(); // enum stored as int

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        // Uniqueness (recommended)
        builder.HasIndex(x => x.Username).IsUnique();
        builder.HasIndex(x => x.Email).IsUnique();

        // Profile as Owned (value object-ish)
        // NOTE: this assumes: User always has a Profile (required).
        // If you decide Profile is optional, remove IsRequired() and make Profile nullable in domain.
        builder.OwnsOne(x => x.Profile, pb =>
        {
            pb.ToTable("UserProfiles"); // optional: separate table; remove for same-table owned columns

            pb.Property(x => x.ProfileImageUrl)
                .HasMaxLength(2048);

            pb.Property(x => x.Biography)
                .HasMaxLength(160);

            pb.Property(x => x.UpdatedAt)
                .IsRequired();

            // If using separate table, ensure FK / key alignment
            pb.WithOwner()
              .HasForeignKey("UserId");

            pb.HasKey("UserId"); // one-to-one owned
        });

        builder.Navigation(x => x.Profile).IsRequired();

        builder.OwnsOne(x => x.AiPersonality, ab =>
        {
            ab.ToTable("AiPersonalities");
            ab.WithOwner().HasForeignKey("UserId");
            ab.HasKey("UserId");
            ab.Property(x => x.TraitsJson).IsRequired();
            ab.Property(x => x.TypingStyle).HasMaxLength(AiPersonality.MaxTypingStyleLength).IsRequired();
            ab.Property(x => x.Summary).HasMaxLength(AiPersonality.MaxSummaryLength).IsRequired();
            ab.Property(x => x.Version).IsRequired();
            ab.Property(x => x.SchedulePrefsJson);
            ab.Property(x => x.TrendPrefsJson);
            ab.Property(x => x.UpdatedAt).IsRequired();
            ab.Ignore(x => x.Traits);
        });

        builder.Property(x => x.IsAdmin).IsRequired().HasDefaultValue(false);

        builder.Property(x => x.CreatedByBulkJobId);
        builder.HasIndex(x => x.CreatedByBulkJobId)
            .HasFilter("[CreatedByBulkJobId] IS NOT NULL");
    }
}
