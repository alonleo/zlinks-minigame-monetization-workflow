---
name: minigame-statistics-verification
description: 独立核查微信或抖音小游戏的统计 SDK 集成、行为埋点及双通道真实网络上报。用于执行或续做数据统计核实阶段；不负责实现业务需求、录屏或发布。
---

# 小游戏数据统计核实

本 skill 用目标平台 IDE 的本次运行网络记录，证明每个待统计行为在必核通道中实际发送、响应成功且字段正确。代码存在调用、SDK 初始化、控制台日志或 HTTP 200 均不能单独证明通过。

目标平台是微信时，先完整读取总工作流的[微信开发者工具 Skills 接入规范](../../references/wechat-devtools-skills.md)，再使用本机最新官方 debugger scene 取得本次 console/network 记录；具体工具名与筛选参数以本机 `tools.yaml` 为准，不在本 skill 中硬编码或猜测。

目标平台是抖音时，先完整读取总工作流的[抖音开发者工具 Skill 接入规范](../../references/douyin-devtools-skill.md)，再使用本机最新官方 Skill 将真实窗口 ID、操作前 network 基线、行为触发和操作后新增请求绑定为同一条 `runtimeContext`；具体命令和参数以 `douyinide <command> -h` 为准。

开始执行前完整读取总工作流的 [Git 基线、平台分支、检查点与推送协议](../../references/git-checkpoints.md)。微信统计证据与自统计检查点只提交到 `miniGame-wx`，抖音只提交到 `miniGame-dy`；每个平台统计核实完成后，在对应平台分支创建 `statistics` 阶段汇总提交和平台化 annotated tag，并推送、核验远程。Git 门禁失败时对应平台阶段不得报告 `completed`。

如本阶段确需新增自动化工具脚本，只能使用 Python。

## 平台通道

- 抖音：自统计服务事件上报 + 抖音官方 SDK 事件上报，使用抖音 IDE 网络监听。
- 微信：自统计服务事件上报 + 腾讯营销 DNSDK 事件上报，使用微信 IDE 网络监听。

平台不明确、产物不匹配或缺少目标平台 IDE 能力时，阶段为 `blocked`；不得改用浏览器网络面板、历史抓包或另一平台证据。

## 执行

完整读取 [统计核实执行协议](references/execution-protocol.md)，然后：

1. 确认平台、构建产物和两个必核上报通道；
2. 读取项目实际采用版本的集成规范，逐项检查 SDK、配置、初始化、调用和平台适配；
3. 沿用户行为入口、封装、常量、配置和平台分支整理“行为 × 通道”埋点表；
4. 在目标平台 IDE 中隔离旧记录，一次触发一个行为并收集本次外部请求与响应；
5. 建立“行为 → 通道 → 事件 → 请求 → 响应”关联，逐字段核对并脱敏保存证据；
6. 按平台汇总通过、失败、阻塞和无法确认项。

## 阶段回执

输出 `completed | partial | blocked | failed`，包括平台分支、`develop` SHA、`miniGame` SHA、平台 HEAD、构建标识、集成规范检查表、埋点统计表、网络采集时间窗、IDE 版本、脱敏请求证据、逐项结论、自统计检查点 commit SHA、阶段汇总 commit、平台化 annotated tag 和远程推送核验。单个平台的待统计行为在全部必核通道中满足通过标准且该平台 Git 门禁通过后，平台状态才是 `completed`；所有请求平台均完成后，整体阶段才是 `completed`。
