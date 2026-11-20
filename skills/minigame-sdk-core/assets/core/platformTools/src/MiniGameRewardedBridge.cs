using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Newtonsoft.Json;

// Shared native WX/TT video bridge. Installed once, independent of vendor C# ad versions.
public sealed class MiniGameRewardedBridge : MonoBehaviour
{
    private static MiniGameRewardedBridge instance;
    private Action<PlatformBase.RewardedAdResult> completed;
    private Action shown;
    private bool exposure;
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void MGK_RewardPrepare(string channel, string id, string receiver);
    [DllImport("__Internal")] private static extern void MGK_RewardPreload(string channel,string id,string receiver);
    [DllImport("__Internal")] private static extern void MGK_RewardShow(string channel, string id, string receiver);
    [DllImport("__Internal")] private static extern void MGK_RewardDispose();
#endif
    private static MiniGameRewardedBridge Ensure()
    {
        if (instance != null) return instance;
        var go = new GameObject("MiniGameKit.RewardedBridge");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MiniGameRewardedBridge>();
        return instance;
    }
    public static void Prepare(string channel, string id)
    {
        if (AppConst.isNoAd || string.IsNullOrWhiteSpace(id)) return;
#if UNITY_WEBGL && !UNITY_EDITOR
        var bridge = Ensure();
        MGK_RewardPrepare(channel, id, bridge.gameObject.name);
#endif
    }
    public static void Preload(string channel,string id) {
#if UNITY_WEBGL && !UNITY_EDITOR
        var bridge=Ensure();MGK_RewardPreload(channel,id,bridge.gameObject.name);
#else
        PlatformManager.Instance.NotifyAdPreload("reward",id,false,-5,"preload_unsupported");
#endif
    }
    public static void Show(string channel, string id, Action<PlatformBase.RewardedAdResult> callback, Action onShow)
    {
        var bridge = Ensure();
        if (bridge.completed != null)
        {
            callback?.Invoke(new PlatformBase.RewardedAdResult { Type=PlatformBase.RewardedAdResultType.Error, ErrorCode=-3, ErrorMessage="already_showing" });
            return;
        }
        bridge.completed = callback ?? (_ => {});
        bridge.shown = onShow; bridge.exposure = false;
#if UNITY_WEBGL && !UNITY_EDITOR
        try { MGK_RewardShow(channel,id,bridge.gameObject.name); }
        catch { bridge.OnAdEvent("{\"type\":\"error\",\"code\":-5,\"message\":\"bridge_unavailable\"}"); }
#else
        bridge.OnAdEvent("{\"type\":\"error\",\"code\":-5,\"message\":\"unsupported\"}");
#endif
    }
    [Serializable] private sealed class AdEvent { public string type,id; public int code; public string message; }
    [UnityEngine.Scripting.Preserve]
    public void OnAdEvent(string json)
    {
        var value = JsonConvert.DeserializeObject<AdEvent>(json);
        if (value == null) return;
        if(value.type=="preload" || value.type=="preload_error") { PlatformManager.Instance.NotifyAdPreload("reward",value.id,value.type=="preload",value.code,value.message);return; }
        if (completed == null) return;
        if (value.type == "opening") { PlatformManager.Instance.NotifyRewardVideoOpening(); return; }
        if (value.type == "show")
        {
            if (!exposure) { exposure=true; shown?.Invoke(); }
            return;
        }
        var callback=completed; completed=null; shown=null;
        callback(new PlatformBase.RewardedAdResult {
            Type=value.type=="complete" ? PlatformBase.RewardedAdResultType.Success : value.type=="cancel" ? PlatformBase.RewardedAdResultType.Closed : PlatformBase.RewardedAdResultType.Error,
            ErrorCode=value.code, ErrorMessage=value.message
        });
    }
    public static void DisposeInstance()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        MGK_RewardDispose();
#endif
        if (instance == null) return;
        instance.completed=null; instance.shown=null;
        Destroy(instance.gameObject); instance=null;
    }
}
