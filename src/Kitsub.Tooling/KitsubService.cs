// Summary: Coordinates media tooling operations across ffmpeg, ffprobe, and mkvmerge.
using Kitsub.Core;
using Kitsub.Tooling.Translation;

namespace Kitsub.Tooling;

/// <summary>Provides high-level media operations by coordinating tooling clients.</summary>
public sealed class KitsubService
{
    private readonly FfprobeClient _ffprobe;
    private readonly MkvmergeClient _mkvmerge;
    private readonly MkvmergeMuxer _mkvmergeMuxer;
    private readonly FfmpegClient _ffmpeg;
    private readonly SubtitleTranslationService _subtitleTranslation;

    /// <summary>Initializes a new instance with the required tooling clients.</summary>
    /// <param name="ffprobe">The ffprobe client used for media inspection.</param>
    /// <param name="mkvmerge">The mkvmerge client used for MKV inspection.</param>
    /// <param name="mkvmergeMuxer">The mkvmerge muxer used for MKV modifications.</param>
    /// <param name="ffmpeg">The ffmpeg client used for media processing.</param>
    public KitsubService(
        FfprobeClient ffprobe,
        MkvmergeClient mkvmerge,
        MkvmergeMuxer mkvmergeMuxer,
        FfmpegClient ffmpeg,
        SubtitleTranslationService subtitleTranslation)
    {
        // Block: Store tooling clients used across service operations.
        _ffprobe = ffprobe;
        _mkvmerge = mkvmerge;
        _mkvmergeMuxer = mkvmergeMuxer;
        _ffmpeg = ffmpeg;
        _subtitleTranslation = subtitleTranslation;
    }

    /// <summary>Inspects a media file and returns metadata along with MKV detection.</summary>
    /// <param name="filePath">The media file path to inspect.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The media info and a flag indicating MKV format.</returns>
    public async Task<(MediaInfo Info, bool IsMkv)> InspectAsync(string filePath, CancellationToken cancellationToken)
    {
        if (Path.GetExtension(filePath).Equals(".mkv", StringComparison.OrdinalIgnoreCase))
        {
            // Block: Use mkvmerge to inspect MKV containers.
            return (await _mkvmerge.IdentifyAsync(filePath, cancellationToken).ConfigureAwait(false), true);
        }

        // Block: Use ffprobe to inspect non-MKV containers.
        return (await _ffprobe.ProbeAsync(filePath, cancellationToken).ConfigureAwait(false), false);
    }

    /// <summary>Muxes subtitle files into an MKV container.</summary>
    /// <param name="inputMkv">The input MKV file path.</param>
    /// <param name="subtitles">The subtitle descriptors to mux.</param>
    /// <param name="outputMkv">The output MKV file path.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    public Task MuxSubtitlesAsync(
        string inputMkv,
        IReadOnlyList<SubtitleDescriptor> subtitles,
        string outputMkv,
        CancellationToken cancellationToken)
    {
        // Block: Delegate subtitle muxing to the mkvmerge muxer.
        return _mkvmergeMuxer.MuxSubtitlesAsync(inputMkv, subtitles, outputMkv, cancellationToken);
    }

    /// <summary>Attaches font files to an MKV container.</summary>
    /// <param name="inputMkv">The input MKV file path.</param>
    /// <param name="fontsDir">The directory containing font files.</param>
    /// <param name="outputMkv">The output MKV file path.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    public Task AttachFontsAsync(string inputMkv, string fontsDir, string outputMkv, CancellationToken cancellationToken)
    {
        // Block: Delegate font attachment to the mkvmerge muxer.
        return _mkvmergeMuxer.AttachFontsAsync(inputMkv, fontsDir, outputMkv, cancellationToken);
    }

    /// <summary>Checks an MKV file for embedded fonts and ASS subtitle tracks.</summary>
    /// <param name="inputMkv">The input MKV file path.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>Flags indicating font and ASS subtitle presence, along with media info.</returns>
    public async Task<(bool HasFonts, bool HasAssSubtitles, MediaInfo Info)> CheckFontsAsync(
        string inputMkv,
        CancellationToken cancellationToken)
    {
        // Block: Inspect the MKV to gather attachment and track metadata.
        var info = await _mkvmerge.IdentifyAsync(inputMkv, cancellationToken).ConfigureAwait(false);
        // Block: Determine whether font attachments are present.
        var hasFonts = info.Attachments.Any(attachment =>
            attachment.FileName.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
            attachment.FileName.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) ||
            attachment.FileName.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase) ||
            attachment.FileName.EndsWith(".woff", StringComparison.OrdinalIgnoreCase) ||
            attachment.FileName.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase));

        // Block: Determine whether ASS/SSA subtitle tracks are present.
        var hasAss = info.Tracks.Any(track =>
            track.Type == TrackType.Subtitle &&
            (track.Codec.Contains("SubStationAlpha", StringComparison.OrdinalIgnoreCase) ||
             track.Codec.Contains("ASS", StringComparison.OrdinalIgnoreCase) ||
             track.Codec.Contains("SSA", StringComparison.OrdinalIgnoreCase)));

        return (hasFonts, hasAss, info);
    }

    /// <summary>Burns subtitles into a video file.</summary>
    /// <param name="inputFile">The input media file path.</param>
    /// <param name="subtitleFile">The subtitle file path.</param>
    /// <param name="outputFile">The output media file path.</param>
    /// <param name="fontsDir">The optional fonts directory.</param>
    /// <param name="crf">The constant rate factor used for encoding.</param>
    /// <param name="preset">The encoder preset.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    public Task BurnSubtitlesAsync(
        string inputFile,
        string subtitleFile,
        string outputFile,
        string? fontsDir,
        int crf,
        string preset,
        CancellationToken cancellationToken)
    {
        // Block: Delegate subtitle burn to the ffmpeg client.
        return _ffmpeg.BurnSubtitlesAsync(inputFile, subtitleFile, outputFile, fontsDir, crf, preset, cancellationToken);
    }

    /// <summary>Extracts an audio track to a file.</summary>
    /// <param name="inputFile">The input media file path.</param>
    /// <param name="selector">The track selector used to choose the audio track.</param>
    /// <param name="outputFile">The output audio file path.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">Thrown when the requested audio track is not found.</exception>
    public async Task ExtractAudioAsync(string inputFile, string selector, string outputFile, CancellationToken cancellationToken)
    {
        // Block: Inspect the media file and resolve the requested audio track.
        var info = await _ffprobe.ProbeAsync(inputFile, cancellationToken).ConfigureAwait(false);
        var track = TrackSelection.SelectTrack(info, TrackType.Audio, selector);
        if (track is null)
        {
            // Block: Fail when the requested audio track cannot be resolved.
            throw new InvalidOperationException("Audio track not found.");
        }

        // Block: Map the track to its type-relative index and extract it.
        var audioIndex = GetTypeIndex(info, track);
        await _ffmpeg.ExtractAudioAsync(inputFile, audioIndex, outputFile, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Extracts the primary video stream to a file.</summary>
    /// <param name="inputFile">The input media file path.</param>
    /// <param name="outputFile">The output video file path.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    public async Task ExtractVideoAsync(string inputFile, string outputFile, CancellationToken cancellationToken)
    {
        // Block: Delegate video extraction to the ffmpeg client.
        await _ffmpeg.ExtractVideoAsync(inputFile, outputFile, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Extracts a subtitle track to a file.</summary>
    /// <param name="inputFile">The input media file path.</param>
    /// <param name="selector">The track selector used to choose the subtitle track.</param>
    /// <param name="outputFile">The output subtitle file path.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">Thrown when the requested subtitle track is not found or unsupported.</exception>
    public async Task ExtractSubtitleAsync(string inputFile, string selector, string outputFile, CancellationToken cancellationToken)
    {
        // Block: Inspect the media file and resolve the requested subtitle track.
        var info = await _ffprobe.ProbeAsync(inputFile, cancellationToken).ConfigureAwait(false);
        var track = TrackSelection.SelectTrack(info, TrackType.Subtitle, selector);
        if (track is null)
        {
            // Block: Fail when the requested subtitle track cannot be resolved.
            throw new InvalidOperationException("Subtitle track not found.");
        }

        if (IsBitmapSubtitle(track))
        {
            // Block: Reject bitmap subtitles that cannot be extracted to text.
            throw new InvalidOperationException("Bitmap subtitles not supported for extraction to text.");
        }

        // Block: Map the track to its type-relative index and extract it.
        var subtitleIndex = GetTypeIndex(info, track);
        await _ffmpeg.ExtractSubtitleAsync(inputFile, subtitleIndex, outputFile, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Converts a subtitle file to another supported format.</summary>
    /// <param name="inputFile">The input subtitle file path.</param>
    /// <param name="outputFile">The output subtitle file path.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    public async Task ConvertSubtitleAsync(string inputFile, string outputFile, CancellationToken cancellationToken)
    {
        // Block: Delegate subtitle conversion to the ffmpeg client.
        await _ffmpeg.ConvertSubtitleAsync(inputFile, outputFile, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Translates a subtitle file with OpenAI while preserving its format and encoding.</summary>
    /// <param name="inputFile">The input subtitle file path.</param>
    /// <param name="outputFile">The output subtitle file path.</param>
    /// <param name="options">The OpenAI translation options.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    public Task TranslateSubtitleAsync(
        string inputFile,
        string outputFile,
        SubtitleTranslationOptions options,
        CancellationToken cancellationToken)
    {
        return _subtitleTranslation.TranslateFileAsync(inputFile, outputFile, options, cancellationToken);
    }

    /// <summary>Extracts a subtitle track to a temporary file and returns the path.</summary>
    /// <param name="inputFile">The input media file path.</param>
    /// <param name="selector">The track selector used to choose the subtitle track.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The temporary subtitle file path.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the requested subtitle track is not found or unsupported.</exception>
    public async Task<string> ExtractSubtitleToTempAsync(string inputFile, string selector, CancellationToken cancellationToken)
    {
        // Block: Inspect the media file and resolve the requested subtitle track.
        var info = await _ffprobe.ProbeAsync(inputFile, cancellationToken).ConfigureAwait(false);
        var track = TrackSelection.SelectTrack(info, TrackType.Subtitle, selector);
        if (track is null)
        {
            // Block: Fail when the requested subtitle track cannot be resolved.
            throw new InvalidOperationException("Subtitle track not found.");
        }

        if (IsBitmapSubtitle(track))
        {
            // Block: Reject bitmap subtitles that cannot be extracted to text.
            throw new InvalidOperationException("Bitmap subtitles not supported for extraction to text.");
        }

        // Block: Choose a temp file extension based on subtitle codec.
        var extension = track.Codec.Contains("ass", StringComparison.OrdinalIgnoreCase)
            ? ".ass"
            : ".srt";
        // Block: Build a temporary file path and extract the subtitle stream.
        var tempFile = Path.Combine(Path.GetTempPath(), $"kitsub_{Guid.NewGuid():N}{extension}");
        var subtitleIndex = GetTypeIndex(info, track);
        await _ffmpeg.ExtractSubtitleAsync(inputFile, subtitleIndex, tempFile, cancellationToken).ConfigureAwait(false);
        return tempFile;
    }

    private static int GetTypeIndex(MediaInfo info, TrackInfo track)
    {
        // Block: Resolve the index of the track within its type grouping.
        return info.Tracks
            .Where(t => t.Type == track.Type)
            .OrderBy(t => t.Index)
            .Select((t, index) => new { Track = t, Index = index })
            .First(pair => ReferenceEquals(pair.Track, track)).Index;
    }

    private static bool IsBitmapSubtitle(TrackInfo track)
    {
        // Block: Identify bitmap subtitle codecs that are unsupported for text extraction.
        return track.Codec.Contains("pgs", StringComparison.OrdinalIgnoreCase) ||
               track.Codec.Contains("dvd", StringComparison.OrdinalIgnoreCase) ||
               track.Codec.Contains("vobsub", StringComparison.OrdinalIgnoreCase) ||
               track.Codec.Contains("hdmv", StringComparison.OrdinalIgnoreCase);
    }
}
