#!/usr/bin/env python3
"""Generate a deterministic call-site inventory; dynamic semantics require human review."""
from pathlib import Path
import argparse, hashlib, re, os
CALL=re.compile(r'\b(ReportEvent|ReportUiShown|ReportUiClosed|ReportLevelStart|ReportLevelResult|ReportLevelRevive|ReportTutorialStart|ReportTutorialStep|ReportTutorialFinish|ShowAd|Track)\s*\(\s*(?:"([^"\r\n]*)")?')
OUTPUT_DIRS={'library','temp','obj','build','builds','dist','publish','published','output','.git','node_modules'}
def source_files(root):
    for base,dirs,files in os.walk(root/'Assets', followlinks=False):
        dirs[:]=sorted(d for d in dirs if d.lower() not in OUTPUT_DIRS and not (Path(base)/d).is_symlink())
        for name in sorted(files):
            p=Path(base)/name
            if p.suffix=='.cs' and not p.is_symlink(): yield p

def render(root):
    rows=[]; digest=hashlib.sha256()
    for p in sorted(source_files(root)):
        raw=p.read_bytes();rel=p.relative_to(root).as_posix();text=raw.decode('utf-8-sig',errors='replace')
        matches=list(CALL.finditer(text))
        if not matches:continue
        digest.update(rel.encode());digest.update(raw)
        for match in matches:
            line=text.count('\n',0,match.start())+1
            value=match.group(2)
            if match.group(1)=='ShowAd':value='广告 ID 参数省略；人工核对中文奖励名、placement 与奖励回调'
            rows.append((rel,line,match.group(1),value or '动态/类型声明：人工核对'))
    text=['# SDK 事件调用点清单','',f'源码指纹：`{digest.hexdigest()}`','', '本表为候选调用点，不把方法声明、注释或动态名称当作业务接线已完成。需沿实际入口核对字段与触发语义。','', '| 文件 | 行 | 调用 | 静态名称或待核对项 |','| --- | --- | --- | --- |']
    for rel,line,method,name in rows:
        name=name.replace("|", "\\|")
        text.append(f"| {rel} | {line} | {method} | {name} |")
    text+=['','人工补充验收：真实页面、业务关卡映射、实时剩余道具、当前关卡、placement、tutorial_start 与 step=1 同时、中文 step_name、跨模块/重启全局连续 step、第一引导模块完成、广告计数和通道后台字段。','']
    return '\n'.join(text)
def main():
    p=argparse.ArgumentParser();p.add_argument('project',type=Path);p.add_argument('--check',action='store_true');a=p.parse_args();root=a.project.resolve()
    if not (root/'Assets').is_dir():p.error('Assets missing')
    dst=root/'Docs/track-events.md';expected=render(root)
    if a.check:
        ok=dst.exists() and dst.read_text(encoding='utf-8')==expected
        print('PASS: event inventory current' if ok else 'FAIL: event inventory missing or stale');return 0 if ok else 1
    if dst.exists() and '源码指纹：' not in dst.read_text(encoding='utf-8-sig'):p.error('existing manually maintained document: merge it explicitly before generating')
    dst.parent.mkdir(parents=True,exist_ok=True);dst.write_text(expected,encoding='utf-8');print(str(dst));return 0
if __name__=='__main__':raise SystemExit(main())
