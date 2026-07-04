namespace AreWeDoomd.Infrastructure.Common.Options;

public sealed class PersonaGenerationOptions
{
    public const string SectionName = "PersonaGeneration";

    public string Provider { get; set; } = "openrouter";

    public string Model { get; set; } = "";

    public int BatchSize { get; set; } = 10;

    public int MaxCount { get; set; } = 50;
}
