namespace AreWeDoomd.AgentService.Prompting;

public interface ILlmSettingsProvider
{
    /// <summary>60 sn cache'li; API erişilemezse son bilinen değer, o da yoksa güvenli varsayılanlar.</summary>
    Task<LlmRuntimeSettings> GetAsync(Guid actingAiUserId, CancellationToken ct);
}
