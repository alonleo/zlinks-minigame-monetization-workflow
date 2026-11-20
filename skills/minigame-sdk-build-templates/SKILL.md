---
name: minigame-sdk-build-templates
description: 接入微信 dnSDK 构建钩子或生成禁用 WASM 函数分包的抖音 CustomizeTemplate，并验证模板与当前平台 SDK 版本和候选一致。用于 Release 构建前的平台模板准备。
---

# 小游戏 SDK 构建模板

微信运行 `python scripts/prepare_wechat_template.py <工程>` 预检，确认后加 `--apply`，安装 `DnSDKBuildHook.cs` 和 vendor；只有显式启用且凭证合法时注入，并按实际 `WXConvertCore.DoExport` 决定是否定义 `MINIGAME_KIT_WX_CONVERTER`。

抖音使用 `python scripts/prepare_douyin_template.py <工程> --destination <实际TTSDK模板目录>` 预检，确认后加 `--apply`。生成模板保留唯一主 WASM、Data/资源子包和轻量开屏。

统一矩阵模式下，微信与抖音模板都必须读取当前 `ResolvedMiniGameProfile` 的 AppID、正式名称和非敏感平台配置，并把同一 `resolved_profile_sha256` 写入构建回执。对实际候选的 `project.config.json` 先运行 `python scripts/apply_resolved_profile.py --profile <resolved.json> --platform <wechat|douyin> --project-config <path>` 预检，确认后加 `--apply`；再核对生成配置和回执哈希。不得从显示名称猜 AppID，不得复用上一个目标残留的模板或临时配置；敏感 dnSDK 凭据仍由受保护环境单独注入。

```yaml
game_sdk_build_template:
  delivery_mode: isolated_branches | unified_matrix
  variant_id: <stable id or null>
  resolved_profile_sha256: <value or null>
  platform: wechat | douyin
  sdk_version: <value>
  template_path: <path>
  wasm_function_split: disabled
  active_split_artifacts: []
  wechat_dnsdk_injection: passed | not-applicable
  result: passed | blocked | failed
```
