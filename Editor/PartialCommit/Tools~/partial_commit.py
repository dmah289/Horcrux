"""Commit đúng các hunk đã chọn, không đụng index và working tree của người khác.

  python partial_commit.py list <path>...
      In các hunk (diff -U0 của working tree so với HEAD), đánh số từ 1 theo từng file.
  python partial_commit.py commit -m <message|@file> [--hunks <path>:<n,n-m>]... [--whole <path>]... [--patch <file>]...
      Dựng tree từ HEAD + các hunk/file đã chọn trong một index tạm, tạo commit, dời nhánh hiện tại lên commit đó.
      Index thật: path nào chưa ai stage (index == HEAD cũ) thì theo commit mới; path đã stage riêng thì giữ và cảnh báo.
      --patch: hunk sửa tay khi hai người sửa hai dòng liền nhau — chép hunk từ `git diff -U0 HEAD -- <path>`, chỉ giữ
      dòng của mình, sửa số dòng phía cũ và số đếm trong header; số phía mới tool tự đánh lại.

Đường dẫn tính từ gốc repo chứa thư mục hiện tại (submodule thì chạy trong submodule). --whole nhận cả file xoá và
con trỏ submodule (gitlink lấy HEAD của submodule).
"""
import os
import re
import subprocess
import sys
import tempfile


def git(args, cwd, env=None, data=None, check=True):
    result = subprocess.run(["git", *args], cwd=cwd, env=env, input=data, capture_output=True)
    if check and result.returncode != 0:
        sys.exit(f"git {' '.join(args)}: {result.stderr.decode('utf-8', 'replace').strip()}")
    return result.stdout


def split_hunks(root, path):
    diff = git(["diff", "--no-color", "--no-ext-diff", "-U0", "HEAD", "--", path], root)
    header, hunks = [], []
    for line in diff.splitlines(keepends=True):
        if line.startswith(b"@@"):
            hunks.append([line])
        elif hunks:
            hunks[-1].append(line)
        else:
            header.append(line)
    return b"".join(header), [b"".join(h) for h in hunks]


HUNK_HEADER = re.compile(rb"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@")


def renumber(hunks):
    """Đánh lại số dòng phía mới theo đúng các hunk sẽ áp. git apply --unidiff-zero đặt hunk chỉ-chèn theo số phía mới;
    số của diff đầy đủ đã cộng độ lệch của cả hunk bị bỏ qua, nên giữ nguyên thì dòng chèn vào sai chỗ, không báo lỗi."""
    out, delta = [], 0
    for hunk in hunks:
        first, rest = hunk.split(b"\n", 1)
        m = HUNK_HEADER.match(first)
        if not m:
            sys.exit(f"header hunk lạ: {first[:60]!r}")
        old_start = int(m.group(1))
        old_count = 1 if m.group(2) is None else int(m.group(2))
        new_count = 1 if m.group(4) is None else int(m.group(4))
        new_start = old_start + delta + (1 if old_count == 0 else 0)
        tail = first[m.end():]
        out.append(b"@@ -%d,%d +%d,%d @@%s\n%s" % (old_start, old_count, new_start, new_count, tail, rest))
        delta += new_count - old_count
    return out


def split_patch(patch):
    """Tách patch nhiều file thành (header, hunks) từng file."""
    files = []
    for line in patch.splitlines(keepends=True):
        if line.startswith(b"diff --git"):
            files.append([[line], []])
        elif line.startswith(b"@@"):
            files[-1][1].append(line)
        elif files and files[-1][1]:
            files[-1][1][-1] += line
        else:
            files[-1][0].append(line)
    return [(b"".join(h), hs) for h, hs in files]


def parse_picks(spec):
    picks = set()
    for part in spec.split(","):
        if "-" in part:
            lo, hi = part.split("-")
            picks.update(range(int(lo), int(hi) + 1))
        else:
            picks.add(int(part))
    return picks


def cmd_list(root, paths):
    for path in paths:
        _, hunks = split_hunks(root, path)
        print(f"== {path}: {len(hunks)} hunk")
        for i, hunk in enumerate(hunks, 1):
            lines = hunk.splitlines()
            changed = [l.decode("utf-8", "replace")[:110] for l in lines[1:] if l[:1] in (b"+", b"-")][:2]
            print(f"  {i:>3} {lines[0].decode('utf-8', 'replace').split('@@')[1].strip():<18} {' | '.join(changed)}")


def index_entry(root, env, path):
    out = git(["ls-files", "-s", "--", path], root, env=env).split(b"\n")[0]
    return out.split(b"\t")[0].split(b" ")[:2] if out else None


def tree_entry(root, rev, path):
    out = git(["ls-tree", rev, "--", path], root).split(b"\n")[0]
    if not out:
        return None
    mode, _, sha = out.split(b"\t")[0].split(b" ")
    return [mode, sha]


def cmd_commit(root, message, hunk_specs, wholes, patches):
    head = git(["rev-parse", "HEAD"], root).strip().decode()
    branch = git(["symbolic-ref", "-q", "--short", "HEAD"], root, check=False).strip().decode()
    fd, tmp_index = tempfile.mkstemp(prefix="partial_commit_index_")
    os.close(fd)
    env = {**os.environ, "GIT_INDEX_FILE": tmp_index}
    touched = []
    try:
        git(["read-tree", head], root, env=env)
        for spec in hunk_specs:
            path, numbers = spec.rsplit(":", 1)
            header, hunks = split_hunks(root, path)
            picks = parse_picks(numbers)
            missing = sorted(n for n in picks if not 1 <= n <= len(hunks))
            if missing:
                sys.exit(f"{path}: không có hunk {missing} (có {len(hunks)})")
            patch = header + b"".join(renumber([h for i, h in enumerate(hunks, 1) if i in picks]))
            git(["apply", "--cached", "--unidiff-zero", "-"], root, env=env, data=patch)
            touched.append(path)
        for patch_path in patches:
            patch = open(patch_path, "rb").read()
            patched = [line[6:].decode("utf-8") for line in patch.splitlines() if line.startswith(b"+++ b/")]
            # Hunk -U0 không có ngữ cảnh: áp chồng lên hunk khác của cùng file thì số dòng trôi, chèn sai chỗ im lặng.
            mixed = sorted(set(patched) & set(touched))
            if mixed:
                sys.exit(f"{mixed}: đã có trong --hunks hay patch trước; gom mọi hunk của file vào một patch")
            patch = b"".join(header + b"".join(renumber(hunks)) for header, hunks in split_patch(patch))
            git(["apply", "--cached", "--unidiff-zero", "-"], root, env=env, data=patch)
            touched += patched
        for path in wholes:
            full = os.path.join(root, path)
            if os.path.isdir(os.path.join(full, ".git")) or os.path.isfile(os.path.join(full, ".git")):
                sha = git(["rev-parse", "HEAD"], full).strip().decode()
                git(["update-index", "--add", "--cacheinfo", f"160000,{sha},{path}"], root, env=env)
            elif os.path.exists(full):
                git(["update-index", "--add", "--", path], root, env=env)
            else:
                git(["update-index", "--force-remove", "--", path], root, env=env)
            touched.append(path)
        tree = git(["write-tree"], root, env=env).strip().decode()
        if tree == git(["rev-parse", f"{head}^{{tree}}"], root).strip().decode():
            sys.exit("không có thay đổi nào được chọn")
        msg = open(message[1:], "rb").read() if message.startswith("@") else message.encode("utf-8")
        commit = git(["commit-tree", tree, "-p", head, "-F", "-"], root, data=msg).strip().decode()
    finally:
        os.remove(tmp_index)

    git(["update-ref", "-m", "partial_commit", "HEAD", commit, head], root)
    kept = []
    for path in dict.fromkeys(touched):
        if index_entry(root, None, path) != tree_entry(root, head, path):
            kept.append(path)
            continue
        new = tree_entry(root, commit, path)
        if new:
            # --add: file mới chưa track không có trong index thật, thiếu cờ thì git từ chối sau khi commit đã tạo.
            git(["update-index", "--add", "--cacheinfo", f"{new[0].decode()},{new[1].decode()},{path}"], root)
        else:
            git(["update-index", "--force-remove", "--", path], root)
    print(git(["log", "--stat", "--format=%h %s", "-1", commit], root).decode("utf-8", "replace").rstrip())
    if not branch:
        print("cảnh báo: HEAD đang detached — đưa nhánh lên commit này (git branch -f <nhánh> HEAD)")
    for path in kept:
        print(f"cảnh báo: {path} có phần đã stage riêng, index giữ nguyên — git status sẽ thấy chênh với HEAD mới")


def main(argv):
    if len(argv) < 2 or argv[1] not in ("list", "commit"):
        sys.exit(__doc__)
    root = git(["rev-parse", "--show-toplevel"], os.getcwd()).strip().decode()
    if argv[1] == "list":
        cmd_list(root, argv[2:])
        return
    message, hunk_specs, wholes, patches = None, [], [], []
    args = iter(argv[2:])
    for arg in args:
        if arg == "-m":
            message = next(args)
        elif arg == "--hunks":
            hunk_specs.append(next(args))
        elif arg == "--whole":
            wholes.append(next(args))
        elif arg == "--patch":
            patches.append(next(args))
        else:
            sys.exit(f"tham số lạ: {arg}")
    if not message or not (hunk_specs or wholes or patches):
        sys.exit(__doc__)
    cmd_commit(root, message, hunk_specs, wholes, patches)


if __name__ == "__main__":
    main(sys.argv)
