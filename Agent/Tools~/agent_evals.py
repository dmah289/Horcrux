"""Bài kiểm hành vi của bộ Agent/: chạy agent thật (Claude Code, chế độ plan — chỉ đọc và lập kế hoạch, không sửa file)
trên các ca trong `evals.json` cạnh file này, chấm bằng những gì agent đã làm, không bằng cảm nhận.

    python -X utf8 -I agent_evals.py list
    python -X utf8 -I agent_evals.py run [ca ...] [--rule <tên>] [--model <m>] [--budget <usd>]
    python -X utf8 -I agent_evals.py grade <ca> <luồng.jsonl>    # chấm lại luồng đã lưu sau khi sửa phép chấm

`check_agent.py` soát hình thức (trần, con trỏ); file này soát luật có làm agent làm đúng không. Sửa rule, skill, bảng
chỉ dẫn hay luật nền thì chạy các ca chạm luật đó (`--rule <tên>`) trước và sau khi sửa: ca xanh thành đỏ là luật vừa sửa
làm hỏng hành vi đang đúng.

Phép chấm mỗi ca:
    must_read     file phải được đọc trọn (Read không offset, limit); khoá như `context_audit.py`, `{base}` là luật nền.
    reply_has     regex phải có trong chữ agent viết ra (câu trả lời, plan); reply_lacks: không được có.

Mỗi ca là một phiên agent, tốn tiền thật: `--budget` chặn trên mỗi ca (mặc định 2 USD). Luồng thô lưu ở
`Library/HorcruxVerify/evals/<ca>-<thời điểm>.jsonl` để đọc lại khi ca đỏ. Cần `claude` trên PATH.
"""
import argparse
import importlib.util
import json
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


def run_case(case, project, args):
    out_dir = project / "Library" / "HorcruxVerify" / "evals"
    out_dir.mkdir(parents=True, exist_ok=True)
    raw = out_dir / f"{case['name']}-{time.strftime('%Y%m%d-%H%M%S')}.jsonl"
    cmd = [shutil.which("claude") or "claude", "-p", case["prompt"], "--permission-mode", "plan",
           "--output-format", "stream-json", "--verbose", "--no-session-persistence",
           "--max-budget-usd", str(args.budget)]
    if args.model:
        cmd += ["--model", args.model]
    r = subprocess.run(cmd, cwd=project, capture_output=True)
    raw.write_bytes(r.stdout)
    fails, cost = grade(r.stdout.decode("utf-8", "replace"), case, project,
                        r.stderr.decode("utf-8", "replace").strip()[:200] if r.returncode else "")
    return fails, cost, raw


def grade(stream, case, project, crash=""):
    """Chấm một luồng stream-json; `crash` là stderr khi claude thoát lỗi."""
    reads, text, cost, error = set(), [], None, None
    for line in stream.splitlines():
        try:
            e = json.loads(line)
        except ValueError:
            continue
        if e.get("type") == "assistant":
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
            cost = e.get("total_cost_usd")
            if e.get("is_error") and e.get("num_turns", 0) <= 1:
                error = e.get("result") or e.get("subtype")
    if error or (crash and not text):
        # agent không chạy được (đăng nhập hết hạn, hết ngân sách ngay lượt đầu): không chấm, kẻo báo đỏ oan
        return [f"không chạy được: {error or crash}"], cost
    reply = "\n".join(text)

    fails = [f"chưa đọc trọn {k}" for k in expand(case["must_read"], project) if k not in reads]
    fails += [f"thiếu /{p}/" for p in case.get("reply_has", []) if not re.search(p, reply, re.I)]
    fails += [f"có /{p}/" for p in case.get("reply_lacks", []) if re.search(p, reply, re.I)]
    return fails, cost


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("list")
    run = sub.add_parser("run")
    run.add_argument("names", nargs="*")
    run.add_argument("--rule")
    run.add_argument("--model")
    run.add_argument("--budget", type=float, default=2.0)
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
    red = 0
    for c in picked:
        fails, cost, raw = run_case(c, project, args)
        red += bool(fails)
        money = f" · {cost:.2f} USD" if cost is not None else ""
        print(f"{'ĐỎ ' if fails else 'xanh'} {c['name']}{money} · {raw.relative_to(project).as_posix()}")
        for f in fails:
            print("     ", f)
    print(f"đỏ: {red}/{len(picked)}")
    return 1 if red else 0


if __name__ == "__main__":
    sys.exit(main())
