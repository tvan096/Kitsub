// Summary: Provides subtitle parsing, formatting protection, and atomic file translation helpers.
using System.Text;
using System.Text.RegularExpressions;

namespace Kitsub.Tooling.Translation;

internal abstract class ParsedSubtitleDocument
{
    public abstract SubtitleFileFormat Format { get; }

    public abstract IReadOnlyList<SubtitleSegment> Segments { get; }

    public abstract string Render(IReadOnlyDictionary<string, string> translatedSegments);

    public static ParsedSubtitleDocument Parse(string path, string text)
    {
        var normalized = text.Replace("\r\n", "\n");
        var extension = Path.GetExtension(path);
        if (extension.Equals(".srt", StringComparison.OrdinalIgnoreCase))
        {
            return SrtSubtitleDocument.Parse(normalized);
        }

        if (extension.Equals(".ass", StringComparison.OrdinalIgnoreCase))
        {
            return AssSubtitleDocument.Parse(normalized, SubtitleFileFormat.Ass);
        }

        if (extension.Equals(".ssa", StringComparison.OrdinalIgnoreCase))
        {
            return AssSubtitleDocument.Parse(normalized, SubtitleFileFormat.Ssa);
        }

        throw new InvalidOperationException($"Unsupported subtitle format: {extension}.");
    }
}

internal sealed class SrtSubtitleDocument : ParsedSubtitleDocument
{
    private readonly IReadOnlyList<SrtCue> _cues;
    private readonly IReadOnlyList<SubtitleSegment> _segments;

    private SrtSubtitleDocument(IReadOnlyList<SrtCue> cues)
    {
        _cues = cues;
        _segments = cues
            .Select(cue => new SubtitleSegment(cue.SegmentId, cue.ProtectedText.Value))
            .ToList();
    }

    public override SubtitleFileFormat Format => SubtitleFileFormat.Srt;

    public override IReadOnlyList<SubtitleSegment> Segments => _segments;

    public static SrtSubtitleDocument Parse(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.None);
        var cues = new List<SrtCue>();
        var block = new List<string>();

        void FlushBlock()
        {
            if (block.Count == 0)
            {
                return;
            }

            if (block.Count < 2)
            {
                throw new InvalidOperationException("Subtitle file does not contain a valid SRT cue structure.");
            }

            var id = $"cue-{cues.Count + 1:D6}";
            var cue = new SrtCue(
                block[0],
                block[1],
                block.Count > 2 ? string.Join("\n", block.Skip(2)) : string.Empty,
                id);
            cues.Add(cue);
            block.Clear();
        }

        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                FlushBlock();
                continue;
            }

            block.Add(line);
        }

        FlushBlock();

        return new SrtSubtitleDocument(cues);
    }

    public override string Render(IReadOnlyDictionary<string, string> translatedSegments)
    {
        var lines = new List<string>();
        foreach (var cue in _cues)
        {
            if (!translatedSegments.TryGetValue(cue.SegmentId, out var translatedProtected))
            {
                throw new InvalidOperationException($"Missing translated segment for {cue.SegmentId}.");
            }

            var restoredText = cue.ProtectedText.RestoreTranslatedText(translatedProtected, cue.SegmentId);
            lines.Add(cue.IndexLine);
            lines.Add(cue.TimingLine);
            if (restoredText.Length > 0)
            {
                lines.AddRange(restoredText.Split('\n', StringSplitOptions.None));
            }

            lines.Add(string.Empty);
        }

        if (lines.Count > 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join("\n", lines);
    }

    private sealed record SrtCue(string IndexLine, string TimingLine, string Text, string SegmentId)
    {
        public ProtectedSubtitleText ProtectedText { get; } = ProtectedSubtitleText.ForSrt(Text);
    }
}

internal sealed class AssSubtitleDocument : ParsedSubtitleDocument
{
    private readonly IReadOnlyList<IAssLine> _lines;
    private readonly IReadOnlyList<SubtitleSegment> _segments;
    private readonly SubtitleFileFormat _format;

    private AssSubtitleDocument(IReadOnlyList<IAssLine> lines, SubtitleFileFormat format)
    {
        _lines = lines;
        _format = format;
        _segments = lines
            .OfType<DialogueLine>()
            .Select(line => new SubtitleSegment(line.SegmentId, line.ProtectedText.Value))
            .ToList();
    }

    public override SubtitleFileFormat Format => _format;

    public override IReadOnlyList<SubtitleSegment> Segments => _segments;

    public static AssSubtitleDocument Parse(string text, SubtitleFileFormat format)
    {
        var lines = text.Split('\n', StringSplitOptions.None);
        var parsedLines = new List<IAssLine>();
        string? currentSection = null;
        int? eventsFieldCount = null;
        int dialogueCounter = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                currentSection = line;
                parsedLines.Add(new RawAssLine(line));
                continue;
            }

            if (string.Equals(currentSection, "[Events]", StringComparison.OrdinalIgnoreCase) &&
                line.StartsWith("Format:", StringComparison.OrdinalIgnoreCase))
            {
                var fields = line["Format:".Length..]
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var textIndex = Array.FindIndex(fields, field => string.Equals(field, "Text", StringComparison.OrdinalIgnoreCase));
                if (textIndex < 0)
                {
                    throw new InvalidOperationException("ASS/SSA events format is missing the Text field.");
                }

                if (textIndex != fields.Length - 1)
                {
                    throw new InvalidOperationException("ASS/SSA events format must use Text as the last field for translation.");
                }

                eventsFieldCount = fields.Length;
                parsedLines.Add(new RawAssLine(line));
                continue;
            }

            if (string.Equals(currentSection, "[Events]", StringComparison.OrdinalIgnoreCase) &&
                line.StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase))
            {
                if (!eventsFieldCount.HasValue)
                {
                    throw new InvalidOperationException("ASS/SSA dialogue lines require an [Events] Format definition before translation.");
                }

                dialogueCounter++;
                parsedLines.Add(DialogueLine.Parse(line, eventsFieldCount.Value, $"dlg-{dialogueCounter:D6}"));
                continue;
            }

            parsedLines.Add(new RawAssLine(line));
        }

        return new AssSubtitleDocument(parsedLines, format);
    }

    public override string Render(IReadOnlyDictionary<string, string> translatedSegments)
    {
        var renderedLines = new List<string>(_lines.Count);
        foreach (var line in _lines)
        {
            renderedLines.Add(line.Render(translatedSegments));
        }

        return string.Join("\n", renderedLines);
    }

    private interface IAssLine
    {
        string Render(IReadOnlyDictionary<string, string> translatedSegments);
    }

    private sealed record RawAssLine(string Value) : IAssLine
    {
        public string Render(IReadOnlyDictionary<string, string> translatedSegments) => Value;
    }

    private sealed record DialogueLine(string Prefix, string SegmentId, ProtectedSubtitleText ProtectedText) : IAssLine
    {
        public static DialogueLine Parse(string line, int fieldCount, string segmentId)
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex < 0)
            {
                throw new InvalidOperationException("ASS/SSA dialogue line is missing a field separator.");
            }

            var prefix = line[..(colonIndex + 1)];
            var remainder = line[(colonIndex + 1)..];
            var splitIndex = FindLastFieldStart(remainder, fieldCount);
            var fieldPrefix = remainder[..splitIndex];
            var text = remainder[splitIndex..];

            return new DialogueLine(prefix + fieldPrefix, segmentId, ProtectedSubtitleText.ForAss(text));
        }

        public string Render(IReadOnlyDictionary<string, string> translatedSegments)
        {
            if (!translatedSegments.TryGetValue(SegmentId, out var translatedProtected))
            {
                throw new InvalidOperationException($"Missing translated segment for {SegmentId}.");
            }

            var restoredText = ProtectedText.RestoreTranslatedText(translatedProtected, SegmentId);
            return Prefix + restoredText;
        }

        private static int FindLastFieldStart(string remainder, int fieldCount)
        {
            var requiredCommaCount = fieldCount - 1;
            var seen = 0;
            for (var index = 0; index < remainder.Length; index++)
            {
                if (remainder[index] != ',')
                {
                    continue;
                }

                seen++;
                if (seen == requiredCommaCount)
                {
                    return index + 1;
                }
            }

            throw new InvalidOperationException("ASS/SSA dialogue line does not match the declared events format.");
        }
    }
}

internal sealed class ProtectedSubtitleText
{
    private const string PlaceholderPrefix = "[[KITSUB_";
    private static readonly Regex SrtTagRegex = new(@"<[^>\r\n]+>", RegexOptions.Compiled);
    private static readonly Regex AssProtectedTokenRegex = new(@"\{[^}]*\}|\\[Nnh]", RegexOptions.Compiled);
    private static readonly Regex PlaceholderRegex = new(@"\[\[KITSUB_[A-Z]+_\d{4}\]\]", RegexOptions.Compiled);

    private readonly IReadOnlyList<KeyValuePair<string, string>> _replacements;

    private ProtectedSubtitleText(string value, IReadOnlyList<KeyValuePair<string, string>> replacements)
    {
        Value = value;
        _replacements = replacements;
    }

    public string Value { get; }

    public static ProtectedSubtitleText ForSrt(string text)
    {
        var replacements = new List<KeyValuePair<string, string>>();
        var counter = 0;
        var protectedText = ReplaceRegexMatches(text, SrtTagRegex, "TAG", replacements, ref counter);
        protectedText = ReplaceLiteralMatches(protectedText, "\n", "LB", replacements, ref counter);
        return new ProtectedSubtitleText(protectedText, replacements);
    }

    public static ProtectedSubtitleText ForAss(string text)
    {
        var replacements = new List<KeyValuePair<string, string>>();
        var counter = 0;
        var protectedText = ReplaceRegexMatches(text, AssProtectedTokenRegex, "TOK", replacements, ref counter);
        return new ProtectedSubtitleText(protectedText, replacements);
    }

    public string RestoreTranslatedText(string translatedProtectedText, string segmentId)
    {
        Validate(translatedProtectedText, segmentId);

        var restored = translatedProtectedText;
        foreach (var replacement in _replacements)
        {
            restored = restored.Replace(replacement.Key, replacement.Value, StringComparison.Ordinal);
        }

        return restored;
    }

    private void Validate(string translatedProtectedText, string segmentId)
    {
        if (translatedProtectedText.Contains('\r') || translatedProtectedText.Contains('\n'))
        {
            throw new InvalidOperationException($"Translated subtitle segment {segmentId} introduced unexpected line breaks.");
        }

        var expectedPlaceholders = ExtractPlaceholderSequence(Value);
        var actualPlaceholders = ExtractPlaceholderSequence(translatedProtectedText);
        if (!expectedPlaceholders.SequenceEqual(actualPlaceholders, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Translated subtitle segment {segmentId} changed protected formatting markers.");
        }
    }

    private static IReadOnlyList<string> ExtractPlaceholderSequence(string value)
    {
        return PlaceholderRegex.Matches(value)
            .Select(match => match.Value)
            .ToList();
    }

    private static string ReplaceRegexMatches(
        string input,
        Regex regex,
        string tokenType,
        ICollection<KeyValuePair<string, string>> replacements,
        ref int counter)
    {
        var localCounter = counter;
        var protectedText = regex.Replace(input, match =>
        {
            localCounter++;
            var placeholder = CreatePlaceholder(tokenType, localCounter);
            replacements.Add(new KeyValuePair<string, string>(placeholder, match.Value));
            return placeholder;
        });

        counter = localCounter;
        return protectedText;
    }

    private static string ReplaceLiteralMatches(
        string input,
        string literal,
        string tokenType,
        ICollection<KeyValuePair<string, string>> replacements,
        ref int counter)
    {
        var builder = new StringBuilder();
        var cursor = 0;
        while (cursor < input.Length)
        {
            var index = input.IndexOf(literal, cursor, StringComparison.Ordinal);
            if (index < 0)
            {
                builder.Append(input, cursor, input.Length - cursor);
                break;
            }

            builder.Append(input, cursor, index - cursor);
            counter++;
            var placeholder = CreatePlaceholder(tokenType, counter);
            replacements.Add(new KeyValuePair<string, string>(placeholder, literal));
            builder.Append(placeholder);
            cursor = index + literal.Length;
        }

        return builder.ToString();
    }

    private static string CreatePlaceholder(string tokenType, int counter)
        => $"{PlaceholderPrefix}{tokenType}_{counter:D4}]]";
}

internal static class SubtitleFileCodec
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static SubtitleFileText Read(string path)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var bytes = File.ReadAllBytes(path);
        var (encoding, bomLength, emitBom) = DetectEncoding(bytes);
        var text = encoding.GetString(bytes, bomLength, bytes.Length - bomLength);
        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return new SubtitleFileText(text, encoding, emitBom, newLine);
    }

    public static byte[] Encode(SubtitleFileText fileText, string translatedText)
    {
        var preamble = fileText.EmitBom ? fileText.Encoding.GetPreamble() : Array.Empty<byte>();
        var payload = fileText.Encoding.GetBytes(translatedText);
        return preamble.Concat(payload).ToArray();
    }

    private static (Encoding Encoding, int BomLength, bool EmitBom) DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return (new UTF8Encoding(true, true), 3, true);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return (new UnicodeEncoding(false, true, true), 2, true);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return (new UnicodeEncoding(true, true, true), 2, true);
        }

        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
        {
            return (new UTF32Encoding(false, true, true), 4, true);
        }

        if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
        {
            return (new UTF32Encoding(true, true, true), 4, true);
        }

        try
        {
            _ = Utf8NoBom.GetString(bytes);
            return (new UTF8Encoding(false, true), 0, false);
        }
        catch (DecoderFallbackException)
        {
            return (Encoding.GetEncoding(1250, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback), 0, false);
        }
    }
}

internal static class AtomicFileWriter
{
    public static void WriteAtomic(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";
        var backupPath = path + ".bak";

        File.WriteAllBytes(tempPath, bytes);
        if (File.Exists(path))
        {
            File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }
}
