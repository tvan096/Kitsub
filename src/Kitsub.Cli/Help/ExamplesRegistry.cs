// Summary: Stores per-command usage examples for error guidance.
namespace Kitsub.Cli;

/// <summary>Provides usage examples for CLI commands.</summary>
public static class ExamplesRegistry
{
    private static readonly IReadOnlyDictionary<string, string[]> Examples =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["inspect"] = new[]
            {
                "kitsub inspect sample.mkv",
                "kitsub inspect mediainfo sample.mkv"
            },
            ["mux"] = new[]
            {
                "kitsub mux --in video.mkv --sub subs.ass",
                "kitsub mux --in video.mkv --sub en.ass --lang eng --title \"English\" --default"
            },
            ["burn"] = new[]
            {
                "kitsub burn --in video.mkv --sub subs.ass --out output.mp4",
                "kitsub burn --in video.mkv --track 2 --out output.mp4 --fontsdir fonts"
            },
            ["extract audio"] = new[]
            {
                "kitsub extract audio --in video.mkv --track 1 --out audio.aac",
                "kitsub extract audio --in video.mkv --track 0 --out audio.flac"
            },
            ["extract sub"] = new[]
            {
                "kitsub extract sub --in video.mkv --track 2 --out subs.ass",
                "kitsub extract sub --in video.mkv --track 3 --out subs.srt"
            },
            ["extract video"] = new[]
            {
                "kitsub extract video --in video.mkv --out video.h264",
                "kitsub extract video --in video.mkv --out video.hevc"
            },
            ["fonts check"] = new[]
            {
                "kitsub fonts check --in video.mkv"
            },
            ["fonts attach"] = new[]
            {
                "kitsub fonts attach --in video.mkv --dir fonts",
                "kitsub fonts attach --in video.mkv --dir fonts --out video.fonts.mkv"
            },
            ["tools status"] = new[]
            {
                "kitsub tools status"
            },
            ["tools fetch"] = new[]
            {
                "kitsub tools fetch"
            },
            ["tools clean"] = new[]
            {
                "kitsub tools clean"
            },
            ["convert sub"] = new[]
            {
                "kitsub convert sub --in subs.ass --out subs.srt",
                "kitsub convert sub --in subs.ssa --out subs.ass"
            },
            ["translate sub"] = new[]
            {
                "kitsub translate sub --in subs.en.srt --out subs.cs.srt --to cs",
                "kitsub translate sub --in signs.ass --out signs.cs.ass --from en --to cs"
            },
            ["release mux"] = new[]
            {
                "kitsub release mux --in video.mkv --sub subs.ass --out release.mkv",
                "kitsub release mux --spec release.json --out-dir releases"
            }
        };

    /// <summary>Gets usage examples for the specified command path.</summary>
    /// <param name="commandPath">The command path to query.</param>
    /// <returns>A list of usage examples, or an empty list when none exist.</returns>
    public static IReadOnlyList<string> GetExamples(string? commandPath)
    {
        if (string.IsNullOrWhiteSpace(commandPath))
        {
            return Array.Empty<string>();
        }

        return Examples.TryGetValue(commandPath, out var examples)
            ? examples
            : Array.Empty<string>();
    }
}
