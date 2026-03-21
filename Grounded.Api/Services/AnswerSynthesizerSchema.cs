namespace Grounded.Api.Services;

public static class AnswerSynthesizerSchema
{
    public const string Name = "AnswerSynthesizerResponse";

    public const string Json = """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["summary", "keyPoints", "tableIncluded"],
          "properties": {
            "summary": { "type": "string" },
            "keyPoints": {
              "type": "array",
              "items": { "type": "string" }
            },
            "tableIncluded": { "type": "boolean" }
          }
        }
        """;
}
