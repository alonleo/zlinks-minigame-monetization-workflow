# 抖音开发者工具 Skill 接入规范

本仓库的抖音 IDE 操作统一接入抖音官方[开发者工具 Skill](https://developer.open-douyin.com/docs/resource/zh-CN/mini-app/develop/dev-tools/developer-instrument/ai/ide-skill)。它用于管理项目窗口、编译和刷新模拟器、执行交互、读取 console/network、截图、预览及上传测试版本；本仓库已有的 FFmpeg 录屏脚本仍负责连续视频和媒体回执。两条证据链互补，不能互相替代。

本文依据官方页面及本机导出的官方 Skill 于 2026-11-10 核对。执行时必须以本机 `douyinide` 返回的最新 Skill 和命令帮助为准，不把本文示例当成固定命令清单。

## 1. 安装与运行时依赖

1. 安装抖音开发者工具 `4.5.7` 或更高版本；官方[版本记录](https://developer.open-douyin.com/docs/resource/zh-CN/mini-app/develop/dev-tools/developer-instrument/download/developer-instrument-update-and-download)显示 `4.5.7` 于 2026-10-22 首次加入开发者工具 Skill。
2. 在终端执行 `douyinide`，取得本机 Skill 目录和版本；也可在开发者工具菜单导出 `douyinide-skill-<version>.zip`，或复制 Skill 安装提示词。
3. Agent 在执行抖音阶段前完整读取导出目录的根 `SKILL.md`，再按目标读取它路由的 environment、compiler、automator、debugger 或 runtime-state-chain 参考。命令和参数以 `douyinide <command> -h` 为准，不得凭本文、微信工具或历史版本编造。
4. 官方 Skill 是随本机开发者工具更新的运行时依赖，不复制进本仓库。回执只记录 Skill/IDE 版本、来源路径和必要的脱敏调用摘要。

当前官方 Skill 通过本机 CLI 与开发者工具通信，没有微信 Skill 的 `--skill-version` 或独立 MCP Token 流程。不要把微信的命令格式、Token 或状态判断套到抖音。

## 2. 每次抖音会话的引导顺序

1. 确认 `douyinide` 可用；命令存在但 IDE 未运行时执行一次 `douyinide start-ide`。命令不存在时按官方 Skill 仅尝试一次 `bytedanceide://` 唤起；仍不可用则要求安装或升级官方工具，不枚举或猜测安装路径。
2. 只查询已导入项目可直接使用 `project-list`；需要在线 IDE、登录或窗口能力时，调用：

   ```shell
   douyinide check-ide-status -c Codex
   ```

   分别判断 `ideOnline` 和 `loginExpired`，不能把离线等同于未登录。需要登录时调用 `douyinide login -c Codex`，停下等待用户完成登录，再重新检查状态；`ok: true, loggedIn: false` 不代表登录完成。
3. 读取项目根 `project.config.json` 并判断类型：小程序为 `app`，小游戏为 `microgame`。小游戏根目录必须有 `game.json`；小程序按 `miniprogramRoot` 定位 `app.json`。两者都有或都没有时停止确认，不从目录名猜测。
4. 需要模拟器窗口时打开项目并取得真实窗口 ID：

   ```shell
   douyinide open-project-window -c Codex --path <项目绝对路径> --type microgame
   douyinide get-project-windows-info -c Codex
   ```

   项目路径始终是包含 `project.config.json` 的根目录。Unity 小游戏也使用 `microgame`，并由 IDE 强制使用完整模式。
5. 后续编译、截图、日志和交互统一绑定同一个真实 `windowId`；窗口关闭、IDE 重启或连接重建后重新查询，不能复用旧 ID。

## 3. 工作流能力映射

| 本仓库阶段 | 官方参考 | 使用范围 |
| --- | --- | --- |
| 公共初始化 | `references/environment.md` | 状态、登录、App ID、项目列表、项目类型与窗口 ID |
| 需求实现与 IDE 验收 | `references/compiler.md`、`references/automator.md`、`references/debugger.md` | 强制重编译、模拟器刷新、坐标交互、截图、console/network 与业务断言 |
| 游戏过程录屏 | `references/automator.md`、`references/debugger.md` | 驱动小游戏 Canvas、保存前后截图和状态探针；FFmpeg 独立录制连续视频 |
| 数据统计核实 | `references/debugger.md` | 建立 network 基线并读取本次操作后的新增请求与响应 |
| 跨步骤验证 | `references/runtime-state-chain.md` | 用同一 `runtimeContext` 绑定项目、窗口、编译、画面、操作和证据 |
| 预览或上传测试版 | `references/previewer.md` | 仅在用户明确要求时使用；上传测试通道不等于审核或正式发布 |

常见调用形式为：

```shell
douyinide <command> -c Codex [options...]
douyinide <command> -h
```

`clientName` 在同一任务保持为 `Codex`。首次连接可能出现 IDE 授权弹窗；用户拒绝后停止，不更换客户端名绕过授权。

## 4. 小游戏 Canvas 强制证据链

抖音小游戏没有可定位的页面元素树或 `backendId`。不得对小游戏调用 selector、Page data、页面栈、元素属性/样式等仅支持小程序的能力；`build-npm` 也不支持小游戏。每次交互验证必须针对同一个 `windowId` 完成：

1. 操作前保存截图，记录 `filePath`、`imageSize`、`layoutViewport`、`contentSize` 和 `screenshotScale`；同时取得相关 console、network、项目调试日志或 `miniapp-execute-js` 状态探针基线。
2. 从截图识别图片像素坐标，按 `cssX = imageX / screenshotScale.x`、`cssY = imageY / screenshotScale.y` 换算 CSS 坐标；不得直接把图片像素坐标传给点击或滑动命令。
3. 使用 `miniapp-tap --x/--y`、`miniapp-swipe` 或聚焦后的输入执行操作。
4. 操作后立即保存新截图，并读取与基线相同的状态探针，只把新增日志或请求归因给本次操作。
5. 视觉变化和状态探针同时符合预期才能通过；缺少探针、证据冲突或无法识别本次增量时标记 `未确认`，不能用退出码 `0` 代替业务断言。

跨编译、交互和取证时维护同一个 `runtimeContext`，至少包含项目路径与类型、真实窗口 ID、编译动作、画面基线、console/network 基线、最近操作、操作后证据和阻塞项。编译、刷新、窗口变化、动画或布局变化会使旧截图坐标及证据基线失效，必须重新锚定。

## 5. 证据与授权边界

每次调用至少记录：官方 Skill/IDE 版本、项目绝对路径或脱敏标识、项目类型、窗口 ID、构建标识、命令名、开始与结束时间、脱敏参数摘要、返回状态，以及截图、console/network 或其他证据位置。不得保存账号信息、App ID、Cookie、请求签名或不必要的玩家数据。

- `simulator-refresh` 或 `simulator-compile` 返回成功只表示动作已触发；必须重新建立画面和状态基线并检查运行结果。
- console 为空不代表没有错误；network 请求存在或 HTTP 200 也不能单独证明业务上报成功。
- 按路径生成预览会重新读取磁盘项目，CLI 没有可与窗口内存状态绑定的 compile revision；只能表述为“对同一项目及启动参数生成预览”，不能声称就是刚验证的内存版本。
- 分享代码、上传测试版本、注销账号、关闭窗口、退出 IDE 和清理登录/权限/全部缓存不得由“IDE 验收”隐含授权。只在用户明确要求的范围内执行。

遇到首次客户端授权、登录、二维码确认或其他用户交互时停下等待。用户拒绝或操作结果不确定时记录 `blocked`，不得绕过授权或盲目重发有副作用的命令。
