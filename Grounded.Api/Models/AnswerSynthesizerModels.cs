using System.Collections.Generic;

namespace Grounded.Api.Models;

public sealed record AnswerSynthesizerRequest(
    string UserQuestion,
    QueryPlan QueryPlan,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    IReadOnlyList<string> Columns,
    QueryExecutionMetadata? ExecutionMetadata,
    string PromptVersion);

public sealed record AnswerSynthesizerResponse(
    string Summary,
    IReadOnlyList<string> KeyPoints,
    bool TableIncluded);
