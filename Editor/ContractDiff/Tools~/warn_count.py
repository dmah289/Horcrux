"""Đếm warning/error compiler không trùng trong log Unity. Xem Editor/ContractDiff/ContractDiff.md.

  warn_count.py [--log <Editor.log>] [--own <root1,root2>] [-o out.txt]

Mặc định đọc %LOCALAPPDATA%/Unity/Editor/Editor.log (log của Editor đang mở). --own là các thư mục con
ngay dưới Assets/ tính là code của mình; không có thì bỏ mục own.
Mã thoát: 0 không có error · 1 có error · 2 dùng sai.
"""
import argparse
import collections
import os
import re
import sys

DIAG_RE = re.compile(r"(Assets[\\/][^\n(]+?)\((\d+),(\d+)\): (warning|error) (CS\d+)")


def unique_diagnostics(log_text):
    """Tập (file, dòng, loại, mã) không trùng, sắp theo file rồi dòng; Unity ghi lặp mỗi lần compile."""
    return sorted({(m.group(1).replace("\\", "/"), int(m.group(2)), m.group(4), m.group(5))
                   for m in DIAG_RE.finditer(log_text)})


def root_of(path):
    parts = path.split("/")
    return parts[1] if len(parts) > 1 else ""


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--log", default=os.path.join(os.environ.get("LOCALAPPDATA", ""), "Unity", "Editor", "Editor.log"))
    ap.add_argument("--own", help="thư mục con dưới Assets/ là code của mình, cách nhau bằng dấu phẩy")
    ap.add_argument("-o", "--out", help="ghi danh sách không trùng vào file này, một dòng một mục")
    args = ap.parse_args(argv)
    if not os.path.isfile(args.log):
        ap.error(f"không thấy log: {args.log}")

    with open(args.log, encoding="utf-8", errors="replace") as fh:
        u = unique_diagnostics(fh.read())
    if args.out:
        with open(args.out, "w", encoding="utf-8") as fh:
            fh.write("\n".join(f"{f}({l}) {k} {c}" for f, l, k, c in u))

    print("unique:", len(u))
    print("by code:", collections.Counter(c for *_, c in u).most_common())
    print("by root:", collections.Counter(root_of(f) for f, *_ in u).most_common())
    if args.own:
        own_roots = tuple(x.strip() for x in args.own.split(",") if x.strip())
        own = [x for x in u if root_of(x[0]) in own_roots]
        print(f"own({','.join(own_roots)}):", collections.Counter(c for *_, c in own).most_common())
        for f, l, k, c in own:
            print("  ", f"{f}({l}) {k} {c}")
    errors = [x for x in u if x[2] == "error"]
    print("errors:", errors[:20])
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
