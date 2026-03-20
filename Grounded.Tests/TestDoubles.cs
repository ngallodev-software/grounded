using Grounded.Api.Models;
using Grounded.Api.Services;

namespace Grounded.Tests;

internal sealed class InMemoryTraceRepository : ITraceRepository
{
    public List<PersistedTraceRecord> Items { get; } = [];

    public Task PersistAsync(PersistedTraceRecord trace, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Items.Add(trace);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryEvalRepository : IEvalRepository
{
    public List<PersistedEvalRun> Items { get; } = [];

    public Task PersistAsync(PersistedEvalRun run, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Items.Add(run);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryConversationStateRepository : IConversationStateRepository
{
    public Dictionary<string, ConversationStateSnapshot> Items { get; } = new(StringComparer.Ordinal);

    public Task<ConversationStateSnapshot?> GetAsync(string conversationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Items.TryGetValue(conversationId, out var snapshot);
        return Task.FromResult(snapshot);
    }

    public Task UpsertAsync(string conversationId, ConversationStateSnapshot snapshot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Items[conversationId] = snapshot;
        return Task.CompletedTask;
    }
}
