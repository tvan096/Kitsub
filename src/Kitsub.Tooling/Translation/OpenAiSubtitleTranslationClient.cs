// Summary: Translates protected subtitle segments using the official OpenAI .NET SDK.
using System.Text.Json;
using System.ClientModel;
using OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001

namespace Kitsub.Tooling.Translation;

/// <summary>Uses the OpenAI Responses API to translate subtitle batches.</summary>
public sealed class OpenAiSubtitleTranslationClient : ISubtitleTranslationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private const string ResponseSchema = """
{
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "segments": {
      "type": "array",
      "items": {
        "type": "object",
        "additionalProperties": false,
        "properties": {
          "id": { "type": "string" },
          "text": { "type": "string" }
        },
        "required": ["id", "text"]
      }
    }
  },
  "required": ["segments"]
}
""";

    public async Task<IReadOnlyList<TranslatedSubtitleSegment>> TranslateBatchAsync(
        SubtitleTranslationOptions options,
        IReadOnlyList<SubtitleSegment> segments,
        CancellationToken cancellationToken)
    {
        if (segments.Count == 0)
        {
            return Array.Empty<TranslatedSubtitleSegment>();
        }

        var clientOptions = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            clientOptions.Endpoint = new Uri(options.BaseUrl, UriKind.Absolute);
        }

        var client = new ResponsesClient(new ApiKeyCredential(options.ApiKey), clientOptions);
        var requestOptions = new CreateResponseOptions
        {
            Model = options.Model,
            Temperature = 0,
            TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    "subtitle_translation_batch",
                    BinaryData.FromString(ResponseSchema),
                    "Structured subtitle translation response.",
                    true)
            }
        };

        requestOptions.InputItems.Add(ResponseItem.CreateDeveloperMessageItem(BuildDeveloperInstructions(options)));
        requestOptions.InputItems.Add(ResponseItem.CreateUserMessageItem(JsonSerializer.Serialize(new TranslationBatchRequest(segments), JsonOptions)));

        var result = await client.CreateResponseAsync(requestOptions, cancellationToken).ConfigureAwait(false);
        var response = result.Value;
        if (response.Error is not null)
        {
            throw new InvalidOperationException($"OpenAI translation failed: {response.Error.Message}");
        }

        var outputText = response.GetOutputText();
        if (string.IsNullOrWhiteSpace(outputText))
        {
            throw new InvalidOperationException("OpenAI returned an empty subtitle translation payload.");
        }

        TranslationBatchResponse payload;
        try
        {
            payload = JsonSerializer.Deserialize<TranslationBatchResponse>(outputText, JsonOptions)
                ?? throw new InvalidOperationException("OpenAI returned an empty subtitle translation payload.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("OpenAI returned an invalid subtitle translation payload.", ex);
        }

        return payload.Segments
            .Select(segment => new TranslatedSubtitleSegment(segment.Id, segment.Text))
            .ToList();
    }

    private static string BuildDeveloperInstructions(SubtitleTranslationOptions options)
    {
        return
            $"Translate subtitle segments from {options.SourceLanguage} to {options.TargetLanguage}. " +
            "Return JSON only that matches the provided schema. " +
            "Do not add or remove segments. " +
            "Keep every placeholder token like [[KITSUB_TAG_0001]] unchanged and in the same order. " +
            "Do not introduce carriage returns or line feeds inside translated segment text.";
    }

    private sealed record TranslationBatchRequest(IReadOnlyList<SubtitleSegment> Segments);

    private sealed record TranslationBatchResponse(IReadOnlyList<TranslationBatchResponseItem> Segments)
    {
        public IReadOnlyList<TranslationBatchResponseItem> Segments { get; init; } = Segments ?? Array.Empty<TranslationBatchResponseItem>();
    }

    private sealed record TranslationBatchResponseItem(string Id, string Text);
}

#pragma warning restore OPENAI001
