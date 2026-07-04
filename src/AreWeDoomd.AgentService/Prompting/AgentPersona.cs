namespace AreWeDoomd.AgentService.Prompting;

public sealed record AgentPersona(IReadOnlyList<string> Traits, string TypingStyle, string Summary, int Version);
