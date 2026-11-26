"""Bài kiểm hành vi của bộ Agent/: chạy agent thật (Claude Code, chế độ plan — chỉ đọc và lập kế hoạch, không sửa file)
trên các ca trong `evals.json` cạnh file này, chấm bằng những gì agent đã làm, không bằng cảm nhận.

    python -X utf8 -I agent_evals.py list
    python -X utf8 -I agent_evals.py run [ca ...] [--rule <tên>] [--model <m>] [--max-turns <n>]
    python -X utf8 -I agent_evals.py grade <ca> <luồng.jsonl>    # chấm lại luồng đã lưu sau khi sửa phép chấm

`check_agent.py` soát hình thức (trần, con trỏ); file này soát luật có làm agent làm đúng không. Sửa rule, skill, bảng
chỉ dẫn hay luật nền thì chạy các ca chạm luật đó (`--rule <tên>`) trước và sau khi sửa: ca xanh thành đỏ là luật vừa sửa
làm hỏng hành vi đang đúng.

Phép chấm mỗi ca:
    must_read     file phải được đọc trọn (Read không offset, limit); khoá như `context_audit.py`, `{base}` là luật nền.
    reply_has     regex phải có trong chữ agent viết ra (câu trả lời, plan); reply_lacks: không được có.

Mỗi ca là một phiên agent thật, **chỉ chạy trên đăng nhập gói** (Pro, Max, Team): tốn hạn mức token của gói như phiên
developer tự mở, không phát sinh hoá đơn theo token. Lối tính tiền theo token (API key, auth token, `apiKeyHelper`,
Bedrock, Vertex) thì từ chối trước khi chạy; phiên nào báo nguồn khoá khác `none` thì dừng mọi ca còn lại. Số in ra là
token, không in USD: `total_cost_usd` của CLI là giá API quy đổi, không phải tiền bị trừ. `--max-turns` (mặc định 40)
chặn một ca chạy vòng; không dùng `--max-budget-usd` vì CLI báo trần cho agent và agent tự bỏ bớt việc đọc. Luồng thô
lưu ở `Library/HorcruxVerify/evals/<ca>-<thời điểm>.jsonl` để đọc lại khi ca đỏ. Cần `claude` trên PATH.
"""
import argparse
import importlib.util
import json
import os
import pathlib
import re
import shutil
import subprocess
import sys
import time

TOOLS = pathlib.Path(__file__).resolve().parent
sys.dont_write_bytecode = True
_spec = importlib.util.spec_from_file_location("context_audit", TOOLS / "context_audit.py")
audit = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(audit)


def load_cases():
    return json.loads((TOOLS / "evals.json").read_text(encoding="utf-8"))["cases"]


def expand(must, project):
    keys = []
    for k in must:
        keys += sorted(audit.file_key(f, project) for f in audit.base_files(project)) if k == "{base}" else [k]
    return keys


HOST_ENV = ("CLAUDECODE", "CLAUDE_CODE_", "CLAUDE_AGENT_SDK_", "CLAUDE_PID", "CLAUDE_EFFORT", "CLAUDE_PREVIEW_", "AI_AGENT")
PAID_ENV = ("ANTHROPIC_API_KEY", "ANTHROPIC_AUTH_TOKEN", "CLAUDE_CODE_USE_BEDROCK", "CLAUDE_CODE_USE_VERTEX",
            "CLAUDE_CODE_USE_FOUNDRY")


def paid_auth(project):
    """Lý do phiên con sẽ tính tiền theo token thay vì trừ hạn mức gói; None khi chỉ còn đường đăng nhập gói."""
    found = [k for k in PAID_ENV if os.environ.get(k)]
    for f in (pathlib.Path.home() / ".claude" / "settings.json", project / ".claude" / "settings.json",
              project / ".claude" / "settings.local.json"):
        try:
            s = json.loads(f.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            continue
        found += [f"{f.name}: apiKeyHelper"] if s.get("apiKeyHelper") else []
        found += [f"{f.name}: env.{k}" for k in PAID_ENV if (s.get("env") or {}).get(k)]
    return ", ".join(found) or None


def clean_env():
    """Chạy từ trong một phiên Claude Code thì con thừa hưởng biến của phiên mẹ (id phiên, kênh tin nhắn, chế độ SDK)
    và không còn là phiên CLI mới: đã đo một lần context hook mở phiên không tới được agent. Bỏ hết để đo đúng phiên
    developer mở."""
    return {k: v for k, v in os.environ.items() if not k.startswith(HOST_ENV) and k not in PAID_ENV}


def run_case(case, project, args):
    out_dir = project / "Library" / "HorcruxVerify" / "evals"
    out_dir.mkdir(parents=True, exist_ok=True)
    raw = out_dir / f"{case['name']}-{time.strftime('%Y%m%d-%H%M%S')}.jsonl"
    cmd = [shutil.which("claude") or "claude", "-p", case["prompt"], "--permission-mode", "plan",
           "--output-format", "stream-json", "--verbose", "--no-session-persistence",
           "--max-turns", str(args.max_turns)]
    if args.model:
        cmd += ["--model", args.model]
    r = subprocess.run(cmd, cwd=project, capture_output=True, stdin=subprocess.DEVNULL, env=clean_env())
    raw.write_bytes(r.stdout)
    fails, info = grade(r.stdout.decode("utf-8", "replace"), case, project,
                        r.stderr.decode("utf-8", "replace").strip()[:200] if r.returncode else "")
    return fails, info, raw


def grade(stream, case, project, crash=""):
    """Chấm một luồng stream-json; `crash` là stderr khi claude thoát lỗi."""
    reads, text, error = set(), [], None
    info = {"auth": None, "tokens": None}  # nguồn khoá ("none" = đăng nhập gói), token đã dùng
    for line in stream.splitlines():
        try:
            e = json.loads(line)
        except ValueError:
            continue
        if e.get("type") == "system" and e.get("subtype") == "init":
            info["auth"] = e.get("apiKeySource")
        elif e.get("type") == "assistant":
            for c in e["message"].get("content", []):
                if c.get("type") == "text":
                    text.append(c["text"])
                elif c.get("type") == "tool_use":
                    ti = c.get("input") or {}
                    if c["name"] == "Read" and "offset" not in ti and "limit" not in ti:
                        reads.add(audit.key_of(ti.get("file_path", "")))
                    elif c["name"] == "ExitPlanMode":
                        text.append(ti.get("plan", ""))
        elif e.get("type") == "result":
            text.append(e.get("result") or "")
            u = e.get("usage") or {}
            info["tokens"] = sum(u.get(k) or 0 for k in ("input_tokens", "cache_creation_input_tokens",
                                                         "cache_read_input_tokens", "output_tokens"))
            if e.get("is_error") and e.get("num_turns", 0) <= 1:
                error = e.get("result") or e.get("subtype")
    if error or (crash and not text):
        # agent không chạy được (đăng nhập hết hạn, hết ngân sách ngay lượt đầu): không chấm, kẻo báo đỏ oan
        return [f"không chạy được: {error or crash}"], info
    reply = "\n".join(text)

    fails = [f"chưa đọc trọn {k}" for k in expand(case["must_read"], project) if k not in reads]
    fails += [f"thiếu /{p}/" for p in case.get("reply_has", []) if not re.search(p, reply, re.I)]
    fails += [f"có /{p}/" for p in case.get("reply_lacks", []) if re.search(p, reply, re.I)]
    return fails, info


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("list")
    run = sub.add_parser("run")
    run.add_argument("names", nargs="*")
    run.add_argument("--rule")
    run.add_argument("--model")
    run.add_argument("--max-turns", type=int, default=40)
    regrade = sub.add_parser("grade")
    regrade.add_argument("name")
    regrade.add_argument("stream")
    args = ap.parse_args()

    cases = load_cases()
    if args.cmd == "list":
        for c in cases:
            print(f"{c['name']:16} {', '.join(c['rules']):32} {c['prompt'][:70]}")
        return 0
    if args.cmd == "grade":
        case = next(c for c in cases if c["name"] == args.name)
        fails, _ = grade(pathlib.Path(args.stream).read_text(encoding="utf-8"), case, audit.project_dir())
        print("ĐỎ " if fails else "xanh", args.name, *(f"\n      {f}" for f in fails))
        return 1 if fails else 0
    picked = [c for c in cases if (not args.names or c["name"] in args.names) and
              (not args.rule or args.rule in c["rules"])]
    if not picked:
        print("không ca nào khớp")
        return 1
    project = audit.project_dir()
    paid = paid_auth(project)
    if paid:
        print(f"không chạy: phiên sẽ tính tiền theo token ({paid}). Eval chỉ chạy trên đăng nhập gói (`claude /login`).")
        return 2
    red = 0
    for c in picked:
        fails, info, raw = run_case(c, project, args)
        red += bool(fails)
        used = f" · {info['tokens'] / 1000:.0f}k token" if info["tokens"] is not None else ""
        print(f"{'ĐỎ ' if fails else 'xanh'} {c['name']}{used} · {raw.relative_to(project).as_posix()}")
        for f in fails:
            print("     ", f)
        if info["auth"] not in (None, "none"):
            print(f"dừng: phiên dùng nguồn khoá {info['auth']}, không phải đăng nhập gói — không chạy ca còn lại.")
            return 2
    print(f"đỏ: {red}/{len(picked)}")
    return 1 if red else 0


if __name__ == "__main__":
    sys.exit(main())
