# Cài bộ Agent vào một dự án

Làm một lần cho mỗi dự án dùng Horcrux làm submodule ở `Assets/Horcrux`.

## 1. `CLAUDE.md` ở gốc dự án

```markdown
## Hard rule — read before anything else

At the start of every session, read `Assets/Horcrux/Agent/horcrux.md` in full, then the always-read rules it lists (`Assets/Horcrux/Agent/Rules/workflow/RULE.md`, `Assets/Horcrux/Agent/Rules/reuse-gate/RULE.md`) and their memory files when present, both the shared tier (`Assets/Horcrux/Agent/Memory/workflow.md`, `Assets/Horcrux/Agent/Memory/reuse-gate.md`) and the project tier (`docs/agent-memory/workflow.md`, `docs/agent-memory/reuse-gate.md`), to understand my system design philosophy and how I work with AI. Read each file entirely, do not skim, before doing anything else. For each task, before touching a domain, read in full every rule, skill and memory file the routing table in `horcrux.md` names for it (memory in both tiers), and strictly follow them. Record lessons by `horcrux.md` › Memory: generalize to the shared tier first; machine-local memory holds only machine-specific facts. Never edit the developer-only section at the end of `horcrux.md`.
```

## 2. Memory dự án

Tạo thư mục `docs/agent-memory/` ở gốc dự án, đi theo git. File đặt tên theo rule hoặc skill như `Agent/Memory/`
(`workflow.md`, `feel-tune.md`, …), chỉ giữ từ vựng, con số, quyết định riêng dự án (`horcrux.md` › Memory).

## 3. File mồi cho skill

Với mỗi thư mục trong `Assets/Horcrux/Agent/Skills/`, tạo `.claude/skills/<việc>/SKILL.md` ở gốc dự án:

```markdown
---
name: <việc>
description: <chép nguyên description của Agent/Skills/<việc>/SKILL.md>
---

Đọc toàn bộ `Assets/Horcrux/Agent/Skills/<việc>/SKILL.md`, `Assets/Horcrux/Agent/Memory/<việc>.md` và
`docs/agent-memory/<việc>.md` (nếu có) rồi làm theo. Bản gốc nằm trong Horcrux; file này chỉ để Claude Code tìm thấy
skill, không chép nội dung.
```

Horcrux thêm skill mới thì thêm file mồi tương ứng.

## 4. Hook

Gộp vào `hooks` của `.claude/settings.json` ở gốc dự án (giữ hook đã có). Mọi sự kiện gọi cùng một lệnh;
`Agent/Tools~/agent_hooks.py` rẽ theo sự kiện. Windows cần Git Bash (lệnh đọc `$CLAUDE_PROJECT_DIR` theo cú pháp bash).

```json
"PreToolUse":  [{ "matcher": "Bash|PowerShell|Edit|Write", "hooks": [{ "type": "command", "command": "<lệnh>", "timeout": 30 }] }],
"PostToolUse": [{ "matcher": "Edit|Write", "hooks": [{ "type": "command", "command": "<lệnh>", "timeout": 10 }] }],
"Stop":        [{ "hooks": [{ "type": "command", "command": "<lệnh>", "timeout": 10 }] }]
```

`<lệnh>` là `python -X utf8 -I "$CLAUDE_PROJECT_DIR/Assets/Horcrux/Agent/Tools~/agent_hooks.py"` (trong JSON viết `\"`).

| Hook | Chặn gì | Luật |
|---|---|---|
| trước lệnh shell | `git add -A`, `-u`, `.` · `git commit -a` · commit không `-- <paths>` (trừ lúc merge) · commit vào `Agent/` khi `check_agent.py` báo lỗi ở chính file đó | `workflow` › Việc lặp lại và git · `horcrux.md` › Cổng ghi |
| trước sửa file | sửa mục developer-only của `horcrux.md` | `horcrux.md` › Yêu cầu của developer |
| sau sửa file, khi dừng | báo xong khi còn `.cs` agent sửa sau lần `verify.sh` OK cuối; chặn một lần mỗi lượt dừng | `compile-check` › Luật |

Không thấy: `.cs` sửa bằng lệnh shell, vì hook chỉ ghi file sửa qua Edit, Write.

File mồi skill và hook chỉ tới máy khác khi đi theo git: dự án ignore cả `.claude/` thì máy khác không có gì. Soát bằng
`git check-ignore -v .claude/settings.json`; bị ignore thì đổi dòng `.claude/` thành `.claude/*`, `!.claude/settings.json`,
`!.claude/skills/` (worktree, `settings.local.json` vẫn ở lại máy).

## 5. Kiểm

```bash
python -X utf8 -I Assets/Horcrux/Agent/Tools~/check_agent.py all
```

Kết quả `lỗi: 0`.
