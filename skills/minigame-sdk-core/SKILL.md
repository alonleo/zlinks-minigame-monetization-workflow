---
name: minigame-sdk-core
description: 将 Unity 小游戏公共运行核心安全接入当前模式的源码分支，包含平台门面、HTTP、持久化、WebGL 桥接、默认中文字体和统一矩阵解析配置；不安装平台专属实现或业务 UI。
---

# 小游戏 SDK Core 接入

本 skill 在当前交付模式规定的源码分支接入公共 C#、WebGL 桥接和默认字体：分支隔离模式使用 `miniGame`，统一矩阵模式使用明确的统一分支。统一模式开始前完整读取总工作流的[交付拓扑与模式路由](../../references/delivery-topologies.md)。

先执行 `python scripts/install_components.py <工程>` 查看计划。确认目标工程和冲突后才加 `--apply`。不同文件、重复类型、GUID 冲突或目标越界必须停止并逐文件合并，不得覆盖。

`AlibabaPuHuiTi-3-105-Heavy.otf` 是所有小游戏默认字体，Core 无条件安装并记录源文件精确字节数与目标路径。该约定是有意的本地包输入；后续资源分包仍须报告它对真实播放器包的影响。Prefab、侧边栏纹理和平台实现不属于 Core。

完成后验证普通编辑器编译、Newtonsoft.Json 可见性、`MiniGamePrefs` 回退路径、公共接口与已有项目兼容性。

统一矩阵模式还必须维护无敏感信息的 JSON catalog，并对每个目标运行：

```powershell
python scripts/resolve_profile.py --catalog <catalog.json> --variant <variant-id> --platform <wechat|douyin> --output <ResolvedMiniGameProfile.json>
```

解析优先级为 `defaults < variant < platform < variant.platforms[platform]`。把输出作为临时构建输入放入 Unity `Resources/ResolvedMiniGameProfile.json` 或项目等价的确定性注入位置；构建结束后不得让另一个目标复用该临时文件。`MiniGameProfile` 会核对编译平台，`AppConst` 在存在解析配置时从中读取 AppID、广告位、CDN 和统计配置，否则保持旧模式常量回退。解析产物不得含 Secret、Token、私钥或访问密钥；统一模式微信 dnSDK 的 Secret 只通过构建进程环境变量 `MINIGAME_DN_SECRET_KEY` 注入，不写入 catalog 或回执。

```yaml
game_sdk_core:
  delivery_mode: isolated_branches | unified_matrix
  variant_id: <stable id or null>
  platform: <wechat | douyin | null>
  resolved_profile_sha256: <value or null>
  asset_sha256: <aggregate or manifest>
  default_font: {path: <target>, bytes: 2151384, sha256: <value>}
  install_report: <path or record>
  compile_result: passed | failed | blocked
  conflicts: []
  result: passed | partial | blocked | failed
```
