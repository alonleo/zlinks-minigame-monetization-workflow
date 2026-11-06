---
name: minigame-kodo-upload
description: 将用户指定的小游戏远程资源上传到七牛 Kodo，从资源目录和游戏内 CDN 配置推导目标前缀，复用本机加密凭据，完成增量上传、哈希校验与回执。用于“上传分包到七牛”“把这个路径推送到 Kodo”；不负责生成资源、代码提交、审核或正式发布。
---

# 小游戏资源快速上传 Kodo

用户明确要求上传即具有本次上传授权，不重复索取确认。仅要求创建 skill、查看配置或预检时不执行远端写入。缺失信息先从项目和已确认配置解析，只询问无法确定且影响目标的字段。

## 定位资源与目标

1. 只使用用户明确指定的文件或资源目录；未给出路径时先询问，不从历史记录、目录排序或修改时间猜测来源。
2. 若指定目录包含 `game.js`、`unity-namespace.js` 或 `cdn-manifest.json`，读取其中的 `DATA_CDN`、`dataFileSubPrefix`、`streamingUrlPrefixPath`、`cdn_prefix` 和文件 URL 进行交叉核对。只读取文本，不执行项目 JS；配置与清单冲突时停止写入并解释。
3. 区分平台代码包与 CDN 资源。常见目录中的 `minigame/` 是平台代码包，`cdn-upload/` 是远程资源目录；以用户指定目录和清单为准。不能上传 Unity 工程根目录、全部 Builds 或整个 release 目录。有 CDN 清单时使用 `--manifest` 精确验证文件集合、大小和 SHA-256。
4. Bucket 和访问域名取用户当前指示或 [本机默认目标](references/default-target.json)。前缀始终从本次游戏配置/用户指示解析，保留大小写、去除域名和开头斜杠。目录内容直接映射为 `前缀 + 相对路径`，不要多套一层 `cdn-upload`。

## 凭据与环境

通过七牛官方 Python SDK 上传，无需启动 Kodo Browser。运行命令见 [操作参考](references/operations.md)。复用本机专用运行时，依赖在 `scripts/requirements.txt`，不向游戏工程安装依赖。

凭据优先读取进程环境 `QINIU_ACCESS_KEY` / `QINIU_SECRET_KEY`，否则读取 `%LOCALAPPDATA%/Codex/minigame-kodo-upload/credentials.dpapi`，由 Windows DPAPI 绑定当前 Windows 用户加密。为兼容改名前已保存的凭据，若新路径不存在，可只读回退到 `%LOCALAPPDATA%/Codex/wechat-kodo-upload/credentials.dpapi`。`credentials.py status` 只报告可用性，不回显值。

用户提供 AK/SK 时用 `credentials.py store --stdin` 接收 JSON，通过支持 stdin 的进程工具传入；不要把密钥放入命令行参数、脚本、项目、报告、明文临时文件或永久环境变量。交互终端可直接运行 `credentials.py store` 隐藏输入。字段标签和明确的行尾格式反斜杠不是密钥内容；存在歧义时不反复猜测。不读取 Kodo Browser 的其他账号凭据。

## 上传闭环

1. 运行 `upload.py --source ... --bucket ... --prefix=... --domain ... --manifest ... --check`，只读预检。省略 `--check` / `--upload` 时只生成本地计划，不访问网络。
2. 通过 commentary 告知源目录、Bucket/前缀、数量/大小与覆盖情况。已有明确上传授权且预检通过，直接运行相同参数加 `--upload`。
3. 默认保留目录结构，跳过 etag 和大小相同的对象，不删除远端文件。同名不同内容会阻止整批写入。只有用户已明确要求更新/替换该目录时使用 `--overwrite`；未授权的冲突先报告具体 key 再询问。不要另改版本目录绕过冲突。
4. 默认 4 文件并发，大文件使用 SDK V2 分片；管理和上传使用 HTTPS。每个文件上传后读取 stat 校验 etag 和大小。SDK 有有限重试，脚本不循环重跑整批。回执未知先查远端，匹配则记已验证，否则停止并保留报告。鉴权失败修正凭据或权限，不尝试其他账户/空间。
5. `.bin.br` 保留文件名和二进制内容，不自行解压或添加 `Content-Encoding: br`。运行 `verify_cdn.py --report <本次上传报告>`，用无凭据 GET 流式核对完整文件 SHA-256。区分 Kodo 成功与 CDN 访问成功；下载域名不接收 AK/SK、管理签名或上传 token。已授权上传中，若源对象正确而 CDN 确认返回旧内容，可刷新本次资源中确切失配的文件 URL 一次，再有界复验，见操作参考；不扩大到目录或全站。
6. 报告在 `%LOCALAPPDATA%/Codex/minigame-kodo-upload/reports/`。最终简述新增/相同/失败数量、目标目录、访问结果和报告链接。Kodo 上传不代表平台代码已提交，也不代表通过运行或真机验收。

不通过本 skill 自动改写游戏配置、刷新全站 CDN、清空 Bucket、提交审核或发布；用户另有明确要求时按其范围处理。
