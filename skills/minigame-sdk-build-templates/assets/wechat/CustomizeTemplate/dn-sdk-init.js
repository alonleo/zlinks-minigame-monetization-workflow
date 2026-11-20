import { SDK } from './dn-sdk/index.js';

// Build hook imports this module only when explicitly enabled with validated credentials.
const cfg = GameGlobal.dnSDKConfig;
const valid = cfg && cfg.enabled === true && Number.isSafeInteger(cfg.user_action_set_id)
    && cfg.user_action_set_id > 0 && typeof cfg.secret_key === 'string' && cfg.secret_key.length === 32 && cfg.appid;
let sdk = null;
let identity = '';
let unionIdentity = '';
const pending = [];
const completion = {identity:false,tutorialStart:false,tutorialFinish:false,levelEnter:false,levelPass:false,levelLose:false,adClick:false,adImpression:false,adVideoFinish:false};
let completionNotified = false;
SDK.setDebug(false);
function safe(value) {
    try { return JSON.parse(JSON.stringify(value,(key,item)=>/^(openid|unionid|secret_key|secretkey|code|clickid)$/i.test(key)?'[REDACTED]':item)); }
    catch (_) { return {}; }
}
function green(label,payload) {
    if (!cfg || cfg.debug !== true) return;
    console.log('%c[TrackManager]['+label+']','color:#16a34a;font-weight:bold',JSON.stringify(safe(payload)));
}
function observe(action,param) {
    if (action === 'TUTORIAL_START') completion.tutorialStart = true;
    else if (action === 'LEVEL_ENTER' && Number.isInteger(param&&param.level_id) && param.level_id >= 1) completion.levelEnter = true;
    else if (action === 'LEVEL_PASS' && Number.isInteger(param&&param.level_id) && param.level_id >= 1) completion.levelPass = true;
    else if (action === 'LEVEL_LOSE' && Number.isInteger(param&&param.level_id) && param.level_id >= 1) completion.levelLose = true;
    else if (action === 'AD_CLICK' && Number.isInteger(param&&param.ad_placement_name) && param.ad_placement_name >= 1 && param.ad_placement_name <= 67) completion.adClick = true;
    else if (action === 'AD_IMPRESSION' && param&&param.ad_type === 1) completion.adImpression = true;
    else if (action === 'AD_VIDEO_FINISH' && Number.isInteger(param&&param.ad_placement_name) && param.ad_placement_name >= 1 && param.ad_placement_name <= 67) completion.adVideoFinish = true;
    const ready = completion.identity && completion.tutorialStart && completion.tutorialFinish && completion.levelEnter && completion.levelPass && completion.levelLose && completion.adClick && completion.adImpression && completion.adVideoFinish;
    if (ready && !completionNotified) {
        completionNotified = true;
        green('✅ dnSdk已完成',{success:true,channel:'tencent',required:completion,note:'本机必填字段与 SDK 同步接收检查通过；后台送达、归因、质量看板和审核仍需渠道验收'});
    }
}
function invoke(method, args) {
    if (!valid) return;
    // Validate before queuing as well as before invoking the vendor.
    if (method === 'track' && (typeof args[0] !== 'string'
        || !/^[a-zA-Z][a-zA-Z\d_]{0,63}$/.test(args[0]) || /[\r\n]/.test(args[0]) || args[0] === 'AD_PLACEMENT_SHOW')) return;
    if (!sdk) {
        if (pending.length >= 500) pending.shift();
        pending.push([method,args]);
        return;
    }
    if (typeof sdk[method] === 'function') {
        const result=sdk[method].apply(sdk,args);
        const success=method === 'track' ? !!result && result.code === 0 : true;
        green(success?'SDK已接收':'SDK拒绝',{success:success,channel:'tencent',action:method === 'track' ? args[0] : method,data:method === 'track' ? (args[1]||{}) : {}});
        if (success) {
            if (method === 'track') observe(args[0],args[1]||{});
            else if (method === 'onTutorialFinish') { completion.tutorialFinish=true; observe('',{}); }
        }
        return result;
    }
}
function setIdentity(openid, unionid) {
    if (!valid || typeof openid !== 'string' || !openid.trim()) return;
    unionid = typeof unionid === 'string' ? unionid.trim() : '';
    if (sdk) {
        if (identity !== openid) sdk.setOpenId(openid);
        if (unionid && unionIdentity !== unionid && sdk.setUnionId) sdk.setUnionId(unionid);
        identity=openid; unionIdentity=unionid || unionIdentity;
        return;
    }
    try {
        const options={user_action_set_id:cfg.user_action_set_id,secret_key:cfg.secret_key,
            appid:cfg.appid,openid:openid,auto_track:true};
        if (unionid) options.unionid=unionid;
        const candidate = new SDK(options);
        if (!candidate.getInitResult().inited) return;
        sdk=candidate; identity=openid; unionIdentity=unionid;
        completion.identity=true;
        while (pending.length) { const item=pending.shift(); invoke(item[0],item[1]); }
        observe('',{});
    } catch (_) { console.warn('[MiniGameKit] dnSDK initialization unavailable'); }
}
function setOpenId(openid) { setIdentity(openid,unionIdentity); }
// A stable facade accepts queued events before login; identity precedes REGISTER delivery.
GameGlobal.dnSDK = {
    setIdentity: setIdentity,
    setOpenId: setOpenId,
    setUnionId: function(id) {
        unionIdentity=typeof id === 'string' ? id.trim() : '';
        if (sdk && unionIdentity && sdk.setUnionId) sdk.setUnionId(unionIdentity);
    },
    track: function(action,param) { invoke('track',param ? [action,param] : [action]); },
    onRegister: function() { invoke('onRegister',[]); },
    onTutorialFinish: function() { invoke('onTutorialFinish',[]); },
    onPurchase: function(value) { invoke('onPurchase',[value]); },
    onAppQuit: function() { invoke('onAppQuit',[]); },
    // auto_track owns these events; compatibility calls must not double-send.
    onAppStart: function() {}, onEnterForeground: function() {}, onEnterBackground: function() {}
};
GameGlobal.dnSDKReady = { check: function() { return valid ? 1 : 0; }, initialized: function() { return sdk ? 1 : 0; } };
