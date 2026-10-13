using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BetterMouse.Core;

namespace BetterMouse.Configuration;

internal static class ConfigStore
{
    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BetterMouse");

    public static string FilePath => Path.Combine(DirectoryPath, "config.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static MouseMappingConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var config = JsonSerializer.Deserialize<MouseMappingConfig>(json, Options);
                if (config is not null)
                    return config;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }

        return new MouseMappingConfig();
    }

    public static void Save(MouseMappingConfig config)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(config, Options));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }
}
