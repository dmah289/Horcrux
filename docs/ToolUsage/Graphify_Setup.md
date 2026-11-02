# Cài graphify cho Category Jam

Cập nhật: 2026-10-07.

Chạy ở terminal, tại root project, theo thứ tự:

1. Cài graphify
   ```
   uv tool install graphifyy
   ```

2. Cài skill `/graphify` cho Claude Code (copy skill vào `~/.claude/skills/`, mỗi máy chạy một lần)
   ```
   graphify install
   ```

3. Cài skill vào project và tích hợp với Claude Code (copy skill vào `.claude/skills/` của project, ghi luật vào `CLAUDE.md` và hook vào `.claude/settings.json`)
   ```
   graphify install --project
   ```

4. Build graph lần đầu: mở Claude Code, gõ `/graphify`. Phạm vi quét đã có sẵn trong `.graphifyignore`. Kết quả ở `graphify-out/`.

5. Cài git hook (tự rebuild graph sau mỗi commit hoặc đổi branch, mỗi máy chạy một lần)
   ```
   graphify hook install
   ```

6. Tạo thêm git hook post-merge để `git pull` cũng rebuild (graphify không cài sẵn hook này). Copy từ post-commit, đổi dòng tính file thay đổi:
   ```
   sed 's|^CHANGED=.*|CHANGED=$(git diff --name-only ORIG_HEAD HEAD 2>/dev/null)|' .git/hooks/post-commit > .git/hooks/post-merge
   chmod +x .git/hooks/post-merge
   ```

Dùng hàng ngày:

- Hỏi về code: `graphify query "<câu hỏi>"` ở terminal, hoặc `/graphify query "..."` trong chat.
- Sửa code xong: `graphify update .` (AI tự chạy theo luật trong `CLAUDE.md`).
- Sửa file `.md`: `/graphify . --update` trong chat.
- Commit, pull, đổi branch: không cần làm gì, hook tự rebuild. Log ở `~/.cache/graphify-rebuild.log`.
