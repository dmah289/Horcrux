"""Nghiệm thu một lần đổi tên định danh: tên cũ còn sót trên YAML, diff .cs chỉ đổi tên, cây gốc + luật = cây mới.
Xem docs/Editor/RenameAudit.md.

  rename_audit.py scan   <map.tsv> [--root Assets] [--ext .unity,.prefab,...] [--also-cs]
  rename_audit.py tokens <map.tsv> --base <git rev> [--repo .] [--paths <glob,...>]
  rename_audit.py verify <map.tsv> --base <git rev> [--repo .] [--paths-map <paths.tsv>] [--exclude <glob,...>]

map.tsv: mỗi dòng `cũ<TAB>mới`, dòng bắt đầu bằng `#` là chú thích. So khớp nguyên từ (biên trên
[A-Za-z0-9_]), phân biệt hoa thường. Dòng `cũ<TAB>cũ` bảo vệ nguyên văn một cụm dài hơn khỏi luật ngắn hơn.
Mã thoát: 0 sạch · 1 còn hit / có vi phạm / có file lệch · 2 dùng sai hoặc git lỗi.
"""
import argparse
import bisect
import fnmatch
import hashlib
import os
import re
import subprocess
import sys
import threading

SCAN_EXTS = ".unity,.prefab,.asset,.anim,.controller,.spriteatlasv2,.mat"
WORD = "[A-Za-z0-9_]"


class Usage(Exception):
    """Lỗi dùng sai hoặc git lỗi: in ra stderr, thoát 2."""


# ---------------------------------------------------------------- bảng ánh xạ

def read_map(path):
    """Danh sách (cũ, mới) theo thứ tự file; cũ trùng nhau hoặc dòng thiếu TAB là dùng sai."""
    try:
        with open(path, encoding="utf-8") as fh:
            raw = fh.read().splitlines()
    except OSError as e:
        raise Usage(f"không đọc được bảng: {e}")
    rows, seen = [], set()
    for n, line in enumerate(raw, 1):
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        if "\t" not in line:
            raise Usage(f"{path}:{n}: thiếu TAB giữa cũ và mới")
        old, new = line.split("\t", 1)
        old, new = old.strip(), new.strip()
        if not old or not new:
            raise Usage(f"{path}:{n}: cũ hoặc mới rỗng")
        if old in seen:
            raise Usage(f"{path}:{n}: `{old}` xuất hiện hai lần")
        seen.add(old)
        rows.append((old, new))
    if not rows:
        raise Usage(f"{path}: bảng rỗng")
    return rows


def word_regex(olds, as_bytes=False):
    """Một regex cho mọi tên cũ, dài trước ngắn sau để cụm dài thắng cụm ngắn nằm trong nó."""
    alts = "|".join(re.escape(o) for o in sorted(olds, key=len, reverse=True))
    pat = f"(?<!{WORD})(?:{alts})(?!{WORD})"
    return re.compile(pat.encode("utf-8") if as_bytes else pat)


def substitute(regex, mapping, data):
    return regex.sub(lambda m: mapping[m.group(0)], data)


# ---------------------------------------------------------------- git

def git(repo, *args):
    try:
        p = subprocess.run(["git", "-C", repo, *args], capture_output=True, check=False)
    except FileNotFoundError:
        raise Usage("không tìm thấy git trên PATH")
    if p.returncode != 0:
        err = "\n".join(p.stderr.decode("utf-8", "replace").strip().splitlines()[:3])
        raise Usage(f"git {' '.join(args)}: {err}")
    return p.stdout


def iter_cat_file(repo, keys):
    """Yield (key, bytes|None) theo đúng thứ tự `keys` từ MỘT tiến trình `git cat-file --batch`.
    stdin ghi ở thread riêng: ghi hết rồi mới đọc sẽ kẹt khi pipe đầy."""
    keys = list(keys)
    proc = subprocess.Popen(["git", "-C", repo, "cat-file", "--batch"],
                            stdin=subprocess.PIPE, stdout=subprocess.PIPE)

    def feed():
        try:
            for k in keys:
                proc.stdin.write(k.encode("utf-8") + b"\n")
            proc.stdin.close()
        except BrokenPipeError:
            pass

    t = threading.Thread(target=feed, daemon=True)
    t.start()
    out = proc.stdout
    for k in keys:
        header = out.readline()
        if not header:
            raise Usage("git cat-file --batch dừng sớm")
        parts = header.rstrip(b"\n").split(b" ")
        if parts[-1] == b"missing":
            yield k, None
            continue
        data = out.read(int(parts[2]))
        out.read(1)
        yield k, data
    proc.wait()
    t.join()


def cat_file_dict(repo, keys):
    return dict(iter_cat_file(repo, keys))


def matches_any(path, globs):
    return any(fnmatch.fnmatchcase(path, g) for g in globs)


def split_globs(s):
    return [g.strip() for g in s.split(",") if g.strip()] if s else []


# ---------------------------------------------------------------- scan

def line_of(text, pos, starts):
    return bisect.bisect_right(starts, pos)


def cmd_scan(args):
    rows = read_map(args.map)
    exts = tuple(e if e.startswith(".") else "." + e for e in split_globs(args.ext))
    if args.also_cs:
        exts += (".cs", ".md")
    regex = word_regex([o for o, _ in rows])
    root = args.root.replace("\\", "/").rstrip("/")
    hits = files_hit = scanned = binaries = 0
    for d, dirs, fs in os.walk(root):
        dirs[:] = sorted(x for x in dirs if not x.endswith("~"))
        for f in sorted(fs):
            if not f.endswith(exts):
                continue
            path = os.path.join(d, f).replace("\\", "/").removeprefix("./")
            scanned += 1
            with open(path, "rb") as fh:
                b = fh.read()
            if b"\0" in b:
                binaries += 1
                print(f"binary: {path}", file=sys.stderr)
                continue
            text = b.decode("utf-8", errors="replace")
            starts = None
            found = False
            for m in regex.finditer(text):
                if starts is None:
                    starts = [i + 1 for i, c in enumerate(text) if c == "\n"]
                print(f"{path}:{line_of(text, m.start(), starts) + 1}: {m.group(0)}")
                hits += 1
                found = True
            files_hit += found
    print(f"scan: {hits} hit trong {files_hit} file · quét {scanned} file · bỏ {binaries} file nhị phân")
    return 1 if hits else 0


# ---------------------------------------------------------------- tokens

TOKEN_RE = re.compile(r"""
      (?P<ws>\s+)
    | (?P<comment>//[^\n]*|/\*.*?\*/)
    | (?P<directive>\#[^\n]*)
    | (?P<string>\$@"(?:[^"]|"")*"|@\$"(?:[^"]|"")*"|@"(?:[^"]|"")*"
                |\$"(?:[^"\\\n]|\\.)*"|"(?:[^"\\\n]|\\.)*")
    | (?P<char>'(?:[^'\\\n]|\\.)*')
    | (?P<number>\d[\w.]*(?:(?<=[eE])[+-]\d\w*)?)
    | (?P<ident>@?[^\W\d]\w*)
    | (?P<punct>.)
""", re.X | re.S)

TEXT_KINDS = ("string", "char", "comment", "directive")


def tokenize(data):
    """[(kind, text, line)] của một file C#; BOM bỏ, khoảng trắng không thành token."""
    text = data.decode("utf-8", errors="replace").lstrip("﻿")
    toks, line = [], 1
    for m in TOKEN_RE.finditer(text):
        kind = m.lastgroup
        if kind != "ws":
            toks.append((kind, m.group(0), line))
        line += m.group(0).count("\n")
    return toks


def diff_entries(repo, base):
    """[(status, old_path, new_path)] giữa cây `base` và working tree; R/C có hai đường dẫn."""
    raw = git(repo, "diff", "--name-status", "-M", "-z", base).split(b"\0")
    out, i = [], 0
    while i < len(raw) and raw[i]:
        status = raw[i].decode("utf-8", "replace")
        if status[0] in "RC":
            out.append((status[0], raw[i + 1].decode("utf-8", "replace"), raw[i + 2].decode("utf-8", "replace")))
            i += 3
        else:
            p = raw[i + 1].decode("utf-8", "replace")
            out.append((status[0], p, p))
            i += 2
    return out


def compare_tokens(path, old_toks, new_toks, mapping, renamed, changed_text, violations):
    if len(old_toks) != len(new_toks):
        k = next((i for i, (a, b) in enumerate(zip(old_toks, new_toks)) if a[1] != b[1]),
                 min(len(old_toks), len(new_toks)))
        a = old_toks[k] if k < len(old_toks) else ("", "<hết>", 0)
        b = new_toks[k] if k < len(new_toks) else ("", "<hết>", 0)
        violations.append(f"{path}:token#{k} (dòng {a[2]}→{b[2]}): {a[1]} → {b[1]}"
                          f" (số token khác: {len(old_toks)} → {len(new_toks)}, lệch đầu tiên tại đây)")
        return
    for k, ((ka, a, la), (kb, b, lb)) in enumerate(zip(old_toks, new_toks)):
        if ka != kb:
            violations.append(f"{path}:token#{k} (dòng {la}): {a} → {b} (đổi loại token {ka} → {kb})")
        elif a == b:
            continue
        elif ka == "ident":
            renamed.setdefault(a, set()).add(b)
            want = mapping.get(a)
            if want is None:
                violations.append(f"{path}:token#{k} (dòng {la}): {a} → {b} (không có trong bảng)")
            elif want != b:
                violations.append(f"{path}:token#{k} (dòng {la}): {a} → {b} (bảng nói {a} → {want})")
        elif ka in TEXT_KINDS:
            changed_text.append(f"{path}:{la}: {a} → {b}")
        else:
            violations.append(f"{path}:token#{k} (dòng {la}): {a} → {b} (đổi {ka})")


def cmd_tokens(args):
    mapping = dict(read_map(args.map))
    repo = args.repo
    globs = split_globs(args.paths)
    entries = [(s, o, n) for s, o, n in diff_entries(repo, args.base)
               if (o.endswith(".cs") or n.endswith(".cs")) and (not globs or matches_any(n, globs))]
    renamed, changed_text, violations = {}, [], []
    pairs = [(o, n) for s, o, n in entries if s not in "ACD"]
    added = {n for s, _, n in entries if s in "AC"}
    deleted = {o for s, o, _ in entries if s == "D"}
    # git -M ghép theo tương đồng dòng; file đổi tên có gần hết dòng đổi nên ghép thêm bằng bảng áp lên đường dẫn
    path_regex = word_regex(mapping)
    for old in sorted(deleted):
        guess = substitute(path_regex, mapping, old)
        if guess in added:
            pairs.append((old, guess))
            added.discard(guess)
    for new in sorted(added):
        violations.append(f"{new}: file thêm mới, không suy từ cây gốc")
    for old in sorted(deleted - {o for o, _ in pairs}):
        violations.append(f"{old}: file bị xoá khỏi cây gốc")
    blobs = cat_file_dict(repo, [f"{args.base}:{o}" for o, _ in pairs])
    for old, new in pairs:
        old_blob = blobs.get(f"{args.base}:{old}")
        if old_blob is None:
            violations.append(f"{old}: không có trong cây {args.base}")
            continue
        with open(os.path.join(repo, new), "rb") as fh:
            new_blob = fh.read()
        compare_tokens(new, tokenize(old_blob), tokenize(new_blob), mapping, renamed, changed_text, violations)
    for old, news in sorted(renamed.items()):
        if len(news) > 1:
            violations.append(f"{old} → {', '.join(sorted(news))} (một tên cũ về nhiều tên mới)")
    moved = sum(1 for o, n in pairs if o != n)
    print(f"files: {len(pairs)} (đổi đường dẫn {moved}) · identifiers renamed: {len(renamed)}"
          + (" (" + ", ".join(f"{o}→{'/'.join(sorted(n))}" for o, n in sorted(renamed.items())) + ")" if renamed else ""))
    print(f"strings/comments changed ({len(changed_text)}):")
    for line in changed_text:
        print("  " + line)
    print(f"violations ({len(violations)}):")
    for line in violations:
        print("  " + line)
    return 1 if violations else 0


# ---------------------------------------------------------------- verify

def ls_tree(repo, base):
    """([(sha, path)] blob, {path gitlink}) của cây `base`; symlink bỏ qua."""
    blobs, gitlinks = [], set()
    for rec in git(repo, "ls-tree", "-r", "-z", "--full-tree", base).split(b"\0"):
        if not rec:
            continue
        meta, path = rec.split(b"\t", 1)
        mode, kind, sha = meta.split(b" ")
        path = path.decode("utf-8", "replace")
        if kind == b"blob" and mode != b"120000":
            blobs.append((sha.decode(), path))
        elif kind == b"commit":
            gitlinks.add(path)
    return blobs, gitlinks


def blob_sha(data):
    """sha1 git gán cho blob có nội dung `data`."""
    h = hashlib.sha1(b"blob %d\0" % len(data))
    h.update(data)
    return h.hexdigest()


def worktree_files(repo):
    raw = git(repo, "ls-files", "-z", "--cached", "--others", "--exclude-standard", "--full-name")
    return [p.decode("utf-8", "replace") for p in raw.split(b"\0") if p]


def cmd_verify(args):
    rows = read_map(args.map)
    mapping = {o.encode("utf-8"): n.encode("utf-8") for o, n in rows}
    regex = word_regex([o for o, _ in rows], as_bytes=True)
    path_rows = read_map(args.paths_map) if args.paths_map else []
    path_map = dict(path_rows)
    path_regex = word_regex([o for o, _ in path_rows]) if path_rows else None
    excludes = split_globs(args.exclude)
    repo = args.repo

    blobs, gitlinks = ls_tree(repo, args.base)
    kept = [(sha, p) for sha, p in blobs if not matches_any(p, excludes)]
    expected, origin, pending = set(), {}, []
    n = {"differ": 0, "missing": 0, "unexpected": 0, "collision": 0}
    # Lượt 1: băm file working tree như git băm blob; trùng sha (thô hoặc sau khi quy CRLF về LF) là không đổi,
    # không cần lấy blob. Lượt 2 chỉ lấy blob của file lệch sha — thường là vài chục trên vạn.
    for sha, path in kept:
        new_path = substitute(path_regex, path_map, path) if path_regex else path
        if new_path in origin:
            n["collision"] += 1
            print(f"collision: {origin[new_path]} và {path} cùng về {new_path}")
        origin[new_path] = path
        expected.add(new_path)
        wt_path = os.path.join(repo, new_path)
        if not os.path.isfile(wt_path):
            n["missing"] += 1
            print(f"missing: {new_path}" + (f" (gốc {path})" if new_path != path else ""))
            continue
        with open(wt_path, "rb") as fh:
            actual = fh.read()
        is_text = b"\0" not in actual
        untouched = blob_sha(actual) == sha or (
            is_text and b"\r\n" in actual and blob_sha(actual.replace(b"\r\n", b"\n")) == sha)
        if untouched:
            # working == gốc: sạch khi luật không đổi nội dung; luật đổi mà file y nguyên là tên cũ còn sót
            if is_text and substitute(regex, mapping, actual) != actual:
                n["differ"] += 1
                print(f"differ: {new_path} (tên cũ còn nguyên)")
            continue
        pending.append((sha, path, new_path, actual))
    for (sha, path, new_path, actual), (_, blob) in zip(pending, iter_cat_file(repo, (p[0] for p in pending))):
        if b"\0" in blob:
            same = blob == actual
        else:
            same = substitute(regex, mapping, blob).replace(b"\r\n", b"\n") == actual.replace(b"\r\n", b"\n")
        if not same:
            n["differ"] += 1
            print(f"differ: {new_path}" + (f" (gốc {path})" if new_path != path else ""))
    for p in worktree_files(repo):
        if p in expected or p in gitlinks or matches_any(p, excludes) or not os.path.isfile(os.path.join(repo, p)):
            continue
        n["unexpected"] += 1
        print(f"unexpected: {p}")
    print(f"verify: cây {len(blobs)} blob · bỏ {len(blobs) - len(kept)} theo --exclude · lấy blob {len(pending)}"
          f" · differ {n['differ']} · missing {n['missing']} · unexpected {n['unexpected']} · collision {n['collision']}")
    return 1 if n["differ"] + n["missing"] + n["unexpected"] + n["collision"] else 0


# ---------------------------------------------------------------- main

def build_parser():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    sub = ap.add_subparsers(dest="cmd", required=True)

    s = sub.add_parser("scan", help="quét tên cũ còn sót trên mọi file YAML dưới root")
    s.add_argument("map")
    s.add_argument("--root", default="Assets")
    s.add_argument("--ext", default=SCAN_EXTS, help=f"mặc định {SCAN_EXTS}")
    s.add_argument("--also-cs", action="store_true", help="quét thêm .cs và .md")
    s.set_defaults(run=cmd_scan)

    t = sub.add_parser("tokens", help="so token C# giữa cây gốc và working tree, chỉ cho phép đổi theo bảng")
    t.add_argument("map")
    t.add_argument("--base", required=True, help="git rev của cây gốc")
    t.add_argument("--repo", default=".")
    t.add_argument("--paths", help="glob trên đường dẫn mới, cách nhau bằng dấu phẩy")
    t.set_defaults(run=cmd_tokens)

    v = sub.add_parser("verify", help="áp bảng lên mọi blob của cây gốc rồi so từng byte với working tree")
    v.add_argument("map")
    v.add_argument("--base", required=True, help="git rev của cây gốc")
    v.add_argument("--repo", default=".")
    v.add_argument("--paths-map", help="tsv `cũ<TAB>mới` áp lên đường dẫn, cùng luật nguyên từ")
    v.add_argument("--exclude", help="glob bỏ qua ở cả hai cây, cách nhau bằng dấu phẩy; `*` khớp cả `/`")
    v.set_defaults(run=cmd_verify)
    return ap


def main(argv=None):
    args = build_parser().parse_args(argv)
    try:
        return args.run(args)
    except Usage as e:
        print(f"lỗi: {e}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
