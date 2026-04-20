// Summary: Boots the CLI application, configures services, and registers command routes.
using Kitsub.Core;
using Kitsub.Tooling;
using Kitsub.Tooling.Provisioning;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Kitsub.Cli;

/// <summary>Provides the application entry point for the CLI.</summary>
public static class Program
{
    /// <summary>Builds and runs the CLI command application.</summary>
    /// <param name="args">The command-line arguments provided by the user.</param>
    /// <returns>The process exit code produced by the command execution.</returns>
    public static async Task<int> Main(string[] args)
    {
        var configService = new AppConfigService();
        AppConfig? effectiveConfig = null;
        try
        {
            effectiveConfig = configService.LoadEffectiveConfig();
        }
        catch (ConfigurationException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return ExitCodes.ValidationError;
        }

        var bootstrap = ParseBootstrapOptions(args);
        var uiConfig = effectiveConfig.Ui;
        var noBanner = bootstrap.NoBanner || (uiConfig.NoBanner ?? false);
        var noColor = bootstrap.NoColor || (uiConfig.NoColor ?? false);
        var consoleSettings = new AnsiConsoleSettings();
        if (noColor)
        {
            consoleSettings.Ansi = AnsiSupport.No;
            consoleSettings.ColorSystem = ColorSystemSupport.NoColors;
        }

        var console = AnsiConsole.Create(consoleSettings);
        AnsiConsole.Console = console;

        if (!noBanner)
        {
            PrintBanner(console);
        }

        // Block: Configure dependency injection services used by CLI commands.
        var services = new ServiceCollection();
        services.AddSingleton<IAnsiConsole>(console);
        services.AddSingleton(configService);
        var bootstrapLogger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();
        services.AddLogging(builder => builder.AddSerilog(bootstrapLogger, dispose: true));
        services.AddSingleton<ToolManifestLoader>();
        services.AddSingleton<ToolCachePaths>();
        services.AddSingleton<WindowsRidDetector>();
        services.AddSingleton<ToolBundleManager>();
        services.AddSingleton<ToolResolver>();
        services.AddSingleton<StartupStateStore>();
        services.AddSingleton<ToolsStartupCoordinator>();

        // Block: Wire up the Spectre.Console command app and its routes.
        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        var helpOnly = IsHelpInvocation(args);
        using var provider = services.BuildServiceProvider();
        var startupCoordinator = provider.GetRequiredService<ToolsStartupCoordinator>();
        app.Configure(config =>
        {
            config.SetInterceptor(new StartupPromptInterceptor(startupCoordinator, effectiveConfig!, console, helpOnly));
            config.SetExceptionHandler((ex, _) => CliAppExceptionHandler.Handle(ex, console, args));
            // Block: Register top-level commands and command groups for the CLI.
            config.SetApplicationName("kitsub");
            config.AddCommand<InspectCommand>("inspect").WithDescription("Inspect media file.");
            config.AddCommand<MuxCommand>("mux").WithDescription("Mux subtitles into MKV.");
            config.AddCommand<BurnCommand>("burn").WithDescription("Burn subtitles into video.");

            config.AddBranch("fonts", fonts =>
            {
                // Block: Configure commands related to font attachment workflows.
                fonts.SetDescription("Font attachments.");
                fonts.AddCommand<FontsAttachCommand>("attach").WithDescription("Attach fonts to MKV.");
                fonts.AddCommand<FontsCheckCommand>("check").WithDescription("Check fonts in MKV.");
            });

            config.AddBranch("extract", extract =>
            {
                // Block: Configure commands that extract streams from media containers.
                extract.SetDescription("Extract media streams.");
                extract.AddCommand<ExtractAudioCommand>("audio").WithDescription("Extract audio track.");
                extract.AddCommand<ExtractSubCommand>("sub").WithDescription("Extract subtitle track.");
                extract.AddCommand<ExtractVideoCommand>("video").WithDescription("Extract video track.");
            });

            config.AddBranch("convert", convert =>
            {
                // Block: Configure commands for subtitle conversion tasks.
                convert.SetDescription("Convert subtitles.");
                convert.AddCommand<ConvertSubCommand>("sub").WithDescription("Convert subtitle file.");
            });

            config.AddBranch("translate", translate =>
            {
                // Block: Configure commands for subtitle translation tasks.
                translate.SetDescription("Translate subtitles.");
                translate.AddCommand<TranslateSubCommand>("sub").WithDescription("Translate subtitle file with OpenAI.");
            });

            config.AddBranch("tools", tools =>
            {
                // Block: Configure commands for tool status and cache management.
                tools.SetDescription("Tool provisioning and cache management.");
                tools.AddCommand<ToolsStatusCommand>("status").WithDescription("Show resolved tool paths.");
                tools.AddCommand<ToolsFetchCommand>("fetch").WithDescription("Download and cache tool binaries.");
                tools.AddCommand<ToolsCleanCommand>("clean").WithDescription("Delete extracted tool cache.");
            });

            config.AddBranch("release", release =>
            {
                // Block: Configure release workflow commands.
                release.SetDescription("Release workflows.");
                release.AddCommand<ReleaseMuxCommand>("mux").WithDescription("Release mux workflow for MKV files.");
            });

            config.AddBranch("config", configBranch =>
            {
                // Block: Configure commands that manage Kitsub configuration files.
                configBranch.SetDescription("Configuration management.");
                configBranch.AddCommand<ConfigPathCommand>("path").WithDescription("Show resolved configuration paths.");
                configBranch.AddCommand<ConfigInitCommand>("init").WithDescription("Initialize the default configuration file.");
                configBranch.AddCommand<ConfigShowCommand>("show").WithDescription("Display configuration files.");
            });

            config.AddCommand<DoctorCommand>("doctor").WithDescription("Run diagnostics and tool checks.");
        });

        // Block: Run the command app and return its exit code.
        return await app.RunAsync(args).ConfigureAwait(false);
    }

    private static BootstrapOptions ParseBootstrapOptions(string[] args)
    {
        bool noBanner = false;
        bool noColor = false;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (arg.Equals("--no-banner", StringComparison.OrdinalIgnoreCase))
            {
                noBanner = true;
            }
            else if (arg.Equals("--no-color", StringComparison.OrdinalIgnoreCase))
            {
                noColor = true;
            }
        }

        return new BootstrapOptions(noBanner, noColor);
    }

    private static bool IsHelpInvocation(string[] args)
    {
        if (args.Length == 0)
        {
            return true;
        }

        foreach (var arg in args)
        {
            if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-?", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void PrintBanner(IAnsiConsole console)
    {
        const string banner = """
 .'.            ,;     
 ..,c,.       .oKc     
 .  ,k0xxxdooxXNx;.    
   .;kNWMMMMMMMXOx,    
  .lO0KNWMMMMMMMNc     ██ ▄█▀ ▄▄ ▄▄▄▄▄▄ ▄▄▄▄ ▄▄ ▄▄ ▄▄▄▄
.,oocd0XNWMMWKx0Nk;    ████   ██   ██  ███▄▄ ██ ██ ██▄██
..:lc:lkXNKkxod0Ol'    ██ ▀█▄ ██   ██  ▄▄██▀ ▀███▀ ██▄█▀
   .'cdxKXOxkkc..      
      ,kKXXKo.         Video & subtitle build tool.
       :0Xd,.          
        ,:.            
""";
        console.WriteLine(banner);
        console.WriteLine();
    }

    private sealed record BootstrapOptions(bool NoBanner, bool NoColor);

    private sealed class StartupPromptInterceptor : ICommandInterceptor
    {
        private readonly ToolsStartupCoordinator _coordinator;
        private readonly AppConfig _config;
        private readonly IAnsiConsole _console;
        private readonly bool _helpOnly;

        public StartupPromptInterceptor(
            ToolsStartupCoordinator coordinator,
            AppConfig config,
            IAnsiConsole console,
            bool helpOnly)
        {
            _coordinator = coordinator;
            _config = config;
            _console = console;
            _helpOnly = helpOnly;
        }

        public void Intercept(CommandContext context, CommandSettings settings)
        {
            var toolSettings = settings as ToolSettings;
            var globalSettings = settings as GlobalSettings;
            var overrides = toolSettings is null
                ? new ToolOverrides(
                    _config.Tools.Ffmpeg,
                    _config.Tools.Ffprobe,
                    _config.Tools.Mkvmerge,
                    _config.Tools.Mkvpropedit,
                    _config.Tools.Mediainfo)
                : ToolingFactory.BuildToolOverrides(toolSettings);
            var resolveOptions = toolSettings is null
                ? BuildResolveOptionsFromConfig(_config)
                : ToolingFactory.BuildResolveOptions(toolSettings, allowProvisioning: false);

            var options = new ToolsStartupOptions(
                _config.Tools.StartupPrompt ?? true,
                _config.Tools.AutoUpdate ?? false,
                _config.Tools.UpdatePromptOnStartup ?? true,
                _config.Tools.CheckIntervalHours ?? 24,
                globalSettings?.CheckUpdates ?? false,
                globalSettings?.AssumeYes ?? false,
                globalSettings?.NoProvision ?? false,
                globalSettings?.NoStartupPrompt ?? false,
                _helpOnly);

            _coordinator.RunAsync(
                    _console,
                    overrides,
                    resolveOptions,
                    options,
                    context.GetCancellationToken())
                .GetAwaiter()
                .GetResult();
        }

        public void InterceptResult(CommandContext context, CommandSettings settings, ref int result)
        {
        }

        private static ToolResolveOptions BuildResolveOptionsFromConfig(AppConfig config)
        {
            return new ToolResolveOptions
            {
                AllowProvisioning = false,
                PreferBundled = config.Tools.PreferBundled ?? true,
                PreferPath = config.Tools.PreferPath ?? false,
                ToolsCacheDir = config.Tools.ToolsCacheDir,
                DryRun = false,
                Verbose = false
            };
        }
    }
}
