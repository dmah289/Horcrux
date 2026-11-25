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

## 3. File mồi skill và hook

```bash
python -X utf8 -I Assets/Horcrux/Agent/Tools~/agent_hooks.py install
```

Lệnh đưa `.claude/` của dự án về khớp Horcrux, chạy lại bao nhiêu lần cũng được:

- `.claude/skills/<việc>/SKILL.md` cho mỗi `Agent/Skills/<việc>`: tạo, cập nhật khi description đổi, xoá khi skill đã bỏ.
  File mồi chỉ trỏ về bản gốc để Claude Code tìm thấy skill, không chép nội dung.
- Hook trong `.claude/settings.json`, giữ nguyên hook và cấu hình khác. Mọi sự kiện gọi cùng `agent_hooks.py`; Windows
  cần Git Bash (lệnh đọc `$CLAUDE_PROJECT_DIR` theo cú pháp bash).

Horcrux đổi skill hay hook thì phiên mới báo lệch (hook lúc mở phiên); chạy lại lệnh rồi commit phần đổi. `--check` chỉ
in phần lệch, không ghi.

| Hook | Chặn gì | Luật |
|---|---|---|
| mở phiên | không chặn; báo khi file mồi hay hook lệch Horcrux | mục này |
| sau compact | không chặn; nhắc agent đọc lại trọn luật nền, vì luật đọc bằng Read chỉ còn bản tóm tắt | `horcrux.md` › Luật đọc |
| trước lệnh shell | `git add -A`, `-u`, `.` · `git commit -a` · commit không `-- <paths>` (trừ lúc merge) · commit vào `Agent/` khi `check_agent.py` báo lỗi ở chính file đó | `workflow` › Việc lặp lại và git · `horcrux.md` › Cổng ghi |
| trước sửa file | sửa mục developer-only của `horcrux.md` | `horcrux.md` › Yêu cầu của developer |
| sau sửa file, khi dừng | báo xong khi còn `.cs` agent sửa sau lần `verify.sh` xanh cuối (OK, hoặc chỉ còn fail có sẵn); chặn một lần mỗi lượt dừng | `compile-check` › Luật |
| sau lệnh shell, sau sửa file | không chặn lệnh đã chạy; báo agent khi git ghi nhận ≥ 10 dòng của một file đổi chỉ vì CR cuối dòng, ở dự án hay submodule. Đo trên diff: `core.autocrlf=true` chuẩn hoá CR nên im; `autocrlf=false` hay file `-text` thì đổi kiểu xuống dòng thành diff cả file. Mỗi file báo một lần cho mỗi lần sửa | ghi giữ kiểu xuống dòng của file gốc |

Không thấy: `.cs` sửa bằng lệnh shell, vì hook chỉ ghi file sửa qua Edit, Write.

Mỗi lần chặn hay nhắc ghi một dòng vào `Library/HorcruxVerify/hook-log.jsonl` (ở lại máy); đếm bằng
`python -X utf8 -I Assets/Horcrux/Agent/Tools~/context_audit.py hooks`.

File mồi skill và hook chỉ tới máy khác khi đi theo git: dự án ignore cả `.claude/` thì máy khác không có gì. Soát bằng
`git check-ignore -v .claude/settings.json`; bị ignore thì đổi dòng `.claude/` thành `.claude/*`, `!.claude/settings.json`,
`!.claude/skills/` (worktree, `settings.local.json` vẫn ở lại máy).

## 4. Kiểm

```bash
python -X utf8 -I Assets/Horcrux/Agent/Tools~/check_agent.py all
python -X utf8 -I Assets/Horcrux/Agent/Tools~/agent_hooks.py install --check
```

Kết quả `lỗi: 0` và `lệch: 0`.
