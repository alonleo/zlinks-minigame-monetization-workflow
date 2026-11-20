using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
#if MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
using TTSDK;
using TTSDK.UNBridgeLib.LitJson;
#endif

// 抖音小游戏平台实现：对接 StarkSDK 6.9.0（TTSDK.TT.* C# API）。
// 编译开关 MINIGAME_SUBPLATFORM_DOUYIN：
//   - 团结引擎 Build Settings 激活抖音子平台时自动定义。
//   - 未激活时方法体退化为 DefaultPlatform 同等空实现，保证工程可编译。
// 上架前需补全：AppId、广告位 ID、排行榜 KVKey（见各方法内 TODO）。分享素材在开放平台
// 「运营-必接能力-分享配置」配置，代码内不传 imageUrl，由平台使用小游戏 icon。
//
// 本文件整合了侧边栏复访能力（原 SidebarManager，必接能力，不接入有拒审风险）。
// 启动参数数据结构 LaunchOptionsData / 侧边栏枚举 SidebarState 定义在 PlatformBase，
// 所有调用统一通过 PlatformManager 访问。
public class DouyinPlatform : PlatformBase
{
    private const string PlatformName = "douyin";

    // PlatformManager constructs this API once during SDK initialization.
    public DouyinPlatform()
    {
#if MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        try
        {
            TT.SetKeepScreenOn(true, null,
                _ => Debug.LogWarning("[DouyinPlatform] 设置屏幕常亮失败"));
        }
        catch (Exception)
        {
            Debug.LogWarning("[DouyinPlatform] 设置屏幕常亮异常");
        }
#endif
    }

    /// <summary>调用方未指定标题时使用的通用分享文案</summary>
    private const string DefaultShareTitle = "我不信，你能过这关？";

#if MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
    private MiniGameDisplayAdBridge.Request _bannerTelemetry, _interstitialTelemetry;
    private TTBannerAd _bannerAd;
    private TTInterstitialAd _interstitialAd;
    private bool _interstitialAdShowing;
    private bool _interstitialShowIssued;
#endif

    private static readonly System.Collections.Generic.HashSet<string> _loadedSubpackages = new System.Collections.Generic.HashSet<string>();

    // ==================== 侧边栏字段（原 SidebarManager） ====================

    private SidebarState _sidebarState = SidebarState.GoToAdd;
    private bool _isSidebarSupported;
    private bool _isFromSidebar;
    private const string SidebarClaimedKey = "sidebar_reward_claimed";

    public override SidebarState CurrentSidebarState => _sidebarState;
    public override bool IsSidebarSupported => _isSidebarSupported;
    public override bool IsFromSidebar => _isFromSidebar;

    // ==================== 启动参数字段 ====================

#if MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
    private TTAppLifeCycle _appLifeCycle;
    /// <summary>onShow 监听是否已注册（避免重复注册）</summary>
    private bool _onShowListenerRegistered;
    private bool _onHideListenerRegistered;

    /// <summary>当前注册的启动参数回调（热启动 onShow 时触发）</summary>
    private System.Action<LaunchOptionsData> _launchOptionsCallback;
#endif

    private LaunchOptionsData _lastLaunchOptions;
    public override LaunchOptionsData LastLaunchOptions => _lastLaunchOptions;

    // ==================== 分包（空实现） ====================

    /// <summary>
    /// 抖音 Unity 小游戏不需要手动加载分包。
    /// WASM 代码分包由抖音开发者工具的 Wasm 分包工具自动处理。
    /// 首包资源（webgl.data）通过 Build Profile 的 dataLoadType 配置为 Package 或 CDN。
    /// 此方法保留为空实现，兼容 ResourceManager/SoundManager 的调用。
    /// </summary>
    public static Task<bool> LoadSubpackageAsync(string name)
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// 抖音小游戏不分包，返回 null 表示所有资源都在主包内。
    /// </summary>
    public static string GetSubpackageByPath(string path)
    {
        return null;
    }

    // ==================== 基础平台信息 ====================

    public override string GetPlatformName()
    {
        return PlatformName;
    }

    public override bool IsFeatureSupported(string featureId)
    {
#if MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        switch (featureId)
        {
            case "share":
            case "rewarded_video":
            case "banner":
            case "interstitial":
            case "vibrate":
            case "sidebar":
            case "rank":
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
#if MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        try
        {

            // 抖音小游戏没有 getDeviceInfo API
            var info = TT.GetSystemInfo();
            if (info != null)
            {
                LastSystemInfo = new SystemInfoData
                {
                    Brand = info.brand ?? "",
                    Model = info.model ?? "",
                    System = info.system ?? "",
                    Platform = info.platform ?? "",
                    SDKVersion = info.sdkVersion ?? "",
                    HostVersion = info.hostVersion ?? "",
                    HostName = info.hostName ?? "",
                    ScreenWidth = info.screenWidth,
                    ScreenHeight = info.screenHeight,
                    PixelRatio = info.pixelRatio,
                    StatusBarHeight = info.statusBarHeight,
                    Language = info.language ?? "",
                    DeviceScore = info.deviceScore.overall,
                    DeviceOrientation = info.deviceOrientation.ToString(),
                };
                Debug.Log("[DouyinPlatform] GetSystemInfo: " + LastSystemInfo);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[DouyinPlatform] GetSystemInfo 异常: " + e.Message);
        }
#else
        LastSystemInfo = new SystemInfoData();
#endif
        // 侧边栏初始化（原 SidebarManager.Init）
        InitSidebar();
    }

    // ==================== 分享 ====================

    public override void Share(string title, string imageUrl, Action<bool> callback)
    {
#if MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        var shareJson = new JsonData();
        shareJson["title"] = string.IsNullOrEmpty(title) ? DefaultShareTitle : title;
        // imageUrl 只接受 ttfile:// 沙盒路径或代码包根目录相对路径，不支持网络图，也不支持
        // 留空时平台自动使用小游戏 icon 作为分享图，因此仅在显式传入时才设置该字段。
        if (!string.IsNullOrEmpty(imageUrl))
        {
            shareJson["imageUrl"] = imageUrl;
        }
        shareJson["channel"] = "invite";
        shareJson["query"] = "from=share";

        TT.ShareAppMessage(
            shareJson,
            successCallback: (System.Collections.Generic.Dictionary<string, object> result) =>
            {
                Debug.Log("[DouyinPlatform] Share success");
                callback?.Invoke(true);
            },
            failedCallback: (string errMsg) =>
            {
                Debug.LogWarning("[DouyinPlatform] Share fail: " + errMsg);
                callback?.Invoke(false);
            },
            cancelledCallback: () =>
            {
                Debug.Log("[DouyinPlatform] Share cancelled");
                callback?.Invoke(false);
            });
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
#if MINIGAME_SUBPLATFORM_DOUYIN && UNITY_WEBGL && !UNITY_EDITOR
        MiniGameDisplayAdBridge.Prepare("banner",adId,GetPlatformName());
#elif MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        if(string.IsNullOrWhiteSpace(adId)||AppConst.isNoAd)return;
        if(_bannerTelemetry!=null && _bannerTelemetry.Active)return;
        DestroyBannerAd();
        var request=_bannerTelemetry=new MiniGameDisplayAdBridge.Request("banner",adId);
        try {
            _bannerAd=TT.CreateBannerAd(new CreateBannerAdParam{BannerAdId=adId,Style=new TTBannerStyle{left=0,top=0,width=320}});
            var ad=_bannerAd;if(ad==null){request.Error(-1,"ad_creation_failed");return;}
            ad.OnLoad+=()=>request.Loaded();
            ad.OnError+=(code,msg)=>{request.Error(code,msg);if(_bannerAd==ad)DestroyBannerAd();};
            ad.OnClose+=()=>{if(_bannerAd!=ad)return;request.Close(true);DestroyBannerAd();};
        }catch(Exception e){request.Error(-1,e.Message);DestroyBannerAd();}
#endif
    }
    public override void ShowBannerAd(string adId)
    {
#if MINIGAME_SUBPLATFORM_DOUYIN && UNITY_WEBGL && !UNITY_EDITOR
        MiniGameDisplayAdBridge.Show("banner",adId,GetPlatformName());
#elif MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        var request=_bannerTelemetry;
        if(_bannerAd==null||request==null||!request.BeginShow())return;
        try { _bannerAd.Show(); }catch(Exception e){request.Error(-1,e.Message);DestroyBannerAd();}
#endif
    }
    public override void HideBannerAd()
    {
#if MINIGAME_SUBPLATFORM_DOUYIN && UNITY_WEBGL && !UNITY_EDITOR
        MiniGameDisplayAdBridge.HideBanner();
#elif MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        _bannerTelemetry?.Hide();_bannerAd?.Hide();
#endif
    }
    public void DestroyBannerAd()
    {
#if MINIGAME_SUBPLATFORM_DOUYIN && UNITY_WEBGL && !UNITY_EDITOR
        MiniGameDisplayAdBridge.Dispose("banner");
#elif MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        _bannerTelemetry?.Cancel();var ad=_bannerAd;_bannerAd=null;ad?.Destroy();
#endif
    }
    public override void PrepareInterstitialAd(string adId)
    {
#if MINIGAME_SUBPLATFORM_DOUYIN && UNITY_WEBGL && !UNITY_EDITOR
        MiniGameDisplayAdBridge.Prepare("interstitial",adId,GetPlatformName());
#elif MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        if(string.IsNullOrWhiteSpace(adId)||AppConst.isNoAd)return;
        if(_interstitialTelemetry!=null && _interstitialTelemetry.Active)return;
        DestroyInterstitialAd();
        var request=_interstitialTelemetry=new MiniGameDisplayAdBridge.Request("interstitial",adId);
        try {
            _interstitialAd=TT.CreateInterstitialAd(new CreateInterstitialAdParam{InterstitialAdId=adId});
            var ad=_interstitialAd;if(ad==null){request.Error(-1,"ad_creation_failed");return;}
            ad.OnError+=(code,msg)=>{request.Error(code,msg);if(_interstitialAd==ad)DestroyInterstitialAd();};
            ad.OnClose+=()=>{if(_interstitialAd!=ad)return;if(_interstitialShowIssued)request.Close(true);DestroyInterstitialAd();};
            ad.OnLoad+=()=>request.Loaded();ad.Load();
        }catch(Exception e){request.Error(-1,e.Message);DestroyInterstitialAd();}
#endif
    }
    public override void ShowInterstitialAd(string adId)
    {
#if MINIGAME_SUBPLATFORM_DOUYIN && UNITY_WEBGL && !UNITY_EDITOR
        MiniGameDisplayAdBridge.Show("interstitial",adId,GetPlatformName());
#elif MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        var request=_interstitialTelemetry;
        if(_interstitialAd==null||request==null||!request.BeginShow())return;
        _interstitialShowIssued=true;
        try { _interstitialAd.Show(); }catch(Exception e){request.Error(-1,e.Message);DestroyInterstitialAd();}
#endif
    }
    public void DestroyInterstitialAd()
    {
#if MINIGAME_SUBPLATFORM_DOUYIN && UNITY_WEBGL && !UNITY_EDITOR
        MiniGameDisplayAdBridge.Dispose("interstitial");
#elif MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        _interstitialTelemetry?.Cancel();var ad=_interstitialAd;_interstitialAd=null;_interstitialShowIssued=false;ad?.Destroy();
#endif
    }
    public override void CancelAdPreload(string kind)
    {
        if(kind=="banner")DestroyBannerAd();else if(kind=="interstitial")DestroyInterstitialAd();else base.CancelAdPreload(kind);
    }

    // ==================== 侧边栏复访（原 SidebarManager） ====================

    /// <summary>
    /// 初始化侧边栏能力：恢复已领取状态 + 监听 onShow/onHide + 检查宿主支持。
    /// 由 GetSystemInfo() 自动调用，无需外部手动调。
    /// </summary>
    private void InitSidebar()
    {
        // 恢复已领取状态
        if (MiniGamePrefs.GetInt(SidebarClaimedKey, 0) == 1)
        {
            _sidebarState = SidebarState.Claimed;
        }

#if MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        if (_appLifeCycle == null) _appLifeCycle = TT.GetAppLifeCycle();
        // 1. 监听 onShow（统一处理侧边栏状态 + 启动参数热启动）
        if (!_onShowListenerRegistered)
        {
            _appLifeCycle.OnShow += OnAppShow;
            _onShowListenerRegistered = true;
            Debug.Log("[DouyinPlatform] InitSidebar: 已注册 onShow 监听");
        }
        if (!_onHideListenerRegistered)
        {
            _appLifeCycle.OnHide += OnAppHide;
            _onHideListenerRegistered = true;
            Debug.Log("[DouyinPlatform] InitSidebar: 已注册 onHide 监听");
        }

        // 2. 检查宿主是否支持侧边栏
        TT.CheckScene(TTSideBar.SceneEnum.SideBar, supported =>
        {
            _isSidebarSupported = supported;
            Debug.Log("[DouyinPlatform] 宿主支持侧边栏: " + supported);
            if (supported && _sidebarState != SidebarState.Claimed)
            {
                RefreshSidebarButtonState();
            }
        }, null, null);
#else
        // 非抖音平台：不支持侧边栏
        _isSidebarSupported = false;
#endif
    }

    public override void RefreshSidebarButtonState()
    {
        RaiseSidebarStateChanged(_sidebarState);
    }

    public override void goSideBar()
    {
        Debug.Log("[DouyinPlatform] 跳转侧边栏");
#if MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        var jsonData = new JsonData();
        jsonData["scene"] = "sidebar";
        TT.NavigateToScene(jsonData,
            () => Debug.Log("[DouyinPlatform] NavigateToScene success"),
            () => Debug.LogWarning("[DouyinPlatform] NavigateToScene fail"),
            (code, msg) => Debug.LogWarning(string.Format("[DouyinPlatform] NavigateToScene error code={0} msg={1}", code, msg)));
#else
        Debug.Log("[DouyinPlatform] (非抖音平台) 跳转侧边栏跳过");
#endif
    }

    public override void ClaimSidebarReward()
    {
        Debug.Log("[DouyinPlatform] 领取侧边栏奖励");
        _sidebarState = SidebarState.Claimed;
        MiniGamePrefs.SetInt(SidebarClaimedKey, 1);
        MiniGamePrefs.Save();
        RaiseSidebarStateChanged(_sidebarState);

        // TODO: 在这里发放实际奖励（金币、道具等）
    }

    public override void ResetSidebarState()
    {
        MiniGamePrefs.DeleteKey(SidebarClaimedKey);
        MiniGamePrefs.Save();
        _sidebarState = SidebarState.GoToAdd;
        _isFromSidebar = false;
        RaiseSidebarStateChanged(_sidebarState);
        Debug.Log("[DouyinPlatform] 侧边栏状态已重置");
    }

    public override void DisposeSidebar()
    {
#if MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        if (_onShowListenerRegistered)
        {
            _appLifeCycle.OnShow -= OnAppShow;
            _onShowListenerRegistered = false;
        }
        if (_onHideListenerRegistered)
        {
            _appLifeCycle.OnHide -= OnAppHide;
            _onHideListenerRegistered = false;
        }
        _appLifeCycle = null;
#endif
    }

    public override void Dispose()
    {
        DisposeSidebar();
#if MINIGAME_SUBPLATFORM_DOUYIN && UNITY_WEBGL && !UNITY_EDITOR
        MiniGameDisplayAdBridge.Dispose("banner");
        MiniGameDisplayAdBridge.Dispose("interstitial");
#endif
    }

    // ==================== 启动参数（冷启动 + 热启动） ====================

    public override void getEnterOptions(Action<LaunchOptionsData> callback)
    {
#if MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        if (!TT.InContainerEnv)
        {
            Debug.Log("[MiniGameKit] 平台回调完成（身份与启动参数已脱敏）");
            callback?.Invoke(new LaunchOptionsData());
            return;
        }

        // 保存回调，供后续 onShow（热启动）使用
        _launchOptionsCallback = callback;

        // onShow 监听已在 InitSidebar() 中注册，此处无需重复注册

        // 冷启动：同步获取启动参数并立即回调
        LaunchOptionsData coldOptions = null;
        try
        {
            var launchOption = TT.GetLaunchOptionsSync();
            if (launchOption != null)
            {
                coldOptions = ConvertFromLaunchOption(launchOption, isColdStart: true);
            }
        }
        catch (System.Exception)
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

#if MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
    /// <summary>
    /// onShow 统一回调：同时处理侧边栏状态判断 + 启动参数热启动更新。
    /// launch_from=homepage, location=sidebar_card 表示从侧边栏进入。
    /// </summary>
    private void OnAppShow(Dictionary<string, object> data)
    {
        Debug.Log("[DouyinPlatform] 生命周期 OnShow：回到前台");
        PlatformManager.Instance?.SetApplicationForeground(true);
        if (data == null) return;

        // 1. 启动参数只更新本地状态，不触发激活上报
        var options = ConvertFromOnShowData(data, isColdStart: false);
        _lastLaunchOptions = options;
        Debug.Log("[MiniGameKit] 平台回调完成（身份与启动参数已脱敏）");
        _launchOptionsCallback?.Invoke(options);

        // 2. 侧边栏状态判断
        string launchFrom = options.LaunchFrom;
        string location = options.Location;

        Debug.Log(string.Format("[DouyinPlatform] OnAppShow launch_from={0} location={1}", launchFrom, location));

        bool fromSidebar = (launchFrom == "homepage" && location == "sidebar_card");

        if (fromSidebar)
        {
            _isFromSidebar = true;
            Debug.Log("[DouyinPlatform] 用户从侧边栏启动游戏");

            // 如果还没领取过，切换为可领取状态
            if (_sidebarState == SidebarState.GoToAdd)
            {
                _sidebarState = SidebarState.CanClaim;
                RaiseSidebarStateChanged(_sidebarState);
            }

            RaiseSidebarEnter();
        }
    }

    private void OnAppHide()
    {
        Debug.Log("[DouyinPlatform] 生命周期 OnHide：进入后台");
        PlatformManager.Instance?.SetApplicationForeground(false);
        MiniGamePrefs.Save();
    }

    /// <summary>将 TTSDK.LaunchOption 转为统一的 LaunchOptionsData</summary>
    private static LaunchOptionsData ConvertFromLaunchOption(LaunchOption launchOption, bool isColdStart)
    {
        // Query / Extra 由 StarkSDK 桥接层拥有，回调返回后可能被回收/清空，
        // 必须在这里立即深拷贝，不能持有 SDK 的原引用。
        var queryCopy = new System.Collections.Generic.Dictionary<string, string>();
        if (launchOption.Query != null)
        {
            foreach (var kv in launchOption.Query)
            {
                if (!string.IsNullOrEmpty(kv.Key))
                    queryCopy[kv.Key] = kv.Value ?? "";
            }
        }

        var data = new LaunchOptionsData
        {
            IsColdStart = isColdStart,
            Scene = launchOption.Scene,
            Path = launchOption.Path,
            Query = queryCopy,
            RawJson = LaunchOptionsData.SerializeRaw(launchOption),
        };

        // Extra 中可能包含 launch_from 等补充信息（同样需要立即取出，避免持有 SDK 引用）
        if (launchOption.Extra != null)
        {
            string launchFrom;
            if (launchOption.Extra.TryGetValue("launch_from", out launchFrom))
            {
                data.LaunchFrom = launchFrom ?? "";
            }
            string location;
            if (launchOption.Extra.TryGetValue("location", out location))
            {
                data.Location = location ?? "";
            }
        }

        return data;
    }

    /// <summary>将 onShow 回调的 JsonData 转为统一的 LaunchOptionsData</summary>
    private static LaunchOptionsData ConvertFromOnShowData(Dictionary<string, object> data, bool isColdStart)
    {
        var options = new LaunchOptionsData { IsColdStart = isColdStart, RawJson = LaunchOptionsData.SerializeRaw(data) };

        if (data.ContainsKey("scene"))
        {
            options.Scene = data["scene"] != null ? data["scene"].ToString() : "";
        }
        if (data.ContainsKey("path"))
        {
            options.Path = data["path"] != null ? data["path"].ToString() : "";
        }
        if (data.ContainsKey("launch_from"))
        {
            options.LaunchFrom = data["launch_from"] != null ? data["launch_from"].ToString() : "";
        }
        if (data.ContainsKey("location"))
        {
            options.Location = data["location"] != null ? data["location"].ToString() : "";
        }

        // query 字段：onShow 中 query 为对象，逐项解析为 Dictionary<string,string>
        if (data.TryGetValue("query", out var queryValue) && queryValue is System.Collections.IDictionary sourceQuery)
        {
            var query = new System.Collections.Generic.Dictionary<string, string>();
            foreach (System.Collections.DictionaryEntry entry in sourceQuery)
            {
                if (entry.Key is string key)
                    query[key] = entry.Value != null ? entry.Value.ToString() : "";
            }
            options.Query = query;
        }

        return options;
    }
#endif

    // ==================== 其他平台能力 ====================

    public override void vibrate(int vibrateType = 1)
    {
#if MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        // vibrateType: 1=短震（Android 30ms / iOS 15ms），2=长震（400ms）
        // 官方推荐短震用 TT.VibrateShort、长震用 TT.VibrateLong（替代旧 TT.Vibrate 兼容接口）。
        // 注意：抖音 SDK 不支持强度参数（微信的 heavy/medium/light），仅有长/短两档。
        // iOS 静音模式下长震会被系统禁用，属正常现象。
        if (vibrateType >= 2)
        {
            TT.VibrateLong(new VibrateLongParam
            {
                Success = res => { },
                Fail = err => Debug.LogWarning("[DouyinPlatform] VibrateLong fail: " + (err != null ? err.ErrMsg : "")),
            });
        }
        else
        {
            TT.VibrateShort(new VibrateShortParam
            {
                Success = res => { },
                Fail = err => Debug.LogWarning("[DouyinPlatform] VibrateShort fail: " + (err != null ? err.ErrMsg : "")),
            });
        }
#endif
    }

    public override void setUserRecord(int score)
    {
#if MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        // 抖音游戏排行榜：通过 TT.SetImRankData 上报已过关卡数到 IM 排行榜。
        // score = 已过关卡数（从 1 开始计）。dataType=0 表示数值类型，
        // value 为分数字符串，zoneId=default 为默认线上分区。
        // 上架前需在抖音开发者后台创建排行榜并确认分区配置。
        var rankData = new JsonData();
        rankData["dataType"] = 0;
        rankData["value"] = score.ToString();
        rankData["zoneId"] = "default";
        TT.SetImRankData(rankData, (success, msg) =>
        {
            Debug.Log("[DouyinPlatform] SetImRankData(已过关卡数=" + score + ") result=" + success + " msg=" + msg);
        });
#endif
    }

    public override bool ShowRankList()
    {
#if MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        // 拉起抖音原生好友排行榜 UI（平台自动绘制，无需开发者画）。
        // relationType=default 同时展示好友榜和总榜；rankType=all 为总榜（半年数据）。
        // 前提：用户已登录（TrackManager.InitAscribe 已在启动时完成 TT.Login）。
        var param = new JsonData();
        param["rankType"] = "all";
        param["dataType"] = 0;
        param["relationType"] = "default";
        param["suffix"] = "关";
        param["rankTitle"] = "好友排行榜";
        param["zoneId"] = "default";
        TT.GetImRankList(param, (success, errMsg) =>
        {
            if (success)
            {
                Debug.Log("[DouyinPlatform] GetImRankList 拉起成功");
            }
            else
            {
                Debug.LogWarning("[DouyinPlatform] GetImRankList 失败: " + errMsg);
            }
        });
        return true;
#else
        return false;
#endif
    }

    public override void AddShortcut(Action<bool> callback)
    {
#if MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        TT.AddShortcut(success =>
        {
            Debug.Log("[DouyinPlatform] AddShortcut 结果: " + success);
            callback?.Invoke(success);
        }, true);
#else
        callback?.Invoke(false);
#endif
    }

    public override void ShowToast(string message, int durationMs = 1500)
    {
#if MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        if (TT.InContainerEnv)
        {
            var param = new TTShowToastParam
            {
                title = message,
                icon = "none",
                duration = durationMs,
            };
            TT.ShowToast(param);
        }
        else
        {

        }
#else
#endif
    }

    // ==================== 登录 ====================

    public override void Login(Action<string, string> callback)
    {
#if MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        if (!TT.InContainerEnv)
        {
            Debug.Log("[MiniGameKit] 平台回调完成（身份与启动参数已脱敏）");
            callback?.Invoke("", "");
            return;
        }

        TT.Login(
            (code, anonymousCode, isLogin) =>
            {
                Debug.Log("[MiniGameKit] 平台回调完成（身份与启动参数已脱敏）");
                callback?.Invoke(code ?? "", anonymousCode ?? "");
            },
            (errMsg) =>
            {
                Debug.Log("[MiniGameKit] 平台回调完成（身份与启动参数已脱敏）");
                callback?.Invoke("", "");
            },
            false); // forceLogin=false：不强制弹登录窗，未登录则只拿 anonymousCode
#else
        callback?.Invoke("", "");
#endif
    }

    // ==================== 事件上报（平台原生 tt.reportAnalytics） ====================

    public override void ReportEvent(string eventName, Dictionary<string, object> properties)
    {
#if MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        if (!TT.InContainerEnv)
        {
            Debug.Log("[DouyinPlatform] ReportEvent: 不在抖音容器内，跳过");
            return;
        }

        if (string.IsNullOrEmpty(eventName))
        {
            Debug.LogWarning("[DouyinPlatform] ReportEvent: eventName 为空，跳过");
            return;
        }

        try
        {
            var param = NormalizeReportAnalyticsProperties(properties);
            TT.ReportAnalytics<object>(eventName, param);
            Debug.Log(string.Format("[DouyinPlatform] ReportEvent: {0} fields={1}", eventName, param.Count));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[DouyinPlatform] ReportEvent 异常: " + e.Message);
        }
#else
        Debug.Log(string.Format("[DouyinPlatform] ReportEvent (非抖音平台) {0} 跳过", eventName));
#endif
    }

    /// <summary>tt.reportAnalytics 只接受 number/string/boolean；复杂值在抖音适配层转 JSON 字符串。</summary>
    internal static Dictionary<string, object> NormalizeReportAnalyticsProperties(Dictionary<string, object> properties)
    {
        var result = new Dictionary<string, object>();
        if (properties == null) return result;
        foreach (var kv in properties)
        {
            if (string.IsNullOrEmpty(kv.Key)) continue;
            if (kv.Value == null)
            {
                result[kv.Key] = "null";
                continue;
            }
            var value = kv.Value;
            if (value is string || value is bool || value is byte || value is sbyte
                || value is short || value is ushort || value is int || value is uint
                || value is long || value is ulong || value is decimal)
            {
                result[kv.Key] = value;
            }
            else if (value is float)
            {
                var number = (float)value;
                result[kv.Key] = float.IsNaN(number) || float.IsInfinity(number)
                    ? number.ToString(System.Globalization.CultureInfo.InvariantCulture) : value;
            }
            else if (value is double)
            {
                var number = (double)value;
                result[kv.Key] = double.IsNaN(number) || double.IsInfinity(number)
                    ? number.ToString(System.Globalization.CultureInfo.InvariantCulture) : value;
            }
            else
            {
                result[kv.Key] = Newtonsoft.Json.JsonConvert.SerializeObject(value);
            }
        }
        return result;
    }
}
