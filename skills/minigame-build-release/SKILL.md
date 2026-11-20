---
name: minigame-build-release
description: 为分支隔离或统一矩阵模式下的 Unity/团结小游戏目标生成全新 Release 候选，强制禁用 WASM 函数分包并执行图片、去水印和 CDN 门禁。用于商业化需求实现阶段；不负责源码实现、审核或正式发布。
---

# 小游戏 Release 构建

本 skill 是需求实现阶段内的支持能力，不增加新的流程阶段。先读取总工作流的[交付拓扑与模式路由](../../references/delivery-topologies.md)。分支隔离模式对平台命名分支生成候选；统一矩阵模式对统一分支中的每个 `variant_id + platform` 组合生成候选。随后把完整身份不变的同一候选交给发布候选门禁和平台 IDE。

执行前必须完整读取并执行[资源构建与分包 skill](../minigame-resource-packaging/SKILL.md)，接收与当前平台、分支、内容版本一致的 `resource_packaging` 回执。资源运行时源码实现和启动真机结论分别属于[资源运行时](../minigame-resource-runtime/SKILL.md)与[启动与迁移验收](../minigame-startup-verification/SKILL.md)，本 skill 不用构建成功替代它们的回执。正式候选仍强制关闭 WASM 函数分包。

还必须完整执行[SDK 构建模板](../minigame-sdk-build-templates/SKILL.md)并取得当前平台 `game_sdk_build_template.result: passed`；抖音候选不得引用 `webgl-wasm-split.js`，微信 dnSDK 构建钩子必须与实际转换流程一致。

## 输入

至少取得模式、平台、平台小游戏正式名称、源码分支与 HEAD、上游基线 SHA、工程路径、精确引擎版本、正式 Release 构建入口、预期输出根目录，以及信息表和工程配置中的 CDN 目标。分支隔离模式还需最终 `miniGame` SHA 和平台合并关系；统一矩阵模式还需稳定 `variant_id`、解析配置文件与 `resolved_profile_sha256`，并证明其平台和正式名称与目标一致。抖音还必须取得四项必接能力配置和非空 CDN 目标。不得从错误分支、旧目录或时间排序猜候选。

还必须取得当前平台的 `resource_packaging.result: passed`；其内容版本、清单和输出路径必须与本次构建输入一致。缺失或不一致时退回资源构建与分包 skill，不在本 skill 临时猜测包归属。

## 强制规则

- 构建当前工作区状态并保留用户全部修改；构建不授权清理、重置、提交、审核、上架或正式发布。
- 统一矩阵模式每次只激活一个 `variant_id + platform`，构建输出使用 `<output-root>/<variant_id>/<platform>/<build_id>/`。同一工程默认串行构建；不得让另一个组合并发改写同一 Library、生成配置或输出目录。
- 统一矩阵构建的运行时、平台模板、资源构建、SDK 初始化和回执必须消费同一解析配置哈希。解析文件出现 Secret、Token、私钥或访问密钥，或实际平台与解析平台不一致时失败关闭。
- 使用项目正式 Release 入口。除非用户明确要求，不能用 QA、Development、Profile 或 prepare 构建替代。
- 强制禁止 WASM 函数分包。构建配置必须关闭该能力；候选中出现 `wasmcode1`、`wasmcode-android`、`wasmcode1-android`、`wasmcode-ios`、`wasmcode1-ios`、`webgl-wasm-split.js` 或其他活动分包加载器时失败关闭。
- 对生成后的平台主包统计 `.png`、`.jpg`、`.jpeg`、`.webp`。排除平台清单明确声明的 Data/WASM 子包根目录后，每张图片必须严格小于 `512000` 字节；缺少扫描证据或任一图片大于等于该值时失败。
- 每个全新平台包必须完整执行[构建后引擎 Logo / 水印处理协议](../minigame-monetization-requirements/references/post-build-logo-removal.md)。未通过不得交付候选。
- 抖音构建必须生成真实远程资源目录并完成 CDN 上传和公开逐文件 SHA-256 校验，不允许 `not-applicable`；其他平台在存在远程资源目录时强制执行，不存在时记录 `not-applicable`。只上传精确的远程资源目录，不能上传工程根目录、全部 `Builds`、完整候选目录或平台代码包。
- 对远程启动清单使用构建唯一文件名，例如 `manifest.<buildId>.json`；稳定 `manifest.json` 加查询参数不能作为不可变发布对象。

## 执行

1. 检查项目说明、Git 状态、交付拓扑、目标身份、平台设置和构建脚本，解析正式 Release 入口及成功标记。
2. 核对 `resource_packaging` 回执中的包归属、启动必要资源集、动态依赖、远程 Catalog/Bundle、版本和平台参数与工作区一致；缺少适用实现时停止并退回对应 skill，不用扩大主包掩盖缺口。
3. 统一矩阵模式先用 SDK Core 的解析器生成配置、核对 SHA 并以临时构建输入注入；随后使用项目记录的精确 Unity/团结版本完成编译预检并启动全新构建。分支隔离模式直接使用对应命名分支的已验证配置。持续读取权威日志，直到明确成功或失败。
4. 只从本次构建成功标记解析候选，固定构建 ID、平台包、远程资源目录、清单、报告和日志；抖音远程资源目录或四项必接能力入口缺失时立即失败。
5. 证明构建配置已关闭 WASM 函数分包，并扫描最终目录确认不存在活动分包文件、加载器或声明。
6. 扫描生成后的主包图片，记录最大图片相对路径和精确字节数；任一图片大于等于 `512000` 字节时停止。
7. 对该平台包执行去水印协议；随后重新运行无函数分包、图片预算、包预算和清单检查，因为处理可能改变哈希与压缩大小。
8. 抖音必须、其他平台在存在远程资源时，生成或核对包含相对路径、字节数和 SHA-256 的精确清单，按提供方调用专项上传 skill。七牛使用项目内的 [Kodo 上传 skill](../minigame-kodo-upload/SKILL.md)：先只读预检，再增量上传，最后通过公开 CDN 对每个文件校验完整 SHA-256。缺少上传权限、目标冲突或公开校验失败时，本候选不得完成。
9. 固定处理和上传后的同一候选，交给[分包与发布候选门禁](../minigame-monetization-requirements/references/subpackage-release-gates.md)及平台 IDE；后续任何重新构建都会使本回执失效。

同名远端对象内容不同时，遵守上传 skill 的覆盖授权要求；没有覆盖授权时标记 `blocked`，不得通过改目录、跳过文件或声称上传完成来绕过。

## 回执

```yaml
build_release:
  delivery_mode: isolated_branches | unified_matrix
  variant_id: <stable id or null>
  platform: wechat | douyin
  platform_game_name: <正式名称>
  source_branch: <resolved platform or unified branch>
  source_commit: <candidate source SHA>
  baseline_commit: <final miniGame or develop baseline SHA>
  resolved_profile_path: <absolute path or null>
  resolved_profile_sha256: <value or null>
  resource_packaging: <receipt identifier>
  game_sdk_build_template: <receipt identifier>
  build_id: <stable identifier>
  release_entry: <method or command>
  release_mode: release
  candidate_path: <absolute path; unified mode includes variant/platform/build-id>
  platform_package_path: <absolute path>
  build_log: <absolute path>
  wasm_function_split: disabled | active | unknown
  active_split_artifacts: []
  main_package_image_limit_bytes: 512000
  largest_main_package_image: {path: <relative path or null>, bytes: 0}
  main_package_image_result: passed | failed
  post_build_logo_removal: <receipt>
  cdn_source_path: <absolute path or null>
  cdn_upload_status: uploaded-and-publicly-verified | not-applicable | blocked | failed # not-applicable 仅限非抖音
  cdn_manifest_sha256: <value or null>
  result: passed | blocked | failed
```

只有无函数分包、图片预算、去水印全部通过，且抖音 CDN 上传与公开校验无条件通过（其他平台在存在远程资源时通过），`result` 才能是 `passed`。
