namespace AreWeDoomd.AgentService.Decisions;

public static class DailyPostScoreSchema
{
    // reasoning score'dan ÖNCE: şema sırası her sağlayıcıda garanti olmadığından
    // prompt metninde de "önce reasoning yaz" talimatı verilir (daily-post-score.md).
    public const string Json = """
    {
      "type": "object",
      "properties": {
        "accounts": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "runItemId": { "type": "string" },
              "reasoning": { "type": "string" },
              "desireScore": { "type": "integer", "minimum": 0, "maximum": 100 },
              "hypotheticalPostCount": { "type": "integer", "minimum": 1, "maximum": 10 }
            },
            "required": ["runItemId", "reasoning", "desireScore", "hypotheticalPostCount"]
          }
        }
      },
      "required": ["accounts"]
    }
    """;
}
