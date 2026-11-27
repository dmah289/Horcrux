# workflow — kinh nghiệm

Bài học đã trả giá khi làm việc với `workflow`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `docs` › Agent.

### File thường là nhiễu hiện trong `git status`
`2026-11` · vấp 2 · đã đo
**Thấy:** `.meta` texture vừa import có thêm mục platform Android, iOS không override · file group Addressables đổi sau mỗi lần mở Editor nên vào danh sách "không commit"; một tool sinh nội dung ghi entry thật vào đúng file đó, commit bỏ nó, nội dung không load được mà không lỗi nào báo trước lúc chơi. **Vì:** Unity tự thêm mục platform mặc định khi import; danh sách nhiễu xét theo tên file, không theo nội dung diff.
**Làm:** `.meta` chỉ thêm mục platform mặc định thì để ngoài commit trừ khi developer muốn giữ. File trong danh sách nhiễu thì đọc diff trước khi bỏ: hash, thời điểm là nhiễu; entry (guid, address) task vừa sinh là phần của task, commit cùng.

### Session ở worktree, việc cần Editor không làm được
`2026-11` · vấp 1 · đã đo · đúng với session mở trong git worktree
**Thấy:** tới lúc sửa prefab, scene hay art chưa track mới phát hiện worktree thiếu submodule, thiếu art, và hook chặn ghi checkout chính. **Vì:** Editor và MCP chỉ mở trên checkout chính; mở worktree trong Editor phải init submodule, chép art, import lại Library rất lâu.
**Làm:** việc cần Editor thì hỏi ngay đầu task làm ở đâu; được làm ở checkout chính thì ghi file ở scratchpad rồi chép sang, giữ kiểu xuống dòng của file gốc; không commit nếu developer chưa bảo.

### Lệnh xoá bị từ chối, kéo cả cụm lệnh đi theo
`2026-11` · vấp 1 · suy luận · đúng với agent chạy auto mode của Claude Code
**Thấy:** `rm -rf` thư mục trong `Assets`, `AssetDatabase.DeleteAsset` trong eval, `git checkout -- <file>` đều bị từ chối, kể cả khi gộp với lệnh khác — cả cụm bị chặn theo. **Vì:** classifier phía harness coi các lệnh đó nguy hiểm, không giải thích; thử lại cùng ý định không qua.
**Làm:** tách lệnh xoá khỏi lệnh sửa để phần sửa vẫn chạy; bước xoá (trong Project window, để `.meta` đi cùng) và bước trả file giao developer ở mục thao tác Editor cuối báo cáo.

### Script Python dừng giữa chừng, hoặc báo `ModuleNotFoundError` cho gói đã cài
`2026-11` · vấp 2 · đã đo · đúng với console Windows, gói cài bằng `pip install --user`
**Thấy:** script in tiếng Việt ném lỗi encode giữa lượt, một nửa số file đã ghi · `python -I -c "import cv2"` báo `No module named 'cv2'` trong khi tool đo dùng OpenCV vẫn chạy. **Vì:** console Windows mặc định cp1252, `-I` bỏ qua `PYTHONIOENCODING` · `-I` gồm `-s`, bỏ site-packages của user, nơi `pip install --user` đặt OpenCV, numpy.
**Làm:** script chỉ dùng thư viện chuẩn chạy `python -X utf8 -I`; script cần gói bên thứ ba chạy `python -X utf8 -P` (vẫn chặn thư mục script khỏi `sys.path`), đúng cờ tài liệu tool ghi; script tự viết thì `sys.stdout.reconfigure(encoding="utf-8")` ở đầu.
