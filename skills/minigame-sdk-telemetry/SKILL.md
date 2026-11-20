---
name: minigame-sdk-telemetry
description: 接入和核验平台原生、自有统计与微信 dnSDK 三条独立通道，覆盖启动、教程、关卡、广告、心跳、队列和事件清单。用于需求实现与统计核实接力。
---

# 小游戏 SDK 统计接入

基于 `game_sdk_core` 接入平台原生统计；只有配置明确启用时接入自有统计，只有微信 IAA 且凭证合法时接入 dnSDK。三条通道独立排队、验证和回执，不能互相证明成功。

统一矩阵模式的事件实现可以共享，但 AppID、数据源索引、开关和候选身份必须来自当前解析配置；敏感凭据只从受保护构建环境注入，不进入 catalog、解析 JSON、Git 或回执。每个 `variant_id + platform` 分别取得真实网络证据。

微信 IAA 接入时完整读取并严格执行 [dnSDK 必报事件与参数门禁](references/wechat-dnsdk-required.md) 的关卡周期、业务 `level_id`、教程、placement 和真实广告次数规则。完成接线后运行 `python scripts/generate_track_events.py <工程>` 生成调用点清单，交付前用 `--check`；把清单、开关、映射和构建 ID 传给数据统计核实 skill。

```yaml
game_sdk_telemetry:
  delivery_mode: isolated_branches | unified_matrix
  variant_id: <stable id or null>
  resolved_profile_sha256: <value or null>
  platform: wechat | douyin
  platform_native: passed | failed
  custom_service: passed | failed | disabled
  wechat_dnsdk: passed | failed | not-applicable
  event_inventory: <path>
  queue_persistence: passed | failed
  channel_evidence: []
  result: passed | partial | blocked | failed
```
