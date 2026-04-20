using FluentAssertions;
using Kitsub.Tests.Helpers;
using Kitsub.Tooling.Translation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kitsub.Tests.Tooling;

public class SubtitleTranslationServiceTests
{
    [Fact]
    public async Task TranslateFileAsync_WhenSrtInput_PreservesEncodingAndLineEndings()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "input.srt");
        var output = Path.Combine(temp.Path, "output.srt");
        var encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var source = "1\r\n00:00:01,000 --> 00:00:02,000\r\n<i>Hello</i>\r\nSecond line\r\n";
        File.WriteAllBytes(input, encoding.GetPreamble().Concat(encoding.GetBytes(source)).ToArray());

        var service = new SubtitleTranslationService(
            new FakeSubtitleTranslationClient(text => "[[KITSUB_TAG_0001]]Ahoj[[KITSUB_TAG_0002]][[KITSUB_LB_0003]]druha"),
            NullLogger<SubtitleTranslationService>.Instance);

        await service.TranslateFileAsync(
            input,
            output,
            new SubtitleTranslationOptions("key", "gpt-4o-mini", "en", "cs", null),
            CancellationToken.None);

        var bytes = File.ReadAllBytes(output);
        bytes.Take(3).Should().Equal(encoding.GetPreamble());
        var text = encoding.GetString(bytes, 3, bytes.Length - 3);
        text.Should().Contain("\r\n");
        text.Should().Contain("<i>Ahoj</i>");
        text.Should().Contain("druha");
    }

    [Fact]
    public async Task TranslateFileAsync_WhenAssInput_PreservesDialogueStructure()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "input.ass");
        var output = Path.Combine(temp.Path, "output.ass");
        var source = """
[Script Info]
Title: Demo

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
Dialogue: 0,0:00:01.00,0:00:03.00,Default,,0,0,0,,{\i1}Hello\Nworld
""";
        File.WriteAllText(input, source.Replace("\n", "\r\n", StringComparison.Ordinal), new System.Text.UTF8Encoding(false));

        var service = new SubtitleTranslationService(new FakeSubtitleTranslationClient(text => "[[KITSUB_TOK_0001]]Ahoj[[KITSUB_TOK_0002]]svete"), NullLogger<SubtitleTranslationService>.Instance);

        await service.TranslateFileAsync(
            input,
            output,
            new SubtitleTranslationOptions("key", "gpt-4o-mini", "en", "cs", null),
            CancellationToken.None);

        var translated = File.ReadAllText(output);
        translated.Should().Contain("Dialogue: 0,0:00:01.00,0:00:03.00,Default,,0,0,0,,{\\i1}Ahoj\\Nsvete");
        translated.Should().Contain("[Script Info]");
        translated.Should().Contain("Format:");
    }

    [Fact]
    public async Task TranslateFileAsync_WhenBatchIdsMismatch_Throws()
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "input.srt");
        var output = Path.Combine(temp.Path, "output.srt");
        File.WriteAllText(input, "1\r\n00:00:01,000 --> 00:00:02,000\r\nHello\r\n");

        var service = new SubtitleTranslationService(new MismatchedSubtitleTranslationClient(), NullLogger<SubtitleTranslationService>.Instance);

        var act = () => service.TranslateFileAsync(
            input,
            output,
            new SubtitleTranslationOptions("key", "gpt-4o-mini", "en", "cs", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("OpenAI returned subtitle segments with mismatched identifiers.");
    }

    private sealed class FakeSubtitleTranslationClient : ISubtitleTranslationClient
    {
        private readonly Func<string, string> _translate;

        public FakeSubtitleTranslationClient(Func<string, string> translate)
        {
            _translate = translate;
        }

        public Task<IReadOnlyList<TranslatedSubtitleSegment>> TranslateBatchAsync(
            SubtitleTranslationOptions options,
            IReadOnlyList<SubtitleSegment> segments,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<TranslatedSubtitleSegment>>(
                segments.Select(segment => new TranslatedSubtitleSegment(segment.Id, _translate(segment.Text))).ToList());
        }
    }

    private sealed class MismatchedSubtitleTranslationClient : ISubtitleTranslationClient
    {
        public Task<IReadOnlyList<TranslatedSubtitleSegment>> TranslateBatchAsync(
            SubtitleTranslationOptions options,
            IReadOnlyList<SubtitleSegment> segments,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<TranslatedSubtitleSegment>>(
                [new TranslatedSubtitleSegment("wrong-id", segments[0].Text)]);
        }
    }
}
