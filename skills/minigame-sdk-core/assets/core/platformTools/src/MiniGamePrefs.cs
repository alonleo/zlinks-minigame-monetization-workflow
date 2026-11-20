using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Mini-game-safe PlayerPrefs facade.
/// WebGL mini-game builds persist one JSON file in the host USER_DATA_PATH;
/// Editor and non-mini-game builds keep UnityEngine.PlayerPrefs behavior.
/// </summary>
public static class MiniGamePrefs
{
    private const string NativeFileName = "minigamekit-prefs.json";
    private static JObject values;
    private static bool loaded;
    private static bool dirty;

    [DllImport("__Internal")] private static extern string MGKPrefsRead(string fileName);
    [DllImport("__Internal")] private static extern int MGKPrefsWrite(string fileName, string json);

    private static bool IsNativeMiniGame
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR && (WEIXINMINIGAME || MINIGAME_SUBPLATFORM_DOUYIN)
            return true;
#else
            return false;
#endif
        }
    }

    public static int GetInt(string key, int defaultValue = 0)
    {
        if (!IsNativeMiniGame) return UnityEngine.PlayerPrefs.GetInt(key, defaultValue);
        EnsureLoaded();
        JToken token = values[key];
        int result;
        if (token != null && TryInt(token, out result)) return result;
        if (!UnityEngine.PlayerPrefs.HasKey(key)) return defaultValue;
        result = UnityEngine.PlayerPrefs.GetInt(key, defaultValue);
        ImportLegacy(key, result);
        return result;
    }

    public static float GetFloat(string key, float defaultValue = 0f)
    {
        if (!IsNativeMiniGame) return UnityEngine.PlayerPrefs.GetFloat(key, defaultValue);
        EnsureLoaded();
        JToken token = values[key];
        float result;
        if (token != null && float.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result)) return result;
        if (!UnityEngine.PlayerPrefs.HasKey(key)) return defaultValue;
        result = UnityEngine.PlayerPrefs.GetFloat(key, defaultValue);
        ImportLegacy(key, result);
        return result;
    }

    public static string GetString(string key, string defaultValue = "")
    {
        if (!IsNativeMiniGame) return UnityEngine.PlayerPrefs.GetString(key, defaultValue);
        EnsureLoaded();
        JToken token = values[key];
        if (token != null && token.Type != JTokenType.Null) return token.Type == JTokenType.String ? (string)token : token.ToString(Formatting.None);
        if (!UnityEngine.PlayerPrefs.HasKey(key)) return defaultValue;
        string result = UnityEngine.PlayerPrefs.GetString(key, defaultValue);
        ImportLegacy(key, result);
        return result;
    }

    public static void SetInt(string key, int value)
    {
        UnityEngine.PlayerPrefs.SetInt(key, value);
        if (!IsNativeMiniGame) return;
        EnsureLoaded(); values[key] = value; dirty = true;
    }

    public static void SetFloat(string key, float value)
    {
        UnityEngine.PlayerPrefs.SetFloat(key, value);
        if (!IsNativeMiniGame) return;
        EnsureLoaded(); values[key] = value; dirty = true;
    }

    public static void SetString(string key, string value)
    {
        value = value ?? string.Empty;
        UnityEngine.PlayerPrefs.SetString(key, value);
        if (!IsNativeMiniGame) return;
        EnsureLoaded(); values[key] = value; dirty = true;
    }

    public static bool HasKey(string key)
    {
        if (!IsNativeMiniGame) return UnityEngine.PlayerPrefs.HasKey(key);
        EnsureLoaded();
        return values.Property(key) != null || UnityEngine.PlayerPrefs.HasKey(key);
    }

    public static void DeleteKey(string key)
    {
        UnityEngine.PlayerPrefs.DeleteKey(key);
        if (!IsNativeMiniGame) return;
        EnsureLoaded();
        JProperty property = values.Property(key);
        if (property != null) { property.Remove(); dirty = true; }
    }

    public static void Save()
    {
        if (IsNativeMiniGame) PersistNative();
        UnityEngine.PlayerPrefs.Save();
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        values = new JObject();
        try
        {
            string raw = MGKPrefsRead(NativeFileName);
            if (!string.IsNullOrWhiteSpace(raw)) values = JObject.Parse(raw);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[MiniGameKit] 原生存档读取失败: " + e.GetType().Name);
        }
    }

    private static void ImportLegacy(string key, JToken value)
    {
        values[key] = value;
        dirty = true;
        PersistNative();
    }

    private static void PersistNative()
    {
        EnsureLoaded();
        if (!dirty) return;
        try
        {
            if (MGKPrefsWrite(NativeFileName, values.ToString(Formatting.None)) == 1) dirty = false;
            else Debug.LogWarning("[MiniGameKit] 原生存档写入失败");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[MiniGameKit] 原生存档写入失败: " + e.GetType().Name);
        }
    }

    private static bool TryInt(JToken token, out int value)
    {
        if (token.Type == JTokenType.Integer)
        {
            try { value = token.Value<int>(); return true; } catch { }
        }
        return int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
