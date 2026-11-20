---
name: minigame-statistics-verification
description: 独立核查微信或抖音小游戏的统计 SDK 集成、行为埋点及双通道真实网络上报。用于执行或续做数据统计核实阶段；不负责实现业务需求、录屏或发布。
---

# 小游戏数据统计核实

本 skill 用目标平台 IDE 的本次运行网络记录，证明每个待统计行为在必核通道中实际发送、响应成功且字段正确。代码存在调用、SDK 初始化、控制台日志或 HTTP 200 均不能单独证明通过。

目标工程接入小游戏统计运行层时，必须接收[SDK 统计 skill](../minigame-sdk-telemetry/SKILL.md)生成的 `game_sdk_telemetry` 与事件调用点清单，按平台原生、自有服务、微信 dnSDK 三条通道分别核实；回执缺失时不得从源码猜测实际启用通道。

目标平台是微信时，先完整读取总工作流的[微信开发者工具 Skills 接入规范](../../references/wechat-devtools-skills.md)，再使用本机最新官方 debugger scene 取得本次 console/network 记录；具体工具名与筛选参数以本机 `tools.yaml` 为准，不在本 skill 中硬编码或猜测。

目标平台是抖音时，先完整读取总工作流的[抖音开发者工具 Skill 接入规范](../../references/douyin-devtools-skill.md)，再使用本机最新官方 Skill 将真实窗口 ID、操作前 network 基线、行为触发和操作后新增请求绑定为同一条 `runtimeContext`；具体命令和参数以 `douyinide <command> -h` 为准。

开始执行前完整读取总工作流的[交付拓扑与模式路由](../../references/delivery-topologies.md)和[Git 基线、交付拓扑、检查点与推送协议](../../references/git-checkpoints.md)。分支隔离模式把统计证据与自统计检查点提交到平台命名分支；统一矩阵模式提交到统一分支，并以 `variant_id + platform + resolved_profile_sha256 + build_id` 锁定候选。每个目标统计核实完成后按对应模式创建 `statistics` 阶段汇总提交和 annotated tag，推送并核验远程。Git 门禁失败时对应目标不得报告 `completed`。

如本阶段确需新增自动化工具脚本，只能使用 Python。

## 平台通道

- 抖音：自统计服务事件上报 + 抖音官方 SDK 事件上报，使用抖音 IDE 网络监听。
- 微信：自统计服务事件上报 + 腾讯营销 DNSDK 事件上报，使用微信 IDE 网络监听。

平台不明确、产物不匹配或缺少目标平台 IDE 能力时，阶段为 `blocked`；不得改用浏览器网络面板、历史抓包或另一平台证据。

抖音的行为清单无条件包含侧边栏入口点击、平台跳转/复访、适用奖励领取、排行榜成绩上报和排行榜打开；这些行为必须同时核对自统计服务与抖音官方 SDK 通道。QA 操作不记录密码值，CDN 由构建和 IDE 网络证据验证，不把资源请求伪装成业务埋点。

## 执行

完整读取 [统计核实执行协议](references/execution-protocol.md)，然后：

1. 确认平台、构建产物和两个必核上报通道；
2. 读取项目实际采用版本的集成规范，逐项检查 SDK、配置、初始化、调用和平台适配；
3. 沿用户行为入口、封装、常量、配置和平台分支整理“行为 × 通道”埋点表；
4. 在目标平台 IDE 中隔离旧记录，一次触发一个行为并收集本次外部请求与响应；
5. 建立“行为 → 通道 → 事件 → 请求 → 响应”关联，逐字段核对并脱敏保存证据；
6. 按平台汇总通过、失败、阻塞和无法确认项。

## 阶段回执

输出 `completed | partial | blocked | failed`，包括模式、变体、平台、解析配置 SHA、源码分支与提交、`develop` SHA、构建标识、集成规范检查表、埋点统计表、网络采集时间窗、IDE 版本、脱敏请求证据、逐项结论、自统计检查点 commit SHA、阶段汇总 commit、目标化 annotated tag 和远程推送核验。单个目标的待统计行为在全部必核通道中满足通过标准且 Git 门禁通过后才是 `completed`；所有请求目标均完成后，整体阶段才是 `completed`。
