// Summary: Loads and validates Kitsub configuration files.
using System.Text.Json;
using System.Text.Json.Serialization;
using Kitsub.Core;

namespace Kitsub.Cli;

/// <summary>Loads Kitsub configuration files and applies overrides.</summary>
public sealed class AppConfigLoader
{
    private const string ConfigFileName = "kitsub.json";
    private const string FfmpegOverrideFileName = "kitsub.ffmpeg.json";
    private const string FfprobeOverrideFileName = "kitsub.ffprobe.json";
    private const string MkvmergeOverrideFileName = "kitsub.mkvmerge.json";
    private const string MkvpropeditOverrideFileName = "kitsub.mkvpropedit.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public ConfigPaths GetConfigPaths()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configRoot = Path.Combine(appData, "Kitsub");
        var globalPath = ResolveConfigPathOverride(Path.Combine(configRoot, ConfigFileName));

        return new ConfigPaths(
            NormalizePath(globalPath),
            NormalizePath(Path.Combine(configRoot, FfmpegOverrideFileName)),
            NormalizePath(Path.Combine(configRoot, FfprobeOverrideFileName)),
            NormalizePath(Path.Combine(configRoot, MkvmergeOverrideFileName)),
            NormalizePath(Path.Combine(configRoot, MkvpropeditOverrideFileName)));
    }

    public ConfigLoadResult LoadGlobalConfig()
    {
        var paths = GetConfigPaths();
        if (!File.Exists(paths.GlobalConfigPath))
        {
            return new ConfigLoadResult(false, paths.GlobalConfigPath, null);
        }

        return new ConfigLoadResult(true, paths.GlobalConfigPath, LoadConfigFile(paths.GlobalConfigPath, isGlobal: true));
    }

    public AppConfig LoadEffectiveConfig()
    {
        var defaults = AppConfigDefaults.CreateDefaults();
        var loaded = LoadGlobalConfig();
        var merged = Merge(defaults, loaded.Config);
        merged = ApplyToolOverrides(merged);
        merged = ApplyEnvironmentOverrides(merged);
        merged = NormalizePaths(merged);
        Validate(merged);
        return merged;
    }

    public static JsonSerializerOptions GetJsonOptions() => JsonOptions;

    private static string ResolveConfigPathOverride(string defaultPath)
    {
        var envOverride = Environment.GetEnvironmentVariable("KITSUB_CONFIG");
        return string.IsNullOrWhiteSpace(envOverride) ? defaultPath : envOverride;
    }

    private static AppConfig LoadConfigFile(string path, bool isGlobal)
    {
        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)
                ?? throw new ConfigurationException($"Configuration file is invalid: {path}. Fix: correct the file or restore from backup.");

            if (config.ConfigVersion != 1)
            {
                throw new ConfigurationException($"Unsupported configVersion {config.ConfigVersion} in {path}. Fix: upgrade the config file to version 1.");
            }

            return config;
        }
        catch (JsonException ex)
        {
            var suffix = isGlobal
                ? $" Fix: restore from {path}.bak or correct the file."
                : string.Empty;
            throw new ConfigurationException($"Configuration file is invalid JSON: {path}.{suffix}", ex);
        }
    }

    private AppConfig ApplyToolOverrides(AppConfig config)
    {
        var paths = GetConfigPaths();
        var overrides = new Dictionary<string, string?>
        {
            ["ffmpeg"] = LoadToolOverride(paths.FfmpegOverridePath),
            ["ffprobe"] = LoadToolOverride(paths.FfprobeOverridePath),
            ["mkvmerge"] = LoadToolOverride(paths.MkvmergeOverridePath),
            ["mkvpropedit"] = LoadToolOverride(paths.MkvpropeditOverridePath)
        };

        return Merge(config, new AppConfig
        {
            Tools = new ToolsConfig
            {
                Ffmpeg = overrides["ffmpeg"],
                Ffprobe = overrides["ffprobe"],
                Mkvmerge = overrides["mkvmerge"],
                Mkvpropedit = overrides["mkvpropedit"]
            }
        });
    }

    private static string? LoadToolOverride(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var overrideConfig = JsonSerializer.Deserialize<ToolPathOverride>(json, JsonOptions);
            return overrideConfig?.Path;
        }
        catch (JsonException ex)
        {
            throw new ConfigurationException($"Tool override file is invalid JSON: {path}. Fix: correct the JSON or delete the override file.", ex);
        }
    }

    private static AppConfig ApplyEnvironmentOverrides(AppConfig config)
    {
        return Merge(config, new AppConfig
        {
            Tools = new ToolsConfig
            {
                Ffmpeg = Environment.GetEnvironmentVariable("KITSUB_FFMPEG"),
                Ffprobe = Environment.GetEnvironmentVariable("KITSUB_FFPROBE"),
                Mkvmerge = Environment.GetEnvironmentVariable("KITSUB_MKVMERGE"),
                Mkvpropedit = Environment.GetEnvironmentVariable("KITSUB_MKVPROPEDIT"),
                Mediainfo = Environment.GetEnvironmentVariable("KITSUB_MEDIAINFO"),
                ToolsCacheDir = Environment.GetEnvironmentVariable("KITSUB_TOOLS_CACHE_DIR")
            }
        });
    }

    private static AppConfig NormalizePaths(AppConfig config)
    {
        return Merge(config, new AppConfig
        {
            Tools = new ToolsConfig
            {
                ToolsCacheDir = NormalizePathOrNull(config.Tools.ToolsCacheDir),
                Ffmpeg = NormalizePathOrNull(config.Tools.Ffmpeg),
                Ffprobe = NormalizePathOrNull(config.Tools.Ffprobe),
                Mkvmerge = NormalizePathOrNull(config.Tools.Mkvmerge),
                Mkvpropedit = NormalizePathOrNull(config.Tools.Mkvpropedit),
                Mediainfo = NormalizePathOrNull(config.Tools.Mediainfo)
            },
            Logging = new LoggingConfig
            {
                LogFile = NormalizePathOrNull(config.Logging.LogFile)
            },
            Defaults = new DefaultsConfig
            {
                Burn = new BurnDefaults
                {
                    FontsDir = NormalizePathOrNull(config.Defaults.Burn.FontsDir)
                }
            }
        });
    }

    private static string NormalizePath(string value)
    {
        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(value));
    }

    private static string? NormalizePathOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : NormalizePath(value);
    }

    private static void Validate(AppConfig config)
    {
        if (config.Defaults.Burn.Crf is < 0 or > 51)
        {
            throw new ConfigurationException("Burn CRF must be between 0 and 51. Fix: update defaults.burn.crf to a value between 0 and 51.");
        }

        if (config.Tools.CheckIntervalHours is < 1)
        {
            throw new ConfigurationException("Tools checkIntervalHours must be at least 1. Fix: set tools.checkIntervalHours to 1 or higher.");
        }

        if (!string.IsNullOrWhiteSpace(config.Logging.LogLevel))
        {
            try
            {
                _ = LogLevelParser.Parse(config.Logging.LogLevel);
            }
            catch (ValidationException ex)
            {
                throw new ConfigurationException(ex.Message, ex);
            }
        }

        if (!string.IsNullOrWhiteSpace(config.OpenAi.BaseUrl) &&
            !Uri.TryCreate(config.OpenAi.BaseUrl, UriKind.Absolute, out _))
        {
            throw new ConfigurationException("OpenAI baseUrl must be an absolute URI. Fix: update openai.baseUrl to a valid absolute URL.");
        }

        ValidateToolPath(config.Tools.Ffmpeg, "ffmpeg");
        ValidateToolPath(config.Tools.Ffprobe, "ffprobe");
        ValidateToolPath(config.Tools.Mkvmerge, "mkvmerge");
        ValidateToolPath(config.Tools.Mkvpropedit, "mkvpropedit");
        ValidateToolPath(config.Tools.Mediainfo, "mediainfo");

        if (!string.IsNullOrWhiteSpace(config.Tools.ToolsCacheDir))
        {
            EnsureDirectoryWritable(config.Tools.ToolsCacheDir);
        }
    }

    private static void ValidateToolPath(string? path, string toolName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!File.Exists(path))
        {
            throw new ConfigurationException($"Configured path for {toolName} does not exist: {path}. Fix: update or remove the tool path.");
        }
    }

    private static void EnsureDirectoryWritable(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probePath = Path.Combine(path, $".kitsub_write_test_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, "kitsub");
            File.Delete(probePath);
        }
        catch (Exception ex)
        {
            throw new ConfigurationException($"Tools cache directory is not writable: {path}. Fix: choose a writable tools cache directory.", ex);
        }
    }

    private static AppConfig Merge(AppConfig baseConfig, AppConfig? overrideConfig)
    {
        if (overrideConfig is null)
        {
            return baseConfig;
        }

        return new AppConfig
        {
            ConfigVersion = overrideConfig.ConfigVersion == 0 ? baseConfig.ConfigVersion : overrideConfig.ConfigVersion,
            Tools = new ToolsConfig
            {
                PreferBundled = overrideConfig.Tools.PreferBundled ?? baseConfig.Tools.PreferBundled,
                PreferPath = overrideConfig.Tools.PreferPath ?? baseConfig.Tools.PreferPath,
                ToolsCacheDir = overrideConfig.Tools.ToolsCacheDir ?? baseConfig.Tools.ToolsCacheDir,
                StartupPrompt = overrideConfig.Tools.StartupPrompt ?? baseConfig.Tools.StartupPrompt,
                CommandPromptOnMissing = overrideConfig.Tools.CommandPromptOnMissing ?? baseConfig.Tools.CommandPromptOnMissing,
                AutoUpdate = overrideConfig.Tools.AutoUpdate ?? baseConfig.Tools.AutoUpdate,
                UpdatePromptOnStartup = overrideConfig.Tools.UpdatePromptOnStartup ?? baseConfig.Tools.UpdatePromptOnStartup,
                CheckIntervalHours = overrideConfig.Tools.CheckIntervalHours ?? baseConfig.Tools.CheckIntervalHours,
                Ffmpeg = overrideConfig.Tools.Ffmpeg ?? baseConfig.Tools.Ffmpeg,
                Ffprobe = overrideConfig.Tools.Ffprobe ?? baseConfig.Tools.Ffprobe,
                Mkvmerge = overrideConfig.Tools.Mkvmerge ?? baseConfig.Tools.Mkvmerge,
                Mkvpropedit = overrideConfig.Tools.Mkvpropedit ?? baseConfig.Tools.Mkvpropedit,
                Mediainfo = overrideConfig.Tools.Mediainfo ?? baseConfig.Tools.Mediainfo
            },
            Logging = new LoggingConfig
            {
                Enabled = overrideConfig.Logging.Enabled ?? baseConfig.Logging.Enabled,
                LogLevel = overrideConfig.Logging.LogLevel ?? baseConfig.Logging.LogLevel,
                LogFile = overrideConfig.Logging.LogFile ?? baseConfig.Logging.LogFile
            },
            Ui = new UiConfig
            {
                NoBanner = overrideConfig.Ui.NoBanner ?? baseConfig.Ui.NoBanner,
                NoColor = overrideConfig.Ui.NoColor ?? baseConfig.Ui.NoColor,
                Progress = overrideConfig.Ui.Progress ?? baseConfig.Ui.Progress
            },
            OpenAi = new OpenAiConfig
            {
                ApiKey = overrideConfig.OpenAi.ApiKey ?? baseConfig.OpenAi.ApiKey,
                Model = overrideConfig.OpenAi.Model ?? baseConfig.OpenAi.Model,
                BaseUrl = overrideConfig.OpenAi.BaseUrl ?? baseConfig.OpenAi.BaseUrl
            },
            Defaults = new DefaultsConfig
            {
                Burn = new BurnDefaults
                {
                    Crf = overrideConfig.Defaults.Burn.Crf ?? baseConfig.Defaults.Burn.Crf,
                    Preset = overrideConfig.Defaults.Burn.Preset ?? baseConfig.Defaults.Burn.Preset,
                    FontsDir = overrideConfig.Defaults.Burn.FontsDir ?? baseConfig.Defaults.Burn.FontsDir
                },
                Mux = new MuxDefaults
                {
                    DefaultLanguage = overrideConfig.Defaults.Mux.DefaultLanguage ?? baseConfig.Defaults.Mux.DefaultLanguage,
                    DefaultTrackName = overrideConfig.Defaults.Mux.DefaultTrackName ?? baseConfig.Defaults.Mux.DefaultTrackName,
                    DefaultDefaultFlag = overrideConfig.Defaults.Mux.DefaultDefaultFlag ?? baseConfig.Defaults.Mux.DefaultDefaultFlag,
                    DefaultForcedFlag = overrideConfig.Defaults.Mux.DefaultForcedFlag ?? baseConfig.Defaults.Mux.DefaultForcedFlag
                },
                Translate = new TranslateDefaults
                {
                    SourceLanguage = overrideConfig.Defaults.Translate.SourceLanguage ?? baseConfig.Defaults.Translate.SourceLanguage,
                    TargetLanguage = overrideConfig.Defaults.Translate.TargetLanguage ?? baseConfig.Defaults.Translate.TargetLanguage
                }
            }
        };
    }

    private sealed class ToolPathOverride
    {
        [JsonPropertyName("path")]
        public string? Path { get; init; }
    }
}
