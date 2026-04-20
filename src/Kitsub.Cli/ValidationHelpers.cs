// Summary: Provides reusable validation helpers for CLI argument inputs.
using System.Text.RegularExpressions;
using Kitsub.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Kitsub.Cli;

/// <summary>Provides helper methods for validating file system inputs.</summary>
public static class ValidationHelpers
{
    private static readonly Regex SrtTimingRegex = new(@"^\d{2}:\d{2}:\d{2},\d{3}\s+-->\s+\d{2}:\d{2}:\d{2},\d{3}", RegexOptions.Compiled);
    private static readonly Regex LanguageTagRegex = new(@"^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$", RegexOptions.Compiled);
    private static readonly HashSet<string> SupportedSubtitleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".srt",
        ".ass",
        ".ssa"
    };

    /// <summary>Validates that a file path is provided and points to an existing file.</summary>
    /// <param name="path">The file path to validate.</param>
    /// <param name="label">The label used in validation error messages.</param>
    /// <returns>A validation result indicating success or failure.</returns>
    public static ValidationResult ValidateFileExists(string? path, string label)
    {
        // Block: Ensure a non-empty file path has been supplied.
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValidationResult.Error($"{label} is required. Fix: provide a valid {label} path.");
        }

        // Block: Return success only when the target file exists on disk.
        return File.Exists(path)
            ? ValidationResult.Success()
            : ValidationResult.Error($"{label} not found: {path}. Fix: provide an existing {label} path.");
    }

    /// <summary>Validates that a file path uses a required extension.</summary>
    /// <param name="path">The file path to validate.</param>
    /// <param name="extension">The expected file extension.</param>
    /// <param name="label">The label used in validation error messages.</param>
    /// <returns>A validation result indicating success or failure.</returns>
    public static ValidationResult ValidateFileExtension(string? path, string extension, string label)
    {
        // Block: Ensure a non-empty file path has been supplied.
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValidationResult.Error($"{label} is required. Fix: provide a {extension} file.");
        }

        // Block: Fail when the file extension does not match expectations.
        return Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase)
            ? ValidationResult.Success()
            : ValidationResult.Error($"{label} must be a {extension} file. Fix: provide a {extension} file.");
    }

    /// <summary>Validates that a directory path is provided and points to an existing directory.</summary>
    /// <param name="path">The directory path to validate.</param>
    /// <param name="label">The label used in validation error messages.</param>
    /// <returns>A validation result indicating success or failure.</returns>
    public static ValidationResult ValidateDirectoryExists(string? path, string label)
    {
        // Block: Ensure a non-empty directory path has been supplied.
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValidationResult.Error($"{label} is required. Fix: provide an existing {label}.");
        }

        // Block: Return success only when the target directory exists on disk.
        return Directory.Exists(path)
            ? ValidationResult.Success()
            : ValidationResult.Error($"{label} not found: {path}. Fix: create the directory or provide a valid path.");
    }

    public static ValidationResult ValidateOutputPath(
        string? outputPath,
        string label,
        bool allowCreateDirectory,
        bool allowOverwrite,
        string? inputPath = null,
        bool createDirectory = true)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return ValidationResult.Error($"{label} is required. Fix: provide a valid output path.");
        }

        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDir = Path.GetDirectoryName(fullOutputPath) ?? Environment.CurrentDirectory;

        if (Directory.Exists(fullOutputPath))
        {
            return ValidationResult.Error($"{label} must be a file path, not a directory. Fix: provide a file path.");
        }

        if (!Directory.Exists(outputDir))
        {
            if (!allowCreateDirectory)
            {
                return ValidationResult.Error($"Output directory not found: {outputDir}. Fix: create the directory or choose another output path.");
            }

            if (createDirectory)
            {
                try
                {
                    Directory.CreateDirectory(outputDir);
                }
                catch (Exception ex)
                {
                    return ValidationResult.Error($"Unable to create output directory: {outputDir}. Fix: choose a writable directory. ({ex.Message})");
                }
            }
            else
            {
                return ValidationResult.Error($"Output directory not found: {outputDir}. Fix: create the directory or choose another output path.");
            }
        }

        if (!string.IsNullOrWhiteSpace(inputPath) && AreSamePath(fullOutputPath, inputPath))
        {
            return ValidationResult.Error($"{label} must be different from the input path. Fix: choose a different output file.");
        }

        if (File.Exists(fullOutputPath))
        {
            if (!allowOverwrite)
            {
                return ValidationResult.Error($"Output file already exists: {fullOutputPath}. Fix: pass --force to overwrite or choose another output path.");
            }

            try
            {
                using var stream = new FileStream(fullOutputPath, FileMode.Open, FileAccess.Write, FileShare.None);
                stream.Close();
            }
            catch (Exception ex)
            {
                return ValidationResult.Error($"Output file is not writable: {fullOutputPath}. Fix: choose a writable output path. ({ex.Message})");
            }
        }
        else
        {
            var writableValidation = EnsureDirectoryWritable(outputDir);
            if (!writableValidation.Successful)
            {
                return writableValidation;
            }
        }

        return ValidationResult.Success();
    }

    public static void EnsureOutputPath(
        string outputPath,
        string label,
        bool allowCreateDirectory,
        bool allowOverwrite,
        string? inputPath = null,
        bool createDirectory = true)
    {
        var result = ValidateOutputPath(outputPath, label, allowCreateDirectory, allowOverwrite, inputPath, createDirectory);
        if (!result.Successful)
        {
            throw new ValidationException(result.Message ?? $"{label} is invalid. Fix: provide a valid output path.");
        }
    }

    public static bool AreSamePath(string left, string right)
    {
        var leftFull = Path.GetFullPath(left);
        var rightFull = Path.GetFullPath(right);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(leftFull, rightFull, comparison);
    }

    public static ValidationResult ValidateSubtitleFile(string path, string label)
    {
        var extension = Path.GetExtension(path);
        if (!SupportedSubtitleExtensions.Contains(extension))
        {
            return ValidationResult.Error($"{label} must be .srt, .ass, or .ssa. Fix: re-export a valid subtitle file.");
        }

        try
        {
            var isSrt = extension.Equals(".srt", StringComparison.OrdinalIgnoreCase);
            var hasSectionHeader = false;
            var hasDialogue = false;
            var hasTiming = false;

            foreach (var line in File.ReadLines(path))
            {
                if (isSrt)
                {
                    if (SrtTimingRegex.IsMatch(line))
                    {
                        hasTiming = true;
                        break;
                    }
                }
                else
                {
                    if (!hasSectionHeader &&
                        (line.Contains("[Script Info]", StringComparison.OrdinalIgnoreCase) ||
                         line.Contains("[Events]", StringComparison.OrdinalIgnoreCase)))
                    {
                        hasSectionHeader = true;
                    }

                    if (!hasDialogue && line.StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase))
                    {
                        hasDialogue = true;
                    }

                    if (hasSectionHeader && hasDialogue)
                    {
                        break;
                    }
                }
            }

            if (isSrt && !hasTiming)
            {
                return ValidationResult.Error($"{label} does not look like a valid SRT file. Fix: re-export a valid SRT subtitle.");
            }

            if (!isSrt && !(hasSectionHeader && hasDialogue))
            {
                return ValidationResult.Error($"{label} does not look like a valid ASS/SSA file. Fix: re-export a valid ASS/SSA subtitle.");
            }
        }
        catch (Exception ex)
        {
            return ValidationResult.Error($"Unable to read {label}: {path}. Fix: ensure the file is accessible. ({ex.Message})");
        }

        return ValidationResult.Success();
    }

    public static ValidationResult ValidateSubtitleConversion(string inputPath, string outputPath)
    {
        var inputExtension = Path.GetExtension(inputPath).ToLowerInvariant();
        var outputExtension = Path.GetExtension(outputPath).ToLowerInvariant();

        if (inputExtension == ".srt" && outputExtension == ".ass")
        {
            return ValidationResult.Success();
        }

        if (inputExtension == ".ass" && outputExtension == ".srt")
        {
            return ValidationResult.Error("ASS to SRT conversion is not supported reliably. Use another tool or convert to ASS first.");
        }

        return ValidationResult.Error($"Unsupported subtitle conversion: {inputExtension} -> {outputExtension}. Fix: use .srt -> .ass.");
    }

    public static ValidationResult ValidateSubtitleTranslation(string inputPath, string outputPath)
    {
        var inputExtension = Path.GetExtension(inputPath).ToLowerInvariant();
        var outputExtension = Path.GetExtension(outputPath).ToLowerInvariant();

        if (!SupportedSubtitleExtensions.Contains(inputExtension))
        {
            return ValidationResult.Error("Input subtitle file must be .srt, .ass, or .ssa. Fix: provide a supported subtitle file.");
        }

        if (!SupportedSubtitleExtensions.Contains(outputExtension))
        {
            return ValidationResult.Error("Output subtitle file must be .srt, .ass, or .ssa. Fix: choose a supported subtitle extension.");
        }

        if (!string.Equals(inputExtension, outputExtension, StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Error($"Subtitle translation must keep the same format: {inputExtension} -> {outputExtension}. Fix: use the same subtitle extension for --in and --out.");
        }

        return ValidationResult.Success();
    }

    public static ValidationResult ValidateTrackSelectorSyntax(string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return ValidationResult.Error("Track selector is required. Fix: provide a track index, ID, language, or title.");
        }

        if (int.TryParse(selector, out var index) && index < 0)
        {
            return ValidationResult.Error("Track selector must be 0 or greater. Fix: use a non-negative track index or ID.");
        }

        return ValidationResult.Success();
    }

    public static TrackSelectionOutcome ResolveTrackSelection(
        MediaInfo info,
        TrackType type,
        string selector,
        bool rejectBitmapSubtitles,
        string? filePathForFix)
    {
        if (int.TryParse(selector, out var index))
        {
            var matches = info.Tracks
                .Where(track => track.Type == type && (track.Index == index || track.Id == index))
                .ToList();

            if (matches.Count == 0)
            {
                throw BuildTrackNotFound(selector, type, filePathForFix);
            }

            var selected = matches[0];
            ValidateSubtitleTrack(selected, rejectBitmapSubtitles, filePathForFix);

            var warning = matches.Count > 1
                ? BuildTrackAmbiguousWarning(selector, type, filePathForFix)
                : null;
            return new TrackSelectionOutcome(selected, warning);
        }

        var textMatches = info.Tracks
            .Where(track => track.Type == type)
            .Where(track =>
                (!string.IsNullOrWhiteSpace(track.Language) &&
                 string.Equals(track.Language, selector, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(track.Title) &&
                 track.Title.Contains(selector, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (textMatches.Count == 0)
        {
            throw BuildTrackNotFound(selector, type, filePathForFix);
        }

        var chosen = textMatches[0];
        ValidateSubtitleTrack(chosen, rejectBitmapSubtitles, filePathForFix);
        var textWarning = textMatches.Count > 1
            ? BuildTrackAmbiguousWarning(selector, type, filePathForFix)
            : null;
        return new TrackSelectionOutcome(chosen, textWarning);
    }

    public static ValidationResult ValidateLanguageTag(string? language, string label)
    {
        if (language is null)
        {
            return ValidationResult.Success();
        }

        var trimmed = language.Trim();
        if (trimmed.Length == 0)
        {
            return ValidationResult.Error($"{label} cannot be blank. Fix: provide a valid language tag like \"en\" or omit it.");
        }

        if (!string.Equals(trimmed, language, StringComparison.Ordinal))
        {
            return ValidationResult.Error($"{label} must not include leading or trailing whitespace. Fix: remove extra spaces.");
        }

        if (!LanguageTagRegex.IsMatch(trimmed))
        {
            return ValidationResult.Error($"{label} is not a valid language tag: {language}. Fix: use a tag like \"en\" or \"en-US\".");
        }

        return ValidationResult.Success();
    }

    public static ValidationResult ValidateTitle(string? title, string label)
    {
        if (title is null)
        {
            return ValidationResult.Success();
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return ValidationResult.Error($"{label} cannot be blank. Fix: provide a title or omit it.");
        }

        return ValidationResult.Success();
    }

    public static bool IsBitmapSubtitle(TrackInfo track)
    {
        return track.Codec.Contains("pgs", StringComparison.OrdinalIgnoreCase) ||
               track.Codec.Contains("dvd", StringComparison.OrdinalIgnoreCase) ||
               track.Codec.Contains("vobsub", StringComparison.OrdinalIgnoreCase) ||
               track.Codec.Contains("hdmv", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAssSubtitleFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".ass", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ssa", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAssSubtitleTrack(TrackInfo track)
    {
        return track.Codec.Contains("SubStationAlpha", StringComparison.OrdinalIgnoreCase) ||
               track.Codec.Contains("ASS", StringComparison.OrdinalIgnoreCase) ||
               track.Codec.Contains("SSA", StringComparison.OrdinalIgnoreCase);
    }

    private static ValidationResult EnsureDirectoryWritable(string directoryPath)
    {
        try
        {
            var probePath = Path.Combine(directoryPath, $".kitsub_write_test_{Guid.NewGuid():N}.tmp");
            using var stream = new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            stream.WriteByte(0);
        }
        catch (Exception ex)
        {
            return ValidationResult.Error($"Output directory is not writable: {directoryPath}. Fix: choose a writable directory. ({ex.Message})");
        }

        return ValidationResult.Success();
    }

    private static ValidationException BuildTrackNotFound(string selector, TrackType type, string? filePathForFix)
    {
        var fileHint = string.IsNullOrWhiteSpace(filePathForFix) ? "<file>" : filePathForFix;
        return new ValidationException($"{type} track not found for selector \"{selector}\". Fix: run `kitsub inspect {fileHint}` to list tracks.");
    }

    private static void ValidateSubtitleTrack(TrackInfo track, bool rejectBitmap, string? filePathForFix)
    {
        if (!rejectBitmap || !IsBitmapSubtitle(track))
        {
            return;
        }

        var fileHint = string.IsNullOrWhiteSpace(filePathForFix) ? "<file>" : filePathForFix;
        throw new ValidationException($"Bitmap subtitles are not supported for this command. Fix: select a text-based subtitle track from `kitsub inspect {fileHint}`.");
    }

    private static string BuildTrackAmbiguousWarning(string selector, TrackType type, string? filePathForFix)
    {
        var fileHint = string.IsNullOrWhiteSpace(filePathForFix) ? "<file>" : filePathForFix;
        return $"Selector \"{selector}\" matched multiple {type} tracks; using the first match. Fix: use a numeric track index or ID from `kitsub inspect {fileHint}`.";
    }
}

public sealed record TrackSelectionOutcome(TrackInfo Track, string? Warning);
