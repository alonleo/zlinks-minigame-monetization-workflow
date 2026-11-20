---
name: minigame-resource-runtime
description: 为 Unity 小游戏设计、接入或核验统一异步资源运行时，覆盖 UI、场景、战斗、音频、对象池、预加载、取消、句柄、缓存和低内存处理。用于资源外置或按需加载的源码实现；不负责 Bundle 分组构建、CDN 上传或启动性能结论。
---

# 小游戏资源运行时

本 skill 独立完成资源运行时的设计、项目适配、实现与正确性验证。业务只表达资源地址、UI ID、场景或领域资源集合，不感知物理包名、CDN URL 和下载状态。平台 SDK 只进入下载/缓存适配器，框架核心不直接绑定微信或抖音。

执行时完整读取并应用[运行时协议](references/runtime-protocol.md)。先定位项目已有资源后端、UI/场景入口、战斗准备边界和生命周期，再做最小兼容改造；不得平行新增另一套下载器，也不得在按钮上逐个追加下载补丁。

## 输出

```yaml
resource_runtime:
  stable_addressing: passed | failed | not-applicable
  unified_async_contract: passed | failed
  shared_request_and_cancellation: passed | failed
  ui_scene_battle_boundaries: passed | failed
  preload_is_optional: passed | failed
  handle_and_memory_lifecycle: passed | failed
  failure_and_retry_semantics: passed | failed
  editor_tests: []
  platform_evidence: []
  unverified_scope: []
  result: passed | partial | blocked | failed
```

只有关闭后台预载后正式按需加载仍正确、取消/重试不重复提交业务、所有持有关系可追踪且适用入口经过验证，`result` 才能为 `passed`。
