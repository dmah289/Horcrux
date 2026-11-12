"""Đọc video demo (mp4) thành ảnh và số để dựng, chỉnh animation theo demo. Xem docs/Editor/VideoDemo.md.

  video_demo.py sheet   <video> --out <png> [--from s] [--to s] [--count N | --step s] [--cols C] [--cell W] [--roi R]
  video_demo.py frame   <video> --at t1,t2 --out-dir <dir> [--size WxH] [--roi R]
  video_demo.py motion  <video> [--from s] [--to s] [--roi R] [--bin s] [--threshold v]
  video_demo.py track   <video> --roi R --ref t [--bg t] [--from s] [--to s] [--csv <file>]
  video_demo.py compare <a> <b> [--roi R] [--out <png>] [--count N] [--max-lag s] [--tempo k]
  video_demo.py selftest

R là vùng x,y,w,h gốc trên trái: số nguyên là pixel của video, cả bốn số <= 1 là phần của khung (dùng chung cho hai video khác cỡ).
Cần opencv-python-headless và numpy.
"""
import argparse
import math
import os
import sys

try:
    import cv2
    import numpy as np
except ImportError:
    sys.exit('video_demo: thiếu thư viện, chạy: python -m pip install opencv-python-headless')

GAP = 4
GAP_COLOR = (40, 40, 40)
LABEL_H = 18
ANALYSIS_WIDTH = 160      # bề rộng ảnh xám dùng đo chuyển động: đủ thấy vật cỡ vài chục px, rẻ cho video dài
BARS = ' ▁▂▃▄▅▆▇█'
FIT_RMS_MAX = 0.15        # RMS tiến độ trên mức này: pha không phải một tween đơn (nhún, dao động, nhiều chặng)
LOOP_RATIO_MAX = 0.35     # lệch ở chu kỳ tốt nhất / lệch trung bình mọi lag; hạt ngẫu nhiên cho ~0.3–0.5 nên chỉ nhận dưới mức này
DUP_EPS = 0.05           # lệch trung bình dưới mức này: frame lặp lại frame trước (bản quay có ít hình mới hơn fps của file)


# ---------------------------------------------------------------- đọc video

class Video:
    def __init__(self, path):
        self.path = path
        cap = cv2.VideoCapture(path)
        if not cap.isOpened():
            sys.exit('video_demo: không mở được %s' % path)
        self.fps = cap.get(cv2.CAP_PROP_FPS) or 30.0
        self.w = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
        self.h = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
        self.n = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
        cap.release()

    @property
    def duration(self):
        return self.n / self.fps

    def header(self):
        return '%s: %dx%d, %.2f fps, %d frame, %.2f s' % (os.path.basename(self.path), self.w, self.h, self.fps, self.n, self.duration)

    def index(self, t):
        return max(0, min(self.n - 1, int(round(t * self.fps))))

    def range(self, t_from, t_to):
        a = self.index(t_from or 0.0)
        b = self.n - 1 if t_to is None else self.index(t_to)
        return a, b

    def read(self, wanted):
        """Đọc tuần tự, trả {index: frame BGR} cho các index trong wanted. Không seek: seek mp4 có thể rơi vào keyframe gần nhất."""
        wanted = set(wanted)
        out = {}
        if not wanted:
            return out
        last = max(wanted)
        cap = cv2.VideoCapture(self.path)
        i = 0
        while i <= last:
            ok, frame = cap.read()
            if not ok:
                break
            if i in wanted:
                out[i] = frame
            i += 1
        cap.release()
        return out

    def scan(self, a, b, fn):
        """Gọi fn(index, frame) cho mọi frame trong [a, b], tuần tự."""
        cap = cv2.VideoCapture(self.path)
        i = 0
        while i <= b:
            ok, frame = cap.read()
            if not ok:
                break
            if i >= a:
                fn(i, frame)
            i += 1
        cap.release()


def parse_roi(text, w, h):
    if not text:
        return 0, 0, w, h
    vals = [float(v) for v in text.split(',')]
    if len(vals) != 4:
        sys.exit('video_demo: --roi cần 4 số x,y,w,h')
    if all(v <= 1.0 for v in vals) and any('.' in v for v in text.split(',')):
        vals = [vals[0] * w, vals[1] * h, vals[2] * w, vals[3] * h]
    x, y, rw, rh = (int(round(v)) for v in vals)
    x, y = max(0, x), max(0, y)
    rw, rh = min(rw, w - x), min(rh, h - y)
    if rw <= 0 or rh <= 0:
        sys.exit('video_demo: --roi nằm ngoài khung %dx%d' % (w, h))
    return x, y, rw, rh


def crop(frame, roi):
    x, y, w, h = roi
    return frame[y:y + h, x:x + w]


def small_gray(frame, roi):
    g = cv2.cvtColor(crop(frame, roi), cv2.COLOR_BGR2GRAY)
    scale = ANALYSIS_WIDTH / g.shape[1]
    if scale < 1:
        g = cv2.resize(g, (ANALYSIS_WIDTH, max(1, int(round(g.shape[0] * scale)))), interpolation=cv2.INTER_AREA)
    return g.astype(np.float32)


# ---------------------------------------------------------------- ảnh lưới

def label_cell(img, text, width):
    h = int(round(img.shape[0] * width / img.shape[1]))
    cell = cv2.resize(img, (width, h), interpolation=cv2.INTER_AREA)
    strip = np.full((LABEL_H, width, 3), 20, np.uint8)
    cv2.putText(strip, text, (4, LABEL_H - 5), cv2.FONT_HERSHEY_SIMPLEX, 0.42, (255, 255, 255), 1, cv2.LINE_AA)
    return np.vstack([strip, cell])


def compose(cells, cols):
    """Ghép lưới: ô 0 trên trái, đi theo hàng, khe GAP px xám. Ô cùng bề rộng, cao lấy ô cao nhất mỗi hàng."""
    rows = []
    cw = cells[0].shape[1]
    for r in range(0, len(cells), cols):
        row_cells = cells[r:r + cols]
        rh = max(c.shape[0] for c in row_cells)
        parts = []
        for k in range(cols):
            c = row_cells[k] if k < len(row_cells) else np.zeros((rh, cw, 3), np.uint8)
            if c.shape[0] < rh:
                c = np.vstack([c, np.zeros((rh - c.shape[0], cw, 3), np.uint8)])
            parts.append(c)
            if k < cols - 1:
                parts.append(np.full((rh, GAP, 3), GAP_COLOR, np.uint8))
        rows.append(np.hstack(parts))
        if r + cols < len(cells):
            rows.append(np.full((GAP, rows[-1].shape[1], 3), GAP_COLOR, np.uint8))
    return np.vstack(rows)


def sample_indices(video, args):
    a, b = video.range(args.t_from, args.t_to)
    if args.step:
        step = max(1, int(round(args.step * video.fps)))
        return list(range(a, b + 1, step))
    count = max(1, args.count)
    if count == 1:
        return [a]
    return sorted({int(round(a + (b - a) * k / (count - 1))) for k in range(count)})


def write_png(path, img):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    if not cv2.imwrite(path, img):
        sys.exit('video_demo: không ghi được %s' % path)


def cmd_sheet(args):
    v = Video(args.video)
    roi = parse_roi(args.roi, v.w, v.h)
    idx = sample_indices(v, args)
    frames = v.read(idx)
    cells = [label_cell(crop(frames[i], roi), '%.2fs #%d' % (i / v.fps, i), args.cell) for i in idx if i in frames]
    sheet = compose(cells, args.cols)
    write_png(args.out, sheet)
    print(v.header())
    print('sheet: %s  %d ô, %dx%d, roi %s' % (args.out, len(cells), sheet.shape[1], sheet.shape[0], ','.join(map(str, roi))))
    print('mốc: ' + ' '.join('%.2f' % (i / v.fps) for i in idx if i in frames))


def cmd_frame(args):
    v = Video(args.video)
    roi = parse_roi(args.roi, v.w, v.h)
    times = [float(t) for t in args.at.split(',')]
    idx = [v.index(t) for t in times]
    frames = v.read(idx)
    size = tuple(int(s) for s in args.size.lower().split('x')) if args.size else None
    stem = os.path.splitext(os.path.basename(args.video))[0]
    print(v.header())
    for t, i in zip(times, idx):
        if i not in frames:
            print('%.3fs: ngoài video' % t)
            continue
        img = crop(frames[i], roi)
        if size:
            img = cv2.resize(img, size, interpolation=cv2.INTER_CUBIC if size[0] > img.shape[1] else cv2.INTER_AREA)
        path = os.path.join(args.out_dir, '%s_%.3fs.png' % (stem, i / v.fps))
        write_png(path, img)
        print('%.3fs #%d -> %s (%dx%d)' % (i / v.fps, i, path, img.shape[1], img.shape[0]))


# ---------------------------------------------------------------- chuyển động

def motion_series(v, roi, a, b):
    """e[k]: lệch tuyệt đối trung bình (0–255) giữa frame a+k và frame trước; e[0] = 0. Trả cả ảnh xám nhỏ để dò vòng lặp."""
    grays = []
    v.scan(a, b, lambda i, f: grays.append(small_gray(f, roi)))
    e = np.zeros(len(grays), np.float32)
    for k in range(1, len(grays)):
        e[k] = float(np.mean(np.abs(grays[k] - grays[k - 1])))
    return e, grays


def find_segments(e, thr, min_gap):
    moving = e > thr
    segs = []
    k = 0
    while k < len(e):
        if moving[k]:
            s = k
            while k < len(e) and moving[k]:
                k += 1
            if segs and s - segs[-1][1] <= min_gap:
                segs[-1] = (segs[-1][0], k)
            else:
                segs.append((s, k))
        else:
            k += 1
    return [(s, t) for s, t in segs if t - s >= 2]


def find_cuts(e):
    cuts = []
    for k in range(1, len(e)):
        around = np.concatenate([e[max(1, k - 3):k], e[k + 1:k + 4]])
        base = float(np.median(around)) if len(around) else 0.0
        if e[k] > max(25.0, 6.0 * base):
            cuts.append(k)
    return cuts


def loop_period(grays, s, t, fps):
    """Chu kỳ lặp trong đoạn [s, t): lag (từ 0.25 s) làm hai frame cách nhau giống nhau nhất ở nửa sau đoạn.
    Trả (giây, tỉ lệ) hoặc None; tỉ lệ = lệch ở lag đó / lệch lớn nhất ở các lag ngắn hơn. Lặp thật thì lệch lên rồi tụt
    về gần nhiễu (tỉ lệ nhỏ); hạt trôi thì lệch tăng đều theo lag, đáy nằm ở lag ngắn nhất và tỉ lệ ~1."""
    n = t - s
    if n < fps * 1.2:
        return None
    lo = max(2, int(fps * 0.25))
    hi = n // 2
    if hi <= lo:
        return None
    start = s + n // 2 - hi // 2
    d = [np.nan]                       # d[lag], lag 0 không dùng
    for lag in range(1, hi + 1):
        idx = range(start, t - lag)
        d.append(float(np.mean([np.mean(np.abs(grays[i] - grays[i + lag])) for i in idx[::2]])) if len(idx) >= 3 else np.inf)
    d = np.array(d)
    k = lo + int(np.argmin(d[lo:]))
    if not np.isfinite(d[k]):
        return None
    ratio = d[k] / float(np.max(d[1:k + 1]))
    lag = k
    if lo < k < len(d) - 1 and np.isfinite(d[k + 1]):
        den = d[k - 1] - 2 * d[k] + d[k + 1]
        if den > 0:
            lag += 0.5 * (d[k - 1] - d[k + 1]) / den
    return lag / fps, ratio


def motion_box(grays, s, t, roi, thr):
    """Khung bao vùng có chuyển động trong đoạn, theo pixel video."""
    acc = np.zeros_like(grays[0])
    for k in range(max(1, s), t):
        acc = np.maximum(acc, np.abs(grays[k] - grays[k - 1]))
    ys, xs = np.nonzero(acc > max(12.0, thr * 4))
    if len(xs) == 0:
        return None
    sx = roi[2] / grays[0].shape[1]
    sy = roi[3] / grays[0].shape[0]
    return (int(roi[0] + xs.min() * sx), int(roi[1] + ys.min() * sy),
            int(math.ceil((xs.max() - xs.min() + 1) * sx)), int(math.ceil((ys.max() - ys.min() + 1) * sy)))


def analyse_motion(v, roi, a, b, threshold, bin_s):
    e, grays = motion_series(v, roi, a, b)
    floor = float(np.percentile(e[1:], 10)) if len(e) > 1 else 0.0
    thr = threshold if threshold is not None else max(3.0 * floor, floor + 0.5)
    segs = find_segments(e, thr, max(1, int(round(0.1 * v.fps))))
    return dict(e=e, grays=grays, floor=floor, thr=thr, segs=segs, cuts=find_cuts(e), a=a, bin=bin_s)


def sparkline(m, fps):
    e, a = m['e'], m['a']
    per = max(1, int(round(m['bin'] * fps)))
    vals = [float(e[k:k + per].max()) for k in range(0, len(e), per)]
    top = max(vals) if vals and max(vals) > 0 else 1.0
    chars = ''.join(BARS[0] if x <= m['thr'] else BARS[max(1, int(math.ceil(x / top * 8)))] for x in vals)
    lines = []
    width = 50
    for k in range(0, len(chars), width):
        lines.append('  %6.2fs |%s|' % ((a + k * per) / fps, chars[k:k + width]))
    return lines, top


def print_motion(v, m, roi):
    fps = v.fps
    print('nhiễu %.2f · ngưỡng %.2f · đỉnh %.2f (lệch trung bình 0–255 giữa hai frame liền)' % (m['floor'], m['thr'], float(m['e'].max())))
    lines, top = sparkline(m, fps)
    print('độ động mỗi %.2fs (trống = dưới ngưỡng, █ = %.1f):' % (m['bin'], top))
    for line in lines:
        print(line)
    for k in m['cuts']:
        print('cắt cảnh: %.2fs (#%d)' % ((m['a'] + k) / fps, m['a'] + k))
    if not m['segs']:
        print('đoạn động: không có')
        return
    print('đoạn động:')
    for s, t in m['segs']:
        seg = m['e'][s:t]
        peak = s + int(np.argmax(seg))
        dup = float(np.mean(seg < DUP_EPS))
        line = '  %6.2f–%6.2fs  dài %5.2fs  đỉnh %5.2fs (%.1f)  tb %.1f' % (
            (m['a'] + s) / fps, (m['a'] + t) / fps, (t - s) / fps, (m['a'] + peak) / fps, float(seg.max()), float(seg.mean()))
        if dup > 0.05:
            line += '  trùng %.0f%% → ~%.0f hình/s' % (100 * dup, fps * (1 - dup))
        box = motion_box(m['grays'], s, t, roi, m['thr'])
        if box:
            line += '  vùng %d,%d,%d,%d' % box
        loop = loop_period(m['grays'], s, t, fps)
        if loop and loop[1] < LOOP_RATIO_MAX:
            line += '  lặp ~%.2fs (giống %.0f%%)' % (loop[0], 100 * (1 - loop[1]))
        print(line)


def cmd_motion(args):
    v = Video(args.video)
    roi = parse_roi(args.roi, v.w, v.h)
    a, b = v.range(args.t_from, args.t_to)
    print(v.header())
    print('roi %s · %.2f–%.2fs' % (','.join(map(str, roi)), a / v.fps, b / v.fps))
    print_motion(v, analyse_motion(v, roi, a, b, args.threshold, args.bin), roi)


# ---------------------------------------------------------------- easing của PrimeTween

BACK = 1.70158
ELASTIC_PERIOD = 0.3


def _out_bounce(x):
    n1, d1 = 7.5625, 2.75
    if x < 1 / d1:
        return n1 * x * x
    if x < 2 / d1:
        x -= 1.5 / d1
        return n1 * x * x + 0.75
    if x < 2.5 / d1:
        x -= 2.25 / d1
        return n1 * x * x + 0.9375
    x -= 2.625 / d1
    return n1 * x * x + 0.984375


def _out_elastic(t):
    if t > 0.9999:
        return 1.0
    return 2 ** (-10 * t) * math.sin((t - ELASTIC_PERIOD / 4) * 2 * math.pi / ELASTIC_PERIOD) + 1


def _in_out(f_in):
    return lambda t: 0.5 * f_in(2 * t) if t < 0.5 else 1 - 0.5 * f_in(2 - 2 * t)


def _back_in(t, s=BACK):
    return t * t * ((s + 1) * t - s)


# Bản Python của PrimeTween StandardEasing.Evaluate; đối chiếu bằng test_eases() (giá trị mốc) khi PrimeTween đổi công thức.
EASES = {
    'Linear': lambda t: t,
    'InSine': lambda t: 1 - math.cos(t * math.pi / 2),
    'OutSine': lambda t: math.sin(t * math.pi / 2),
    'InOutSine': lambda t: -0.5 * (math.cos(math.pi * t) - 1),
    'InQuad': lambda t: t ** 2,
    'OutQuad': lambda t: 1 - (1 - t) ** 2,
    'InOutQuad': _in_out(lambda t: t ** 2),
    'InCubic': lambda t: t ** 3,
    'OutCubic': lambda t: 1 - (1 - t) ** 3,
    'InOutCubic': _in_out(lambda t: t ** 3),
    'InQuart': lambda t: t ** 4,
    'OutQuart': lambda t: 1 - (1 - t) ** 4,
    'InOutQuart': _in_out(lambda t: t ** 4),
    'InQuint': lambda t: t ** 5,
    'OutQuint': lambda t: 1 - (1 - t) ** 5,
    'InOutQuint': _in_out(lambda t: t ** 5),
    'InExpo': lambda t: 0.0 if t == 0 else 2 ** (10 * (t - 1)),
    'OutExpo': lambda t: 1.0 if t == 1 else 1 - 2 ** (-10 * t),
    'InOutExpo': lambda t: 0.0 if t == 0 else 1.0 if t == 1 else (0.5 * 2 ** (10 * (2 * t - 1)) if t < 0.5 else 0.5 * (2 - 2 ** (-10 * (2 * t - 1)))),
    'InCirc': lambda t: 1 - math.sqrt(max(0.0, 1 - t * t)),
    'OutCirc': lambda t: math.sqrt(max(0.0, 1 - (t - 1) ** 2)),
    'InOutCirc': _in_out(lambda t: 1 - math.sqrt(max(0.0, 1 - t * t))),
    'InBack': _back_in,
    'OutBack': lambda t: 1 - _back_in(1 - t),
    'InOutBack': _in_out(lambda t: _back_in(t, BACK * 1.525)),
    'InElastic': lambda t: 1 - _out_elastic(1 - t),
    'OutElastic': _out_elastic,
    'InOutElastic': lambda t: (1 - _out_elastic(1 - 2 * t)) * 0.5 if t < 0.5 else 0.5 + _out_elastic((t - 0.5) * 2) * 0.5,
    'InBounce': lambda t: 1 - _out_bounce(1 - t),
    'OutBounce': _out_bounce,
    'InOutBounce': lambda t: (1 - _out_bounce(1 - 2 * t)) / 2 if t < 0.5 else (1 + _out_bounce(2 * t - 1)) / 2,
}


def back_overshoot(s):
    """Đỉnh vượt của OutBack với hằng s: 1 + 4s³ / (27 (s+1)²), đạt tại t = 1 − 2s / (3(s+1))."""
    return 4 * s ** 3 / (27 * (s + 1) ** 2)


def overshoot_strength(o):
    """Đảo back_overshoot bằng chia đôi: s cho đỉnh vượt o (phần của quãng). Trả k của Easing.Overshoot(k) = s / 1.70158."""
    lo, hi = 0.0, 20.0
    for _ in range(60):
        mid = (lo + hi) / 2
        if back_overshoot(mid) < o:
            lo = mid
        else:
            hi = mid
    return (lo + hi) / 2 / BACK


def fit_ease(t, p, t0_range, t1_range):
    """Tìm ease và cặp (t0, t1) cho RMS nhỏ nhất giữa p (tiến độ chuẩn hoá 0→1) và ease((t − t0)/(t1 − t0))."""
    results = []
    t0s = np.linspace(t0_range[0], t0_range[1], 5)
    t1s = np.linspace(t1_range[0], t1_range[1], 11)
    for name, f in EASES.items():
        best = None
        for t0 in t0s:
            for t1 in t1s:
                if t1 - t0 <= 1e-6:
                    continue
                u = np.clip((t - t0) / (t1 - t0), 0.0, 1.0)
                pred = np.array([f(x) for x in u])
                rms = float(np.sqrt(np.mean((pred - p) ** 2)))
                if best is None or rms < best[0]:
                    best = (rms, t0, t1)
        results.append((best[0], name, best[1], best[2]))
    results.sort()
    return results


def test_eases():
    """Mốc biên và giá trị giữa so với PrimeTween (tính tay từ StandardEasing.cs)."""
    for name, f in EASES.items():
        assert abs(f(0.0)) < 1e-6 and abs(f(1.0) - 1) < 1e-6, name
    checks = {'OutQuad': (0.5, 0.75), 'InOutCubic': (0.25, 0.0625), 'OutBack': (0.5, 1.0876975), 'InBack': (0.5, -0.0876975),
              'OutBounce': (0.5, 0.765625), 'InOutSine': (0.5, 0.5), 'OutCirc': (0.5, math.sqrt(0.75))}
    for name, (x, want) in checks.items():
        assert abs(EASES[name](x) - want) < 1e-5, (name, EASES[name](x), want)
    peak = max(EASES['OutBack'](k / 10000) for k in range(10001)) - 1
    assert abs(peak - back_overshoot(BACK)) < 1e-6, peak
    assert abs(overshoot_strength(back_overshoot(BACK)) - 1) < 1e-6
    print('eases: %d hàm khớp mốc, OutBack vượt %.4f' % (len(EASES), back_overshoot(BACK)))


# ---------------------------------------------------------------- bám một vùng

def match_scales(lo, hi, step):
    out = []
    s = lo
    while s <= hi:
        out.append(s)
        s *= step
    return out


def best_match(frame, tmpl, scales, window=None):
    """Khớp tmpl ở mọi scale trong scales. Trả (score, cx, cy, scale) theo toạ độ frame, hoặc None."""
    ox, oy = 0, 0
    img = frame
    if window:
        x, y, w, h = window
        ox, oy = x, y
        img = frame[y:y + h, x:x + w]
    best = None
    for s in scales:
        tw, th = int(round(tmpl.shape[1] * s)), int(round(tmpl.shape[0] * s))
        if tw < 6 or th < 6 or tw > img.shape[1] or th > img.shape[0]:
            continue
        t = cv2.resize(tmpl, (tw, th), interpolation=cv2.INTER_AREA if s < 1 else cv2.INTER_LINEAR)
        if float(t.std()) < 1e-3:
            continue
        r = cv2.matchTemplate(img, t, cv2.TM_CCOEFF_NORMED)
        _, score, _, loc = cv2.minMaxLoc(r)
        if best is None or score > best[0]:
            best = (float(score), ox + loc[0] + tw / 2.0, oy + loc[1] + th / 2.0, s)
    return best


def clamp_window(cx, cy, w, h, fw, fh):
    x0, y0 = max(0, int(cx - w / 2)), max(0, int(cy - h / 2))
    x1, y1 = min(fw, int(cx + w / 2)), min(fh, int(cy + h / 2))
    return x0, y0, x1 - x0, y1 - y0


def alpha_at(gray, bg, tmpl, cx, cy, s):
    """Độ đậm a trong F = a·T + (1 − a)·B, nghiệm bình phương tối thiểu: a = Σ(F−B)(T−B) / Σ(T−B)². None khi T gần trùng nền."""
    tw, th = int(round(tmpl.shape[1] * s)), int(round(tmpl.shape[0] * s))
    x, y = int(round(cx - tw / 2.0)), int(round(cy - th / 2.0))
    if x < 0 or y < 0 or x + tw > gray.shape[1] or y + th > gray.shape[0] or tw < 2 or th < 2:
        return None
    t = cv2.resize(tmpl, (tw, th)).astype(np.float32)
    f = gray[y:y + th, x:x + tw].astype(np.float32)
    b = bg[y:y + th, x:x + tw].astype(np.float32)
    d = t - b
    den = float(np.sum(d * d))
    if den < 25.0 * d.size:          # chênh trung bình dưới 5 mức xám: không đủ để tách vật khỏi nền
        return None
    return float(np.sum((f - b) * d)) / den


def phases(t, v, eps, min_still):
    """Các pha chuyển động của một kênh: đoạn liên tiếp mà giá trị đổi quá eps so với frame trước, gộp khe ngắn hơn min_still frame."""
    moving = np.zeros(len(v), bool)
    moving[1:] = np.abs(np.diff(v)) > eps
    out = []
    k = 0
    while k < len(v):
        if moving[k]:
            s = k
            while k < len(v) and moving[k]:
                k += 1
            if out and s - out[-1][1] < min_still:
                out[-1] = (out[-1][0], k)
            else:
                out.append((s, k))
        else:
            k += 1
    # Pha mà cả biên độ chỉ cỡ vài eps là nhiễu khớp ảnh (±1 px, ±0.5% scale), không phải chuyển động.
    return [(s, e) for s, e in out if e - s >= 2 and float(np.ptp(v[max(0, s - 1):e])) >= 4 * eps]


def channel_phases(t, v, eps, fps):
    """Mỗi pha một dict: kind 'ease' (một tween đơn, kèm fits xếp theo RMS) hay 'osc' (nhún, dao động, nhiều chặng)."""
    out = []
    ps = phases(t, v, eps, max(2, int(round(0.1 * fps))))
    settle_n = max(3, int(round(0.3 * fps)))
    for k, (s, e) in enumerate(ps):
        i0 = max(0, s - 1)                  # frame tĩnh cuối trước pha
        i1 = min(len(v) - 1, e - 1)         # frame cuối đổi quá eps
        # Đuôi ease-out đổi ít hơn eps mỗi frame nên pha dò được dừng sớm: lấy giá trị đích ở đoạn đứng sau pha
        # (tới pha kế hay 0.3 s), và để t1 rơi đâu đó trong đoạn ấy.
        nxt = ps[k + 1][0] - 1 if k + 1 < len(ps) else len(v)
        i2 = min(len(v) - 1, nxt - 1, i1 + settle_n)
        v0 = float(v[i0])
        v1 = float(np.median(v[max(i1, i2 - 2):i2 + 1]))
        seg_t, seg_v = t[i0:i2 + 1], v[i0:i2 + 1]
        lo, hi = float(v[i0:i1 + 1].min()), float(v[i0:i1 + 1].max())
        ph = dict(kind='osc', t0=float(t[i0]), t1=float(t[i1]), v0=v0, v1=v1, lo=lo, hi=hi, fits=[], over=0.0, rms=None, period=None)
        span = v1 - v0
        if abs(span) >= 0.35 * max(hi - lo, 1e-9):
            p = (seg_v - v0) / span
            dt = 1.0 / fps
            fits = fit_ease(seg_t, p, (t[i0], t[i0] + dt), (t[i1] - dt, t[i2]))
            ph['rms'] = fits[0][0]
            if fits[0][0] <= FIT_RMS_MAX:
                ph.update(kind='ease', fits=fits, t0=fits[0][2], t1=fits[0][3], over=max(0.0, float(p.max()) - 1.0))
        out.append(ph)
    return group_swings(out, t, v)


def turning_points(t, v, g):
    """Mốc đỉnh giữa hai nửa nhịp liền, trong khoảng từ tâm nửa trước tới tâm nửa sau. Đỉnh phẳng nhiều frame và giá trị đo
    theo bước rời rạc làm frame cực trị nhảy ±1 frame: lấy tâm dải frame nằm trong 10% biên độ quanh cực trị."""
    tps = []
    for p, q in zip(g, g[1:]):
        lo_t, hi_t = (p['t0'] + p['t1']) / 2, (q['t0'] + q['t1']) / 2
        idx = np.nonzero((t >= lo_t) & (t <= hi_t))[0]
        if len(idx) == 0:
            continue
        seg = v[idx]
        ext = seg.max() if p['v1'] > p['v0'] else seg.min()
        near = idx[np.abs(seg - ext) <= 0.1 * abs(p['v1'] - p['v0'])]
        tps.append(float((t[near[0]] + t[near[-1]]) / 2))
    return tps


def group_swings(phs, t, v):
    """Ba pha ease trở lên liền nhau, đổi chiều luân phiên, biên độ gần nhau: một dao động (idle nhún, Yoyo), không phải n tween.
    Pha dò được bỏ phần gần đứng ở đỉnh nên ease của từng nửa nhịp fit lại từ đỉnh tới đỉnh, chu kỳ đo giữa các đỉnh."""
    out, k = [], 0
    while k < len(phs):
        j = k
        while (j + 1 < len(phs) and phs[j]['kind'] == 'ease' and phs[j + 1]['kind'] == 'ease'
               and (phs[j]['v1'] - phs[j]['v0']) * (phs[j + 1]['v1'] - phs[j + 1]['v0']) < 0
               and phs[j + 1]['t0'] - phs[j]['t1'] < 0.3
               and abs(abs(phs[j + 1]['v1'] - phs[j + 1]['v0']) / abs(phs[j]['v1'] - phs[j]['v0']) - 1) < 0.5):
            j += 1
        if j - k < 2:
            out.append(phs[k])
            k += 1
            continue
        g = phs[k:j + 1]
        tps = turning_points(t, v, g)
        names = []
        for a, b in zip(tps, tps[1:]):
            idx = np.nonzero((t >= a) & (t <= b))[0]
            if len(idx) < 3 or v[idx[-1]] == v[idx[0]]:
                continue
            pr = (v[idx] - v[idx[0]]) / (v[idx[-1]] - v[idx[0]])
            names.append(fit_ease(t[idx], pr, (a, a), (b, b))[0][1])
        period = 2 * (tps[-1] - tps[0]) / (len(tps) - 1) if len(tps) >= 2 else None
        out.append(dict(kind='osc', t0=g[0]['t0'], t1=g[-1]['t1'], v0=g[0]['v0'], v1=g[-1]['v1'],
                        lo=min(min(p['v0'], p['v1'], p['lo']) for p in g), hi=max(max(p['v0'], p['v1'], p['hi']) for p in g),
                        fits=[], over=0.0, rms=None, swings=len(g), period=period,
                        ease=max(set(names), key=names.count) if names else None))
        k = j + 1
    return out


def format_channel(name, t, v, phs, fmt):
    if not phs:
        return ['  %-6s đứng yên ở %s' % (name, fmt % float(np.median(v)))]
    lines = []
    for ph in phs:
        head = '  %-6s %.2f–%.2fs  %s → %s' % (name, ph['t0'], ph['t1'], fmt % ph['v0'], fmt % ph['v1'])
        if ph['kind'] == 'osc':
            if ph.get('period'):
                why = ' (%d nửa nhịp, mỗi nửa %s%.2fs)' % (ph['swings'], (ph['ease'] + ' ') if ph['ease'] else '', ph['period'] / 2)
                lines.append(head + '  dao động %s…%s chu kỳ %.2fs%s' % (fmt % ph['lo'], fmt % ph['hi'], ph['period'], why))
            else:
                why = '' if ph['rms'] is None else ' (không ease nào khớp, RMS %.2f)' % ph['rms']
                lines.append(head + '  dao động %s…%s%s' % (fmt % ph['lo'], fmt % ph['hi'], why))
            continue
        head += '  dài %.2fs' % (ph['t1'] - ph['t0'])
        if ph['over'] > 0.02:
            head += '  vượt %.0f%%' % (100 * ph['over'])
        lines.append(head)
        hint = ''
        if ph['over'] > 0.02 and ph['fits'][0][1] == 'OutBack':
            hint = '  → Easing.Overshoot(%.2f)' % overshoot_strength(ph['over'])
        lines.append('         ease gần nhất (RMS tiến độ): %s%s' % (', '.join('%s %.3f' % (n, r) for r, n, _, _ in ph['fits'][:3]), hint))
    return lines


def track_rows(v, roi, a, b, ref_i, bg_i, min_scale, max_scale, min_score):
    """Mỗi frame trong [a, b] một hàng (index, điểm khớp, dx, dy, scale, alpha). dx, dy là tâm vật trừ tâm roi (px video)."""
    extra = v.read([ref_i] + ([bg_i] if bg_i is not None else []))
    to_gray = lambda f: cv2.cvtColor(f, cv2.COLOR_BGR2GRAY)
    tmpl = crop(to_gray(extra[ref_i]), roi)
    tmpl_half = cv2.resize(tmpl, (max(1, tmpl.shape[1] // 2), max(1, tmpl.shape[0] // 2)), interpolation=cv2.INTER_AREA)
    bg = to_gray(extra[bg_i]) if bg_i is not None else None
    full = match_scales(min_scale, max_scale, 1.04)
    rows = []
    state = {'prev': None}
    rcx, rcy = roi[0] + roi[2] / 2.0, roi[1] + roi[3] / 2.0

    def step(i, frame):
        g = to_gray(frame)
        prev = state['prev']
        m = None
        # Frame trước đã khớp: chỉ dò quanh chỗ cũ, scale ±15% — rẻ, và không nhảy sang vật giống mẫu ở chỗ khác.
        if prev and prev[0] >= min_score:
            s0 = prev[3]
            win = clamp_window(prev[1], prev[2], roi[2] * s0 * 3 + 40, roi[3] * s0 * 3 + 40, g.shape[1], g.shape[0])
            m = best_match(g, tmpl, match_scales(s0 / 1.15, s0 * 1.15, 1.01), win)
        # Mất dấu: quét cả khung ở nửa độ phân giải mọi scale, rồi tinh chỉnh quanh chỗ tốt nhất ở độ phân giải gốc.
        if m is None or m[0] < min_score:
            half = cv2.resize(g, (g.shape[1] // 2, g.shape[0] // 2), interpolation=cv2.INTER_AREA)
            coarse = best_match(half, tmpl_half, full)
            if coarse:
                s0 = coarse[3]
                win = clamp_window(coarse[1] * 2, coarse[2] * 2, roi[2] * s0 * 1.5 + 24, roi[3] * s0 * 1.5 + 24, g.shape[1], g.shape[0])
                fine = best_match(g, tmpl, match_scales(s0 / 1.06, s0 * 1.06, 1.01), win)
                if fine and (m is None or fine[0] > m[0]):
                    m = fine
        state['prev'] = m
        if m is None:
            rows.append((i, 0.0, np.nan, np.nan, np.nan, np.nan))
            return
        al = alpha_at(g, bg, tmpl, m[1], m[2], m[3]) if bg is not None else np.nan
        rows.append((i, m[0], m[1] - rcx, m[2] - rcy, m[3], np.nan if al is None else al))

    v.scan(a, b, step)
    return rows


def seen_runs(rows, min_score):
    """Các đoạn frame thấy vật liền mạch (khe tối đa 2 frame). Qua chỗ mất dấu giá trị nhảy giữa hai lần khớp, không phải chuyển động."""
    runs = []
    for r in rows:
        if r[1] < min_score:
            continue
        if runs and r[0] - runs[-1][-1][0] <= 2:
            runs[-1].append(r)
        else:
            runs.append([r])
    return runs


def run_channels(run, fps, eps_px):
    """{kênh: (t, giá trị, pha)} của một đoạn thấy; alpha chỉ có khi đo được."""
    t = np.array([r[0] / fps for r in run])
    out = {}
    for name, col, eps in (('dx', 2, eps_px), ('dy', 3, eps_px), ('scale', 4, 0.008), ('alpha', 5, 0.03)):
        vals = np.array([r[col] for r in run])
        ok = ~np.isnan(vals)
        if not ok.any():
            continue
        out[name] = (t[ok], vals[ok], channel_phases(t[ok], vals[ok], eps, fps))
    return out


def cmd_track(args):
    v = Video(args.video)
    roi = parse_roi(args.roi, v.w, v.h)
    a, b = v.range(args.t_from, args.t_to)
    ref_i = v.index(args.ref)
    bg_i = v.index(args.bg) if args.bg is not None else None
    rows = track_rows(v, roi, a, b, ref_i, bg_i, args.min_scale, args.max_scale, args.min_score)
    print(v.header())
    print('roi %s · mẫu ở %.2fs%s · %.2f–%.2fs · thấy khi điểm khớp >= %.2f' % (
        ','.join(map(str, roi)), ref_i / v.fps, (' · nền ở %.2fs' % (bg_i / v.fps)) if bg_i is not None else '',
        a / v.fps, b / v.fps, args.min_score))
    if args.csv:
        os.makedirs(os.path.dirname(os.path.abspath(args.csv)), exist_ok=True)
        with open(args.csv, 'w', encoding='utf-8') as fh:
            fh.write('t,frame,score,dx,dy,scale,alpha\n')
            for i, sc, dx, dy, s, al in rows:
                fh.write('%.4f,%d,%.3f,%.2f,%.2f,%.4f,%.3f\n' % (i / v.fps, i, sc, dx, dy, s, al))
        print('csv: %s (%d dòng)' % (args.csv, len(rows)))
    runs = seen_runs(rows, args.min_score)
    if not runs:
        print('không thấy vùng ở frame nào: chọn --ref lúc vật hiện rõ, hoặc hạ --min-score')
        return
    seen = [r for run in runs for r in run]
    print('thấy %d/%d frame, lần đầu %.2fs, lần cuối %.2fs' % (len(seen), len(rows), seen[0][0] / v.fps, seen[-1][0] / v.fps))
    print('bảng mẫu (dx, dy px so với tâm roi; scale so với mẫu; alpha 0–1 khi có --bg):')
    for i, sc, dx, dy, s, al in rows[::max(1, len(rows) // 24)]:
        if sc < args.min_score:
            print('  %6.2fs  —' % (i / v.fps))
        else:
            print('  %6.2fs  dx %7.1f  dy %7.1f  scale %.3f%s  khớp %.2f' % (
                i / v.fps, dx, dy, s, '' if np.isnan(al) else '  alpha %.2f' % al, sc))
    # Frame trùng (bản quay ít hình mới hơn fps của file) đọc ở `motion`; ở đây hai frame cùng số có thể chỉ do scale dò theo bước 1%.
    print('pha (ease khớp trên tiến độ chuẩn hoá; mốc lệch tới ±%.3fs, scale theo bước 1%%):' % (0.5 / v.fps))
    fmts = {'dx': '%.1f', 'dy': '%.1f', 'scale': '%.3f', 'alpha': '%.2f'}
    for run in runs:
        if len(runs) > 1:
            print(' đoạn thấy %.2f–%.2fs:' % (run[0][0] / v.fps, run[-1][0] / v.fps))
        for name, (t, vals, phs) in run_channels(run, v.fps, args.eps_px).items():
            for line in format_channel(name, t, vals, phs, fmts[name]):
                print(line)


# ---------------------------------------------------------------- phép kiểm trên video tổng hợp

def synth_video(path, fps=30, w=300, h=600):
    """Video đã biết đáp án: vật 80x50 hiện ở 0.3s (scale 0.2→1 OutBack 0.5s, alpha 0→1 Linear 0.2s), đứng tới 1.2s,
    trượt x +60 px OutCubic 0.6s, rồi nhún scale 1 ± 5% chu kỳ 1.0s tới hết 4.8s."""
    rng = np.random.default_rng(7)
    bg = np.full((h, w, 3), (40, 28, 20), np.uint8)
    bg[:, :, 1] = np.linspace(20, 60, h, dtype=np.uint8)[:, None]
    obj = np.zeros((50, 80, 3), np.uint8)
    obj[:] = (30, 160, 240)
    obj[::10, :] = (255, 255, 255)
    cv2.circle(obj, (25, 25), 15, (200, 40, 160), -1)
    cv2.putText(obj, 'W', (45, 38), cv2.FONT_HERSHEY_SIMPLEX, 1.0, (20, 20, 20), 2)
    out = cv2.VideoWriter(path, cv2.VideoWriter_fourcc(*'mp4v'), fps, (w, h))
    n = int(4.8 * fps)
    cx0, cy = 110.0, 300.0
    for i in range(n):
        t = i / fps
        frame = bg.copy()
        frame[rng.integers(0, h, 40), rng.integers(0, w, 40)] = 255      # chấm sáng lấp lánh: nhiễu nền như hạt
        u = lambda t0, d: min(1.0, max(0.0, (t - t0) / d))
        scale = 0.2 + 0.8 * EASES['OutBack'](u(0.3, 0.5))
        alpha = EASES['Linear'](u(0.3, 0.2))
        cx = cx0 + 60 * EASES['OutCubic'](u(1.2, 0.6))
        if t >= 1.8:
            scale = 1 + 0.05 * math.sin(2 * math.pi * (t - 1.8) / 1.0)
        if t >= 0.3:
            ow, oh = int(round(80 * scale)), int(round(50 * scale))
            if ow > 1 and oh > 1:
                o = cv2.resize(obj, (ow, oh), interpolation=cv2.INTER_AREA).astype(np.float32)
                x0, y0 = int(round(cx - ow / 2)), int(round(cy - oh / 2))
                region = frame[y0:y0 + oh, x0:x0 + ow].astype(np.float32)
                frame[y0:y0 + oh, x0:x0 + ow] = (alpha * o + (1 - alpha) * region).astype(np.uint8)
        out.write(frame)
    out.release()
    return (int(cx0 - 40), int(cy - 25), 80, 50)


def test_video():
    import tempfile
    path = os.path.join(tempfile.mkdtemp(prefix='video_demo_'), 'synth.mp4')
    roi = synth_video(path)
    v = Video(path)
    rows = track_rows(v, roi, 0, v.n - 1, v.index(1.0), v.index(0.1), 0.1, 2.0, 0.7)
    runs = seen_runs(rows, 0.7)
    ch = run_channels(max(runs, key=len), v.fps, 1.0)
    fails = []

    def check(cond, msg):
        print('  %s %s' % ('ok  ' if cond else 'SAI ', msg))
        if not cond:
            fails.append(msg)

    sc = [p for p in ch['scale'][2] if p['kind'] == 'ease']
    check(bool(sc) and sc[0]['fits'][0][1] == 'OutBack', 'scale hiện ra: ease OutBack (%s)' % (sc[0]['fits'][0][1] if sc else '—'))
    if sc:
        check(abs(sc[0]['t1'] - sc[0]['t0'] - 0.5) <= 0.05, 'scale dài 0.50s ± 0.05 (%.2f)' % (sc[0]['t1'] - sc[0]['t0']))
        # đo vượt trên quãng 0.2→1 nên đỉnh 1.08 là vượt 10% quãng, như OutBack chuẩn
        check(abs(overshoot_strength(sc[0]['over']) - 1.0) <= 0.2, 'Overshoot ≈ 1.0 (%.2f)' % overshoot_strength(sc[0]['over']))
    dx = [p for p in ch['dx'][2] if p['kind'] == 'ease']
    check(bool(dx) and 'OutCubic' in [f[1] for f in dx[0]['fits'][:3]], 'dx trượt: OutCubic trong ba ease gần nhất (%s)' % (
        ', '.join(f[1] for f in dx[0]['fits'][:3]) if dx else '—'))
    if dx:
        check(abs(dx[0]['v1'] - dx[0]['v0'] - 60) <= 2, 'dx quãng 60 px ± 2 (%.1f)' % (dx[0]['v1'] - dx[0]['v0']))
        check(abs(dx[0]['t1'] - dx[0]['t0'] - 0.6) <= 0.05, 'dx dài 0.60s ± 0.05 (%.2f)' % (dx[0]['t1'] - dx[0]['t0']))
    osc = [p for p in ch['scale'][2] if p['kind'] == 'osc' and p['t0'] > 1.6]
    check(bool(osc) and abs(osc[-1]['hi'] - 1.05) < 0.02 and abs(osc[-1]['lo'] - 0.95) < 0.02,
          'nhún scale 0.95…1.05 ± 0.02 (%s)' % ('%.3f…%.3f' % (osc[-1]['lo'], osc[-1]['hi']) if osc else '—'))
    check(bool(osc) and osc[-1].get('period') is not None and abs(osc[-1]['period'] - 1.0) <= 0.08,
          'nhún chu kỳ 1.00s ± 0.08 (%s)' % ('%.2f' % osc[-1]['period'] if osc and osc[-1].get('period') else '—'))
    check(bool(osc) and osc[-1].get('ease') == 'InOutSine', 'nhún mỗi nửa nhịp InOutSine (%s)' % (osc[-1].get('ease') if osc else '—'))
    # Nhún 5% chìm dưới ngưỡng khi đo cả khung: đo nhịp nhỏ thì roi quanh vật.
    m = analyse_motion(v, (roi[0] - 40, roi[1] - 30, 200, 110), 0, v.n - 1, None, 0.1)
    last = m['segs'][-1] if m['segs'] else None
    loop = loop_period(m['grays'], last[0], last[1], v.fps) if last else None
    check(bool(loop) and loop[1] < LOOP_RATIO_MAX and abs(loop[0] - 1.0) <= 0.05,
          'motion: lặp ~1.00s ± 0.05 (%s)' % ('%.2fs, tỉ lệ %.2f' % loop if loop else '—'))
    al = ch.get('alpha')
    check(al is not None and abs(float(np.median(al[1][-20:])) - 1.0) < 0.05, 'alpha lúc đứng ≈ 1')
    # Bản B chậm 10% (lặp mỗi frame thứ 10) và trễ 0.3 s: compare phải đọc ra đúng nhịp và trễ.
    slow = os.path.join(os.path.dirname(path), 'slow.mp4')
    cap = cv2.VideoCapture(path)
    out = cv2.VideoWriter(slow, cv2.VideoWriter_fourcc(*'mp4v'), v.fps, (v.w, v.h))
    ok, f = cap.read()
    for _ in range(int(round(0.3 * v.fps))):
        out.write(f)
    i = 0
    while ok:
        out.write(f)
        if i % 10 == 9:
            out.write(f)
        ok, f = cap.read()
        i += 1
    out.release()
    vb = Video(slow)
    box = (roi[0] - 40, roi[1] - 30, 200, 110)
    ca = norm_curve(v, analyse_motion(v, box, 0, v.n - 1, None, 0.1))
    cb = norm_curve(vb, analyse_motion(vb, box, 0, vb.n - 1, None, 0.1))
    r, k, c = best_warp(ca, cb, 2.0, 1.25)
    check(abs(k - 1.1) <= 0.02 and abs(c - 0.3) <= 0.1, 'compare: nhịp 1.10 ± 0.02, trễ 0.30 ± 0.1 (%.3f, %+.2f, r %.2f)' % (k, c, r))
    if fails:
        sys.exit('video: %d phép kiểm sai' % len(fails))
    print('video: mọi phép kiểm khớp đáp án dựng sẵn')


# ---------------------------------------------------------------- so hai video

GRID = 1.0 / 60            # lưới thời gian chung khi so hai video khác fps
EVENT_LEVEL = 0.4          # sự kiện: độ động (chuẩn hoá theo đỉnh của chính video) vượt mức này
QUIET_LEVEL = 0.15         # dưới mức này là lặng; một lần "bắt đầu động" là đi từ lặng lên EVENT_LEVEL


def norm_curve(v, m):
    """Độ động trên lưới GRID, chia cho phân vị 99 của chính video: hai bản quay khác nhiễu, khác độ sáng vẫn so được hình dạng."""
    t = (m['a'] + np.arange(len(m['e']))) / v.fps
    grid = np.arange(0.0, t[-1] + 1e-9, GRID)
    e = np.interp(grid, t, m['e'])
    top = float(np.percentile(e, 99)) or 1.0
    return np.clip(e / top, 0.0, 1.5)


def best_warp(ca, cb, max_lag, tempo):
    """(r, k, c) cho tương quan Pearson lớn nhất giữa A(t) và B(k·t + c): k là nhịp (B chậm hơn thì k > 1), c là trễ.
    Dò k trong [1/tempo, tempo] bước 1%, c trong [−max_lag, max_lag] bước GRID; chỉ tính trên phần A có B tương ứng."""
    ta = np.arange(len(ca)) * GRID
    tb = np.arange(len(cb)) * GRID
    best = (-2.0, 1.0, 0.0)
    ks = sorted({1.0} | set(np.exp(np.arange(-math.log(tempo), math.log(tempo) + 1e-9, 0.01)).round(4)))
    for k in ks:
        for c in np.arange(-max_lag, max_lag + 1e-9, GRID):
            x = k * ta + c
            ok = (x >= 0) & (x <= tb[-1])
            if ok.sum() < int(1.0 / GRID):
                continue
            a = ca[ok]
            b = np.interp(x[ok], tb, cb)
            if a.std() < 1e-6 or b.std() < 1e-6:
                continue
            r = float(np.corrcoef(a, b)[0, 1])
            if r > best[0]:
                best = (r, float(k), float(c))
    return best


def curve_events(c):
    """Mốc bắt đầu động (lặng ≥ 0.2 s rồi vượt EVENT_LEVEL) và đỉnh cục bộ cao (≥ 0.6, cách nhau ≥ 0.25 s), theo giây."""
    events = []
    quiet = int(0.2 / GRID)
    calm = 0
    for k, x in enumerate(c):
        if x < QUIET_LEVEL:
            calm += 1
            continue
        if x >= EVENT_LEVEL and calm >= quiet:
            j = k
            while j > 0 and c[j - 1] >= QUIET_LEVEL:
                j -= 1
            events.append(('bắt đầu', j * GRID))
        if x >= EVENT_LEVEL:
            calm = 0
    sep = int(0.25 / GRID)
    for k in range(1, len(c) - 1):
        if c[k] >= 0.6 and c[k] == c[max(0, k - sep):k + sep + 1].max():
            if not any(kind == 'đỉnh' and abs(t - k * GRID) < 0.25 for kind, t in events):
                events.append(('đỉnh', k * GRID))
    return sorted(events, key=lambda e: e[1])


def cmd_compare(args):
    va, vb = Video(args.a), Video(args.b)
    print('A ' + va.header())
    print('B ' + vb.header())
    ra, rb = parse_roi(args.roi, va.w, va.h), parse_roi(args.roi, vb.w, vb.h)
    ma = analyse_motion(va, ra, 0, va.n - 1, None, args.bin)
    mb = analyse_motion(vb, rb, 0, vb.n - 1, None, args.bin)
    ca, cb = norm_curve(va, ma), norm_curve(vb, mb)
    r, k, c = best_warp(ca, cb, min(args.max_lag, 0.5 * max(va.duration, vb.duration)), args.tempo)
    print('canh: B(t) = A(%.3f·t %+.2fs) (tương quan độ động %.2f%s)' % (k, c, r, '' if r >= 0.5 else ', thấp: hai video có thể khác nội dung, hãy đặt --roi'))
    ea, eb = curve_events(ca), curve_events(cb)
    used = set()
    pairs = []
    print('sự kiện (giây của A · của B · B lệch so với mốc canh k·tA + c):')
    for kind, ta in ea:
        want = k * ta + c
        cand = [(abs(tb - want), j, tb) for j, (kb, tb) in enumerate(eb) if kb == kind and j not in used and abs(tb - want) <= args.pair_window]
        if cand:
            _, j, tb = min(cand)
            used.add(j)
            pairs.append((ta, tb))
            print('  %-8s A %6.2fs · B %6.2fs · %+.2fs' % (kind, ta, tb, tb - want))
        else:
            print('  %-8s A %6.2fs · B —' % (kind, ta))
    for j, (kind, tb) in enumerate(eb):
        if j not in used:
            print('  %-8s A — · B %6.2fs' % (kind, tb))
    if len(pairs) >= 3:
        x = np.array([p[0] for p in pairs])
        y = np.array([p[1] for p in pairs])
        k2, c2 = np.polyfit(x, y, 1)
        verdict = 'cùng tốc độ (lệch < 2%)' if abs(k2 - 1) < 0.02 else 'B %s hơn A %.0f%%' % ('chậm' if k2 > 1 else 'nhanh', abs(k2 - 1) * 100)
        print('nhịp theo %d cặp sự kiện: tB = %.3f × tA %+.2fs → %s' % (len(pairs), k2, c2, verdict))
    for tag, v, m in (('A', va, ma), ('B', vb, mb)):
        for s, t in m['segs']:
            loop = loop_period(m['grays'], s, t, v.fps)
            if loop and loop[1] < LOOP_RATIO_MAX:
                print('%s lặp ~%.2fs trong đoạn %.2f–%.2fs' % (tag, loop[0], s / v.fps, t / v.fps))
    if not args.out:
        return
    # Mốc của A có B tương ứng theo k·t + c, chia đều.
    t_lo = max(0.0, -c / k)
    t_hi = min(va.duration, (vb.duration - c) / k) - 1.0 / va.fps
    count = max(2, args.count)
    ta_list = [t_lo + (t_hi - t_lo) * j / (count - 1) for j in range(count)]
    tb_list = [k * x + c for x in ta_list]
    fa = va.read([va.index(x) for x in ta_list])
    fb = vb.read([vb.index(x) for x in tb_list])
    cells = [label_cell(crop(fa[va.index(x)], ra), 'A %.2fs' % x, args.cell) for x in ta_list if va.index(x) in fa]
    cells += [label_cell(crop(fb[vb.index(x)], rb), 'B %.2fs' % x, args.cell) for x in tb_list if vb.index(x) in fb]
    sheet = compose(cells, count)
    write_png(args.out, sheet)
    print('sheet: %s  hàng trên A, hàng dưới B cùng mốc sau khi canh, %d mốc, %dx%d' % (args.out, count, sheet.shape[1], sheet.shape[0]))


# ---------------------------------------------------------------- CLI

def main():
    p = argparse.ArgumentParser(description='Đọc video demo thành ảnh và số. Xem docs/Editor/VideoDemo.md.')
    sub = p.add_subparsers(dest='cmd', required=True)

    def window(sp):
        sp.add_argument('--from', dest='t_from', type=float, default=None, help='giây bắt đầu')
        sp.add_argument('--to', dest='t_to', type=float, default=None, help='giây kết thúc')
        sp.add_argument('--roi', default=None, help='x,y,w,h gốc trên trái; <=1 là phần của khung')

    sp = sub.add_parser('sheet', help='ảnh lưới khung hình có nhãn giây')
    sp.add_argument('video')
    sp.add_argument('--out', required=True)
    window(sp)
    sp.add_argument('--count', type=int, default=12)
    sp.add_argument('--step', type=float, default=None, help='giây giữa hai ô, thay cho --count')
    sp.add_argument('--cols', type=int, default=6)
    sp.add_argument('--cell', type=int, default=180, help='bề rộng mỗi ô (px)')
    sp.set_defaults(fn=cmd_sheet)

    sp = sub.add_parser('frame', help='xuất khung hình PNG ở các mốc giây')
    sp.add_argument('video')
    sp.add_argument('--at', required=True, help='mốc giây, cách nhau dấu phẩy')
    sp.add_argument('--out-dir', required=True)
    sp.add_argument('--size', default=None, help='WxH, ví dụ 1080x2340 để so cùng cỡ Game view')
    sp.add_argument('--roi', default=None)
    sp.set_defaults(fn=cmd_frame)

    sp = sub.add_parser('motion', help='đoạn động, đoạn tĩnh, cắt cảnh, chu kỳ lặp')
    sp.add_argument('video')
    window(sp)
    sp.add_argument('--bin', type=float, default=0.1, help='giây mỗi ký tự của dải độ động')
    sp.add_argument('--threshold', type=float, default=None, help='ép ngưỡng động (0–255)')
    sp.set_defaults(fn=cmd_motion)

    sp = sub.add_parser('track', help='bám một vùng: vị trí, scale, alpha theo thời gian và ease gần nhất')
    sp.add_argument('video')
    window(sp)
    sp.add_argument('--ref', type=float, required=True, help='giây vật hiện rõ ở đúng vùng --roi, làm mẫu')
    sp.add_argument('--bg', type=float, default=None, help='giây vùng chưa có vật, để đo alpha')
    sp.add_argument('--min-scale', type=float, default=0.1)
    sp.add_argument('--max-scale', type=float, default=2.0)
    sp.add_argument('--min-score', type=float, default=0.7, help='dưới mức này coi là không thấy; mẫu nhỏ khớp nhiễu tới ~0.65')
    sp.add_argument('--eps-px', type=float, default=1.0, help='đổi dưới mức này mỗi frame coi là đứng yên')
    sp.add_argument('--csv', default=None)
    sp.set_defaults(fn=cmd_track)

    sp = sub.add_parser('compare', help='so nhịp hai video (demo và bản quay Unity): canh trễ và tốc độ, ghép cặp sự kiện')
    sp.add_argument('a')
    sp.add_argument('b')
    sp.add_argument('--roi', default=None, help='nên dùng phần của khung (<=1) vì hai video khác cỡ')
    sp.add_argument('--bin', type=float, default=0.1)
    sp.add_argument('--out', default=None)
    sp.add_argument('--count', type=int, default=8)
    sp.add_argument('--cell', type=int, default=150)
    sp.add_argument('--max-lag', type=float, default=5.0, help='giây lệch tối đa khi canh hai video')
    sp.add_argument('--tempo', type=float, default=1.25, help='dò nhịp B so với A trong [1/tempo, tempo]')
    sp.add_argument('--pair-window', type=float, default=0.4, help='hai sự kiện cùng loại cách nhau tới mức này (sau khi canh) là một cặp')
    sp.set_defaults(fn=cmd_compare)

    sp = sub.add_parser('selftest', help='đối chiếu bảng ease với PrimeTween, rồi đo video tổng hợp đã biết đáp án')
    sp.set_defaults(fn=lambda a: (test_eases(), test_video()))

    args = p.parse_args()
    args.fn(args)


if __name__ == '__main__':
    main()
