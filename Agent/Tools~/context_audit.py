"""Đo ngữ cảnh bộ Agent/ nạp cho agent: theo bảng chỉ dẫn (lý thuyết) và theo transcript Claude Code (thực đọc).

    python -X utf8 -I context_audit.py plan
    python -X utf8 -I context_audit.py sessions [--last N] [--transcripts <dir>]
    python -X utf8 -I context_audit.py files    [--last N] [--transcripts <dir>]
    python -X utf8 -I context_audit.py hooks    [--days N]

plan      mỗi hàng bảng chỉ dẫn: nền (mọi phiên đọc) + Bắt buộc, và khi chạm hết cột Thêm khi chạm. Hàng trỏ một mục
          (`tên` › Mục) tính trọn file: cận trên.
sessions  mỗi phiên: KB và token luật đã đọc so với đỉnh context · file nền chưa đọc trọn trước lần sửa file đầu ·
          số lần compact và nền có được đọc lại sau đó không · KB subagent tự đọc luật (`workflow` › Subagent: orchestrator
          bơm ngữ cảnh, subagent không tự đi đọc).
files     mỗi file luật, skill, memory: số phiên đọc, tổng KB. Nặng mà ít phiên cần → ứng viên tách sang tra cứu; không
          phiên nào đọc → bảng chỉ dẫn trỏ hụt, hoặc file thừa.
hooks     số lần hook chặn theo luật, từ `Library/HorcruxVerify/hook-log.jsonl` (`agent_hooks.py` ghi).

Token = byte ÷ tỉ lệ byte/token đo trên chính transcript: lượt chỉ đọc một file .md ≥ 8 KB, lấy phần context tăng ở lượt
sau so với byte kết quả. Dưới 3 mẫu thì dùng 2.3 (tài liệu tiếng Việt của Horcrux, 2026-11). Ước lượng, lệch cỡ ±10%.
Transcript: `~/.claude/projects/<đường dẫn dự án, ký tự ngoài chữ-số thành '-'>/<phiên>.jsonl`, subagent ở
`<phiên>/subagents/`. Gốc dự án: `CLAUDE_PROJECT_DIR`, không có thì tính từ chỗ đặt Agent/.
"""
import argparse
import collections
import importlib.util
import json
import os
import pathlib
import re
import statistics
import sys
import time

TOOLS = pathlib.Path(__file__).resolve().parent
AGENT = TOOLS.parent
sys.dont_write_bytecode = True
_spec = importlib.util.spec_from_file_location("check_agent", TOOLS / "check_agent.py")
check_agent = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(check_agent)

DEFAULT_RATIO = 2.3
AGENT_FILE = re.compile(r"/Agent/((?:horcrux|SETUP)\.md|(?:Rules|Skills)/[\w-]+/[\w-]+\.md|Memory/[\w-]+\.md)$")
PROJECT_MEMORY = re.compile(r"/docs/agent-memory/([\w-]+)\.md$")
SHELL_READ = re.compile(r"^\s*(cat|head|tail|sed|less|more|type|Get-Content|gc)\b")
SHELL_PATH = re.compile(r"((?:Agent/)(?:horcrux\.md|(?:Rules|Skills)/[\w-]+/[\w-]+\.md|Memory/[\w-]+\.md)"
                        r"|docs/agent-memory/[\w-]+\.md)")
EDIT_TOOLS = {"Edit", "Write", "MultiEdit", "NotebookEdit"}


def project_dir():
    env = os.environ.get("CLAUDE_PROJECT_DIR")
    return pathlib.Path(env) if env else AGENT.parent.parent.parent  # <project>/Assets/Horcrux/Agent


def size(p):
    return p.stat().st_size if p.exists() else 0


def unit_files(name, project):
    """File một rule hay skill kéo theo khi đọc: file chính, memory chung, memory dự án."""
    return [check_agent.main_file(name), AGENT / "Memory" / f"{name}.md", project / "docs" / "agent-memory" / f"{name}.md"]


def base_files(project):
    files = [AGENT / "horcrux.md"]
    for name in check_agent.ALWAYS:
        files += unit_files(name, project)
    return [f for f in files if f.exists()]


def key_of(path):
    """Khoá chung cho file luật dù đọc từ checkout nào (worktree, máy khác): `Rules/x/RULE.md`, `proj:Memory/x.md`."""
    p = path.replace("\\", "/")
    m = AGENT_FILE.search(p)
    if m:
        return m.group(1)
    m = PROJECT_MEMORY.search(p)
    return f"proj:Memory/{m.group(1)}.md" if m else None


def key_path(key, project):
    return project / "docs" / "agent-memory" / key[12:] if key.startswith("proj:") else AGENT / key


def file_key(f, project):
    return key_of(f.resolve().as_posix())


def tok(nbytes, ratio):
    return f"{nbytes / ratio / 1000:.1f}k tok"


def kb(nbytes):
    return f"{nbytes / 1024:.1f} KB"


# ---------- plan: theo bảng chỉ dẫn ----------

def routing_rows():
    text = (AGENT / "horcrux.md").read_text(encoding="utf-8")
    block = text.split("## Bảng chỉ dẫn", 1)[1].split("\n## ", 1)[0]
    known = set(check_agent.RULES) | set(check_agent.SKILLS)
    for line in block.splitlines():
        cols = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cols) != 3 or cols[0] in ("Loại việc", "---") or set(cols[0]) <= set("-"):
            continue
        names = lambda col: [n for n in re.findall(r"`([a-z-]+)`", col) if n in known]
        yield cols[0], names(cols[1]), names(cols[2])


def cmd_plan(args):
    project = project_dir()
    ratio = calibrate(transcript_files(args.transcripts, project, 30))[0]
    base = base_files(project)
    claude_md = project / "CLAUDE.md"
    nb = sum(size(f) for f in base) + size(claude_md)
    print(f"tỉ lệ {ratio:.2f} byte/token")
    print(f"nền {kb(nb)} ≈ {tok(nb, ratio)}: CLAUDE.md {kb(size(claude_md))} · " +
          " · ".join(f"{file_key(f, project)} {kb(size(f))}" for f in base))
    print(f"\n{'loại việc':52} {'bắt buộc':>22} {'chạm hết':>22}")
    seen_base = {f.resolve() for f in base}
    for task, must, extra in routing_rows():
        if task.startswith("Mọi tác vụ") or not must:
            continue
        def load(names):
            fs = {f.resolve() for n in names for f in unit_files(n, project) if f.exists()} - seen_base
            return sum(size(f) for f in fs)
        m, e = nb + load(must), nb + load(must + extra)
        label = task if len(task) <= 50 else task[:49] + "…"
        print(f"{label:52} {kb(m):>9} ≈{tok(m, ratio):>11} {kb(e):>9} ≈{tok(e, ratio):>11}")
    return 0


# ---------- transcript ----------

def transcript_dir(project):
    return pathlib.Path.home() / ".claude" / "projects" / re.sub(r"[^A-Za-z0-9]", "-", str(project.resolve()))


def transcript_files(override, project, last):
    d = pathlib.Path(override) if override else transcript_dir(project)
    files = sorted(d.glob("*.jsonl"), key=lambda p: p.stat().st_mtime, reverse=True)
    return files[:last]


def result_text(c):
    cc = c.get("content")
    if isinstance(cc, str):
        return cc
    return "".join(x.get("text", "") for x in cc or [] if isinstance(x, dict))


def total_in(u):
    return u.get("input_tokens", 0) + u.get("cache_creation_input_tokens", 0) + u.get("cache_read_input_tokens", 0)


def parse(path):
    """Một transcript → các sự kiện theo thứ tự: ('read', key, bytes, trọn?), ('edit',), ('compact',); kèm tiêu đề,
    ngày, đỉnh context, và mẫu hiệu chỉnh token."""
    events, title, date, peak = [], "", "", 0
    msgs, pending, results = [], {}, {}
    order = []  # (kiểu, id) theo thứ tự xuất hiện; read chờ tool_result để biết byte
    with path.open(encoding="utf-8") as f:
        for line in f:
            if not any(s in line for s in ('"assistant"', "tool_result", "compact_boundary", "custom-title")):
                continue
            try:
                d = json.loads(line)
            except ValueError:
                continue
            t = d.get("type")
            if t == "custom-title":
                title = d.get("customTitle", title)
            elif t == "system" and d.get("subtype") == "compact_boundary":
                order.append(("compact", None))
            elif t == "assistant":
                date = date or d.get("timestamp", "")[:10]
                m = d["message"]
                u = m.get("usage") or {}
                peak = max(peak, total_in(u))
                if msgs and msgs[-1]["id"] == m.get("id"):
                    msgs[-1]["content"] += m.get("content", [])
                else:
                    msgs.append({"id": m.get("id"), "usage": u, "content": list(m.get("content", []))})
                for c in m.get("content", []):
                    if c.get("type") != "tool_use":
                        continue
                    name, ti = c.get("name"), c.get("input") or {}
                    if name == "Read":
                        k = key_of(ti.get("file_path", ""))
                        whole = "offset" not in ti and "limit" not in ti
                        pending[c["id"]] = ("read", k, whole, ti.get("file_path", ""))
                        order.append(("tool", c["id"]))
                    elif name in ("Bash", "PowerShell") and SHELL_READ.search(ti.get("command", "")):
                        keys = [key_of("/" + p) for p in SHELL_PATH.findall(ti.get("command", ""))]
                        if keys:
                            pending[c["id"]] = ("shell", keys, True, "")
                            order.append(("tool", c["id"]))
                    elif name in EDIT_TOOLS:
                        order.append(("edit", None))
            elif t == "user" and isinstance(d.get("message", {}).get("content"), list):
                for c in d["message"]["content"]:
                    if c.get("type") == "tool_result":
                        results[c.get("tool_use_id")] = len(result_text(c).encode("utf-8"))
    for kind, tid in order:
        if kind != "tool":
            events.append((kind,))
            continue
        what, k, whole, _ = pending[tid]
        n = results.get(tid, 0)
        for key in (k if what == "shell" else [k]):
            if key:
                events.append(("read", key, n // (len(k) if what == "shell" else 1), whole))
    samples = []
    for a, b in zip(msgs, msgs[1:]):
        tus = [c for c in a["content"] if c.get("type") == "tool_use"]
        if len(tus) != 1 or tus[0].get("name") != "Read" or not a["usage"] or not b["usage"]:
            continue
        if not tus[0]["input"].get("file_path", "").endswith(".md"):
            continue
        n = results.get(tus[0]["id"], 0)
        out = a["usage"].get("output_tokens", 0) - a["usage"].get("output_tokens_details", {}).get("thinking_tokens", 0)
        delta = total_in(b["usage"]) - total_in(a["usage"]) - out
        if n >= 8000 and delta > 0:
            samples.append(n / delta)
    return {"title": title, "date": date, "peak": peak, "events": events, "samples": samples}


def calibrate(files):
    samples = []
    for f in files:
        samples += parse(f)["samples"]
    return (statistics.median(samples) if len(samples) >= 3 else DEFAULT_RATIO), len(samples)


def subagent_reads(session):
    total = 0
    for f in (session.with_suffix("") / "subagents").glob("*.jsonl"):
        total += sum(e[2] for e in parse(f)["events"] if e[0] == "read")
    return total


def audit(info, base_keys):
    reads = [e for e in info["events"] if e[0] == "read"]
    first_edit = next((i for i, e in enumerate(info["events"]) if e[0] == "edit"), len(info["events"]))
    before = {e[1] for e in info["events"][:first_edit] if e[0] == "read" and e[3]}
    compacts = [i for i, e in enumerate(info["events"]) if e[0] == "compact"]
    reread = 0
    for i in compacts:
        after = {e[1] for e in info["events"][i + 1:] if e[0] == "read" and e[3]}
        reread += base_keys <= after
    return {"bytes": sum(e[2] for e in reads), "files": {e[1] for e in reads},
            "missing": sorted(base_keys - before) if first_edit < len(info["events"]) else None,
            "compacts": len(compacts), "reread": reread}


def cmd_sessions(args):
    project = project_dir()
    files = transcript_files(args.transcripts, project, args.last)
    if not files:
        print("không có transcript ở", transcript_dir(project))
        return 1
    ratio, n = calibrate(files)
    base_keys = {file_key(f, project) for f in base_files(project)}
    print(f"tỉ lệ {ratio:.2f} byte/token ({n} mẫu{'' if n >= 3 else ', dùng mặc định'}) · nền: {len(base_keys)} file")
    for f in files:
        info = parse(f)
        a = audit(info, base_keys)
        sub = subagent_reads(f)
        title = (info["title"] or f.stem[:8])[:40]
        share = f" / đỉnh context {info['peak'] / 1000:.0f}k" if info["peak"] else ""
        if a["missing"] is None:
            base = "không sửa file"
        else:
            base = ("nền đủ" if not a["missing"] else "chưa đọc nền" if len(a["missing"]) == len(base_keys)
                    else "thiếu " + ", ".join(a["missing"]))
            base += " trước lần sửa đầu"
        comp = f" · compact {a['compacts']}, đọc lại nền {a['reread']}" if a["compacts"] else ""
        subs = f" · subagent tự đọc {kb(sub)}" if sub else ""
        print(f"{info['date']}  {title:40}  {kb(a['bytes']):>9} ≈{tok(a['bytes'], ratio)}{share} "
              f"({len(a['files'])} file) · {base}{comp}{subs}")
    return 0


def cmd_files(args):
    project = project_dir()
    files = transcript_files(args.transcripts, project, args.last)
    sessions, total = collections.Counter(), collections.Counter()
    for f in files:
        for e in parse(f)["events"]:
            if e[0] == "read":
                total[e[1]] += e[2]
        sessions.update({e[1] for e in parse(f)["events"] if e[0] == "read"})
    print(f"{len(files)} phiên gần nhất")
    print(f"{'file':42} {'cỡ hiện tại':>11} {'số phiên':>9} {'tổng đọc':>10}")
    existing = {file_key(p, project) for p in check_agent.all_files() if p.suffix == ".md"}
    existing |= {file_key(p, project) for p in (project / "docs" / "agent-memory").glob("*.md")}
    for key in sorted(existing | set(sessions), key=lambda k: (-sessions[k], k)):
        p = key_path(key, project)
        print(f"{key:42} {kb(size(p)):>11} {sessions[key]:>9} {kb(total[key]):>10}" + ("" if p.exists() else "  (đã xoá)"))
    return 0


def cmd_hooks(args):
    log = project_dir() / "Library" / "HorcruxVerify" / "hook-log.jsonl"
    if not log.exists():
        print("chưa có", log)
        return 0
    since = time.strftime("%Y-%m-%dT%H:%M:%S", time.localtime(time.time() - args.days * 86400))
    count, sessions, last = collections.Counter(), collections.defaultdict(set), {}
    for line in log.read_text(encoding="utf-8").splitlines():
        try:
            e = json.loads(line)
        except ValueError:
            continue
        if e.get("t", "") < since:
            continue
        count[e["rule"]] += 1
        sessions[e["rule"]].add(e.get("session"))
        last[e["rule"]] = e
    print(f"{args.days} ngày qua")
    print(f"{'luật':22} {'lần':>5} {'phiên':>6}  lần cuối")
    for rule, n in count.most_common():
        print(f"{rule:22} {n:>5} {len(sessions[rule]):>6}  {last[rule]['t']} {last[rule].get('detail', '')[:60]}")
    return 0


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    for name in ("plan", "sessions", "files"):
        p = sub.add_parser(name)
        p.add_argument("--last", type=int, default=10)
        p.add_argument("--transcripts")
    sub.add_parser("hooks").add_argument("--days", type=int, default=30)
    args = ap.parse_args()
    return {"plan": cmd_plan, "sessions": cmd_sessions, "files": cmd_files, "hooks": cmd_hooks}[args.cmd](args)


if __name__ == "__main__":
    sys.exit(main())
