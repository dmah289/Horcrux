# code-design — kinh nghiệm

Bài học đã trả giá khi làm việc với `code-design`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

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
`2026-11` · vấp 1 · suy luận · đúng với shader tự viết vẽ phủ cả màn trên GPU mobile tầm thấp
**Thấy:** lớp tối hướng dẫn hay popup hiện là FPS rơi 60 → 43–50, tắt là về 60; luồng main chỉ ~30% một nhân. **Vì:** fragment shader chạy cho mọi pixel màn (~2,6 triệu ở 1080×2400) và lặp hết mảng cố định (8 lỗ khoét, mỗi lỗ một SDF float) kể cả ô trống — cỡ chục ms GPU, đủ lỡ vsync.
**Làm:** shader toàn màn chỉ lặp phần tử đang sống (C# dồn lên đầu mảng, truyền số lượng); ước giá bằng phép tính mỗi pixel × số pixel trước khi thêm vòng lặp; nghiệm thu bằng FPS trên máy yếu thật (`feel-tune.md` › FPS trên máy thật bị gán cho một thao tác).
