# 微信开发者工具 Skills 接入规范

本仓库的微信 IDE 操作统一接入微信官方[开发者工具 Skills](https://developers.weixin.qq.com/miniprogram/dev/devtools/Skills.html)。它用于打开项目、控制模拟器、编译、读取 console/network、截图与断言；本仓库已有的 FFmpeg 录屏脚本仍负责生成连续视频和媒体回执。两条证据链互补，不能互相替代。

本文依据官方页面于 2026-11-10 核对。执行时必须以本机开发者工具导出的最新 skill、工具注册表和参数 schema 为准，不把本文示例当成固定 API 清单。

## 1. 安装与运行时依赖

1. 安装官方文档要求的微信开发者工具版本；当前页面给出的最低版本是 Nightly Electron Build `2.02.2607032`。
2. 在终端执行 `wechatide`，取得本机导出的 Skill 路径；也可从开发者工具菜单“导出开发者工具 Skill”导出。
3. Agent 在执行微信阶段前完整读取导出目录的根 `SKILL.md`、`skill.yaml`，以及当前任务对应的 scene 文档。工具名和参数必须从 `miniprogram-tools/references/tools.yaml` 的 `inputSchema` 解析，不得凭本文或历史记录编造。
4. 官方 Skill 是随本机开发者工具更新的运行时依赖，不复制进本仓库。回执只记录版本、来源路径和必要的脱敏调用摘要。

如接入 MCP，从开发者工具“设置 → 安全”复制配置。`wechatide mcp` 的参数必须包含独立的 `--token <MCP_TOKEN>`；MCP Token、CLI 访问令牌和 `clientName` 授权是三层独立校验。任何 Token 都不得写入仓库、日志、截图或阶段回执。

## 2. 每次微信会话的引导顺序

每次 Agent 会话只做一次状态检查，后续复用已确认会话：

1. 读取本机 `skill.yaml` 的 `version`，不得硬编码版本号。
2. 读取项目根目录的 `project.config.json`，确认 `appid` 存在、非空且不是 `touristappid`。缺失或无效时，先按官方 initializer 流程取得用户授权并选择有效 AppID；不得猜测或把 AppID 写入公开回执。
3. 调用：

   ```shell
   wechatide -c Codex -t check_devtools_status --skill-version <skill.yaml 中的 version>
   ```

4. 返回 `openid` 且没有版本 `warning` 才可继续。无 `openid` 时调用 `scan_login`，停下等待用户扫码，再重新检查；有版本警告时按返回路径更新官方 Skill 后重试。
5. 需要立即运行项目时调用：

   ```shell
   wechatide -c Codex -t project_open_window --project <项目绝对路径>
   ```

   只登记项目而不打开窗口时使用 `project_import`。路径已存在且返回 `alreadyImported: true` 视为幂等成功。

## 3. 工作流能力映射

| 本仓库阶段 | 官方 scene | 使用范围 |
| --- | --- | --- |
| 公共初始化 | `skills/initializer/SKILL.md` | 登录/AppID、打开或关闭项目窗口、构建 npm |
| 项目列表维护 | `skills/project-manager/SKILL.md` | 查询、导入项目；删除项目不属于普通验收步骤 |
| 需求实现与 IDE 验收 | `skills/compiler/SKILL.md`、`skills/automator/SKILL.md`、`skills/debugger/SKILL.md` | 编译/单文件校验、打开页面、刷新模拟器、点击输入、截图断言、读取错误日志 |
| 游戏过程录屏 | `skills/automator/SKILL.md`、`skills/debugger/SKILL.md` | 按录制清单驱动模拟器并记录操作/断言；FFmpeg 独立录制连续视频 |
| 数据统计核实 | `skills/debugger/SKILL.md` | 隔离并读取本次 console/network 记录，关联真实请求和响应 |
| 手机预览或体验版 | `skills/previewer/SKILL.md` | 仅在用户明确要求该动作时使用，不是普通 IDE 验收的默认步骤 |
| 云开发资源 | `skills/cloudbase-operator/SKILL.md` | 仅在任务明确包含对应云环境、函数、数据库或存储操作时使用 |

常见调用形式为：

```shell
wechatide -c Codex -t <toolName> [--field value]
```

对象或数组参数通过 `--<field>-file <json-path>` 传入。`simulator_refresh` 成功只表示刷新动作已触发，不能代替编译工具的成功结果。

## 4. 证据与完成判定

每次调用至少记录：官方 Skill 版本、IDE 版本、项目绝对路径或脱敏项目标识、构建标识、scene/tool 名、开始与结束时间、脱敏参数摘要、返回状态、生成的截图/日志/网络记录位置。不得保存 `openid`、AppID、Token、请求签名或不必要的玩家数据。

- 编译通过：必须来自 compiler scene 的实际编译结果；刷新模拟器、无 console 报错或旧日志不等价。
- UI/行为通过：必须有 automator 的操作记录与可观察断言；需要连续过程证据时还必须有本仓库录屏回执。
- 网络上报通过：必须有 debugger 取得的本次真实请求和响应；console 文本或 HTTP 200 单独不足以通过。
- 预览、上传体验版、云函数部署、云数据库写入、云存储上传/删除、项目删除等写入或发布动作不得由“IDE 验收”隐含授权。只在用户明确要求且通过官方确认门时执行。

遇到授权弹窗、扫码、AppID/云环境选择或确认窗口时，停下等待用户完成。用户拒绝或超时后记录 `blocked`，不得绕过确认或反复重试破坏性操作。
