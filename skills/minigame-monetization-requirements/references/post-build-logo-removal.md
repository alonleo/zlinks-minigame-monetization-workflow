# 构建后引擎 Logo / 水印处理协议

本协议适用于本工作流产出的 Unity / 团结微信、抖音小游戏包。每个平台完成本次最终导出后、进入分包与发布候选门禁前，必须使用 `D:\Tools\MiniGameKit\remove-logo` 处理该平台的实际导出目录。重新构建会覆盖处理结果，因此每次新构建都必须重新执行，不能沿用旧包回执。

## 1. 工具与依赖预检

固定使用下列脚本，不复制到游戏工程，也不修改脚本来绕过校验：

- `D:\Tools\MiniGameKit\remove-logo\disable_watermark_logic.py`：校验并禁用已支持的 WASM 水印调度函数；
- `D:\Tools\MiniGameKit\remove-logo\blank_unityweb_texture.py`：把默认的 `UnityWatermark-trial` 和 `UnityWatermark-trial-big` 贴图透明化。

执行前完整读取该目录的 `README.md`，确认 Python、`brotli`、`UnityPy` 和 PATH 中的 Node.js 可用。缺少依赖、脚本目录不存在或目标导出目录不唯一时停止，不下载依赖、不猜路径，也不改用历史构建。

脚本默认范围不包含加载页 `images/unity_logo.png`、其他名称的水印或远程资源。只有来源需求明确要求并完成入口定位时，才把这些内容作为独立需求处理；不得把默认脚本执行结果扩大表述为所有 Logo 已移除。

## 2. 每个平台的执行顺序

对微信和抖音分别绑定本次构建的绝对导出目录，并按以下顺序执行：

1. 记录处理前候选目录、平台、分支 HEAD、构建标识、`game.js`、WASM 与目标数据包哈希；
2. 运行 `disable_watermark_logic.py <导出目录>` 做默认只读预检；需要诊断未知指纹时改用 `--inspect`，不得把诊断成功当作允许写入；
3. 只读预检确认完整指纹、WASM 与 `game.js` 哈希关系、压缩标记和目标范围均有效后，对同一目录运行 `disable_watermark_logic.py <导出目录> --apply`；
4. 运行 `blank_unityweb_texture.py <导出目录> --dry-run`，确认候选列表包含实际发布使用的主数据资源，尤其是 `data-package` 或微信实际上传的根目录资源；
5. 对同一目录运行 `blank_unityweb_texture.py <导出目录>`，仅使用默认目标名和透明色；
6. 重新运行 WASM `--inspect`，确认 `already_disabled` 和 `unity_brotli_marker`；用临时副本解码抽样或等价只读检查确认两张默认目标贴图的实际像素已透明。贴图名称会继续存在，不能以再次 `--dry-run` 返回“无匹配”作为通过标准；
7. 记录处理后 `game.js`、WASM、数据包和相关清单哈希、脚本退出码、报告与备份路径，并检查备份没有进入发布目录；
8. 重新核对或生成受内容变化影响的大小、内容哈希、code MD5、平台分包库存和渠道清单。处理后的目录才是后续唯一候选。

两个脚本只允许作用于本次已确认的本地导出目录。不得对工程源资源、旧候选、CDN 对象、开发者工具缓存或另一平台目录顺手执行。

## 3. 完成与失败语义

每个平台保存一份 `post_build_logo_removal` 回执：

```yaml
post_build_logo_removal:
  platform: wechat | douyin
  tool_root: D:\Tools\MiniGameKit\remove-logo
  candidate_path: <absolute export path>
  source_build_id: <build identifier>
  source_branch: miniGame-wx | miniGame-dy
  source_commit: <platform HEAD>
  wasm_before_sha256: <value>
  wasm_after_sha256: <value>
  data_files_processed: []
  logic_inspect_result: <summary>
  logic_apply_exit_code: <value>
  texture_dry_run_result: <summary>
  texture_apply_exit_code: <value>
  verification_result: passed | failed | blocked
  backup_paths: []
  limitations: []
```

以下任一情况均失败关闭，不进入分包与发布候选门禁：

- WASM 指纹未知、无符号版本不受支持、拆分布局不受支持或 `game.js` / WASM 哈希不一致；
- 脚本退出非零、只处理了部分目标、写回或 Brotli / UnityWeb 往返校验失败；
- 贴图脚本未扫描到实际发布主数据资源，或处理后默认目标的实际像素未透明；
- 处理后清单、code MD5、平台分包库存或实际文件不一致；
- 包被重建、再次导出或被其他步骤覆盖，但没有重新执行本协议；
- 备份、`.bak`、报告、symbols、prepared 目录或开发者工具缓存进入发布范围。

静态处理成功只证明脚本目标已修改，不能替代平台 IDE 和真机画面验收。IDE 必须打开处理后的同一候选，并核对启动与运行画面、错误日志和缓存影响。
