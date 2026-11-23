# Cài bộ Agent vào một dự án

Làm một lần cho mỗi dự án dùng Horcrux làm submodule ở `Assets/Horcrux`.

## 1. `CLAUDE.md` ở gốc dự án

```markdown
## Hard rule — read before anything else

At the start of every session, read `Assets/Horcrux/Agent/horcrux.md` in full, then the always-read rules it lists (`Assets/Horcrux/Agent/Rules/workflow/RULE.md`, `Assets/Horcrux/Agent/Rules/reuse-gate/RULE.md`) and their memory files when present (`Assets/Horcrux/Agent/Memory/workflow.md`, `Assets/Horcrux/Agent/Memory/reuse-gate.md`), to understand my system design philosophy and how I work with AI. Read each file entirely, do not skim, before doing anything else. For each task, before touching a domain, read in full every rule, skill and memory file the routing table in `horcrux.md` names for it, and strictly follow them. Never edit the developer-only section at the end of `horcrux.md`.
```

## 2. File mồi cho skill

Với mỗi thư mục trong `Assets/Horcrux/Agent/Skills/`, tạo `.claude/skills/<việc>/SKILL.md` ở gốc dự án:

```markdown
---
name: <việc>
description: <chép nguyên description của Agent/Skills/<việc>/SKILL.md>
---

Đọc toàn bộ `Assets/Horcrux/Agent/Skills/<việc>/SKILL.md` và `Assets/Horcrux/Agent/Memory/<việc>.md` (nếu có) rồi làm
theo. Bản gốc nằm trong Horcrux; file này chỉ để Claude Code tìm thấy skill, không chép nội dung.
```

Horcrux thêm skill mới thì thêm file mồi tương ứng.

## 3. Kiểm

```bash
python -X utf8 -I Assets/Horcrux/Agent/Tools~/check_agent.py all
```

Kết quả `lỗi: 0`.
