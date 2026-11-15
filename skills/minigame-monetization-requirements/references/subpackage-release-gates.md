# 分包与发布候选门禁

本规范适用于 Unity 微信、抖音小游戏的需求实现阶段。它把资源分包、平台代码分包、产物审计、CDN 一致性和启动证据纳入同一条发布候选链路。进入本门禁的输入必须已经通过[构建后引擎 Logo / 水印处理协议](post-build-logo-removal.md)；门禁只认处理后的 WASM、数据包、`game.js`、清单和哈希。原始设计和历史实验可作为背景材料，但具体项目路径、AppID、哈希、候选目录和历史体积不得直接复用。

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

每个平台分别记录：

```yaml
platform: wechat | douyin
engine_version: <exact version>
player_runtime: <UnityPlugin, WXGameKit, StarkSDK or equivalent>
project_path: <absolute path>
source_branch: miniGame-wx | miniGame-dy
source_commit: <HEAD>
upstream_branch: develop
upstream_commit: <selected develop SHA>
upstream_merge_commit: <develop-to-miniGame merge or already-contained>
baseline_branch: miniGame
baseline_commit: <merged miniGame SHA>
baseline_merge_commit: <merge commit or already-contained>
content_version: <immutable version>
cdn_prefix: <exact configured prefix or null>
previous_candidate: <identifier or null>
previous_code_md5: <value or null>
```

微信构建必须来自 `miniGame-wx`，抖音构建必须来自 `miniGame-dy`，并证明 `baseline_commit` 包含 `upstream_commit`、`source_commit` 包含 `baseline_commit`。旧候选只用于比较和增量基线，不得作为本次验证证据。构建输入存在未提交修改时，记录并保留其归属。

## 4. 资源分包门禁

从本次构建的真实报告和目录检查：

1. 平台主包、平台数据分包、WASM 和远程内容分别统计；
2. 展开首屏、首次路由、主页和首关的实际依赖闭包；
3. 检查 `Resources`、预加载资产、全局 Shader、字体、图集、渲染配置和自动注册入口；
4. 检查动态字符串地址是否由 catalog 或显式规则覆盖；
5. 检查远程场景、Prefab 的脚本和组件保留；
6. 检查大共享包、异常单包和资源回流；
7. 只以最终平台候选目录计算预算，排除 prepared、cache、临时副本和失败目录。

资源移动到 CDN 不代表启动等待已经消失。若 Unity 启动或首次路由仍阻塞等待该资源，必须计入对应时间终点。

## 5. WASM 函数分包通用门禁

- 以本次完成 Logo / 水印处理后的 WASM 完整哈希或平台规定的 code MD5 绑定分包库存；处理前库存只有在平台工具证明仍与处理后 code MD5 匹配时才能复用。
- 哈希不一致、库存不完整、模板不匹配或加载器不匹配时失败关闭，不回退后冒充 Release 成品。
- Profile、prepare、采集缓存和 symbols 不是发布产物。
- Release 必须同时校验主 WASM、延迟 WASM、平台子包声明、运行时加载器和分包版本。
- 主 WASM 变小不能覆盖函数采集不足。Unity 自己的首个加载画面呈现前出现缺失函数等待时，候选失败。
- 代码、引擎、导出插件或启动路径变化后，历史采集只可作为平台明确支持的增量基线，并需重新验证覆盖。

## 6. 微信分支

1. 识别当前导出使用 UnityPlugin 还是 WXGameKit，并选择匹配的官方分包工具版本；两者不能混用。
2. 按本次 code MD5 查找库存。无匹配库存时生成 Profile，完成规定路径采集后再生成同 MD5 Release。
3. Release 审计至少覆盖：主 `wasmcode`、实际生成的延迟包、`game.json` 子包声明、运行时加载器，以及发布范围不含 Profile 标记和插件缓存。
4. 若使用历史版本增量采集，记录基线 MD5、匹配函数数、增量函数数和最终分包版本。
5. 模拟器和真机首屏前均监控 `wait for func`、`fetch js` 或等价缺失函数等待。

## 7. 抖音分支

1. 检查 data-package 与主 WASM 的平台加载顺序。只有日志证明并行时，预算才可取两者最大值；串行时必须按总量计算并修复调度。
2. 分包输入必须来自本次最终原始 WASM 与 symbols；使用官方 init、采集和 dosplit 流程。
3. Release 审计至少覆盖 Android/iOS 主包与延迟包、`game.json`、`webgl-wasm-split.js`、分包版本和各文件 MD5。
4. `.dy-game-cache`、prepared 目录和 symbols 不得进入平台发布目录；可在候选根目录单独归档以供增量分包。
5. 模拟器和真机首屏前监控 `wait wasm func` 或等价缺失函数等待。

## 8. 启动预算

至少区分平台启动页、平台 SDK 准备完成、Unity/WASM 接管、Unity 首场景呈现、目标入口资源就绪和首次可操作。预算必须记录字节、并行/串行假设、有效吞吐和固定尾部成本。模型结果标记为 `estimated-only`，不能写成真机结果。

```yaml
startup_budget:
  main_bytes: 0
  data_package_bytes: 0
  critical_wasm_bytes: 0
  deferred_wasm_bytes: 0
  critical_remote_bytes: 0
  scheduling: parallel | serial | mixed | unknown
  estimated_fast_seconds: null
  estimated_conservative_seconds: null
  result: passed | failed | estimated-only | not-measured
```

项目已有硬上限时沿用更严格的上限。没有项目标准时只报告测量结果和风险，不自行把示例数字设为通用合格线。

## 9. CDN 发布合同

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
- 编译后重新取得本次日志、截图和状态探针；旧 console 不算证据。
- 至少验证冷启动、首次路由、主页或首关、改动功能及一个延迟资源路径。
- 记录首屏前缺失函数等待次数、404、catalog 版本、远程资源失败和异常路由。
- 真机性能结论至少区分冷缓存、文件缓存和弱网；样本数量及 P75/P95 使用项目既有验收标准。
- 未完成真机样本时，只能报告构建、静态审计、模拟器或模型估算层级。

## 11. 失败关闭条件

出现以下任一情况时，需求不得进入 `done`：

- 当前代码对应的函数分包缺失或哈希不一致；
- 缺少本次构建的 `post_build_logo_removal` 通过回执，或门禁输入仍引用处理前文件与哈希；
- Profile、prepare 或半成品被当作 Release；
- 子包声明、加载器和实际文件不一致；
- catalog 指向尚未上传或哈希不符的 Bundle；
- CDN 目标三方不一致；
- 预算来自旧候选、缓存或中间目录；
- 首屏前出现缺失函数等待；
- 平台构建或 IDE 使用的不是同一个最终候选；
- 仅有估算或模拟器数据却声明真机达标。

## 12. 阶段回执

适用平台逐一输出：

```yaml
subpackage_release_gate:
  platform: wechat | douyin
  candidate_id: <stable build identifier>
  candidate_path: <absolute path>
  content_version: <value>
  catalog_sha256: <value or null>
  bundle_count: 0
  remote_bytes: 0
  code_md5: <value or null>
  wasm_split_status: inventory-hit | generated | missing | invalid | not-applicable
  wasm_split_version: <value or null>
  main_wasm_bytes: 0
  deferred_wasm_bytes: 0
  profile_in_release: false
  startup_budget: <section 8 record>
  cdn_manifest_sha256: <value or null>
  cdn_public_hash_verified: false
  ide_verified: false
  first_screen_missing_func_waits: null
  runtime_evidence: []
  unverified_scope: []
  result: passed | partial | blocked | failed | not-applicable
```

只有 `result: passed` 或有证据的 `not-applicable` 才能进入该平台 IDE 录屏。`partial`、`blocked` 或 `failed` 保留已完成事实，但不得被总流程提升为需求实现阶段完成。
