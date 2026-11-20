using UnityEngine;

/// <summary>
/// 全局常量配置：上报地址、appId、广告位 ID 等环境相关常量集中管理。
/// 通过编译宏切换平台：
///   - MINIGAME_SUBPLATFORM_DOUYIN → 抖音
///   - WEIXINMINIGAME → 微信
///   - 其他 → 默认（编辑器 / WebGL）
/// 分支隔离模式可使用本文件常量；统一矩阵模式优先读取 ResolvedMiniGameProfile。
/// </summary>
public static class AppConst
{
    private static MiniGameResolvedProfile ResolvedProfile { get { return MiniGameProfile.CurrentOrNull; } }

    public static string VariantId { get { return ResolvedProfile != null ? ResolvedProfile.variant_id : "legacy"; } }
    public static string DisplayName { get { return ResolvedProfile != null ? ResolvedProfile.display_name : ""; } }
    public static string ProfileSha256 { get { return ResolvedProfile != null ? ResolvedProfile.profile_sha256 : ""; } }
    public static string CdnPrefix { get { return ResolvedProfile != null ? ResolvedProfile.cdn_prefix : ""; } }
    public static bool IsWeixinPlatform
    {
        get
        {
            if (ResolvedProfile != null) return ResolvedProfile.platform == "wechat";
#if WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            return true;
#else
            return false;
#endif
        }
    }

    // ==================== 小游戏 appId ====================

    /// <summary>抖音小游戏 appId</summary>
    public const string DouyinAppId = "";

    /// <summary>微信小游戏 appId</summary>
    public const string WeixinAppId = "";

    /// <summary>当前平台 appId（按编译宏自动选择）</summary>
    public static string AppId
    {
        get
        {
            if (ResolvedProfile != null) return ResolvedProfile.app_id;
#if MINIGAME_SUBPLATFORM_DOUYIN
            return DouyinAppId;
#elif WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            return WeixinAppId;
#else
            return "";
#endif
        }
    }

    // ==================== 广告位 ID ====================

    // ---- 抖音 ----
    /// <summary>抖音 Banner 广告位 ID（上架前替换为正式值）</summary>
    public const string DouyinBannerId = "";
    /// <summary>抖音插屏广告位 ID</summary>
    public const string DouyinInterstitialId = "";
    /// <summary>抖音激励视频广告位 ID</summary>
    public const string DouyinRewardVideoId = "";

    // ---- 微信 ----
    /// <summary>微信 Banner 广告位 ID（上架前替换为正式值）</summary>
    public const string WeixinBannerId = "";
    /// <summary>微信插屏广告位 ID</summary>
    public const string WeixinInterstitialId = "";
    /// <summary>微信激励视频广告位 ID</summary>
    public const string WeixinRewardVideoId = "";

    /// <summary>当前平台 Banner 广告位 ID</summary>
    public static string BannerId
    {
        get
        {
            if (ResolvedProfile != null && ResolvedProfile.ads != null) return ResolvedProfile.ads.banner ?? "";
#if MINIGAME_SUBPLATFORM_DOUYIN
            return DouyinBannerId;
#elif WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            return WeixinBannerId;
#else
            return "";
#endif
        }
    }

    /// <summary>当前平台插屏广告位 ID</summary>
    public static string InterstitialId
    {
        get
        {
            if (ResolvedProfile != null && ResolvedProfile.ads != null) return ResolvedProfile.ads.interstitial ?? "";
#if MINIGAME_SUBPLATFORM_DOUYIN
            return DouyinInterstitialId;
#elif WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            return WeixinInterstitialId;
#else
            return "";
#endif
        }
    }

    /// <summary>当前平台激励视频广告位 ID</summary>
    public static string RewardVideoId
    {
        get
        {
            if (ResolvedProfile != null && ResolvedProfile.ads != null) return ResolvedProfile.ads.rewarded ?? "";
#if MINIGAME_SUBPLATFORM_DOUYIN
            return DouyinRewardVideoId;
#elif WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            return WeixinRewardVideoId;
#else
            return "";
#endif
        }
    }

    // ==================== 腾讯营销小游戏 SDK（DataNexus / dn-sdk） ====================

    /// <summary>
    /// 腾讯营销 DataNexus 数据源 ID（user_action_set_id）。
    /// 在 DataNexus 后台创建数据源后填入，留空时 dn-sdk 不初始化。
    /// </summary>
    public const string DnUserActionSetId = "";

    /// <summary>
    /// 腾讯营销 DataNexus 数据源 secret_key。
    /// 在 DataNexus 后台创建数据源后填入，留空时 dn-sdk 不初始化。
    /// </summary>
    public const string DnSecretKey = "";

    /// <summary>
    /// 是否启用腾讯营销 SDK 上报（dn-sdk）。
    /// 仅微信平台且配置完整时生效。
    /// </summary>
    public const bool DnSDKEnabled = false;
    public const bool CustomReportEnabled = false;
    public const bool TelemetryDebug = true;
    public const bool UseTestTelemetry = false;
    public static bool DnSDKConfigured
    {
        get
        {
            return ResolvedProfile != null && ResolvedProfile.telemetry != null
                ? ResolvedProfile.telemetry.dn_enabled
                : DnSDKEnabled;
        }
    }
    public static string EffectiveDnUserActionSetId
    {
        get
        {
            return ResolvedProfile != null && ResolvedProfile.telemetry != null &&
                !string.IsNullOrWhiteSpace(ResolvedProfile.telemetry.dn_user_action_set_id)
                ? ResolvedProfile.telemetry.dn_user_action_set_id
                : DnUserActionSetId;
        }
    }
    public static string EffectiveDnSecretKey
    {
        get
        {
#if UNITY_EDITOR
            string injected = System.Environment.GetEnvironmentVariable("MINIGAME_DN_SECRET_KEY");
            if (!string.IsNullOrWhiteSpace(injected)) return injected;
#endif
            return DnSecretKey;
        }
    }
    public static bool TelemetryDebugEnabled
    {
        get
        {
            return ResolvedProfile != null && ResolvedProfile.telemetry != null
                ? ResolvedProfile.telemetry.debug
                : TelemetryDebug;
        }
    }
    public static bool IsMiniGameRuntime
    {
        get {
#if (WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN || MINIGAME_SUBPLATFORM_DOUYIN) && !UNITY_EDITOR && !MINIGAME_KIT_BROWSER_TEST
            return true;
#else
            return false;
#endif
        }
    }
    public static bool HasValidDnCredentials
    {
        get {
            long id;
            return !string.IsNullOrWhiteSpace(AppId)
                && System.Text.RegularExpressions.Regex.IsMatch(EffectiveDnUserActionSetId ?? "", @"^[1-9][0-9]*$")
                && long.TryParse(EffectiveDnUserActionSetId, out id) && id <= 9007199254740991L
                && EffectiveDnSecretKey != null && EffectiveDnSecretKey.Length == 32 && EffectiveDnSecretKey.Trim() == EffectiveDnSecretKey;
        }
    }
    public static bool EnableDnSDK
    {
        get {
#if (WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN) && !UNITY_EDITOR && !MINIGAME_KIT_BROWSER_TEST
            return DnSDKConfigured;
#else
            return false;
#endif
        }
    }

    // ==================== 上报开关 ====================

    /// <summary>
    /// 开关1：是否启用自定义服务器上报（激活 / 归因 / 业务打点 → 自有服务器）。
    /// 由 CustomReportEnabled 手动开启，默认关闭。
    /// 影响：TrackManager.Track / InitAscribe / ReportExposure
    /// </summary>
    public static bool EnableCustomReport
    {
        get
        {
            bool configured = ResolvedProfile != null && ResolvedProfile.telemetry != null
                ? ResolvedProfile.telemetry.custom_report_enabled
                : CustomReportEnabled;
            return IsMiniGameRuntime && configured;
        }
    }

    /// <summary>
    /// 开关2：是否启用平台原生上报（tt.reportAnalytics / wx.reportEvent → 平台数据看板）。
    /// 目前固定为 true，所有平台均启用。
    /// 影响：PlatformBase.ReportEvent → 平台 SDK 原生上报
    /// </summary>
    public const bool EnablePlatformReport = true;

    // ==================== 调试开关 ====================

    /// <summary>
    /// 调试钩子：QA 面板「模拟看广告」开启时，激励视频立即走成功回调。
    /// 仅开发者手动配置；不由远程配置、空 ID 或 UI 连击改写。
    /// </summary>
    public static bool isNoAd = false;

    // ==================== 归因回传服务 ====================

    /// <summary>测试环境归因服务 Base URL（抖音）</summary>
    private const string Dy_BaseUrl_Test = "http://192.168.1.19:8002/dy-ascribe-server";
    /// <summary>生产环境归因服务 Base URL（抖音）</summary>
    private const string Dy_BaseUrl_Release = "https://mini.zllinks.com/dy-ascribe-server";

    /// <summary>测试环境归因服务 Base URL（微信）</summary>
    private const string Wx_BaseUrl_Test = "http://192.168.1.19:8002/wx-ascribe-server";
    /// <summary>生产环境归因服务 Base URL（微信）</summary>
    private const string Wx_BaseUrl_Release = "https://mini.zllinks.com/wx-ascribe-server";

    /// <summary>
    /// 归因服务 Base URL（按环境 + 平台自动选择）。
    /// 编辑器/测试构建 → 测试环境；正式包 → 生产环境。
    /// 抖音平台 → dy-ascribe-server；微信平台 → wx-ascribe-server。
    /// </summary>
    public static string BaseUrl
    {
        get {
            if (ResolvedProfile != null && ResolvedProfile.telemetry != null &&
                !string.IsNullOrWhiteSpace(ResolvedProfile.telemetry.base_url))
                return ResolvedProfile.telemetry.base_url.TrimEnd('/');
#if MINIGAME_SUBPLATFORM_DOUYIN
            return UseTestTelemetry ? Dy_BaseUrl_Test : Dy_BaseUrl_Release;
#elif WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            return UseTestTelemetry ? Wx_BaseUrl_Test : Wx_BaseUrl_Release;
#else
            return "";
#endif
        }
    }
}
