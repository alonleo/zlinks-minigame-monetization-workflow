---
name: minigame-startup-verification
description: 对 Unity 微信或抖音小游戏的启动路线、首屏、入口可操作时间、缓存状态和资源异步化回归进行分阶段验证。用于同一正式候选的 IDE/真机性能与正确性证据；不负责实现资源框架、生成 Bundle 或启用 WASM 函数分包。
---

# 小游戏启动与迁移验收

本 skill 独立验证“看到平台封面”“看到游戏加载画面”和“入口可操作”等不同终点，不用静态体积或单条日志推导真机性能。正式候选必须关闭 WASM 函数分包；若日志出现函数子包等待，候选失败并退回构建。

执行时完整读取并应用[启动验收协议](references/startup-verification.md)。没有真机或明确缓存控制证据时，只能报告 `estimated-only`、`partial` 或 `blocked`。

## 输出

```yaml
startup_verification:
  platform: wechat | douyin
  build_id: <same final candidate>
  device_and_os: <value>
  network_profile: <value>
  cache_state: cold | file-cache | warm-memory | unknown
  samples: 0
  milestones_ms:
    platform_cover: null
    unity_first_scene: null
    catalog_ready: null
    entry_resources_ready: null
    first_interactive: null
  missing_wasm_function_waits: 0
  correctness_matrix: <path or record>
  migration_regressions: []
  unverified_scope: []
  result: passed | failed | estimated-only | partial | blocked
```
