---
name: minigame-sdk-platform-capabilities
description: 在当前交付模式的源码分支安装并接入微信/抖音平台实现、生命周期、侧边栏、排行榜、快捷方式、震动、Toast 与常亮；不负责公共 Core 或构建模板。
---

# 小游戏 SDK 平台能力

先确认 `game_sdk_core.result: passed`，再在当前模式规定的源码分支运行组件安装器：微信用 `--component wechat`，抖音用 `--component douyin`。统一矩阵模式对所选矩阵一次性安装所有适用平台组件，并由编译宏选择适配器；不得把平台判断散布到业务代码。抖音需要侧边栏 UI 时额外加 `--component ui`；微信只有实际使用公共 UI 时才安装 UI。默认字体已经由 Core 安装。

运行 `python scripts/install_components.py <工程> --component <wechat|douyin|ui>` 先 dry-run，确认后加 `--apply`。可重复传入 `--component`；冲突时逐文件合并。

抖音必须补齐侧边栏真实奖励与幂等状态、排行榜 KVKey/结算上报/原生 UI；微信排行榜模板未实现，必须由独立排行榜能力交付。验证 onShow/onHide、启动参数快照、常亮、快捷方式、震动和 Dispose。

```yaml
game_sdk_platform:
  delivery_mode: isolated_branches | unified_matrix
  variant_id: <stable id or null>
  resolved_profile_sha256: <value or null>
  platform: wechat | douyin
  install_report: <record>
  lifecycle: passed | failed
  keep_screen_on: passed | failed
  sidebar: passed | failed | not-applicable
  leaderboard: passed | failed | not-applicable
  other_capabilities: []
  result: passed | partial | blocked | failed
```
