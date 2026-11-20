# 抖音小游戏四项必接能力

所有抖音小游戏（包括审核包、正式包和马甲包）必须接入并验证侧边栏复访、运行时 QA 面板、CDN 和排行榜。来源需求未提到、项目当前没有入口或历史版本未接入，都不能作为 `not-applicable`；缺少平台配置、账号、真机、远程目标或实现能力时标记 `blocked`，不能静默跳过。

需求实现阶段为四项能力分别建立稳定标准需求记录并冻结提示词，不依赖外部商业化文档是否存在对应行：`DY-MANDATORY-SIDEBAR`、`DY-MANDATORY-QA`、`DY-MANDATORY-CDN`、`DY-MANDATORY-RANK`。每项独立实现、验证和提交；不得合并成一个无法追踪的“抖音适配”提交。

## 1. 实施基线

实施侧边栏和排行榜前，完整读取[平台能力 skill](../../minigame-sdk-platform-capabilities/SKILL.md)，并以目标工程实际安装的宿主 SDK/API 为准复核接口：

Unity / 团结项目按平台能力资产语义合并并保留项目现有架构；Cocos 等其他引擎用本引擎语言重写等价行为，不复制 C# 或 `.meta`。

## 2. 侧边栏复访

- 首页必须有可见、可点击的侧边栏入口，位于左侧中下区域。以屏幕左上为原点，入口中心 x 位于宽度的 `0–25%`、y 位于高度的 `50–75%`，默认 y 为 `62%`；父节点覆盖首页安全区，布局组不得覆盖最终位置。
- 入口只挂在真实首页，不得挂到关卡 Canvas 冒充首页。已领取奖励后仍保留入口；宿主不支持侧边栏时显示友好说明，不能隐藏入口冒充完成。
- 实现 `GoToAdd -> CanClaim -> Claimed` 状态机、宿主能力检查、平台跳转、`onShow`/`onHide` 生命周期和热启动参数处理。`launch_from=homepage` 且 `location=sidebar_card` 作为从侧边栏返回的判定信号。
- 侧边栏奖励必须幂等并持久化；重复回调、重复进入、前后台切换或重启不得重复发奖。监听不得重复注册，销毁时正确解绑。
- 接通真实弹窗、素材、按钮命中区、状态刷新和平台回调，修复空引用；不能只复制模板或只打印成功日志。
- 至少取得源码接线、抖音 IDE 运行和当前 `build_id` 真机证据。真机验收至少包含两种不同布局/设备，以及“主页点击入口 → 平台跳转/回调 → 从侧边栏返回 → 再进关 → 完成一次操作”的完整往返；核对背景、输入、安全区和奖励幂等。

## 3. 运行时 QA 面板

抖音目标无条件执行[Unity 小游戏 QA 面板接入规范](qa-panel.md)。共享命令服务可以在 `miniGame` 实现，但抖音正式 Release 候选必须包含五连击入口、密码弹窗、项目字体、授权策略和可用命令；不得用 Development/QA 专用包替代。回执、截图、日志和录屏不得暴露明文密码。

## 4. CDN

- 抖音候选必须具有真实远程资源目录、不可变内容版本和明确 CDN 目标；`cdn_upload_status: not-applicable` 对抖音无效。工程尚无远程资源链路时先补齐构建、清单和运行时引用，缺少信息表目标或上传能力时阻塞。
- 只上传本次候选的精确远程资源目录。信息表前缀、工程配置、清单、Bucket/域名和运行时 URL 必须一致。
- 使用包含相对路径、大小与 SHA-256 的本次构建清单；启动清单使用 `manifest.<buildId>.json` 等不可变路径，不能依赖稳定文件名加查询参数逃避缓存。
- 上传后分别验证对象存储和公开 CDN，公开逐文件比较完整 SHA-256。CDN 未通过时抖音候选不得进入 IDE 完成态、录屏或统计阶段。

## 5. 抖音原生排行榜

- 首页必须提供可见、可点击的排行榜入口，使用抖音原生 IM 排行榜能力；默认不把微信开放数据域代码或自绘假榜单移植到抖音。
- 上架前在抖音开发者后台创建排行榜并确认 `dataType`、`zoneId`、指标、排序和展示分区。指标必须来自真实结算或存档；用已过关卡数时从 `1` 开始，不上报随机值、编辑器样例或伪造玩家数据。
- 结算或进度确认后通过当前 SDK 等价于 `TT.SetImRankData` 的接口上报；打开排行榜前再次从项目的 `RankScoreProvider` 或等价真实数据源同步最新值，避免展示旧数据。
- 通过当前 SDK 等价于 `TT.GetImRankList` 的接口拉起平台原生 UI；`dataType`、`zoneId`、标题、后缀、关系范围和排行周期必须与后台及上报合同一致。
- 处理登录前置、成功、失败、空数据、重复打开、前后台恢复和网络异常；失败时保留可理解反馈，不降级为模拟排行。
- 至少取得后台配置索引、源码接线、真实上报回执、抖音 IDE 入口/日志和当前 `build_id` 真机原生排行榜画面。排行榜打开及结果上报行为必须进入自统计与抖音官方 SDK 的双通道统计矩阵。

## 6. 统一回执与失败关闭

```yaml
douyin_required_capabilities:
  sidebar:
    source_result: passed | failed | blocked
    ide_result: passed | failed | blocked
    real_device_roundtrip: passed | failed | blocked
    build_id: <value>
  qa_panel:
    release_candidate_result: passed | failed | blocked
    build_id: <value>
  cdn:
    upload_status: uploaded-and-publicly-verified | blocked | failed
    manifest_sha256: <value>
    build_id: <value>
  leaderboard:
    backend_config: verified | blocked | failed
    score_upload_result: passed | failed | blocked
    native_ui_result: passed | failed | blocked
    real_device_result: passed | failed | blocked
    build_id: <value>
  result: passed | failed | blocked
```

四项必须绑定同一最终候选 `build_id`。任一项缺失、使用旧候选证据、只有模板/静态搜索/模拟数据，或状态不是 `passed`，抖音需求实现阶段不得成为 `completed`。
