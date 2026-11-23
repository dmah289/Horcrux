# inbox — bài học chưa rõ miền

Chờ lượt dọn xếp về đúng miền.

### Lệnh xoá bị từ chối, kéo cả cụm lệnh đi theo
`2026-11` · vấp 1 · suy luận · đúng với agent chạy auto mode của Claude Code
**Thấy:** `rm -rf` thư mục trong `Assets`, `AssetDatabase.DeleteAsset` trong eval, `git checkout -- <file>` đều bị từ chối, kể cả khi gộp với lệnh khác — cả cụm bị chặn theo. **Vì:** classifier phía harness coi các lệnh đó nguy hiểm, không giải thích; thử lại cùng ý định không qua.
**Làm:** tách lệnh xoá khỏi lệnh sửa để phần sửa vẫn chạy; bước xoá (trong Project window, để `.meta` đi cùng) và bước trả file giao developer ở mục thao tác Editor cuối báo cáo.

### Phép thay chuỗi trong script nhúng heredoc khớp 0 chỗ, không báo
`2026-10` · vấp 1 · đã đo · đúng với Bash tool là Git Bash trên Windows
**Thấy:** script Python nhúng bằng heredoc `<<'EOF'` để thay khối C# khớp 0 chỗ mà không báo; `'\\'` thành `'\'`, `$"…"` mất `$`. **Vì:** heredoc trong Bash tool không giữ nguyên văn `\` và `$`.
**Làm:** khối có `$`, `\`, backtick thì sửa bằng Edit, hoặc ghi script bằng Write rồi `python -X utf8 -I script.py`; mọi phép thay theo chuỗi `assert count == 1` trước khi ghi.
