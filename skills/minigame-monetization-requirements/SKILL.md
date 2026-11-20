---
name: minigame-monetization-requirements
description: 独立执行小游戏商业化需求的提取、定位、经验检查、提示词冻结、实现、Release 构建和 IDE/真机验收；抖音无条件、其他平台在存在远程资源时强制上传 CDN。用于完整执行或续做需求实现阶段；不负责过程录屏、统计请求核验或发布。
---

# 商业化需求实现

本 skill 独立完成“从来源需求到可验证构建产物”的闭环。开始修改前先完整读取并遵守项目内的 [小游戏处理标准](../minigame-standard/SKILL.md)；飞书电子表格信息行使用项目内的 [信息表拉取 skill](../minigame-feishu-sheet-fetch/SKILL.md)；平台 Release 必须使用项目内的[小游戏 Release 构建 skill](../minigame-build-release/SKILL.md)，平台 IDE 仍路由到对应官方 skill。七牛上传使用项目内的 [Kodo 上传 skill](../minigame-kodo-upload/SKILL.md)。

目标平台包含微信时，还必须完整读取总工作流的[微信开发者工具 Skills 接入规范](../../references/wechat-devtools-skills.md)，并使用本机导出的最新官方 Skill 完成微信 IDE 的打开项目、编译、模拟器操作、截图断言和日志核验。不得把模拟器刷新或旧 console 记录当作本次编译与运行证据。

目标平台包含抖音时，还必须完整读取总工作流的[抖音开发者工具 Skill 接入规范](../../references/douyin-devtools-skill.md)，并使用本机最新官方 Skill 将项目、真实窗口 ID、编译动作、Canvas 截图坐标、操作和状态探针绑定为同一条 `runtimeContext` 证据链。小游戏不使用小程序的元素树、`backendId`、Page data 或页面栈能力。

目标平台包含抖音时，必须完整读取小游戏处理标准的[抖音小游戏四项必接能力](../minigame-standard/references/douyin-mandatory-capabilities.md)，并把侧边栏复访、运行时 QA 面板、CDN 和抖音原生排行榜纳入冻结需求、平台适配、构建、IDE/真机验收和阶段回执。四项无 `not-applicable` 例外。

目标平台包含抖音且工程存在视频广告时，还必须完整读取小游戏处理标准的[图标素材、抖音规范与排查流程](../minigame-standard/references/rewarded-video-icons.md)，生成全量视频广告入口清单，在当前模式的源码分支中以抖音条件或抖音平台适配实现官方标识，并取得 `douyin_video_ad_icon_audit` 静态与 IDE 回执。该门禁不是仅在来源需求提到 icon 时才适用；项目自有、用户自绘或本仓库候选图不能替代官方素材。

开始执行前还必须完整读取总工作流的[交付拓扑与模式路由](../../references/delivery-topologies.md)和[Git 基线、交付拓扑、检查点与推送协议](../../references/git-checkpoints.md)，运行拓扑校验器并固定唯一目标列表。分支隔离模式执行既有 `develop -> miniGame -> 平台命名分支`；统一矩阵模式执行 `develop -> unified_branch`，在同一源码分支完成公共、平台和变体实现，并为每个 `variant_id + platform` 生成解析配置。自统计、阶段回执、annotated tag 和远程核验按所选模式独立完成；Git 门禁失败时不得把受影响目标报告为 `completed`。

目标平台包含微信或抖音且工程为 Unity 小游戏时，按职责完整读取并执行三个独立支持 skill：[资源运行时](../minigame-resource-runtime/SKILL.md)负责异步资源服务与 UI/场景/战斗接入，[资源构建与分包](../minigame-resource-packaging/SKILL.md)负责包归属、依赖、Catalog/Bundle 和首包审计，[启动与迁移验收](../minigame-startup-verification/SKILL.md)负责同一正式候选的启动与回归证据。随后执行[分包与发布候选门禁](references/subpackage-release-gates.md)汇总独立回执。即使当前需求未改资源，也必须判断三项适用性；已有实现可审计后复用回执，但不能因存在旧代码或历史报告而跳过本次候选核验。

同一工程还必须按职责执行 [SDK Core](../minigame-sdk-core/SKILL.md)、[SDK 广告](../minigame-sdk-ads/SKILL.md)、[SDK 统计](../minigame-sdk-telemetry/SKILL.md)、[SDK 平台能力](../minigame-sdk-platform-capabilities/SKILL.md)和[SDK 构建模板](../minigame-sdk-build-templates/SKILL.md)。分支隔离模式保持 Core 位于 `miniGame`、平台能力位于命名分支；统一矩阵模式全部位于统一分支，但平台实现仍通过适配器和构建目标隔离，变体通过解析配置隔离。已有项目能力优先合并，不能重复铺设同名类型或覆盖用户配置。

Unity / 团结微信或抖音小游戏完成平台构建后，还必须完整读取并执行[构建后引擎 Logo / 水印处理协议](references/post-build-logo-removal.md)，使用 `D:\Tools\MiniGameKit\remove-logo` 处理本次实际导出目录。处理后的包才进入分包与发布候选门禁；任何重新构建都使旧处理回执失效。

Unity 工程已安装团结「性能 AI 助手」且需求涉及资源、场景、渲染、粒子、热路径、加载流程或明确性能问题时，完整读取并执行[性能 AI 助手接入协议](references/performance-assistant-integration.md)。普通文案、埋点字段、广告位 ID 等低性能风险改动默认不运行全量体检；插件未安装或能力面不适用时记录 `skipped` 及原因，不为普通需求杜撰检查结果。

所有 Unity / 团结小游戏目标平台均完整读取并执行小游戏处理标准的[Unity 小游戏 QA 面板接入规范](../minigame-standard/references/qa-panel.md)。QA 命令服务、五连击密码入口和项目字体适配在当前模式的共享源码分支实现，并包含在所有目标平台的正式发布包中；各平台必须在同一最终 Release 候选验证入口、密码、授权策略和面板可用性。

如本阶段确需新增自动化工具脚本，只能使用 Python。

## 开始条件

至少取得信息表目标游戏行、商业化方案文档、工程与引擎版本、目标平台、信息表 CDN 路径（抖音必填，其他平台允许明确为空）以及项目经验文档位置。字段解析、来源安全和规范记录格式见 [输入与需求契约](references/input-contract.md)，执行前完整读取。

## 执行

完整读取 [需求实现执行协议](references/execution-protocol.md)，并按其状态机执行：

1. 使用信息表拉取 skill 读取唯一目标游戏行，再打开该行“商业化方案”文档，建立只读需求基线并逐行生成稳定需求 ID；目标包含抖音时另行加入 `DY-MANDATORY-SIDEBAR`、`DY-MANDATORY-QA`、`DY-MANDATORY-CDN`、`DY-MANDATORY-RANK` 四条标准需求；
2. 结合说明、示意图和工程事实定位实际入口；
3. 读取相关经验文档并把风险转为预处理和回退点；Unity 微信或抖音小游戏分别执行资源运行时、资源构建与分包、启动验收的适用性判断，并把适用 skill 的要求和回执纳入冻结提示词；
4. 为每条需求创建版本化冻结实施提示词；
5. 固定 `delivery_topology.mode` 和规范化目标列表，记录选定的 `develop` SHA，并按对应 Git 协议同步到 `miniGame` 或统一分支，验证祖先关系；
6. 判断性能检查适用性；适用时在写入项目前基于已同步的当前模式源码分支建立只读基线；
7. 在当前模式规定的分支调用 SDK Core，实施公共游戏内容、适用的资源运行时及全平台 QA；统一矩阵模式同时建立稳定 `variant_id` catalog、解析 Provider 和平台适配边界；完成编译、性能复验和检查点；
8. 分支隔离模式推送 `miniGame` 并合入各平台分支；统一矩阵模式推送统一分支，不创建平台命名分支，并对每个目标生成 `ResolvedMiniGameProfile` 与 SHA-256；
9. 在当前模式的源码分支调用 SDK 广告、SDK 统计和 SDK 平台能力，完成真实业务入口、事件、生命周期和平台能力接线；统一模式必须同时安装微信和抖音适用组件，但生产构建每次只激活一个平台与变体；
10. 对每个规范化目标先调用资源构建与分包、SDK 构建模板，再调用小游戏 Release 构建；统一模式的构建输出固定为 `<root>/<variant_id>/<platform>/<build_id>` 并消费同一解析配置；抖音模板不得保留活动 WASM 函数分包加载器；完成图片预算、去水印和适用 CDN；
11. 对每个平台的同一最终候选调用启动与迁移验收 skill；没有真机或明确缓存控制时如实记录 `estimated-only | partial | blocked`，不得用体积估算冒充启动通过；
12. 对 `build_release.result: passed` 的微信、抖音候选分别执行分包与发布候选门禁，汇总 `resource_runtime`、`resource_packaging`、`startup_verification` 和构建回执，得到各自唯一最终候选；抖音必须确认运行时 QA、侧边栏和排行榜均进入正式候选；
13. 使用门禁通过的同一候选完成对应平台 IDE 验证；抖音在该候选验证 QA 入口、侧边栏入口与状态、排行榜上报与原生 UI，并取得本流程要求的侧边栏和排行榜真机证据；
14. 逐目标组合形成状态和证据回执；
15. 按对应模式的 Git 协议完成逐需求验证提交、阶段汇总提交、目标化标签与远程推送核验。

## 强制门禁

- 未读取适用经验文档且未记录搜索结果，不进入实现。
- 未冻结实施提示词，不修改代码或资源。
- 当前模式的源码分支未包含本轮选定的 `develop` SHA 时不得开始修改；本工作流不直接在 `develop` 上实施商业化修改。
- 分支隔离模式必须遵守 `develop -> miniGame -> 平台命名分支`，共享内容不得首改在平台分支，两个平台分支不得互相合并。
- 统一矩阵模式不得创建或借用平台命名分支，不得把其他模式的分支合入统一分支；平台和变体差异必须通过适配器、构建模板和稳定配置隔离。
- 统一模式未取得唯一 `variant_id + platform + resolved_profile_sha256`，解析配置包含敏感字段，或解析平台与实际构建平台不一致时不得构建。
- 编译失败不进入平台构建；构建失败不报告 IDE 验收通过。
- 平台 Release 未使用项目内构建 skill、WASM 函数分包未明确关闭、发现任何活动函数分包产物，或生成后主包任一 `.png`、`.jpg`、`.jpeg`、`.webp` 大于等于 `512000` 字节时，不得进入候选门禁。
- Unity / 团结微信或抖音构建完成后，未取得 `post_build_logo_removal.verification_result: passed` 不得进入分包与发布候选门禁；构建或重新导出后必须重新处理，不能复用旧回执。
- 构建产生远程资源时，未完成精确目录上传和公开逐文件 SHA-256 校验不得进入平台 IDE；仅非抖音平台没有远程资源时，才可以构建清单证明并记录 `cdn_upload_status: not-applicable`。
- 抖音不接受 `cdn_upload_status: not-applicable`；缺少真实远程资源目录、不可变清单、明确 CDN 目标、上传或公开逐文件 SHA-256 验证时，抖音候选失败关闭。
- 抖音未按当前规范完成首页侧边栏入口、`GoToAdd/CanClaim/Claimed` 状态机、复访识别、奖励幂等及当前候选真机往返，不得进入 `done`。
- 抖音未完成后台排行榜配置、真实成绩上报、打开前同步最新成绩、原生排行榜 UI 和当前候选真机验证，不得进入 `done`；不得用模拟榜单或微信开放数据域代码替代。
- Unity 游戏端 UI 或场景修改必须用正确版本的 Unity CLI 执行项目可用验证入口并检查本次日志。
- 每个所选 `平台 + 变体` 修改必须产生该目标的新构建，并由对应平台 IDE 核验；单个平台或原包结果不得证明其他组合。
- 任一目标平台未在最终 Release 候选取得五连击边界、确定/取消、空密码与错误密码提示、正确密码进入面板、高权限命令授权、项目字体与字符覆盖、输入不穿透及默认流程无干扰结果，不得把 QA 接入标记完成。回执、日志、截图和录屏不得包含明文密码。
- 抖音工程存在视频广告时，未取得 `douyin_video_ad_icon_audit.static_result: passed` 不得构建最终抖音候选；未取得同一候选的 `ide_result: passed` 不得把抖音需求标记为 `done`。审计必须从真实广告调用反查全部入口，不能用文件名搜索、单张截图或一个示例按钮代替全局闭环。
- 性能检查只在协议判定适用时执行；只读基线或复验不得修改项目，自动修复必须取得单独授权并作为独立需求处理。
- 性能助手产物校验失败、确定性构建场景断引用或有充分证据的阻断级回退不得被普通警告降级；P1/P2 建议、零引用候选和未覆盖项按协议记录，不自动阻断。
- Unity 微信或抖音候选未通过适用的分包与发布候选门禁时，不进入 IDE 录屏，不把需求标记为 `done`。
- WASM、catalog、Bundle、CDN 和 IDE 证据必须指向同一最终候选；Profile、prepare、缓存或中间目录不得作为 Release 证据。
- 模型估算、静态审计和模拟器结果不得表述为真机性能达标。
- CDN 本地源、信息表前缀、工程配置或实际目标不一致时禁止上传。
- 不用静态搜索、旧日志、旧程序集、浏览器预览或单平台结果替代所需运行证据。
- 不合并多个已完成小需求的检查点提交，不把用户已有或其他工程的改动带入提交。

## 阶段回执

输出 `completed | partial | blocked | failed`，并列出模式、规范化目标、Git 基线、统一模式的解析配置 SHA、修改与编译、三个资源回执、五个 SDK 回执、`build_release`、无函数分包、图片预算、性能、Logo、水印、分包门禁、平台 IDE/真机、CDN、提交和未验证范围。只有每个目标适用的资源、SDK、Release、Logo、候选门禁和抖音四项能力全部通过，且对应模式的 Git 里程碑远程核验完成，阶段状态才是 `completed`。
