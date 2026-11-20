using System;
using UnityEngine;

// TODO: DefaultPlatform 方法体未反编译（b1bb84-b1bda0 未在反编译集内），按 non-core 安全空实现
public class DefaultPlatform : PlatformBase
{
    public override void Share(string title, string imageUrl, Action<bool> callback)
    {
        // 无分享 SDK 的平台（编辑器 / 非抖音构建）：直接回调成功，
        // 保证 ReviveDlg 等依赖分享成功才能继续的逻辑可用（与 ShowAd 直接下发奖励一致）
        callback?.Invoke(true);
    }

    public override void ShowAd(string adId, Action<RewardedAdResult> callback, Action onShow = null)
    {
        // 无广告 SDK 的平台（编辑器 / 非抖音构建）：直接下发奖励，保证道具/复活等功能可用
        onShow?.Invoke();
        callback?.Invoke(new RewardedAdResult { Type = RewardedAdResultType.Success });
    }

    public override string GetPlatformName()
    {
        return "default";
    }

    public override bool IsFeatureSupported(string featureId)
    {
        return false;
    }

    public override void GetSystemInfo()
    {
    }

    public override void goSideBar()
    {
    }

    public override void vibrate(int vibrateType = 1)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // 还原补实现：原方法体未反编译（原版应是小游戏平台 SDK 震动，vibrateType 对应短/长震），
        // APK 无该平台，用 Unity 内置震动顶替（固定时长，不区分 vibrateType；构建时 Unity 自动加 VIBRATE 权限）
        Handheld.Vibrate();
#endif
    }

    public override void getEnterOptions(System.Action<PlatformBase.LaunchOptionsData> callback)
    {
        // 非目标平台：回调空参数，保证调用方逻辑可继续
        callback?.Invoke(new PlatformBase.LaunchOptionsData());
    }

    public override void setUserRecord(int score)
    {
    }

    public override void AddShortcut(Action<bool> callback)
    {
        callback?.Invoke(false);
    }
}
