using System.Text;
using System.Text.Json;
using Grounded.Api.Models;

namespace Grounded.Api.Services;

public sealed class PlannerPromptRenderer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly PromptStore _promptStore;
    private readonly PlannerContextBuilder _contextBuilder;

    public PlannerPromptRenderer(PromptStore promptStore, PlannerContextBuilder contextBuilder)
    {
        _promptStore = promptStore;
        _contextBuilder = contextBuilder;
    }

    public PlannerPromptRenderResult Render(string userQuestion, ConversationStateSnapshot? conversationState = null)
    {
        var prompt = _promptStore.GetVersionedPrompt("planner", "v1");
        var context = _contextBuilder.Build();
        var builder = new StringBuilder(prompt.Content);
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine("## Supported Surface");
        builder.AppendLine("```json");
        builder.AppendLine(JsonSerializer.Serialize(context, SerializerOptions));
        builder.AppendLine("```");
        builder.AppendLine();
        if (conversationState is not null)
        {
            builder.AppendLine("## Prior Conversation State");
            builder.AppendLine("```json");
            builder.AppendLine(JsonSerializer.Serialize(conversationState, SerializerOptions));
            builder.AppendLine("```");
            builder.AppendLine();
        }

        builder.AppendLine("## User Question");
        builder.AppendLine(userQuestion.Trim());

        return new PlannerPromptRenderResult(prompt, builder.ToString(), context);
    }
}
