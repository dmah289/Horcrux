"""So trace của Horcrux FeelTraceRecorder. Xem Editor/FeelTrace/FeelTrace.md.

  feeltrace.py render <script.cs> <đường dẫn file ra, kiểu Windows> <file .cs đích>
  feeltrace.py sig <glob>...
  feeltrace.py diff <a.txt> <b.txt> [--tol 2e-4] [--limit 40]
"""
import argparse
import glob
import io
import sys

MARKER = '__TRACE_OUT__'


def render(script, out_path, dest):
    src = io.open(script, encoding='utf8').read()
    if src.count(MARKER) != 1:
        sys.exit('%s phải chứa %s đúng một lần' % (script, MARKER))
    io.open(dest, 'w', encoding='utf8').write(src.replace(MARKER, out_path))


def read_header(path):
    """Các dòng trước LOG: đọc từng dòng tới đó, không nạp cả trace nhiều MB."""
    lines = {}
    with io.open(path, encoding='utf8') as f:
        for line in f:
            line = line.rstrip('\n')
            if line == 'LOG':
                break
            name, _, value = line.partition(' ')
            lines[name] = value
    return lines


def load(path):
    """frame → {khoá entity → chuỗi token thô}. Khoá 'kind?' thành 'kind#n' theo thứ tự trong frame."""
    text = io.open(path, encoding='utf8').read()
    trace = text.split('\nTRACE\n', 1)[1]
    frames = {}
    for line in trace.split('\n'):
        if not line:
            continue
        parts = line.split(' |')
        order = {}
        entities = {}
        for part in parts[1:]:
            key, _, rest = part.partition(' ')
            if key.endswith('?'):
                kind = key[:-1]
                index = order.get(kind, 0)
                order[kind] = index + 1
                key = '%s#%d' % (kind, index)
            entities[key] = rest
        frames[int(parts[0][1:])] = entities
    return frames


def numbers(tokens):
    out = []
    for token in tokens:
        for piece in token.split(','):
            try:
                out.append(float(piece))
            except ValueError:
                out.append(piece)
    return out


def differs(x, y, tol):
    if isinstance(x, float) and isinstance(y, float):
        return abs(x - y) > tol
    return x != y


def diff(a, b, tol, limit):
    print('A', read_header(a).get('SIG', ''))
    print('B', read_header(b).get('SIG', ''))
    frames_a, frames_b = load(a), load(b)
    count = 0
    for frame in sorted(set(frames_a) | set(frames_b)):
        ea, eb = frames_a.get(frame, {}), frames_b.get(frame, {})
        for key in sorted(set(ea) | set(eb)):
            if key not in ea or key not in eb:
                count += 1
                if count <= limit:
                    print('F%d %s chỉ có ở %s' % (frame, key, 'A' if key in ea else 'B'))
                continue
            ra, rb = ea[key], eb[key]
            # Gần như mọi entity trùng từng ký tự: so chuỗi trước, chỉ tách số khi khác.
            if ra == rb:
                continue
            ta, tb = ra.split(), rb.split()
            va, vb = numbers(ta), numbers(tb)
            bad = [i for i, (x, y) in enumerate(zip(va, vb)) if differs(x, y, tol)]
            if len(va) != len(vb) or bad:
                count += 1
                if count <= limit:
                    print('F%d %s token %s\n  A %s\n  B %s' % (frame, key, bad[:6], ' '.join(ta), ' '.join(tb)))
    print('mismatches:', count)
    return count


def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest='command', required=True)
    p = sub.add_parser('render')
    p.add_argument('script')
    p.add_argument('out_path')
    p.add_argument('dest')
    p = sub.add_parser('sig')
    p.add_argument('patterns', nargs='+')
    p = sub.add_parser('diff')
    p.add_argument('a')
    p.add_argument('b')
    p.add_argument('--tol', type=float, default=2e-4)
    p.add_argument('--limit', type=int, default=40)
    args = parser.parse_args()
    if args.command == 'render':
        render(args.script, args.out_path, args.dest)
    elif args.command == 'sig':
        for pattern in args.patterns:
            for path in sorted(glob.glob(pattern)):
                print(path, '=>', read_header(path).get('SIG', ''))
    else:
        sys.exit(1 if diff(args.a, args.b, args.tol, args.limit) else 0)


if __name__ == '__main__':
    main()
