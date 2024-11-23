// PreferencesManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Newtonsoft.Json;
using UnityEngine;

namespace SkripOrderUp
{
    public static class PreferencesManager
    {
        private static readonly string prefsFilePath = Path.Combine(Application.persistentDataPath, "ModData", "Skrip", "OrderUpPrefs.json");
        private static Dictionary<string, object> preferences = new Dictionary<string, object>();

        static PreferencesManager()
        {
            LoadPreferences();
        }

        public static void Set<T>(string key, T value)
        {
            preferences[key] = value;
            SavePreferences();
        }

        public static T Get<T>(string key, T defaultValue = default)
        {
            if (preferences.TryGetValue(key, out object value))
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        public static bool HasKey(string key)
        {
            return preferences.ContainsKey(key);
        }

        private static void LoadPreferences()
        {
            if (File.Exists(prefsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(prefsFilePath);
                    preferences = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[KitchenCustomerInfo] Failed to load preferences: {ex.Message}");
                    preferences = new Dictionary<string, object>();
                }
            }
            else
            {
                preferences = new Dictionary<string, object>();
            }
        }

        private static void SavePreferences()
        {
            try
            {
                string directory = Path.GetDirectoryName(prefsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(preferences, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(prefsFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KitchenCustomerInfo] Failed to save preferences: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes multiple preferences based on the provided keys.
        /// </summary>
        /// <param name="keys">List of preference keys to remove.</param>
        /// <returns>Number of preferences successfully removed.</returns>
        public static int Remove(IEnumerable<string> keys)
        {
            int removedCount = 0;
            foreach (var key in keys)
            {
                if (preferences.Remove(key))
                {
                    removedCount++;
                    Debug.Log($"[KitchenCustomerInfo] Preference '{key}' has been removed.");
                }
                else
                {
                    Debug.LogWarning($"[KitchenCustomerInfo] Preference '{key}' not found. No action taken.");
                }
            }

            if (removedCount > 0)
            {
                SavePreferences();
            }

            return removedCount;
        }

        /// <summary>
        /// Removes the preference associated with the given key and saves the preferences.
        /// </summary>
        /// <param name="key">Preference key to remove.</param>
        /// <returns>True if the key was found and removed; otherwise, false.</returns>
        public static bool Remove(string key)
        {
            if (preferences.Remove(key))
            {
                SavePreferences();
                Debug.Log($"[KitchenCustomerInfo] Preference '{key}' has been removed.");
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
