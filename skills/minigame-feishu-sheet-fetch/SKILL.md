---
name: minigame-feishu-sheet-fetch
description: 只读拉取飞书电子表格中的小游戏信息表，解析工作表、表头和唯一目标游戏行，并输出需求实现阶段需要的规范化 JSON。用于商业化流程第 1 步的信息表读取；不用于飞书多维表格、文档正文解析或任何回写。
---

# 飞书电子表格信息拉取

本 skill 负责把用户指定的飞书电子表格目标行转换为需求实现阶段可消费的只读输入。只访问用户指定的表格，不搜索或猜选其他表格，不回写单元格、评论或状态。

## 输入

需要：

- 项目配置文件；其中保存飞书电子表格 URL、知识库节点 URL 或 `spreadsheet_token`；
- 目标游戏的稳定 ID 或精确名称；
- 工作表 ID/标题；URL 已含 `?sheet=` 或工作簿只有一个可见工作表时可以省略；
- 自建应用凭据，默认以当前 Windows 用户 DPAPI 密文保存在项目配置中；未配置密文时才读取 `FEISHU_APP_ID`、`FEISHU_APP_SECRET` 环境变量；
- 应用已取得电子表格只读权限，且目标表格已允许该应用身份访问。

配置文件不得保存明文 App Secret。使用 `scripts/import_credentials.py` 从已有可信 JSON 读取凭据并写入 DPAPI 密文；密文只可由导入时的 Windows 用户解密。凭据只用于换取内存中的访问令牌，不写入输出、日志或命令行。

## 执行

先完整读取 [操作与字段协议](references/operations.md)，再运行：

```powershell
python skills/minigame-feishu-sheet-fetch/scripts/fetch_sheet.py `
  --config 'config/feishu-sheet.json' `
  --target-game '<游戏稳定 ID 或名称>' `
  --output '<需求台账目录>/source-row.json'
```

脚本依次执行：

1. 读取配置，但拒绝其中出现 App Secret 或访问令牌；
2. 使用自建应用凭据获取 `tenant_access_token`；
3. 直接 URL 提取 `spreadsheet_token`；知识库 URL 先解析节点并确认 `obj_type` 是电子表格；
4. 查询工作簿中的工作表并唯一确定目标工作表；
5. 只读目标范围，识别信息表表头及项目列别名；
6. 对游戏标识做去首尾空白、大小写不敏感的精确匹配；
7. 目标为零行或多行时失败，不猜选；
8. 输出来源位置、规范字段、缺失字段、原始表头与原始行。

需求实现阶段使用 `--strict`，要求 `game`、`commercialization_document`、`cdn_prefix`、`target_platform`、`project_path` 五个规范字段都有对应列；CDN 单元格本身允许为空。若表头使用项目特有名称，通过 `--aliases` 提供 JSON 别名，不修改脚本内默认映射来迎合单个项目。

## 边界与失败

- 本 skill 只拉取电子表格目标行，不读取“商业化方案”链接中的飞书文档正文。
- 知识库 URL 解析出的对象不是 `sheet` 时立即停止，不把文档或多维表格当作电子表格处理。
- 工作表不唯一、表头不完整、目标行不存在或重复、权限不足、接口响应超限时停止并输出明确错误。
- 不自动降级为浏览器抓取、OCR、搜索结果或本地旧副本。
- `--auth-mode legacy` 只在项目明确仍使用旧鉴权接口时启用，不在鉴权失败后自动携带凭据重试另一接口。

成功输出是需求实现 skill 建立只读需求基线的输入证据，不代表商业化文档已经解析，也不代表需求已进入 `extracted` 状态。
