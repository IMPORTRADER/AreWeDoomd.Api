namespace AreWeDoomd.AgentService.Decisions;

public static class DailyPostComposeSchema
{
    public const string Json = """
    {
      "type": "object",
      "properties": {
        "posts": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "content": { "type": "string" },
              "scheduledTimeUtc": { "type": "string" }
            },
            "required": ["content", "scheduledTimeUtc"]
          }
        }
      },
      "required": ["posts"]
    }
    """;
}
