using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Models;

namespace AllStartingBonuses;

public class ModConfig
{
    public const string ConfigFileName = "AllStartingBonuses.config.json";

    /// <summary>
    /// If true, overrides individual Ancient settings and unlocks all options for every Ancient.
    /// Default: false.
    /// </summary>
    public bool UnlockAll { get; set; } = false;

    /// <summary>
    /// Individual toggle for each Ancient in Slay the Spire 2.
    /// Default: Neow is true, all other Ancients are false.
    /// </summary>
    public Dictionary<string, bool> Ancients { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Neow", true },
        { "Darv", false },
        { "Nonupeipe", false },
        { "Orobas", false },
        { "Pael", false },
        { "Tanx", false },
        { "Tezcatara", false },
        { "Vakuu", false }
    };

    public static ModConfig Current { get; private set; } = new();

    public static void Load()
    {
        try
        {
            string configPath = GetConfigFilePath();
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                var loaded = JsonSerializer.Deserialize<ModConfig>(json, options);
                if (loaded != null)
                {
                    if (loaded.Ancients != null)
                    {
                        loaded.Ancients = new Dictionary<string, bool>(loaded.Ancients, StringComparer.OrdinalIgnoreCase);
                    }
                    Current = loaded;
                    GD.Print($"[AllStartingBonuses] Loaded config from '{configPath}' (UnlockAll: {Current.UnlockAll}, Neow: {ShouldUnlock("Neow")})");
                    return;
                }
            }
            else
            {
                SaveDefault(configPath);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[AllStartingBonuses] Failed to load config, using defaults: {ex.Message}");
        }

        Current = new ModConfig();
    }

    public static void SaveDefault(string configPath)
    {
        try
        {
            string? dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(new ModConfig(), options);
            File.WriteAllText(configPath, json);
            GD.Print($"[AllStartingBonuses] Created default config at '{configPath}'");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[AllStartingBonuses] Failed to create default config: {ex.Message}");
        }
    }

    private static string GetConfigFilePath()
    {
        string? asmLocation = typeof(ModConfig).Assembly.Location;
        if (!string.IsNullOrEmpty(asmLocation))
        {
            string asmDir = Path.GetDirectoryName(asmLocation)!;
            string directPath = Path.Combine(asmDir, ConfigFileName);
            if (File.Exists(directPath) || Directory.Exists(asmDir))
            {
                return directPath;
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "mods", ConfigFileName);
    }

    public static bool ShouldUnlockFor(AncientEventModel model)
    {
        if (Current.UnlockAll)
        {
            return true;
        }

        string typeName = model.GetType().Name;
        return ShouldUnlock(typeName) || (model.Id != null && ShouldUnlock(model.Id.ToString()));
    }

    public static bool ShouldUnlock(string nameOrId)
    {
        if (Current.UnlockAll)
        {
            return true;
        }

        if (Current.Ancients == null)
        {
            return nameOrId.Equals("Neow", StringComparison.OrdinalIgnoreCase);
        }

        if (Current.Ancients.TryGetValue(nameOrId, out bool enabled))
        {
            return enabled;
        }

        string cleanName = nameOrId.StartsWith("EVENT.", StringComparison.OrdinalIgnoreCase)
            ? nameOrId.Substring("EVENT.".Length)
            : nameOrId;

        if (Current.Ancients.TryGetValue(cleanName, out bool enabledClean))
        {
            return enabledClean;
        }

        return cleanName.Equals("Neow", StringComparison.OrdinalIgnoreCase);
    }
}
