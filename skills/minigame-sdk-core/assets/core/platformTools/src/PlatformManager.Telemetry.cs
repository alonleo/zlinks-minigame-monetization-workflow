using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Service;

public partial class PlatformManager
{
    // Business integration supplies real values; no project save schema is hard-coded.
    public Func<Dictionary<string, object>> RemainingItemsProvider;
    public Func<int, int> BusinessLevelMapper = value => value;
    // Resolve legacy placement keys to the reward actually visible at click time.
    public Func<string, string> RewardNameResolver;
    public static bool IsValidEventName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 64) return false;
        foreach (char c in name) if (char.IsControl(c)) return false;
        return true;
    }
    private static bool IsChineseDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        bool chinese = false;
        foreach (char c in value) {
            if (char.IsControl(c)) return false;
            if (c >= '\u3400' && c <= '\u9fff') chinese = true;
        }
        return chinese;
    }
    private string ResolveRewardName(string value)
    {
        if (IsChineseDisplayName(value)) return value.Trim();
        string resolved = null;
        try { if (RewardNameResolver != null) resolved = RewardNameResolver(value); }
        catch { Debug.LogWarning("[MiniGameKit] 道具中文名解析失败"); }
        if (IsChineseDisplayName(resolved)) return resolved.Trim();
        Debug.LogWarning("[MiniGameKit] 广告入口缺少实际中文奖励名，请接入点击时名称；暂记未知");
        return "未知";
    }
    public bool FirstLevelIsTutorial { get; set; }
    private int currentLevel, adCount;
    private bool levelActive, levelResultSent;
    private double levelStarted;
    private string currentMode = "level";
    private readonly HashSet<string> visiblePages = new HashSet<string>();
    private string TutorialKey(string phase) { return "mgk:tutorial:" + AppConst.AppId + ":" + phase; }
    private double ActiveSeconds { get { return _gameplayElapsedSeconds + (IsGameplayActive ? Math.Max(0, Time.realtimeSinceStartupAsDouble - _gameplaySegmentStartedAt) : 0); } }

    public void ReportUiShown(string page) { if (visiblePages.Add(page)) ReportEvent("ui_"+page+"_show"); }
    public void ReportUiClosed(string page) { if (visiblePages.Remove(page)) ReportEvent("ui_"+page+"_close"); }
    public void ReportTutorialStart(string stepName = "新手引导_开始")
    {
        if (MiniGamePrefs.GetInt(TutorialKey("start"),0)!=0) return;
        stepName=IsChineseDisplayName(stepName) ? stepName.Trim() : "新手引导_开始";
        if(stepName.Length>64) stepName=stepName.Substring(0,64);
        if (ReportEventCore("tutorial_start",null)) ReportTutorialStep(1,stepName);
    }
    public void ReportTutorialStep(int step, string stepName)
    {
        ReportEvent("tutorial_step",new Dictionary<string,object>{{"step",step},{"step_name",stepName}});
    }
    public void ReportTutorialFinish() { ReportEvent("tutorial_finish"); }
    public void ReportLevelStart(int level, string mode = "level") { ReportEvent("level_start", new Dictionary<string,object>{{"level",level},{"mode",mode}}); }
    public void ReportLevelResult(string result, float progress = 1f)
    {
        if (result!="success" && result!="fail" && result!="exit") throw new ArgumentException("result");
        ReportEvent("level_"+result,new Dictionary<string,object>{{"level",currentLevel},{"progress",progress},{"__businessLevel",true}});
    }
    public void ReportLevelRevive() { ReportEvent("level_revive"); }

    private Dictionary<string,object> PrepareTelemetry(string name, Dictionary<string,object> source)
    {
        if (name=="ad_placement_show") return null;
        var p=source==null ? new Dictionary<string,object>() : new Dictionary<string,object>(source);
        bool result=name=="level_success" || name=="level_fail" || name=="level_exit";
        if (name=="tutorial_step")
        {
            int localStep=ReadInt(p,"step",0);
            string stepName=p.ContainsKey("step_name") ? Convert.ToString(p["step_name"])?.Trim() : "";
            if(localStep<=0 || !IsChineseDisplayName(stepName)) return null;
            if(stepName.Length>64) stepName=stepName.Substring(0,64);
            int step=MiniGamePrefs.GetInt(TutorialKey("step"),0);
            if(step==int.MaxValue) return null;
            step++;
            p.Clear(); p["step"]=step; p["step_name"]=stepName;
            MiniGamePrefs.SetInt(TutorialKey("step"),step); MiniGamePrefs.Save();
        }
        if (name=="level_start" || result)
        {
            var items=RemainingItemsProvider != null ? RemainingItemsProvider() : (p.ContainsKey("剩余道具") ? p["剩余道具"] as Dictionary<string,object> : null);
            var snapshot=new Dictionary<string,object>();
            if (items==null) Debug.LogWarning("[MiniGameKit] 尚未接入即时剩余道具快照");
            else foreach(var kv in items)
            {
                double value; bool chinese=false;
                foreach(char c in kv.Key) if(c>='\u4e00' && c<='\u9fff') chinese=true;
                if (!chinese || !double.TryParse(Convert.ToString(kv.Value,CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out value) || double.IsNaN(value) || double.IsInfinity(value) || value<0)
                    throw new ArgumentException("剩余道具需中文名和有限非负数量");
                snapshot[kv.Key]=value;
            }
            p["剩余道具"]=snapshot;
        }
        if (name=="tutorial_start" || name=="tutorial_finish")
        {
            string phase=name=="tutorial_start" ? "start" : "finish";
            if (MiniGamePrefs.GetInt(TutorialKey(phase),0)!=0) return null;
            if (phase=="finish" && MiniGamePrefs.GetInt(TutorialKey("start"),0)==0) return null;
            MiniGamePrefs.SetInt(TutorialKey(phase),1); MiniGamePrefs.Save();
        }
        if (name=="level_start")
        {
            int input=ReadInt(p,"level",0);
            int business=BusinessLevelMapper(input);
            if (business<1) { Debug.LogWarning("[MiniGameKit] level 必须映射为 1 起业务编号"); return null; }
            string mode=p.ContainsKey("mode") ? Convert.ToString(p["mode"]) : "level";
            if (levelActive && !levelResultSent && business==currentLevel && mode==currentMode) return null;
            currentLevel=business; currentMode=mode; levelActive=true; levelResultSent=false;
            adCount=0; levelStarted=ActiveSeconds;
            p["level"]=business; p["mode"]=mode;
            if (FirstLevelIsTutorial && business==1) ReportTutorialStart();
        }
        if (result)
        {
            if (!levelActive || levelResultSent) return null;
            if (p.ContainsKey("level") && !p.ContainsKey("__businessLevel") && BusinessLevelMapper(ReadInt(p,"level",0))!=currentLevel) return null;
            p.Remove("__businessLevel");
            p["level"]=currentLevel; p["mode"]=currentMode; p["ad_count"]=adCount;
            double duration=p.ContainsKey("duration") ? Convert.ToDouble(p["duration"],CultureInfo.InvariantCulture) : Math.Floor(Math.Max(0,ActiveSeconds-levelStarted));
            if(double.IsNaN(duration) || double.IsInfinity(duration) || duration<0) throw new ArgumentException("duration");
            p["duration"]=duration;
            double progress=p.ContainsKey("progress") ? Convert.ToDouble(p["progress"],CultureInfo.InvariantCulture) : (name=="level_success" ? 1 : 0);
            p["progress"]=double.IsNaN(progress) || double.IsInfinity(progress) ? 0 : Math.Max(0,Math.Min(1,progress));
            levelResultSent=true;
        }
        if (name=="level_revive")
        {
            if (!levelActive || !levelResultSent) return null;
            levelResultSent=false; levelStarted=ActiveSeconds;
            p["level"]=currentLevel; p["mode"]=currentMode;
        }
        if (name.StartsWith("ad_reward_",StringComparison.Ordinal) && name!="ad_reward_preload"
            && !(p.ContainsKey("phase") && object.Equals(p["phase"],"preload")))
        {
            p["prop_name"]=ResolveRewardName(p.ContainsKey("prop_name") ? Convert.ToString(p["prop_name"]) : null);
            p["当前关卡"]=currentLevel;
            if (name=="ad_reward_success" && levelActive) adCount++;
        }
        return p;
    }
    private static int ReadInt(Dictionary<string,object> p,string key,int fallback)
    {
        object value; if(!p.TryGetValue(key,out value)) return fallback;
        int n; return int.TryParse(Convert.ToString(value,CultureInfo.InvariantCulture),out n) ? n : fallback;
    }
    private static int ReadNonNegativeIntegerOrZero(Dictionary<string,object> p,string key)
    {
        object value; if(!p.TryGetValue(key,out value) || value==null || value is string) return 0;
        if(value is byte) return (byte)value;
        if(value is ushort) return (ushort)value;
        if(value is uint) return (uint)value<=int.MaxValue ? (int)(uint)value : 0;
        if(value is ulong) return (ulong)value<=int.MaxValue ? (int)(ulong)value : 0;
        if(value is sbyte) return (sbyte)value>=0 ? (sbyte)value : 0;
        if(value is short) return (short)value>=0 ? (short)value : 0;
        if(value is int) return (int)value>=0 ? (int)value : 0;
        if(value is long) return (long)value>=0 && (long)value<=int.MaxValue ? (int)(long)value : 0;
        return 0;
    }
    private void DispatchTencent(string name, Dictionary<string,object> p)
    {
        if (name=="tutorial_step") return;
        if (!AppConst.EnableDnSDK || !DnSDKBridge.IsValidActionType(name)) return;
        if (name=="tutorial_start") DnSDKBridge.TrackTutorialStart();
        else if (name=="tutorial_finish") DnSDKBridge.OnTutorialFinish();
        else if (name=="load_finish") DnSDKBridge.TrackLoadFinish();
        else if (name=="level_start" || name=="level_revive" || name=="level_success" || name=="level_fail" || name=="level_exit")
        {
            var payload=new Dictionary<string,object> {
                {"enter_level_name",p.ContainsKey("level_name") ? p["level_name"] : "第"+currentLevel+"关"},
                {"coin_amount",ReadInt(p,"coins",0)}, {"stamina_value",ReadInt(p,"stamina",0)}, {"level_value",currentLevel}
            };
            if(ReadInt(p,"chapter",0)>0) payload["chapter_id"]=ReadInt(p,"chapter",0);
            payload["level_id"]=currentLevel;
            payload["enter_level_id"]=p.ContainsKey("progress") ? p["progress"] : 0;
            payload["game_mode"]=currentMode;
            if(name!="level_start" && name!="level_revive") {
                payload["ad_cnt"]=ReadNonNegativeIntegerOrZero(p,"ad_count");
                payload["duration"]=p["duration"];
                var used = new List<string>();
                object value;
                if(p.TryGetValue("used_items",out value) && value is List<DnSDKBridge.DnItemUsage>)
                    foreach(var item in (List<DnSDKBridge.DnItemUsage>)value) if(item.itemNum>0) used.Add(item.ToString());
                payload["items"]=used;
            }
            DnSDKBridge.Track(name=="level_start" || name=="level_revive" ? "LEVEL_ENTER" : name=="level_success" ? "LEVEL_PASS" : name=="level_fail" ? "LEVEL_LOSE" : "LEVEL_EXIT",payload);
        }
        else if (name=="ad_reward_show" || name=="ad_banner_show" || name=="ad_interstitial_show")
            DnSDKBridge.TrackAdImpression(name=="ad_reward_show" ? 1 : name=="ad_banner_show" ? 4 : 2);
        else if (name=="ad_reward_click" || name=="ad_reward_success")
        {
            int id=ReadInt(p,"ad_placement_name",0);
            if (id<1 || id>67) return; // Invalid/Unknown: do not fabricate a Tencent placement.
            if(name=="ad_reward_click") DnSDKBridge.TrackAdClick(id);
            else DnSDKBridge.TrackAdVideoFinish(id);
        }
        else if (name=="share_result" && p.ContainsKey("success") && object.Equals(p["success"],true)) DnSDKBridge.TrackShare("APP_MESSAGE");
        else if (name=="desktop_add_success") DnSDKBridge.TrackAddToWishlist("desktop");
        else if (name=="subscribe_success") DnSDKBridge.TrackSubscribe();
        else if (name=="app_quit") DnSDKBridge.OnAppQuit();
        else if (name=="app_reactive") DnSDKBridge.TrackReActive(ReadInt(p,"backFlowDay",0));
        else if (name=="purchase_success" && p.ContainsKey("value"))
        {
            double value=Convert.ToDouble(p["value"],CultureInfo.InvariantCulture);
            if (!double.IsNaN(value) && !double.IsInfinity(value) && value>0) DnSDKBridge.OnPurchase(value);
        }
    }
}
