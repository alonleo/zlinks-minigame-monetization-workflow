# 交付拓扑与模式路由

每次流程运行必须选择且只选择一种交付拓扑。模式决定 Git 边界、配置来源、候选身份和阶段里程碑；不得在同一 `workflow_id` 内混用两个模式，亦不得用一个模式的候选或证据完成另一个模式。

## 1. 模式选择

```yaml
delivery_topology:
  mode: isolated_branches | unified_matrix
```

- 用户明确选择时严格采用其选择。
- 用户未选择时，为兼容既有工程使用 `isolated_branches`，并在运行记录中写明该默认值。
- 开始修改、创建分支或构建后不得原地切换模式；切换模式必须建立新的 `workflow_id`，重新固定源码、配置和候选。
- 开始执行前使用 `python scripts/validate_delivery_topology.py <workflow-input.json>` 校验并保存规范化目标。该脚本只校验路由与目标身份，不替代工程、平台账号和 Release 门禁。

## 2. 分支隔离模式 `isolated_branches`

该模式保持既有模型：`develop -> miniGame -> 平台正式名称分支`。每个微信、抖音小游戏和同平台马甲包使用独立物理分支；完整 Git 规则见 [Git 检查点协议](git-checkpoints.md)。

```yaml
delivery_topology:
  mode: isolated_branches
branch_targets:
  - platform: wechat
    platform_game_name: <正式名称>
    branch: miniGame-wx-<name-initials>
```

候选身份至少是 `mode + source_commit + platform + platform_game_name + build_id`。

## 3. 统一矩阵模式 `unified_matrix`

该模式在同一个 Unity / 团结工程和同一物理分支中保存全部平台适配与全部小游戏变体。推荐默认分支为 `miniGame-unified`；用户可以指定其他明确分支，但不得使用 `miniGame-wx-*` 或 `miniGame-dy-*`。

统一源码不代表统一成品包。微信和抖音仍分别构建；AppID、项目模板、平台 API、CDN 和平台后台配置不得在一个已导出的成品包中运行时切换。生产候选在构建前固定 `variant_id + platform`，运行时仅消费该候选内已解析的配置。

```yaml
delivery_topology:
  mode: unified_matrix
  unified_branch: miniGame-unified

variant_catalog:
  - variant_id: original
    names:
      wechat: <微信原包正式名称>
      douyin: <抖音原包正式名称>
  - variant_id: skin_a
    names:
      wechat: <微信马甲正式名称>
      douyin: <抖音马甲正式名称>

selected_targets:
  - {variant_id: original, platform: wechat}
  - {variant_id: skin_a, platform: douyin}
```

### 3.1 稳定身份

- `variant_id` 是 Git ref 和路径安全的稳定标识，只允许小写字母、数字、点、下划线和连字符，并以字母或数字开头；显示名称不是主键。
- 每个 `variant_id + platform` 组合必须有唯一正式名称。改名不自动改变 `variant_id`。
- 目标键为 `mode + source_commit + variant_id + platform + resolved_profile_sha256 + build_id`。任何一项改变都会产生新候选，旧候选不得继续用于 IDE、录屏或统计证据。

### 3.2 配置叠加

有效配置按固定优先级深度合并，后者覆盖前者：

```text
defaults < variant < platform < variant.platforms[platform]
```

配置至少能够解析：正式名称、非敏感 AppID、广告位、统计配置索引、品牌资源、游戏数据配置、CDN 前缀，以及抖音侧边栏和排行榜配置。SDK Secret、访问令牌、CDN 密钥和私钥不得进入 catalog、解析产物、Git 或回执；它们只能由受保护的本机凭据或构建环境注入。

使用 `python skills/minigame-sdk-core/scripts/resolve_profile.py --catalog <catalog.json> --variant <variant_id> --platform <wechat|douyin> --output <ResolvedMiniGameProfile.json>` 生成规范化配置。构建模板、运行时、资源构建、SDK 初始化和回执必须消费同一份解析结果，并记录其 SHA-256。

### 3.3 源码与资源边界

- 平台差异集中在 `PlatformManager` / 平台适配器和构建模板，不把平台宏散落到业务代码。
- 马甲差异通过稳定配置和资产引用解析，不在业务中按显示名称写条件分支。
- 生产构建只激活一个 `variant_id + platform`。编辑器 QA 可以提供受控选择器，但不得让选择器或其他马甲凭据进入生产入口。
- 构建输出使用 `<output-root>/<variant_id>/<platform>/<build_id>/`，不得让不同组合覆盖同一路径。
- 同一个 Unity 工程的矩阵构建默认串行；如需并行，必须使用相互隔离的工作区和 Library，不得并发改写同一工程状态。

### 3.4 统一分支 Git 边界

统一模式固定使用 `develop -> <unified_branch>`。平台适配、变体配置和共享实现均提交到该分支，但每个规范化小需求仍独立提交。提交必须说明适用范围：`shared`、`wechat`、`douyin` 或具体 `variant_id`，不得用条件配置掩盖未验证的组合。

阶段里程碑按目标组合独立记录，标签格式为：

```text
monetization/<game-slug>/<workflow-id>/<variant-id>/<platform>/<stage>-v<N>
```

阶段回执记录 `develop` SHA、同步 merge commit、统一分支源码 SHA、目标组合、解析配置 SHA、构建 ID、阶段提交和远程核验。多个组合可以来自同一源码 SHA，但各自的构建、门禁、IDE、录屏和统计证据不能共用。

## 4. 模式无关完成规则

- 每个请求目标组合独立推进，某个组合阻塞不自动阻塞其他组合。
- 微信成功不能证明抖音成功；原包成功不能证明马甲成功。
- 后续阶段必须接收并核对前一阶段的完整候选身份。
- 汇总必须按 `目标组合 × 阶段` 报告状态。
- 只有所选模式的 Git 门禁、全部请求组合的 Release 门禁和请求阶段均完成，整体才能报告 `completed`。
