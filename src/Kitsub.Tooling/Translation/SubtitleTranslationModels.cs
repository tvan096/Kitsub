// Summary: Defines models and options used by subtitle translation services.
namespace Kitsub.Tooling.Translation;

/// <summary>Represents supported subtitle file formats for translation.</summary>
public enum SubtitleFileFormat
{
    Srt,
    Ass,
    Ssa
}

/// <summary>Represents the options used to translate subtitle files.</summary>
/// <param name="ApiKey">The OpenAI API key.</param>
/// <param name="Model">The model used for translation.</param>
/// <param name="SourceLanguage">The source subtitle language tag.</param>
/// <param name="TargetLanguage">The target subtitle language tag.</param>
/// <param name="BaseUrl">The optional OpenAI-compatible base URL.</param>
public sealed record SubtitleTranslationOptions(
    string ApiKey,
    string Model,
    string SourceLanguage,
    string TargetLanguage,
    string? BaseUrl
);

/// <summary>Represents a single subtitle segment sent to the translation model.</summary>
/// <param name="Id">The stable segment identifier.</param>
/// <param name="Text">The protected text sent to the translation model.</param>
public sealed record SubtitleSegment(string Id, string Text);

/// <summary>Represents a translated subtitle segment returned by the translation model.</summary>
/// <param name="Id">The stable segment identifier.</param>
/// <param name="Text">The translated protected text returned by the model.</param>
public sealed record TranslatedSubtitleSegment(string Id, string Text);

/// <summary>Represents the decoded subtitle file along with its original text encoding details.</summary>
/// <param name="Text">The decoded subtitle text.</param>
/// <param name="Encoding">The detected text encoding.</param>
/// <param name="EmitBom">Indicates whether the source file included a byte order mark.</param>
/// <param name="NewLine">The normalized line ending sequence to use when rewriting the file.</param>
public sealed record SubtitleFileText(
    string Text,
    System.Text.Encoding Encoding,
    bool EmitBom,
    string NewLine
);

/// <summary>Defines a translation client capable of translating subtitle batches.</summary>
public interface ISubtitleTranslationClient
{
    /// <summary>Translates a batch of subtitle segments.</summary>
    /// <param name="options">The translation options for the batch.</param>
    /// <param name="segments">The subtitle segments to translate.</param>
    /// <param name="cancellationToken">The token used to cancel the request.</param>
    /// <returns>The translated segments in the same cardinality as the input batch.</returns>
    Task<IReadOnlyList<TranslatedSubtitleSegment>> TranslateBatchAsync(
        SubtitleTranslationOptions options,
        IReadOnlyList<SubtitleSegment> segments,
        CancellationToken cancellationToken);
}
