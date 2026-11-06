# 操作参考

## 环境

```powershell
$taskSkill = (Resolve-Path 'skills\minigame-kodo-upload').Path
$taskRuntime = Join-Path $env:USERPROFILE '.codex\runtimes\minigame-kodo-upload'
$legacyRuntime = Join-Path $env:USERPROFILE '.codex\runtimes\wechat-kodo-upload'
if (-not (Test-Path -LiteralPath $taskRuntime) -and (Test-Path -LiteralPath $legacyRuntime)) {
  $taskRuntime = $legacyRuntime
}
$taskPython = Join-Path $taskRuntime 'Scripts\python.exe'
# 仅在运行时缺失时创建，已有环境直接复用。
python -m venv $taskRuntime
& $taskPython -m pip install -r (Join-Path $taskSkill 'scripts\requirements.txt')
& $taskPython (Join-Path $taskSkill 'scripts\credentials.py') status
```

上述 `$taskSkill` 从本商业化流程工程根目录解析；当前工作目录是游戏工程时，改为本项目所复制 `skills/minigame-kodo-upload` 的绝对路径，不回退使用 `.codex/skills` 中的副本。首次设置在交互终端运行 `credentials.py store`；自动化使用 stdin JSON，字段为 `access_key`、`secret_key`。秘密只传入子进程 input，不作为 argv，不打印包含秘密的异常对象。DPAPI 密文不能复制到另一 Windows 用户直接使用。保存成功只证明本地加密完成；目标 `--check` 才验证空间权限。

## 命令

```powershell
$taskArgs = @(
  (Join-Path $taskSkill 'scripts\upload.py'),
  '--source', 'D:\Exports\my-game\cdn-upload',
  '--bucket', 'wjtech',
  '--prefix=game/miniGame/wx/MyGame/1.0/',
  '--domain', 'https://cdn.zllinks.com',
  '--manifest', 'D:\Exports\my-game\cdn-manifest.json'
)
& $taskPython @taskArgs --check
# 已授权且预检通过后执行：
& $taskPython @taskArgs --upload
# 将上一条输出的真实报告路径传入：
& $taskPython (Join-Path $taskSkill 'scripts\verify_cdn.py') --report 'C:\path\to\upload-report.json'
```

不能使用示例游戏前缀作为默认值。无清单时省略 `--manifest`，仍核对实际引用路径。空间根目录用单个参数 `--prefix=`，避免 PowerShell 丢失空字符串。支持文件或目录，默认排除私钥、`.env`、Git、Unity 缓存、node_modules 和平台私人项目配置。额外排除用重复 `--exclude` 相对路径 glob。

`--list-buckets` 是只读账户空间列表检查；空间受限凭据可能无法列举所有空间，应改用指定目标 `--check`，不要因此断言凭据无效。

## 结果和故障

- `plan` 仅本地扫描，`check` 仅读远端，`upload` 执行上传。退出码 0 表示该模式完成，plan/check 成功不能说成上传成功。大于 4 MiB 的文件必须使用 SDK V2 分片上传，不能提高到 8 MiB，否则会越过七牛表单上传边界。
- `new` / `replace` 是预检结果，`unchanged` 是 etag+大小一致，`conflict` 为未授权同名变更，`uploaded` 为回执及 stat 一致，`verified_after_uncertain_receipt` 为回执未知但 stat 一致，`failed` / `not_started` 为未完成。
- 预检全部通过后才开始写入。并发失败时已开始的少量文件可能完成，后续任务停止，不回滚删除。先重新只读核对，不能在回执未知时自动整批覆盖重试。
- Kodo stat 成功而 CDN 404/旧内容时，检查负缓存及 `Age`/`ETag`，再作有界 GET 验证；不要重复上传已校验对象。JSON 还应比较解析结果，区分仅空白压缩和真实旧数据。已授权的资源上传可包含修复该批次的旧缓存；仅预检时不刷新。
- 本地扫描/上传期间发生变化会报错，改用稳定构建产物，重新核对当前指针。
- 不输出 SDK 原始 ResponseInfo、异常、请求头或 token。诊断只记录阶段、状态码和 key。

实现依据：[七牛官方 Python SDK](https://developer.qiniu.com/kodo/sdk/python)、[官方源码](https://github.com/qiniu/python-sdk)、[上传策略](https://developer.qiniu.com/kodo/1206/put-policy)。锁定 qiniu 7.18.0；升级需复核 HTTPS、覆盖策略、分片、返回 hash 和 stat 行为。

## 精确文件缓存刷新

在已授权上传且源站正确、CDN 确认旧内容时，使用 `qiniu.CdnManager(Auth(...))`，凭据从 `credentials.load()` 获取，设置 `manager.server = 'https://fusion.qiniuapi.com'`，调用 `refresh_urls([确切的失配资源URL])` 一次。不调用 `refresh_dirs`，不重复提交结果未知的请求。记录 HTTP 状态、响应 `code`、`requestId`、`taskIds`、无效 URL 和配额；不能输出 SDK 原始对象。

HTTP 200 且响应 code 200 / taskId 只证明请求已接受。每 30–60 秒只读查询任务或重新 GET 该文件，最长约 10 分钟。任务成功后再运行完整 `verify_cdn.py`。查询接口是 `POST https://fusion.qiniuapi.com/v2/tune/refresh/list`，body 为 `{"requestId":"实际回执"}`，按 QBox 鉴权。接口详见 [缓存管理](https://developer.qiniu.com/fusion/13367/fusion-api-cache-management)。超过界限仍不一致就报告真实状态，不宣称 CDN 已完成，也不自动修改域名回源或缓存规则。
