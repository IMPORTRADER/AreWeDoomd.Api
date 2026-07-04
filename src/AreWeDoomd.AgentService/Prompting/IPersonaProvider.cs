namespace AreWeDoomd.AgentService.Prompting;

public interface IPersonaProvider
{
    /// <summary>Returns the persona for <paramref name="userId"/>. Never throws.</summary>
    Task<PersonaResolution> GetAsync(Guid userId, CancellationToken ct);
}
