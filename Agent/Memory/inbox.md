# inbox — bài học chưa rõ miền

Chờ lượt dọn xếp về đúng miền.

### Lệnh xoá bị từ chối, kéo cả cụm lệnh đi theo
`2026-11` · vấp 1 · suy luận · đúng với agent chạy auto mode của Claude Code
**Thấy:** `rm -rf` thư mục trong `Assets`, `AssetDatabase.DeleteAsset` trong eval, `git checkout -- <file>` đều bị từ chối, kể cả khi gộp với lệnh khác — cả cụm bị chặn theo. **Vì:** classifier phía harness coi các lệnh đó nguy hiểm, không giải thích; thử lại cùng ý định không qua.
**Làm:** tách lệnh xoá khỏi lệnh sửa để phần sửa vẫn chạy; bước xoá (trong Project window, để `.meta` đi cùng) và bước trả file giao developer ở mục thao tác Editor cuối báo cáo.

### Script Python nhúng heredoc khớp 0 chỗ hoặc báo SyntaxError
`2026-11` · vấp 2 · đã đo · đúng với Bash tool là Git Bash trên Windows
**Thấy:** script Python nhúng bằng heredoc `<<'EOF'` thay khối C# khớp 0 chỗ mà không báo, hoặc dừng ở `SyntaxError: unterminated string literal` khi chuỗi chứa đường dẫn `\`; `'\\'` thành `'\'`, `$"…"` mất `$`. **Vì:** heredoc trong Bash tool không giữ nguyên văn `\` và `$`.
**Làm:** khối có `$`, `\`, backtick thì sửa bằng Edit, hoặc ghi script bằng Write rồi `python -X utf8 -I script.py`; mọi phép thay theo chuỗi `assert count == 1` trước khi ghi.

### Script Python dừng giữa chừng sau khi đã ghi một phần file
`2026-10` · vấp 1 · đã đo · đúng với console Windows
**Thấy:** script in tiếng Việt ném lỗi encode giữa lượt, một nửa số file đã ghi. **Vì:** console Windows mặc định cp1252; `-I` bỏ qua `PYTHONIOENCODING`.
**Làm:** chạy `python -X utf8 -I`, hoặc script tự `sys.stdout.reconfigure(encoding="utf-8")` ở đầu.

### Script sửa file text làm diff lệch cả file
`2026-11` · vấp 2 · đã đo · đúng với working copy Windows `core.autocrlf=true`
**Thấy:** script chỉ thay vài token mà diff báo đổi mọi dòng; git cảnh báo "LF will be replaced by CRLF" ở mọi file chạm · `sed -i` của Git Bash xoá một dòng mà cả file .cs thành LF. **Vì:** working copy là CRLF, Unity ghi YAML bằng LF; đọc ghi ở chế độ text đổi kiểu xuống dòng — `sed` của MSYS mặc định cũng vậy.
**Làm:** đọc ghi bytes (`sed -i -b`), giữ kiểu xuống dòng của file gốc, kiểm bằng `file <path>` sau khi sửa; cảnh báo CRLF là nhiễu, xác nhận diff chỉ còn token bằng `git diff --ignore-space-at-eol --stat`.

### FPS tụt hẳn sau khi app Android quay lại từ nền, không tự hồi
`2026-11` · vấp 1 · đọc tài liệu, chưa kiểm trên máy · đúng với Android màn 90/120Hz, Unity 6, Optimized Frame Pacing bật
**Thấy:** `targetFrameRate` vẫn 60, nằm nền vài giây rồi quay lại thì bộ đếm báo 30–45 FPS tới khi tắt app. **Vì:** nhịp FPS trên Android là ước số của tần số quét (120Hz → 60/40/30, 90Hz → 45); sau resume Unity và Swappy có thể chốt chu kỳ vsync khác — lỗi Unity còn mở "targetFrameRate changes after locking/unlocking the screen". Gán lại đúng số cũ có thể bị bỏ qua.
**Làm:** một chủ giữ frame rate, khi về foreground trả `-1` một frame rồi xin lại số mục tiêu; vẫn tụt thì thử tắt Optimized Frame Pacing. Nghiệm thu trên máy 90/120Hz bằng bộ đếm FPS.

### Nhóm thử của AB test lẫn người chơi đang chạy bản mặc định
`2026-11` · vấp 1 · suy luận · đúng với giá trị remote chọn nội dung đóng sẵn trong build, Firebase Remote Config và A/B Testing
**Thấy:** remote chọn một nội dung trong build (bộ level, bảng cấu hình), chạy đúng trong Editor, nhưng số liệu nhóm thử sẽ lẫn người chơi bản mặc định. **Vì:** build cũ thiếu nội dung mới nên rơi về mặc định mà vẫn được tính vào nhóm · fetch phiên đầu quá hạn chờ thì phiên đó chạy mặc định, giá trị chỉ áp từ phiên sau · `FetchAsync()` không tham số giữ cache 12 giờ, đổi trên console không thấy ngay khi kiểm.
**Làm:** nêu ở mục việc ngoài repo của báo cáo: experiment lọc theo phiên bản app có nội dung · cân nhắc nhóm lẫn ở phiên đầu khi khác biệt nằm ở đầu game · kiểm nhanh bằng cheat hoặc xoá dữ liệu app (`workflow` › Đối chiếu).
