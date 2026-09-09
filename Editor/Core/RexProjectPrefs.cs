using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RexTools.Editor.Core
{
    /// <summary>
    /// Manages persistent per-project tool options stored in UserSettings/RexToolsSettings.json.
    /// Keeps options localized to the current project while avoiding Git clutter.
    /// </summary>
    public static class RexProjectPrefs
    {
        [Serializable]
        private class SettingsData
        {
            public List<SettingEntry> entries = new List<SettingEntry>();
        }

        [Serializable]
        private class SettingEntry
        {
            public string key;
            public string value;
        }

        [Serializable]
        private class JsonWrapper<T>
        {
            public T value;
        }

        private static Dictionary<string, string> _cache;
        private static bool _isLoaded = false;
        private static bool _isDirty = false;

        private static string GetFilePath()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
            {
                projectRoot = Directory.GetCurrentDirectory();
            }
            string userSettingsDir = Path.Combine(projectRoot, "UserSettings");
            return Path.Combine(userSettingsDir, "RexToolsSettings.json");
        }

        private static void EnsureLoaded()
        {
            if (_isLoaded && _cache != null) return;

            _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _isLoaded = true;

            string path = GetFilePath();
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var data = JsonUtility.FromJson<SettingsData>(json);
                    if (data != null && data.entries != null)
                    {
                        foreach (var entry in data.entries)
                        {
                            if (!string.IsNullOrEmpty(entry.key))
                            {
                                _cache[entry.key] = entry.value;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[RexTools] Failed to load {path}: {ex.Message}");
                }
            }
        }

        public static void Save()
        {
            if (!_isDirty && _isLoaded) return;

            EnsureLoaded();
            string path = GetFilePath();
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var data = new SettingsData();
                foreach (var kvp in _cache)
                {
                    data.entries.Add(new SettingEntry { key = kvp.Key, value = kvp.Value });
                }

                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(path, json);
                _isDirty = false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RexTools] Failed to save {path}: {ex.Message}");
            }
        }

        private static string BuildKey(string tool, string key) => $"{tool}_{key}";

        public static void SetString(string tool, string key, string value)
        {
            EnsureLoaded();
            string fullKey = BuildKey(tool, key);
            _cache[fullKey] = value ?? string.Empty;
            _isDirty = true;
            Save();
        }

        public static string GetString(string tool, string key, string defaultValue = "")
        {
            EnsureLoaded();
            string fullKey = BuildKey(tool, key);
            return _cache.TryGetValue(fullKey, out var val) ? val : defaultValue;
        }

        public static void SetBool(string tool, string key, bool value)
        {
            SetString(tool, key, value ? "1" : "0");
        }

        public static bool GetBool(string tool, string key, bool defaultValue = false)
        {
            EnsureLoaded();
            string fullKey = BuildKey(tool, key);
            if (_cache.TryGetValue(fullKey, out var val))
            {
                if (val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
                if (val == "0" || val.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            }
            return defaultValue;
        }

        public static void SetInt(string tool, string key, int value)
        {
            SetString(tool, key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        public static int GetInt(string tool, string key, int defaultValue = 0)
        {
            EnsureLoaded();
            string fullKey = BuildKey(tool, key);
            if (_cache.TryGetValue(fullKey, out var val) && int.TryParse(val, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int result))
            {
                return result;
            }
            return defaultValue;
        }

        public static void SetFloat(string tool, string key, float value)
        {
            SetString(tool, key, value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }

        public static float GetFloat(string tool, string key, float defaultValue = 0f)
        {
            EnsureLoaded();
            string fullKey = BuildKey(tool, key);
            if (_cache.TryGetValue(fullKey, out var val) && float.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float result))
            {
                return result;
            }
            return defaultValue;
        }

        public static void SetObject<T>(string tool, string key, T obj) where T : UnityEngine.Object
        {
            if (obj == null)
            {
                SetString(tool, key, string.Empty);
                return;
            }

            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path))
            {
                SetString(tool, key, string.Empty);
                return;
            }

            string guid = AssetDatabase.AssetPathToGUID(path);
            SetString(tool, key, guid);
        }

        public static T GetObject<T>(string tool, string key) where T : UnityEngine.Object
        {
            string guid = GetString(tool, key, string.Empty);
            if (string.IsNullOrEmpty(guid)) return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;

            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        public static void SetJson<T>(string tool, string key, T data)
        {
            if (data == null)
            {
                SetString(tool, key, string.Empty);
                return;
            }

            try
            {
                var wrapper = new JsonWrapper<T> { value = data };
                string json = JsonUtility.ToJson(wrapper);
                SetString(tool, key, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RexTools] Failed to serialize JSON for {tool}_{key}: {ex.Message}");
            }
        }

        public static T GetJson<T>(string tool, string key, T defaultValue = default)
        {
            string json = GetString(tool, key, string.Empty);
            if (string.IsNullOrEmpty(json)) return defaultValue;

            try
            {
                var wrapper = JsonUtility.FromJson<JsonWrapper<T>>(json);
                return wrapper != null ? wrapper.value : defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        public static bool HasKey(string tool, string key)
        {
            EnsureLoaded();
            return _cache.ContainsKey(BuildKey(tool, key));
        }

        public static void DeleteKey(string tool, string key)
        {
            EnsureLoaded();
            string fullKey = BuildKey(tool, key);
            if (_cache.Remove(fullKey))
            {
                _isDirty = true;
                Save();
            }
        }
    }
}

