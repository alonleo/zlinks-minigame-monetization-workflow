#if MINIGAME_SUBPLATFORM_WEIXIN && !WEIXINMINIGAME
#define WEIXINMINIGAME
#endif

using System;
using System.Collections.Generic;
using UnityEngine;
using Service;
#if WEIXINMINIGAME && !UNITY_EDITOR
using WeChatWASM;
#endif

// 微信小游戏平台实现：对接 WX-WASM-SDK-V2（WeChatWASM.WX.* C# API）。
// 编译开关 WEIXINMINIGAME：
//   - 团结引擎 Build Settings 激活微信子平台时由 WX SDK 构建管线自动定义。
//   - 未激活时方法体退化为空实现，保证工程可编译。
// 上架前需补全：AppId、广告位 ID（见各方法内 TODO）。
//
// 与 DouyinPlatform 的差异：
//   - 微信没有侧边栏复访能力，侧边栏相关方法保持基类默认空实现。
//   - 微信 scene 为 Double 类型，需转换为 string。
//   - 微信 LaunchOptionsGame / OnShowListenerResult 没有 path 字段，Path 始终为空。
//   - 微信没有 anonymousCode，Login 只返回 code。
public class WeiXinPlatform : PlatformBase
{
    private const string PlatformName = "weixin";

    // PlatformManager constructs this API once during SDK initialization.
    public WeiXinPlatform()
    {
#if WEIXINMINIGAME && !UNITY_EDITOR
        try
        {
            WX.SetKeepScreenOn(new SetKeepScreenOnOption {
                keepScreenOn = true,
                fail = _ => Debug.LogWarning("[WeiXinPlatform] 设置屏幕常亮失败")
            });
        }
        catch (Exception)
        {
            Debug.LogWarning("[WeiXinPlatform] 设置屏幕常亮异常");
        }
#endif
    }

    /// <summary>调用方未指定标题时使用的通用分享文案</summary>
    private const string DefaultShareTitle = "我不信，你能过这关？";

#if WEIXINMINIGAME && !UNITY_EDITOR
    private MiniGameDisplayAdBridge.Request _bannerTelemetry, _interstitialTelemetry;
    private WXBannerAd _bannerAd;
    private WXInterstitialAd _interstitialAd;
    private bool _interstitialShowIssued;
    // wx.reportEvent 在部分 iOS 微信版本不存在，JS 异常在 WASM 下无法被 C# try-catch 可靠捕获，
    // 首次失败后熔断，避免后续每次调用都抛出并中断调用方流程。
    private static bool _reportEventDisabled;
#endif

    // ==================== 启动参数字段 ====================

#if WEIXINMINIGAME && !UNITY_EDITOR
    /// <summary>onShow 监听是否已注册（避免重复注册）</summary>
    private bool _onShowListenerRegistered;

    /// <summary>当前注册的启动参数回调（热启动 onShow 时触发）</summary>
    private System.Action<LaunchOptionsData> _launchOptionsCallback;

    /// <summary>onShow 监听委托引用（注销时需要传入同一引用）</summary>
    private Action<OnShowListenerResult> _onShowAction;
    private Action<GeneralCallbackResult> _onHideAction;
    private bool _onHideListenerRegistered;

    /// <summary>WX.GetSystemInfoSync().platform 缓存（"android" / "ios" / "devtools" ...），用于震动 API 平台分支</summary>
    private string _systemPlatform;
#endif

    private LaunchOptionsData _lastLaunchOptions;
    public override LaunchOptionsData LastLaunchOptions => _lastLaunchOptions;

    // ==================== 基础平台信息 ====================

    public override string GetPlatformName()
    {
        return PlatformName;
    }

    /// <summary>微信小游戏下，_systemPlatform 由 WX.GetSystemInfoSync().platform 填充（"ios"/"android"/"devtools"）。</summary>
    public override bool IsIOS
    {
        get
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            return _systemPlatform == "ios";
#else
            return false;
#endif
        }
    }

    public override bool IsFeatureSupported(string featureId)
    {
#if WEIXINMINIGAME && !UNITY_EDITOR
        switch (featureId)
        {
            case "share":
            case "rewarded_video":
            case "banner":
            case "interstitial":
            case "vibrate":
            case "login":
                return true;
            default:
                return false;
        }
#else
        return false;
#endif
    }

    public override void GetSystemInfo()
    {
#if WEIXINMINIGAME && !UNITY_EDITOR
        try
        {
            var info = WX.GetSystemInfoSync();
            // wx.getDeviceInfo() 同步获取设备基础信息（基础库 2.25.3+），低版本做兼容处理
            WeChatWASM.DeviceInfo deviceInfo = null;
            try { deviceInfo = WX.GetDeviceInfo(); }
            catch (System.Exception de) { Debug.LogWarning("[WeiXinPlatform] GetDeviceInfo 异常(可能基础库版本过低): " + de.Message); }
            if (deviceInfo != null)
            {
                Debug.Log(string.Format("[WeiXinPlatform] GetDeviceInfo: abi={0} deviceAbi={1} cpuType={2} memorySize={3} brand={4} model={5} platform={6} system={7} benchmarkLevel={8}",
                    deviceInfo.abi, deviceInfo.deviceAbi, deviceInfo.cpuType, deviceInfo.memorySize,
                    deviceInfo.brand, deviceInfo.model, deviceInfo.platform, deviceInfo.system, deviceInfo.benchmarkLevel));
            }

            if (info != null)
            {
                _systemPlatform = info.platform;
                // 设备相关字段优先取 getDeviceInfo()（专用 API），取不到时回退 getSystemInfoSync()
                LastSystemInfo = new SystemInfoData
                {
                    Brand = deviceInfo?.brand ?? info.brand ?? "",
                    Model = deviceInfo?.model ?? info.model ?? "",
                    System = deviceInfo?.system ?? info.system ?? "",
                    Platform = deviceInfo?.platform ?? info.platform ?? "",
                    SDKVersion = info.SDKVersion ?? "",
                    HostVersion = info.version ?? "",
                    HostName = info.host?.appId ?? "",
                    ScreenWidth = info.screenWidth,
                    ScreenHeight = info.screenHeight,
                    PixelRatio = info.pixelRatio,
                    StatusBarHeight = info.statusBarHeight,
                    Language = info.language ?? "",
                    DeviceScore = deviceInfo?.benchmarkLevel ?? info.benchmarkLevel,
                    DeviceOrientation = info.deviceOrientation ?? "",
                    // wx.getDeviceInfo() 独有字段
                    Abi = deviceInfo?.abi ?? "",
                    DeviceAbi = deviceInfo?.deviceAbi ?? "",
                    CpuType = deviceInfo?.cpuType ?? "",
                    MemorySize = deviceInfo?.memorySize ?? "",
                };
                Debug.Log("[WeiXinPlatform] GetSystemInfo: " + LastSystemInfo);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[WeiXinPlatform] GetSystemInfoSync 异常: " + e.Message);
        }
        RegisterLifecycleListeners();
#else
        LastSystemInfo = new SystemInfoData();
#endif
    }

#if WEIXINMINIGAME && !UNITY_EDITOR
    private void RegisterLifecycleListeners()
    {
        if (!_onShowListenerRegistered)
        {
            _onShowAction = OnAppShow;
            WX.OnShow(_onShowAction);
            _onShowListenerRegistered = true;
        }
        if (!_onHideListenerRegistered)
        {
            _onHideAction = OnAppHide;
            WX.OnHide(_onHideAction);
            _onHideListenerRegistered = true;
        }
    }
#endif

    // ==================== 分享 ====================

    public override void Share(string title, string imageUrl, Action<bool> callback)
    {
#if WEIXINMINIGAME && !UNITY_EDITOR
        var option = new ShareAppMessageOption
        {
            title = string.IsNullOrEmpty(title) ? DefaultShareTitle : title,
        };
        // imageUrl：网络图或本地图，留空时使用游戏画面截图
        if (!string.IsNullOrEmpty(imageUrl))
        {
            option.imageUrl = imageUrl;
        }
        option.query = "from=share";

        try
        {
            WX.ShareAppMessage(option);
            Debug.Log("[WeiXinPlatform] Share 已调用");
            // 微信 ShareAppMessage 为异步拉起转发面板，无直接成功/失败回调，
            // 调用成功即视为发起成功，实际是否完成转发由用户操作决定。
            // 分享统计统一由 PlatformManager 的 share_result 分发，避免重复上报腾讯 SHARE。
            callback?.Invoke(true);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[WeiXinPlatform] Share 异常: " + e.Message);
            callback?.Invoke(false);
        }
#else
        callback?.Invoke(false);
#endif
    }

    // ==================== 激励视频广告 ====================

    public override void PrepareRewardedAd(string adId)
    {
        MiniGameRewardedBridge.Prepare(GetPlatformName(), adId);
    }

    public override void ShowAd(string adId, Action<RewardedAdResult> callback, Action onShow = null)
    {
        MiniGameRewardedBridge.Show(GetPlatformName(), adId, callback, onShow);
    }

    // Compatibility entry point: the JS bridge now owns unscaled preload/retry timers.
    public void TickAdTimeout() { }
    private void DestroyRewardedAd() { MiniGameRewardedBridge.DisposeInstance(); }

    // ==================== Banner 广告 ====================

    public override void PrepareBannerAd(string adId)
    {
#if WEIXINMINIGAME && UNITY_WEBGL && !UNITY_EDITOR
        MiniGameDisplayAdBridge.Prepare("banner",adId,GetPlatformName());
#elif WEIXINMINIGAME && !UNITY_EDITOR
        if(string.IsNullOrWhiteSpace(adId)||AppConst.isNoAd)return;
        if(_bannerTelemetry!=null && _bannerTelemetry.Active)return;
        DestroyBannerAd();
        var request=_bannerTelemetry=new MiniGameDisplayAdBridge.Request("banner",adId);
        try {
            _bannerAd=WX.CreateBannerAd(new WXCreateBannerAdParam{adUnitId=adId,style=new Style{left=0,top=0,width=320,height=100}});
            var ad=_bannerAd;if(ad==null){request.Error(-1,"ad_creation_failed");return;}
            ad.OnLoad(()=>request.Loaded());
            ad.OnError((WXADErrorResponse e)=>{request.Error(e!=null?e.errCode:-1,e!=null?e.errMsg:"");if(_bannerAd==ad)DestroyBannerAd();});
        }catch(Exception e){request.Error(-1,e.Message);DestroyBannerAd();}
#endif
    }
    public override void ShowBannerAd(string adId)
    {
#if WEIXINMINIGAME && UNITY_WEBGL && !UNITY_EDITOR
        MiniGameDisplayAdBridge.Show("banner",adId,GetPlatformName());
#elif WEIXINMINIGAME && !UNITY_EDITOR
        var request=_bannerTelemetry;
        if(_bannerAd==null||request==null||!request.BeginShow())return;
        try { _bannerAd.Show(success:_=>request.Show(),failed:e=>{request.Error(-1,e!=null?e.errMsg:"show_failed");if(_bannerTelemetry==request)DestroyBannerAd();}); }catch(Exception e){request.Error(-1,e.Message);DestroyBannerAd();}
#endif
    }
    public override void HideBannerAd()
    {
#if WEIXINMINIGAME && UNITY_WEBGL && !UNITY_EDITOR
        MiniGameDisplayAdBridge.HideBanner();
#elif WEIXINMINIGAME && !UNITY_EDITOR
        _bannerTelemetry?.Hide();_bannerAd?.Hide();
#endif
    }
    public void DestroyBannerAd()
    {
#if WEIXINMINIGAME && UNITY_WEBGL && !UNITY_EDITOR
        MiniGameDisplayAdBridge.Dispose("banner");
#elif WEIXINMINIGAME && !UNITY_EDITOR
        _bannerTelemetry?.Cancel();var ad=_bannerAd;_bannerAd=null;ad?.Destroy();
#endif
    }
    public override void PrepareInterstitialAd(string adId)
    {
#if WEIXINMINIGAME && UNITY_WEBGL && !UNITY_EDITOR
        MiniGameDisplayAdBridge.Prepare("interstitial",adId,GetPlatformName());
#elif WEIXINMINIGAME && !UNITY_EDITOR
        if(string.IsNullOrWhiteSpace(adId)||AppConst.isNoAd)return;
        if(_interstitialTelemetry!=null && _interstitialTelemetry.Active)return;
        DestroyInterstitialAd();
        var request=_interstitialTelemetry=new MiniGameDisplayAdBridge.Request("interstitial",adId);
        try {
            _interstitialAd=WX.CreateInterstitialAd(new WXCreateInterstitialAdParam{adUnitId=adId});
            var ad=_interstitialAd;if(ad==null){request.Error(-1,"ad_creation_failed");return;}
            ad.OnError((WXADErrorResponse e)=>{request.Error(e!=null?e.errCode:-1,e!=null?e.errMsg:"");if(_interstitialAd==ad)DestroyInterstitialAd();});
            ad.OnClose(()=>{if(_interstitialAd!=ad)return;if(_interstitialShowIssued)request.Close(true);DestroyInterstitialAd();});
            ad.Load(success:_=>request.Loaded(),failed:e=>{request.Error(-1,e!=null?e.errMsg:"load_failed");if(_interstitialAd==ad)DestroyInterstitialAd();});
        }catch(Exception e){request.Error(-1,e.Message);DestroyInterstitialAd();}
#endif
    }
    public override void ShowInterstitialAd(string adId)
    {
#if WEIXINMINIGAME && UNITY_WEBGL && !UNITY_EDITOR
        MiniGameDisplayAdBridge.Show("interstitial",adId,GetPlatformName());
#elif WEIXINMINIGAME && !UNITY_EDITOR
        var request=_interstitialTelemetry;
        if(_interstitialAd==null||request==null||!request.BeginShow())return;
        _interstitialShowIssued=true;
        try { _interstitialAd.Show(success:_=>request.Show(),failed:e=>{request.Error(-1,e!=null?e.errMsg:"show_failed");if(_interstitialTelemetry==request)DestroyInterstitialAd();}); }catch(Exception e){request.Error(-1,e.Message);DestroyInterstitialAd();}
#endif
    }
    public void DestroyInterstitialAd()
    {
#if WEIXINMINIGAME && UNITY_WEBGL && !UNITY_EDITOR
        MiniGameDisplayAdBridge.Dispose("interstitial");
#elif WEIXINMINIGAME && !UNITY_EDITOR
        _interstitialTelemetry?.Cancel();var ad=_interstitialAd;_interstitialAd=null;_interstitialShowIssued=false;ad?.Destroy();
#endif
    }
    public override void CancelAdPreload(string kind)
    {
        if(kind=="banner")DestroyBannerAd();else if(kind=="interstitial")DestroyInterstitialAd();else base.CancelAdPreload(kind);
    }

    // ==================== 启动参数（冷启动 + 热启动） ====================

    public override void getEnterOptions(Action<LaunchOptionsData> callback)
    {
#if WEIXINMINIGAME && !UNITY_EDITOR
        // 保存回调，供后续 onShow（热启动）使用
        _launchOptionsCallback = callback;

        RegisterLifecycleListeners();

        // 冷启动：同步获取启动参数并立即回调
        LaunchOptionsData coldOptions = null;
        try
        {
            var launchOption = WX.GetLaunchOptionsSync();
            if (launchOption != null)
            {
                coldOptions = ConvertFromLaunchOption(launchOption, isColdStart: true);
            }
        }
        catch (System.Exception e)
        {
            Debug.Log("[MiniGameKit] 平台回调完成（身份与启动参数已脱敏）");
        }

        if (coldOptions == null)
        {
            coldOptions = new LaunchOptionsData { IsColdStart = true };
        }

        _lastLaunchOptions = coldOptions;
        Debug.Log("[MiniGameKit] 平台回调完成（身份与启动参数已脱敏）");
        callback?.Invoke(coldOptions);
#else
        callback?.Invoke(new LaunchOptionsData());
#endif
    }

#if WEIXINMINIGAME && !UNITY_EDITOR
    /// <summary>
    /// onShow 回调：热启动参数更新。微信没有侧边栏，仅处理启动参数。
    /// </summary>
    private void OnAppShow(OnShowListenerResult result)
    {
        PlatformManager.Instance?.SetApplicationForeground(true);
        // IAA: SDK 生命周期 — 应用回到前台
        // DataNexus auto_track owns foreground events.
        if (result == null) return;

        // 热启动参数只更新本地状态，不触发激活上报。
        var options = ConvertFromOnShowResult(result, isColdStart: false);
        _lastLaunchOptions = options;
        Debug.Log("[MiniGameKit] 平台回调完成（身份与启动参数已脱敏）");
        _launchOptionsCallback?.Invoke(options);
    }

    private void OnAppHide(GeneralCallbackResult result)
    {
        PlatformManager.Instance?.SetApplicationForeground(false);
        // IAA: SDK 生命周期 — 应用进入后台
        // DataNexus auto_track owns background events.
    }

    /// <summary>将 LaunchOptionsGame 转为统一的 LaunchOptionsData</summary>
    private static LaunchOptionsData ConvertFromLaunchOption(LaunchOptionsGame launchOption, bool isColdStart)
    {
        // Query 由 SDK 桥接层拥有，回调返回后可能被回收/清空，必须立即深拷贝。
        var queryCopy = new Dictionary<string, string>();
        if (launchOption.query != null)
        {
            foreach (var kv in launchOption.query)
            {
                if (!string.IsNullOrEmpty(kv.Key))
                    queryCopy[kv.Key] = kv.Value ?? "";
            }
        }

        var data = new LaunchOptionsData
        {
            IsColdStart = isColdStart,
            // 微信 scene 为 Double，转为 string
            Scene = launchOption.scene.ToString(),
            // 微信 LaunchOptionsGame 没有 path 字段
            Path = "",
            Query = queryCopy,
            RawJson = LaunchOptionsData.SerializeRaw(launchOption),
        };

        return data;
    }

    /// <summary>将 OnShowListenerResult 转为统一的 LaunchOptionsData</summary>
    private static LaunchOptionsData ConvertFromOnShowResult(OnShowListenerResult result, bool isColdStart)
    {
        var options = new LaunchOptionsData
        {
            IsColdStart = isColdStart,
            RawJson = LaunchOptionsData.SerializeRaw(result),
        };

        options.Scene = result.scene.ToString();
        // 微信 OnShowListenerResult 没有 path 字段
        options.Path = "";

        // query 字段：逐项拷贝为 Dictionary<string,string>
        if (result.query != null)
        {
            var query = new Dictionary<string, string>();
            foreach (var kv in result.query)
            {
                if (!string.IsNullOrEmpty(kv.Key))
                    query[kv.Key] = kv.Value ?? "";
            }
            options.Query = query;
        }

        return options;
    }
#endif

    // ==================== 其他平台能力 ====================

    public override void goSideBar()
    {
        // 微信没有侧边栏概念，空实现
        Debug.Log("[WeiXinPlatform] goSideBar: 微信不支持侧边栏，跳过");
    }

    public override void vibrate(int vibrateType = 1)
    {
#if WEIXINMINIGAME && !UNITY_EDITOR
        // vibrateType: 1=短震（15ms），2=长震（400ms）
        // 注意：wx.vibrateShort 在 Android 及 iPhone 7/7 Plus 上官方文档明确标注"可能无效"，
        // 实测在多数安卓机型上完全无反馈。因此在 Android 上短震也改用 VibrateLong（400ms），
        // 保证震动反馈可感知；iOS 上 vibrateShort（type=heavy）正常生效，保留短震体验。
        bool useLong = vibrateType >= 2 || _systemPlatform == "android";
        if (useLong)
        {
            var option = new VibrateLongOption
            {
                fail = err => Debug.LogWarning("[WeiXinPlatform] VibrateLong fail: " + (err != null ? err.errMsg : "")),
            };
            WX.VibrateLong(option);
        }
        else
        {
            var option = new VibrateShortOption
            {
                type = "heavy",
                fail = err => Debug.LogWarning("[WeiXinPlatform] VibrateShort fail: " + (err != null ? err.errMsg : "")),
            };
            WX.VibrateShort(option);
        }
#endif
    }

    public override void setUserRecord(int score)
    {
        // 微信开放数据域排行榜需通过 WX.GetRankManager 上报，但 getRankManager 仅在开放数据域可用，
        // 主游戏上下文调用会抛 wx.getRankManager is not a function，且 WASM 下 JS 异常无法被 C#
        // try-catch 可靠捕获，会导致调用方（如 WinLevel）中断、后续结算弹窗不弹出。
        // 上报排行榜需在开放数据域 via sharedCanvas 实现，此处暂留空，待开放数据域接入后补全。
#if WEIXINMINIGAME && !UNITY_EDITOR
        Debug.Log("[WeiXinPlatform] setUserRecord score=" + score + " (TODO: 开放数据域排行榜未接入)");
#endif
    }

    public override void AddShortcut(Action<bool> callback)
    {
#if WEIXINMINIGAME && !UNITY_EDITOR
        // 微信没有直接的 AddShortcut API，可通过 CheckIsAddedToMyMiniProgram 引导用户添加
        Debug.Log("[WeiXinPlatform] AddShortcut: 微信暂不支持主动添加桌面快捷方式");
        callback?.Invoke(false);
#else
        callback?.Invoke(false);
#endif
    }

    public override void ShowToast(string message, int durationMs = 1500)
    {
#if WEIXINMINIGAME && !UNITY_EDITOR
        try
        {
            var option = new ShowToastOption
            {
                title = message,
                icon = "none",
                duration = durationMs,
            };
            WX.ShowToast(option);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[WeiXinPlatform] ShowToast 异常: " + e.Message);
        }
#else

#endif
    }

    // ==================== 登录 ====================

    public override void Login(Action<string, string> callback)
    {
#if WEIXINMINIGAME && !UNITY_EDITOR
        var option = new LoginOption
        {
            success = res =>
            {
                Debug.Log("[MiniGameKit] 平台回调完成（身份与启动参数已脱敏）");
                // 微信没有 anonymousCode，第二个参数返回空
                callback?.Invoke(res != null ? (res.code ?? "") : "", "");
            },
            fail = err =>
            {
                Debug.Log("[MiniGameKit] 平台回调完成（身份与启动参数已脱敏）");
                callback?.Invoke("", "");
            },
        };
        WX.Login(option);
#else
        callback?.Invoke("", "");
#endif
    }

    // ==================== 事件上报（平台原生 wx.reportEvent） ====================

    public override void ReportEvent(string eventName, Dictionary<string, object> properties)
    {
#if WEIXINMINIGAME && !UNITY_EDITOR
        if (string.IsNullOrEmpty(eventName))
        {
            Debug.LogWarning("[WeiXinPlatform] ReportEvent: eventName 为空，跳过");
            return;
        }

        if (_reportEventDisabled)
            return;

        try
        {
            // WX.ReportEvent 要求 Dictionary<string, string>，需将 object 值转为 string
            var param = new Dictionary<string, string>();
            if (properties != null)
            {
                foreach (var kv in properties)
                {
                    if (!string.IsNullOrEmpty(kv.Key) && kv.Value != null)
                    {
                        param[kv.Key] = kv.Value is System.Collections.IDictionary || kv.Value is System.Collections.IList ? Newtonsoft.Json.JsonConvert.SerializeObject(kv.Value) : Convert.ToString(kv.Value, System.Globalization.CultureInfo.InvariantCulture);
                    }
                }
            }
            WX.ReportEvent(eventName, param);
            Debug.Log(string.Format("[WeiXinPlatform] ReportEvent: {0} fields={1}", eventName, param.Count));
        }
        catch (System.Exception e)
        {
            // WASM 下 wx.reportEvent 不存在时 JS 抛 TypeError，部分场景无法被 C# try-catch 捕获，
            // 一旦捕获到异常立即熔断，避免后续调用重复抛出并中断业务流程（如 WinLevel 结算弹窗）。
            _reportEventDisabled = true;
            Debug.LogWarning("[WeiXinPlatform] ReportEvent 异常（已熔断后续调用）: " + e.Message);
        }
#else
        Debug.Log(string.Format("[WeiXinPlatform] ReportEvent (非微信平台) {0} 跳过", eventName));
#endif
    }

    // ==================== 销毁 ====================

    public override void Dispose()
    {
#if WEIXINMINIGAME && !UNITY_EDITOR
        if (_onShowListenerRegistered && _onShowAction != null)
        {
            WX.OffShow(_onShowAction);
            _onShowListenerRegistered = false;
            _onShowAction = null;
        }
        if (_onHideListenerRegistered && _onHideAction != null)
        {
            WX.OffHide(_onHideAction);
            _onHideListenerRegistered = false;
            _onHideAction = null;
        }
        DestroyRewardedAd();
        DestroyBannerAd();
#endif
    }
}
