namespace AreWeDoomd.AgentService.Prompting;

public interface IPersonaProvider
{
    /// <summary>Returns the persona for <paramref name="userId"/>. Never throws except OperationCanceledException when the caller's token is cancelled.</summary>
    Task<PersonaResolution> GetAsync(Guid userId, CancellationToken ct);
}
