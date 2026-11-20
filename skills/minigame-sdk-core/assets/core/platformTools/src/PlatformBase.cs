using System;
using System.Collections.Generic;

public abstract class PlatformBase
{
    // ==================== 嵌套类型 ====================

    /// <summary>
    /// 统一系统信息数据结构，跨平台通用。
    /// 各平台 GetSystemInfo() 实现负责填充，并通过 LastSystemInfo 暴露。
    /// </summary>
    public class SystemInfoData
    {
        /// <summary>设备品牌（如 "iPhone" / "Xiaomi" / "OPPO"）</summary>
        public string Brand;
        /// <summary>设备型号（如 "iPhone X" / "MI 9"）</summary>
        public string Model;
        /// <summary>操作系统版本（如 "iOS 14.0" / "Android 10"）</summary>
        public string System;
        /// <summary>客户端平台（如 "ios" / "android" / "devtools" / "wii"）</summary>
        public string Platform;
        /// <summary>宿主 APP / 小游戏基础库版本（抖音为 SDKVersion，微信为 SDKVersion）</summary>
        public string SDKVersion;
        /// <summary>宿主 APP 版本（抖音 hostVersion，微信 version）</summary>
        public string HostVersion;
        /// <summary>宿主名称（抖音 hostName，微信 host）</summary>
        public string HostName;
        /// <summary>屏幕宽度（物理像素）</summary>
        public double ScreenWidth;
        /// <summary>屏幕高度（物理像素）</summary>
        public double ScreenHeight;
        /// <summary>设备像素比</summary>
        public double PixelRatio;
        /// <summary>状态栏高度（px）</summary>
        public double StatusBarHeight;
        /// <summary>系统语言（如 "zh_CN" / "en_US"）</summary>
        public string Language;
        /// <summary>设备性能评分（抖音 deviceScore，微信 benchmarkLevel），无则 0</summary>
        public double DeviceScore;
        /// <summary>设备方向（"portrait" / "landscape"）</summary>
        public string DeviceOrientation;

        // ---- wx.getDeviceInfo() 补充字段（仅微信平台，基础库 2.25.3+） ----

        /// <summary>应用（微信APP）二进制接口类型（仅 Android）</summary>
        public string Abi;
        /// <summary>设备二进制接口类型（仅 Android，基础库 2.25.1+）</summary>
        public string DeviceAbi;
        /// <summary>设备 CPU 型号（仅 Android，基础库 2.29.0+）</summary>
        public string CpuType;
        /// <summary>设备内存大小，单位 MB（基础库 2.30.0+）</summary>
        public string MemorySize;

        public override string ToString()
        {
            return string.Format(
                "{{ brand={0}, model={1}, system={2}, platform={3}, SDKVersion={4}, hostVersion={5}, hostName={6}, " +
                "screen={7}x{8}, pixelRatio={9}, statusBarHeight={10}, language={11}, deviceScore={12}, orientation={13}, " +
                "abi={14}, deviceAbi={15}, cpuType={16}, memorySize={17} }}",
                Brand, Model, System, Platform, SDKVersion, HostVersion, HostName,
                ScreenWidth, ScreenHeight, PixelRatio, StatusBarHeight, Language, DeviceScore, DeviceOrientation,
                Abi, DeviceAbi, CpuType, MemorySize);
        }
    }

    /// <summary>
    /// 统一启动参数数据结构，跨平台通用。
    /// 冷启动（GetLaunchOptionsSync）与热启动（onShow）回调均使用此结构。
    /// </summary>
    public class LaunchOptionsData
    {
        /// <summary>启动场景值（对应 tt.getLaunchOptionsSync 的 scene）</summary>
        public string Scene;

        /// <summary>启动页面路径（冷启动有效，热启动通常为空）</summary>
        public string Path;

        /// <summary>启动 query 参数，key-value 形式</summary>
        public Dictionary<string, string> Query;

        /// <summary>启动来源补充信息（仅 onShow 在侧边栏等场景返回，如 "homepage"）</summary>
        public string LaunchFrom;

        /// <summary>启动位置（仅 onShow 在侧边栏场景返回，如 "sidebar_card"）</summary>
        public string Location;

        /// <summary>是否冷启动：true=冷启动（GetLaunchOptionsSync），false=热启动（onShow）</summary>
        public bool IsColdStart;

        /// <summary>原始启动参数 JSON 快照，用于冷启动完整激活及热启动本地状态（可能为 null）</summary>
        public string RawJson;

        /// <summary>安全快照平台返回的完整启动对象，避免新增字段在公共模型转换时丢失。</summary>
        public static string SerializeRaw(object value)
        {
            if (value == null) return null;
            try { return Newtonsoft.Json.JsonConvert.SerializeObject(value); }
            catch { return null; }
        }

        /// <summary>
        /// 生成冷启动激活字段：保留原始启动对象全部字段，同时补齐跨平台标准字段，
        /// 并将 query 第一层展开以兼容归因服务。原始 query 对象仍完整保留。
        /// </summary>
        public Dictionary<string, object> ToReportProperties()
        {
            var result = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(RawJson))
            {
                try
                {
                    var raw = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(RawJson);
                    if (raw != null)
                        foreach (var kv in raw) if (!string.IsNullOrEmpty(kv.Key)) result[kv.Key] = kv.Value;
                }
                catch { }
            }

            result["isColdStart"] = IsColdStart;
            result["scene"] = Scene ?? "";
            result["path"] = Path ?? "";
            result["launchFrom"] = LaunchFrom ?? "";
            result["location"] = Location ?? "";

            var queryCopy = Query == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(Query);
            if (!result.ContainsKey("query")) result["query"] = queryCopy;
            foreach (var kv in queryCopy)
                if (!string.IsNullOrEmpty(kv.Key) && !result.ContainsKey(kv.Key)) result[kv.Key] = kv.Value ?? "";
            return result;
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{ ");
            sb.Append("isColdStart=").Append(IsColdStart);
            sb.Append(", scene=").Append(Scene);
            sb.Append(", path=").Append(Path);
            sb.Append(", launchFrom=").Append(LaunchFrom);
            sb.Append(", location=").Append(Location);
            if (Query != null && Query.Count > 0)
            {
                sb.Append(", query={");
                bool first = true;
                foreach (var kv in Query)
                {
                    if (!first) sb.Append(",");
                    sb.Append(kv.Key).Append("=").Append(kv.Value);
                    first = false;
                }
                sb.Append("}");
            }
            sb.Append(" }");
            return sb.ToString();
        }
    }

    /// <summary>侧边栏按钮状态</summary>
    public enum RewardedAdResultType
    {
        Success,
        Closed,
        Error
    }

    public class RewardedAdResult
    {
        public RewardedAdResultType Type;
        public int ErrorCode;
        public string ErrorMessage;
    }

    public enum SidebarState
    {
        /// <summary>未添加，点击跳转侧边栏</summary>
        GoToAdd,
        /// <summary>已添加，可领取奖励</summary>
        CanClaim,
        /// <summary>已领取，置灰不可点击</summary>
        Claimed
    }

    // ==================== 侧边栏（virtual，默认空实现，DouyinPlatform 覆写） ====================

    public virtual SidebarState CurrentSidebarState => SidebarState.GoToAdd;
    public virtual bool IsSidebarSupported => false;
    public virtual bool IsFromSidebar => false;

    /// <summary>侧边栏状态变化回调（UI 监听刷新按钮）</summary>
    public event Action<SidebarState> OnSidebarStateChanged;

    /// <summary>从侧边栏启动回调（用于触发奖励逻辑）</summary>
    public event Action OnSidebarEnter;

    /// <summary>子类调用：触发侧边栏状态变化事件</summary>
    protected void RaiseSidebarStateChanged(SidebarState state) => OnSidebarStateChanged?.Invoke(state);

    /// <summary>子类调用：触发从侧边栏启动事件</summary>
    protected void RaiseSidebarEnter() => OnSidebarEnter?.Invoke();

    public virtual void RefreshSidebarButtonState() { }
    public virtual void ClaimSidebarReward() { }
    public virtual void ResetSidebarState() { }
    public virtual void DisposeSidebar() { }
    public virtual void Dispose() { }
    public virtual void PrepareRewardedAd(string adId) { }

    // ==================== 启动参数 ====================

    /// <summary>最近一次获取到的启动参数（同步访问，默认 null）</summary>
    public virtual LaunchOptionsData LastLaunchOptions => null;

    /// <summary>最近一次获取到的系统信息（同步访问，默认 null）。</summary>
    public SystemInfoData LastSystemInfo { get; protected set; }

    /// <summary>
    /// 获取启动参数（统一入口，冷启动 + 热启动）。
    /// callback 至少被回调一次：调用时立即回调冷启动参数（若可用），
    /// 之后每次 onShow（热启动）会再次回调更新后的参数。
    /// 非目标平台（编辑器/Default）会以空 LaunchOptionsData 回调一次。
    /// </summary>
    public abstract void getEnterOptions(Action<LaunchOptionsData> callback);

    // ==================== 原有抽象方法 ====================

    public abstract void Share(string title, string imageUrl, Action<bool> callback);

    public abstract void ShowAd(string adId, Action<RewardedAdResult> callback, Action onShow = null);

    public virtual void PrepareBannerAd(string adId) { }
    public virtual void PrepareInterstitialAd(string adId) { }
    public virtual void PreloadRewardedAd(string adId) { MiniGameRewardedBridge.Preload(GetPlatformName(),adId); }
    public virtual void CancelAdPreload(string kind) {
        if(kind=="reward") MiniGameRewardedBridge.DisposeInstance();else MiniGameDisplayAdBridge.Dispose(kind);
    }
    public virtual void ShowBannerAd(string adId) { }

    public virtual void HideBannerAd() { }

    public virtual void ShowInterstitialAd(string adId) { }

    public abstract string GetPlatformName();

    public abstract bool IsFeatureSupported(string featureId);

    public abstract void GetSystemInfo();

    public abstract void goSideBar();

    public abstract void vibrate(int vibrateType = 1);

    public abstract void setUserRecord(int score);

    /// <summary>
    /// 拉起平台原生好友排行榜 UI。
    /// 抖音平台走 TT.GetImRankList（native UI，无需开发者画），
    /// 微信/默认平台为空实现（微信需开放数据域自行绘制，暂不支持）。
    /// </summary>
    /// <returns>true=已拉起平台原生排行榜；false=当前平台不支持</returns>
    public virtual bool ShowRankList() { return false; }

    /// <summary>添加桌面快捷方式</summary>
    /// <param name="callback">true=成功，false=失败</param>
    public abstract void AddShortcut(Action<bool> callback);

    /// <summary>
    /// 显示平台原生 Toast 提示。
    /// 抖音平台走 tt.showToast，非抖音平台走 UIManager.ShowMsg 兜底。
    /// </summary>
    /// <param name="message">提示内容</param>
    /// <param name="durationMs">停留时长（毫秒），默认 1500</param>
    public virtual void ShowToast(string message, int durationMs = 1500) { }

    // ==================== 登录 ====================

    /// <summary>
    /// 平台登录，获取临时登录凭证。
    /// callback(code, anonymousCode)：
    ///   - 抖音平台：TT.Login 返回 code（可换取 openId）+ anonymousCode
    ///   - 非抖音平台：code 和 anonymousCode 均为空字符串
    /// </summary>
    public virtual void Login(Action<string, string> callback)
    {
        callback?.Invoke("", "");
    }

    // ==================== 事件上报（平台原生） ====================

    /// <summary>
    /// 平台原生事件上报（如抖音 tt.reportAnalytics）。
    /// 数据上报到小程序开发者平台，需提前在后台配置事件名与字段。
    /// 非抖音平台为空实现。
    /// </summary>
    /// <param name="eventName">事件名（需在平台后台预先创建）</param>
    /// <param name="properties">上报字段，key 对应后台配置的字段名</param>
    public virtual void ReportEvent(string eventName, Dictionary<string, object> properties) { }

    // ==================== 设备/平台识别 ====================

    /// <summary>当前是否运行在 iOS 真机上（基于平台 SDK systemInfo.platform 判断）。</summary>
    public virtual bool IsIOS => false;
}
