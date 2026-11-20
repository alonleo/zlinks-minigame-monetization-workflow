#if MINIGAME_SUBPLATFORM_WEIXIN && !WEIXINMINIGAME
#define WEIXINMINIGAME
#endif

using System.Collections.Generic;
using UnityEngine;
#if WEIXINMINIGAME && !UNITY_EDITOR
using WeChatWASM;
#endif

namespace Service
{
    /// <summary>
    /// 腾讯营销小游戏 SDK（@dn-sdk/minigame）C# → JS 桥接。
    ///
    /// JS 侧由 dn-sdk-init.js 初始化后将 SDK 实例挂到 GameGlobal.dnSDK。
    /// C# 侧通过 WX.CallJSFunction("dnSDK", methodName, args) 调用 JS 方法。
    ///
    /// 仅在微信小游戏构建（WEIXINMINIGAME 宏）下生效，其他平台为空实现。
    /// IAA 必报事件清单参考：腾讯广告-IAA微信小游戏采集行为列表.docx
    /// </summary>
    public static class DnSDKBridge
    {
        /// <summary>JS 侧 GameGlobal 上的 SDK 实例名，与 dn-sdk-init.js 一致。</summary>
        private const string JsInstanceName = "dnSDK";

        /// <summary>
        /// 道具使用信息（IAA items 字段）。
        /// 文档要求 string_array 格式，每个元素为 "{item_type:N,item_num:N}" 字符串。
        /// </summary>
        public struct DnItemUsage
        {
            public int itemType;
            public int itemNum;

            public DnItemUsage(int itemType, int itemNum)
            {
                this.itemType = itemType;
                this.itemNum = itemNum;
            }

            public override string ToString()
            {
                return "{item_type:" + itemType + ",item_num:" + itemNum + "}";
            }
        }

        // ==================== 用户 ID ====================

        /// <summary>一次性交付真实身份；JS 首次构造 dnSDK 时同时携带 OpenID/UnionID。</summary>
        public static void SetIdentity(string openId, string unionId)
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            if (!IsReady()) return;
            WX.CallJSFunction(JsInstanceName, "setIdentity", openId, unionId ?? "");
#endif
        }

        /// <summary>设置微信 OpenID（获取后尽早调用，否则数据只缓存不上报）。</summary>
        public static void SetOpenId(string openId)
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            if (!IsReady()) return;
            WX.CallJSFunction(JsInstanceName, "setOpenId", openId);
#endif
        }

        /// <summary>设置微信 UnionID（无 OpenID 时使用）。</summary>
        public static void SetUnionId(string unionId)
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            if (!IsReady()) return;
            WX.CallJSFunction(JsInstanceName, "setUnionId", unionId);
#endif
        }

        public static bool TryRegister()
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            if (!IsReady()) return false;
            string initialized=WX.CallJSFunctionWithReturn("dnSDKReady","initialized");
            if(initialized!="1") return false;
            WX.CallJSFunction(JsInstanceName,"onRegister");
            return true;
#else
            return false;
#endif
        }

        // ==================== 通用上报 ====================

        /// <summary>
        /// 通用行为上报方法。
        /// </summary>
        /// <param name="actionType">行为名称，如 "LEVEL_ENTER"、"SHARE"、"RE_ACTIVE"</param>
        /// <param name="param">行为参数，Key-Value；为 null 时不上报参数</param>
        public static bool IsValidActionType(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length>64) return false;
            for (int i=0;i<name.Length;i++) {
                char c=name[i];
                bool letter=(c>='A' && c<='Z') || (c>='a' && c<='z');
                if (!letter && (i==0 || (c!='_' && !(c>='0' && c<='9')))) return false;
            }
            return true;
        }
        public static void Track(string actionType, Dictionary<string, object> param = null)
        {
            if (!IsValidActionType(actionType) || actionType=="AD_PLACEMENT_SHOW") return;
#if WEIXINMINIGAME && !UNITY_EDITOR
            if (!IsReady()) return;
            if (param == null)
                WX.CallJSFunction(JsInstanceName, "track", actionType);
            else
                WX.CallJSFunction(JsInstanceName, "track", actionType, param);
#endif
        }

        // ==================== 专用上报方法 ====================

        /// <summary>注册（REGISTER）— 新用户首次打开小游戏时上报。</summary>
        public static void OnRegister()
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            if (!IsReady()) return;
            WX.CallJSFunction(JsInstanceName, "onRegister");
#endif
        }

        /// <summary>小游戏启动（START_APP）— SDK 初始化后立即上报（JS 侧已自动调用）。</summary>
        public static void OnAppStart()
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            if (!IsReady()) return;
            WX.CallJSFunction(JsInstanceName, "onAppStart");
#endif
        }

        /// <summary>应用回到前台（ENTER_FOREGROUND）— 由 WeiXinPlatform.OnAppShow 调用。</summary>
        public static void OnEnterForeground()
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            if (!IsReady()) return;
            WX.CallJSFunction(JsInstanceName, "onEnterForeground");
#endif
        }

        /// <summary>应用进入后台（ENTER_BACKGROUND）— 由 WeiXinPlatform.OnAppHide 调用。</summary>
        public static void OnEnterBackground()
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            if (!IsReady()) return;
            WX.CallJSFunction(JsInstanceName, "onEnterBackground");
#endif
        }

        /// <summary>退出小游戏（APP_QUIT）— 应用退出时上报。</summary>
        public static void OnAppQuit()
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            if (!IsReady()) return;
            WX.CallJSFunction(JsInstanceName, "onAppQuit");
#endif
        }

        /// <summary>付费（PURCHASE）— 用户付费成功时上报。</summary>
        /// <param name="value">付费金额（元）</param>
        public static void OnPurchase(double value)
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            if (!IsReady()) return;
            WX.CallJSFunction(JsInstanceName, "onPurchase", value);
#endif
        }

        /// <summary>完成加载（LOAD_FINISH）— loading 完成、进入游戏第一帧时上报。</summary>
        public static void TrackLoadFinish()
        {
            Track("LOAD_FINISH");
        }

        /// <summary>沉默唤起（RE_ACTIVE）— 老用户沉默后再次打开时上报。</summary>
        /// <param name="backFlowDay">回流周期天数，如 7/14/30</param>
        public static void TrackReActive(int backFlowDay)
        {
            Track("RE_ACTIVE", new Dictionary<string, object> { { "backFlowDay", backFlowDay } });
        }

        /// <summary>分享（SHARE）— 分享成功时上报。</summary>
        /// <param name="target">分享目标："APP_MESSAGE"（转发好友）或 "TIME_LINE"（朋友圈）</param>
        public static void TrackShare(string target)
        {
            Track("SHARE", new Dictionary<string, object> { { "target", target } });
        }

        /// <summary>收藏（ADD_TO_WISHLIST）— 用户收藏小游戏时上报。</summary>
        /// <param name="type">收藏类型："default"（普通收藏）/ "my"（添加到我的小程序）/ "desktop"（添加到桌面）/ "others"</param>
        public static void TrackAddToWishlist(string type)
        {
            Track("ADD_TO_WISHLIST", new Dictionary<string, object> { { "type", type ?? "default" } });
        }

        /// <summary>订阅（SUBSCRIBE）— 订阅成功时上报。</summary>
        public static void TrackSubscribe()
        {
            Track("SUBSCRIBE");
        }

        // ==================== 新手引导 ====================

        /// <summary>新手引导开始（TUTORIAL_START）— 第 1 关新手引导流程触发时上报。</summary>
        public static void TrackTutorialStart()
        {
            Track("TUTORIAL_START");
        }

        /// <summary>完成新手指引（TUTORIAL_FINISH）— 新手教程完成时上报。</summary>
        public static void OnTutorialFinish()
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            if (!IsReady()) return;
            WX.CallJSFunction(JsInstanceName, "onTutorialFinish");
#endif
        }

        // ==================== 关卡事件 ====================

        /// <summary>进入关卡（LEVEL_ENTER）— 关卡场景第一帧渲染时上报。</summary>
        public static void TrackLevelEnter(int levelId, int chapterId, float enterLevelId,
            string enterLevelName, string gameMode, int coinAmount, int staminaValue, int levelValue)
        {
            Track("LEVEL_ENTER", new Dictionary<string, object>
            {
                { "level_id", levelId },
                { "chapter_id", chapterId },
                { "enter_level_id", enterLevelId },
                { "enter_level_name", enterLevelName ?? "" },
                { "game_mode", gameMode ?? "" },
                { "coin_amount", coinAmount },
                { "stamina_value", staminaValue },
                { "level_value", levelValue },
            });
        }

        /// <summary>通过关卡（LEVEL_PASS）— 关卡通关时上报。</summary>
        public static void TrackLevelPass(int levelId, int chapterId, float enterLevelId,
            string gameMode, int adCnt, int duration, int coinAmount, int staminaValue, int levelValue,
            List<DnItemUsage> items = null)
        {
            var param = BuildLevelResultParam(levelId, chapterId, enterLevelId, gameMode, adCnt, duration,
                coinAmount, staminaValue, levelValue, items);
            Track("LEVEL_PASS", param);
        }

        /// <summary>关卡失败（LEVEL_LOSE）— 关卡失败时上报。</summary>
        public static void TrackLevelLose(int levelId, int chapterId, float enterLevelId,
            string gameMode, int adCnt, int duration, int coinAmount, int staminaValue, int levelValue,
            List<DnItemUsage> items = null)
        {
            var param = BuildLevelResultParam(levelId, chapterId, enterLevelId, gameMode, adCnt, duration,
                coinAmount, staminaValue, levelValue, items);
            Track("LEVEL_LOSE", param);
        }

        /// <summary>中途退出关卡（LEVEL_EXIT）— 中途退出关卡时上报。</summary>
        public static void TrackLevelExit(int levelId, int chapterId, float enterLevelId,
            string gameMode, int adCnt, int duration, int coinAmount, int staminaValue, int levelValue,
            List<DnItemUsage> items = null)
        {
            var param = BuildLevelResultParam(levelId, chapterId, enterLevelId, gameMode, adCnt, duration,
                coinAmount, staminaValue, levelValue, items);
            Track("LEVEL_EXIT", param);
        }

        // ==================== 广告事件 ====================

        /// <summary>广告位点击（AD_CLICK）— 玩家点击激励视频广告位按钮时上报。</summary>
        public static void TrackAdClick(int adPlacementName)
        {
            Track("AD_CLICK", new Dictionary<string, object> { { "ad_placement_name", adPlacementName } });
        }

        /// <summary>广告播放结束（AD_VIDEO_FINISH）— 激励视频完整播放完毕时上报。</summary>
        public static void TrackAdVideoFinish(int adPlacementName)
        {
            Track("AD_VIDEO_FINISH", new Dictionary<string, object> { { "ad_placement_name", adPlacementName } });
        }

        /// <summary>广告曝光（AD_IMPRESSION）— 插屏/banner 等非激励广告曝光时上报。</summary>
        /// <param name="adType">广告位类型：1=激励视频, 2=插屏, 3=格子, 4=banner</param>
        public static void TrackAdImpression(int adType)
        {
            Track("AD_IMPRESSION", new Dictionary<string, object> { { "ad_type", adType } });
        }

        // ==================== 内部工具 ====================

        /// <summary>检查 JS 侧 SDK 是否已初始化（GameGlobal.dnSDK 非空）。</summary>
        private static bool IsReady()
        {
#if WEIXINMINIGAME && !UNITY_EDITOR
            if (!AppConst.EnableDnSDK) return false;
            // 通过 dnSDKReady.check() 安全检查，避免对 null 实例调用方法导致 JS 异常。
            // 返回 "1" 表示已初始化，"0" 表示未初始化。
            string result = WX.CallJSFunctionWithReturn("dnSDKReady", "check");
            return result == "1";
#else
            return false;
#endif
        }

        /// <summary>构建关卡结果（PASS/LOSE/EXIT）的公共参数。</summary>
        private static Dictionary<string, object> BuildLevelResultParam(
            int levelId, int chapterId, float enterLevelId, string gameMode,
            int adCnt, int duration, int coinAmount, int staminaValue, int levelValue,
            List<DnItemUsage> items)
        {
            var param = new Dictionary<string, object>
            {
                { "level_id", levelId },
                { "chapter_id", chapterId },
                { "enter_level_id", enterLevelId },
                { "game_mode", gameMode ?? "" },
                { "ad_cnt", adCnt },
                { "duration", duration },
                { "coin_amount", coinAmount },
                { "stamina_value", staminaValue },
                { "level_value", levelValue },
            };
            // IAA 文档要求 items 为 string_array 格式，每个元素为 "{item_type:N,item_num:N}" 字符串。
            // 字段为关闭（强制必填），无道具时传空数组。
            var itemStrings = new List<string>();
            if (items != null)
            {
                foreach (var item in items)
                {
                    itemStrings.Add(item.ToString());
                }
            }
            param["items"] = itemStrings;
            return param;
        }
    }
}
