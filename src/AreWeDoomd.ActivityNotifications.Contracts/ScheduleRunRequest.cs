namespace AreWeDoomd.ActivityNotifications.Contracts;

/// <summary>
/// API → AgentService: "bu hesaplar için bugünün gönderi planını LLM'e sor" isteği.
/// Threshold burada YALNIZ Aşama 2'yi (içerik üretimi) kapılamak için taşınır;
/// otoriter karşılaştırma her zaman API'de, run'a snapshot'lanmış eşikle yapılır.
/// Strategy: 0 = TwoStage, 1 = SingleCall (LlmSchedulingStrategy ile aynı değerler;
/// Contracts projesi Domain'e referans veremediği için int taşınır).
/// </summary>
public sealed record ScheduleRunRequest(
    Guid RunId,
    int Threshold,
    int MaxPostsPerAccount,
    int PostLengthGuide,
    int Strategy,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    List<ScheduleRunRequestItem> Items);
