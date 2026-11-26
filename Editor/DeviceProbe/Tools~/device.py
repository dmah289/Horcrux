"""Nói chuyện với máy Android đang nối adb: đọc máy và app, điều khiển chạm, đo FPS và CPU theo luồng, kiểm APK đang cài.
Tài liệu: docs/Editor/DeviceProbe.md
"""
import argparse, json, os, re, shutil, statistics, subprocess, sys, tempfile, time, zipfile

sys.stdout.reconfigure(encoding="utf-8")


# ---------- tìm adb, máy, package ----------

def project_root():
    """Dự án chứa thư mục hiện tại, không thì dự án chứa chính tool (Horcrux nằm trong Assets của dự án)."""
    for start in (os.getcwd(), os.path.dirname(os.path.abspath(__file__))):
        d = start
        while True:
            if os.path.isfile(os.path.join(d, "ProjectSettings", "ProjectSettings.asset")):
                return d
            parent = os.path.dirname(d)
            if parent == d:
                break
            d = parent
    return None


def find_adb():
    if os.environ.get("ADB"):
        return os.environ["ADB"]
    on_path = shutil.which("adb")
    if on_path:
        return on_path
    exe = "adb.exe" if os.name == "nt" else "adb"
    candidates = []
    root = project_root()
    version = None
    if root:
        m = re.search(r"m_EditorVersion: (\S+)", open(os.path.join(root, "ProjectSettings", "ProjectVersion.txt"),
                                                         encoding="utf-8").read())
        version = m.group(1) if m else None
    hubs = [r"C:\Program Files\Unity\Hub\Editor", "/Applications/Unity/Hub/Editor"]
    hub_file = os.path.join(os.environ.get("APPDATA", ""), "UnityHub", "secondaryInstallPath.json")
    if os.path.isfile(hub_file):
        hubs.insert(0, json.load(open(hub_file, encoding="utf-8")))
    for hub in hubs:
        if not hub or not os.path.isdir(hub):
            continue
        versions = [version] if version else []
        versions += [v for v in sorted(os.listdir(hub), reverse=True) if v != version]
        for v in versions:
            for mid in ("Editor/Data/PlaybackEngines/AndroidPlayer", "PlaybackEngines/AndroidPlayer"):
                candidates.append(os.path.join(hub, v, mid, "SDK", "platform-tools", exe))
    for env in ("ANDROID_HOME", "ANDROID_SDK_ROOT"):
        if os.environ.get(env):
            candidates.append(os.path.join(os.environ[env], "platform-tools", exe))
    candidates.append(os.path.join(os.environ.get("LOCALAPPDATA", ""), "Android", "Sdk", "platform-tools", exe))
    for c in candidates:
        if os.path.isfile(c):
            return c
    sys.exit("không tìm thấy adb: đặt biến ADB, hoặc cài Android Build Support cho bản Unity của dự án")


ADB = None
SERIAL = None
PKG = None


def adb(*args, check=False, binary=False):
    r = subprocess.run([ADB, "-s", SERIAL, *args], capture_output=True)
    if check and r.returncode:
        sys.exit(f"adb {' '.join(args)}: {r.stderr.decode('utf-8', 'replace').strip()}")
    return r.stdout if binary else r.stdout.decode("utf-8", "replace")


def sh(cmd):
    """Một chuỗi lệnh chạy trong shell của máy; chuỗi có khoảng trắng phải tự quote phía máy."""
    return adb("shell", cmd)


def pick_serial(wanted):
    lines = subprocess.run([ADB, "devices"], capture_output=True, text=True).stdout.splitlines()[1:]
    ready = [l.split()[0] for l in lines if l.strip().endswith("device")]
    if wanted:
        if wanted not in ready:
            sys.exit(f"máy {wanted} không ở trạng thái device; đang nối: {ready or 'không có'}")
        return wanted
    if len(ready) != 1:
        sys.exit(f"cần đúng một máy hoặc --serial; đang nối: {ready or 'không có'} "
                 "(máy hiện 'unauthorized' thì bấm Cho phép gỡ lỗi USB trên máy)")
    return ready[0]


def pick_package(wanted):
    if wanted:
        return wanted
    root = project_root()
    if root:
        text = open(os.path.join(root, "ProjectSettings", "ProjectSettings.asset"), encoding="utf-8").read()
        m = re.search(r"applicationIdentifier:\s*\n(?:\s+\S+: .*\n)*?\s+Android: (\S+)", text)
        if m:
            return m.group(1)
    sys.exit("không đọc được package Android từ ProjectSettings: truyền --pkg")


# ---------- đọc máy và app ----------

def surface_layer():
    """Layer SurfaceView của app; tên đổi sau mỗi lần app quay lại foreground nên đọc lại mỗi lần đo."""
    rows = [l.strip() for l in sh("dumpsys SurfaceFlinger --list").splitlines()
            if l.startswith("SurfaceView - " + PKG)]
    blast = [l for l in rows if "(BLAST)" in l]
    return (blast or rows or [None])[0]


def render_sizes():
    out = sh("dumpsys SurfaceFlinger")
    sizes = re.findall(r"^\+ name:SurfaceView - " + re.escape(PKG) + r".*?w/h:([0-9a-f]+)x([0-9a-f]+)", out, re.M)
    counts = {}
    for w, h in sizes:
        key = f"{int(w, 16)}x{int(h, 16)}"
        counts[key] = counts.get(key, 0) + 1
    return counts


def cmd_info(a):
    prop = lambda k: sh(f"getprop {k}").strip()
    print(f"máy       {prop('ro.product.manufacturer')} {prop('ro.product.model')} · Android {prop('ro.build.version.release')}"
          f" · chip {prop('ro.board.platform')} · serial {SERIAL}")
    display = sh("dumpsys display")
    modes = sorted({float(f) for f in re.findall(r"fps=([\d.]+)", display.split("mDisplayModeSpecs")[0])})
    print(f"màn       {sh('wm size').strip().replace(chr(10), ' · ')} · tần số quét hỗ trợ {modes} Hz")
    pkg = sh(f"dumpsys package {PKG}")
    if "versionName" not in pkg:
        print(f"app       {PKG} chưa cài")
        return
    field = lambda k: (re.search(k + r"=(\S+(?: \S+)?)", pkg) or [None, "?"])[1]
    print(f"app       {PKG} {field('versionName')} ({field('versionCode').split()[0]}) · cài {field('lastUpdateTime')}")
    pid = sh(f"pidof {PKG}").strip()
    focus = re.search(r"mCurrentFocus=.*? (\S+)\}", sh("dumpsys window | grep mCurrentFocus"))
    print(f"chạy      pid {pid or '-'} · đang trước mặt {focus.group(1) if focus else '?'}")
    sizes = render_sizes()
    if sizes:
        print(f"render    buffer của app {', '.join(f'{k} ×{v}' for k, v in sizes.items())}"
              " (nhỏ hơn màn: hệ máy tự hạ độ phân giải game)")


def save_shot(path, scale=1.0, crop=None):
    png = adb("exec-out", "screencap -p", binary=True)
    w, h = int.from_bytes(png[16:20], "big"), int.from_bytes(png[20:24], "big")
    if scale == 1.0 and not crop:
        open(path, "wb").write(png)
        return f"{path} {w}x{h} — toạ độ tap = pixel ảnh"
    try:
        import site
        # Chạy với -I thì thư mục gói của user (nơi pip --user cài Pillow) không nằm trên sys.path.
        if site.getusersitepackages() not in sys.path:
            sys.path.append(site.getusersitepackages())
        from PIL import Image
    except ImportError:
        open(path, "wb").write(png)
        return f"{path} {w}x{h} (không có Pillow: bỏ --scale, --crop) — toạ độ tap = pixel ảnh"
    import io
    img = Image.open(io.BytesIO(png)).convert("RGB")
    x0, y0 = 0, 0
    if crop:
        x0, y0, x1, y1 = crop
        img = img.crop((x0, y0, x1, y1))
    if scale != 1.0:
        img = img.resize((max(1, round(img.width * scale)), max(1, round(img.height * scale))), Image.LANCZOS)
    img.save(path, quality=80) if path.lower().endswith((".jpg", ".jpeg")) else img.save(path)
    return (f"{path} {img.width}x{img.height} — toạ độ tap = (x / {scale:g} + {x0}, y / {scale:g} + {y0}) "
            f"trên màn {w}x{h}")


def cmd_shot(a):
    crop = [int(v) for v in a.crop.split(",")] if a.crop else None
    print(save_shot(a.path, a.scale, crop))


_INPUT = None


def input_cmd():
    """`cmd input` đi thẳng vào service đang chạy; `input` khởi một tiến trình Java mỗi lần (~40 ms chậm hơn mỗi chạm)."""
    global _INPUT
    if _INPUT is None:
        _INPUT = "cmd input" if re.search(r"^input$", sh("cmd -l"), re.M) else "input"
    return _INPUT


class Shell:
    """Một `adb shell` giữ sống, lệnh gửi qua stdin: bỏ được chi phí mở kết nối adb cho mỗi thao tác."""

    def __init__(self):
        self.p = subprocess.Popen([ADB, "-s", SERIAL, "shell"], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                  stderr=subprocess.DEVNULL, text=True, encoding="utf-8", errors="replace")
        self.n = 0

    def run(self, cmd):
        self.n += 1
        tag = f"__done{self.n}__"
        self.p.stdin.write(f"{cmd}; echo {tag}\n")
        self.p.stdin.flush()
        out = []
        for line in self.p.stdout:
            if line.strip() == tag:
                return "".join(out)
            out.append(line)
        return "".join(out)

    def close(self):
        try:
            self.p.stdin.write("exit\n")
            self.p.stdin.flush()
            self.p.wait(timeout=5)
        except Exception:
            self.p.kill()


def after_input(a):
    if getattr(a, "wait", 0):
        time.sleep(a.wait)
    if getattr(a, "shot", None):
        print(save_shot(a.shot, a.scale))


def cmd_tap(a):
    pts = a.xy
    if len(pts) % 2:
        sys.exit("tap cần cặp x y")
    for i in range(0, len(pts), 2):
        sh(f"{input_cmd()} tap {pts[i]} {pts[i + 1]}")
        if i + 2 < len(pts):
            time.sleep(a.gap)
    after_input(a)


def cmd_swipe(a):
    sh(f"{input_cmd()} swipe {a.x1} {a.y1} {a.x2} {a.y2} {a.ms}")
    after_input(a)


def keycode(name):
    return name if name.isdigit() or name.startswith("KEYCODE_") else "KEYCODE_" + name.upper()


def cmd_key(a):
    sh(f"{input_cmd()} keyevent {keycode(a.code)}")
    after_input(a)


def launcher_activity():
    out = sh(f"cmd package resolve-activity --brief -c android.intent.category.LAUNCHER {PKG}").strip().splitlines()
    if not out or "/" not in out[-1]:
        sys.exit(f"không tìm thấy activity khởi động của {PKG}")
    return out[-1].strip()


def cmd_resume(a):
    """Đưa app về foreground như bấm icon: task đang chạy được kéo lên, không mở lại từ đầu."""
    sh(f"am start -n {launcher_activity()}")
    after_input(a)


def cmd_stop(a):
    sh(f"am force-stop {PKG}")
    print(f"{PKG} đã dừng (dữ liệu app giữ nguyên)")


def cmd_wait(a):
    time.sleep(a.seconds)


# ---------- FPS ----------

class FrameSampler:
    """Đếm khung app thật sự đưa lên màn qua SurfaceFlinger --latency (127 khung gần nhất, mỗi lần đọc lấy phần mới)."""

    def __init__(self, target_fps):
        self.seen = set()
        self.miss_ms = 1.5 * 1000 / target_fps

    def read(self):
        name = surface_layer()
        if not name:
            return None
        stamps = []
        for l in sh(f"dumpsys SurfaceFlinger --latency '{name}'").splitlines()[1:]:
            p = l.split()
            if len(p) == 3 and p[1] != "0" and int(p[1]) < 2 ** 62:
                stamps.append(int(p[1]))
        fresh = sorted(s for s in stamps if s not in self.seen)
        self.seen.update(stamps)
        return fresh

    def line(self):
        fresh = self.read()
        stamp = time.strftime("%H:%M:%S")
        if fresh is None:
            return stamp + " -", None
        if len(fresh) < 2:
            return f"{stamp} n={len(fresh)}", None
        gaps = sorted((b - a) / 1e6 for a, b in zip(fresh, fresh[1:]))
        fps = (len(fresh) - 1) / ((fresh[-1] - fresh[0]) / 1e9)
        miss = sum(1 for g in gaps if g > self.miss_ms)
        return f"{stamp} fps={fps:5.1f} lỡ={miss:2d} max={gaps[-1]:6.1f}ms", (fps, miss)


def sample(seconds, target_fps, out=None):
    sampler = FrameSampler(target_fps)
    sampler.read()
    rows, end = [], time.time() + seconds
    while time.time() < end:
        time.sleep(1.0)
        text, row = sampler.line()
        if out:
            out.write(text + "\n")
            out.flush()
        if row:
            rows.append(row)
    return rows


def describe(rows):
    if not rows:
        return "không có khung (app không ở trước mặt?)"
    fps = [r[0] for r in rows]
    return (f"{len(rows)} giây: TB {statistics.mean(fps):.1f} fps, thấp nhất {min(fps):.1f}, "
            f"lỡ TB {statistics.mean(r[1] for r in rows):.1f}/giây")


def cmd_fps(a):
    if a.out:
        with open(a.out, "a", encoding="utf-8") as out:
            print(describe(sample(a.seconds, a.target, out)))
    else:
        rows = sample(a.seconds, a.target)
        print(describe(rows))


def cmd_mark(a):
    with open(a.log, "a", encoding="utf-8") as out:
        out.write(f"# {time.strftime('%H:%M:%S')} {a.text}\n")


def cmd_summary(a):
    segments, label, rows = [], "(đầu log)", []
    for l in open(a.log, encoding="utf-8").read().splitlines():
        if l.startswith("# "):
            segments.append((label, rows))
            label, rows = l[2:], []
            continue
        m = re.search(r"fps=\s*([\d.]+) lỡ=\s*(\d+)", l)
        if m:
            rows.append((float(m.group(1)), int(m.group(2))))
    segments.append((label, rows))
    if a.last:
        all_rows = [r for _, rs in segments for r in rs]
        print(f"{a.last} giây cuối: {describe(all_rows[-a.last:])}")
        return
    for label, rows in segments:
        if rows[a.skip:]:
            print(f"{label}\n    {describe(rows[a.skip:])}")


def cmd_res_ab(a):
    if not a.yes:
        sys.exit(f"res-ab đổi độ phân giải toàn máy (wm size {a.size}) trong ~{2 * a.seconds + 4:.0f} giây rồi trả lại: "
                 "hỏi developer, rồi chạy lại kèm --yes")
    out = open(a.out, "a", encoding="utf-8") if a.out else None
    note = lambda t: out and out.write(f"# {time.strftime('%H:%M:%S')} {t}\n")
    try:
        note("res-ab: độ phân giải gốc")
        native = sample(a.seconds, a.target, out)
        sh(f"wm size {a.size}")
        time.sleep(3)
        note(f"res-ab: wm size {a.size}")
        small = sample(a.seconds, a.target, out)
    finally:
        sh("wm size reset")
        if out:
            out.close()
    print(f"gốc     {describe(native)}\n{a.size:8}{describe(small)}")
    print(f"đã trả: {sh('wm size').strip()}")


# ---------- ghi thao tác người chơi, phát lại kịch bản ----------

def touchscreen():
    """(thiết bị cảm ứng, max X thô, max Y thô) đọc từ `getevent -p`: thiết bị có trục ABS_MT_POSITION_X (0035)."""
    dev = None
    for line in sh("getevent -p").splitlines():
        m = re.match(r"add device \d+: (\S+)", line)
        if m:
            dev, mx = m.group(1), None
            continue
        m = re.search(r"\b0035\s*:.*max (\d+)", line)
        if m and dev:
            mx = int(m.group(1))
        m = re.search(r"\b0036\s*:.*max (\d+)", line)
        if m and dev and mx is not None:
            return dev, mx, int(m.group(1))
    sys.exit("không tìm thấy màn cảm ứng trong getevent -p")


def screen_size():
    out = sh("wm size")
    m = re.search(r"Override size: (\d+)x(\d+)", out) or re.search(r"Physical size: (\d+)x(\d+)", out)
    return int(m.group(1)), int(m.group(2))


def cmd_record(a):
    dev, max_x, max_y = touchscreen()
    sw, sh_ = screen_size()
    to_screen = lambda x, y: (round(x * sw / (max_x + 1)), round(y * sh_ / (max_y + 1)))
    proc = subprocess.Popen([ADB, "-s", SERIAL, "shell", "getevent", "-lt", dev], stdout=subprocess.PIPE,
                            stderr=subprocess.DEVNULL, text=True, encoding="utf-8", errors="replace")
    print(f"đang ghi {dev} ({a.seconds:g} s, Ctrl-C để dừng sớm) — chơi trên máy", flush=True)
    end = time.time() + a.seconds

    def until_end():
        for line in proc.stdout:
            yield line
            if time.time() > end:
                return

    try:
        gestures = parse_touches(until_end(), to_screen)
    finally:
        proc.kill()
    write_script(a.out, gestures, a.move, f"màn {sw}x{sh_}")


def parse_touches(lines, to_screen):
    """Dòng `getevent -lt` → các lần chạm của ngón đầu: {t0, t1, pts}. Chạm đa điểm bỏ: input tap/swipe không phát lại được."""
    gestures, slot, cur, x, y = [], 0, None, None, None
    try:
        for line in lines:
            m = re.match(r"\[\s*([\d.]+)\]\s+EV_(\w+)\s+(\w+)\s+(\S+)", line)
            if not m:
                continue
            t, kind, code, val = float(m.group(1)), m.group(2), m.group(3), m.group(4)
            if code == "ABS_MT_SLOT":
                slot = int(val, 16)
            elif slot != 0:
                continue
            elif code == "ABS_MT_TRACKING_ID":
                if val != "ffffffff":
                    cur = {"t0": t, "pts": []}
                elif cur and cur["pts"]:
                    cur["t1"] = t
                    gestures.append(cur)
                    cur = None
            elif code == "ABS_MT_POSITION_X":
                x = int(val, 16)
            elif code == "ABS_MT_POSITION_Y":
                y = int(val, 16)
            elif kind == "SYN" and cur is not None and x is not None and y is not None:
                cur["pts"].append(to_screen(x, y))
    except KeyboardInterrupt:
        pass
    return gestures


def write_script(out, gestures, move_px, note):
    lines, prev_end = [f"# ghi {time.strftime('%Y-%m-%d %H:%M')} · {note}"], None
    for g in gestures:
        if prev_end is not None:
            lines.append(f"wait {max(0.0, g['t0'] - prev_end):.2f}")
        (x0, y0), (x1, y1) = g["pts"][0], g["pts"][-1]
        ms = round((g["t1"] - g["t0"]) * 1000)
        if abs(x1 - x0) + abs(y1 - y0) > move_px or ms > 500:
            lines.append(f"swipe {x0} {y0} {x1} {y1} {max(ms, 50)}")  # giữ lâu tại chỗ cũng phát lại bằng swipe
        else:
            lines.append(f"tap {x0} {y0}")
        prev_end = g["t1"]
    open(out, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    print(f"{out}: {len(gestures)} thao tác")


def cmd_play(a):
    steps = []
    for n, raw in enumerate(open(a.script, encoding="utf-8").read().splitlines(), 1):
        line = raw.split("#", 1)[0].strip() if not raw.lstrip().startswith("mark") else raw.strip()
        if line:
            steps.append((n, line.split(maxsplit=1)[0], line))
    known = {"tap", "swipe", "key", "wait", "resume", "mark", "shot"}
    bad = [f"dòng {n}: {w}" for n, w, _ in steps if w not in known]
    if bad:
        sys.exit("lệnh lạ trong kịch bản: " + ", ".join(bad))

    log = open(a.fps_out, "a", encoding="utf-8") if a.fps_out else None
    stop = None
    if log:
        import threading
        stop = threading.Event()

        def run_sampler():
            sampler = FrameSampler(a.target)
            sampler.read()
            while not stop.wait(1.0):
                text, _ = sampler.line()
                log.write(text + "\n")
                log.flush()

        threading.Thread(target=run_sampler, daemon=True).start()

    shell, inp = Shell(), input_cmd()
    t_start = time.time()
    try:
        for n, word, line in steps:
            arg = line.split()[1:]
            if word == "wait":
                time.sleep(float(arg[0]) / a.speed)
            elif word == "tap":
                shell.run(f"{inp} tap {arg[0]} {arg[1]}")
            elif word == "swipe":
                ms = round(int(arg[4] if len(arg) > 4 else 300) / a.speed)
                shell.run(f"{inp} swipe {arg[0]} {arg[1]} {arg[2]} {arg[3]} {max(ms, 50)}")
            elif word == "key":
                shell.run(f"{inp} keyevent {keycode(arg[0])}")
            elif word == "resume":
                shell.run(f"am start -n {launcher_activity()}")
            elif word == "mark":
                text = line.split(maxsplit=1)[1] if len(line.split()) > 1 else f"dòng {n}"
                if log:
                    log.write(f"# {time.strftime('%H:%M:%S')} {text}\n")
                    log.flush()
                print(f"{time.time() - t_start:6.1f}s  mốc: {text}", flush=True)
            elif word == "shot":
                print(f"{time.time() - t_start:6.1f}s  " + save_shot(arg[0], float(arg[1]) if len(arg) > 1 else 1.0),
                      flush=True)
    finally:
        shell.close()
        if stop:
            stop.set()
            time.sleep(1.2)
            log.close()
    print(f"xong {len(steps)} bước trong {time.time() - t_start:.1f} s")
    if a.fps_out:
        cmd_summary(argparse.Namespace(log=a.fps_out, skip=a.skip, last=None))


# ---------- CPU theo luồng ----------

def cmd_threads(a):
    script = (f"p=$(pidof {PKG}); [ -z \"$p\" ] && exit 1; "
              "snap() { cat /proc/uptime; cat /proc/$p/task/*/stat 2>/dev/null; }; "
              f"snap; echo ===; sleep {a.seconds}; snap")
    out = sh(script)
    if "===" not in out:
        sys.exit(f"{PKG} không chạy")

    def parse(block):
        lines = block.strip().splitlines()
        ticks = {}
        for l in lines[1:]:
            # "<tid> (<tên, có thể có khoảng trắng>) <state> ... utime stime": trường 14, 15 tính từ 1.
            m = re.match(r"(\d+) \((.*)\) (.*)", l)
            if m:
                rest = m.group(3).split()
                ticks[m.group(1)] = (m.group(2), int(rest[11]) + int(rest[12]))
        return float(lines[0].split()[0]), ticks

    (t0, before), (t1, after) = (parse(b) for b in out.split("==="))
    span = t1 - t0  # chia theo uptime thật của máy, không theo --seconds: lệnh adb và cat cũng tốn thời gian
    rows = sorted(((v[1] - before[k][1], v[0]) for k, v in after.items() if k in before), reverse=True)
    hz = 100  # USER_HZ của kernel Android
    print(f"{span:.1f} s · tổng {sum(r[0] for r in rows) * 1000 / hz / span:.0f} ms CPU mỗi giây")
    for d, name in rows[:a.top]:
        print(f"{d * 1000 / hz / span:6.0f} ms/s  {name}")


# ---------- APK đang cài ----------

def cmd_apk_has(a):
    path = sh(f"pm path {PKG}").strip().splitlines()
    base = next((p[8:] for p in path if p.endswith("base.apk")), None)
    if not base:
        sys.exit(f"{PKG} chưa cài")
    tmp = tempfile.mkdtemp()
    local = os.path.join(tmp, "base.apk")
    try:
        adb("pull", base, local, check=True)
        z = zipfile.ZipFile(local)
        blobs = [n for n in z.namelist() if n.startswith("assets/bin/Data/Managed/")]
        data = [z.read(n) for n in blobs]
        z.close()
        for s in a.strings:
            needles = (s.encode("utf-8"), s.encode("utf-16-le"))
            hit = [n for n, d in zip(blobs, data) if any(x in d for x in needles)]
            print(f"{'có   ' if hit else 'KHÔNG'} {s}" + (f"  ({os.path.basename(hit[0])})" if hit else ""))
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


# ---------- logcat ----------

def cmd_log(a):
    if a.clear:
        adb("logcat", "-c")
        print("đã xoá logcat")
        return
    seen, kept = set(), []
    for l in adb("logcat", "-d").splitlines():
        if a.grep and not re.search(a.grep, l):
            continue
        # Dòng threadtime: "<ngày> <giờ> <pid> <tid> <mức> <tag>: <nội dung>"; khử trùng bỏ phần giờ và pid.
        body = re.sub(r"^\S+ \S+\s+\d+\s+\d+ ", "", l) if a.dedupe else l
        if body not in seen:
            seen.add(body)
            kept.append(l)
    if len(kept) > a.max:
        print(f"… bỏ {len(kept) - a.max} dòng cũ hơn")
    for l in kept[-a.max:]:
        print(l[:a.width])


# ---------- CLI ----------

def main():
    global ADB, SERIAL, PKG
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--serial", default=os.environ.get("ANDROID_SERIAL"))
    p.add_argument("--pkg")
    sub = p.add_subparsers(dest="cmd", required=True)

    def with_after(sp):
        sp.add_argument("--wait", type=float, default=0, help="giây chờ sau thao tác")
        sp.add_argument("--shot", help="chụp màn vào file này sau khi chờ")
        sp.add_argument("--scale", type=float, default=1.0, help="thu nhỏ ảnh chụp (cần Pillow)")
        return sp

    sub.add_parser("devices")
    sub.add_parser("info")
    s = sub.add_parser("shot"); s.add_argument("path"); s.add_argument("--scale", type=float, default=1.0)
    s.add_argument("--crop", help="x0,y0,x1,y1 theo pixel màn, cắt trước khi thu nhỏ")
    s = sub.add_parser("record"); s.add_argument("out"); s.add_argument("--seconds", type=float, default=60)
    s.add_argument("--move", type=int, default=30, help="ngón dời quá số pixel này thì là swipe")
    s = sub.add_parser("play"); s.add_argument("script"); s.add_argument("--fps-out")
    s.add_argument("--speed", type=float, default=1.0, help="2 = chờ và vuốt nhanh gấp đôi kịch bản")
    s.add_argument("--target", type=float, default=60); s.add_argument("--skip", type=int, default=2)
    s = with_after(sub.add_parser("tap")); s.add_argument("xy", type=int, nargs="+"); s.add_argument("--gap", type=float, default=0.3)
    s = with_after(sub.add_parser("swipe"))
    for k in ("x1", "y1", "x2", "y2"):
        s.add_argument(k, type=int)
    s.add_argument("ms", type=int, nargs="?", default=300)
    s = with_after(sub.add_parser("key")); s.add_argument("code", help="HOME, BACK, KEYCODE_… hoặc số")
    with_after(sub.add_parser("resume"))
    sub.add_parser("stop")
    s = sub.add_parser("wait"); s.add_argument("seconds", type=float)
    s = sub.add_parser("fps"); s.add_argument("--seconds", type=float, default=30); s.add_argument("--out")
    s.add_argument("--target", type=float, default=60, help="FPS mục tiêu; khung dài hơn 1.5 lần nhịp này tính là lỡ")
    s = sub.add_parser("mark"); s.add_argument("log"); s.add_argument("text")
    s = sub.add_parser("summary"); s.add_argument("log"); s.add_argument("--skip", type=int, default=2,
                                                                         help="bỏ n giây đầu mỗi đoạn (chuyển cảnh)")
    s.add_argument("--last", type=int, help="chỉ tóm n giây cuối của log")
    s = sub.add_parser("res-ab"); s.add_argument("--size", default="720x1600"); s.add_argument("--seconds", type=float, default=25)
    s.add_argument("--target", type=float, default=60); s.add_argument("--out"); s.add_argument("--yes", action="store_true")
    s = sub.add_parser("threads"); s.add_argument("--seconds", type=float, default=5); s.add_argument("--top", type=int, default=10)
    s = sub.add_parser("apk-has"); s.add_argument("strings", nargs="+")
    s = sub.add_parser("log"); s.add_argument("--grep"); s.add_argument("--clear", action="store_true")
    s.add_argument("--max", type=int, default=80); s.add_argument("--width", type=int, default=220)
    s.add_argument("--no-dedupe", dest="dedupe", action="store_false")
    s = sub.add_parser("adb", help="chạy adb với đường dẫn và máy đã chọn"); s.add_argument("args", nargs=argparse.REMAINDER)
    a = p.parse_args()

    ADB = find_adb()
    if a.cmd == "devices":
        print(ADB)
        print(subprocess.run([ADB, "devices", "-l"], capture_output=True, text=True).stdout.strip())
        return
    offline = {"summary": cmd_summary, "mark": cmd_mark, "wait": cmd_wait}
    if a.cmd in offline:
        offline[a.cmd](a)
        return
    SERIAL = pick_serial(a.serial)
    if a.cmd == "adb":
        r = subprocess.run([ADB, "-s", SERIAL, *a.args])
        sys.exit(r.returncode)
    PKG = pick_package(a.pkg)
    {"info": cmd_info, "shot": cmd_shot, "tap": cmd_tap, "swipe": cmd_swipe, "key": cmd_key, "resume": cmd_resume,
     "stop": cmd_stop, "fps": cmd_fps, "record": cmd_record, "play": cmd_play, "res-ab": cmd_res_ab, "threads": cmd_threads,
     "apk-has": cmd_apk_has, "log": cmd_log}[a.cmd](a)


if __name__ == "__main__":
    main()
