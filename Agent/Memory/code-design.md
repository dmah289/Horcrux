# code-design — kinh nghiệm

Bài học đã trả giá khi làm việc với `code-design`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `docs` › Agent.

### Phép đo cấp phát báo +0 B dù code có closure
`2026-10` · vấp 1 · đã đo
**Thấy:** meter theo kích thước heap báo +0 B cho 1000 closure. **Vì:** kích thước heap không cho thấy cấp phát đó.
**Làm:** đếm GC.Alloc (`Is.Not.AllocatingGCMemory()`) thì thấy — không đo kích thước heap (`feel-tune` › Chọn công cụ).

### Helper ở class con làm overload của engine biến mất
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** helper cùng tên trong class con che **mọi** overload của `UnityEngine.Object`. **Vì:** tên trùng ở class con che mọi overload cùng tên của base.
**Làm:** đưa ra extension method thì hết, vì extension không che instance method (`code-design` › tra cứu › Bất biến).

### Service tiêm qua `Init` null lúc chạy, compile chỉ có warning CS0108
`2026-10` · vấp 1 · đã đo
**Thấy:** service null lúc chạy; build sạch lỗi, chỉ có `warning CS0108` — bước kiểm compile chỉ in lỗi nên không thấy. **Vì:** class con của base InitArgs khai báo `Awake` riêng, che `Awake` của base nên bước tiêm `Init` không chạy.
**Làm:** viết `protected override void OnAwake()`; dò ca này bằng cách đếm `warning CS0108` trong output build (`code-design` › tra cứu › Stack và quy ước).

### Bản sao sinh lúc chạy có field service null, bản gốc thì không
`2026-10` · vấp 1 · đã đo · đúng với InitArgs
**Thấy:** compile sạch; phần tử đầu chạy đúng, phần tử thứ hai nổ `NullReferenceException` ở service. **Vì:** `*Initializer` tiêm ở `Awake` rồi tự huỷ; `Instantiate` từ một **instance** đã khởi tạo (ô mẫu trong cùng prefab) cho bản sao không còn Initializer. Nhân bản từ prefab asset thì vẫn còn.
**Làm:** service đặt ở chủ đã được tiêm, con nhận qua chủ; trước khi thêm `IInitializable` vào component, grep `Instantiate(` xem có ai nhân bản nó từ instance không (`code-design` › tra cứu › Stack và quy ước).

### Refactor, tối ưu xong thì nội dung sinh ngẫu nhiên ra khác
`2026-10` · vấp 1 · chuyển sang, chưa kiểm lại · đúng với nội dung sinh từ `UnityEngine.Random` toàn cục
**Thấy:** thay LINQ bằng vòng lặp tay, hay thêm một lần lấy ngẫu nhiên cho trình diễn (cao độ tiếng), làm level, thứ tự phần tử khác bản cũ. **Vì:** mọi nơi dùng chung một luồng `Random`: thêm, bớt, đổi thứ tự một lần gọi ở bất kỳ đâu là dời cả chuỗi sau đó · `OrderBy` ổn định, `List.Sort` không.
**Làm:** trong đường sinh nội dung giữ nguyên thứ tự và số lần gọi `Random`; ngẫu nhiên cho trình diễn lấy từ hàm của dữ liệu (chỉ số ô) hoặc nguồn riêng; thay `OrderBy` bằng `Sort` thì thêm tie-break theo chỉ số gốc (`code-design` › tra cứu › Bất biến).

### Lớp UI phủ toàn màn bật lên thì FPS máy yếu tụt dù CPU nhàn
`2026-11` · vấp 1 · đã đo · đúng với GPU mobile tầm thấp, game 2D nhiều lớp UI trong suốt
**Thấy:** lớp tối hay popup toàn màn hiện là FPS rơi từ ~58 xuống 40–50, tắt là về; luồng main nhàn (~5 ms mỗi khung). Hạ độ phân giải thì màn đó hồi gần đủ, màn thường không đổi. **Vì:** màn thường đã sát ngân sách GPU; mỗi lớp phủ toàn màn cộng một lượt vẽ mọi pixel, shader lặp mảng cố định (cả phần tử trống) cộng thêm phép tính mỗi pixel.
**Làm:** shader toàn màn chỉ lặp phần tử đang sống (dồn lên đầu mảng, truyền số lượng): màn lớp tối lên thêm 5–8 FPS. Phần còn lại là số pixel vẽ chồng: đo bằng A/B độ phân giải trên máy (`docs/Editor/DeviceProbe.md` › `res-ab`), rồi bớt lớp phủ toàn màn chồng nhau hoặc hạ độ phân giải render.

### Nhóm thử hay tập user lẫn người chơi đang chạy bản mặc định
`2026-11` · vấp 1 · suy luận · đúng với giá trị remote chọn nội dung hay quyết một lần rồi lưu save, Firebase Remote Config và A/B Testing
**Thấy:** remote chọn một nội dung trong build (bộ level, bảng cấu hình) hoặc quyết một thứ lưu vào save ở phiên đầu (tập user mặc định), chạy đúng trong Editor, nhưng số liệu nhóm lẫn người chơi bản mặc định. **Vì:** build cũ thiếu nội dung mới nên rơi về mặc định mà vẫn được tính vào nhóm · fetch phiên đầu quá hạn chờ (khởi tạo SDK lần đầu nằm trong hạn đó) thì phiên đó chạy giá trị trong asset; giá trị chỉ chọn nội dung thì áp từ phiên sau, còn giá trị **quyết một lần rồi lưu** thì khoá user vào bản asset mãi mãi · `FetchAsync()` không tham số giữ cache 12 giờ, đổi trên console không thấy ngay khi kiểm.
**Làm:** giá trị lưới đỡ trong asset phải khớp console (so bằng tool diff remote config trước build) · thứ quyết một lần mà đọc remote thì nêu ca fetch trễ ở phiên đầu cho developer chọn: chấp nhận, nới hạn chờ phiên đầu, hay quyết lại khi quyết định cũ đứng trên giá trị asset · experiment lọc theo phiên bản app có nội dung · kiểm nhanh bằng cheat hoặc xoá dữ liệu app (`workflow` › Đối chiếu).
