# async-presentation — kinh nghiệm

Bài học đã trả giá khi làm việc với `async-presentation`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### Bước sau lượt chờ chết im lặng chỉ khi một điều kiện mở
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** lượt chờ chỉ có trong một điều kiện (từ một mốc tiến độ) — ngoài điều kiện hàm chờ trả ngay trước khi caller huỷ, lỗi chỉ lộ khi điều kiện mở. **Vì:** caller tự huỷ khi đóng mà còn chờ bằng token của chính nó thì bước sau chết im lặng; hàm chờ trả ngay che mất điều đó.
**Làm:** bước sau một lượt chờ thuộc chủ sống qua cả lượt; cặp "chờ rồi làm" vào chủ đời dài, caller gọi một hàm (`async-presentation` › Async và vòng đời).

### Vật đã biến mất hiện lại rỗng lúc nạp màn
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** đường dọn màn đặt lại cỡ nghỉ cho vật đã co biến mất mà không tắt nó, vật rỗng hiện lại lúc nạp màn. **Vì:** đường dọn từ ngoài không về đúng trạng thái cuối của đường ra tự nhiên.
**Làm:** đường dọn từ ngoài về đúng trạng thái cuối của đường ra tự nhiên — vật đã biến mất thì cũng tắt (`async-presentation` › Async và vòng đời).

### Vật diễn xong rồi đứng kẹt vì chỗ đã bị lấy
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** kiểm chỗ trống trước nhịp lấy đà; trong nhịp đó luồng khác lấy mất chỗ, vật vẫn diễn rồi đứng kẹt. **Vì:** điều kiện kiểm trước một lượt chờ là bản chết sau lượt chờ.
**Làm:** chỗ đã nhắm kiểm lại ngay trước bước người chơi thấy; mất rồi thì bỏ bước, không diễn nửa chừng (`async-presentation` › Async và vòng đời).

### Trạng thái tạm kẹt lại sau khi lượt diễn bị huỷ
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** lượt diễn huỷ trước bước bật cờ ngoài, `finally` đọc cờ đó thành "đã bàn giao", trạng thái tạm kẹt lại. **Vì:** nghĩa vụ trả trạng thái tạm đi theo bên đang cầm; cờ ngoài không nói bên này đã qua mốc bàn giao chưa.
**Làm:** `finally` quyết theo biến cục bộ ghi ngay sau mốc bàn giao; trả theo thứ tự ngược lúc dựng (`async-presentation` › Async và vòng đời).

### Canvas lồng giữ sorting đã nâng sau khi trả
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** tắt canvas lồng rồi mới trả sorting, canvas giữ sorting đã nâng. **Vì:** `overrideSorting`, `sortingOrder` đặt lên canvas lồng đang tắt không ăn.
**Làm:** bật rồi mới đặt, trả rồi mới tắt (`async-presentation` › tra cứu › Renderer và chữ).

### Nhún xong vật đổi cỡ so với lúc nghỉ
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** nhún quanh scale 1 trên node scale khác 1 — co vĩnh viễn · gán scale 0 rồi mới bật object tắt sẵn — `Awake` chụp nghỉ là 0. **Vì:** tween không nhân, cộng trên giá trị nghỉ thật của node; giá trị nghỉ chụp ở `Awake` lấy đúng thứ đang có lúc object bật.
**Làm:** giá trị nghỉ đọc ở `Awake`; tween nhân hoặc cộng trên giá trị nghỉ, reset về nghỉ, không về `one`/`zero` (`async-presentation` › Trình diễn một thay đổi; `async-presentation` › tra cứu › Trình diễn).

### Màn kết che mất cú diễn cuối
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** delay mở màn kết bị override ngắn hơn nhịp vật cuối, màn kết che mất cú diễn cuối. **Vì:** bước kế bắt đầu theo delay cố định, không theo sự kiện nhìn thấy.
**Làm:** kết cục ghi sự thật ngay, màn kết bắt đầu theo sự kiện nhìn thấy, không theo delay cố định (`async-presentation` › Trình diễn một thay đổi).

### Nhạc nền bật lại ở mỗi màn
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** nhạc nền gắn vào lúc vào một màn, track đặt tên theo màn đó. **Vì:** phản hồi kéo dài (nhạc, tiếng loop) thuộc chủ có cùng đời; màn ngắn đời hơn nhạc suốt phiên.
**Làm:** nhạc suốt phiên phát từ thứ sống suốt phiên, không bật lại ở mỗi màn; bắt đầu, dừng lúc nào thì hỏi (`async-presentation` › tra cứu › Trình diễn).
