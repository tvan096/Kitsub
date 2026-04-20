using FluentAssertions;
using Kitsub.Cli;
using Xunit;

namespace Kitsub.Cli.Tests;

public class AppConfigDefaultsTests
{
    [Fact]
    public void CreateDefaults_ReturnsExpectedBaselineValues()
    {
        var config = AppConfigDefaults.CreateDefaults();

        config.ConfigVersion.Should().Be(1);
        config.Tools.PreferBundled.Should().BeTrue();
        config.Tools.PreferPath.Should().BeFalse();
        config.Tools.StartupPrompt.Should().BeTrue();
        config.Tools.CommandPromptOnMissing.Should().BeTrue();
        config.Tools.AutoUpdate.Should().BeFalse();
        config.Tools.UpdatePromptOnStartup.Should().BeTrue();
        config.Tools.CheckIntervalHours.Should().Be(24);
        config.Logging.Enabled.Should().BeTrue();
        config.Logging.LogLevel.Should().Be("info");
        config.Logging.LogFile.Should().Be("logs/kitsub.log");
        config.Ui.NoBanner.Should().BeFalse();
        config.Ui.NoColor.Should().BeFalse();
        config.Ui.Progress.Should().Be(UiProgressMode.Auto);
        config.OpenAi.ApiKey.Should().BeNull();
        config.OpenAi.Model.Should().Be("gpt-4o-mini");
        config.Defaults.Translate.SourceLanguage.Should().Be("en");
        config.Defaults.Translate.TargetLanguage.Should().BeNull();
        config.Defaults.Burn.Crf.Should().Be(18);
        config.Defaults.Burn.Preset.Should().Be("medium");
    }

    [Fact]
    public void CreateDefaults_DoesNotSetOptionalToolPaths()
    {
        var config = AppConfigDefaults.CreateDefaults();

        config.Tools.Ffmpeg.Should().BeNull();
        config.Tools.Ffprobe.Should().BeNull();
        config.Tools.Mkvmerge.Should().BeNull();
        config.Tools.Mkvpropedit.Should().BeNull();
        config.Tools.Mediainfo.Should().BeNull();
    }
}
