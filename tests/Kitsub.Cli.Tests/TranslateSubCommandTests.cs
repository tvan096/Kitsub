using FluentAssertions;
using Kitsub.Cli;
using Xunit;

namespace Kitsub.Cli.Tests;

public class TranslateSubCommandTests
{
    [Fact]
    public void Validate_WhenFormatsDiffer_ReturnsError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "kitsub-translate-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var input = Path.Combine(tempDir, "subs.srt");
            File.WriteAllText(input, "1\r\n00:00:01,000 --> 00:00:02,000\r\nHello\r\n");

            var settings = new TranslateSubCommand.Settings
            {
                InputFile = input,
                OutputFile = Path.Combine(tempDir, "subs.ass")
            };

            var result = settings.Validate();

            result.Successful.Should().BeFalse();
            result.Message.Should().Be("Subtitle translation must keep the same format: .srt -> .ass. Fix: use the same subtitle extension for --in and --out.");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Validate_WhenToLanguageInvalid_ReturnsError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "kitsub-translate-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var input = Path.Combine(tempDir, "subs.srt");
            File.WriteAllText(input, "1\r\n00:00:01,000 --> 00:00:02,000\r\nHello\r\n");

            var settings = new TranslateSubCommand.Settings
            {
                InputFile = input,
                OutputFile = Path.Combine(tempDir, "subs.srt"),
                ToLanguage = " "
            };

            var result = settings.Validate();

            result.Successful.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
