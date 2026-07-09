using AreWeDoomd.Domain.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AreWeDoomd.Infrastructure.Common.Persistence.Configurations;

public sealed class LlmSettingsConfiguration : IEntityTypeConfiguration<LlmSettings>
{
    public void Configure(EntityTypeBuilder<LlmSettings> settings)
    {
        settings.ToTable("LlmSettings");
        settings.HasKey(x => x.Id);
        settings.Property(x => x.Model).IsRequired().HasMaxLength(200);
        settings.Property(x => x.ScoringModel).IsRequired().HasMaxLength(200);
        settings.Property(x => x.ThinkingEnabled).IsRequired();
        settings.Property(x => x.ScoringTokensPerAccount).IsRequired();
        settings.Property(x => x.CompositionTokensPerPost).IsRequired();
        settings.Property(x => x.ReplyMaxTokens).IsRequired();
        settings.Property(x => x.UpdatedAt).IsRequired();
    }
}
