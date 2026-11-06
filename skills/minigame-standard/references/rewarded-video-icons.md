# 激励视频图标素材与项目实例

在使用本技能附带图标、根据截图寻找原始资源或处理 Unity Sprite 图集引用时读取本文件。以当前用户选择和当前工程实际引用为准。

## 附带素材

| 文件 | 外观与用途 |
| --- | --- |
| [generated-camera-transparent-play-v3.png](../assets/rewarded-video/generated-camera-transparent-play-v3.png) | 1254×1254 PNG，内置 imagegen 生成的新版摄像机图标；白色机身、黑色描边、浅灰底缘。外部背景与播放三角内部均镂空透明，无紫色填充。作为候选美术素材保存，本实例项目最终没有采用。 |
| [cluck-survivor-native-clapperboard.png](../assets/rewarded-video/cluck-survivor-native-clapperboard.png) | 90×90 PNG，来自 cluck-survivor-miniGame 工程原有 `Icon_Advertisement.png` 的场记板图标视觉样例。该项目实际使用的权威资源是下述 Sprite `.asset`，应优先引用工程内资源。 |

使用新版候选图时保留原始 PNG alpha，不能把三角中心改成紫色、白色或黑色填充，也不能把棋盘格烙入图片。用户要求透明中心时，抽样检查中心 alpha 为 0，并在有色背景下确认能透出底色。将候选图归档到 skill 不等于将其导入任何游戏项目；用户已明确指定现有图标时不使用候选图替代。

## cluck-survivor-miniGame 已采用的标准

用户最终选择商店宝石卡底部的原有场记板图标，要求该项目全部激励视频入口统一复用它。以下路径与 GUID 是本项目定位证据，不作为其他项目的固定值：

- 权威 Sprite：`Assets/Resources/sprite/ui/icon/Icon_Advertisement_0.asset`，GUID `a2a0f57756f67344bb2e7ccba0c884ea`，fileID `21300000`，引用 type `2`。
- 截图定位路径：`GameScene / ViewManager / SafeArea / MainView / SView_Shop (1) / Shop_Scroll / Viewport / Content / Exchange / Exchange / Shop_Gem_Free_Item_1 / Get_Text_AD / Icon`。
- 被统一的另一种资源：`Assets/Resources/sprite/ui/icon/RewardedVideoIcon.png`，GUID `02e6af0581a449629946b9dd290939e9`，fileID `21300000`，引用 type `3`。替换采用修改使用处的 Sprite 引用，保留原资源和无关改动。
- 权威 Sprite 实际引用图集纹理 GUID `b919e41910d11e941be223a2f15be4af`；只覆盖同名 `Icon_Advertisement.png` 不会改变该 Sprite 的显示。`Shop_Icon_AD_0.asset` 外观相近，但不是截图所指节点的实际绑定资源。
- `Icon_Advertisement_None` 是不可用状态的红斜线叠层，由 `AdButtonView.noAdMark` 控制，应保留。武器演示 `Video_Frame`、按钮背景与激励视频主体图标分开核查。

后续新增或修改激励视频入口时复用该项目选定的 Sprite。全局任务再次执行前重扫当前引用，不把历史数量当作固定断言；覆盖场景、Prefab、运行时加载与换图，保留各位置布局和显隐逻辑。横向摄像机改为方形场记板时尤其检查保持比例及实际可见尺寸。
