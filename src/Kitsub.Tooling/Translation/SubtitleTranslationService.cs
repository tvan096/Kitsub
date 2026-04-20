// Summary: Coordinates subtitle parsing, batching, translation, validation, and atomic writes.
using Microsoft.Extensions.Logging;

namespace Kitsub.Tooling.Translation;

/// <summary>Provides high-level subtitle translation file operations.</summary>
public sealed class SubtitleTranslationService
{
    private const int MaxBatchSegments = 40;
    private const int MaxBatchCharacters = 6_000;

    private readonly ISubtitleTranslationClient _translationClient;
    private readonly ILogger<SubtitleTranslationService> _logger;

    /// <summary>Initializes a new instance with the required translation client.</summary>
    /// <param name="translationClient">The translation client used for OpenAI requests.</param>
    /// <param name="logger">The logger used for diagnostics.</param>
    public SubtitleTranslationService(
        ISubtitleTranslationClient translationClient,
        ILogger<SubtitleTranslationService> logger)
    {
        _translationClient = translationClient;
        _logger = logger;
    }

    /// <summary>Translates a subtitle file while preserving format, encoding, and line endings.</summary>
    /// <param name="inputPath">The source subtitle file path.</param>
    /// <param name="outputPath">The translated subtitle file path.</param>
    /// <param name="options">The translation options used for the request.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    public async Task TranslateFileAsync(
        string inputPath,
        string outputPath,
        SubtitleTranslationOptions options,
        CancellationToken cancellationToken)
    {
        var sourceFile = SubtitleFileCodec.Read(inputPath);
        var document = ParsedSubtitleDocument.Parse(inputPath, sourceFile.Text);
        if (document.Segments.Count == 0)
        {
            throw new InvalidOperationException("Subtitle file does not contain any translatable text segments.");
        }

        var translations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var batch in CreateBatches(document.Segments))
        {
            var translatedBatch = await _translationClient.TranslateBatchAsync(options, batch, cancellationToken)
                .ConfigureAwait(false);
            ValidateBatch(batch, translatedBatch);
            foreach (var segment in translatedBatch)
            {
                translations[segment.Id] = segment.Text;
            }
        }

        var translatedText = document.Render(translations);
        var normalizedOutput = translatedText.Replace("\n", sourceFile.NewLine, StringComparison.Ordinal);
        _ = ParsedSubtitleDocument.Parse(outputPath, normalizedOutput);

        var bytes = SubtitleFileCodec.Encode(sourceFile, normalizedOutput);
        AtomicFileWriter.WriteAtomic(outputPath, bytes);
        _logger.LogInformation("Translated subtitles from {Input} to {Output}", inputPath, outputPath);
    }

    private static IReadOnlyList<IReadOnlyList<SubtitleSegment>> CreateBatches(IReadOnlyList<SubtitleSegment> segments)
    {
        var batches = new List<IReadOnlyList<SubtitleSegment>>();
        var current = new List<SubtitleSegment>();
        var charCount = 0;

        foreach (var segment in segments)
        {
            var nextLength = charCount + segment.Text.Length;
            if (current.Count > 0 &&
                (current.Count >= MaxBatchSegments || nextLength > MaxBatchCharacters))
            {
                batches.Add(current.ToList());
                current.Clear();
                charCount = 0;
            }

            current.Add(segment);
            charCount += segment.Text.Length;
        }

        if (current.Count > 0)
        {
            batches.Add(current);
        }

        return batches;
    }

    private static void ValidateBatch(
        IReadOnlyList<SubtitleSegment> sourceBatch,
        IReadOnlyList<TranslatedSubtitleSegment> translatedBatch)
    {
        if (sourceBatch.Count != translatedBatch.Count)
        {
            throw new InvalidOperationException("OpenAI returned a different number of translated subtitle segments than requested.");
        }

        var expectedIds = sourceBatch.Select(segment => segment.Id).ToHashSet(StringComparer.Ordinal);
        var actualIds = translatedBatch.Select(segment => segment.Id).ToHashSet(StringComparer.Ordinal);
        if (!expectedIds.SetEquals(actualIds))
        {
            throw new InvalidOperationException("OpenAI returned subtitle segments with mismatched identifiers.");
        }
    }
}
