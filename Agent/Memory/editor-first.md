# editor-first — kinh nghiệm

Bài học đã trả giá khi làm việc với `editor-first`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### Asset cấu hình mất dữ liệu sau một lần lưu scene
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** lưu scene làm một asset cấu hình rơi dữ liệu của field đã xoá khỏi code. **Vì:** lệnh lưu kéo theo mọi asset đang bẩn trong Editor.
**Làm:** sau lệnh lưu qua công cụ soát `git status`; file không thuộc task thì không stage, stage theo danh sách file của task (`workflow` › tra cứu › Git).

### `.meta` đổi trong git status sau khi chép thư mục
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** `.meta` đổi được báo là vỡ reference; khôi phục xong Unity đổi lại, và atlas chép vẫn pack thư mục cũ. **Vì:** hai asset trùng GUID thì Unity cấp GUID mới cho bản nhập sau và ghi lại `.meta` của nó; reference cũ vẫn trỏ bản gốc.
**Làm:** không khôi phục `.meta`; trỏ lại thứ chép theo (atlas, ô trong asset cấu hình, entry Addressables) vào bản chép, rồi soát `AssetReference` gãy (`editor-first` › tra cứu › Sửa asset).
