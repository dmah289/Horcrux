# demo-compare — kinh nghiệm

Bài học đã trả giá khi làm việc với `demo-compare`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### Cụm trên cùng tụt sâu trên máy có tai thỏ, Game view vẫn đúng
`2026-11` · vấp 1 · đã đo
**Thấy:** HUD và cụm world cùng neo trong safe area, khớp demo ở Game view, trên máy punch-hole tụt cả cụm hơn trăm pixel xuống dưới dải trống demo đã chừa. **Vì:** demo đã chừa dải trống trên cùng cho thanh trạng thái và lỗ camera; cộng cả inset của máy lên trên dải đó là đếm hai lần.
**Làm:** inset chỉ đẩy phần vượt dải trống thiết kế; HUD và mọi khối world neo cùng mép đọc cùng một quy tắc (`demo-compare` › Vùng an toàn).

### Khối world cách HUD xa dần trên màn rộng hơn demo
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** camera giữ bề rộng, khoảng cách tính bằng unit world: khớp demo ở tỉ lệ demo, trên tablet khối world cách HUD gấp nhiều lần demo; bị nghi là vùng an toàn trong khi Game view cùng cỡ máy đã ra trùng ảnh máy. **Vì:** hai luật co giãn khác nhau trùng đúng ở tỉ lệ demo nên Game view cỡ demo không bao giờ lộ; lệch lớn dần theo tỉ số hai aspect.
**Làm:** camera giữ trọn khung thiết kế, khoảng từ mép tới khối đo bằng pixel thiết kế nhân cùng hệ số canvas; so thêm một màn rộng hơn demo và một màn cao hơn (`demo-compare` › Tỉ lệ màn).

### Ảnh chụp khớp demo, vào chơi thì khối đè HUD
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** Ảnh chụp khớp demo mà vào chơi khối đè HUD. **Vì:** chụp lúc phần tử hiện theo gameplay chưa hiện.
**Làm:** ép hiện với nội dung dài nhất rồi mới đo (`demo-compare` › Hai chế độ).

### Khung object lệch demo vài px dù hình đã khớp
`2026-11` · vấp 1 · đã đo
**Thấy:** Số khung object lệch pixel demo vài px dù hình đã khớp. **Vì:** bounds gồm viền trong suốt và bóng của art.
**Làm:** khung chỉ để tìm cột; số so với demo lấy bằng dải màu cùng cột trên cả hai ảnh.

### Ảnh đè mờ báo lệch hàng chục px, đo thật chỉ vài px
`2026-11` · vấp 1 · đã đo
**Thấy:** Developer đè demo mờ lên Game view thấy lệch hàng chục px, đo thật chỉ vài px. **Vì:** Game view khác tỉ lệ demo.
**Làm:** đọc cỡ Game view trước khi tin ảnh đè; so demo chỉ ở đúng cỡ demo.

### Popup khớp trên điện thoại, tràn hai mép trên tablet
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** Khối toàn màn (popup) khớp demo trên điện thoại, trên tablet tiêu đề và nút tràn hai mép. **Vì:** canvas của nó match khác canvas HUD (popup không match height): khung thấp hơn khối thiết kế.
**Làm:** khối toàn màn thu theo trục chật hơn, `min(1, rộng/thiết kế, cao/thiết kế)`; đọc Match của **đúng canvas chứa khối**, chụp thêm một cỡ tablet.

### Trên tablet, 16:9 vật world nhỏ và xa HUD
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** Tablet hay 16:9: khối world xa HUD, vật world nhỏ hơn vật cùng cỡ ở khối khác. **Vì:** camera giữ một trục khác trục canvas.
**Làm:** camera giữ trọn khung thiết kế, khoảng cách đo pixel thiết kế (mục Tỉ lệ màn) — `demo-compare` › Tỉ lệ màn.

### Lệch đều mọi khối cùng một mép chỉ trên máy
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** Đúng ở Editor, lệch trên máy; lệch đều mọi khối cùng một mép. **Vì:** inset của máy, Game view không có.
**Làm:** tái hiện trên Simulator với file máy đó.

### Kết quả Simulator mang tên máy mới, số của máy cũ
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** Kết quả Simulator mang tên máy mới mà số là của máy cũ. **Vì:** chọn máy hỏng (tên sai) mà vẫn chạy.
**Làm:** runner dừng khi chọn máy hỏng; đọc dòng chọn máy trong kết quả.

### Simulator cùng cỡ mà không giống máy thật
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** Simulator cùng cỡ mà không giống máy. **Vì:** máy mẫu của Unity là máy chung chung, inset và cutout khác máy thật.
**Làm:** thêm file máy thật, kiểm bằng ảnh chụp máy.

### Gradient shader đúng màu hai đầu mà sai nhịp tắt
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** Gradient shader dựng theo demo đúng màu tâm và mép mà sai nhịp tắt. **Vì:** hệ số đo ở sRGB của ảnh, shader nội suy ở Linear.
**Làm:** `DemoCompare.FitRadialGlow` fit theo không gian màu project kèm số lệch mô phỏng; giao số chỉ sau khi chụp trong Play đúng chế độ canvas và `Diff` với demo.

### Lớp tối, lớp phủ đặt đúng số mà nhạt hơn demo
`2026-11` · vấp 1 · đã đo
**Thấy:** Lớp tối alpha cao mà màn chơi phía sau vẫn rõ hơn demo nhiều; lớp phủ đặt đúng màu và % hoạ sĩ ghi mà nhạt hơn demo. **Vì:** project Linear trộn alpha trước khi đổi về sRGB: alpha 0.88 còn khoảng 38% độ sáng nhìn thấy, không phải 12%; màu + % của hoạ sĩ là phép trộn sRGB.
**Làm:** so độ sáng nền sau lớp tối đo trên cả hai ảnh, không so alpha; lớp phủ thì fit lại trong Linear trên mọi pixel đặc của art bị phủ — chỉ có màu + % hoạ sĩ thì fit cả màu lẫn alpha (`DemoCompare.FitOverlay`), màu phủ đã chốt thì fit alpha (`DemoCompare.FitOverlayAlpha`: alpha sRGB nó trả khớp số hoạ sĩ thì phép đo đúng), kiểm bằng chụp trong Play.

### `max` lệch lớn ở mép ảnh dù nền đã khớp
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** `max` lệch lớn ở mép, góc ảnh demo dù nền đã khớp. **Vì:** demo xuất kèm khung viền, mỗi cạnh dày khác nhau.
**Làm:** `Runs` ở mép tìm viền, đưa vào `exclude` trước khi tin số.

### Cỡ màn đọc trong Editor là cỡ cửa sổ, không phải cỡ máy
`2026-10` · vấp 1 · đã đo
**Thấy:** tính bố cục theo `Handles.GetMainGameViewSize()` ra số của cửa sổ Game view. **Vì:** ở Free Aspect hàm trả cỡ cửa sổ đang mở, không phải độ phân giải máy đang giả lập.
**Làm:** đặt Game view về cỡ máy cần đo trước khi đọc, hoặc đo trong Simulator; đọc cỡ hiện tại trước khi tin số (`demo-compare` › Hai chế độ).

### Đổi cỡ Game view bật thêm cửa sổ Game nổi
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** Đổi cỡ Game view thì bật thêm một cửa sổ Game nổi. **Vì:** Simulator đang chiếm chỗ view Play.
**Làm:** công cụ đổi cỡ tự đưa Game view về; swap tay thì trả lại.
