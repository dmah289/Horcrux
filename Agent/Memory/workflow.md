# workflow — kinh nghiệm

Bài học đã trả giá khi làm việc với `workflow`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### Đã sửa số trong repo mà hành vi không đổi
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** hạ một hệ số trong asset rồi coi là đã sửa, trong khi console ngoài vẫn giữ số cũ. **Vì:** giá trị sống ngoài repo thì bản đang chạy là bản ngoài; bản trong repo chỉ là lưới đỡ.
**Làm:** phát hiện nêu cả hai bản; nghiệm thu giao developer đối chiếu bằng công cụ so (`workflow` › Đối chiếu; `workflow` › tra cứu › Đối chiếu).

### Developer bắt trả lại nhãn đã đổi theo logic
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** đổi logic một nút rồi đổi luôn nhãn cho "đúng nghĩa" — developer bắt trả lại. **Vì:** chữ người chơi thấy là hành vi quan sát được.
**Làm:** đổi logic không kéo theo đổi nhãn, kể cả khi nhãn cũ nghe lệch nghĩa — nêu một dòng (`workflow` › tra cứu › Đối chiếu).

### Đề xuất giữ cơ chế cũ dưới tên mới bị gạt
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** developer muốn bỏ một cơ chế vì vai của input đã đổi; đề xuất giữ cơ chế dưới tên mới, phải brainstorm lại. **Vì:** yêu cầu đổi vai của một khái niệm là đổi mô hình; đổi tên mà giữ cơ chế cũ là chưa đổi.
**Làm:** soát mọi thứ sinh ra từ vai cũ (cờ, khoá, tên field, nhánh riêng) rồi đề xuất trên mô hình mới (`workflow` › Phỏng vấn).

### Màu lấy mẫu trên ảnh là của vật khác
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** lấy mẫu vật nền thay vì vật đang bay qua nó, báo sai màu. **Vì:** ở chỗ nhiều vật chồng nhau trên ảnh, một điểm ảnh thuộc nhiều vật; số đo phải nằm trên đúng vật được hỏi.
**Làm:** định vị vật bằng toạ độ runtime của nó ở frame đó rồi mới lấy mẫu (`workflow` › tra cứu › Nghiệm thu).

### Số đo, phép kiểm đúng ở điều kiện khác điều kiện game chạy
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** chép bề dày từ tài liệu đo ở đơn vị khác · kiểm hiệu ứng hạt bằng ảnh chụp ở chế độ canvas khác chế độ game chạy · code mẫu gắn prefab của canvas vào vật ngoài canvas, không vẽ gì. **Vì:** số trong tài liệu mang theo đơn vị và điều kiện nó được đo; công cụ kiểm đổi điều kiện (chụp ở chế độ render khác) không chứng minh gì cho điều kiện kia.
**Làm:** đo và kiểm ở đúng điều kiện nơi dùng thật — chế độ render, đơn vị, quality (`workflow` › tra cứu › Nghiệm thu).

### Tiến độ, tài nguyên trên save của developer đổi sau khi dò
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** dò luồng bằng eval rời làm đổi tiến độ và tài nguyên trên save của developer. **Vì:** Play qua công cụ chạy trên save thật của developer.
**Làm:** sao lưu đúng các key sẽ chạm trước khi vào, trả lại và so khớp trước khi báo (`playrun.sh` tự làm; eval rời thì `PrefsSnapshot`); dừng ở phần tĩnh khi thứ cần kiểm developer cảm được (`workflow` › tra cứu › Nghiệm thu).

### Cùng một khung eval chép lại hàng trăm lần
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** hàng trăm lần eval chép cùng một khung mà không đề xuất gom thành công cụ. **Vì:** lần thứ hai làm cùng chuỗi thao tác đã là việc lặp lại.
**Làm:** đề xuất công cụ ngay lượt đó: việc gì · lặp mấy lần · công cụ trông thế nào · chỗ đặt · công viết · tiết kiệm mỗi lần (`workflow` › Việc lặp lại).

### Nút cheat gọi lệnh thật mà bấm không đổi gì
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** nút gọi lệnh thật chỉ chạm vào cổng đã khoá từ phiên đầu, bấm không đổi gì. **Vì:** lệnh thật có cổng (khoá, cửa sổ thời gian) mà đường thật đóng cổng trước khi QA kịp bấm.
**Làm:** cheat là lệnh riêng vượt cổng, đặt trong hệ (`workflow` › tra cứu › Cheat).

### Hai mốc cách xa không so được vì diff thô ngập đổi tên
`2026-10` · vấp 1 · đã đo
**Thấy:** cần chứng minh refactor không đổi thuật toán giữa hai commit cách xa; diff thô hàng chục nghìn dòng vì nhiều đợt đổi tên và dời thư mục. **Vì:** đổi tên và dời file che mọi thay đổi thật.
**Làm:** ghép cặp file bằng `git diff -M20%` rồi ghép tay phần còn lại · áp bảng đổi tên (lấy lại từ git history nếu Plan đổi tên đã xoá) lên bản cũ · diff bỏ `using` và khoảng trắng · **so riêng tập chuỗi ký tự** để đổi tên không che chuỗi · đọc tay mọi cặp còn lệch · hàm trùng tên ở hai namespace thì soát `using` từng file xem resolve về bản nào · prefab lọc guid, fileID nhưng **không** lọc `m_IsActive`, `m_Enabled`, `m_LocalPosition` (`workflow` › tra cứu › Nghiệm thu).

### Commit gom file developer đã stage, hoặc lệnh pathspec hỏng cả cụm
`2026-11` · vấp 1 · đã đo
**Thấy:** commit lẫn prefab, scene developer tự stage, chạy nền nên không kịp dừng · `git add` file vừa `git rm` báo "did not match" làm hỏng cả lệnh · file mới không vào commit. **Vì:** `git commit` không pathspec lấy cả index; pathspec của commit không nhận file chưa track; file đã xoá khỏi index không còn để `git add`.
**Làm:** soát cột index của `git status --short`; `git add -- <file mới>` trước; file đã `git rm` chỉ để trong pathspec của `git commit -- <paths>`; không chạy commit nền (`workflow` › tra cứu › Git).

### `.meta` texture đổi sau khi import mà không ai sửa
`2026-11` · vấp 1 · đã đo
**Thấy:** `.meta` của texture vừa import hiện trong `git status` với mục platform Android, iOS không override. **Vì:** Unity tự thêm mục platform mặc định khi import.
**Làm:** coi là nhiễu, để ngoài commit trừ khi developer muốn giữ.
