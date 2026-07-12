namespace AreWeDoomd.AgentService.Decisions;

public static class AgentDecisionSchema
{
    public const string Json = """
    {
      "type": "object",
      "properties": {
        "actions": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "type": { "type": "string", "enum": ["like_post", "like_comment", "reply_comment"] },
              "content": { "type": "string" }
            },
            "required": ["type"]
          }
        },
        "reasoning": { "type": "string" }
      },
      "required": ["actions", "reasoning"]
    }
    """;
}
