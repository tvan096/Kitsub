// Summary: Provides a registry of command paths and suggestion helpers.
using System;

namespace Kitsub.Cli;

/// <summary>Provides the full inventory of known command paths.</summary>
public static class CommandInventory
{
    private const double SuggestionThreshold = 0.45;
    private const int MaxSuggestions = 3;

    public static IReadOnlyList<string> All { get; } = new[]
    {
        "inspect",
        "mux",
        "burn",
        "fonts attach",
        "fonts check",
        "extract audio",
        "extract sub",
        "extract video",
        "convert sub",
        "translate sub",
        "tools status",
        "tools fetch",
        "tools clean",
        "release mux",
        "config path",
        "config init",
        "config show",
        "doctor"
    };

    private static readonly HashSet<string> RootCommands = All
        .Select(command => command.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0])
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> GroupCommands = All
        .Select(command => command.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        .Where(parts => parts.Length > 1)
        .Select(parts => parts[0])
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Resolves a known command path from the provided arguments.</summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <returns>The matched command path, or null when none is found.</returns>
    public static string? ResolveKnownCommandPath(string[] args)
    {
        var tokens = ExtractCommandTokens(args, includeUnknownSubcommand: false);
        if (tokens.Count == 0)
        {
            return null;
        }

        if (tokens.Count > 1)
        {
            var candidate = string.Join(" ", tokens.ToArray());
            if (All.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return All.Contains(tokens[0], StringComparer.OrdinalIgnoreCase) ? tokens[0] : null;
    }

    /// <summary>Gets the user-provided command input for diagnostics.</summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <returns>The user command input string, or null when not found.</returns>
    public static string? GetUserCommandInput(string[] args)
    {
        var tokens = ExtractCommandTokens(args, includeUnknownSubcommand: true);
        if (tokens.Count > 0)
        {
            return tokens.Count == 1 ? tokens[0] : string.Join(" ", tokens);
        }

        var fallback = FindNextToken(args, 0);
        return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
    }

    /// <summary>Gets command suggestions for the provided input.</summary>
    /// <param name="input">The user-provided command input.</param>
    /// <returns>A list of suggested command paths.</returns>
    public static IReadOnlyList<string> Suggest(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Array.Empty<string>();
        }

        var normalized = input.Trim();
        return All
            .Select(command => new
            {
                Command = command,
                Score = ScoreSimilarity(normalized, command)
            })
            .Where(entry => entry.Score >= SuggestionThreshold)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Command, StringComparer.OrdinalIgnoreCase)
            .Take(MaxSuggestions)
            .Select(entry => entry.Command)
            .ToList();
    }

    private static IReadOnlyList<string> ExtractCommandTokens(string[] args, bool includeUnknownSubcommand)
    {
        for (var index = 0; index < args.Length; index++)
        {
            var token = args[index];
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (token.StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            if (!RootCommands.Contains(token))
            {
                continue;
            }

            if (GroupCommands.Contains(token))
            {
                var subcommand = FindNextToken(args, index + 1);
                if (!string.IsNullOrWhiteSpace(subcommand) && includeUnknownSubcommand)
                {
                    return new[] { token, subcommand };
                }

                if (!string.IsNullOrWhiteSpace(subcommand))
                {
                    var candidate = string.Join(" ", new[] { token, subcommand });
                    if (All.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                    {
                        return new[] { token, subcommand };
                    }
                }

                return new[] { token };
            }

            return new[] { token };
        }

        return Array.Empty<string>();
    }

    private static string? FindNextToken(string[] args, int startIndex)
    {
        for (var index = startIndex; index < args.Length; index++)
        {
            var token = args[index];
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (token.StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            return token;
        }

        return null;
    }

    private static double ScoreSimilarity(string input, string candidate)
    {
        var normalizedInput = input.ToLowerInvariant();
        var normalizedCandidate = candidate.ToLowerInvariant();
        var distance = ComputeLevenshteinDistance(normalizedInput, normalizedCandidate);
        var maxLength = Math.Max(normalizedInput.Length, normalizedCandidate.Length);
        var baseScore = maxLength == 0 ? 1.0 : 1.0 - (double)distance / maxLength;
        var bonus = normalizedCandidate.StartsWith(normalizedInput, StringComparison.OrdinalIgnoreCase)
            ? 0.2
            : normalizedCandidate.Contains(normalizedInput, StringComparison.OrdinalIgnoreCase)
                ? 0.1
                : 0.0;

        return Math.Min(1.0, baseScore + bonus);
    }

    private static int ComputeLevenshteinDistance(string input, string candidate)
    {
        if (input.Length == 0)
        {
            return candidate.Length;
        }

        if (candidate.Length == 0)
        {
            return input.Length;
        }

        var previousRow = new int[candidate.Length + 1];
        var currentRow = new int[candidate.Length + 1];

        for (var index = 0; index <= candidate.Length; index++)
        {
            previousRow[index] = index;
        }

        for (var i = 1; i <= input.Length; i++)
        {
            currentRow[0] = i;
            for (var j = 1; j <= candidate.Length; j++)
            {
                var cost = input[i - 1] == candidate[j - 1] ? 0 : 1;
                currentRow[j] = Math.Min(
                    Math.Min(currentRow[j - 1] + 1, previousRow[j] + 1),
                    previousRow[j - 1] + cost);
            }

            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return previousRow[candidate.Length];
    }
}
