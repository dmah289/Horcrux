"""Tóm console Unity thành dòng thời gian gọn: dò luồng treo, đọc lỗi một phiên Play. Xem docs/Editor/Probe.md.

  console_digest.py [file.json] [--tail 300] [--level log|warn|error] [--since SEQ]
                    [--drop REGEX]... [--frames 2] [--width 160] [--gap 5] [--max 120]

Không truyền file: gọi `unity cmd console get` của Editor đang mở (UNITY_CLI, PROJECT_PATH như playrun.sh).
File: JSON của `console get` (MCP lưu khi kết quả quá cỡ, hay CLI `--result-only`).
"""
import argparse
import io
import json
import os
import re
import shutil
import subprocess
import sys
from collections import Counter
from datetime import datetime

LEVELS = ('log', 'warn', 'error')
# Frame của dự án: đường dẫn bắt đầu bằng Assets/; package, thư viện, engine là nhiễu khi dò chỗ chuỗi đứt.
PROJECT_FRAME = re.compile(r'^(?P<call>.+?) \(at (?P<path>Assets/[^:)]+):(?P<line>\d+)\)')


def default_cli():
    cli = os.environ.get('UNITY_CLI')
    if cli:
        return cli
    local = os.path.join(os.environ.get('LOCALAPPDATA', ''), 'Unity', 'bin', 'unity.exe')
    return local if os.path.isfile(local) else (shutil.which('unity') or 'unity')


def fetch(args):
    cmd = [default_cli(), 'cmd', '--project-path', os.environ.get('PROJECT_PATH', os.getcwd()), '--result-only',
           'console', 'get', '--tail', str(args.tail), '--level', args.level]
    if args.since is not None:
        cmd += ['--since', str(args.since)]
    out = subprocess.run(cmd, capture_output=True, text=True, encoding='utf8')
    if out.returncode != 0:
        sys.exit('console get lỗi (%d): %s' % (out.returncode, (out.stderr or out.stdout).strip()[:400]))
    return json.loads(out.stdout)


def load(args):
    if not args.file:
        return fetch(args)
    with io.open(args.file, encoding='utf8') as f:
        data = json.load(f)
    return data if isinstance(data, dict) else {'entries': data}


def parse_time(stamp):
    """ISO của Unity có 7 chữ số lẻ giây; datetime chỉ nhận 6."""
    if not stamp:
        return None
    stamp = re.sub(r'(\.\d{6})\d+', r'\1', stamp.rstrip('Z'))
    try:
        return datetime.fromisoformat(stamp)
    except ValueError:
        return None


def short_call(call):
    """Frame log ("Ns.Type`1<Arg>/<Inner>d__7:Method (args)") hay frame exception ("Ns.Type`1[T].Method ()") → "Type.Method"."""
    call = call.split(' (')[0]
    if ':' in call:
        owner, _, method = call.rpartition(':')   # tên method do compiler sinh (<Disable>b__4_0) giữ nguyên
        owner = strip_generics(owner)
    else:
        owner, _, method = strip_generics(call).rpartition('.')
    owner = owner.split('/')[0].rsplit('.', 1)[-1]
    return '%s.%s' % (owner, method) if owner else method


def strip_generics(name):
    """Bỏ tham số generic trước khi tách theo dấu chấm: chúng chứa namespace của kiểu khác."""
    while True:
        stripped = re.sub(r'<[^<>]*>|\[[^\[\]]*\]', '', name)
        if stripped == name:
            return re.sub(r'`\d+', '', name)
        name = stripped


def project_frames(stack, limit):
    frames = []
    for line in (stack or '').split('\n'):
        m = PROJECT_FRAME.match(line.strip())
        if not m:
            continue
        frames.append('%s @%s:%s' % (short_call(m.group('call')), m.group('path').rsplit('/', 1)[-1], m.group('line')))
        if len(frames) == limit:
            break
    return frames


def first_line(message, width):
    text = (message or '').strip().split('\n', 1)[0].strip()
    return text if len(text) <= width else text[:width - 1] + '…'


def digest(data, args):
    rank = LEVELS.index(args.level)
    drops = [re.compile(p) for p in args.drop]
    entries = []
    for e in data.get('entries', []):
        level = e.get('level', 'log')
        if level in LEVELS and LEVELS.index(level) < rank:
            continue
        if args.since is not None and e.get('seq', 0) <= args.since:
            continue
        text = first_line(e.get('message'), args.width)
        if any(d.search(e.get('message') or '') for d in drops):
            continue
        entries.append({
            'seq': e.get('seq'), 'time': parse_time(e.get('timestampUtc')), 'level': level, 'text': text,
            'frames': project_frames(e.get('stackTrace'), args.frames)})

    out = []
    counts = Counter(e['level'] for e in entries)
    seqs = [e['seq'] for e in entries if e['seq'] is not None]
    out.append('entries %d%s · error %d warn %d log %d · cursor %s' % (
        len(entries), ' (seq %d–%d)' % (min(seqs), max(seqs)) if seqs else '',
        counts['error'], counts['warn'], counts['log'], data.get('cursor', '-')))

    # Dòng liền nhau cùng nội dung gộp một dòng ×n; lặp cách quãng thì in lại, đếm ở bảng cuối.
    timeline = []
    previous = None
    for e in entries:
        key = (e['level'], e['text'], tuple(e['frames'][:1]))
        if previous and previous['key'] == key:
            previous['count'] += 1
            previous['last'] = e
            continue
        if previous and previous['last']['time'] and e['time']:
            gap = (e['time'] - previous['last']['time']).total_seconds()
            if gap >= args.gap:
                timeline.append(['  ··· im %ds ···' % gap])
        previous = {'key': key, 'first': e, 'last': e, 'count': 1}
        timeline.append(previous)

    lines = []
    for item in timeline:
        if isinstance(item, list):
            lines.append(item[0])
            continue
        e = item['first']
        when = e['time'].strftime('%H:%M:%S') if e['time'] else '--:--:--'
        repeat = ''
        if item['count'] > 1:
            until = item['last']['time'].strftime('%H:%M:%S') if item['last']['time'] else '?'
            repeat = '  ×%d (đến #%s %s)' % (item['count'], item['last']['seq'], until)
        lines.append('#%s %s %s %s%s' % (e['seq'], when, e['level'][0].upper(), e['text'], repeat))
        lines.extend('      ← ' + f for f in e['frames'])

    if len(lines) > args.max:
        out.append('… bỏ %d dòng đầu (--max %d)' % (len(lines) - args.max, args.max))
        lines = lines[-args.max:]
    out.extend(lines)

    repeated = Counter((e['level'], e['text']) for e in entries).most_common()
    repeated = [(k, n) for k, n in repeated if n > 2][:5]
    if repeated:
        out.append('LẶP')
        out.extend('  ×%d %s %s' % (n, level[0].upper(), text) for (level, text), n in repeated)

    if entries:
        last = entries[-1]
        tail = 'CUỐI #%s %s' % (last['seq'], last['text'])
        sampled = parse_time((data.get('groundTruth') or {}).get('sampledUtc'))
        if sampled and last['time']:
            tail += ' · im %ds tới lúc đọc' % (sampled - last['time']).total_seconds()
        out.append(tail)
    return '\n'.join(out)


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument('file', nargs='?', help='JSON của console get; bỏ trống thì gọi Unity CLI')
    p.add_argument('--tail', type=int, default=300, help='số entry lấy khi gọi CLI')
    p.add_argument('--level', choices=LEVELS, default='log', help='mức thấp nhất giữ lại')
    p.add_argument('--since', type=int, help='chỉ entry có seq lớn hơn')
    p.add_argument('--drop', action='append', default=[], help='regex trên message, khớp thì bỏ; lặp được')
    p.add_argument('--frames', type=int, default=2, help='số frame của dự án dưới mỗi dòng')
    p.add_argument('--width', type=int, default=160, help='cắt dòng message')
    p.add_argument('--gap', type=float, default=5, help='giây im giữa hai dòng thì in mốc')
    p.add_argument('--max', type=int, default=120, help='giữ chừng ấy dòng cuối của dòng thời gian')
    args = p.parse_args(argv)
    sys.stdout.reconfigure(encoding='utf8')
    print(digest(load(args), args))


if __name__ == '__main__':
    main()
