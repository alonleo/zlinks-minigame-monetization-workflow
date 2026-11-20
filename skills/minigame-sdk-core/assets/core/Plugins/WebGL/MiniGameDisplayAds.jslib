mergeInto(LibraryManager.library, {
  $MGKDisplayAds: {
    slots: {},
    valid: function (s) { return MGKDisplayAds.slots[s.kind] === s; },
    notify: function (s, stage, error, phase) {
      var e={kind:s.kind,id:s.id,stage:stage,phase:phase||''};
      if(stage==='error') {
        e.code=error && typeof error.errCode==='number'?error.errCode:-1;
        e.message=error && (error.errMsg||error.message)||'ad_failed';
      }
      var json=JSON.stringify(e);
      if(typeof SendMessage==='function') SendMessage(s.receiver,'OnDisplayAdEvent',json);
      else if(typeof Module!=='undefined' && Module.SendMessage) Module.SendMessage(s.receiver,'OnDisplayAdEvent',json);
    },
    dispose: function (kind) {
      var s=MGKDisplayAds.slots[kind];if(!s)return;
      delete MGKDisplayAds.slots[kind];
      if(!s.ad)return;
      if(s.ad.offLoad)s.ad.offLoad(s.onLoad);
      if(s.ad.offError)s.ad.offError(s.onError);
      if(s.ad.offClose)s.ad.offClose(s.onClose);
      try{s.ad.destroy();}catch(e){console.warn('[MiniGameKit] Display ad cleanup failed');}
    },
    fail: function (s,error) {
      if(!MGKDisplayAds.valid(s))return;
      var phase=s.requested?'show':'preload';
      MGKDisplayAds.dispose(s.kind);
      MGKDisplayAds.notify(s,'error',error,phase);
    },
    emit: function (s) {
      if(!MGKDisplayAds.valid(s)||!s.requested||s.reported)return;
      s.reported=true;MGKDisplayAds.notify(s,'show');
    },
    loaded: function (s) {
      if(!MGKDisplayAds.valid(s)||!s.loading)return;
      s.loading=false;s.ready=true;
      MGKDisplayAds.notify(s,'preload'); // Never calls show.
    },
    prepare: function (kind,id,receiver,channel) {
      if(!id||(kind!=='banner'&&kind!=='interstitial'))return;
      channel=channel||'douyin';
      var old=MGKDisplayAds.slots[kind];
      if(old&&old.id===id&&old.channel===channel&&(old.loading||old.ready))return;
      MGKDisplayAds.dispose(kind);
      var host=channel==='weixin'?(typeof wx!=='undefined'?wx:null):(typeof tt!=='undefined'?tt:null);
      var s={kind:kind,id:id,receiver:receiver,channel:channel,ad:null,loading:true,ready:false,
        requested:false,reported:false,issued:false,attempt:0};
      MGKDisplayAds.slots[kind]=s;
      try {
        if(!host)throw new Error('host_unavailable');
        s.ad=kind==='banner'
          ?host.createBannerAd({adUnitId:id,style:{left:0,top:0,width:320}})
          :host.createInterstitialAd({adUnitId:id});
        if(!s.ad)throw new Error('ad_creation_failed');
        s.onLoad=function(){MGKDisplayAds.loaded(s);};
        s.onError=function(e){MGKDisplayAds.fail(s,e);};
        s.onClose=function(){
          if(!MGKDisplayAds.valid(s)||!s.requested||!s.issued)return;
          MGKDisplayAds.emit(s);
          MGKDisplayAds.dispose(kind);MGKDisplayAds.notify(s,'close');
        };
        s.ad.onLoad(s.onLoad);s.ad.onError(s.onError);
        if(typeof s.ad.onClose==='function')s.ad.onClose(s.onClose);
        if(kind==='interstitial') {
          var p=s.ad.load();
          if(p&&typeof p.then==='function')p.then(s.onLoad,function(e){if(s.loading)s.onError(e);});
        }
      }catch(e){MGKDisplayAds.fail(s,e);}
    },
    show: function(kind,id,receiver,channel) {
      var s=MGKDisplayAds.slots[kind];
      if(!s||s.id!==id||s.channel!==(channel||'douyin')||!s.ready||s.loading||s.requested)return;
      s.receiver=receiver;s.requested=true;s.reported=false;s.issued=true;var attempt=++s.attempt;
      // PlatformManager reports the accepted use request exactly once.
      try {
        var p=s.ad.show();
        if(p&&typeof p.then==='function')p.then(function(){if(s.attempt===attempt)MGKDisplayAds.emit(s);},
          function(e){if(s.attempt===attempt)MGKDisplayAds.fail(s,e);});
      }catch(e){MGKDisplayAds.fail(s,e);}
    },
    hideBanner:function(){
      var s=MGKDisplayAds.slots.banner;if(!s)return;
      s.requested=false;s.issued=false;s.attempt++;
      try{s.ad.hide();}catch(e){MGKDisplayAds.dispose('banner');}
    }
  },
  MGK_DisplayAdPrepare__deps:['$MGKDisplayAds'],
  MGK_DisplayAdPrepare:function(kind,id,receiver,channel){MGKDisplayAds.prepare(UTF8ToString(kind),UTF8ToString(id),UTF8ToString(receiver),UTF8ToString(channel));},
  MGK_DisplayAdShow__deps:['$MGKDisplayAds'],
  MGK_DisplayAdShow:function(kind,id,receiver,channel){MGKDisplayAds.show(UTF8ToString(kind),UTF8ToString(id),UTF8ToString(receiver),UTF8ToString(channel));},
  MGK_DisplayAdHideBanner__deps:['$MGKDisplayAds'],
  MGK_DisplayAdHideBanner:function(){MGKDisplayAds.hideBanner();},
  MGK_DisplayAdDispose__deps:['$MGKDisplayAds'],
  MGK_DisplayAdDispose:function(kind){MGKDisplayAds.dispose(UTF8ToString(kind));}
});
