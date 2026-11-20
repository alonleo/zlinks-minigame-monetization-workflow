---
name: minigame-monetization-workflow
description: 编排小游戏商业化完整流程，按用户选择在平台/马甲分支隔离或单分支统一矩阵模式下协调需求实现、目标平台 IDE 录屏和双通道统计核实。用于执行或续做完整流程；单独阶段应直接使用对应阶段 skill。
---

# 小游戏商业化总协调

本 skill 只负责编排，不直接实施需求、控制录屏或核验统计。三个执行阶段各自是独立 skill，可单独调用、失败、续做和交付：

1. [需求实现 skill](skills/minigame-monetization-requirements/SKILL.md)
2. [IDE 游戏过程录屏 skill](skills/minigame-ide-gameplay-recording/SKILL.md)
3. [数据统计核实 skill](skills/minigame-statistics-verification/SKILL.md)

用户要求完整商业化流程时，按上述顺序协调；用户只要求一个阶段时，直接路由到对应 skill，不加载其余阶段。开始任何小游戏修改前，需求实现 skill 必须先完整读取并使用项目内的 [小游戏处理标准](skills/minigame-standard/SKILL.md)。本工作流明确包含下述 Git 提交、阶段标签与推送，不因此扩大为审核、上架或正式发布授权。

开始执行前完整读取[交付拓扑与模式路由](references/delivery-topologies.md)，取得 `delivery_topology.mode` 并运行其中的输入校验器；随后完整读取[Git 基线、检查点与推送协议](references/git-checkpoints.md)中对应模式的章节。`isolated_branches` 保持 `develop -> miniGame -> 平台/马甲命名分支`；`unified_matrix` 使用 `develop -> unified_branch`，在同一分支保存全部平台适配与变体，并按 `variant_id + platform` 分别生成候选。单次 `workflow_id` 只允许一种模式，中途切换必须建立新运行。每个验证通过的小需求、自统计检查点和大阶段里程碑仍须可追踪并推送；缺少对应模式的 Git 门禁时不能报告 `completed`。

项目内的支持能力包括：信息表拉取、小游戏处理标准、资源运行时、资源构建与分包、启动验收、SDK Core、SDK 广告、SDK 统计、SDK 平台能力、SDK 构建模板、Release 构建与 Kodo 上传。对应入口位于 `skills/` 下，均可单独调用、失败和续做，但不增加商业化流程的大阶段计数。

Unity 工程已安装团结「性能 AI 助手」时，需求实现阶段按[性能 AI 助手接入协议](skills/minigame-monetization-requirements/references/performance-assistant-integration.md)判断是否执行修改前基线、编译后定向检查或构建后交叉核对。该能力是条件式质量检查器，不增加第四个大阶段；编辑器、Android 或 iOS 结论不得替代微信/抖音最终候选的分包门禁、平台 IDE 或真机证据。

所有 Unity / 团结小游戏目标平台均按[Unity 小游戏 QA 面板接入规范](skills/minigame-standard/references/qa-panel.md)在需求实现阶段内交付共享命令层、指定位置五连击密码入口、生产授权策略和项目字体适配。运行时 QA 面板必须包含在所有目标平台的正式发布包中，并在同一最终候选验证可打开；该能力不增加第四个大阶段。

目标平台包含抖音时，需求实现阶段还必须完整执行[抖音小游戏四项必接能力](skills/minigame-standard/references/douyin-mandatory-capabilities.md)。侧边栏复访、运行时 QA 面板、CDN 和抖音原生排行榜均为无条件门禁；四项必须绑定同一最终候选，任一项缺失时抖音阶段不得报告 `completed`。

Unity / 团结微信或抖音小游戏按职责调用三个独立 skill：资源异步化或业务入口改造使用[资源运行时](skills/minigame-resource-runtime/SKILL.md)，包归属、依赖闭包、Catalog/Bundle、首包审计和远程发布集合使用[资源构建与分包](skills/minigame-resource-packaging/SKILL.md)，同一正式候选的启动时序、缓存口径和迁移回归使用[启动与迁移验收](skills/minigame-startup-verification/SKILL.md)。三者分别形成回执，不得用一个回执替代另两个。所有正式候选仍必须关闭 WASM 函数分包。

Unity / 团结微信或抖音小游戏还按职责调用五个 SDK skill：[SDK Core](skills/minigame-sdk-core/SKILL.md)接入公共门面、桥接、持久化、默认字体和统一矩阵的解析配置 Provider；[SDK 广告](skills/minigame-sdk-ads/SKILL.md)、[SDK 统计](skills/minigame-sdk-telemetry/SKILL.md)与[SDK 平台能力](skills/minigame-sdk-platform-capabilities/SKILL.md)在当前模式的源码分支完成真实业务接线；[SDK 构建模板](skills/minigame-sdk-build-templates/SKILL.md)在 Release 前消费同一解析配置并处理平台模板。五项分别形成回执，隔离测试不得替代当前候选的平台证据。

Unity / 团结微信或抖音小游戏的平台构建必须调用项目内的[小游戏 Release 构建 skill](skills/minigame-build-release/SKILL.md)。每个新候选必须关闭 WASM 函数分包，生成后的主包图片必须逐张严格小于 `512000` 字节，并按[构建后引擎 Logo / 水印处理协议](skills/minigame-monetization-requirements/references/post-build-logo-removal.md)调用 `D:\Tools\MiniGameKit\remove-logo`；抖音无条件、其他平台在存在远程资源时，必须上传 CDN 并完成公开逐文件哈希验证。任一门禁未通过时不得把候选交给后续阶段。

平台 IDE 操作统一使用对应官方 Skill：微信目标完整读取[微信开发者工具 Skills 接入规范](references/wechat-devtools-skills.md)，抖音目标完整读取[抖音开发者工具 Skill 接入规范](references/douyin-devtools-skill.md)，再读取本机开发者工具导出的最新官方 Skill。官方 Skill 负责项目窗口、模拟器、编译、交互和诊断；本仓库录屏脚本负责连续视频证据。两平台命令、鉴权、项目类型和完成判定不得混用。

流程中如确需新增自动化工具脚本，只能使用 Python；不得改用其他脚本语言。

## 协调输入

建立一次流程运行记录，至少包含：

```yaml
workflow_id: MONETIZATION-<stable-id>
game: <stable game identifier>
project_path: <absolute project path>
git_remote: <explicit writable remote>
delivery_topology:
  mode: isolated_branches | unified_matrix
  unified_branch: miniGame-unified # 仅 unified_matrix
branch_targets: [] # 仅 isolated_branches；元素为 platform/platform_game_name/branch
variant_catalog: [] # 仅 unified_matrix；稳定 variant_id 与各平台正式名称
selected_targets: [] # 仅 unified_matrix；元素为 variant_id/platform
source_spreadsheet: <spreadsheet and row identifier>
commercialization_document: <document identifier>
target_platforms: [wechat | douyin]
cdn_prefix: <exact source value; douyin required, other platforms may be null>
release_gates:
  wasm_function_split: disabled
  main_package_image_limit_bytes: 512000
  post_build_logo_removal: required
  cdn_upload: required-when-remote-resources-exist
  douyin_cdn_upload: required
douyin_required_capabilities: [sidebar, qa_panel, cdn, leaderboard]
requested_stages: [requirements, recording, statistics]
```

不得猜测模式、游戏行、工程、目标平台、变体、商业化文档或 CDN 前缀。未指定模式时按兼容规则显式记录 `isolated_branches`；某项缺失只阻塞依赖它的目标或阶段，其余目标如有充分输入可继续。

## 编排协议

### 1. 需求实现

完整读取并执行 `skills/minigame-monetization-requirements/SKILL.md`。接收其阶段回执，至少保留：

- 需求基线版本与逐条状态；
- 选定的 `develop` SHA，以及同步到 `miniGame` 的 merge commit；
- 当前模式的源码分支、基线提交和同步 merge commit；统一模式还保留 `variant_id + platform + resolved_profile_sha256`；
- 逐条完成与验证提交的 commit SHA；
- 实际构建产物路径、版本或哈希；
- 各平台 `build_release` 回执、无函数分包证据及主包图片最大文件与精确字节数；
- 各平台构建后 `post_build_logo_removal` 回执、处理后哈希与限制；
- 适用需求的 `performance_assessment` 回执及未覆盖范围；
- 各平台分包与发布候选门禁回执；
- 适用的 `resource_runtime`、各平台 `resource_packaging` 和 `startup_verification` 独立回执；
- `game_sdk_core` 及各平台 `game_sdk_ads`、`game_sdk_telemetry`、`game_sdk_platform`、`game_sdk_build_template` 回执；
- 各目标平台的构建与 IDE 核验证据；
- 抖音工程存在视频广告时的 `douyin_video_ad_icon_audit` 入口清单、静态结果与同一最终候选的 IDE 结果；
- 抖音 `douyin_required_capabilities` 回执：侧边栏真机往返、正式候选 QA、CDN 公开验证及原生排行榜上报/展示结果；
- CDN 回执及尚未覆盖项；
- 各目标组合的阶段汇总 commit、模式规定的 annotated tag 与远程推送核验。

只有该 skill 的回执状态为 `completed`，才自动进入后续依赖新构建产物的阶段。`partial` 或 `blocked` 时，仍可执行与失败项无关且用户明确要求的后续核验，但必须使用已确认产物并披露依赖缺口。

微信或抖音 Unity 小游戏的新构建还必须由需求实现阶段执行[分包与发布候选门禁](skills/minigame-monetization-requirements/references/subpackage-release-gates.md)。每个平台只把门禁 `passed` 的最终候选传给 IDE 录屏与统计核实；`partial`、`blocked`、`failed` 或仅有估算结果的候选不得被后续阶段静默提升为可发布成品。

### 2. IDE 游戏过程录屏

对每个规范化目标组合分别完整读取并执行 `skills/minigame-ide-gameplay-recording/SKILL.md`。向其传递模式、源码提交、统一模式的 `variant_id` 与解析配置哈希、已确认的构建路径、构建标识、需求验收点和平台；抖音必须同时传递 `douyin_required_capabilities`，存在视频广告时再传递 `douyin_video_ad_icon_audit` 全入口清单。不得用一个入口、平台、变体或旧候选的录像证明其他目标。

### 3. 数据统计核实

对每个规范化目标组合分别完整读取并执行 `skills/minigame-statistics-verification/SKILL.md`。向其传递完整候选身份、本次构建产物、待核行为、平台和录屏阶段记录的实际操作序列；抖音行为必须包含侧边栏与排行榜。统计核实必须重新取得自己的网络证据，不能把录像或控制台日志当成请求证据。

每个目标组合的大阶段条件满足后，仍需在当前模式的源码分支按 Git 协议写入阶段回执、创建里程碑提交和标签并推送；只有远程核验通过才接力到下一阶段。分支隔离模式继续执行两级同步；统一矩阵模式只同步到统一分支，并用目标组合和解析配置哈希隔离候选。小需求点提交不要求立即逐个推送，由所属同步点或大阶段完成时统一推送。

## 阶段接力规则

- 阶段间只传递已验证事实、稳定标识和证据位置，不把推测提升为事实。
- 每个阶段维护自己的状态：`not-started | running | completed | partial | blocked | failed | not-requested`。
- 后一阶段发现前一阶段产物或事实不一致时，停止受影响目标并退回对应阶段修正；不得在后一阶段静默修改前一阶段的基线、解析配置或回执。
- 分支隔离模式按平台正式名称分支推进；统一矩阵模式按 `variant_id + platform` 推进但共享同一源码分支。任一目标阻塞不自动阻塞其他目标。
- 外部信息表和文档默认只读；没有明确授权不得回写状态、评论或链接。
- 每个工程和模式分别记录源码提交、标签和推送。统一模式允许条件化的平台实现位于同一提交，但阶段证据与标签仍按目标组合隔离。

## 总体完成定义

汇总时按“阶段 × 目标组合”报告模式、`develop` SHA、源码分支与 SHA、分支隔离模式的基线/合并关系或统一模式的 `variant_id + platform + resolved_profile_sha256`、输入、输出、证据、阶段标签、推送核验和缺口。只有源码分支包含本轮 `develop` 基线、所有目标 `build_release.result: passed`、抖音必接能力通过、所有请求阶段由独立 skill 判定为 `completed`，且对应模式的远程分支与标签核验通过，才能报告整个流程完成。任一目标或阶段为 `partial`、`blocked` 或 `failed`，候选身份不一致，构建仍有函数分包、超限图片、未通过的去水印或必需 CDN 上传，抖音必接能力未通过，或 Git 里程碑未完整落到远程时，只能报告部分完成并给出准确续做动作。
