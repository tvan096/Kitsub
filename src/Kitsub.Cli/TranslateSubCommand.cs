// Summary: Implements the CLI command that translates subtitle files using OpenAI.
using Kitsub.Tooling;
using Kitsub.Tooling.Provisioning;
using Kitsub.Tooling.Translation;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Kitsub.Cli;

/// <summary>Executes subtitle translation using OpenAI.</summary>
public sealed class TranslateSubCommand : CommandBase<TranslateSubCommand.Settings>
{
    private readonly ToolResolver _toolResolver;

    /// <summary>Defines command-line settings for subtitle translation.</summary>
    public sealed class Settings : ToolSettings
    {
        [CommandOption("--in <FILE>")]
        public string InputFile { get; init; } = string.Empty;

        [CommandOption("--out <FILE>")]
        public string OutputFile { get; init; } = string.Empty;

        [CommandOption("--from <LANG>")]
        public string? FromLanguage { get; init; }

        [CommandOption("--to <LANG>")]
        public string? ToLanguage { get; init; }

        [CommandOption("--force")]
        public bool Force { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(InputFile))
            {
                return ValidationResult.Error("Missing required option: --in. Fix: provide --in <file>.");
            }

            var inputValidation = ValidationHelpers.ValidateFileExists(InputFile, "Input file");
            if (!inputValidation.Successful)
            {
                return inputValidation;
            }

            if (string.IsNullOrWhiteSpace(OutputFile))
            {
                return ValidationResult.Error("Missing required option: --out. Fix: provide --out <file>.");
            }

            var subtitleValidation = ValidationHelpers.ValidateSubtitleFile(InputFile, "Input subtitle file");
            if (!subtitleValidation.Successful)
            {
                return subtitleValidation;
            }

            var translationValidation = ValidationHelpers.ValidateSubtitleTranslation(InputFile, OutputFile);
            if (!translationValidation.Successful)
            {
                return translationValidation;
            }

            if (!string.IsNullOrWhiteSpace(FromLanguage))
            {
                var fromValidation = ValidationHelpers.ValidateLanguageTag(FromLanguage, "Source language");
                if (!fromValidation.Successful)
                {
                    return fromValidation;
                }
            }

            if (!string.IsNullOrWhiteSpace(ToLanguage))
            {
                var toValidation = ValidationHelpers.ValidateLanguageTag(ToLanguage, "Target language");
                if (!toValidation.Successful)
                {
                    return toValidation;
                }
            }

            return ValidationResult.Success();
        }
    }

    public TranslateSubCommand(
        IAnsiConsole console,
        ToolResolver toolResolver,
        ToolBundleManager bundleManager,
        WindowsRidDetector ridDetector,
        AppConfigService configService) : base(console, configService, toolResolver, bundleManager, ridDetector)
    {
        _toolResolver = toolResolver;
    }

    protected override async Task<int> ExecuteAsyncCore(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        ValidationHelpers.EnsureOutputPath(
            settings.OutputFile,
            "Output file",
            allowCreateDirectory: true,
            allowOverwrite: settings.Force,
            inputPath: settings.InputFile,
            createDirectory: !settings.DryRun);

        var apiKey = EffectiveConfig.OpenAi.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ValidationException("OpenAI API key is required. Fix: set openai.apiKey in kitsub.json.");
        }

        var sourceLanguage = settings.FromLanguage ?? EffectiveConfig.Defaults.Translate.SourceLanguage ?? "en";
        var sourceValidation = ValidationHelpers.ValidateLanguageTag(sourceLanguage, "Source language");
        if (!sourceValidation.Successful)
        {
            throw new ValidationException(sourceValidation.Message!);
        }

        var targetLanguage = settings.ToLanguage ?? EffectiveConfig.Defaults.Translate.TargetLanguage;
        if (string.IsNullOrWhiteSpace(targetLanguage))
        {
            throw new ValidationException("Missing required option: --to. Fix: provide --to <language> or configure defaults.translate.targetLanguage.");
        }

        var targetValidation = ValidationHelpers.ValidateLanguageTag(targetLanguage, "Target language");
        if (!targetValidation.Successful)
        {
            throw new ValidationException(targetValidation.Message!);
        }

        var translationOptions = new SubtitleTranslationOptions(
            apiKey,
            EffectiveConfig.OpenAi.Model ?? "gpt-4o-mini",
            sourceLanguage,
            targetLanguage,
            EffectiveConfig.OpenAi.BaseUrl);

        if (settings.DryRun)
        {
            Console.MarkupLine($"[grey]Translate subtitles:[/] {Markup.Escape(settings.InputFile)} -> {Markup.Escape(settings.OutputFile)}");
            Console.MarkupLine($"[grey]Model:[/] {Markup.Escape(translationOptions.Model)} [grey]From:[/] {Markup.Escape(sourceLanguage)} [grey]To:[/] {Markup.Escape(targetLanguage)}");
            return ExitCodes.Success;
        }

        using var tooling = ToolingFactory.CreateTooling(settings, Console, _toolResolver);
        await tooling.Service.TranslateSubtitleAsync(
            settings.InputFile,
            settings.OutputFile,
            translationOptions,
            cancellationToken).ConfigureAwait(false);

        var outputValidation = ValidationHelpers.ValidateSubtitleFile(settings.OutputFile, "Translated subtitle file");
        if (!outputValidation.Successful)
        {
            throw new ValidationException(outputValidation.Message!);
        }

        Console.MarkupLine($"[green]Translated subtitles to[/] {Markup.Escape(settings.OutputFile)}");
        return ExitCodes.Success;
    }
}
