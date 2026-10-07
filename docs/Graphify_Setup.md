# Cài graphify cho Category Jam

Cập nhật: 2026-10-07.

Chạy ở terminal, tại root project, theo thứ tự:

1. Cài graphify
   ```
   uv tool install graphifyy
   ```

2. Build graph lần đầu: mở Claude Code, gõ `/graphify`. Phạm vi quét đã có sẵn trong `.graphifyignore`. Kết quả ở `graphify-out/`.

3. Cài tích hợp với Claude Code (ghi luật vào `CLAUDE.md` và hook vào `.claude/settings.json`)
   ```
   graphify claude install
   ```

4. Cài git hook (tự rebuild graph sau mỗi commit hoặc đổi branch, mỗi máy chạy một lần)
   ```
   graphify hook install
   ```

Dùng hàng ngày:

- Hỏi về code: `graphify query "<câu hỏi>"` ở terminal, hoặc `/graphify query "..."` trong chat.
- Sửa code xong: `graphify update .` (AI tự chạy theo luật trong `CLAUDE.md`).
- Sửa file `.md`: `/graphify . --update` trong chat.
- Commit: không cần làm gì, hook tự rebuild. Log ở `~/.cache/graphify-rebuild.log`.
