"""So lớp contract của dự án với repo nguồn: file nào thiếu, khác nội dung, khác GUID, chỉ có ở dự án.
Xem docs/Editor/ContractDiff.md.

  contract_diff.py --source <Assets gốc> --project <Assets dự án> --roots <a,b/c,...>
                   [--ext .cs,.asmdef] [-o report.md]

Mã thoát: 0 sạch · 1 có missing, differ hoặc guid · 2 dùng sai.
"""
import argparse
import os
import re
import sys

GUID_RE = re.compile(r"^guid:\s*([0-9a-f]{32})", re.M)


def walk_files(base, exts):
    """Mọi file đuôi `exts` dưới `base`, đường dẫn tương đối dùng `/`; bỏ thư mục đuôi `~` như Unity."""
    for d, dirs, fs in os.walk(base):
        dirs[:] = [x for x in dirs if not x.endswith("~")]
        for f in fs:
            if f.endswith(exts):
                yield os.path.relpath(os.path.join(d, f), base).replace("\\", "/")


def files_under(base, root, exts):
    """Đường dẫn tương đối với `base` (Assets), để hai cây so được bằng cùng một khoá."""
    return {f"{root}/{rel}" for rel in walk_files(os.path.join(base, root), exts)}


def normalized_text(path):
    """Nội dung so sánh: bỏ BOM, CRLF về LF, cắt khoảng trắng cuối dòng và cuối file."""
    with open(path, "rb") as fh:
        b = fh.read()
    if b.startswith(b"\xef\xbb\xbf"):
        b = b[3:]
    t = b.decode("utf-8", errors="replace").replace("\r\n", "\n")
    return "\n".join(line.rstrip() for line in t.split("\n")).strip()


def guid_of(path):
    try:
        with open(path + ".meta", encoding="utf-8", errors="replace") as fh:
            m = GUID_RE.search(fh.read())
    except FileNotFoundError:
        return None
    return m.group(1) if m else None


def guid_index(base, exts):
    """GUID → đường dẫn tương đối, cho mọi file đuôi `exts` dưới `base`."""
    idx = {}
    for rel in walk_files(base, exts):
        g = guid_of(os.path.join(base, rel))
        if g:
            idx[g] = rel
    return idx


def compare_root(source, project, root, exts, source_guids):
    a = files_under(source, root, exts)
    b = files_under(project, root, exts)
    both = sorted(a & b)
    rows = {
        "missing": sorted(a - b),
        "differ": [f for f in both
                   if normalized_text(os.path.join(source, f)) != normalized_text(os.path.join(project, f))],
        "guid": [],
        "copyOnly": [],
    }
    for f in both:
        gs, gp = guid_of(os.path.join(source, f)), guid_of(os.path.join(project, f))
        if gs != gp:
            rows["guid"].append((f, gs, gp))
    for f in sorted(b - a):
        g = guid_of(os.path.join(project, f))
        rows["copyOnly"].append((f, source_guids.get(g) if g else None))
    return len(a), len(b), len(both), rows


def render_root(root, na, nb, nboth, rows):
    out = [f"## {root}\n",
           f"source {na} · project {nb} · chung {nboth} · thiếu {len(rows['missing'])} · khác {len(rows['differ'])}"
           f" · GUID khác {len(rows['guid'])} · chỉ project {len(rows['copyOnly'])}\n"]
    if rows["missing"]:
        out.append("### Thiếu (có ở source)\n" + "\n".join(f"- {f}" for f in rows["missing"]) + "\n")
    if rows["differ"]:
        out.append("### Khác nội dung\n" + "\n".join(f"- {f}" for f in rows["differ"]) + "\n")
    if rows["guid"]:
        out.append("### GUID khác\n" + "\n".join(f"- {f}: source {s} / project {p}" for f, s, p in rows["guid"]) + "\n")
    if rows["copyOnly"]:
        out.append("### Chỉ có ở project\n" + "\n".join(
            f"- {f}" + (f" (GUID trùng source `{src}`)" if src else "") for f, src in rows["copyOnly"]) + "\n")
    return out


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--source", required=True, help="thư mục Assets của repo nguồn")
    ap.add_argument("--project", required=True, help="thư mục Assets của dự án")
    ap.add_argument("--roots", required=True, help="thư mục con dưới Assets, cách nhau bằng dấu phẩy")
    ap.add_argument("--ext", default=".cs,.asmdef", help="đuôi file so sánh, mặc định .cs,.asmdef")
    ap.add_argument("-o", "--out", help="ghi báo cáo Markdown vào file này")
    args = ap.parse_args(argv)

    for label, path in (("--source", args.source), ("--project", args.project)):
        if not os.path.isdir(path):
            ap.error(f"{label} không phải thư mục: {path}")
    roots = [r.strip().strip("/") for r in args.roots.split(",") if r.strip()]
    exts = tuple(e.strip() if e.strip().startswith(".") else "." + e.strip()
                 for e in args.ext.split(",") if e.strip())
    if not roots or not exts:
        ap.error("--roots và --ext không được rỗng")

    source_guids = guid_index(args.source, exts)
    totals = {"missing": 0, "differ": 0, "guid": 0, "copyOnly": 0}
    lines = []
    for root in roots:
        na, nb, nboth, rows = compare_root(args.source, args.project, root, exts, source_guids)
        for k in totals:
            totals[k] += len(rows[k])
        lines.extend(render_root(root, na, nb, nboth, rows))

    summary = " · ".join(f"{k} {v}" for k, v in totals.items())
    if args.out:
        report = (f"# Contract diff\n\nsource `{args.source}` ↔ project `{args.project}` · ext `{' '.join(exts)}`\n\n"
                  + summary + "\n\n" + "\n".join(lines))
        with open(args.out, "w", encoding="utf-8") as fh:
            fh.write(report)
    print(summary)
    return 1 if totals["missing"] + totals["differ"] + totals["guid"] > 0 else 0


if __name__ == "__main__":
    sys.exit(main())
