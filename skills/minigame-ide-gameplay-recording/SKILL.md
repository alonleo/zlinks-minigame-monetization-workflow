---
name: minigame-ide-gameplay-recording
description: 独立在微信或抖音开发者工具中录制指定小游戏构建的可追溯游戏过程，生成 MP4、会话记录和媒体回执。用于执行或续做 IDE 模拟器录屏；不负责实现需求、统计核验或真机验收。
---

# IDE 游戏过程录屏

本 skill 只录制目标平台 IDE 当前打开的指定构建产物。录像是 IDE 模拟器证据，不替代真机、真实广告、平台登录、兼容性或性能验收。

目标平台是微信时，先完整读取总工作流的[微信开发者工具 Skills 接入规范](../../references/wechat-devtools-skills.md)，使用本机导出的最新官方 automator/debugger scene 打开并驱动模拟器、记录逐步操作和断言；`scripts/ide_recording.py` 仍负责连续画面录制、封装、媒体探测和哈希回执。官方截图不能替代要求连续过程的 MP4，MP4 也不能替代可查询的操作断言和日志。

目标平台是抖音时，先完整读取总工作流的[抖音开发者工具 Skill 接入规范](../../references/douyin-devtools-skill.md)，使用本机最新官方 Skill 建立同一窗口的 `runtimeContext`，以操作前截图和状态探针为基线，通过换算后的 CSS 坐标驱动小游戏 Canvas，并保存操作后截图与探针增量。`scripts/ide_recording.py` 同样独立负责连续视频回执。

开始执行前完整读取总工作流的 [Git 基线、平台分支、检查点与推送协议](../../references/git-checkpoints.md)。微信录屏证据只提交到 `miniGame-wx`，抖音录屏证据只提交到 `miniGame-dy`；每个平台录屏通过后，在对应平台分支创建 `recording` 阶段汇总提交和平台化 annotated tag，并推送、核验远程。Git 门禁失败时对应平台阶段不得报告 `completed`。

如需修改或新增录屏辅助脚本，只能使用 Python。

## 开始条件

取得目标平台、构建绝对路径及标识、IDE 及版本、画面范围、分辨率、帧率、声音要求、可重复操作步骤、关键验收点和敏感区域。平台或构建不明确时不得开始录制。

## 执行

完整读取 [录屏执行协议](references/execution-protocol.md)，然后：

1. 冻结录制清单和逐步操作脚本；
2. 使用 `scripts/ide_recording.py` 预检 FFmpeg、FFprobe 和唯一目标窗口；
3. 微信或抖音平台按对应官方 Skill 完成一次状态、登录、项目和模拟器预检；
4. 按清单复位账号、缓存、存档、网络和关卡前置状态；
5. 启动录制并保存会话文件后，才通过目标平台 IDE skill 执行操作步骤；
6. 记录每步时间、工具调用、断言结果、重试和异常；
7. 停止录制，核验 MP4、日志、时长、关键画面、媒体信息和 SHA-256；
8. 各平台分别交付，不互相替代。

不得为了画面顺畅跳过清单要求的真实行为。窗口匹配不唯一、构建无法确认、敏感信息泄露、关键步骤不可见或回执缺失时，不得标记通过。

## 阶段回执

输出 `completed | partial | blocked | failed`，并包含录制清单、平台、平台分支、`develop` SHA、`miniGame` SHA、平台 HEAD、构建路径及标识、IDE 版本、逐步执行记录、MP4、会话与媒体回执、文件哈希、阶段汇总 commit、平台化 annotated tag、远程推送核验，以及声音、真机、广告和网络环境等未覆盖项。
