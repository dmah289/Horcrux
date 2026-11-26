"""Đọc dữ liệu Spine xuất dạng JSON (.json, .atlas.txt) thành bảng gọn, không cần mở Spine Editor hay Unity.
Xem docs/Editor/SpineJson.md.

  spine_json.py summary <skeleton.json> [<skeleton.json> …]
  spine_json.py anim    <skeleton.json> <tên anim> [--timeline <chuỗi trong tên xương hay slot>]
  spine_json.py atlas   <file.atlas.txt>
  spine_json.py selftest

Chỉ cần thư viện chuẩn của Python; chạy được bằng -I.
"""
import argparse
import json
import os
import sys
import tempfile


def num(v):
    """Số gọn: bỏ đuôi .0, tối đa 4 chữ số lẻ."""
    if isinstance(v, float):
        s = f"{v:.4f}".rstrip("0").rstrip(".")
        return s if s not in ("", "-0") else "0"
    return str(v)


def max_time(node):
    """Mốc lớn nhất của mọi khoá trong một anim. Khoá không ghi `time` là mốc 0 (định dạng JSON của Spine 4)."""
    best = 0.0
    if isinstance(node, dict):
        t = node.get("time")
        if isinstance(t, (int, float)):
            best = float(t)
        for v in node.values():
            best = max(best, max_time(v))
    elif isinstance(node, list):
        for v in node:
            best = max(best, max_time(v))
    return best


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


# ---------------------------------------------------------------- summary

def timeline_owners(anim):
    """Mỗi loại timeline → tên xương, slot, ràng buộc có khoá trong anim, kèm tên kênh."""
    out = []
    for kind in ("bones", "slots", "ik", "transform", "path", "physics"):
        for owner, channels in anim.get(kind, {}).items():
            names = ",".join(channels.keys()) if isinstance(channels, dict) else ""
            out.append(f"{kind[:-1] if kind.endswith('s') else kind}:{owner}({names})")
    for skin, slots in anim.get("attachments", {}).items():
        for slot, atts in slots.items():
            for att in atts:
                out.append(f"deform:{slot}/{att}")
    if "drawOrder" in anim:
        out.append(f"drawOrder×{len(anim['drawOrder'])}")
    return out


def cmd_summary(args):
    for path in args.files:
        d = load(path)
        sk = d.get("skeleton", {})
        print(f"== {path}")
        print(f"spine {sk.get('spine', '?')} · khung xuất x={num(sk.get('x', 0))} y={num(sk.get('y', 0))} "
              f"{num(sk.get('width', 0))}x{num(sk.get('height', 0))}")

        print(f"xương ({len(d.get('bones', []))}): tên ← cha · x,y · xoay · scale")
        for b in d.get("bones", []):
            extra = ""
            if b.get("inherit", "normal") != "normal":
                extra += f" · inherit {b['inherit']}"
            if "shearX" in b or "shearY" in b:
                extra += " · shear"
            print(f"  {b['name']} ← {b.get('parent', '-')} · {num(b.get('x', 0))},{num(b.get('y', 0))} · "
                  f"{num(b.get('rotation', 0))} · {num(b.get('scaleX', 1))},{num(b.get('scaleY', 1))}{extra}")

        print(f"slot ({len(d.get('slots', []))}, thứ tự vẽ dưới lên): tên ← xương · attachment setup · blend")
        for s in d.get("slots", []):
            print(f"  {s['name']} ← {s['bone']} · {s.get('attachment', '-')} · {s.get('blend', 'normal')}")

        skins = d.get("skins", [])
        for skin in skins:
            atts = skin.get("attachments", {})
            print(f"skin {skin.get('name', '?')}: slot/attachment · kiểu · ảnh · cỡ")
            for slot, items in atts.items():
                for name, a in items.items():
                    kind = a.get("type", "region")
                    size = f"{num(a['width'])}x{num(a['height'])}" if "width" in a else "-"
                    print(f"  {slot}/{name} · {kind} · {a.get('path', name)} · {size}")

        events = d.get("events", {})
        print("event: " + (", ".join(events.keys()) if events else "không có"))

        print("anim: tên · thời lượng · timeline")
        for name, anim in d.get("animations", {}).items():
            print(f"  {name} · {num(max_time(anim))} s · {' '.join(timeline_owners(anim))}")
            for e in anim.get("events", []):
                print(f"    event {e.get('name')} @ {num(e.get('time', 0))} s")


# ---------------------------------------------------------------- anim

def key_values(key):
    """Kênh khoá ghi ra; Spine bỏ kênh bằng mặc định (x 0, value 0), khoá không còn kênh nào là về setup."""
    text = " ".join(f"{k}={num(v) if isinstance(v, (int, float)) else v}"
                    for k, v in key.items() if k not in ("time", "curve"))
    return text or "setup"


def print_channels(prefix, channels, filt):
    for owner, chans in channels.items():
        if filt and filt not in owner:
            continue
        for chan, keys in chans.items():
            print(f"{prefix}:{owner}.{chan}")
            for k in keys:
                curve = k.get("curve")
                ease = "" if curve is None else (" stepped" if curve == "stepped" else " bezier")
                print(f"  {num(k.get('time', 0))}: {key_values(k)}{ease}")


def cmd_anim(args):
    d = load(args.file)
    anims = d.get("animations", {})
    if args.anim not in anims:
        sys.exit(f"không có anim '{args.anim}'; có: {', '.join(anims)}")
    anim = anims[args.anim]
    print(f"{args.anim} · {num(max_time(anim))} s")
    for kind in ("bones", "slots", "ik", "transform", "path", "physics"):
        print_channels(kind[:-1], anim.get(kind, {}), args.timeline)
    for skin, slots in anim.get("attachments", {}).items():
        for slot, atts in slots.items():
            if args.timeline and args.timeline not in slot:
                continue
            for att, chans in atts.items():
                for chan, keys in chans.items():
                    print(f"{chan}:{skin}/{slot}/{att} · {len(keys)} khoá @ " +
                          " ".join(num(k.get("time", 0)) for k in keys))
    for k in anim.get("drawOrder", []):
        print(f"drawOrder {num(k.get('time', 0))}: {k.get('offsets', [])}")
    for e in anim.get("events", []):
        print(f"event {e.get('name')} @ {num(e.get('time', 0))}")


# ---------------------------------------------------------------- atlas

def parse_atlas(text):
    """Atlas Spine 4: trang mở bằng tên ảnh sau dòng trống, rồi `khoá:giá trị` của trang; dòng không có ':' là tên vùng."""
    pages, page, region = [], None, None
    expect_page = True
    for raw in text.splitlines():
        line = raw.strip()
        if not line:
            expect_page = True
            continue
        if expect_page:
            page = {"name": line, "regions": []}
            pages.append(page)
            region = None
            expect_page = False
            continue
        if ":" not in line:
            region = {"name": line}
            page["regions"].append(region)
            continue
        key, value = (s.strip() for s in line.split(":", 1))
        (region if region is not None else page)[key] = value
    return pages


def cmd_atlas(args):
    with open(args.file, encoding="utf-8") as f:
        pages = parse_atlas(f.read())
    for p in pages:
        alpha = "PMA (nhân sẵn alpha)" if p.get("pma") == "true" else "straight"
        print(f"trang {p['name']} · {p.get('size', '?')} · {alpha} · filter {p.get('filter', '?')}")
        print("  vùng · bounds x,y,w,h · offsets (cắt viền: x,y,cỡ gốc) · xoay")
        for r in p["regions"]:
            print(f"  {r['name']} · {r.get('bounds', '?')} · {r.get('offsets', '-')} · {r.get('rotate', '-')}")


# ---------------------------------------------------------------- selftest

SAMPLE_JSON = {
    "skeleton": {"spine": "4.3.26", "x": -10, "y": -5, "width": 20, "height": 10},
    "bones": [{"name": "root"}, {"name": "L", "parent": "root", "x": -8.5},
              {"name": "R", "parent": "root", "x": 8.5, "inherit": "noScale"}],
    "slots": [{"name": "glue", "bone": "root", "attachment": "glue"}],
    "skins": [{"name": "default", "attachments": {"glue": {"glue": {"width": 40, "height": 12}}}}],
    "events": {"break": {}},
    "animations": {
        "Snap": {
            "bones": {"L": {"translate": [{"x": 0}, {"time": 0.5333, "x": -30, "curve": [0.2, 0, 0.4, 1]}]},
                      "R": {"rotate": [{"value": 0}, {"time": 1.1333, "value": 25}]}},
            "slots": {"glue": {"rgba": [{"time": 0.6, "color": "ffffffff"}, {"time": 0.9, "color": "ffffff00"}]}},
            "events": [{"time": 0.5333, "name": "break"}],
        }
    },
}
SAMPLE_ATLAS = "\nsheet.png\nsize:432,208\nfilter:Linear,Linear\npma:true\na\nbounds:2,2,238,204\noffsets:1,1,240,240\nb\nbounds:337,63,48,81\nrotate:90\n"


def cmd_selftest(_):
    import contextlib
    import io
    tmp = tempfile.mkdtemp()
    jp = os.path.join(tmp, "s.json")
    with open(jp, "w", encoding="utf-8") as f:
        json.dump(SAMPLE_JSON, f)
    ap = os.path.join(tmp, "s.atlas.txt")
    with open(ap, "w", encoding="utf-8") as f:
        f.write(SAMPLE_ATLAS)

    def run(fn, **kw):
        buf = io.StringIO()
        with contextlib.redirect_stdout(buf):
            fn(argparse.Namespace(**kw))
        return buf.getvalue()

    checks = []
    out = run(cmd_summary, files=[jp])
    checks.append(("thời lượng là khoá muộn nhất của mọi timeline", "Snap · 1.1333 s" in out))
    checks.append(("khoá không ghi time tính là 0, không làm hỏng max", max_time({"a": [{"x": 1}]}) == 0.0))
    checks.append(("event khai báo và mốc trong anim", "event: break" in out and "event break @ 0.5333 s" in out))
    checks.append(("inherit khác normal được đánh dấu", "inherit noScale" in out))
    checks.append(("attachment có cỡ", "glue/glue · region · glue · 40x12" in out))
    out = run(cmd_anim, file=jp, anim="Snap", timeline="L")
    checks.append(("anim lọc theo tên xương", "bone:L.translate" in out and "bone:R" not in out))
    checks.append(("khoá có curve bezier", "0.5333: x=-30 bezier" in out))
    out = run(cmd_atlas, file=ap)
    checks.append(("atlas đọc pma và vùng xoay", "PMA" in out and "b · 337,63,48,81 · - · 90" in out))
    checks.append(("atlas không pma là straight", "straight" in run(cmd_atlas, file=_write(tmp, SAMPLE_ATLAS.replace("pma:true\n", "")))))

    failed = [name for name, ok in checks if not ok]
    for name, ok in checks:
        print(("ok   " if ok else "FAIL ") + name)
    sys.exit(1 if failed else 0)


def _write(tmp, text):
    p = os.path.join(tmp, "n.atlas.txt")
    with open(p, "w", encoding="utf-8") as f:
        f.write(text)
    return p


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("summary")
    p.add_argument("files", nargs="+")
    p = sub.add_parser("anim")
    p.add_argument("file")
    p.add_argument("anim")
    p.add_argument("--timeline", help="chỉ in timeline có tên xương, slot chứa chuỗi này")
    p = sub.add_parser("atlas")
    p.add_argument("file")
    sub.add_parser("selftest")
    args = ap.parse_args()
    {"summary": cmd_summary, "anim": cmd_anim, "atlas": cmd_atlas, "selftest": cmd_selftest}[args.cmd](args)


if __name__ == "__main__":
    main()
