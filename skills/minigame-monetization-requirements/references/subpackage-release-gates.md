# 分包与发布候选门禁

本规范适用于 Unity 微信、抖音小游戏的需求实现阶段。它把资源/Data 子包、WASM 无函数分包、主包图片预算、产物审计、CDN 一致性和启动证据纳入同一条发布候选链路；抖音还强制纳入侧边栏、运行时 QA、CDN 和原生排行榜。进入本门禁的输入必须已经通过项目内 Release 构建 skill 和[构建后引擎 Logo / 水印处理协议](post-build-logo-removal.md)；门禁只认处理后的 WASM、数据包、`game.js`、清单和哈希。

## 0. 独立能力回执

本门禁不承担资源运行时实现、资源包规划或启动测量。进入门禁前分别调用并保存：[资源运行时](../../minigame-resource-runtime/SKILL.md)的 `resource_runtime`、[资源构建与分包](../../minigame-resource-packaging/SKILL.md)的 `resource_packaging`、[启动与迁移验收](../../minigame-startup-verification/SKILL.md)的 `startup_verification`。三项各自按照适用范围独立执行、失败和续做；门禁只校验回执与同一源码基线、平台、内容版本和最终候选相符。

同时保存 `game_sdk_core` 及当前平台的 `game_sdk_ads`、`game_sdk_telemetry`、`game_sdk_platform`、`game_sdk_build_template`。任一适用回执缺失、失败或指向其他分支/候选时门禁不得通过。

所有正式候选必须关闭 WASM 函数分包，发现活动函数分包产物、加载器或声明即失败。平台 Data 子包、资源子包和 CDN 远程资源不属于 WASM 函数分包，可按 `resource_packaging` 回执保留。

## 1. 适用判定

目标平台包含微信或抖音时，对本次新构建并完成 Logo / 水印处理的候选执行门禁。先记录本次改动是否影响：

- C#、IL2CPP、平台桥接、启动模板或条件编译；
- 场景、Prefab、Resources、Addressables、Shader、字体、图集或音频；
- 远程 catalog、Bundle、CDN 根路径或内容版本；
- `data-package`、WASM、原生子包声明或加载顺序。
- 构建后水印调度补丁、透明化贴图、`game.js` 代码引用、code MD5 或渠道清单。

不能仅凭“只改 UI”判定无影响。C# 改动通常会改变 WASM；序列化引用变化可能改变资源闭包；重新构建也可能产生新哈希。结论必须来自本次产物。

## 2. 通用资源合同

- 业务只表达打开界面、进入场景或取得资源，不拼接 CDN、不识别物理包名。
- 本地、文件缓存和远程资源使用同一异步结果语义；加载开始不等于资源可用。
- 同一地址、类型和版本的并发请求共享底层任务，但每个调用者保留自己的取消、结果和持有关系。
- UI、场景和战斗在提交业务动作前等待必要资源准备完成；失败重试不得重复发奖、扣体力或保存结算。
- 后台预载只优化性能。关闭预载后，正式请求仍须正确工作。
- 包按生命周期、访问局部性、更新频率和依赖闭包划分，禁止把“共享”变成全项目大包。
- catalog、Bundle 和播放器记录不可变内容版本；运行时禁止混用不兼容版本。
- 远程 Bundle 所需代码、反射类型和引擎模块必须保留在播放器中。

## 3. 构建前基线

每个目标组合分别记录：

```yaml
platform: wechat | douyin
delivery_mode: isolated_branches | unified_matrix
variant_id: <stable id or null>
resolved_profile_sha256: <value or null>
engine_version: <exact version>
player_runtime: <UnityPlugin, WXGameKit, StarkSDK or equivalent>
project_path: <absolute path>
source_branch: <platform-named branch or unified branch>
source_commit: <HEAD>
upstream_branch: develop
upstream_commit: <selected develop SHA>
upstream_merge_commit: <develop-to-miniGame merge or already-contained>
baseline_branch: <miniGame for isolated mode; develop for unified mode>
baseline_commit: <mode-specific baseline SHA>
baseline_merge_commit: <merge commit or already-contained>
content_version: <immutable version>
cdn_prefix: <exact configured prefix or null>
previous_candidate: <identifier or null>
previous_code_md5: <value or null>
```

分支隔离模式的微信、抖音构建必须来自对应平台命名分支，并证明两级祖先关系。统一矩阵模式必须来自明确的统一分支，证明其包含选定 `develop`，并核对 `variant_id + platform + resolved_profile_sha256` 与运行时、模板、资源和 SDK 回执完全一致。旧候选只用于比较和增量基线，不得作为本次验证证据。构建输入存在未提交修改时，记录并保留其归属。

## 4. 资源分包门禁

从本次构建的真实报告和目录检查：

1. 平台主包、平台数据分包、WASM 和远程内容分别统计；
2. 展开首屏、首次路由、主页和首关的实际依赖闭包；
3. 检查 `Resources`、预加载资产、全局 Shader、字体、图集、渲染配置和自动注册入口；
4. 检查动态字符串地址是否由 catalog 或显式规则覆盖；
5. 检查远程场景、Prefab 的脚本和组件保留；
6. 检查大共享包、异常单包和资源回流；
7. 只以最终平台候选目录计算预算，排除 prepared、cache、临时副本和失败目录。
8. 扫描生成后的平台主包 `.png`、`.jpg`、`.jpeg`、`.webp`；排除清单明确声明的 Data/WASM 子包根目录后，每张图片必须严格小于 `512000` 字节，并记录最大图片路径和精确字节数。

资源移动到 CDN 不代表启动等待已经消失。若 Unity 启动或首次路由仍阻塞等待该资源，必须计入对应时间终点。

## 5. WASM 禁止函数分包门禁

- 构建配置必须显式关闭 WASM 函数分包；不能仅凭目录中暂时没有延迟包推断配置已关闭。
- 完成 Logo / 水印处理后重新扫描同一候选。只允许一个有效主 WASM 布局；`wasmcode1`、`wasmcode-android`、`wasmcode1-android`、`wasmcode-ios`、`wasmcode1-ios`、`webgl-wasm-split.js` 及等价活动分包加载器、声明或延迟函数包均失败关闭。
- Profile、prepare、函数采集缓存、`.dy-game-cache` 和 symbols 不是发布产物，不得进入平台包；它们存在于候选根目录外也不能作为启用函数分包的理由。
- `game.js`、平台配置和加载器必须只引用本次处理后的主 WASM；哈希、code MD5、压缩标记或引用不一致时失败。
- 平台 Data 子包、资源子包和 CDN 远程资源不属于 WASM 函数分包，可以按已验证的平台清单保留。

## 6. 微信分支

1. 识别当前导出使用 UnityPlugin 还是 WXGameKit，并确认其 Release 构建配置已关闭函数分包。
2. Release 审计至少覆盖主 `wasmcode`、`game.js`、`game.json`、平台子包声明和运行时加载器，确认不存在延迟函数包、Profile 标记或插件分包缓存。
3. 平台 IDE 与真机日志中不得出现 `wait for func`、`fetch js` 或其他函数分包加载行为；出现即说明候选与禁用策略不一致。

## 7. 抖音分支

1. 检查 data-package 与唯一主 WASM 的平台加载顺序。只有日志证明并行时，预算才可取两者最大值；串行时必须按总量计算并修复调度。
2. Release 审计至少覆盖主 WASM、`game.json`、`game.js` 和各文件 MD5，确认不存在 Android/iOS 延迟函数包、`webgl-wasm-split.js` 或 dosplit 输出。
3. `.dy-game-cache`、prepared 目录和 symbols 不得进入平台发布目录。
4. 模拟器和真机首屏前不得出现 `wait wasm func` 或等价函数分包加载行为。

## 8. 启动预算

至少区分平台启动页、平台 SDK 准备完成、Unity/WASM 接管、Unity 首场景呈现、目标入口资源就绪和首次可操作。预算必须记录字节、并行/串行假设、有效吞吐和固定尾部成本。模型结果标记为 `estimated-only`，不能写成真机结果。

```yaml
startup_budget:
  main_bytes: 0
  data_package_bytes: 0
  wasm_bytes: 0
  critical_remote_bytes: 0
  scheduling: parallel | serial | mixed | unknown
  estimated_fast_seconds: null
  estimated_conservative_seconds: null
  result: passed | failed | estimated-only | not-measured
```

项目已有其他包体或启动硬上限时沿用更严格的上限；主包图片仍无条件执行每张严格小于 `512000` 字节的固定门禁。其他指标没有项目标准时只报告测量结果和风险，不自行把示例数字设为通用合格线。

## 9. CDN 发布合同

抖音 CDN 上传无条件不可跳过，必须存在真实远程资源目录；其他平台存在远程资源时不可跳过，没有远程资源时以构建清单证明并记录 `not-applicable`。

1. 从本次最终候选生成上传清单与哈希；
2. 对照信息表前缀、工程运行时配置和实际上传目标；
3. 先上传内容寻址 Bundle，验证源站对象；
4. 再上传或覆盖 catalog、settings 等入口元数据；
5. 通过公开 CDN 逐文件校验大小和 SHA-256；仅校验大小不合格；
6. 缓存不一致时只刷新确认不一致的精确 URL，随后重新执行完整公开校验；
7. 不删除旧哈希 Bundle，除非另有明确、可回滚的清理授权。

若同一 CDN 版本已被旧客户端使用，而本次 catalog 或 Bundle 集合变化，应升级不可变内容版本；不得通过覆盖共享 catalog 破坏旧客户端。

## 10. IDE 与真机验证

- 平台 IDE 必须打开本次最终候选的真实项目入口，不得打开候选外层、WebGL 中间目录或旧窗口中的旧工程。
- 所有目标平台的最终候选均必须包含五连击入口、密码弹窗、项目字体、QA 面板和经项目授权策略保护的命令服务；必须直接在同一最终 Release 候选验证可打开。另行测试的 QA 构建不能替代该证据。未触发时不得出现额外 QA 标识、常驻输入遮罩或正式流程行为变化。
- 抖音最终候选必须按[抖音小游戏四项必接能力](../../minigame-standard/references/douyin-mandatory-capabilities.md)验证首页侧边栏入口、状态机、复访奖励幂等、排行榜真实成绩上报和原生 UI；IDE 证据之外还要有同一 `build_id` 的侧边栏往返与排行榜真机证据。
- 编译后重新取得本次日志、截图和状态探针；旧 console 不算证据。
- 至少验证冷启动、首次路由、主页或首关、改动功能及一个延迟资源路径。
- 记录首屏前缺失函数等待次数、404、catalog 版本、远程资源失败和异常路由。
- 真机性能结论至少区分冷缓存、文件缓存和弱网；样本数量及 P75/P95 使用项目既有验收标准。
- 未完成真机样本时，只能报告构建、静态审计、模拟器或模型估算层级。

## 11. 失败关闭条件

出现以下任一情况时，需求不得进入 `done`：

- 适用的 `resource_runtime`、`resource_packaging` 或 `startup_verification` 回执缺失、未满足各自完成条件，或与当前源码基线、平台、内容版本、候选不一致；
- `game_sdk_core` 或当前平台适用的 `game_sdk_ads`、`game_sdk_telemetry`、`game_sdk_platform`、`game_sdk_build_template` 缺失、失败或与当前候选不一致；
- WASM 函数分包配置未明确关闭、发现活动分包产物/加载器/声明，或主 WASM 引用与哈希不一致；
- 主包图片扫描缺失，或任一适用图片大于等于 `512000` 字节；
- 缺少本次构建的 `post_build_logo_removal` 通过回执，或门禁输入仍引用处理前文件与哈希；
- Profile、prepare 或半成品被当作 Release；
- 子包声明、加载器和实际文件不一致；
- catalog 指向尚未上传或哈希不符的 Bundle；
- 存在远程资源但未完成上传和公开逐文件 SHA-256 校验；
- 抖音没有真实远程资源目录、CDN 状态为 `not-applicable`，或侧边栏、QA、排行榜任一必接能力未通过；
- CDN 目标三方不一致；
- 预算来自旧候选、缓存或中间目录；
- 首屏前出现缺失函数等待；
- 平台构建或 IDE 使用的不是同一个最终候选；
- 仅有估算或模拟器数据却声明真机达标。

## 12. 阶段回执

适用平台逐一输出：

```yaml
subpackage_release_gate:
  delivery_mode: isolated_branches | unified_matrix
  variant_id: <stable id or null>
  platform: wechat | douyin
  source_branch: <branch>
  source_commit: <sha>
  resolved_profile_sha256: <value or null>
  candidate_id: <stable build identifier>
  candidate_path: <absolute path>
  content_version: <value>
  resource_runtime: <receipt or not-applicable with evidence>
  resource_packaging: <receipt>
  startup_verification: <receipt>
  game_sdk_core: <receipt>
  game_sdk_ads: <receipt>
  game_sdk_telemetry: <receipt>
  game_sdk_platform: <receipt>
  game_sdk_build_template: <receipt>
  catalog_sha256: <value or null>
  bundle_count: 0
  remote_bytes: 0
  code_md5: <value or null>
  wasm_function_split: disabled | active | unknown
  active_split_artifacts: []
  main_wasm_bytes: 0
  profile_in_release: false
  main_package_image_limit_bytes: 512000
  largest_main_package_image: {path: <relative path or null>, bytes: 0}
  main_package_image_result: passed | failed
  startup_budget: <section 8 record>
  cdn_manifest_sha256: <value or null>
  cdn_upload_status: uploaded-and-publicly-verified | not-applicable | blocked | failed # not-applicable 仅限非抖音
  cdn_public_hash_verified: false
  ide_verified: false
  first_screen_missing_func_waits: null
  douyin_required_capabilities: <receipt or null>
  runtime_evidence: []
  unverified_scope: []
  result: passed | partial | blocked | failed | not-applicable
```

只有 `result: passed` 或有证据的 `not-applicable` 才能进入该平台 IDE 录屏。`partial`、`blocked` 或 `failed` 保留已完成事实，但不得被总流程提升为需求实现阶段完成。
