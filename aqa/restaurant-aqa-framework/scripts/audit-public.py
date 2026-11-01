"""Conservative read-only audit. Reports locations/rules, never matched secret values."""
import argparse
import json
from pathlib import Path
import re
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('--history', action='store_true')
parser.add_argument('--check', action='store_true')
parser.add_argument('--output', type=Path)
args = parser.parse_args()
root = Path(subprocess.check_output(['git', 'rev-parse', '--show-toplevel'], text=True).strip())
rules = {
    'aws-access-key': re.compile(rb'\b(?:AKIA|ASIA)[A-Z0-9]{16}\b'),
    'jwt': re.compile(rb'eyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}'),
    'private-key': re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'),
    'github-token': re.compile(rb'\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{50,})\b'),
    'literal-guest-token': re.compile(rb'(?i)"sessionToken"\s*:\s*"[A-Za-z0-9+/]{40,}={0,2}"'),
}
generated = re.compile(r'(^|/)(?:_local_hw8_sources|bin|obj|TestResults|node_modules|\.terraform)/|\.dpapi\.xml$|\.feature\.cs$|(?:^|/)\.env(?:\.local)?$|\.tfstate(?:\.|$)')
findings = []
scanned = 0


def scan(label, data):
    global scanned
    if b'\0' in data[:8000]:
        return
    scanned += 1
    for rule, pattern in rules.items():
        for match in pattern.finditer(data):
            findings.append({'file': label, 'line': data[:match.start()].count(b'\n') + 1, 'rule': rule})


tracked = subprocess.check_output(['git', 'ls-files', '-z'], cwd=root).decode().split('\0')
for name in filter(None, tracked):
    if generated.search(name):
        findings.append({'file': name, 'rule': 'generated-or-local-file-tracked'})
    path = root / name
    if path.is_file() and path.stat().st_size < 2_000_000:
        scan(name, path.read_bytes())

blob_count = 0
if args.history:
    objects = subprocess.check_output(['git', 'rev-list', '--objects', '--all'], cwd=root).decode().splitlines()
    names = dict(line.split(' ', 1) if ' ' in line else (line, '(tree/commit)') for line in objects)
    metadata = subprocess.run(['git', 'cat-file', '--batch-check'], input='\n'.join(names) + '\n', capture_output=True, text=True, check=True, cwd=root).stdout
    ids = [line.split()[0] for line in metadata.splitlines() if line.split()[1] == 'blob' and int(line.split()[2]) < 2_000_000]
    raw = subprocess.run(['git', 'cat-file', '--batch'], input=('\n'.join(ids) + '\n').encode(), capture_output=True, check=True, cwd=root).stdout
    offset = 0
    while offset < len(raw):
        end = raw.index(b'\n', offset)
        oid, kind, size = raw[offset:end].decode().split()
        start = end + 1
        data = raw[start:start + int(size)]
        scan(f'{names[oid]}@{oid[:12]}', data)
        blob_count += 1
        offset = start + int(size) + 1

result = {'trackedFiles': len(list(filter(None, tracked))), 'textScans': scanned, 'historyBlobs': blob_count,
          'findings': findings, 'limitations': ['Heuristics, not proof of absence of secrets.', 'Binary images, archives, blobs >=2 MB, unreachable Git objects, remote refs not fetched, GitHub artifacts and logs require separate review.', 'Licensing and personal information need human review.']}
if args.output:
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
print(json.dumps(result, indent=2, ensure_ascii=False))
if args.check and findings:
    raise SystemExit(1)
