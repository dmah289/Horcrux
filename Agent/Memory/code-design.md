# code-design — kinh nghiệm

Bài học đã trả giá khi làm việc với `code-design`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### Dừng Play ra lỗi thiếu service, vá xong lộ hàng chục lỗi trả tài nguyên
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** dừng Play ra một lỗi thiếu service; vá lỗi đó thì lộ hàng chục lỗi trả tài nguyên — đường dọn không nên chạy, không phải chỗ ném sai. **Vì:** dọn "dự phòng" ở `OnDestroy` chạy cả lúc thoát app, khi các hệ kia đã tự dọn trước.
**Làm:** trả tài nguyên cho hệ khác nằm ở đường rời chủ động, chủ gọi trước khi unload; `OnDestroy` chỉ dừng việc của chính nó (`code-design` › Hình dạng).

### Bấm component dùng chung ở một host không ra gì, không lỗi nào báo
`2026-09` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** component dùng chung mang cờ chế độ; ở host không có ngữ cảnh của chế độ đó, bấm không ra gì, không lỗi nào báo. **Vì:** cờ chế độ trên instance đòi host có ngữ cảnh của chế độ đó; component dùng chung mặc định thụ động, không cờ chế độ.
**Làm:** service đã tự chọn theo ngữ cảnh thì component chỉ gọi service; host không cần hành vi thì tắt component (`code-design` › tra cứu › Stack và quy ước).

### Phép đo cấp phát báo +0 B dù code có closure
`2026-10` · vấp 1 · đã đo
**Thấy:** meter theo kích thước heap báo +0 B cho 1000 closure. **Vì:** kích thước heap không cho thấy cấp phát đó.
**Làm:** đếm GC.Alloc (`Is.Not.AllocatingGCMemory()`) thì thấy — không đo kích thước heap (`feel-tune` › Chọn công cụ).

### Cấp phát chuỗi được biện là "chỉ lúc chạm"
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** chuỗi nội suy cấp phát mỗi lần chạm với lý do "chạm là tương tác, không phải mỗi frame". **Vì:** đường chạy theo mỗi lần chạm vẫn là đường nóng của Sổ tay hiệu năng — không string concat, closure ở hot path.
**Làm:** số vào `StringBuilder` bằng `TryFormat` vào `stackalloc` (`code-design` › tra cứu › Hiệu năng).

### Field thời gian bằng 0 chạy sai âm thầm, không log
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** field khoảng thời gian không default thành 0, ghi đĩa mỗi frame, không một dòng log. **Vì:** giá trị mặc định của kiểu là hợp lệ — một trong năm hình dạng **không** nổ.
**Làm:** khai default trong khai báo, `[Min]`/`[Range]` (`code-design` › tra cứu › Guard).

### UI hiện ca "chưa có" như ca có thật trên save cài mới
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** luật coi `null` là "chưa báo"; save cài mới lấy mặc định từ asset ra `""`, UI hiện như ca có thật. **Vì:** serializer của Unity không giữ `null` cho `string` và class `[Serializable]` lồng: đọc từ asset ra `""` và object rỗng.
**Làm:** nghĩa *"chưa có"* suy từ field hệ quyết được (id 0 = chưa quyết) hoặc chuẩn hoá ở cửa quyết nghĩa (`code-design` › tra cứu › Guard).

### Helper ở class con làm overload của engine biến mất
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** helper cùng tên trong class con che **mọi** overload của `UnityEngine.Object`. **Vì:** tên trùng ở class con che mọi overload cùng tên của base.
**Làm:** đưa ra extension method thì hết, vì extension không che instance method (`code-design` › tra cứu › Bất biến).

### Vòng duyệt cả enum cấp cho một phần tử thứ không ai đọc
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** phần tử khác hành vi nằm chung enum với nhóm; vòng duyệt cả enum cấp cho nó thứ không ai đọc. **Vì:** tập "tất cả X" chỉ gom phần tử cùng hành vi.
**Làm:** tách phần tử khác hành vi ra khỏi tập mà vòng duyệt đi qua (`code-design` › Bất biến).

### Báo giá đổi khoá ngoài repo sau khi đã đổi tên
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** đổi tên field DTO theo yêu cầu rồi mới báo khoá ngoài repo phải đổi cùng build. **Vì:** định danh vừa là tên trong code vừa là khoá ngoài repo thì đổi tên là hai quyết định; máy còn build cũ, cấu hình ngoài phải đổi cùng lần phát hành.
**Làm:** hỏi tách trước khi làm: giữ khoá dưới tên mới, hay đổi khoá kèm giá (`code-design` › Naming; `code-design` › tra cứu › Naming).
