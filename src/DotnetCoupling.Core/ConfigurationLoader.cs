using System.Text.Json;
using Tomlyn;

namespace DotnetCoupling.Core;

public static class ConfigurationLoader
{
    private static readonly string[] JsonConfigFileNames = [".coupling.json", "coupling.json"];
    private static readonly string[] TomlConfigFileNames = [".coupling.toml", "coupling.toml"];

    public static ConfigurationLoadResult Load(string targetPath, FileInfo? explicitConfig)
    {
        FileInfo? configFile = explicitConfig ?? FindConfigFile(targetPath);
        if (configFile is null)
        {
            return new ConfigurationLoadResult(AnalysisOptions.Default, null, []);
        }

        if (!configFile.Exists)
        {
            throw new ConfigurationException($"Configuration file does not exist: {configFile.FullName}");
        }

        if (!IsSupportedConfigFile(configFile))
        {
            throw new ConfigurationException($"Unsupported configuration file format: {configFile.FullName}");
        }

        try
        {
            RawConfiguration rawConfiguration = IsJsonConfigFile(configFile)
                ? JsonConfigurationReader.Load(configFile)
                : TomlConfigurationReader.Load(configFile);
            return new ConfigurationLoadResult(ConfigurationOptionsFactory.Create(rawConfiguration), configFile.FullName, []);
        }
        catch (JsonException ex)
        {
            throw new ConfigurationException($"Invalid JSON configuration: {ex.Message}");
        }
        catch (TomlException ex)
        {
            throw new ConfigurationException($"Invalid TOML configuration: {ex.Message}");
        }
    }

    private static FileInfo? FindConfigFile(string targetPath)
    {
        DirectoryInfo? directory = File.Exists(targetPath)
            ? new FileInfo(targetPath).Directory
            : new DirectoryInfo(targetPath);

        FileInfo? jsonConfig = FindConfigFile(directory, JsonConfigFileNames);
        return jsonConfig ?? FindConfigFile(directory, TomlConfigFileNames);
    }

    private static FileInfo? FindConfigFile(DirectoryInfo? directory, IReadOnlyList<string> fileNames)
    {
        while (directory is not null)
        {
            foreach (string fileName in fileNames)
            {
                FileInfo config = new(Path.Combine(directory.FullName, fileName));
                if (config.Exists)
                {
                    return config;
                }
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static bool IsSupportedConfigFile(FileInfo configFile) =>
        IsJsonConfigFile(configFile) || IsTomlConfigFile(configFile);

    private static bool IsJsonConfigFile(FileInfo configFile) =>
        string.Equals(configFile.Extension, ".json", StringComparison.OrdinalIgnoreCase);

    private static bool IsTomlConfigFile(FileInfo configFile) =>
        string.Equals(configFile.Extension, ".toml", StringComparison.OrdinalIgnoreCase);
}

public sealed record ConfigurationLoadResult(
    AnalysisOptions Options,
    string? ConfigPath,
    IReadOnlyList<string> Warnings);

public sealed class ConfigurationException(string message) : Exception(message);
