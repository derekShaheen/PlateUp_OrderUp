using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SkripOrderUp
{
    internal sealed class DisplayNameReplacement
    {
        public string Find { get; set; }
        public string ReplaceWith { get; set; }
    }

    internal sealed class DisplayNameOverride
    {
        public string Contains { get; set; }
        public string DisplayName { get; set; }
    }

    internal sealed class DisplayNameConfiguration
    {
        public int BundledDefaultsVersion { get; set; }

        public List<DisplayNameReplacement> DishAndIngredientReplacements { get; set; }
            = new List<DisplayNameReplacement>();

        public List<DisplayNameReplacement> IngredientOnlyReplacements { get; set; }
            = new List<DisplayNameReplacement>();

        public List<DisplayNameOverride> ContainsOverrides { get; set; }
            = new List<DisplayNameOverride>();

    }

    internal static class DisplayNameConfig
    {
        private const string FileName = "OrderUp.DisplayNames.json";
        private static DisplayNameConfiguration _configuration;
        private static bool _lastFileExists;
        private static DateTime _lastWriteTimeUtc;
        private static long _lastFileLength = -1;

        public static string ConfigPath
        {
            get
            {
                return Path.Combine(
                    Application.persistentDataPath,
                    "ModData",
                    "Skrip",
                    FileName);
            }
        }

        public static void Load()
        {
            DisplayNameConfiguration defaults;
            try
            {
                defaults = LoadBundledDefaults();
            }
            catch (Exception ex)
            {
                defaults = new DisplayNameConfiguration();
                Debug.LogError(
                    "[OrderUp] Failed to load the bundled display-name defaults. " + ex.Message);
            }

            try
            {
                if (!File.Exists(ConfigPath))
                {
                    _configuration = defaults;
                    EnsureConfigDirectory();
                    File.WriteAllText(
                        ConfigPath,
                        JsonConvert.SerializeObject(_configuration, Formatting.Indented));
                    RememberFileState();
                    Debug.Log("[OrderUp] Created display-name config at " + ConfigPath);
                    return;
                }

                string json = File.ReadAllText(ConfigPath);
                _configuration = JsonConvert.DeserializeObject<DisplayNameConfiguration>(json);
                if (_configuration == null)
                    throw new InvalidDataException("The display-name config was empty.");

                Normalize(_configuration);
                if (MigrateBundledDefaults(_configuration, defaults))
                {
                    try
                    {
                        File.WriteAllText(
                            ConfigPath,
                            JsonConvert.SerializeObject(_configuration, Formatting.Indented));
                        Debug.Log(
                            "[OrderUp] Migrated display-name config to bundled defaults version " +
                            _configuration.BundledDefaultsVersion + ".");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(
                            "[OrderUp] Applied display-name migrations in memory, but failed " +
                            "to save the config. " + ex.Message);
                    }
                }

                RememberFileState();
                Debug.Log("[OrderUp] Loaded display-name config from " + ConfigPath);
            }
            catch (Exception ex)
            {
                _configuration = defaults;
                RememberFileState();
                Debug.LogError(
                    "[OrderUp] Failed to load display-name config; using built-in defaults. " +
                    ex.Message);
            }
        }

        public static void ReloadIfChanged()
        {
            try
            {
                var file = new FileInfo(ConfigPath);
                if (file.Exists != _lastFileExists ||
                    (file.Exists &&
                     (file.LastWriteTimeUtc != _lastWriteTimeUtc ||
                      file.Length != _lastFileLength)))
                {
                    Load();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[OrderUp] Failed to check the display-name config for changes. " +
                    ex.Message);
            }
        }

        public static string Clean(string name, bool isIngredient)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            if (_configuration == null)
                Load();

            string displayName = ApplyReplacements(
                name,
                _configuration.DishAndIngredientReplacements).Trim();

            if (isIngredient)
            {
                displayName = ApplyReplacements(
                    displayName,
                    _configuration.IngredientOnlyReplacements).Trim();
            }

            for (int i = 0; i < _configuration.ContainsOverrides.Count; i++)
            {
                DisplayNameOverride rule = _configuration.ContainsOverrides[i];
                if (!string.IsNullOrEmpty(rule.Contains) && displayName.Contains(rule.Contains))
                {
                    displayName = rule.DisplayName ?? string.Empty;
                    break;
                }
            }

            while (displayName.Contains("  "))
                displayName = displayName.Replace("  ", " ");

            return displayName;
        }

        private static string ApplyReplacements(
            string value,
            List<DisplayNameReplacement> replacements)
        {
            for (int i = 0; i < replacements.Count; i++)
            {
                DisplayNameReplacement rule = replacements[i];
                if (string.IsNullOrEmpty(rule.Find))
                    continue;

                value = value.Replace(rule.Find, rule.ReplaceWith ?? string.Empty);
            }

            return value;
        }

        private static void Normalize(DisplayNameConfiguration configuration)
        {
            if (configuration.DishAndIngredientReplacements == null)
                configuration.DishAndIngredientReplacements = new List<DisplayNameReplacement>();
            if (configuration.IngredientOnlyReplacements == null)
                configuration.IngredientOnlyReplacements = new List<DisplayNameReplacement>();
            if (configuration.ContainsOverrides == null)
                configuration.ContainsOverrides = new List<DisplayNameOverride>();
        }

        private static bool MigrateBundledDefaults(
            DisplayNameConfiguration configuration,
            DisplayNameConfiguration defaults)
        {
            if (configuration.BundledDefaultsVersion >= defaults.BundledDefaultsVersion)
                return false;

            AddMissingReplacements(
                configuration.DishAndIngredientReplacements,
                defaults.DishAndIngredientReplacements);
            AddMissingReplacements(
                configuration.IngredientOnlyReplacements,
                defaults.IngredientOnlyReplacements);
            AddMissingOverrides(
                configuration.ContainsOverrides,
                defaults.ContainsOverrides);

            configuration.BundledDefaultsVersion = defaults.BundledDefaultsVersion;
            return true;
        }

        private static void AddMissingReplacements(
            List<DisplayNameReplacement> configuration,
            List<DisplayNameReplacement> defaults)
        {
            for (int i = 0; i < defaults.Count; i++)
            {
                DisplayNameReplacement bundledRule = defaults[i];
                bool exists = false;

                for (int j = 0; j < configuration.Count; j++)
                {
                    if (string.Equals(
                        configuration[j].Find,
                        bundledRule.Find,
                        StringComparison.Ordinal))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    configuration.Add(new DisplayNameReplacement
                    {
                        Find = bundledRule.Find,
                        ReplaceWith = bundledRule.ReplaceWith
                    });
                }
            }
        }

        private static void AddMissingOverrides(
            List<DisplayNameOverride> configuration,
            List<DisplayNameOverride> defaults)
        {
            for (int i = 0; i < defaults.Count; i++)
            {
                DisplayNameOverride bundledRule = defaults[i];
                bool exists = false;

                for (int j = 0; j < configuration.Count; j++)
                {
                    if (string.Equals(
                        configuration[j].Contains,
                        bundledRule.Contains,
                        StringComparison.Ordinal))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    configuration.Add(new DisplayNameOverride
                    {
                        Contains = bundledRule.Contains,
                        DisplayName = bundledRule.DisplayName
                    });
                }
            }
        }

        private static void RememberFileState()
        {
            var file = new FileInfo(ConfigPath);
            if (!file.Exists)
            {
                _lastFileExists = false;
                _lastWriteTimeUtc = DateTime.MinValue;
                _lastFileLength = -1;
                return;
            }

            _lastFileExists = true;
            _lastWriteTimeUtc = file.LastWriteTimeUtc;
            _lastFileLength = file.Length;
        }

        private static void EnsureConfigDirectory()
        {
            string directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        private static DisplayNameConfiguration LoadBundledDefaults()
        {
            System.Reflection.Assembly assembly =
                System.Reflection.Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(
                "SkripOrderUp.OrderUp.DisplayNames.json"))
            {
                if (stream == null)
                    throw new InvalidDataException("The embedded JSON resource was not found.");

                using (var reader = new StreamReader(stream))
                {
                    DisplayNameConfiguration configuration =
                        JsonConvert.DeserializeObject<DisplayNameConfiguration>(reader.ReadToEnd());
                    if (configuration == null)
                        throw new InvalidDataException("The embedded JSON resource was empty.");

                    Normalize(configuration);
                    return configuration;
                }
            }
        }
    }
}
