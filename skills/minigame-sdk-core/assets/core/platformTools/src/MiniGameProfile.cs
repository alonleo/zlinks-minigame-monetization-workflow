using System;
using UnityEngine;

/// <summary>
/// Immutable resolved profile embedded by the unified-matrix build pipeline.
/// Legacy branch-isolated projects may omit the resource and keep using AppConst fallbacks.
/// </summary>
[Serializable]
public sealed class MiniGameResolvedProfile
{
    public int schema_version = 1;
    public string variant_id = "";
    public string platform = "";
    public string display_name = "";
    public string app_id = "";
    public string cdn_prefix = "";
    public string profile_sha256 = "";
    public MiniGameBrandingProfile branding = new MiniGameBrandingProfile();
    public MiniGameGameDataProfile game_data = new MiniGameGameDataProfile();
    public MiniGameAdProfile ads = new MiniGameAdProfile();
    public MiniGameTelemetryProfile telemetry = new MiniGameTelemetryProfile();
}

[Serializable]
public sealed class MiniGameBrandingProfile
{
    public string icon_resource = "";
    public string logo_resource = "";
    public string splash_resource = "";
    public string theme_id = "";
}

[Serializable]
public sealed class MiniGameGameDataProfile
{
    public string profile_id = "";
    public string resource_label = "";
    public string remote_config_key = "";
}

[Serializable]
public sealed class MiniGameAdProfile
{
    public string banner = "";
    public string interstitial = "";
    public string rewarded = "";
}

[Serializable]
public sealed class MiniGameTelemetryProfile
{
    public string profile_id = "";
    public string base_url = "";
    public bool custom_report_enabled;
    public bool dn_enabled;
    public string dn_user_action_set_id = "";
    public bool debug = true;
}

public static class MiniGameProfile
{
    private const string ResourceName = "ResolvedMiniGameProfile";
    private static bool loaded;
    private static MiniGameResolvedProfile current;

    public static MiniGameResolvedProfile CurrentOrNull
    {
        get
        {
            EnsureLoaded();
            return current;
        }
    }

    public static bool IsResolved { get { return CurrentOrNull != null; } }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        TextAsset asset = Resources.Load<TextAsset>(ResourceName);
        if (asset == null) return;
        MiniGameResolvedProfile parsed;
        try { parsed = JsonUtility.FromJson<MiniGameResolvedProfile>(asset.text); }
        catch (Exception error) { throw new InvalidOperationException("Invalid resolved mini-game profile JSON.", error); }
        if (parsed == null || string.IsNullOrWhiteSpace(parsed.variant_id) ||
            string.IsNullOrWhiteSpace(parsed.platform) || string.IsNullOrWhiteSpace(parsed.app_id) ||
            string.IsNullOrWhiteSpace(parsed.display_name) || string.IsNullOrWhiteSpace(parsed.profile_sha256))
            throw new InvalidOperationException("Resolved mini-game profile is incomplete.");
        string compiled = CompiledPlatform();
        if (!string.IsNullOrEmpty(compiled) && !string.Equals(parsed.platform, compiled, StringComparison.Ordinal))
            throw new InvalidOperationException("Resolved profile platform does not match compiled platform: " + parsed.platform + " != " + compiled);
        current = parsed;
    }

    public static string CompiledPlatform()
    {
#if MINIGAME_SUBPLATFORM_DOUYIN
        return "douyin";
#elif WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
        return "wechat";
#else
        return "";
#endif
    }

#if UNITY_EDITOR
    public static void ResetForTests()
    {
        loaded = false;
        current = null;
    }
#endif
}
