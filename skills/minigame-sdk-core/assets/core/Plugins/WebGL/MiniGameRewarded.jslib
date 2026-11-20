// Unity WebGL bridge; platform objects own real ad callbacks, not simulated C# success.
mergeInto(LibraryManager.library, {
  $MGKRewarded: {
    state: null,
    host: function (channel) {
      return channel === 'weixin' ? (typeof wx !== 'undefined' ? wx : null)
        : channel === 'douyin' ? (typeof tt !== 'undefined' ? tt : null) : null;
    },
    emit: function (s, type, code, message) {
      if (MGKRewarded.state !== s) return;
      var json = JSON.stringify({type: type, id:s.id, code: Number(code) || 0, message: message || ''});
      if (typeof SendMessage === 'function') SendMessage(s.receiver, 'OnAdEvent', json);
      else if (typeof Module !== 'undefined' && Module.SendMessage) Module.SendMessage(s.receiver, 'OnAdEvent', json);
    },
    clearTimer: function (s) { clearTimeout(s.timer); s.timer = null; },
    detach: function (s) {
      clearTimeout(s.timeout); s.timeout = null;
      s.generation++;
      var ad = s.ad; s.ad = null; s.loading = false; s.ready = false;
      if (!ad) return;
      if (ad.offLoad) ad.offLoad(s.onLoad);
      if (ad.offClose) ad.offClose(s.onClose);
      if (ad.offError) ad.offError(s.onError);
      if (ad.destroy) ad.destroy();
    },
    fail: function (s, error) {
      if (MGKRewarded.state !== s) return;
      var hadRequest = s.request;
      s.request = false; s.showing = false;
      MGKRewarded.detach(s);
      // Schedule before C# callback: callback may immediately request another ad.
      if (!hadRequest) MGKRewarded.emit(s,'preload_error',error && error.errCode || -1,error && (error.errMsg||error.message) || 'ad_failed');
      if (hadRequest) MGKRewarded.emit(s, 'error', error && error.errCode || -1, error && error.errMsg || 'ad_failed');
    },
    create: function (s) {
      if (s.ad) return true;
      if (!s.host || !s.host.createRewardedVideoAd) { MGKRewarded.fail(s, {errCode:-5,errMsg:'unsupported'}); return false; }
      try {
        s.ad = s.host.createRewardedVideoAd({adUnitId:s.id, multiton:false});
        if (!s.ad) throw new Error('create_failed');
        var generation = ++s.generation;
        var valid = function () { return MGKRewarded.state === s && generation === s.generation; };
        s.onLoad = function () { if (valid()) MGKRewarded.loaded(s); };
        s.onError = function (e) { if (valid()) MGKRewarded.fail(s,e); };
        s.onClose = function (result) {
          if (!valid() || !s.request) return;
          // A close callback also proves the video was displayed if show() has no Promise.
          MGKRewarded.shown(s);
          s.request = false; s.showing = false; s.ready = false;
          clearTimeout(s.timeout); s.timeout = null;
          // PlatformManager schedules the next preload after frequency checks.
          MGKRewarded.emit(s, result && result.isEnded === true ? 'complete' : 'cancel');
        };
        if (s.ad.onLoad) s.ad.onLoad(s.onLoad);
        if (s.ad.onError) s.ad.onError(s.onError);
        if (s.ad.onClose) s.ad.onClose(s.onClose);
        return true;
      } catch (_) { MGKRewarded.fail(s,{errCode:-4,errMsg:'create_failed'}); return false; }
    },
    loaded: function (s) {
      if (MGKRewarded.state !== s || !s.loading) return;
      clearTimeout(s.timeout); s.timeout = null;
      s.loading = false; s.ready = true;
      MGKRewarded.emit(s,'preload');
      if (s.request) MGKRewarded.showReady(s);
    },
    load: function (s) {
      if (s.loading || s.ready) { if (s.ready && s.request) MGKRewarded.showReady(s); return; }
      if (!s.ad.load) { if (s.request) MGKRewarded.showReady(s); return; }
      s.loading = true;
      var generation = s.generation;
      var valid = function () { return MGKRewarded.state === s && generation === s.generation; };
      s.timeout = setTimeout(function () { if (valid() && s.loading) MGKRewarded.fail(s,{errCode:-1,errMsg:'load_timeout'}); },30000);
      try {
        var p = s.ad.load();
        if (p && p.then) p.then(function () { if (valid()) MGKRewarded.loaded(s); }, function (e) { if (valid() && s.loading) MGKRewarded.fail(s,e); });
      } catch (e) { if (valid()) MGKRewarded.fail(s,e); }
    },
    shown: function (s) {
      if (MGKRewarded.state !== s || !s.request || s.exposure) return;
      s.exposure = true; clearTimeout(s.timeout); s.timeout = null;
      MGKRewarded.emit(s,'show');
    },
    showReady: function (s) {
      if (!s.request || s.showing) return;
      s.showing = true; s.ready = false;
      var generation = s.generation;
      var valid = function () { return MGKRewarded.state === s && generation === s.generation && s.request; };
      // Pause gameplay timing at invocation, but do not report exposure until confirmed.
      MGKRewarded.emit(s,'opening');
      s.timeout = setTimeout(function () { if (valid() && !s.exposure) MGKRewarded.fail(s,{errCode:-1,errMsg:'show_timeout'}); },30000);
      try {
        var p = s.ad.show();
        if (p && p.then) p.then(function () { if (valid()) MGKRewarded.shown(s); }, function(e) { if (valid()) MGKRewarded.fail(s,e); });
      } catch (e) { if (valid()) MGKRewarded.fail(s,e); }
    },
    prepare: function (channel,id,receiver) {
      var old=MGKRewarded.state;
      if (old && old.channel===channel && old.id===id) return old;
      MGKRewarded.dispose();
      var s={channel:channel,id:id,receiver:receiver,host:MGKRewarded.host(channel),generation:0,request:false};
      MGKRewarded.state=s;
      // Preparing state never requests an ad; PlatformManager decides when to preload.
      return s;
    },
    preload:function(channel,id,receiver) {
      var s=MGKRewarded.prepare(channel,id,receiver);
      if(s.request||s.loading||s.ready)return;
      if(MGKRewarded.create(s)) {
        if(!s.ad.load) { MGKRewarded.emit(s,'preload_error',-5,'preload_unsupported');return; }
        MGKRewarded.load(s);
      }
    },
    request: function (channel,id,receiver) {
      var s=MGKRewarded.prepare(channel,id,receiver);
      if (s.request) return;
      s.request=true; s.exposure=false;
      MGKRewarded.clearTimer(s);
      if (MGKRewarded.create(s)) MGKRewarded.load(s);
    },
    dispose: function () {
      var s=MGKRewarded.state; if (!s) return;
      MGKRewarded.state=null;
      MGKRewarded.clearTimer(s); MGKRewarded.detach(s);
    }
  },
  MGK_RewardPrepare__deps: ['$MGKRewarded'],
  MGK_RewardPrepare: function (channel,id,receiver) { MGKRewarded.prepare(UTF8ToString(channel),UTF8ToString(id),UTF8ToString(receiver)); },
  MGK_RewardPreload__deps: ['$MGKRewarded'],
  MGK_RewardPreload: function(channel,id,receiver){MGKRewarded.preload(UTF8ToString(channel),UTF8ToString(id),UTF8ToString(receiver));},
  MGK_RewardShow__deps: ['$MGKRewarded'],
  MGK_RewardShow: function (channel,id,receiver) { MGKRewarded.request(UTF8ToString(channel),UTF8ToString(id),UTF8ToString(receiver)); },
  MGK_RewardDispose__deps: ['$MGKRewarded'],
  MGK_RewardDispose: function () { MGKRewarded.dispose(); }
});
