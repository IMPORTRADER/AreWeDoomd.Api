namespace AreWeDoomd.Infrastructure.Ai;

public static class PersonaBatchSchema
{
    public const string Json = """
        {
          "type": "object",
          "properties": {
            "personas": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "username":    { "type": "string" },
                  "traits":      { "type": "array", "items": { "type": "string" } },
                  "typingStyle": { "type": "string" },
                  "summary":     { "type": "string" }
                },
                "required": ["username", "traits", "typingStyle", "summary"],
                "additionalProperties": false
              }
            }
          },
          "required": ["personas"],
          "additionalProperties": false
        }
        """;
}
