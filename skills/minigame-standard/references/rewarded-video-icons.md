# 激励视频图标素材、抖音规范与项目实例

在使用本技能附带图标、根据截图寻找原始资源或处理 Unity Sprite 图集引用时读取本文件。以当前用户选择和当前工程实际引用为准。

## 抖音小游戏强制规范

抖音目标中只要存在视频广告，必须把所有实际触发视频广告的按钮或背景纳入全局审计，并使用抖音官方“摄像机 + 播放三角”标识。平台规范高于项目既有图标和用户自绘样式；本技能附带素材及下方历史项目场记板都不是抖音官方标识。

- 官方审核说明：<https://developer.open-douyin.com/docs/resource/zh-CN/mini-game/guide/contactus/ad>
- 官方规范说明：<https://developer.open-douyin.com/forum/synthesize/post/60054b91d09204ee1b0ef9b7>
- 官方 PDF：<https://sf1-cdn-tos.douyinstatic.com/obj/microapp/frontend/docs/images/microapp.pdf>
- 官方 PSD：<https://sf1-cdn-tos.douyinstatic.com/obj/microapp/frontend/docs/images/ad-exposure-icon.psd>

使用官方提供的白、黑或灰色版本适配背景，不重新绘制。默认大小为 `38×28 px @3x`；按钮内 icon 高度按按钮高度约 `40%` 计算并就近取 4 的倍数，宽度等比缩放；大面积背景中的 icon 高度不低于同级文案字号。icon 位于同层级文案左侧，且与文案、载体保持强对比。不得使用纯描边样式，不得修改透明度、圆角或长宽比例。

全局审计从真实广告调用反查 UI，而不是只搜资源名：盘点广告封装、`createRewardedVideoAd`、展示调用与成功/取消/失败回调，逐项映射场景、Prefab、嵌套或动态生成实例、按钮文案、Sprite GUID/fileID、图集与运行时换图路径。替换范围是所有视频广告触发入口；普通教学视频播放钮、演示框、按钮背景和广告不可用红斜线不替换。

抖音静态审计至少证明：调用点与入口清单闭环、每个入口引用官方素材、旧自制标识在目标入口零残留、动态生成与运行时换图已覆盖、其他平台资源未被污染。IDE 运行时逐入口核对实际显示、文案左侧位置、比例、尺寸、对比、透明边缘、点击不被装饰图拦截，以及成功、取消/失败和不可用状态；存在“看/不看”选择时，还要检查同时出现、大小一致、邻近且不默认选择观看。模拟 Provider 或仅截图不能证明真实广告播放与发奖。

回执保存 `entry_id`、广告调用点、UI 层级、当前/目标 Sprite 标识、静态结果、IDE 截图或录像索引、状态覆盖和未验证项。抖音版本只有入口清单全部关闭且静态与 IDE 证据指向同一最终候选时，图标门禁才可记为 `passed`。

## 附带素材

| 文件 | 外观与用途 |
| --- | --- |
| [generated-camera-transparent-play-v3.png](../assets/rewarded-video/generated-camera-transparent-play-v3.png) | 1254×1254 PNG，内置 imagegen 生成的新版摄像机图标；白色机身、黑色描边、浅灰底缘。外部背景与播放三角内部均镂空透明，无紫色填充。作为非抖音强制场景的候选美术素材保存，本实例项目最终没有采用，也不是抖音官方标识。 |
| [cluck-survivor-native-clapperboard.png](../assets/rewarded-video/cluck-survivor-native-clapperboard.png) | 90×90 PNG，来自 cluck-survivor-miniGame 工程原有 `Icon_Advertisement.png` 的场记板图标视觉样例。该项目实际使用的权威资源是下述 Sprite `.asset`，应优先引用工程内资源。 |

使用新版候选图时保留原始 PNG alpha，不能把三角中心改成紫色、白色或黑色填充，也不能把棋盘格烙入图片。用户要求透明中心时，抽样检查中心 alpha 为 0，并在有色背景下确认能透出底色。将候选图归档到 skill 不等于将其导入任何游戏项目；用户已明确指定现有图标时不使用候选图替代。

## cluck-survivor-miniGame 已采用的标准

用户最终选择商店宝石卡底部的原有场记板图标，要求该项目全部激励视频入口统一复用它。以下路径与 GUID 是本项目历史定位证据，不作为其他项目的固定值，也不得覆盖抖音官方标识要求：

- 权威 Sprite：`Assets/Resources/sprite/ui/icon/Icon_Advertisement_0.asset`，GUID `a2a0f57756f67344bb2e7ccba0c884ea`，fileID `21300000`，引用 type `2`。
- 截图定位路径：`GameScene / ViewManager / SafeArea / MainView / SView_Shop (1) / Shop_Scroll / Viewport / Content / Exchange / Exchange / Shop_Gem_Free_Item_1 / Get_Text_AD / Icon`。
- 被统一的另一种资源：`Assets/Resources/sprite/ui/icon/RewardedVideoIcon.png`，GUID `02e6af0581a449629946b9dd290939e9`，fileID `21300000`，引用 type `3`。替换采用修改使用处的 Sprite 引用，保留原资源和无关改动。
- 权威 Sprite 实际引用图集纹理 GUID `b919e41910d11e941be223a2f15be4af`；只覆盖同名 `Icon_Advertisement.png` 不会改变该 Sprite 的显示。`Shop_Icon_AD_0.asset` 外观相近，但不是截图所指节点的实际绑定资源。
- `Icon_Advertisement_None` 是不可用状态的红斜线叠层，由 `AdButtonView.noAdMark` 控制，应保留。武器演示 `Video_Frame`、按钮背景与激励视频主体图标分开核查。

后续新增或修改激励视频入口时复用该项目选定的 Sprite。全局任务再次执行前重扫当前引用，不把历史数量当作固定断言；覆盖场景、Prefab、运行时加载与换图，保留各位置布局和显隐逻辑。横向摄像机改为方形场记板时尤其检查保持比例及实际可见尺寸。
