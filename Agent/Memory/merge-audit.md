# merge-audit — kinh nghiệm

Bài học đã trả giá khi làm việc với `merge-audit`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### Merge ở checkout chính bị từ chối vì file đã stage
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** merge ở checkout chính bị từ chối vì index có file đã stage; agent cất chúng rồi merge tiếp, conflict hiện ra trong cây Unity đang mở. **Vì:** Unity đang mở import `.meta` và asset còn dấu conflict, sinh GUID mới cho file nó đọc không được.
**Làm:** merge trong worktree riêng — worktree không cần index của checkout chính sạch, file developer đã stage để nguyên, không `stash`; đưa nhánh lên bằng `merge --ff-only` (`merge-audit` › Luật).
