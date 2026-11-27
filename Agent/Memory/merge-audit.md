# merge-audit — kinh nghiệm

Bài học đã trả giá khi làm việc với `merge-audit`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `docs` › Agent.

### Hai mốc cách xa không so được vì diff thô ngập đổi tên
`2026-10` · vấp 1 · đã đo
**Thấy:** cần chứng minh refactor không đổi thuật toán giữa hai commit cách xa; diff thô hàng chục nghìn dòng vì nhiều đợt đổi tên và dời thư mục. **Vì:** đổi tên và dời file che mọi thay đổi thật.
**Làm:** ghép cặp file bằng `git diff -M20%` rồi ghép tay phần còn lại · áp bảng đổi tên (lấy lại từ git history nếu Plan đổi tên đã xoá) lên bản cũ · diff bỏ `using` và khoảng trắng · **so riêng tập chuỗi ký tự** để đổi tên không che chuỗi · đọc tay mọi cặp còn lệch · hàm trùng tên ở hai namespace thì soát `using` từng file xem resolve về bản nào · prefab lọc guid, fileID nhưng **không** lọc `m_IsActive`, `m_Enabled`, `m_LocalPosition` (`workflow` › tra cứu › Nghiệm thu).
