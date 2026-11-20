---
name: minigame-resource-packaging
description: 为 Unity 小游戏设计、生成和审计资源包归属、依赖闭包、动态配置依赖、Catalog/Bundle、轻量启动索引和 CDN 发布集合。用于资源分包与首包治理；不负责业务运行时改造、CDN 凭据上传或平台启动性能结论。
---

# 小游戏资源构建与分包

本 skill 独立负责把游戏资源规则转换为可审计的构建分组和发布集合。它不通过扩大启动包掩盖运行时缺失，也不把移动目录当成完成资源外置。

执行时完整读取并应用[构建分包协议](references/packaging-protocol.md)。构建目标必须匹配对应播放器；微信和抖音分别生成、审计和记录，不假设 Bundle 可跨平台混用。正式候选的 WASM 函数分包由 Release skill 强制关闭；本 skill 只处理 Data/资源子包和远程内容。

## 输出

```yaml
resource_packaging:
  platform: wechat | douyin
  content_version: <immutable version>
  package_rules: <path or identifier>
  dependency_report: <path>
  local_main_bytes: 0
  data_package_bytes: 0
  remote_bytes: 0
  default_font: {path: <AlibabaPuHuiTi target>, source_bytes: 2151384, player_bytes: <measured>}
  bundle_count: 0
  dynamic_dependency_validation: passed | failed | not-applicable
  startup_index: full-local | lightweight-local-remote-full | not-applicable
  catalog_and_bundle_version_match: false
  player_package_audit: passed | failed
  remote_manifest: <path or null>
  unverified_scope: []
  result: passed | partial | blocked | failed
```

只有包归属稳定、依赖闭包完整、动态地址已覆盖、播放器真实产物完成审计且远程集合具有不可变版本和精确清单时，`result` 才能为 `passed`。
