# demo-compare — kinh nghiệm

Bài học đã trả giá khi làm việc với `demo-compare`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### Popup khớp trên điện thoại, tràn hai mép trên tablet
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** Khối toàn màn (popup) khớp demo trên điện thoại, trên tablet tiêu đề và nút tràn hai mép. **Vì:** canvas của nó match khác canvas HUD (popup không match height): khung thấp hơn khối thiết kế.
**Làm:** khối toàn màn thu theo trục chật hơn, `min(1, rộng/thiết kế, cao/thiết kế)`; đọc Match của **đúng canvas chứa khối**, chụp thêm một cỡ tablet.

### Lớp tối, lớp phủ đặt đúng số mà nhạt hơn demo
`2026-11` · vấp 1 · đã đo
**Thấy:** Lớp tối alpha cao mà màn chơi phía sau vẫn rõ hơn demo nhiều; lớp phủ đặt đúng màu và % hoạ sĩ ghi mà nhạt hơn demo. **Vì:** project Linear trộn alpha trước khi đổi về sRGB: alpha 0.88 còn khoảng 38% độ sáng nhìn thấy, không phải 12%; màu + % của hoạ sĩ là phép trộn sRGB.
**Làm:** so độ sáng nền sau lớp tối đo trên cả hai ảnh, không so alpha; lớp phủ thì fit lại trong Linear trên mọi pixel đặc của art bị phủ — chỉ có màu + % hoạ sĩ thì fit cả màu lẫn alpha (`DemoCompare.FitOverlay`), màu phủ đã chốt thì fit alpha (`DemoCompare.FitOverlayAlpha`: alpha sRGB nó trả khớp số hoạ sĩ thì phép đo đúng), kiểm bằng chụp trong Play.

### Hai video demo mang tên của nhau
`2026-11` · vấp 1 · đã đo · đúng với gói demo hoạ sĩ đặt tên tay
**Thấy:** video tên theo vật A quay vật B và ngược lại; đo theo tên là dựng nhịp của hiệu ứng này cho hiệu ứng kia. **Vì:** hoạ sĩ xuất nhiều bản preview rồi đặt tên tay.
**Làm:** `video_demo.py sheet` từng video trước khi đo, gọi video theo nội dung thấy trong lưới; tên lệch thì nói trong báo cáo để developer báo lại hoạ sĩ (`demo-compare` › Demo là video).

### Khoảng cách tới mép màn tưởng là hằng, lệch theo tỉ lệ màn
`2026-11` · vấp 1 · đã đo · đúng với vật world neo mép bằng độ lệch pixel thiết kế, camera đổi trục khớp theo tỉ lệ
**Thấy:** thiết kế chọn một hằng số cho quãng đưa vật ra ngoài mép vì vật "neo theo mép"; đo sáu cỡ màn thì quãng cần bằng nhau ở mọi cỡ rộng hơn tỉ lệ thiết kế, rồi tăng dần ở màn cao hơn. **Vì:** độ lệch pixel thiết kế co giãn theo chiều cao màn, còn camera qua tỉ lệ thiết kế thì giữ bề ngang: lề tính bằng unit lớn dần theo chiều cao. Nhiều cỡ cho cùng một số không chứng minh là hằng khi chúng nằm cùng phía của ngưỡng.
**Làm:** câu "không đổi theo tỉ lệ màn" trong thiết kế là giả định tới khi đo: plan đặt bước đo ở các cỡ nằm hai phía tỉ lệ thiết kế, kèm luật dừng khi lệch quá ngưỡng; hằng số lấy theo cỡ xấu nhất cộng lề, hoặc tính lúc chạy (`workflow` › Nghiệm thu).

### Cỡ màn đọc trong Editor là cỡ cửa sổ, không phải cỡ máy
`2026-10` · vấp 1 · đã đo
**Thấy:** tính bố cục theo `Handles.GetMainGameViewSize()` ra số của cửa sổ Game view. **Vì:** ở Free Aspect hàm trả cỡ cửa sổ đang mở, không phải độ phân giải máy đang giả lập.
**Làm:** đặt Game view về cỡ máy cần đo trước khi đọc, hoặc đo trong Simulator; đọc cỡ hiện tại trước khi tin số (`demo-compare` › Hai chế độ).
