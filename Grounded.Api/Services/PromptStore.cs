using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Grounded.Api.Services;

public sealed class PromptStore
{
    private readonly Dictionary<string, PromptDefinition> _prompts;

    public PromptStore()
    {
        var promptRoot = LocatePromptRoot();
        _prompts = Directory
            .EnumerateFiles(promptRoot, "*.md", SearchOption.AllDirectories)
            .Select(file => CreateDefinition(promptRoot, file))
            .ToDictionary(def => NormalizePath(def.RelativePath), StringComparer.OrdinalIgnoreCase);
    }

    public PromptDefinition GetPrompt(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("relativePath must be provided", nameof(relativePath));
        }

        var key = NormalizePath(relativePath);
        if (!_prompts.TryGetValue(key, out var definition))
        {
            throw new InvalidOperationException($"Prompt file '{relativePath}' was not found in the prompt store.");
        }

        return definition;
    }

    public PromptDefinition GetVersionedPrompt(string promptKey, string version)
    {
        if (string.IsNullOrWhiteSpace(promptKey))
        {
            throw new ArgumentException("promptKey must be provided", nameof(promptKey));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("version must be provided", nameof(version));
        }

        return GetPrompt($"{NormalizePath(promptKey)}/{version}.md");
    }

    private static string LocatePromptRoot()
    {
        var searchRoots = new[]
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        };

        foreach (var root in searchRoots)
        {
            var found = TryFindPromptRoot(root);
            if (found is not null)
            {
                return found;
            }
        }

        throw new InvalidOperationException("Unable to locate the prompts directory. Ensure prompts are copied to the output folder.");
    }

    private static string? TryFindPromptRoot(string? start)
    {
        if (string.IsNullOrEmpty(start))
        {
            return null;
        }

        var current = Path.GetFullPath(start);
        while (!string.IsNullOrEmpty(current))
        {
            var candidate = Path.Combine(current, "prompts");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(current);
            current = parent?.FullName;
        }

        return null;
    }

    private static PromptDefinition CreateDefinition(string root, string filePath)
    {
        var relative = Path.GetRelativePath(root, filePath);
        var content = File.ReadAllText(filePath, Encoding.UTF8);
        var checksum = ComputeChecksum(content);
        var normalizedRelative = relative.Replace(Path.DirectorySeparatorChar, '/');
        var segments = normalizedRelative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var version = Path.GetFileNameWithoutExtension(normalizedRelative);
        var promptKey = segments.Length > 1
            ? string.Join('/', segments.Take(segments.Length - 1))
            : version;
        return new PromptDefinition(promptKey, version, normalizedRelative, content, checksum, DateTimeOffset.UtcNow);
    }

    private static string ComputeChecksum(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return BitConverter.ToString(hash).Replace("-", string.Empty, StringComparison.Ordinal);
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').TrimStart('/');
}

public sealed record PromptDefinition(
    string PromptKey,
    string Version,
    string RelativePath,
    string Content,
    string Checksum,
    DateTimeOffset LoadedAt);
