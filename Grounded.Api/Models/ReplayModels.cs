using System.Collections.Generic;

namespace Grounded.Api.Models;

public sealed record ReplayFixture(
    string InvokerName,
    string PromptKey,
    string MatchText,
    string ResponseContent,
    string Provider,
    string ModelName,
    int TokensIn,
    int TokensOut);
