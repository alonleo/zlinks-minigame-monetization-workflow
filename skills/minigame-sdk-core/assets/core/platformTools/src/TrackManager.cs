using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Service;

// Persistent, single-flight telemetry queue. Platform reporting does not depend on login.
public class TrackManager
{
    public static TrackManager Instance { get; private set; } = new TrackManager();
    public static bool HasInstance => Instance != null;
    public long Session { get; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public string ReportUrl = AppConst.BaseUrl + "/v1/track";
    public string AscribeBaseUrl = AppConst.BaseUrl;
    public string AppId = AppConst.AppId;
    public int MaxPersistEvents = 500;
    public float SaveInterval = 5f;
    public string OpenId { get; private set; }
    public string UnionId { get; private set; }
    public int PendingCount => queue.Count;
    private readonly List<TrackEvent> queue = new List<TrackEvent>();
    private PlatformBase platform;
    private TrackManagerRunner runner;
    private bool initialized, identityStarted, launchReported, activeQueued, loginBusy, sendBusy, disposed, dirty;
    private bool launchRequestBusy, launchAccepted;
    private Dictionary<string,object> launchParameters;
    private bool heartbeatLogged, customCompletionNotified;
    private bool completedLaunch, completedActive, completedTutorialStep, completedRewardedVideo;
    private int loginGeneration, failures, launchFailures;
    private double nextAttempt, loginDeadline, nextSave, nextDnAttempt, nextLaunchAttempt;
    private PlatformBase.LaunchOptionsData _cachedLaunchOptions;
    private Dictionary<string,object> _cachedLaunchParameters = new Dictionary<string,object>();
    private PlatformBase.SystemInfoData _cachedSystemInfo;
    private Dictionary<string,string> _cachedAdParams = new Dictionary<string,string>();
    private string _cachedLaunchScene="", _cachedLaunchFrom="", _cachedLocation="", launchId="";
    private string Key(string suffix) { return "mgk:telemetry:"+AscribeBaseUrl+":"+AppId+":"+suffix; }
    private TrackManager() { }
    public void Init(string reportUrl)
    {
        ReportUrl=reportUrl;
        if(initialized || disposed) return;
        initialized=true;
        if(AppConst.EnableCustomReport) LoadQueue();
        EnsureRunner();
    }
    public void InitPlatform(PlatformBase value)
    {
        if(disposed) return;
        platform=value; Init(ReportUrl);
        if(launchReported) return;
        launchReported=true;
        var first=Key("first_open");
        if(MiniGamePrefs.GetInt(first,0)==0)
        {
            Startup("app_first_open"); MiniGamePrefs.SetInt(first,1); MiniGamePrefs.Save();
        }
        Startup("app_launch");
    }
    private void Startup(string name)
    {
        if(AppConst.EnablePlatformReport) { try { platform?.ReportEvent(name,null); } catch {} }
        Track(name);
    }
    private void EnsureRunner()
    {
        if(runner!=null || disposed) return;
        var go=new GameObject("[TrackManagerRunner]"); UnityEngine.Object.DontDestroyOnLoad(go);
        runner=go.AddComponent<TrackManagerRunner>();
        runner.SetTickAction(_ => Tick());
        runner.SetPauseAction(paused => { if(paused) SaveQueue(); });
        runner.SetQuitAction(SaveQueue);
    }
    public void InitAscribe(PlatformBase value=null)
    {
        if(disposed || identityStarted || (!AppConst.EnableCustomReport && !AppConst.EnableDnSDK)) return;
        platform=value ?? PlatformManager.Instance.CurrentPlatform;
        if(platform==null || string.IsNullOrWhiteSpace(AppId) || string.IsNullOrWhiteSpace(AscribeBaseUrl)) return;
        identityStarted=true; Init(ReportUrl);
        OpenId=MiniGamePrefs.GetString(Key("open_id"),""); UnionId=MiniGamePrefs.GetString(Key("union_id"),"");
        platform.getEnterOptions(options => {
            if(disposed || options==null) return;
            if(options.IsColdStart) {
                _cachedLaunchOptions=options;
                _cachedLaunchParameters=options.ToReportProperties();
                _cachedSystemInfo=platform.LastSystemInfo;
            }
            // Hot-start callbacks refresh local attribution state only. They must
            // never create another /v1/active event.
            _cachedAdParams.Clear();
            foreach(var kv in ParseAdParams(options)) _cachedAdParams[kv.Key]=kv.Value;
            _cachedLaunchScene=options.Scene ?? "";
            _cachedLaunchFrom=options.LaunchFrom ?? "";
            _cachedLocation=options.Location ?? "";
            if(options.IsColdStart) { BeginLaunch(); QueueActive(); }
        });
        if(!string.IsNullOrEmpty(OpenId)) IdentityReady();
        Tick();
    }
    private void BeginLaunch()
    {
        if(disposed || launchRequestBusy || launchAccepted || !AppConst.EnableCustomReport || _cachedLaunchOptions==null) return;
        // Freeze complete cold-start parameters once; retries must not pick up hot-start changes.
        if(launchParameters==null)
            launchParameters=JsonConvert.DeserializeObject<Dictionary<string,object>>(JsonConvert.SerializeObject(_cachedLaunchParameters));
        launchRequestBusy=true;
        launchId=ResolveLaunchId();
        foreach(var item in queue)
            if(item!=null && item.path=="/v1/active" && item.payload!=null) item.payload["launchId"]=launchId;
        var payload=new Dictionary<string,object>(launchParameters);
        payload["appId"]=AppId; payload["launchId"]=launchId;
        HttpManager.Instance.SendPostOwned(this,AscribeBaseUrl+"/v1/launch",JsonConvert.SerializeObject(payload),(ok,response)=>{
            if(disposed) return;
            launchRequestBusy=false;
            bool accepted=IsBusinessSuccess(ok,response);
            DebugServerResult("/v1/launch",payload,accepted);
            if(accepted) {
                launchAccepted=true; launchFailures=0; nextLaunchAttempt=0;
                ObserveCustomCompletion("/v1/launch",payload);
            } else {
                nextLaunchAttempt=Time.realtimeSinceStartupAsDouble+Math.Min(60,2*Math.Pow(2,Math.Min(launchFailures++,5)));
            }
        });
    }
    private string ResolveLaunchId()
    {
        string cached=MiniGamePrefs.GetString(Key("launch_id"),"");
        if(System.Text.RegularExpressions.Regex.IsMatch(cached ?? "", "^[0-9]{13,17}[0-9a-f]{32}$")) return cached;
        string timestamp=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        string value=timestamp+Guid.NewGuid().ToString("N").ToLowerInvariant();
        MiniGamePrefs.SetString(Key("launch_id"),value); MiniGamePrefs.Save();
        return value;
    }
    private void IdentityReady()
    {
        if(disposed || string.IsNullOrWhiteSpace(OpenId)) return;
        failures=0; nextAttempt=0; loginBusy=false;
        if(AppConst.EnableDnSDK)
        {
            DnSDKBridge.SetIdentity(OpenId,UnionId);
            if(MiniGamePrefs.GetInt(Key("dn_registered"),0)==0 && DnSDKBridge.TryRegister())
            { MiniGamePrefs.SetInt(Key("dn_registered"),1); MiniGamePrefs.Save(); }
        }
        QueueActive(); Flush();
    }
    private void QueueActive()
    {
        if(activeQueued || !AppConst.EnableCustomReport || _cachedLaunchOptions==null) return;
        activeQueued=true;
        Enqueue(new TrackEvent{path="/v1/active",payload=BuildActivePayload(_cachedAdParams)});
    }
    private void Retry()
    {
        nextAttempt=Time.realtimeSinceStartupAsDouble+Math.Min(60,2*Math.Pow(2,Math.Min(failures++,5)));
    }
    private void BeginLogin()
    {
        loginBusy=true; int generation=++loginGeneration;
        loginDeadline=Time.realtimeSinceStartupAsDouble+15;
        bool callbackReceived=false;
        try { platform.Login((code,anonymousCode) => {
            if(disposed || generation!=loginGeneration || callbackReceived) return;
            callbackReceived=true;
            if(string.IsNullOrWhiteSpace(code)) { loginBusy=false; Retry(); return; }
            var p=new Dictionary<string,object>{{"appId",AppId},{"code",code},{"session",Session}};
            if(!string.IsNullOrEmpty(anonymousCode)) p["anonymousCode"]=anonymousCode;
            HttpManager.Instance.SendPostOwned(this,AscribeBaseUrl+"/v1/code2session",JsonConvert.SerializeObject(p),(ok,response)=>{
                if(disposed || generation!=loginGeneration) return;
                loginBusy=false;
                try {
                    var data=ok ? JObject.Parse(response) : null;
                    string id=data==null ? null : (string)data["data"]?["openId"];
                    bool accepted=data!=null && (int?)data["code"]==0 && !string.IsNullOrWhiteSpace(id);
                    DebugServerResult("/v1/code2session",p,accepted);
                    if(!accepted) { Retry(); return; }
                    OpenId=id; UnionId=(string)data["data"]?["unionId"] ?? "";
                    MiniGamePrefs.SetString(Key("open_id"),OpenId); MiniGamePrefs.SetString(Key("union_id"),UnionId); MiniGamePrefs.Save();
                    IdentityReady();
                } catch { DebugServerResult("/v1/code2session",p,false); Retry(); }
            });
        }); } catch { loginBusy=false; Retry(); }
    }
    public void Track(string eventName, Dictionary<string,object> eventValue=null)
    {
        if(disposed || !AppConst.EnableCustomReport || !PlatformManager.IsValidEventName(eventName)) return;
        Init(ReportUrl);
        // Deep snapshot: later inventory mutations cannot modify queued events.
        var p=eventValue==null ? new Dictionary<string,object>() : JsonConvert.DeserializeObject<Dictionary<string,object>>(JsonConvert.SerializeObject(eventValue));
        p["session"]=Session;
        Enqueue(new TrackEvent{path="/v1/track",eventName=eventName,timestamp=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),eventValue=p});
    }
    private void Enqueue(TrackEvent item)
    {
        queue.Add(item);
        int limit=Math.Max(1,Math.Min(500,MaxPersistEvents));
        while(queue.Count>limit) queue.RemoveAt(sendBusy && queue.Count>1 ? 1 : 0);
        dirty=true; Flush();
    }
    public void ReportExposure()
    {
        if(disposed || !AppConst.EnableCustomReport || platform==null || platform.GetPlatformName()=="weixin") return;
        Enqueue(new TrackEvent{path="/v1/exposure",payload=new Dictionary<string,object>{{"session",Session}}});
    }
    public void Flush()
    {
        if(disposed || !AppConst.EnableCustomReport || sendBusy || string.IsNullOrEmpty(OpenId) || queue.Count==0 || Time.realtimeSinceStartupAsDouble<nextAttempt) return;
        var item=queue[0];
        var p=item.path=="/v1/track" ? new Dictionary<string,object>{{"eventName",item.eventName},{"eventValue",item.eventValue},{"timestamp",item.timestamp},{"clickid",_cachedAdParams.ContainsKey("clickid") ? _cachedAdParams["clickid"] : ""}} : new Dictionary<string,object>(item.payload);
        p["appId"]=AppId; p["openId"]=OpenId; p["unionId"]=UnionId ?? "";
        sendBusy=true;
        string url=item.path=="/v1/track" ? ReportUrl : AscribeBaseUrl+item.path;
        HttpManager.Instance.SendPostOwned(this,url,JsonConvert.SerializeObject(p),(ok,response)=>{
            if(disposed) return;
            sendBusy=false;
            bool accepted=IsBusinessSuccess(ok,response);
            bool heartbeat=item.path=="/v1/track" && item.eventName=="player_is_gaming";
            if(heartbeat) {
                if(accepted && !heartbeatLogged) { heartbeatLogged=true; DebugTelemetry("#a855f7","心跳首条",new Dictionary<string,object>{{"success",true},{"channel","heartbeat"},{"path",item.path},{"data",p}}); }
            } else DebugServerResult(item.path,p,accepted);
            if(accepted) { ObserveCustomCompletion(item.path,p); queue.Remove(item); dirty=true; failures=0; nextAttempt=0; SaveQueue(); }
            else Retry();
        });
    }
    private static bool IsBusinessSuccess(bool transportOk,string response)
    {
        if(!transportOk) return false;
        try { return (int?)JObject.Parse(response)["code"]==0; } catch { return false; }
    }
    private static bool ValidText(JToken value) { return value!=null && value.Type==JTokenType.String && !string.IsNullOrWhiteSpace((string)value); }
    private static bool ValidChineseText(JToken value)
    {
        if(!ValidText(value)) return false;
        foreach(char c in (string)value) if(c>='\u3400' && c<='\u9fff') return true;
        return false;
    }
    private static bool ValidInteger(JToken value,int minimum=0)
    {
        if(value==null || value.Type!=JTokenType.Integer) return false;
        try { return value.Value<long>()>=minimum; } catch { return false; }
    }
    private void ObserveCustomCompletion(string path,object sent)
    {
        JObject data;
        try { data=JObject.FromObject(sent); } catch { return; }
        if(path=="/v1/launch") completedLaunch=ValidText(data["appId"]) && System.Text.RegularExpressions.Regex.IsMatch((string)data["launchId"] ?? "","^[0-9]{13,17}[0-9a-f]{32}$");
        else if(path=="/v1/active") completedActive=ValidText(data["appId"]) && ValidText(data["openId"]) && ValidInteger(data["session"]) && data["isColdStart"]?.Value<bool>()==true;
        else if(path=="/v1/track")
        {
            string eventName=(string)data["eventName"];
            var value=data["eventValue"] as JObject;
            if(eventName=="tutorial_step" && value!=null)
                completedTutorialStep=ValidInteger(value["session"]) && ValidInteger(value["step"],1) && ValidChineseText(value["step_name"]);
            if(value!=null && (eventName=="ad_reward_click" || eventName=="ad_reward_show" || eventName=="ad_reward_success" || eventName=="ad_reward_close" || eventName=="ad_reward_error"))
                completedRewardedVideo=ValidInteger(value["session"]) && ValidInteger(value["ad_placement_name"],1) && ValidChineseText(value["prop_name"]) && ValidInteger(value["当前关卡"]);
        }
        if(!customCompletionNotified && completedLaunch && completedActive && completedTutorialStep && completedRewardedVideo)
        {
            customCompletionNotified=true;
            DebugTelemetry("#2563eb","✅ 自定义上报已完成",new Dictionary<string,object>{{"success",true},{"channel","server"},{"required",new Dictionary<string,object>{{"启动",true},{"激活",true},{"TUTORIAL_STEP",true},{"任意激励视频事件",true}}},{"note","本机 HTTP 与业务 code=0、必填字段检查通过；后台归因、实际入库和质量看板仍需渠道验收"}});
        }
    }
    private void DebugServerResult(string path,object data,bool success)
    {
        DebugTelemetry("#2563eb",success ? "发送成功" : "发送失败待重试",new Dictionary<string,object>{{"success",success},{"channel","server"},{"path",path},{"data",data}});
    }
    private static void DebugTelemetry(string color,string label,object data)
    {
        if(!AppConst.TelemetryDebug) return;
        Debug.Log("<color="+color+">[TrackManager]["+label+"] "+SanitizedJson(data)+"</color>");
    }
    private static string SanitizedJson(object data)
    {
        JToken token;
        try { token=data==null ? new JObject() : JToken.FromObject(data); } catch { return "{}"; }
        Redact(token);
        return token.ToString(Formatting.None);
    }
    private static void Redact(JToken token)
    {
        var obj=token as JObject;
        if(obj!=null) foreach(var property in new List<JProperty>(obj.Properties()))
        {
            string key=property.Name.ToLowerInvariant();
            if(key=="code" || key=="openid" || key=="unionid" || key=="clickid" || key=="secret_key" || key=="secretkey") property.Value="[REDACTED]";
            else Redact(property.Value);
        }
        var array=token as JArray;
        if(array!=null) foreach(var child in array) Redact(child);
    }
    private void Tick()
    {
        if(disposed) return;
        double now=Time.realtimeSinceStartupAsDouble;
        if(dirty && now>=nextSave) { SaveQueue(); nextSave=now+Math.Max(.5,SaveInterval); }
        if(identityStarted && AppConst.EnableCustomReport && !launchAccepted && !launchRequestBusy && now>=nextLaunchAttempt)
            BeginLaunch();
        if(identityStarted && string.IsNullOrEmpty(OpenId))
        {
            if(loginBusy && now>=loginDeadline) { loginGeneration++; loginBusy=false; Retry(); }
            if(!loginBusy && now>=nextAttempt) BeginLogin();
        }
        if(AppConst.EnableDnSDK && !string.IsNullOrEmpty(OpenId) && MiniGamePrefs.GetInt(Key("dn_registered"),0)==0 && now>=nextDnAttempt)
        { nextDnAttempt=now+30; IdentityReady(); }
        Flush();
    }
    private void SaveQueue()
    {
        if(!dirty || !AppConst.EnableCustomReport) return;
        try { MiniGamePrefs.SetString(Key("queue"),JsonConvert.SerializeObject(queue)); MiniGamePrefs.Save(); dirty=false; }
        catch { Debug.LogWarning("[MiniGameKit] 统计队列保存失败"); }
    }
    private void LoadQueue()
    {
        try {
            var saved=JsonConvert.DeserializeObject<List<TrackEvent>>(MiniGamePrefs.GetString(Key("queue"),"[]"));
            if(saved==null) return;
            foreach(var item in saved)
                if(item!=null && ((item.path=="/v1/track" && item.eventValue!=null) || (item.path=="/v1/active" && item.payload!=null) || (item.path=="/v1/exposure" && item.payload!=null && !AppConst.IsWeixinPlatform))) queue.Add(item);
            if(queue.Count>500) queue.RemoveRange(0,queue.Count-500);
        } catch { Debug.LogWarning("[MiniGameKit] 统计队列读取失败"); }
    }
    public void Clear() { queue.Clear(); MiniGamePrefs.DeleteKey(Key("queue")); MiniGamePrefs.Save(); dirty=false; nextSave=0; }
    public void Dispose()
    {
        SaveQueue(); disposed=true; loginGeneration++;
        HttpManager.Instance.CancelOwner(this);
        if(runner!=null) { UnityEngine.Object.Destroy(runner.gameObject); runner=null; }
    }
    private Dictionary<string, string> ParseAdParams(PlatformBase.LaunchOptionsData options)
    {
        var result = new Dictionary<string, string>();

        if (options?.Query == null || options.Query.Count == 0)
            return result;

        // query 第一层字段原样透传
        foreach (var kv in options.Query)
        {
            if (!string.IsNullOrEmpty(kv.Key))
            {
                result[kv.Key] = kv.Value ?? "";
            }
        }

        return result;
    }

    /// <summary>将启动参数 + 固定字段 + 系统信息组装为 /v1/active 请求体</summary>
    private Dictionary<string, object> BuildActivePayload(Dictionary<string, string> launchParams)
    {
        // Begin with the complete raw launch snapshot so future platform fields are
        // forwarded automatically. Trusted SDK/identity fields overwrite conflicts.
        var payload = new Dictionary<string, object>(_cachedLaunchParameters);
        payload["appId"] = AppId;
        payload["openId"] = OpenId ?? "";
        payload["unionId"] = UnionId ?? "";
        payload["isColdStart"] = _cachedLaunchOptions?.IsColdStart ?? false;
        payload["scene"] = _cachedLaunchScene;
        payload["path"] = _cachedLaunchOptions?.Path ?? "";
        payload["launchFrom"] = _cachedLaunchFrom;
        payload["location"] = _cachedLocation;
        payload["session"] = Session;
        payload["launchId"] = launchId;

        // 系统信息（InitAscribe 冷启动时获取，随激活一起上传）
        if (_cachedSystemInfo != null)
        {
            payload["brand"] = _cachedSystemInfo.Brand ?? "";
            payload["model"] = _cachedSystemInfo.Model ?? "";
            payload["system"] = _cachedSystemInfo.System ?? "";
            payload["platform"] = _cachedSystemInfo.Platform ?? "";
            payload["SDKVersion"] = _cachedSystemInfo.SDKVersion ?? "";
            payload["hostVersion"] = _cachedSystemInfo.HostVersion ?? "";
            payload["hostName"] = _cachedSystemInfo.HostName ?? "";
            payload["screenWidth"] = _cachedSystemInfo.ScreenWidth;
            payload["screenHeight"] = _cachedSystemInfo.ScreenHeight;
            payload["pixelRatio"] = _cachedSystemInfo.PixelRatio;
            payload["statusBarHeight"] = _cachedSystemInfo.StatusBarHeight;
            payload["language"] = _cachedSystemInfo.Language ?? "";
            payload["deviceScore"] = _cachedSystemInfo.DeviceScore;
            payload["deviceOrientation"] = _cachedSystemInfo.DeviceOrientation ?? "";
            // wx.getDeviceInfo() 补充字段
            payload["abi"] = _cachedSystemInfo.Abi ?? "";
            payload["deviceAbi"] = _cachedSystemInfo.DeviceAbi ?? "";
            payload["cpuType"] = _cachedSystemInfo.CpuType ?? "";
            payload["memorySize"] = _cachedSystemInfo.MemorySize ?? "";
        }

        // query 第一层字段原样透传，字符串值尝试转为数字/布尔以匹配服务端期望类型
        foreach (var kv in launchParams)
        {
            if (!payload.ContainsKey(kv.Key)) payload[kv.Key] = ConvertValue(kv.Value);
        }

        // Launch query fields must not override the session.
        payload["session"] = Session;
        return payload;
    }

    /// <summary>尝试将字符串转为 bool/long/double，无法转换则保留原字符串。</summary>
    private static object ConvertValue(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        // bool
        if (s == "true") return true;
        if (s == "false") return false;

        // long（整数，含 16 位长 ID，避免 double 精度丢失）
        // 跳过 "0" 开头且长度>1 的字符串（如 "025001"），可能是编码值不能丢前导零
        if (long.TryParse(s, out var l) && !(s[0] == '0' && s.Length > 1))
            return l;

        // double（浮点数）
        if (double.TryParse(s, out var d))
            return d;

        return s;
    }



}
[Serializable]
public class TrackEvent
{
    public string eventName;
    public long timestamp;
    public Dictionary<string,object> eventValue;
    public string path;
    public Dictionary<string,object> payload;
}
public class TrackManagerRunner : MonoBehaviour
{
    private Action<float> tick; private Action<bool> pause; private Action quit;
    public void SetTickAction(Action<float> value) { tick=value; }
    public void SetPauseAction(Action<bool> value) { pause=value; }
    public void SetQuitAction(Action value) { quit=value; }
    private void Update() { tick?.Invoke(Time.unscaledDeltaTime); }
    private void OnApplicationPause(bool value) { pause?.Invoke(value); }
    private void OnApplicationQuit() { quit?.Invoke(); }
}
