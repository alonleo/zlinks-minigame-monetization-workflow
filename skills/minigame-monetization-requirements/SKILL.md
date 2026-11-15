---
name: minigame-monetization-requirements
description: 独立执行小游戏商业化需求的提取、定位、经验检查、提示词冻结、实现、构建、IDE 验收与按需 CDN 上传。用于完整执行或续做需求实现阶段；不负责过程录屏、统计请求核验或发布。
---

# 商业化需求实现

本 skill 独立完成“从来源需求到可验证构建产物”的闭环。开始修改前先完整读取并遵守项目内的 [小游戏处理标准](../minigame-standard/SKILL.md)；飞书电子表格信息行使用项目内的 [信息表拉取 skill](../minigame-feishu-sheet-fetch/SKILL.md)；广告、构建、平台 IDE 和 CDN 上传等仍路由到实际可用的专项 skill。七牛上传使用项目内的 [Kodo 上传 skill](../minigame-kodo-upload/SKILL.md)。

目标平台包含微信时，还必须完整读取总工作流的[微信开发者工具 Skills 接入规范](../../references/wechat-devtools-skills.md)，并使用本机导出的最新官方 Skill 完成微信 IDE 的打开项目、编译、模拟器操作、截图断言和日志核验。不得把模拟器刷新或旧 console 记录当作本次编译与运行证据。

目标平台包含抖音时，还必须完整读取总工作流的[抖音开发者工具 Skill 接入规范](../../references/douyin-devtools-skill.md)，并使用本机最新官方 Skill 将项目、真实窗口 ID、编译动作、Canvas 截图坐标、操作和状态探针绑定为同一条 `runtimeContext` 证据链。小游戏不使用小程序的元素树、`backendId`、Page data 或页面栈能力。

开始执行前还必须完整读取总工作流的 [Git 基线、平台分支、检查点与推送协议](../../references/git-checkpoints.md)。先把本轮选定的 `develop` 基线同步到 `miniGame`；共享游戏内容在 `miniGame` 修改并逐条提交，验证完成后单向合并到微信 `miniGame-wx` 和/或抖音 `miniGame-dy`，平台专属适配只提交到对应平台分支。自统计接入或改造完成并验证后保留平台独立检查点；对应平台的需求实现条件满足后，在该平台分支创建阶段汇总提交和 annotated tag，并推送、核验远程。Git 门禁失败时不得把对应平台阶段报告为 `completed`。

目标平台包含微信或抖音且工程为 Unity 小游戏时，还必须完整读取并执行[分包与发布候选门禁](references/subpackage-release-gates.md)。该门禁使用本次真实构建产物，不把原始策略文档中的项目路径、历史哈希、示例体积或模拟器结果当成当前证据。

Unity / 团结微信或抖音小游戏完成平台构建后，还必须完整读取并执行[构建后引擎 Logo / 水印处理协议](references/post-build-logo-removal.md)，使用 `D:\Tools\MiniGameKit\remove-logo` 处理本次实际导出目录。处理后的包才进入分包与发布候选门禁；任何重新构建都使旧处理回执失效。

Unity 工程已安装团结「性能 AI 助手」且需求涉及资源、场景、渲染、粒子、热路径、加载流程或明确性能问题时，完整读取并执行[性能 AI 助手接入协议](references/performance-assistant-integration.md)。普通文案、埋点字段、广告位 ID 等低性能风险改动默认不运行全量体检；插件未安装或能力面不适用时记录 `skipped` 及原因，不为普通需求杜撰检查结果。

如本阶段确需新增自动化工具脚本，只能使用 Python。

## 开始条件

至少取得信息表目标游戏行、商业化方案文档、工程与引擎版本、目标平台、信息表 CDN 路径（允许明确为空）以及项目经验文档位置。字段解析、来源安全和规范记录格式见 [输入与需求契约](references/input-contract.md)，执行前完整读取。

## 执行

完整读取 [需求实现执行协议](references/execution-protocol.md)，并按其状态机执行：

1. 使用信息表拉取 skill 读取唯一目标游戏行，再打开该行“商业化方案”文档，建立只读需求基线并逐行生成稳定需求 ID；
2. 结合说明、示意图和工程事实定位实际入口；
3. 读取相关经验文档并把风险转为预处理和回退点；
4. 为每条需求创建版本化冻结实施提示词；
5. 记录选定的 `develop` SHA，将其同步到 `miniGame` 并验证祖先关系；
6. 判断性能检查适用性；适用时在写入项目前基于已同步的 `miniGame` 建立只读基线；
7. 在 `miniGame` 仅按冻结范围实施共享游戏内容，完成编译、适用性能复验和逐需求基线实现检查点；
8. 推送已验证的 `miniGame` 基线，并把该最终基线单向合并到每个目标平台分支；
9. 在 `miniGame-wx` / `miniGame-dy` 分别完成平台专属适配、编译和检查，不把平台专属改动反向合入基线或另一平台；
10. 在对应平台分支完成与改动层级相称的引擎和平台构建，并按需用性能报告做交叉核对；
11. 对微信、抖音 Unity / 团结小游戏分别执行构建后引擎 Logo / 水印处理，固定处理后的导出目录及回执；
12. 对处理后的微信、抖音 Unity 小游戏分别执行分包与发布候选门禁，得到各自唯一最终候选；
13. 仅在产生远程资源且三方路径一致时调用匹配的 CDN 上传 skill，并将公开 CDN 校验并入对应候选门禁；
14. 使用门禁通过的同一候选完成对应平台 IDE 验证；
15. 逐平台形成状态和证据回执；
16. 按 Git 协议完成逐需求平台完成提交、阶段汇总提交、平台化标签与远程推送核验。

## 强制门禁

- 未读取适用经验文档且未记录搜索结果，不进入实现。
- 未冻结实施提示词，不修改代码或资源。
- `miniGame` 未包含本轮选定的 `develop` SHA 时不得开始共享内容修改；本工作流不直接在 `develop` 上实施商业化修改。
- `miniGame-wx` / `miniGame-dy` 不得绕过 `miniGame` 直接合并 `develop`，`miniGame` 的商业化提交也不由本工作流反向合入 `develop`。
- 共享游戏内容不得直接首改在 `miniGame-wx` 或 `miniGame-dy`；平台候选构建前必须证明对应分支包含本轮最终 `miniGame` 基线提交。
- `miniGame-wx` 与 `miniGame-dy` 不得互相合并，平台专属提交不得反向合入 `miniGame`；确需提升为共享能力时，必须在 `miniGame` 独立重新实现和验证。
- 编译失败不进入平台构建；构建失败不报告 IDE 验收通过。
- Unity / 团结微信或抖音构建完成后，未取得 `post_build_logo_removal.verification_result: passed` 不得进入分包与发布候选门禁；构建或重新导出后必须重新处理，不能复用旧回执。
- Unity 游戏端 UI 或场景修改必须用正确版本的 Unity CLI 执行项目可用验证入口并检查本次日志。
- 微信或抖音小游戏端修改必须产生该平台新构建，并由对应平台 IDE 核验。
- 性能检查只在协议判定适用时执行；只读基线或复验不得修改项目，自动修复必须取得单独授权并作为独立需求处理。
- 性能助手产物校验失败、确定性构建场景断引用或有充分证据的阻断级回退不得被普通警告降级；P1/P2 建议、零引用候选和未覆盖项按协议记录，不自动阻断。
- Unity 微信或抖音候选未通过适用的分包与发布候选门禁时，不进入 IDE 录屏，不把需求标记为 `done`。
- WASM、catalog、Bundle、CDN 和 IDE 证据必须指向同一最终候选；Profile、prepare、缓存或中间目录不得作为 Release 证据。
- 模型估算、静态审计和模拟器结果不得表述为真机性能达标。
- CDN 本地源、信息表前缀、工程配置或实际目标不一致时禁止上传。
- 不用静态搜索、旧日志、旧程序集、浏览器预览或单平台结果替代所需运行证据。
- 不合并多个已完成小需求的检查点提交，不把用户已有或其他工程的改动带入提交。

## 阶段回执

输出 `completed | partial | blocked | failed`，并逐需求列出冻结提示词版本、选定的 `develop` SHA、同步到 `miniGame` 的 merge SHA、`miniGame` 基线 SHA、各平台 merge SHA 与 HEAD、修改位置、编译结果、构建标识、性能检查适用性与 `performance_assessment` 回执、各平台 `post_build_logo_removal` 回执、分包与发布候选门禁回执、平台 IDE 证据、CDN 回执、检查点 commit SHA 和未验证范围；每个平台另列本分支的阶段汇总 commit、annotated tag、远程与推送核验。只有 `miniGame` 包含选定的 `develop`、共享基线已推送、所有目标平台分支包含最终小游戏基线、所有适用需求满足协议中的 `done` 定义、适用平台 Logo / 水印处理与候选门禁均为 `passed`，且各平台阶段提交、标签和远程核验全部通过，阶段状态才是 `completed`。
