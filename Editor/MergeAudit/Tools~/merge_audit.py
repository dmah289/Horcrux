"""Kiểm một lần merge nhánh Unity: trước merge tìm chỗ git sẽ ghép sai, sau merge chứng minh không mất dòng, không gãy asset.
Xem docs/Editor/MergeAudit.md.

  merge_audit.py plan   <ours> <theirs> [--repo .] [--min-renames 20] [--top 30]
  merge_audit.py lines  [<merge>] [--repo .] [--ext .cs,.md,...] [--top 60] [--samples 3]
  merge_audit.py assets [<merge>] [--repo .] [--all] [--groups <dir>] [--top 60]
  merge_audit.py replay <merge>... | --range <a>..<b> [--repo .] [--find-renames <n>]

<merge> mặc định HEAD, phải là commit hai cha. Mã thoát: 0 sạch · 1 có phát hiện · 2 dùng sai hoặc git lỗi.
"""
import argparse
import collections
import os
import posixpath
import re
import subprocess
import sys

TEXT_EXTS = ".cs,.md,.prefab,.unity,.asset,.asmdef,.asmref,.json,.mat,.controller,.anim,.shader,.uss,.uxml,.txt"
YAML_GLOBS = ["*.prefab", "*.unity", "*.asset", "*.mat", "*.controller", "*.overrideController", "*.anim",
              "*.playable", "*.spriteatlas", "*.spriteatlasv2", "*.mask", "*.physicsMaterial2D", "*.preset",
              "*.lighting", "*.signal", "*.terrainlayer"]
GROUPS_DIR = "Assets/AddressableAssetsData/AssetGroups"
# Thư mục Unity coi là một asset: file bên trong không có .meta riêng.
BUNDLE_DIR_SUFFIXES = (".androidlib", ".framework", ".xcframework", ".bundle", ".plugin")
TRIVIAL = re.compile(r"^[{}()\[\];,]*$")
GUID_LINE = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
AA_ENTRY = re.compile(r"m_GUID: ([0-9a-f]{32})\s*\n\s*m_Address: (.*)")


class Usage(Exception):
    """Lỗi dùng sai hoặc git lỗi: in ra stderr, thoát 2."""


# ---------------------------------------------------------------- git

def git_rc(repo, *args):
    try:
        p = subprocess.run(["git", "-C", repo, *args], capture_output=True, check=False)
    except FileNotFoundError:
        raise Usage("không tìm thấy git trên PATH")
    return p.returncode, p.stdout.decode("utf-8", "replace"), p.stderr.decode("utf-8", "replace")


def git(repo, *args, ok=(0,)):
    rc, out, err = git_rc(repo, *args)
    if rc not in ok:
        raise Usage(f"git {' '.join(args)}: " + " ".join(err.strip().splitlines()[:3]))
    return out


def resolve(repo, rev):
    return git(repo, "rev-parse", "--verify", f"{rev}^{{commit}}").strip()


def short(repo, rev):
    return git(repo, "log", "-1", "--format=%h %s", rev).strip()


def parents_of(repo, rev):
    ids = git(repo, "rev-list", "--parents", "-n1", rev).split()
    return ids[1:]


def merge_parents(repo, rev):
    merge = resolve(repo, rev)
    ps = parents_of(repo, merge)
    if len(ps) != 2:
        raise Usage(f"{rev} có {len(ps)} cha; cần merge commit hai cha")
    return merge, ps[0], ps[1], git(repo, "merge-base", ps[0], ps[1]).strip()


class Blobs:
    """Đọc nhiều blob qua một tiến trình `git cat-file --batch`."""

    def __init__(self, repo):
        self._p = subprocess.Popen(["git", "-C", repo, "cat-file", "--batch"], stdin=subprocess.PIPE,
                                   stdout=subprocess.PIPE)

    def raw(self, rev, path):
        self._p.stdin.write(f"{rev}:{path}\n".encode("utf-8"))
        self._p.stdin.flush()
        # Đường dẫn có dấu cách: "<rev>:<path> missing" cũng tách ra ba mảnh, nên nhận dạng theo đuôi.
        header = self._p.stdout.readline().rstrip(b"\n")
        if header.endswith((b" missing", b" ambiguous")):
            return None
        parts = header.split()
        if len(parts) != 3:
            raise Usage(f"git cat-file trả về dòng lạ: {header[:200]!r}")
        data = self._p.stdout.read(int(parts[2]))
        self._p.stdout.read(1)
        return data if parts[1] == b"blob" else None

    def text(self, rev, path):
        data = self.raw(rev, path)
        if data is None or b"\0" in data[:8000]:
            return None
        return data.decode("utf-8", "replace")

    def guid(self, rev, meta_path):
        text = self.text(rev, meta_path)
        m = GUID_LINE.search(text) if text else None
        return m.group(1) if m else None

    def close(self):
        self._p.stdin.close()
        self._p.wait()


def tree_entries(repo, rev):
    """(file, dir) của rev; submodule tính là thư mục."""
    files, dirs = set(), set()
    for line in git(repo, "ls-tree", "-r", "-t", rev).splitlines():
        meta, _, path = line.partition("\t")
        kind = meta.split()[1]
        (files if kind == "blob" else dirs).add(path)
    return files, dirs


def guid_paths(repo, rev):
    """GUID → các đường dẫn asset (bỏ đuôi .meta) ở rev, kể cả submodule ở đúng commit rev trỏ tới."""
    out = git(repo, "grep", "-I", "-e", "^guid: ", rev, "--", "*.meta", ok=(0, 1))
    res, prefix = collections.defaultdict(list), rev + ":"
    for line in out.splitlines():
        if line.startswith(prefix):
            path, _, g = line[len(prefix):].rpartition(":guid: ")
            res[g.strip()].append(path[:-5])
    # Asset dự án trỏ script của submodule (framework) suốt; bỏ submodule thì mọi tham chiếu đó thành "gãy" giả.
    for line in git(repo, "ls-tree", "-r", rev).splitlines():
        meta, _, path = line.partition("\t")
        mode, kind, sha = meta.split()
        if kind != "commit":
            continue
        sub = os.path.join(repo, path)
        if git_rc(sub, "cat-file", "-e", f"{sha}^{{commit}}")[0] != 0:
            print(f"[merge_audit]: submodule {path} không có commit {sha[:8]}; tham chiếu sang nó bị báo gãy", file=sys.stderr)
            continue
        for g, paths in guid_paths(sub, sha).items():
            res[g].extend(f"{path}/{p}" for p in paths)
    return res


def name_status(repo, a, b, *opts):
    rows = []
    for line in git(repo, "diff", "--name-status", *opts, a, b).splitlines():
        cols = line.split("\t")
        rows.append((cols[0], cols[1:]))
    return rows


def norm_lines(text):
    return [l.strip() for l in (text or "").splitlines()]


def print_capped(rows, top, indent="  "):
    for r in rows[:top]:
        print(indent + r)
    if len(rows) > top:
        print(f"{indent}… còn {len(rows) - top} mục (--top)")


# ---------------------------------------------------------------- plan

def restructure_commits(repo, base, side, min_renames):
    """Commit (không phải merge) trên base..side có ít nhất min_renames rename nguyên vẹn, cũ trước mới sau."""
    out = git(repo, "log", "--reverse", "--no-merges", "--format=%x00%H %s", "-M100%", "--name-status",
              "--diff-filter=R", f"{base}..{side}")
    found = []
    for chunk in out.split("\0")[1:]:
        lines = chunk.strip().splitlines()
        renames = sum(1 for l in lines[1:] if l.startswith("R"))
        if renames >= min_renames:
            found.append((lines[0], renames))
    return found


def moved_dirs(repo, base, side, side_dirs):
    """Thư mục cũ → thư mục mới theo đa số rename nguyên vẹn, chỉ thư mục đã biến mất ở side."""
    votes = collections.defaultdict(collections.Counter)
    for st, paths in name_status(repo, base, side, "-M100%"):
        if st.startswith("R"):
            od, nd = posixpath.dirname(paths[0]), posixpath.dirname(paths[1])
            if od != nd:
                votes[od][nd] += 1
    return {od: c.most_common(1)[0][0] for od, c in votes.items() if od not in side_dirs}


def cmd_plan(a):
    ours, theirs = resolve(a.repo, a.ours), resolve(a.repo, a.theirs)
    base = git(a.repo, "merge-base", ours, theirs).strip()
    blobs = Blobs(a.repo)
    findings = 0
    print(f"base   {short(a.repo, base)}\nours   {short(a.repo, ours)}\ntheirs {short(a.repo, theirs)}")
    sides = {"ours": ours, "theirs": theirs}
    for label, side in sides.items():
        other_label = "theirs" if label == "ours" else "ours"
        other = sides[other_label]
        print(f"\n=== {label}: commit dời thư mục (≥ {a.min_renames} rename nguyên vẹn)")
        commits = restructure_commits(a.repo, base, side, a.min_renames)
        for head, n in commits:
            print(f"  {head[:9]} {n:5} rename  {head[41:]}")
        if commits:
            stages = []
            for head, _ in commits:
                stages += [head[:9] + "^", head[:9]]
            print(f"  bậc merge {label} vào {other_label}: " + " → ".join(stages + [label]))
        else:
            print("  không có")

        other_modified = {p[0] for st, p in name_status(a.repo, base, other, "--no-renames") if st == "M"}
        pairs = [(st, p) for st, p in name_status(a.repo, base, side, "-M") if st.startswith("R")]
        asset_pair = {old: new for st, (old, new) in pairs if not old.endswith(".meta")}
        risky = []
        for st, (old, new) in pairs:
            score = int(st[1:] or 100)
            is_meta = old.endswith(".meta")
            g_old = blobs.guid(base, old if is_meta else old + ".meta")
            g_new = blobs.guid(side, new if is_meta else new + ".meta")
            if g_old and g_new and g_old != g_new:
                if old in other_modified:
                    risky.append((0, f"[GHÉP SAI GUID] {old} -> {new}"))
            elif is_meta and g_old and old[:-5] in other_modified and asset_pair.get(old[:-5]) != new[:-5]:
                where = asset_pair.get(old[:-5], "git coi là xoá + thêm mới (dưới ngưỡng giống)")
                risky.append((0, f"[ASSET KHÔNG THEO META] {old[:-5]} -> {new[:-5]}; git ghép: {where}"))
            elif not g_old and score < 100 and old in other_modified:
                # File ngoài Unity (không .meta): không có GUID để xác nhận cặp ghép theo độ giống.
                risky.append((1, f"[giống {score}%, không GUID] {old} -> {new}"))
        risky.sort()
        print(f"\n=== {label}: rename git ghép (ngưỡng mặc định) sai hoặc chưa xác nhận được, mà {other_label} có "
              f"sửa file cũ — merge thường đổ thay đổi của {other_label} vào sai file")
        print_capped([r for _, r in risky], a.top)
        if not risky:
            print("  không có")
        findings += sum(1 for k, _ in risky if k == 0)

        side_files, side_dirs = tree_entries(a.repo, side)
        moves = moved_dirs(a.repo, base, side, side_dirs)
        stray = []
        for st, paths in name_status(a.repo, base, other, "--no-renames"):
            if st != "A":
                continue
            d = posixpath.dirname(paths[0])
            while d and d not in moves:
                d = posixpath.dirname(d)
            if d:
                stray.append(f"{paths[0]} -> {moves[d]}{paths[0][len(d):]}")
        print(f"\n=== {other_label}: file mới nằm trong thư mục {label} đã dời — merge với rename nguyên vẹn để "
              f"chúng ở thư mục cũ, cần git mv")
        print_capped(stray, a.top)
        if not stray:
            print("  không có")
        findings += len(stray)
    blobs.close()

    print("\n=== conflict dự kiến (git merge-tree, không ghi gì vào repo)")
    for label, opts in (("rename mặc định", []), ("find-renames=100%", ["-Xfind-renames=100%"])):
        rc, out, err = git_rc(a.repo, "merge-tree", "--write-tree", "--name-only", *opts, ours, theirs)
        if rc not in (0, 1):
            raise Usage("git merge-tree: " + err.strip())
        conflicted = out.split("\n\n", 1)[0].splitlines()[1:]
        print(f"  {label}: {len(conflicted)} file conflict")
        if opts:
            print_capped(conflicted, a.top, "    ")
    return 1 if findings else 0


# ---------------------------------------------------------------- lines

def cmd_lines(a):
    merge, p1, p2, base = merge_parents(a.repo, a.merge)
    exts = tuple(e.strip() for e in a.ext.split(",") if e.strip())
    merge_files, _ = tree_entries(a.repo, merge)
    gm = {g: ps[0] for g, ps in guid_paths(a.repo, merge).items()}
    gb = {g: ps[0] for g, ps in guid_paths(a.repo, base).items()}
    blobs = Blobs(a.repo)
    findings = 0
    print(f"merge {short(a.repo, merge)}\nbase  {short(a.repo, base)}")
    for label, side in (("cha 1", p1), ("cha 2", p2)):
        rows = []
        for st, paths in name_status(a.repo, base, side, "--no-renames"):
            path = paths[0]
            if st not in ("A", "M") or not path.endswith(exts):
                continue
            g = blobs.guid(side, path + ".meta")
            target = gm.get(g) if g in gm else (path if path in merge_files else None)
            if target is None:
                rows.append((10 ** 9, f"KHÔNG CÒN trong merge   {path}"))
                continue
            side_lines = norm_lines(blobs.text(side, path))
            kept = collections.Counter(norm_lines(blobs.text(merge, target)))
            base_lines = collections.Counter(norm_lines(blobs.text(base, gb.get(g, path) if g else path)))
            lost = [l for l in dict.fromkeys(side_lines)
                    if l and not TRIVIAL.match(l) and kept[l] == 0 and base_lines[l] == 0]
            if lost:
                moved = "" if target == path else f" -> {target}"
                body = "\n".join(f"      | {l[:150]}" for l in lost[:a.samples])
                rows.append((len(lost), f"{len(lost):4} dòng mất   {path}{moved}\n{body}"))
        rows.sort(key=lambda r: -r[0])
        print(f"\n=== {label} {short(a.repo, side)}: {len(rows)} file có dòng {label} thêm mà merge không còn")
        print_capped([r for _, r in rows], a.top)
        findings += len(rows)
    blobs.close()
    return 1 if findings else 0


# ---------------------------------------------------------------- assets

def package_guids(repo):
    """GUID của package trong Library/PackageCache: tham chiếu sang package không phải tham chiếu gãy."""
    found = set()
    cache = os.path.join(repo, "Library", "PackageCache")
    for root, _, files in os.walk(cache):
        for f in files:
            if f.endswith(".meta"):
                try:
                    with open(os.path.join(root, f), encoding="utf-8", errors="replace") as fh:
                        m = GUID_LINE.search(fh.read(400))
                except OSError:
                    continue
                if m:
                    found.add(m.group(1))
    return found


def meta_issues(files, dirs):
    """Asset dưới Assets/ thiếu .meta và .meta không có asset."""
    issues = set()

    def unity_sees(path):
        parts = path.split("/")
        for part in parts[:-1]:
            if part.endswith("~") or part.startswith(".") or part.endswith(BUNDLE_DIR_SUFFIXES):
                return False
        return not parts[-1].startswith(".") and not parts[-1].endswith("~")

    for f in files:
        if not f.startswith("Assets/") or not unity_sees(f):
            continue
        if f.endswith(".meta"):
            if f[:-5] not in files and f[:-5] not in dirs:
                issues.add(f"meta mồ côi      {f}")
        elif f + ".meta" not in files:
            issues.add(f"thiếu meta       {f}")
    for d in dirs:
        if d.startswith("Assets/") and unity_sees(d + "/x") and d + ".meta" not in files:
            issues.add(f"thư mục thiếu meta {d}")
    return issues


def dangling_refs(repo, rev, guids, extra):
    out = git(repo, "grep", "-I", "-o", "-E", "guid: [0-9a-f]{32}", rev, "--", *YAML_GLOBS, ok=(0, 1))
    res, prefix = set(), rev + ":"
    for line in out.splitlines():
        path, _, g = line[len(prefix):].rpartition(":guid: ")
        if g not in guids and g not in extra and not g.startswith("0000000000000000"):
            res.add((path, g))
    return res


def aa_entries(repo, rev, blobs, groups_dir):
    out = git(repo, "ls-tree", "-r", "--name-only", rev, "--", groups_dir)
    res = {}
    for path in out.splitlines():
        if path.endswith(".asset"):
            for g, addr in AA_ENTRY.findall(blobs.text(rev, path) or ""):
                res[g] = f"{posixpath.basename(path)[:-6]}: {addr.strip()}"
    return res


def cmd_assets(a):
    merge, p1, p2, base = merge_parents(a.repo, a.merge)
    revs = {"merge": merge, "cha 1": p1, "cha 2": p2, "base": base}
    print(f"merge {short(a.repo, merge)}\ncha 1 {short(a.repo, p1)}\ncha 2 {short(a.repo, p2)}")
    G = {k: guid_paths(a.repo, r) for k, r in revs.items()}
    T = {k: tree_entries(a.repo, r) for k, r in revs.items()}
    extra = package_guids(a.repo)
    findings = 0

    def owner(in1, in2):
        """Nhãn cha mang sẵn lỗi: lỗi chỉ ở cha 2 là mới với nhánh đích dù không mới do merge."""
        return "[cả hai]" if in1 and in2 else "[cha 1]" if in1 else "[cha 2]"

    def section(title, new, old):
        nonlocal findings
        print(f"\n=== {title}: {len(new)} mới do merge · {len(old)} có sẵn ở một cha")
        print_capped(sorted(new), a.top)
        if a.all and old:
            print("  -- có sẵn:")
            print_capped(sorted(old), a.top)
        findings += len(new)

    dup = {k: {g for g, ps in G[k].items() if len(ps) > 1} for k in ("merge", "cha 1", "cha 2")}
    rows_new = [f"{g} {G['merge'][g]}" for g in dup["merge"] - dup["cha 1"] - dup["cha 2"]]
    rows_old = [f"{owner(g in dup['cha 1'], g in dup['cha 2'])} {g} {G['merge'][g]}"
                for g in dup["merge"] & (dup["cha 1"] | dup["cha 2"])]
    section("GUID trùng", rows_new, rows_old)

    mi = {k: meta_issues(*T[k]) for k in ("merge", "cha 1", "cha 2")}
    section(".meta mồ côi, asset thiếu .meta", mi["merge"] - mi["cha 1"] - mi["cha 2"],
            {f"{owner(x in mi['cha 1'], x in mi['cha 2'])} {x}" for x in mi["merge"] & (mi["cha 1"] | mi["cha 2"])})

    dang = {k: dangling_refs(a.repo, revs[k], G[k], extra) for k in ("merge", "cha 1", "cha 2")}
    parent_pairs = dang["cha 1"] | dang["cha 2"]
    parent_guids = {g for _, g in parent_pairs}
    new, old = [], []
    for path, g in dang["merge"]:
        was = next((f" (từng là {G[k][g][0]} ở {k})" for k in ("cha 1", "cha 2", "base") if g in G[k]), "")
        row = f"{path} -> {g}{was}"
        if (path, g) in parent_pairs or g in parent_guids:
            in1 = (path, g) in dang["cha 1"] or g in {x for _, x in dang["cha 1"]}
            in2 = (path, g) in dang["cha 2"] or g in {x for _, x in dang["cha 2"]}
            old.append(f"{owner(in1, in2)} {row}")
        else:
            new.append(row)
    section("tham chiếu GUID gãy trong YAML", new, old)

    _, merge_dirs = T["merge"]
    _, base_dirs = T["base"]
    rows = []
    for k in ("cha 1", "cha 2"):
        gone = (base_dirs - T[k][1]) & merge_dirs
        for d in sorted(gone):
            if posixpath.dirname(d) not in gone:
                n = sum(1 for f in T["merge"][0] if f.startswith(d + "/"))
                rows.append(f"{d} ({n} file) — {k} đã dời hoặc xoá")
    section("thư mục một cha đã bỏ mà sống lại trong merge", rows, [])

    blobs = Blobs(a.repo)
    E = {k: aa_entries(a.repo, r, blobs, a.groups) for k, r in revs.items()}
    blobs.close()
    dang_new = [f"{E['merge'][g]} -> {g}" for g in E["merge"] if g not in G["merge"]
                and not (g in E["cha 1"] and g not in G["cha 1"]) and not (g in E["cha 2"] and g not in G["cha 2"])]
    lost, removed = [], []
    for g in (E["cha 1"].keys() | E["cha 2"].keys()) - E["merge"].keys():
        if g not in G["merge"]:
            continue
        entry = E["cha 1"].get(g) or E["cha 2"].get(g)
        if g in E["base"] and (g not in E["cha 1"] or g not in E["cha 2"]):
            who = "cha 1" if g not in E["cha 1"] else "cha 2"
            removed.append(f"{entry} — {who} gỡ")
        else:
            lost.append(f"{entry} -> {G['merge'][g][0]}")
    section("entry Addressables trỏ GUID không còn", dang_new, [])
    section("entry Addressables một cha thêm mà merge không còn (asset vẫn còn)", lost, [])
    if removed:
        print("  -- gỡ chủ ý theo merge ba chiều (một cha gỡ, cha kia giữ như base), soát lại ý định:")
        print_capped(sorted(removed), a.top, "     ")
    return 1 if findings else 0


# ---------------------------------------------------------------- replay

def cmd_replay(a):
    revs = list(a.merges)
    if a.range:
        revs += git(a.repo, "rev-list", "--merges", "--reverse", a.range).split()
    if not revs:
        raise Usage("cần ít nhất một merge commit hoặc --range")
    opts = [f"-Xfind-renames={a.find_renames}%"] if a.find_renames else []
    findings = 0
    for rev in revs:
        merge = resolve(a.repo, rev)
        ps = parents_of(a.repo, merge)
        print(f"\n=== {short(a.repo, merge)}")
        if len(ps) != 2:
            print(f"  bỏ qua: {len(ps)} cha")
            continue
        rc, out, err = git_rc(a.repo, "merge-tree", "--write-tree", "--name-only", *opts, ps[0], ps[1])
        if rc not in (0, 1):
            raise Usage("git merge-tree: " + err.strip())
        head, _, _ = out.partition("\n\n")
        lines = head.splitlines()
        tree, conflicted = lines[0], set(lines[1:])
        diff = git(a.repo, "diff", "--numstat", "--no-renames", tree, merge).splitlines()
        hand = [d for d in diff if d.split("\t")[-1] not in conflicted]
        resolved = [d for d in diff if d.split("\t")[-1] in conflicted]
        print(f"  merge tự động: {len(conflicted)} file conflict · merge thật khác ở {len(diff)} file")
        if resolved:
            print("  giải conflict (thêm, bớt, file):")
            print_capped(resolved, a.top, "    ")
        if hand:
            print("  SỬA NGOÀI CONFLICT (thêm, bớt, file):")
            print_capped(hand, a.top, "    ")
        findings += len(hand)
    return 1 if findings else 0


# ---------------------------------------------------------------- cli

def build_parser():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    sub = ap.add_subparsers(dest="cmd", required=True)

    p = sub.add_parser("plan", help="trước merge: commit dời thư mục, rename ghép sai, file mới lạc thư mục, conflict")
    p.add_argument("ours")
    p.add_argument("theirs")
    p.add_argument("--min-renames", type=int, default=20, help="ngưỡng coi một commit là commit dời thư mục")
    p.set_defaults(run=cmd_plan)

    l = sub.add_parser("lines", help="sau merge: dòng mỗi cha thêm (so base) không còn trong merge")
    l.add_argument("merge", nargs="?", default="HEAD")
    l.add_argument("--ext", default=TEXT_EXTS, help=f"mặc định {TEXT_EXTS}")
    l.add_argument("--samples", type=int, default=3, help="số dòng mẫu mỗi file")
    l.set_defaults(run=cmd_lines)

    s = sub.add_parser("assets", help="sau merge: GUID trùng, meta, tham chiếu gãy, thư mục sống lại, Addressables")
    s.add_argument("merge", nargs="?", default="HEAD")
    s.add_argument("--all", action="store_true", help="in cả lỗi có sẵn ở một cha")
    s.add_argument("--groups", default=GROUPS_DIR, help=f"thư mục group Addressables, mặc định {GROUPS_DIR}")
    s.set_defaults(run=cmd_assets)

    r = sub.add_parser("replay", help="merge commit có sửa gì ngoài kết quả merge tự động")
    r.add_argument("merges", nargs="*")
    r.add_argument("--range", help="mọi merge commit trong a..b")
    r.add_argument("--find-renames", type=int, help="ngưỡng rename khi dựng lại, mặc định như git merge thường")
    r.set_defaults(run=cmd_replay)

    for sp in (p, l, s, r):
        sp.add_argument("--repo", default=".")
        sp.add_argument("--top", type=int, default=40, help="số dòng tối đa mỗi mục")
    return ap


def main(argv=None):
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    args = build_parser().parse_args(argv)
    try:
        return args.run(args)
    except Usage as e:
        print(f"lỗi: {e}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
