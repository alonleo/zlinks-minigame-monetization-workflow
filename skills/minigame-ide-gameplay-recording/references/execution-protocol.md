# IDE 游戏过程录屏执行协议

本阶段在微信开发者工具或抖音开发者工具中录制本次构建产物的实际运行过程。录像用于复查模拟器内的画面和操作，不替代真机性能、兼容性、触控、平台登录或真实广告验收。

## 1. 录制清单

开始前创建可追踪清单，至少包含：

```yaml
recording_id: REC-<platform>-<stable-id>
platform: wechat | douyin
project: <game/project identifier>
build_path: <absolute build output path>
build_version: <commit/build/hash or timestamp>
ide_version: <actual IDE version>
capture:
  mode: window | region
  frame_rate: 30
  max_duration_seconds: 300
  audio: false
steps:
  - id: STEP-01
    action: <observable user operation>
    expected: <observable result>
    required_shot: <key screen or null>
sensitive_areas: [<account/token/player data to hide or avoid>]
```

操作必须可重复，不能只写“玩一遍”。登录、加载、进入关卡、核心操作、暂停、结算、广告触发或返回前台等需要验收的节点分别列出。录制前确认账号、存档、网络和关卡前置状态；任何会改变结果的跳过、模拟或调试开关都写入清单。

## 2. 环境预检

使用本技能的 Python 脚本：

```shell
python scripts/ide_recording.py --action doctor --platform wechat
python scripts/ide_recording.py --action doctor --platform douyin
```

预检要求：

- `ffmpeg` 和 `ffprobe` 可执行文件存在；也可用 `--ffmpeg-path`、`--ffprobe-path` 显式指定。
- 目标 IDE 已打开本次构建产物，模拟器可运行且窗口标题唯一。
- 自动识别不到或匹配多个窗口时，用 `--window-title-pattern` 提供能唯一匹配的正则，不猜选窗口。
- 区域录制只在桌面坐标、尺寸、DPI 缩放和窗口位置已固定时使用；窗口移动后必须重新核对。
- 默认不录声音。确需声音时用 `--audio-device-name` 指定 FFmpeg DirectShow 可见的回环设备；未验证设备前不得声称包含游戏声音。

## 3. 启动、操作与停止

默认录制完整 IDE 窗口：

```shell
python scripts/ide_recording.py --action start --platform wechat \
  --output-directory D:\evidence\wechat \
  --window-title-pattern "微信开发者工具" \
  --max-duration-seconds 300
```

只录固定模拟器区域：

```shell
python scripts/ide_recording.py --action start --platform douyin \
  --output-directory D:\evidence\douyin \
  --capture-mode region --capture-x 120 --capture-y 180 \
  --capture-width 750 --capture-height 1334
```

`Start` 返回 `recording-session.json` 路径后才执行清单步骤。每步记录实际开始时间、结果和异常；需要重试时保留失败表现，或停止后新建一次录制并在交付中说明取舍，不剪辑成无法追溯的“首次成功”。

完成操作后停止：

```shell
python scripts/ide_recording.py --action stop \
  --session-file D:\evidence\wechat\recording-session.json
```

查询状态：

```shell
python scripts/ide_recording.py --action status \
  --session-file D:\evidence\wechat\recording-session.json
```

达到 `MaxDurationSeconds` 后脚本会自动安全停止，避免遗留无限录制。停止流程先向 FFmpeg 发送 `q`，生成 Matroska 中间文件，再无损封装为 MP4；中间文件默认保留用于故障恢复。

## 4. 回执与证据

同一输出目录包含：

- `recording-session.json`：当前会话、目标窗口、录制参数、进程和状态；
- `<platform>-<timestamp>.mkv`：抗中断中间文件；
- `<platform>-<timestamp>.mp4`：交付视频；
- `<platform>-<timestamp>.ffmpeg.log`：录制与封装日志；
- `<platform>-<timestamp>.receipt.json`：结果、时间、大小、SHA-256、媒体探测结果和失败原因。

把录制清单、步骤执行记录、视频和回执用 `recording_id` 关联。视频、日志或回执中出现账号、令牌、请求签名、调试密钥或其他不必要的敏感数据时，不直接交付；重新录制或使用不破坏证据含义的遮挡方案，并说明处理方式。

## 5. 通过标准

单个平台只有同时满足下列条件才能标记录屏阶段通过：

1. 录像对应目标 IDE 和本次构建产物，平台、版本和路径可追溯；
2. 清单中的必需步骤均有执行结果，关键行为在视频时间范围内可见；
3. MP4 可读取，回执状态为 `completed`，文件大小和 SHA-256 已记录；
4. 画面没有被其他窗口遮挡，分辨率和帧率足以判断需求；
5. 交付明确标记为 IDE 模拟器录像，并列出声音、真机、真实广告或网络环境等未覆盖项。

视频损坏、窗口匹配不唯一、FFmpeg 失败、关键步骤不在画面内、录制中切换了项目，或回执缺失时标记失败或阻塞。一个平台失败不删除另一平台的有效证据；分别报告。

所有适用平台通过后，将录屏清单、脱敏会话/媒体回执和大文件证据位置写入仓库内阶段回执；视频是否入库遵守项目既有大文件策略，不得为提交而强行纳入超大文件。随后按总工作流 Git 协议创建 `recording` 阶段汇总提交、annotated tag，推送分支与标签并核验远程。远程核验通过后阶段才是 `completed`。
