"""Defense in depth for local/CI reports, including failed bootstrap attempts. Never prints secrets."""
import argparse
import html
import io
import json
import os
from pathlib import Path
import re
import urllib.parse
import zipfile

parser = argparse.ArgumentParser()
parser.add_argument('directories', nargs='+', type=Path)
args = parser.parse_args()
patterns = [
    re.compile(r'eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+'),
    re.compile(r'(?<![A-Za-z0-9+/])[A-Za-z0-9+/]{43}=(?![A-Za-z0-9+/=])'),
]
secrets = set()
for name, value in os.environ.items():
    if value and (name.endswith('_PASSWORD') or name.endswith('_USERNAME')):
        secrets.update([value, html.escape(value, quote=True), urllib.parse.quote(value, safe=''), json.dumps(value)[1:-1]])


def clean(data):
    try:
        text = data.decode('utf-8')
    except UnicodeDecodeError:
        return data
    for value in sorted(secrets, key=len, reverse=True):
        text = text.replace(value, '[REDACTED]')
    for pattern in patterns:
        text = pattern.sub('[REDACTED]', text)
    return text.encode('utf-8')


changed = 0
for directory in args.directories:
    for path in directory.rglob('*'):
        if not path.is_file() or path.suffix.lower() not in {'.trx', '.html', '.json', '.ndjson', '.log', '.txt', '.trace', '.network', '.zip'}:
            continue
        if path.suffix == '.zip' and 'TestResults' not in path.parts:
            continue
        before = path.read_bytes()
        if path.suffix == '.zip':
            output = io.BytesIO()
            with zipfile.ZipFile(io.BytesIO(before)) as original, zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED) as sanitized:
                for entry in original.infolist():
                    raw = original.read(entry.filename)
                    sanitized.writestr(entry, raw if entry.filename.endswith(('.png', '.jpeg', '.jpg')) else clean(raw))
            after = output.getvalue()
        else:
            after = clean(before)
        if before != after:
            path.write_bytes(after)
            changed += 1
print(f'Artifacts sanitized; changed files: {changed}. No secret values printed.')
