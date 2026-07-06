namespace AreWeDoomd.Api.Startup;

public sealed class DatabasePreflightOptions
{
    public const string SectionName = "Preflight:Database";

    /// <summary>Integration tests disable the preflight the same way they disable admin seeding.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Waits between attempts; total attempts = RetryDelays.Count + 1.</summary>
    public List<TimeSpan> RetryDelays { get; set; } = [];
}
