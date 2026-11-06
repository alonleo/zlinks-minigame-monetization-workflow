---
name: minigame-monetization-workflow
description: 编排小游戏商业化完整流程，在需求实现、目标平台 IDE 游戏过程录屏、双通道数据统计核实三个独立 skill 之间传递输入、证据和状态。用于执行或续做完整商业化流程；单独执行某一阶段时应直接使用对应阶段 skill。
---

# 小游戏商业化总协调

本 skill 只负责编排，不直接实施需求、控制录屏或核验统计。三个执行阶段各自是独立 skill，可单独调用、失败、续做和交付：

1. [需求实现 skill](skills/minigame-monetization-requirements/SKILL.md)
2. [IDE 游戏过程录屏 skill](skills/minigame-ide-gameplay-recording/SKILL.md)
3. [数据统计核实 skill](skills/minigame-statistics-verification/SKILL.md)

用户要求完整商业化流程时，按上述顺序协调；用户只要求一个阶段时，直接路由到对应 skill，不加载其余阶段。开始任何小游戏修改前，需求实现 skill 必须先完整读取并使用项目内的 [小游戏处理标准](skills/minigame-standard/SKILL.md)。本工作流明确包含下述 Git 提交、阶段标签与推送，不因此扩大为审核、上架或正式发布授权。

对处理的每个工程，开始执行前完整读取并遵守 [Git 检查点、阶段标签与推送协议](references/git-checkpoints.md)。每个已完成且验证通过的小需求点必须独立提交；每个平台的自统计处理完成后必须有独立可追踪提交；每个大阶段完成后必须创建阶段汇总提交和 annotated tag，并立即推送当前分支及该标签。缺少任一要求时，该阶段不能报告 `completed`。

项目内还包含三个被执行阶段按需调用的支持 skill：[飞书电子表格信息拉取](skills/minigame-feishu-sheet-fetch/SKILL.md)、[小游戏处理标准](skills/minigame-standard/SKILL.md) 与 [七牛 Kodo 上传](skills/minigame-kodo-upload/SKILL.md)。它们不是额外流程阶段，不纳入阶段完成计数。

流程中如确需新增自动化工具脚本，只能使用 Python；不得改用其他脚本语言。

## 协调输入

建立一次流程运行记录，至少包含：

```yaml
workflow_id: MONETIZATION-<stable-id>
game: <stable game identifier>
project_path: <absolute project path>
git_remote: <explicit writable remote>
git_branch: <current non-detached branch>
source_spreadsheet: <spreadsheet and row identifier>
commercialization_document: <document identifier>
target_platforms: [wechat | douyin]
cdn_prefix: <exact source value or null>
requested_stages: [requirements, recording, statistics]
```

不得猜测游戏行、工程、目标平台、商业化文档或 CDN 前缀。某项缺失只阻塞依赖它的阶段；其余阶段如有充分输入可继续。

## 编排协议

### 1. 需求实现

完整读取并执行 `skills/minigame-monetization-requirements/SKILL.md`。接收其阶段回执，至少保留：

- 需求基线版本与逐条状态；
- 逐条完成与验证提交的 commit SHA；
- 实际构建产物路径、版本或哈希；
- 各目标平台的构建与 IDE 核验证据；
- CDN 回执及尚未覆盖项；
- 阶段汇总 commit、annotated tag 与远程推送核验。

只有该 skill 的回执状态为 `completed`，才自动进入后续依赖新构建产物的阶段。`partial` 或 `blocked` 时，仍可执行与失败项无关且用户明确要求的后续核验，但必须使用已确认产物并披露依赖缺口。

### 2. IDE 游戏过程录屏

对每个目标平台分别完整读取并执行 `skills/minigame-ide-gameplay-recording/SKILL.md`。向其传递已确认的构建路径、构建标识、需求验收点和平台；不得用一个平台的录像证明另一个平台。

### 3. 数据统计核实

对每个目标平台分别完整读取并执行 `skills/minigame-statistics-verification/SKILL.md`。向其传递本次构建产物、待核行为、平台和录屏阶段记录的实际操作序列。统计核实必须重新取得自己的网络证据，不能把录像或控制台日志当成请求证据。

每个大阶段的业务与验证条件满足后，仍需按 Git 协议写入阶段回执、创建里程碑提交和标签并推送；只有远程核验通过才接力到下一阶段。小需求点提交不要求立即逐个推送，由所属大阶段完成时统一推送。

## 阶段接力规则

- 阶段间只传递已验证事实、稳定标识和证据位置，不把推测提升为事实。
- 每个阶段维护自己的状态：`not-started | running | completed | partial | blocked | failed | not-requested`。
- 后一阶段发现前一阶段产物或事实不一致时，停止受影响分支并退回对应阶段修正；不得在后一阶段静默修改前一阶段的基线或回执。
- 每个平台独立推进。微信分支阻塞不自动阻塞抖音分支，反之亦然。
- 外部信息表和文档默认只读；没有明确授权不得回写状态、评论或链接。
- 每个工程独立提交、打标签和推送；不得把多个工程的改动或里程碑混在同一提交或标签中。

## 总体完成定义

汇总时按“阶段 × 平台”报告状态、输入、输出、证据、commit SHA、阶段标签、推送核验和缺口。只有所有 `requested_stages` 在所有目标平台适用的分支都由对应独立 skill 判定为 `completed`，且各阶段远程分支与标签核验通过，才能报告整个商业化流程完成。任一阶段为 `partial`、`blocked` 或 `failed`，或 Git 里程碑未完整落到远程时，只能报告流程部分完成，并明确下一步应续做哪个 skill 或 Git 动作。
