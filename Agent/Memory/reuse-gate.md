# reuse-gate — kinh nghiệm

Bài học đã trả giá khi làm việc với `reuse-gate`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### CS0121 giữa hai extension method cùng tên
`2026-10` · vấp 1 · đã đo
**Thấy:** file vừa thêm `using` namespace Horcrux báo CS0121 ambiguous giữa extension của Horcrux và extension cùng tên của dự án. **Vì:** cả hai namespace cùng được `using` trong file đó.
**Làm:** sửa `using` ở chính file đó — giữ bản Horcrux, bỏ `using` namespace extension của dự án nếu file không cần gì khác từ đó; không alias, không đụng file extension nào trong bước này; việc gộp hai bản là đề xuất riêng (`reuse-gate` › Có sẵn chưa).

### Nhóm thử của AB test lẫn người chơi đang chạy bản mặc định
`2026-11` · vấp 1 · suy luận · đúng với giá trị remote chọn nội dung đóng sẵn trong build, Firebase Remote Config và A/B Testing
**Thấy:** remote chọn một nội dung trong build (bộ level, bảng cấu hình), chạy đúng trong Editor, nhưng số liệu nhóm thử sẽ lẫn người chơi bản mặc định. **Vì:** build cũ thiếu nội dung mới nên rơi về mặc định mà vẫn được tính vào nhóm · fetch phiên đầu quá hạn chờ thì phiên đó chạy mặc định, giá trị chỉ áp từ phiên sau · `FetchAsync()` không tham số giữ cache 12 giờ, đổi trên console không thấy ngay khi kiểm.
**Làm:** nêu ở mục việc ngoài repo của báo cáo: experiment lọc theo phiên bản app có nội dung · cân nhắc nhóm lẫn ở phiên đầu khi khác biệt nằm ở đầu game · kiểm nhanh bằng cheat hoặc xoá dữ liệu app (`workflow` › Đối chiếu).
