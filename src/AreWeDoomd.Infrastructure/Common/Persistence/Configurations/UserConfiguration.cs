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
            .HasMaxLength(32);

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
                .HasMaxLength(2_000);

            pb.Property(x => x.UpdatedAt)
                .IsRequired();

            // If using separate table, ensure FK / key alignment
            pb.WithOwner()
              .HasForeignKey("UserId");

            pb.HasKey("UserId"); // one-to-one owned
        });

        builder.Navigation(x => x.Profile).IsRequired();
    }
}
