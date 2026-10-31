"""So trace của Horcrux FeelTraceRecorder. Xem Editor/FeelTrace/FeelTrace.md.

  feeltrace.py sig <glob>...
  feeltrace.py pairs <glob>...        # nhóm file cùng chữ ký; exit 0 khi có ít nhất một cặp so được
  feeltrace.py diff <a.txt> <b.txt> [--tol 2e-4] [--limit 40]
  feeltrace.py stat <trace.txt> <entity> [--cols 1.y,3] [--from F] [--to F] [--series N]
"""
import argparse
import glob
import io
import sys

# Dưới ngưỡng này hai số coi là bằng khi dò chỗ bắt đầu, dừng chuyển động: F5 ghi 5 chữ số thập phân.
STILL = 1e-5


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


def columns(tokens):
    """Nhãn cột theo token: token một số là '<i>', vector là '<i>.x', '<i>.y', '<i>.z'."""
    labels = []
    for i, token in enumerate(tokens):
        pieces = token.split(',')
        if len(pieces) == 1:
            labels.append(str(i))
        else:
            labels.extend('%d.%s' % (i, 'xyzw'[k]) for k in range(len(pieces)))
    return labels


def fmt(value):
    return ('%.5f' % value).rstrip('0').rstrip('.') if isinstance(value, float) else value


def describe(label, series):
    """Một dòng cho một cột: đầu→cuối, cực trị, khung chuyển động, vọt lố hoặc biên độ, bước lớn nhất, tổng."""
    frames = [f for f, _ in series]
    values = [v for _, v in series]
    if not all(isinstance(v, float) for v in values):
        changes = [(f, v) for (f, v), (_, prev) in zip(series[1:], series) if v != prev]
        head = ' '.join('F%d=%s' % c for c in changes[:6])
        return '%s %s, đổi %d lần %s' % (label, values[0], len(changes), head)
    first, last = values[0], values[-1]
    lo = min(range(len(values)), key=values.__getitem__)
    hi = max(range(len(values)), key=values.__getitem__)
    moving = [frames[i] for i in range(1, len(values)) if abs(values[i] - values[i - 1]) > STILL]
    if not moving:
        return None
    steps = [abs(values[i] - values[i - 1]) for i in range(1, len(values))]
    big = max(range(len(steps)), key=steps.__getitem__)
    travel = last - first
    if abs(travel) > STILL:
        beyond = max((v - last) * (1 if travel > 0 else -1) for v in values)
        shape = 'vọt lố %.1f%%' % (100 * beyond / abs(travel)) if beyond > STILL else 'không vọt lố'
    else:
        peak = max(range(len(values)), key=lambda i: abs(values[i] - first))
        shape = 'về chỗ cũ, lệch xa nhất %s ở F%d' % (fmt(values[peak] - first), frames[peak])
    return '%s %s→%s min %s@F%d max %s@F%d chạy F%d..F%d %s bước lớn nhất %s@F%d tổng %s' % (
        label, fmt(first), fmt(last), fmt(values[lo]), frames[lo], fmt(values[hi]), frames[hi],
        moving[0] - 1, moving[-1], shape, fmt(steps[big]), frames[big + 1], fmt(sum(values)))


def stat(path, entity, cols, start, end, series_step):
    frames = load(path)
    rows = [(f, frames[f][entity].split()) for f in sorted(frames)
            if start <= f <= end and entity in frames[f]]
    if not rows:
        sys.exit('%s không có ở F%d..F%d' % (entity, start, end))
    labels = columns(rows[0][1])
    wanted = [i for i, label in enumerate(labels) if not cols or label in cols]
    data = [(f, numbers(tokens)) for f, tokens in rows]
    print('%s có ở F%d..F%d (%d frame), %d cột' % (entity, rows[0][0], rows[-1][0], len(rows), len(labels)))
    still = []
    for i in wanted:
        line = describe(labels[i], [(f, values[i]) for f, values in data if len(values) == len(labels)])
        if line:
            print(' ', line)
        else:
            still.append('%s=%s' % (labels[i], fmt(data[0][1][i])))
    if still:
        print('  đứng yên:', ' '.join(still))
    if series_step:
        for f, values in data[::series_step]:
            print('  F%d %s' % (f, ' '.join('%s=%s' % (labels[i], fmt(values[i])) for i in wanted)))


def pairs(patterns):
    """Chia box ở boot là ngẫu nhiên: quay nhiều lần rồi chỉ so hai file cùng chữ ký. In từng nhóm, trả True khi có nhóm từ hai file."""
    groups = {}
    for pattern in patterns:
        for path in sorted(glob.glob(pattern)):
            groups.setdefault(read_header(path).get('SIG', ''), []).append(path)
    found = False
    for sig, paths in groups.items():
        if len(paths) < 2:
            continue
        found = True
        print(sig)
        for path in paths:
            print('  ', path)
    if not found:
        print('không có hai file nào cùng chữ ký; quay thêm')
    return found


def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest='command', required=True)
    p = sub.add_parser('sig')
    p.add_argument('patterns', nargs='+')
    p = sub.add_parser('pairs')
    p.add_argument('patterns', nargs='+')
    p = sub.add_parser('diff')
    p.add_argument('a')
    p.add_argument('b')
    p.add_argument('--tol', type=float, default=2e-4)
    p.add_argument('--limit', type=int, default=40)
    p = sub.add_parser('stat')
    p.add_argument('path')
    p.add_argument('entity')
    p.add_argument('--cols', default='')
    p.add_argument('--from', dest='start', type=int, default=0)
    p.add_argument('--to', dest='end', type=int, default=1 << 30)
    p.add_argument('--series', type=int, default=0)
    args = parser.parse_args()
    if args.command == 'stat':
        stat(args.path, args.entity, [c for c in args.cols.split(',') if c], args.start, args.end, args.series)
    elif args.command == 'pairs':
        sys.exit(0 if pairs(args.patterns) else 1)
    elif args.command == 'sig':
        for pattern in args.patterns:
            for path in sorted(glob.glob(pattern)):
                print(path, '=>', read_header(path).get('SIG', ''))
    else:
        sys.exit(1 if diff(args.a, args.b, args.tol, args.limit) else 0)


if __name__ == '__main__':
    main()
