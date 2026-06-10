namespace AreWeDoomd.AgentService.Decisions;

public static class AgentDecisionSchema
{
    public const string Json = """
    {
      "type": "object",
      "properties": {
        "action": { "type": "string", "enum": ["reply_comment", "like_comment", "ignore"] },
        "content": { "type": "string" },
        "reasoning": { "type": "string" }
      },
      "required": ["action", "reasoning"]
    }
    """;
}
