# 操作与字段协议

## 飞书应用准备

使用企业自建应用的应用身份访问。应用需要开通电子表格只读能力，并且目标电子表格必须处于该应用身份可访问的数据范围。新应用默认可使用 OAuth 2.0 Client Credentials：

- `POST https://accounts.feishu.cn/oauth/v3/token`
- `grant_type=client_credentials`
- App ID 和 App Secret 分别来自进程环境变量 `FEISHU_APP_ID`、`FEISHU_APP_SECRET`

从 `D:/Tools/auto-pack` 导入的既有应用按其现有客户端配置使用 `legacy`，对应 `POST /open-apis/auth/v3/tenant_access_token/internal`。两种模式不自动互相回退。

读取链路：

- Wiki URL：`GET /open-apis/wiki/v2/spaces/get_node?token={node_token}`，并要求返回 `obj_type=sheet`；
- `GET /open-apis/sheets/v3/spreadsheets/{spreadsheet_token}/sheets/query`
- `GET /open-apis/sheets/v2/spreadsheets/{spreadsheet_token}/values/{range}`

相关官方文档：

- https://open.feishu.cn/document/uAjLw4CM/ukTMukTMukTM/authentication-management/access-token/tenant_access_token
- https://open.feishu.cn/document/server-docs/docs/docs-overview
- https://open.feishu.cn/document/server-docs/docs/sheets-v3/overview

使用知识库 URL 时，应用还需要知识库节点读取权限，并且能够访问该知识空间。

## 项目配置

默认配置位于项目根目录 `config/feishu-sheet.json`。配置只保存非敏感参数：

```json
{
  "source": {
    "url": "https://example.feishu.cn/wiki/<node-token>",
    "sheet": null,
    "target_game": null,
    "header_row": null,
    "range": null
  },
  "auth": {
    "mode": "legacy",
    "app_id_env": "FEISHU_APP_ID",
    "app_secret_env": "FEISHU_APP_SECRET"
  },
  "credentials": {
    "provider": "windows-dpapi-current-user",
    "source": "D:\\Tools\\auto-pack\\tools\\check_resources\\feishu_config.json",
    "app_id_dpapi": "<base64 DPAPI ciphertext>",
    "app_secret_dpapi": "<base64 DPAPI ciphertext>"
  },
  "limits": {
    "max_rows": 5000,
    "max_columns": 100,
    "timeout": 30,
    "retries": 2
  },
  "aliases": null,
  "strict": true,
  "output": null
}
```

`target_game` 可以在配置中固定，也可以每次通过 `--target-game` 覆盖。`sheet`、`range`、`aliases` 和 `output` 同样允许命令行覆盖；配置中的相对文件路径以配置文件所在目录为基准。配置出现明文 `app_secret`、`access_token` 或 `tenant_access_token` 等敏感键时，脚本拒绝运行。

从现有配置导入并加密：

```powershell
python skills/minigame-feishu-sheet-fetch/scripts/import_credentials.py `
  --source 'D:\Tools\auto-pack\tools\check_resources\feishu_config.json' `
  --target 'config\feishu-sheet.json'
```

导入脚本不接受命令行 Secret，也不打印凭据。重新导入会替换配置中的旧密文；不要手工复制明文。

## 凭据设置

只有配置没有 DPAPI 密文时，才在当前进程或其父进程设置环境变量：

```powershell
$env:FEISHU_APP_ID = '<app-id>'
$env:FEISHU_APP_SECRET = '<app-secret>'
```

不要在终端历史、命令参数、报告或错误文本中打印 Secret 和访问令牌。运行结束后可从当前会话移除环境变量。

## 默认字段映射

| 规范字段 | 默认别名示例 |
| --- | --- |
| `game` | 游戏标识、游戏 ID、游戏编号、游戏名称、游戏名、游戏、项目名称、项目 |
| `commercialization_document` | 商业化方案、商业化文档、变现方案、商业化需求 |
| `cdn_prefix` | CDN 路径、CDN 地址、资源路径、远程资源路径 |
| `target_platform` | 目标平台、平台、小游戏平台、发布平台 |
| `project_path` | 工程路径、项目路径、工程、仓库、仓库地址、Git 仓库 |

表头匹配忽略空格、下划线、连字符和 ASCII 大小写。不要使用包含匹配寻找游戏；目标游戏只做规范化后的精确匹配。

项目自定义别名文件示例：

```json
{
  "game": ["内部游戏代号"],
  "commercialization_document": ["商业化需求文档"]
}
```

自定义别名会追加到默认别名，不会移除安全必需字段。

## 常用命令

使用项目配置：

```powershell
python skills/minigame-feishu-sheet-fetch/scripts/fetch_sheet.py --config 'config/feishu-sheet.json' --target-game 'GAME-001' --output '.codex/monetization/GAME-001/source-row.json'
```

按工作表标题选择：

```powershell
python skills/minigame-feishu-sheet-fetch/scripts/fetch_sheet.py --config 'config/feishu-sheet.json' --sheet '小游戏信息表' --target-game 'GAME-001'
```

只查看工作表清单：

```powershell
python skills/minigame-feishu-sheet-fetch/scripts/fetch_sheet.py --config 'config/feishu-sheet.json' --list-sheets
```

默认读取目标工作表实际网格范围，但最多 5000 行、100 列。表格更大或信息表位于特定区域时，用 `--range 'A1:Z8000'`、`--max-rows` 或 `--max-columns` 显式调整。`--range` 不包含工作表 ID，脚本会自动加上已确认的 `sheet_id`。

## 输出和退出码

成功 JSON 包含：

- `source`：表格、工作表、读取范围、表头行和目标数据行；
- `fields`：五个规范字段的文本值；
- `links`：从富文本单元格中可辨认的链接；
- `missing_columns`、`empty_fields`：缺列和空值；
- `headers`、`raw_row`：用于追溯的原始值。

退出码：`0` 成功，`2` 表示输入、表头或唯一性错误，`3` 表示鉴权、网络或飞书 API 错误。接口失败时输出飞书错误码和脱敏消息，不输出访问令牌或 Secret。
