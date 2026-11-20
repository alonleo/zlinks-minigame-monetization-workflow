using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 平台管理器：纯 C# 类，不依赖 GameObject/MonoBehaviour。
/// 按编译宏分发到对应平台实现，所有平台调用统一入口。
/// 侧边栏、启动参数等能力全部通过本类访问，不直接引用 DouyinPlatform。
/// </summary>
public partial class PlatformManager
{
    public static PlatformManager Instance { get; private set; } = new PlatformManager();

    private bool _adRequestPending;
    private bool _disposed;
    private const float GamingReportInterval = 5f;
    private const float ShareCallbackDelay = 3f;
    private readonly List<KeyValuePair<float, Action>> _shareCallbacks = new List<KeyValuePair<float, Action>>();
    private PlatformBase _platform;
    private double _nextGamingReportTime = Time.realtimeSinceStartupAsDouble + GamingReportInterval;
    private double _gameplaySegmentStartedAt = Time.realtimeSinceStartupAsDouble;
    private double _gameplayElapsedSeconds;
    private bool _isApplicationForeground = true;
    private bool _isRewardedAdShowing;

    private bool IsGameplayActive => _isApplicationForeground && !_isRewardedAdShowing;


    public PlatformBase CurrentPlatform => _platform;
    public bool EnablePlayerIsGamingReport { get; set; } = true;// 是否上报心跳

    /// <summary>
    /// 排行榜分数提供者：返回当前要上报到排行榜的数值。
    /// 由项目业务层赋值（如已过关卡数 = SaveBridge.Level - 1），
    /// platformTools 本身不依赖项目存档结构。null 时 ShowRankList 不自动上报。
    /// </summary>
    public Func<int> RankScoreProvider;

    /// <summary>检查 Instance 是否存在</summary>
    public static bool HasInstance => Instance != null;

    private PlatformManager()
    {
        InitPlatform();
    }

    private void InitPlatform()
    {
        // 编辑器下没有真实的小游戏容器，TT.* / wx.* 等 SDK 调用无法生效，
        // 即使平台编译宏被全局定义也走空实现，避免编辑器内报错。
#if MINIGAME_KIT_BROWSER_TEST && !UNITY_EDITOR
        _platform = new BrowserTestPlatform();
#elif MINIGAME_SUBPLATFORM_DOUYIN && !UNITY_EDITOR
        _platform = new DouyinPlatform();
        Debug.Log("DouyinPlatform");
        // 侧边栏复访能力初始化已整合到 DouyinPlatform.GetSystemInfo() 内
#elif (WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN) && !UNITY_EDITOR
        _platform = new WeiXinPlatform();
        Debug.Log("WeiXinPlatform");
#else
        _platform = new DefaultPlatform();
        Debug.Log("DefaultPlatform");
#endif
        _platform.GetSystemInfo();
        InitializeAdPreloads();

        // 归因回传：获取启动参数 + 登录 + 激活上报
        // 注意：此时 PlatformManager.Instance 尚未赋值（构造函数未返回），
        // 不能在 InitAscribe 内用 PlatformManager.Instance，直接传 _platform 进去。
        TrackManager.Instance.InitPlatform(_platform);
        if (AppConst.EnableCustomReport || AppConst.EnableDnSDK)
            TrackManager.Instance.InitAscribe(_platform);
        ReportPlayerIsGaming(Time.realtimeSinceStartupAsDouble);
    }

    // ==================== 基础能力 ====================

    /// <summary>当前是否运行在 iOS 真机上（委托给当前平台实现）。</summary>
    public bool IsIOS => _platform != null && _platform.IsIOS;

    /// <summary>
    /// 发起平台分享，并上报分享来源界面。
    /// </summary>
    /// <param name="from">分享来源界面标识（如 "revive"、"sidebar"），随 share_click / share_result 上报</param>
    /// <param name="callback">分享结果回调，true=成功，false=失败/取消</param>
    public void Share(string from, Action<bool> callback)
    {
        var callbackAt = Time.realtimeSinceStartup + ShareCallbackDelay;
        var receivedResult = false;
        var props = string.IsNullOrEmpty(from)
            ? null
            : new Dictionary<string, object> { { "from", from } };
        ReportEvent("share_click", props);
        _platform.Share("", "", result =>
        {
            if (receivedResult) return;
            receivedResult = true;
            Action deliverResult = () =>
            {
                var resultProps = string.IsNullOrEmpty(from)
                    ? null
                    : new Dictionary<string, object> { { "from", from }, { "success", result } };
                ReportEvent("share_result", resultProps);
                callback?.Invoke(result);
                ShowToast(result ? "分享成功" : "分享失败");
            };
            // WeChat only acknowledges opening its share panel; synthesize the delayed
            // result there. Douyin and other platforms keep their native callback timing.
            if (_platform.GetPlatformName() == "weixin" && result)
                _shareCallbacks.Add(new KeyValuePair<float, Action>(callbackAt, deliverResult));
            else
                deliverResult();
        });
    }

    // Legacy callers retain their signature, but no generic template can infer a Tencent placement.
    // Fixed rewarded-video entries must migrate to the explicit placement overload below.
    public bool ShowAd(string adId, string propName, Action<bool> callback)
    {
        if (_disposed || _adRequestPending) return false;
        return ShowAd(adId, propName, 0, callback);
    }

    // The Tencent placement ID and the player-facing reward name are independent.
    public bool ShowAd(string adId, string propName, int dnPlacementId, Action<bool> callback)
    {
        if (_disposed || !_isApplicationForeground || _adRequestPending || FrequencyBlocked("reward",_adUtcNow())) return false;
        propName = ResolveRewardName(propName);
        dnPlacementId = dnPlacementId >= 1 && dnPlacementId <= 67 ? dnPlacementId : 0;
        var clickProperties = new Dictionary<string, object> { { "prop_name", propName }, { "ad_placement_name", dnPlacementId } };
        ReportEvent("ad_reward_load", clickProperties);
        ReportEvent("ad_reward_click", clickProperties);
#if MINIGAME_KIT_BROWSER_TEST && !UNITY_EDITOR
        if (BrowserTestPlatform.NextOutcome == BrowserTestPlatform.Outcome.NotStarted)
        {
            ReportEvent("ad_reward_skipped", new Dictionary<string, object> { { "prop_name", propName }, { "ad_placement_name", dnPlacementId } });
            return false;
        }
#else
        if (AppConst.isNoAd || string.IsNullOrWhiteSpace(adId))
        {
            MiniGameRewardedBridge.DisposeInstance();
            ShowToast("恭喜您获得奖励，已开启无广告活动");
            callback?.Invoke(true);
            return true; // no fake exposure, completion or ad_count
        }
#endif
        var rewardPreload=RegisterPreload("reward",adId);
        if(!rewardPreload.Ready) { rewardPreload.Loading=true;rewardPreload.Started=_adUtcNow(); }
        _adRequestPending = true;
        bool completed = false, shown = false;
        Action<PlatformBase.RewardedAdResult> finish = result =>
        {
            if (completed || _disposed) return;
            completed = true; _adRequestPending = false;
            FinishAdResource("reward",adId,result.Type==PlatformBase.RewardedAdResultType.Error);
            SetRewardedAdShowing(false);
            var props = new Dictionary<string, object> { { "prop_name", propName }, { "ad_placement_name", dnPlacementId } };
            if (result.Type == PlatformBase.RewardedAdResultType.Success)
                ReportEvent("ad_reward_success", props);
            else if (result.Type == PlatformBase.RewardedAdResultType.Closed)
                ReportEvent("ad_reward_close", props);
            else
            {
                props["code"] = result.ErrorCode; props["message"] = result.ErrorMessage;
                ReportEvent("ad_reward_error", props);
            }
            callback?.Invoke(result.Type == PlatformBase.RewardedAdResultType.Success);
        };
        try
        {
            _platform.ShowAd(adId, finish, () =>
            {
                if (shown || completed || _disposed) return;
                shown = true; SetRewardedAdShowing(true);
                ReportEvent("ad_reward_show", new Dictionary<string, object> { { "prop_name", propName }, { "ad_placement_name", dnPlacementId } });
                TrackManager.Instance.ReportExposure();
            });
        }
        catch { finish(new PlatformBase.RewardedAdResult { Type=PlatformBase.RewardedAdResultType.Error, ErrorCode=-1, ErrorMessage="platform_error" }); }
        return true;
    }

    private sealed class AdPreload
    {
        public string Kind, Id;
        public bool Ready, Loading, ShowRequested, Stopped;
        public int Failures;
        public long Next, Started;
    }
    private readonly Dictionary<string,AdPreload> _adPreloads=new Dictionary<string,AdPreload>();
    private AdPreload RegisterPreload(string kind,string id,long delay=0)
    {
        AdPreload s;
        if(_adPreloads.TryGetValue(kind,out s) && s.Id==id) return s;
        if(s!=null) _platform.CancelAdPreload(kind);
        s=new AdPreload{Kind=kind,Id=id,Next=_adUtcNow()+delay};
        _adPreloads[kind]=s;return s;
    }
    private void InitializeAdPreloads()
    {
        if(AppConst.isNoAd || (_platform.GetPlatformName()!="weixin" && _platform.GetPlatformName()!="douyin")) return;
        if(!string.IsNullOrWhiteSpace(AppConst.BannerId)) RegisterPreload("banner",AppConst.BannerId);
        if(!string.IsNullOrWhiteSpace(AppConst.InterstitialId)) RegisterPreload("interstitial",AppConst.InterstitialId);
        if(!string.IsNullOrWhiteSpace(AppConst.RewardVideoId)) RegisterPreload("reward",AppConst.RewardVideoId,30000);
    }
    public bool PreloadBannerAd(string id) { return RequestPreload("banner",id); }
    public bool PreloadInterstitialAd(string id) { return RequestPreload("interstitial",id); }
    public bool PreloadRewardedAd(string id) { return RequestPreload("reward",id); }
    private bool RequestPreload(string kind,string id)
    {
        if(_disposed || AppConst.isNoAd || !_isApplicationForeground || string.IsNullOrWhiteSpace(id) || FrequencyBlocked(kind,_adUtcNow())) return false;
        if(kind!="reward" && !AdmitDisplayAd(kind,false)) return false;
        var s=RegisterPreload(kind,id);
        if(s.Ready || s.Loading) return false;
        return StartPreload(s);
    }
    private bool FrequencyBlocked(string kind,long now)
    {
        long until;return long.TryParse(MiniGamePrefs.GetString(AdFrequencyKey(kind+"-blocked-until"),""),out until) && now<until;
    }
    private bool StartPreload(AdPreload s,bool useRequest=false)
    {
        long now=_adUtcNow();
        if(_platform.GetPlatformName()!="weixin" && _platform.GetPlatformName()!="douyin") return false;
        if(_disposed || AppConst.isNoAd || !_isApplicationForeground || _adRequestPending || _isRewardedAdShowing
            || s.Ready || s.Loading || s.Stopped || now<s.Next || FrequencyBlocked(s.Kind,now)) return false;
        if(s.Kind!="reward" && !AdmitDisplayAd(s.Kind,s.Failures==0)) return false;
        if(s.Kind!="reward" && s.Failures>0) SaveAdTime(s.Kind+"-requested",now);
        s.Loading=true;s.Started=now;
        if(useRequest) { s.ShowRequested=true;ReportEvent("ad_"+s.Kind+"_load"); }
        try {
            if(s.Kind=="banner") _platform.PrepareBannerAd(s.Id);
            else if(s.Kind=="interstitial") _platform.PrepareInterstitialAd(s.Id);
            else _platform.PreloadRewardedAd(s.Id);
        } catch(Exception e) { NotifyAdPreload(s.Kind,s.Id,false,-1,e.Message); }
        return true;
    }
    internal void NotifyAdPreload(string kind,string id,bool success,int code=0,string message="")
    {
        AdPreload s;if(!_adPreloads.TryGetValue(kind,out s) || s.Id!=id || _disposed || (!s.Loading && !(!success && s.Ready && !s.ShowRequested))) return;
        s.Loading=false;s.Ready=success;
        if(success) {
            s.Failures=0;s.Stopped=false;s.Next=long.MaxValue;ReportEvent("ad_"+kind+"_preload");
            if(kind!="reward" && s.ShowRequested) {
                if(_isApplicationForeground && AdmitDisplayAd(kind,false)) InvokePreparedDisplay(s);
                else s.ShowRequested=false;
            }
            return;
        }
        s.ShowRequested=false;
        s.Failures++;
        s.Stopped=code==-5 || code==1001 || code==1002 || code==1005 || code==1006 || code==1007 || code==1008 || code==20002;
        long delay=10000L*Math.Min(s.Failures,3);
        string text=(message ?? "").ToLowerInvariant();
        bool frequency=code==2001 || code==2002 || code==2003 || text.Contains("频控") || text.Contains("频率") || text.Contains("frequency") || text.Contains("too frequent");
        if(frequency) {
            delay=Math.Max(delay,60000);
            SaveAdTime(kind+"-blocked-until",_adUtcNow()+delay);
        }
        s.Next=s.Stopped ? long.MaxValue : _adUtcNow()+delay;
        ReportEvent("ad_"+kind+"_error",new Dictionary<string,object>{{"code",code},{"message",message ?? ""},{"phase","preload"}});
    }
    internal void FinishAdResource(string kind,string id,bool failed=false)
    {
        AdPreload s;if(!_adPreloads.TryGetValue(kind,out s) || s.Id!=id) return;
        s.Loading=false;s.Ready=false;s.ShowRequested=false;s.Next=_adUtcNow()+(failed?10000:0);
    }
    private void TickAdPreloads()
    {
        if(_disposed || !_isApplicationForeground || AppConst.isNoAd || _adRequestPending || _isRewardedAdShowing) return;
        foreach(var s in new List<AdPreload>(_adPreloads.Values)) {
            if(s.Loading && _adUtcNow()-s.Started>=30000) {
                _platform.CancelAdPreload(s.Kind);NotifyAdPreload(s.Kind,s.Id,false,-1,"preload_timeout");
            }
            StartPreload(s);
        }
    }
    private bool ShowPreparedDisplay(string kind,string id)
    {
        if(_disposed || AppConst.isNoAd || !_isApplicationForeground || string.IsNullOrWhiteSpace(id)
            || !AdmitDisplayAd(kind,false)) return false;
        var s=RegisterPreload(kind,id);
        if(s.ShowRequested) return false;
        bool waiting=!s.Ready;
        if(waiting && !s.Loading) {
            // A genuine use request overrides ordinary retry delay, never host frequency.
            if(s.Stopped) return false;
            s.Next=_adUtcNow();
            return StartPreload(s,true);
        }
        s.ShowRequested=true;
        ReportEvent("ad_"+kind+"_load");
        if(s.Ready) InvokePreparedDisplay(s);
        return true;
    }

    private void InvokePreparedDisplay(AdPreload s)
    {
        try { if(s.Kind=="banner") _platform.ShowBannerAd(s.Id);else _platform.ShowInterstitialAd(s.Id); }
        catch(Exception e) { FinishAdResource(s.Kind,s.Id,true);ReportEvent("ad_"+s.Kind+"_error",new Dictionary<string,object>{{"code",-1},{"message",e.Message}}); }
    }

    // Local request throttles; Banner 30s is our policy, not a host Show cooldown.
    public double BannerRequestIntervalSeconds { get; set; } = 30;
    public double InterstitialRequestIntervalSeconds { get; set; } = 60;
    private Func<long> _adUtcNow = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private long _adSessionStartedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private string AdFrequencyKey(string name)
    {
        return "mgk:ad-frequency:"+_platform.GetPlatformName()+":"+AppConst.AppId+":"+name;
    }
    private bool AdIntervalElapsed(string name,long now,double seconds)
    {
        if(double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds<0) seconds=0;
        string key=AdFrequencyKey(name);
        long previous;
        if(!long.TryParse(MiniGamePrefs.GetString(key,""),System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,out previous) || previous<=0) return true;
        // A backwards wall-clock change must not unlock requests or block them for days.
        if(previous>now) { previous=now;MiniGamePrefs.SetString(key,now.ToString(System.Globalization.CultureInfo.InvariantCulture));MiniGamePrefs.Save(); }
        return now-previous>=seconds*1000d;
    }
    private void SaveAdTime(string name,long now)
    {
        MiniGamePrefs.SetString(AdFrequencyKey(name),now.ToString(System.Globalization.CultureInfo.InvariantCulture));
        MiniGamePrefs.Save();
    }
    private bool AdmitDisplayAd(string kind,bool forPreload=true)
    {
        string channel=_platform.GetPlatformName();
        if(channel!="douyin" && channel!="weixin") return true;
        long now=_adUtcNow();
        double interval=kind=="banner" ? BannerRequestIntervalSeconds : Math.Max(60,InterstitialRequestIntervalSeconds);
        if(kind=="interstitial") {
            if(_adRequestPending || _isRewardedAdShowing) return false;
            if(now<_adSessionStartedAt) _adSessionStartedAt=now;
            if(now-_adSessionStartedAt<30000) return false;
            if(!AdIntervalElapsed("interstitial-shown",now,60)) return false;
            if(channel=="douyin" && !AdIntervalElapsed("reward-shown",now,60)) return false;
        }
        if(FrequencyBlocked(kind,now)) return false;
        if(!forPreload) return true;
        if(!AdIntervalElapsed(kind+"-requested",now,interval)) return false;
        // Reserve before invoking the adapter, so synchronous/reentrant callbacks cannot bypass throttling.
        SaveAdTime(kind+"-requested",now);
        return true;
    }
    private void ObserveAdFrequency(string eventName,Dictionary<string,object> properties)
    {
        if(_platform.GetPlatformName()!="douyin" && _platform.GetPlatformName()!="weixin") return;
        if(eventName=="ad_interstitial_show") SaveAdTime("interstitial-shown",_adUtcNow());
        else if(eventName=="ad_reward_show") SaveAdTime("reward-shown",_adUtcNow());
        if(eventName=="ad_reward_error" || eventName=="ad_banner_error" || eventName=="ad_interstitial_error") {
            object value;int code=0;
            if(properties.TryGetValue("code",out value)) int.TryParse(Convert.ToString(value),out code);
            string message=properties.TryGetValue("message",out value)?Convert.ToString(value).ToLowerInvariant():"";
            if(code==2001 || code==2002 || code==2003 || message.Contains("频控") || message.Contains("频率") || message.Contains("frequency") || message.Contains("too frequent")) {
                string kind=eventName=="ad_reward_error"?"reward":eventName=="ad_banner_error"?"banner":"interstitial";
                SaveAdTime(kind+"-blocked-until",_adUtcNow()+60000);
            }
        }
    }

    public bool ShowBannerAd(string adId)
    {
        return ShowPreparedDisplay("banner",adId);
    }
    public void HideBannerAd() {
        _platform.HideBannerAd();
        AdPreload s;if(_adPreloads.TryGetValue("banner",out s)) s.ShowRequested=false;
    }
    public bool ShowInterstitialAd(string adId)
    {
        return ShowPreparedDisplay("interstitial",adId);
    }
    internal void NotifyRewardVideoOpening() { SetRewardedAdShowing(true); }

    /// <summary>
    /// 同步小游戏前后台状态。后台期间暂停游戏心跳和游戏时长累计；
    /// 回到前台后从当前时刻继续累计。游戏内暂停仍计时；本状态只由宿主 onShow/onHide 更新。
    /// </summary>
    public void SetApplicationForeground(bool isForeground)
    {
        if (_isApplicationForeground == isForeground) return;
        bool wasActive = IsGameplayActive;
        _isApplicationForeground = isForeground;
        ApplyGameplayActivityTransition(wasActive);
        if (isForeground) ReportPlayerIsGaming(Time.realtimeSinceStartupAsDouble);
        ReportEvent(isForeground ? "app_show" : "app_hide");
    }

    private void SetRewardedAdShowing(bool isShowing)
    {
        if (_isRewardedAdShowing == isShowing) return;
        bool wasActive = IsGameplayActive;
        _isRewardedAdShowing = isShowing;
        ApplyGameplayActivityTransition(wasActive);
    }

    private void ApplyGameplayActivityTransition(bool wasActive)
    {
        bool isActive = IsGameplayActive;
        if (wasActive == isActive) return;

        double now = Time.realtimeSinceStartupAsDouble;
        if (!isActive)
        {
            _gameplayElapsedSeconds += Math.Max(0d, now - _gameplaySegmentStartedAt);
            return;
        }

        _gameplaySegmentStartedAt = now;
        _nextGamingReportTime = now + GamingReportInterval;
    }

    private void ReportPlayerIsGaming(double now)
    {
        if (!EnablePlayerIsGamingReport || !IsGameplayActive) return;
        _nextGamingReportTime = now + GamingReportInterval;
        long session = TrackManager.Instance.Session;
        long gameDuration = (long)Math.Floor(_gameplayElapsedSeconds +
                                             Math.Max(0d, now - _gameplaySegmentStartedAt));
        ReportEvent("player_is_gaming", new Dictionary<string, object>
        {
            { "session", session },
            { "time", gameDuration },
        });
    }

    internal void Tick()
    {
        if (_disposed) return;
        TickAdPreloads();
        // Unscaled time: reward callbacks must still run while gameplay is paused.
        for (var i = _shareCallbacks.Count - 1; i >= 0; i--)
        {
            if (Time.realtimeSinceStartup < _shareCallbacks[i].Key) continue;
            var callback = _shareCallbacks[i].Value;
            _shareCallbacks.RemoveAt(i);
            try { callback(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        if (!EnablePlayerIsGamingReport || !IsGameplayActive)
        {
            return;
        }

        double now = Time.realtimeSinceStartupAsDouble;
        if (now < _nextGamingReportTime)
        {
            return;
        }

        // session 仍是本次启动标识；time 只统计应用处于前台的累计秒数。
        ReportPlayerIsGaming(now);
    }

    public string GetPlatformName()
    {
        return _platform.GetPlatformName();
    }

    public bool IsFeatureSupported(string featureId)
    {
        return _platform.IsFeatureSupported(featureId);
    }

    public void GetSystemInfo()
    {
        _platform.GetSystemInfo();
    }

    /// <summary>最近一次获取到的系统信息（同步访问，可能为 null）。</summary>
    public PlatformBase.SystemInfoData LastSystemInfo => _platform?.LastSystemInfo;

    public void goSideBar()
    {
        _platform.goSideBar();
    }

    public void vibrate(int vibrateType = 1)
    {
        _platform.vibrate(vibrateType);
    }

    public void setUserRecord(int score)
    {
        _platform.setUserRecord(score);
    }

    /// <summary>
    /// 拉起平台原生好友排行榜。
    /// 抖音平台走 TT.GetImRankList（native UI），其他平台空实现。
    /// 拉起前会通过 RankScoreProvider 上报一次最新分数，保证排行榜展示的是当前进度。
    /// </summary>
    /// <returns>true=已拉起；false=当前平台不支持</returns>
    public bool ShowRankList()
    {
        // 拉起前先上报当前分数，避免排行榜展示旧数据
        if (RankScoreProvider != null)
        {
            setUserRecord(RankScoreProvider());
        }
        return _platform.ShowRankList();
    }

    public void AddShortcut(Action<bool> callback)
    {
        _platform.AddShortcut(callback);
    }

    // ==================== 启动参数（冷启动 + 热启动） ====================

    /// <summary>
    /// 获取启动参数（冷启动 + 热启动统一入口）。
    /// callback 至少回调一次（冷启动参数），之后每次 onShow 热启动会再次回调。
    /// 调用示例：
    ///   PlatformManager.Instance.getEnterOptions(opts => {
    ///       Debug.Log("启动参数: " + opts);
    ///       // opts.IsColdStart 区分冷/热启动
    ///       // opts.Scene / opts.Query / opts.LaunchFrom / opts.Location
    ///   });
    /// </summary>
    public void getEnterOptions(Action<PlatformBase.LaunchOptionsData> callback)
    {
        _platform.getEnterOptions(callback);
    }

    /// <summary>最近一次获取到的启动参数（同步访问，可能为 null）</summary>
    public PlatformBase.LaunchOptionsData LastLaunchOptions => _platform.LastLaunchOptions;

    // ==================== 侧边栏复访 ====================

    /// <summary>当前侧边栏状态</summary>
    public PlatformBase.SidebarState CurrentSidebarState => _platform.CurrentSidebarState;

    /// <summary>宿主是否支持侧边栏</summary>
    public bool IsSidebarSupported => _platform.IsSidebarSupported;

    /// <summary>是否从侧边栏启动</summary>
    public bool IsFromSidebar => _platform.IsFromSidebar;

    /// <summary>侧边栏状态变化回调（UI 监听刷新按钮）</summary>
    public event Action<PlatformBase.SidebarState> OnSidebarStateChanged
    {
        add => _platform.OnSidebarStateChanged += value;
        remove => _platform.OnSidebarStateChanged -= value;
    }

    /// <summary>从侧边栏启动回调（用于触发奖励逻辑）</summary>
    public event Action OnSidebarEnter
    {
        add => _platform.OnSidebarEnter += value;
        remove => _platform.OnSidebarEnter -= value;
    }

    /// <summary>刷新侧边栏按钮状态（UI 打开时调用）</summary>
    public void RefreshSidebarButtonState()
    {
        _platform.RefreshSidebarButtonState();
    }

    /// <summary>领取侧边栏奖励</summary>
    public void ClaimSidebarReward()
    {
        _platform.ClaimSidebarReward();
    }

    /// <summary>重置侧边栏状态（调试用）</summary>
    public void ResetSidebarState()
    {
        _platform.ResetSidebarState();
    }

    /// <summary>销毁时移除 onShow 监听</summary>
    public void DisposeSidebar()
    {
        _platform.DisposeSidebar();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _shareCallbacks.Clear();
        foreach(var s in _adPreloads.Values) _platform.CancelAdPreload(s.Kind);
        _adPreloads.Clear();
        MiniGameRewardedBridge.DisposeInstance();
        TrackManager.Instance.Dispose();
        _platform.Dispose();
    }

    // ==================== Toast ====================

    /// <summary>
    /// 显示平台原生 Toast 提示。
    /// 抖音平台走 tt.showToast，非抖音平台走 UIManager.ShowMsg 兜底。
    /// </summary>
    /// <param name="message">提示内容</param>
    /// <param name="durationMs">停留时长（毫秒），默认 1500</param>
    public void ShowToast(string message, int durationMs = 1500)
    {
        _platform.ShowToast(message, durationMs);
    }

    // ==================== 登录 ====================

    /// <summary>
    /// 平台登录，获取临时登录凭证。
    /// callback(code, anonymousCode)：抖音平台返回 code（可换 openId）+ anonymousCode，非抖音平台均返回空。
    /// </summary>
    public void Login(Action<string, string> callback)
    {
        _platform.Login(callback);
    }

    // ==================== 事件上报（平台原生） ====================

    /// <summary>
    /// 统一事件上报入口。同时发两路：
    ///   1. 平台原生上报（抖音 tt.reportAnalytics → 开发者平台数据看板）
    ///   2. 自建打点服务器（TrackManager.Track → 自有服务器）
    /// 所有业务打点统一调本方法，不再直接调 TrackManager.Track。
    /// </summary>
    /// <param name="eventName">事件名</param>
    /// <param name="properties">上报字段</param>
    public void ReportEvent(string eventName, Dictionary<string, object> properties = null)
    {
        if (eventName == "tutorial_start") { ReportTutorialStart(); return; }
        ReportEventCore(eventName,properties);
    }

    private bool ReportEventCore(string eventName, Dictionary<string, object> properties)
    {
        if (_disposed || !IsValidEventName(eventName)) return false;
        Dictionary<string, object> payload;
        try { payload = PrepareTelemetry(eventName, properties); }
        catch { Debug.LogWarning("[MiniGameKit] 事件字段不合法，已跳过上报"); return false; }
        if (payload == null) return false;
        ObserveAdFrequency(eventName,payload);
        if (AppConst.EnablePlatformReport)
        {
            try { _platform.ReportEvent(eventName, payload); }
            catch { Debug.LogWarning("[MiniGameKit] 平台统计暂不可用"); }
        }
        try { if (AppConst.EnableCustomReport) TrackManager.Instance.Track(eventName, payload); } catch { Debug.LogWarning("[MiniGameKit] 自有统计暂不可用"); }
        try { DispatchTencent(eventName, payload); } catch { Debug.LogWarning("[MiniGameKit] dnSDK 暂不可用"); }
        if (eventName == "level_success" && FirstLevelIsTutorial && currentLevel == 1) ReportTutorialFinish();
        return true;
    }
}

internal sealed class PlatformManagerDriver : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        var gameObject = new GameObject(nameof(PlatformManagerDriver));
        DontDestroyOnLoad(gameObject);
        gameObject.AddComponent<PlatformManagerDriver>();
    }

    private void Update()
    {
        PlatformManager.Instance.Tick();
    }

    // Host onShow/onHide owns telemetry foreground state. Unity pause must not change it.
    private void OnDestroy()
    {
        if (PlatformManager.HasInstance)
        {
            PlatformManager.Instance.Dispose();
        }
    }
}
