using System;
using LlmIntegrationDemo.Api.Models;

namespace LlmIntegrationDemo.Api.Services;

public sealed class AnswerOutputValidator
{
    public void Validate(AnswerSynthesizerResponse response)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        if (string.IsNullOrWhiteSpace(response.Summary))
        {
            throw new InvalidOperationException("The synthesized answer must include a summary.");
        }

        if (response.KeyPoints is null)
        {
            throw new InvalidOperationException("The synthesized answer must include keyPoints.");
        }
    }
}
