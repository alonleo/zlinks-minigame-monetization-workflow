---
name: minigame-sdk-ads
description: 接入并验证微信/抖音激励、Banner、插屏广告及预加载、频控、重试和统计回调。用于广告业务接线；不负责平台账号配置、审核或发布。
---

# 小游戏 SDK 广告接入

在当前模式的源码分支基于 `game_sdk_core` 接入广告。分支隔离模式只接入当前平台；统一矩阵模式在统一分支保留两个平台适配，但广告位从当前解析配置读取，每个生产候选只激活一个 `variant_id + platform`。复用 `PlatformManager`、`MiniGameRewardedBridge`、`MiniGameDisplayAdBridge` 与 WebGL jslib，不另建自动重试器。

逐入口绑定真实中文奖励名、1～67 合法 placement、奖励回调和当前关卡。预加载不得自动展示或伪造点击；展示、失败、关闭和完成只按宿主真实回调生成并去重。频控命中不请求、不统计；取消、错误、空 ID 直发和 `isNoAd` 直发不得增加观看次数或伪造曝光。

用目标工程内的桥接文件执行 JS/C# 隔离测试，至少覆盖预加载不展示、展示去重、取消、错误、完整观看、超时、销毁、前后台切换和频控；再在同一正式候选验证真实平台回调。

```yaml
game_sdk_ads:
  delivery_mode: isolated_branches | unified_matrix
  variant_id: <stable id or null>
  resolved_profile_sha256: <value or null>
  platform: wechat | douyin
  rewarded: passed | failed | not-applicable
  banner: passed | failed | not-applicable
  interstitial: passed | failed | not-applicable
  preload_retry_frequency: passed | failed
  entry_inventory: []
  native_evidence: []
  result: passed | partial | blocked | failed
```
