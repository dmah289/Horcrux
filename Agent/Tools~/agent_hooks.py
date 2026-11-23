"""Hook Claude Code canh luật của bộ Agent/ bằng máy, thay vì kỷ luật của agent. Một lệnh cho mọi sự kiện; đọc JSON
của hook trên stdin, rẽ theo `hook_event_name`. Cài: `Agent/SETUP.md` › Hook.

    PreToolUse  Bash|PowerShell  chặn `git add -A|-u|.`, `git commit -a`, commit không pathspec (`-- <paths>`) ngoài lúc
                                 merge; commit vào `Agent/` mà `check_agent.py` báo lỗi ở chính file được commit.
    PreToolUse  Edit|Write       chặn sửa mục developer-only của `horcrux.md`.
    PostToolUse Edit|Write       ghi file `.cs` agent vừa sửa vào `Library/HorcruxVerify/agent-edits/<session>.txt`.
    Stop                         không cho dừng khi có `.cs` agent sửa sau lần `verify.sh` OK cuối
                                 (`Library/HorcruxVerify/last-ok`); lần chặn thứ hai trong cùng lượt dừng thì để qua.

Hook hỏng (ngoại lệ) thì im lặng để qua: một hook lỗi không được chặn việc của developer.
"""
import importlib.util
import json
import os
import pathlib
import re
import subprocess
import sys

TOOLS = pathlib.Path(__file__).resolve().parent
AGENT = TOOLS.parent

# -I bỏ thư mục script khỏi sys.path: nạp check_agent theo đường dẫn để dấu developer-only chỉ định nghĩa một nơi.
# Không ghi __pycache__ cạnh tool trong repo.
sys.dont_write_bytecode = True
_spec = importlib.util.spec_from_file_location("check_agent", TOOLS / "check_agent.py")
check_agent = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(check_agent)


def project_dir(data):
    env = os.environ.get("CLAUDE_PROJECT_DIR")
    if env:
        return pathlib.Path(env)
    return AGENT.parent.parent.parent  # <project>/Assets/Horcrux/Agent


def state_dir(data):
    return project_dir(data) / "Library" / "HorcruxVerify"


def deny(reason):
    print(json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse", "permissionDecision": "deny",
                                             "permissionDecisionReason": reason}}, ensure_ascii=False))


# ---------- lệnh shell ----------

HEREDOC = re.compile(r"<<-?[ \t]*(['\"]?)(\w+)\1.*?^[ \t]*\2[ \t]*$", re.S | re.M)
PS_HERE = re.compile(r"@(['\"])\r?\n.*?\r?\n\1@", re.S)
QUOTED = re.compile(r'"(?:\\.|[^"\\])*"|\'[^\']*\'')
SEP = re.compile(r"&&|\|\||[;|&\n]")
GIT_OPT_VALUE = {"-C", "-c", "--git-dir", "--work-tree", "--namespace"}


def segments(command):
    """Tách lệnh thành các đoạn token; thân heredoc bỏ đi, chuỗi trong nháy giữ nguyên thành một token."""
    text = PS_HERE.sub(" H ", HEREDOC.sub(" H ", command))
    quoted = []

    def keep(m):
        quoted.append(m.group(0)[1:-1])
        return f"\x00{len(quoted) - 1}\x00"

    text = QUOTED.sub(keep, text)
    restore = lambda tok: re.sub(r"\x00(\d+)\x00", lambda m: quoted[int(m.group(1))], tok)
    return [[restore(t) for t in seg.split()] for seg in SEP.split(text) if seg.strip()]


def git_calls(command, cwd):
    """(thư mục chạy, lệnh con, tham số) cho mỗi lần gọi git; theo `cd`, `Set-Location`, `-C`."""
    here = pathlib.Path(cwd)
    for toks in segments(command):
        if not toks:
            continue
        if toks[0] in ("cd", "Set-Location", "pushd") and len(toks) > 1:
            here = (here / toks[-1]).resolve()
            continue
        if pathlib.PurePath(toks[0]).name.lower() not in ("git", "git.exe"):
            continue
        i, where = 1, here
        while i < len(toks) and toks[i].startswith("-"):
            if toks[i] == "-C" and i + 1 < len(toks):
                where = (where / toks[i + 1]).resolve()
            i += 2 if toks[i] in GIT_OPT_VALUE else 1
        if i < len(toks):
            yield where, toks[i], toks[i + 1:]


def git(where, *args):
    r = subprocess.run(["git", "-C", str(where), *args], capture_output=True)
    return r.stdout.decode("utf-8").strip() if r.returncode == 0 else None


def in_merge(where):
    for name in ("MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD"):
        p = git(where, "rev-parse", "--git-path", name)
        if p and (pathlib.Path(where) / p).exists():
            return True
    return False


def short_flags(args, letter):
    return any(re.fullmatch(r"-[a-zA-Z]+", a) and letter in a[1:] for a in args)


def agent_errors(top, paths):
    """Dòng lỗi của check_agent.py thuộc chính các file sắp commit dưới Agent/."""
    rels = []
    for p in paths:
        try:
            rels.append(p.resolve().relative_to(top / "Agent").as_posix())
        except ValueError:
            continue
    if not rels:
        return []
    r = subprocess.run([sys.executable, "-X", "utf8", "-I", str(top / "Agent" / "Tools~" / "check_agent.py"),
                        "all", "--base", "HEAD"], capture_output=True, cwd=top)
    keys = set(rels)
    keys |= {parts[1] for parts in (rel.split("/") for rel in rels) if len(parts) > 2 and parts[0] in ("Rules", "Skills")}
    out = []
    for line in r.stdout.decode("utf-8").replace("\\", "/").splitlines():
        error = "VƯỢT TRẦN" in line or "ĐÃ BỊ ĐỔI" in line or "THIẾU DẤU" in line or re.match(
            r"(KHÔNG CÓ|THIẾU|TRA CỨU TRỎ HỤT|CÒN BẪY|SAI DÒNG)", line)
        if not error:
            continue
        if "developer-only" in line:
            if "horcrux.md" in keys:
                out.append(line.strip())
        elif any(re.search(rf"(^|[\s/]){re.escape(k)}($|[\s/:])", line) for k in keys):
            out.append(line.strip())
    return out


def pre_shell(data):
    command = data.get("tool_input", {}).get("command", "")
    if "git" not in command:
        return
    for where, sub, args in git_calls(command, data.get("cwd") or os.getcwd()):
        if sub == "add":
            bad = [a for a in args if a in ("-A", "--all", "-u", "--update", ".", "*", ":/") or
                   short_flags([a], "A") or short_flags([a], "u")]
            if bad:
                return deny(f"`git add {' '.join(bad)}` gom cả file developer đang sửa. Add từng file: "
                            "`git add -- <file mới>` (workflow › Git).")
        if sub != "commit":
            continue
        if "--all" in args or short_flags([a for a in args if not a.startswith("--")], "a"):
            return deny("`git commit -a` lấy mọi file đã sửa. Commit theo pathspec: `git commit -m … -- <paths>`.")
        if "--" in args:
            paths = [where / p for p in args[args.index("--") + 1:]]
        elif any(a.startswith("--pathspec-from-file") for a in args) or in_merge(where):
            continue
        else:
            return deny("Commit không pathspec lấy cả index, kể cả file developer đã stage. Ghi rõ phần của việc: "
                        "`git commit -m … -- <paths>` (workflow › Git).")
        top = git(where, "rev-parse", "--show-toplevel")
        if top and (pathlib.Path(top) / "Agent" / "horcrux.md").exists():
            errs = agent_errors(pathlib.Path(top), paths)
            if errs:
                return deny("check_agent.py báo lỗi ở file sắp commit — sửa trước khi commit:\n" + "\n".join(errs))


# ---------- sửa file ----------

def is_horcrux_md(path):
    p = pathlib.Path(path)
    return p.name == "horcrux.md" and p.parent.name == "Agent" and (p.parent / "Rules").is_dir()


def pre_edit(data):
    ti = data.get("tool_input", {})
    path = ti.get("file_path", "")
    if not is_horcrux_md(path):
        return
    old = pathlib.Path(path).read_text(encoding="utf-8").replace("\r\n", "\n")
    if data.get("tool_name") == "Write":
        new = ti.get("content", "")
    else:
        edits = ti.get("edits") or [ti]
        new = old
        for e in edits:
            a, b = e.get("old_string", "").replace("\r\n", "\n"), e.get("new_string", "").replace("\r\n", "\n")
            if not a or a not in new:
                return  # Edit sẽ tự báo không khớp
            new = new.replace(a, b) if e.get("replace_all") else new.replace(a, b, 1)
    if check_agent.dev_block(old) != check_agent.dev_block(new.replace("\r\n", "\n")):
        deny("Mục developer-only của horcrux.md chỉ developer gõ. Thấy lỗi ở đó thì báo trong chat, không sửa.")


def post_edit(data):
    path = data.get("tool_input", {}).get("file_path", "")
    if not path.endswith(".cs"):
        return
    log = state_dir(data) / "agent-edits" / f"{data.get('session_id', 'unknown')}.txt"
    log.parent.mkdir(parents=True, exist_ok=True)
    seen = set(log.read_text(encoding="utf-8").splitlines()) if log.exists() else set()
    if path not in seen:
        with log.open("a", encoding="utf-8") as f:
            f.write(path + "\n")


# ---------- dừng lượt ----------

def compiled(path):
    """File trong thư mục đuôi `~` (Tools~, Samples~) Unity không import, không compile."""
    return not any(part.endswith("~") for part in pathlib.PurePath(path).parts)


def stop(data):
    if data.get("stop_hook_active"):
        return
    log = state_dir(data) / "agent-edits" / f"{data.get('session_id', 'unknown')}.txt"
    if not log.exists():
        return
    ok = state_dir(data) / "last-ok"
    ok_time = ok.stat().st_mtime if ok.exists() else 0
    stale = [p for p in log.read_text(encoding="utf-8").splitlines()
             if compiled(p) and os.path.exists(p) and os.path.getmtime(p) > ok_time]
    if stale:
        names = ", ".join(pathlib.Path(p).name for p in stale[:5]) + (" …" if len(stale) > 5 else "")
        print(json.dumps({"decision": "block", "reason":
                          f"Có {len(stale)} file .cs sửa sau lần verify.sh OK cuối ({names}). Chạy compile-check "
                          "(`verify.sh` theo lệnh của dự án) và dán dòng kết quả vào báo cáo; không chạy được "
                          "(Editor đóng, thiếu builder) thì nói rõ là chưa kiểm."}, ensure_ascii=False))


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    try:
        data = json.loads(sys.stdin.buffer.read().decode("utf-8") or "{}")
        event, tool = data.get("hook_event_name"), data.get("tool_name")
        if event == "PreToolUse" and tool in ("Bash", "PowerShell"):
            pre_shell(data)
        elif event == "PreToolUse" and tool in ("Edit", "Write", "MultiEdit"):
            pre_edit(data)
        elif event == "PostToolUse" and tool in ("Edit", "Write", "MultiEdit"):
            post_edit(data)
        elif event == "Stop":
            stop(data)
    except Exception as e:  # hook hỏng không chặn việc
        print(f"agent_hooks: {type(e).__name__}: {e}", file=sys.stderr)


if __name__ == "__main__":
    main()
