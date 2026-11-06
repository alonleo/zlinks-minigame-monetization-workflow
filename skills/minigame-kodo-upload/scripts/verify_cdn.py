"""Verify public CDN bytes against an upload report, without any credentials."""
import argparse
import hashlib
import json
from pathlib import Path
from urllib.parse import urlsplit

import requests


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', required=True)
    args = parser.parse_args()
    source = Path(args.report)
    report = json.loads(source.read_text(encoding='utf-8'))
    domain = report.get('domain', '')
    origin = urlsplit(domain)
    if origin.scheme != 'https' or not origin.netloc or origin.username or origin.password:
        raise SystemExit('Report has no valid HTTPS domain.')
    checks = []
    for item in report['files']:
        check = {'key': item['key'], 'url': item.get('url'), 'ok': False}
        try:
            target = urlsplit(item['url'])
            if target.scheme != origin.scheme or target.netloc != origin.netloc or target.query or target.fragment:
                raise ValueError('URL does not match the expected public origin')
            # No auth, no cookies, no redirects, no automatic Content-Encoding decoding.
            with requests.get(item['url'], headers={'Accept-Encoding': 'identity'},
                              stream=True, allow_redirects=False, timeout=(10, 60)) as response:
                check['http_status'] = response.status_code
                check['content_encoding'] = response.headers.get('Content-Encoding', '')
                if response.status_code != 200:
                    check['error'] = 'CDN did not return HTTP 200'
                else:
                    digest, length = hashlib.sha256(), 0
                    for block in response.raw.stream(1024 * 1024, decode_content=False):
                        digest.update(block)
                        length += len(block)
                        if length > item['bytes']:
                            break
                    check.update(bytes=length, sha256=digest.hexdigest())
                    check['ok'] = length == item['bytes'] and digest.hexdigest() == item['sha256']
        except Exception as error:
            check['error'] = type(error).__name__
        checks.append(check)
    result = {'ok': bool(checks) and all(c['ok'] for c in checks), 'upload_report': str(source), 'cdn': checks}
    destination = source.with_suffix('.cdn.json')
    destination.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({**result, 'report': str(destination)}, ensure_ascii=False, indent=2))
    return 0 if result['ok'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
