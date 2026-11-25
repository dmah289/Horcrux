"""Hook Claude Code canh luật của bộ Agent/ bằng máy, thay vì kỷ luật của agent, và lệnh cài chúng vào dự án.

    python -X utf8 -I agent_hooks.py install [--check]

`install` đưa dự án về khớp Horcrux: file mồi `.claude/skills/<việc>/SKILL.md` cho mỗi `Agent/Skills/<việc>` (tạo,
cập nhật description, xoá file mồi của skill đã bỏ) và hook dưới đây trong `.claude/settings.json` (giữ hook khác).
`--check` chỉ in phần lệch, mã thoát 1 khi lệch. Gốc dự án: `CLAUDE_PROJECT_DIR`, không có thì tính từ chỗ đặt Agent/.

Không tham số thì là hook: đọc JSON trên stdin, rẽ theo `hook_event_name`.

    SessionStart startup         lệch như `install --check` thì báo agent và developer chạy `install`.
    SessionStart compact         nhắc agent đọc lại trọn luật nền: sau compact chỉ còn bản tóm tắt.
    PreToolUse  Bash|PowerShell  chặn `git add -A|-u|.`, `git commit -a`, commit không pathspec (`-- <paths>`) ngoài lúc
                                 merge; commit vào `Agent/` mà `check_agent.py` báo lỗi ở chính file được commit.
    PreToolUse  Edit|Write       chặn sửa mục developer-only của `horcrux.md`.
    PostToolUse Edit|Write       ghi file `.cs` agent vừa sửa vào `Library/HorcruxVerify/agent-edits/<session>.txt`.
    Stop                         không cho dừng khi có `.cs` agent sửa sau lần `verify.sh` xanh cuối (OK, hoặc chỉ còn fail có sẵn)
                                 (`Library/HorcruxVerify/last-ok`); lần chặn thứ hai trong cùng lượt dừng thì để qua.

Mỗi lần chặn hay nhắc ghi một dòng vào `Library/HorcruxVerify/hook-log.jsonl` (luật: git-add-all, git-commit-all,
commit-no-pathspec, agent-check, dev-only, verify-stale, install-drift, compact-reread); `context_audit.py hooks` đếm.

Hook hỏng (ngoại lệ) thì im lặng để qua: một hook lỗi không được chặn việc của developer.
"""
import importlib.util
import json
import os
import pathlib
import re
import subprocess
import sys
import time

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


def log_event(data, rule, detail=""):
    """Một dòng mỗi lần hook chặn hay nhắc, để `context_audit.py hooks` đếm luật nào bị vấp."""
    try:
        log = state_dir(data) / "hook-log.jsonl"
        log.parent.mkdir(parents=True, exist_ok=True)
        with log.open("a", encoding="utf-8") as f:
            f.write(json.dumps({"t": time.strftime("%Y-%m-%dT%H:%M:%S"), "session": data.get("session_id", ""),
                                "event": data.get("hook_event_name", ""), "rule": rule,
                                "detail": " ".join(detail.split())[:200]}, ensure_ascii=False) + "\n")
    except OSError:
        pass


def deny(data, rule, detail, reason):
    log_event(data, rule, detail)
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
                return deny(data, "git-add-all", command,
                            f"`git add {' '.join(bad)}` gom cả file developer đang sửa. Add từng file: "
                            "`git add -- <file mới>` (workflow › Git).")
        if sub != "commit":
            continue
        if "--all" in args or short_flags([a for a in args if not a.startswith("--")], "a"):
            return deny(data, "git-commit-all", command,
                        "`git commit -a` lấy mọi file đã sửa. Commit theo pathspec: `git commit -m … -- <paths>`.")
        if "--" in args:
            paths = [where / p for p in args[args.index("--") + 1:]]
        elif any(a.startswith("--pathspec-from-file") for a in args) or in_merge(where):
            continue
        else:
            return deny(data, "commit-no-pathspec", command,
                        "Commit không pathspec lấy cả index, kể cả file developer đã stage. Ghi rõ phần của việc: "
                        "`git commit -m … -- <paths>` (workflow › Git).")
        top = git(where, "rev-parse", "--show-toplevel")
        if top and (pathlib.Path(top) / "Agent" / "horcrux.md").exists():
            errs = agent_errors(pathlib.Path(top), paths)
            if errs:
                return deny(data, "agent-check", "; ".join(errs),
                            "check_agent.py báo lỗi ở file sắp commit — sửa trước khi commit:\n" + "\n".join(errs))


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
        deny(data, "dev-only", path,
             "Mục developer-only của horcrux.md chỉ developer gõ. Thấy lỗi ở đó thì báo trong chat, không sửa.")


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
        log_event(data, "verify-stale", " ".join(stale))
        names = ", ".join(pathlib.Path(p).name for p in stale[:5]) + (" …" if len(stale) > 5 else "")
        print(json.dumps({"decision": "block", "reason":
                          f"Có {len(stale)} file .cs sửa sau lần verify.sh xanh cuối ({names}). Chạy compile-check "
                          "(`verify.sh` theo lệnh của dự án) và dán dòng kết quả vào báo cáo; không chạy được "
                          "(Editor đóng, thiếu builder) thì nói rõ là chưa kiểm."}, ensure_ascii=False))


# ---------- cài vào dự án ----------

HOOKS = [  # (sự kiện, matcher, timeout giây); mọi sự kiện gọi cùng một lệnh
    ("SessionStart", "startup|compact", 10),
    ("PreToolUse", "Bash|PowerShell|Edit|Write", 30),
    ("PostToolUse", "Edit|Write", 10),
    ("Stop", None, 10),
]
STUB_MARK = "Bản gốc nằm trong Horcrux; file này chỉ để Claude Code tìm thấy"


def agent_rel(project):
    return pathlib.Path(os.path.relpath(AGENT, project)).as_posix()


def stub_text(project, name, desc):
    a = agent_rel(project)
    return (f"---\nname: {name}\ndescription: {desc}\n---\n\n"
            f"Đọc toàn bộ `{a}/Skills/{name}/SKILL.md`, `{a}/Memory/{name}.md` và\n"
            f"`docs/agent-memory/{name}.md` (nếu có) rồi làm theo. {STUB_MARK}\nskill, không chép nội dung.\n")


def merge_hooks(settings, command):
    hooks = settings.setdefault("hooks", {})
    for event, matcher, timeout in HOOKS:
        groups = hooks.setdefault(event, [])
        want = {"type": "command", "command": command, "timeout": timeout}
        mine = [(g, h) for g in groups for h in g.get("hooks", []) if "agent_hooks.py" in h.get("command", "")]
        if not mine:
            groups.append({"matcher": matcher, "hooks": [want]} if matcher else {"hooks": [want]})
            continue
        group, hook = mine[0]
        hook.clear()
        hook.update(want)
        if matcher:
            group["matcher"] = matcher
        else:
            group.pop("matcher", None)
    return settings


def plan(project):
    """[(việc, đường dẫn, nội dung hoặc None khi xoá)] để dự án khớp Horcrux."""
    acts = []
    names = set()
    for skill in sorted((AGENT / "Skills").glob("*/SKILL.md")):
        name = skill.parent.name
        names.add(name)
        desc = re.search(r"^description: (.*)$", skill.read_text(encoding="utf-8"), re.M).group(1)
        want = stub_text(project, name, desc)
        stub = project / ".claude" / "skills" / name / "SKILL.md"
        if not stub.exists():
            acts.append(("tạo", stub, want))
        elif stub.read_text(encoding="utf-8").replace("\r\n", "\n") != want:
            acts.append(("cập nhật", stub, want))
    for stub in sorted((project / ".claude" / "skills").glob("*/SKILL.md")):
        if stub.parent.name not in names and STUB_MARK in stub.read_text(encoding="utf-8"):
            acts.append(("xoá", stub, None))

    path = project / ".claude" / "settings.json"
    raw = path.read_bytes().decode("utf-8") if path.exists() else ""  # read_text nuốt \r\n, mất kiểu xuống dòng gốc
    old = json.loads(raw) if raw.strip() else {}
    command = f'python -X utf8 -I "$CLAUDE_PROJECT_DIR/{agent_rel(project)}/Tools~/agent_hooks.py"'
    new = merge_hooks(json.loads(json.dumps(old)), command)
    if new != old:
        text = json.dumps(new, indent=2, ensure_ascii=False) + "\n"
        acts.append(("ghi hook", path, text.replace("\n", "\r\n") if "\r\n" in raw else text))
    return acts


def install(check):
    project = project_dir({})
    acts = plan(project)
    for verb, path, _ in acts:
        print(f"{verb:9} {path.relative_to(project).as_posix()}")
    if check:
        print("lệch:", len(acts))
        return 1 if acts else 0
    for verb, path, text in acts:
        if text is None:
            path.unlink()
            if not any(path.parent.iterdir()):
                path.parent.rmdir()
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(text.encode("utf-8"))
    print("đã cài:", len(acts), "thay đổi" if acts else "(đã khớp)")
    return 0


def reread_list(project):
    a = agent_rel(project)
    files = [f"{a}/horcrux.md"]
    for name in check_agent.ALWAYS:
        files += [f"{a}/Rules/{name}/RULE.md", f"{a}/Memory/{name}.md", f"docs/agent-memory/{name}.md"]
    return [f for f in files if (project / f).exists()]


def session_start(data):
    project = project_dir(data)
    if data.get("source") == "compact":
        # Luật đọc bằng Read chỉ còn bản tóm tắt sau compact; CLAUDE.md thì được nạp lại nguyên văn.
        log_event(data, "compact-reread")
        msg = ("Phiên vừa compact: nội dung luật đọc đầu phiên chỉ còn bản tóm tắt. Trước bước tiếp, đọc lại trọn "
               + ", ".join(f"`{f}`" for f in reread_list(project))
               + ", và rule, skill, memory hàng bảng chỉ dẫn của việc đang làm.")
        print(json.dumps({"hookSpecificOutput": {"hookEventName": "SessionStart", "additionalContext": msg}},
                         ensure_ascii=False))
        return
    acts = plan(project)
    if acts:
        log_event(data, "install-drift", ", ".join(f"{v} {p.name}" for v, p, _ in acts))
        what = ", ".join(f"{verb} {path.relative_to(project).as_posix()}" for verb, path, _ in acts[:4])
        msg = (f"Bộ Agent của dự án lệch Horcrux ({len(acts)} mục: {what}{' …' if len(acts) > 4 else ''}). Chạy "
               f"`python -X utf8 -I {agent_rel(project)}/Tools~/agent_hooks.py install` rồi commit phần đổi.")
        print(json.dumps({"systemMessage": msg, "hookSpecificOutput": {
            "hookEventName": "SessionStart", "additionalContext": msg}}, ensure_ascii=False))


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    if sys.argv[1:2] == ["install"]:
        sys.exit(install("--check" in sys.argv))
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
        elif event == "SessionStart":
            session_start(data)
    except Exception as e:  # hook hỏng không chặn việc
        print(f"agent_hooks: {type(e).__name__}: {e}", file=sys.stderr)


if __name__ == "__main__":
    main()
