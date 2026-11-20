# 性能 AI 助手接入协议

本协议把团结「性能 AI 助手」作为需求实现阶段内的条件式质量检查器。它不构成新的工作流大阶段，不替代编译、平台构建、微信/抖音分包门禁、平台 IDE 核验或目标平台真机证据。

## 1. 适用性与预检

仅在目标工程为 Unity/团结工程且已安装可用的 `perf-assistant` 扩展时执行。开始前记录：

- 扩展版本与性能产物契约版本；
- Unity/团结版本、渲染管线、当前提交与工作区状态；
- Codely Bridge、所需本地包和对应工具面是否可用；
- 本轮目标平台、场景、构建形态与性能预算；
- 插件能力面与当前宿主、设备和目标平台是否匹配。

插件缺失、Bridge 离线、渲染管线不受支持、所需工具不可用或需求不触发性能检查时，回执写 `result: skipped` 和具体 `skip_reason`。除非用户或项目基线明确把该检查设为必需，否则插件不可用不阻塞普通需求。

Android/iOS 采集只证明对应原生构建与设备；Unity Editor 数据只证明编辑器场景。二者均不能证明微信/抖音小游戏真机性能。

## 2. 触发矩阵

| 改动或目标 | 最小检查面 |
| --- | --- |
| 贴图、音频、模型、字体、资源目录 | 资源体积、导入设置、重复资源；零引用仅作候选 |
| Scene、Prefab、UI 结构 | Missing Script、断引用、场景复杂度 |
| Shader、URP、后处理、渲染设置 | Shader 审计；必要时 Frame Debugger |
| 粒子、特效 | 粒子静态审计；可复现场景下再做运行时采样 |
| `Update`/`LateUpdate`、加载流程、对象池或热点代码 | 热路径静态检查；必要时 Profiler |
| 明确卡顿、掉帧、内存或综合优化诉求 | 统一性能流程的 `standard` 检查轮 |
| 发版候选或大范围综合改造 | 一次可追溯基线与一次可比复验 |
| 文案、埋点字段、广告位 ID、普通业务配置 | 默认不运行全量检查；有直接性能影响证据时再升级 |

不得为了“覆盖完整”而对每条低风险需求运行全项目扫描。检查范围应与改动和已知风险相称。

## 3. 执行位置与写入边界

### 修改前

性能敏感或大范围改动在 `prompt-frozen` 后、`implementing` 前建立 `baseline`。只运行与改动相关的能力面；发版候选或综合性能诉求可使用 `standard`。基线必须只读，除了 `.codely-cli/perf-runs/<runId>/` 产物外不得写入项目。

### 编译后

编译成功后执行定向检查。存在有效基线且构建形态、场景、设备或编辑器环境可比时运行 `verify`；不具备可比性时只报告本轮事实，不声称“改善”或“回退”。

### 构建后

可用 Unity 构建报告交叉核对纹理、音频和大资源热点，但小游戏最终包体、WASM、分包、catalog、Bundle 和 CDN 结论仍以分包与发布候选门禁为准。性能助手报告不得把中间构建提升为最终候选。

### 修复与实验

- `baseline` / `verify`：只读，禁止修改项目本体。
- `fix`：必须取得用户对具体修复项的明确授权；每项转成独立需求，独立实现、验证与提交，不混入原需求的基线轮。
- `experiment`：只允许临时改动；结束前必须还原并记录 `reverted: true` 或等价证据。

发现问题不等于授权自动修复。不得自动删除零引用候选、批量改导入设置或重写场景、Prefab、材质及代码。

## 4. 阻断与警告

以下情况阻断受影响需求继续成为 `done`：

- 性能助手自身产物校验为 `FAIL`，导致证据不可信；
- 构建场景存在确定的 Missing Script、断引用、Shader 编译错误或资源导入错误；
- 有充分证据的 P0/阻断级问题；
- 基线与复验可比，并确认超过项目明确预算或发生阻断级回退；
- 检查被项目或用户明确设为必需，但能力不可用或证据缺失。

以下结果默认记录为 `warning`，不自动阻断：

- P1/P2 优化建议；
- 零引用、重复内容或大资源候选；
- UnityEvent 等无法解析但尚无运行失败证据的引用；
- 某能力面未覆盖、仅有 Editor 证据或平台不匹配；
- 缺少项目预算时基于通用阈值生成的建议。

“未检查”“不适用”和“检查后无问题”必须分开记录。未覆盖能力面不得表述为通过。

## 5. 证据与回执

插件原始产物保持在：

```text
<project>/.codely-cli/perf-runs/<runId>/
  context.json
  findings.json
  fixes.json
  verify.json
  report.md
```

Profiler、trace、索引和其他大型原始数据默认不提交 Git。需求台账保存轻量、可追溯且已脱敏的 `performance_assessment`：

```yaml
performance_assessment:
  provider: tuanjie-perf-assistant
  extension_version: <version>
  contract_version: <version-or-null>
  run_id: <run-id-or-null>
  run_kind: baseline | verify | fix | experiment | null
  requirement_id: <stable requirement id>
  delivery_mode: isolated_branches | unified_matrix
  variant_id: <stable id or null>
  source_branch: <miniGame, platform-named branch, or unified branch>
  source_commit: <commit or worktree fingerprint>
  upstream_commit: <selected develop commit>
  baseline_commit: <miniGame commit or null>
  unity_version: <version>
  target_platform: wechat | douyin | android | ios | editor
  scene: <scene or null>
  build_form: <editor-or-build description>
  scope: [asset, scene, code, render, particle, runtime, size]
  result: passed | warning | failed | skipped
  blocking_findings: []
  warning_findings: []
  coverage:
    <domain>: checked | clean | skipped | not-applicable
  raw_artifact_path: <local or artifact-store path>
  report_sha256: <sha256 or null>
  limitations: []
  skip_reason: <required when skipped>
```

所有检查记录本轮选定的 `upstream_commit`。分支隔离模式按共享 `miniGame` 或平台命名分支记录；统一矩阵模式记录统一分支，并在目标相关检查中补充 `variant_id`、平台和解析配置 SHA。`source_commit` 尚未产生时使用可重现的工作区指纹，并在提交后回填 commit。回执不得保存账号、令牌、设备标识、请求签名或玩家数据。原始证据不入库时，记录其本地/制品位置和 SHA-256；无法长期保留时明确写入限制，不伪造可追溯性。

## 6. 与现有门禁的关系

性能回执是需求实现证据的一部分，但不改变主状态机：

```text
extracted -> located -> experience-reviewed -> prompt-frozen
          -> implementing -> compiled -> built -> package-gated
          -> ide-verified -> done
```

- 修改前基线附着在 `prompt-frozen -> implementing` 边界；
- 定向检查或复验附着在 `compiled`；
- 构建报告交叉核对附着在 `built`；
- 最终候选资格仍由 `package-gated` 决定；
- 平台可运行性仍由 `ide-verified` 决定；
- 真机性能只有目标平台真机证据可以确认。

需求交付时同时列出检查范围、结论、阻断项、警告、未覆盖面、插件版本、runId、报告哈希和证据位置。未触发性能检查的需求也要在回执中写明 `skipped` 原因，避免把没有运行误读为通过。
