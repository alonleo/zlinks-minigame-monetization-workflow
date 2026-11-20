using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Runtime.InteropServices;
using UnityEngine;

// Receives the host's successful show result, which the TTSDK C# wrappers omit.
public sealed class MiniGameDisplayAdBridge : MonoBehaviour
{
    private static MiniGameDisplayAdBridge instance;
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void MGK_DisplayAdPrepare(string kind, string id, string receiver, string channel);
    [DllImport("__Internal")] private static extern void MGK_DisplayAdShow(string kind, string id, string receiver, string channel);
    [DllImport("__Internal")] private static extern void MGK_DisplayAdHideBanner();
    [DllImport("__Internal")] private static extern void MGK_DisplayAdDispose(string kind);
#endif

    public static void Prepare(string kind,string id,string channel)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if(instance==null) { var go=new GameObject("MiniGameKit.DisplayAdBridge");DontDestroyOnLoad(go);instance=go.AddComponent<MiniGameDisplayAdBridge>(); }
        MGK_DisplayAdPrepare(kind,id,instance.gameObject.name,channel);
#endif
    }
    public static void Show(string kind, string id, string channel = "douyin")
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (instance == null)
        {
            var go = new GameObject("MiniGameKit.DisplayAdBridge");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MiniGameDisplayAdBridge>();
        }
        MGK_DisplayAdShow(kind, id, instance.gameObject.name, channel);
#endif
    }

    public static void HideBanner()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        MGK_DisplayAdHideBanner();
#endif
    }

    public static void Dispose(string kind)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        MGK_DisplayAdDispose(kind);
#endif
    }

    // One request owns one load/show and at most one terminal error/close.
    public sealed class Request
    {
        private readonly string kind;
        public bool Active { get; private set; } = true;
        private bool shown,requested;
        private readonly string id;
        public bool Ready { get; private set; }
        public Request(string kind) { this.kind=kind;requested=true;Report("load"); }
        public Request(string kind,string id) { this.kind=kind;this.id=id; }
        public void Loaded() { if(!Active || Ready)return;Ready=true;PlatformManager.Instance.NotifyAdPreload(kind,id,true); }
        public bool BeginShow() { if(!Active || !Ready || requested)return false;requested=true;shown=false;return true; }
        public void Hide() { requested=false;shown=false; }
        private void Report(string stage, Dictionary<string,object> fields=null) { PlatformManager.Instance.ReportEvent("ad_"+kind+"_"+stage,fields); }
        public void Show() { if(!Active || shown || !requested) return; shown=true; Report("show"); }
        public void Error(int code=-1,string message="ad_failed") {
            if(!Active) return; Active=false;
            if(id!=null && !requested) { PlatformManager.Instance.NotifyAdPreload(kind,id,false,code,message);return; }
            PlatformManager.Instance.FinishAdResource(kind,id,true);
            Report("error",new Dictionary<string,object>{{"code",code},{"message",message ?? ""}});
        }
        public void Close(bool confirmsShow=false) {
            if(!Active || !requested) return; if(confirmsShow) Show(); Active=false;PlatformManager.Instance.FinishAdResource(kind,id);Report("close");
        }
        public void Cancel() { Active=false; }
    }
    [Serializable] private sealed class DisplayEvent { public string kind, id, stage, message, phase; public int code; }
    [UnityEngine.Scripting.Preserve]
    public void OnDisplayAdEvent(string json)
    {
        var e=JsonConvert.DeserializeObject<DisplayEvent>(json);
        if(e==null || (e.kind!="banner" && e.kind!="interstitial")
            || (e.stage!="preload" && e.stage!="load" && e.stage!="show" && e.stage!="error" && e.stage!="close")) return;
        if(e.stage=="preload") { PlatformManager.Instance.NotifyAdPreload(e.kind,e.id,true);return; }
        if(e.stage=="error" && e.phase=="preload") { PlatformManager.Instance.NotifyAdPreload(e.kind,e.id,false,e.code,e.message);return; }
        if(e.stage=="close" || e.stage=="error") PlatformManager.Instance.FinishAdResource(e.kind,e.id,e.stage=="error");
        Dictionary<string,object> fields=e.stage=="error"
            ? new Dictionary<string,object>{{"code",e.code},{"message",e.message ?? ""}} : null;
        PlatformManager.Instance.ReportEvent("ad_"+e.kind+"_"+e.stage,fields);
    }
    [UnityEngine.Scripting.Preserve]
    public void OnDisplayAdShown(string kind)
    {
        if (kind == "banner") PlatformManager.Instance.ReportEvent("ad_banner_show");
        else if (kind == "interstitial") PlatformManager.Instance.ReportEvent("ad_interstitial_show");
    }
}
