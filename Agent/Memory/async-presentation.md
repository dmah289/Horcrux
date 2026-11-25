# async-presentation — kinh nghiệm

Bài học đã trả giá khi làm việc với `async-presentation`. Đọc trọn cùng lúc với rule hoặc skill đó. Hình dạng và vòng đời ở `horcrux.md` › Memory.

### Bước sau lượt chờ chết im lặng chỉ khi một điều kiện mở
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** lượt chờ chỉ có trong một điều kiện (từ một mốc tiến độ) — ngoài điều kiện hàm chờ trả ngay trước khi caller huỷ, lỗi chỉ lộ khi điều kiện mở. **Vì:** caller tự huỷ khi đóng mà còn chờ bằng token của chính nó thì bước sau chết im lặng; hàm chờ trả ngay che mất điều đó.
**Làm:** bước sau một lượt chờ thuộc chủ sống qua cả lượt; cặp "chờ rồi làm" vào chủ đời dài, caller gọi một hàm (`async-presentation` › Async và vòng đời).

### Nhún xong vật đổi cỡ so với lúc nghỉ
`2026-11` · vấp 1 · chuyển sang, chưa kiểm lại
**Thấy:** nhún quanh scale 1 trên node scale khác 1 — co vĩnh viễn · gán scale 0 rồi mới bật object tắt sẵn — `Awake` chụp nghỉ là 0. **Vì:** tween không nhân, cộng trên giá trị nghỉ thật của node; giá trị nghỉ chụp ở `Awake` lấy đúng thứ đang có lúc object bật.
**Làm:** giá trị nghỉ đọc ở `Awake`; tween nhân hoặc cộng trên giá trị nghỉ, reset về nghỉ, không về `one`/`zero` (`async-presentation` › Trình diễn một thay đổi; `async-presentation` › tra cứu › Trình diễn).

### Port DOTween sang PrimeTween đúng số mà nhịp khác
`2026-10` · vấp 1 · đã đo · đúng với dự án chuyển từ DOTween mà phải giữ cảm giác
**Thấy:** chép thời lượng, ease theo call site DOTween mà chuyển động, độ trễ khác bản cũ; PrimeTween cảnh báo tween bắt đầu ở đích. **Vì:** cảm giác cũ nằm trong mặc định ẩn của DOTween, không ở call site: không có settings asset thì ease `OutQuad`, scaled time, autoKill · `DOVirtual.DelayedCall` bỏ qua `timeScale` · `SetRelative` tính đích lúc tween bắt đầu · DOTween im lặng khi giá trị đầu bằng đích.
**Làm:** ghi ease tường minh · delay phải chạy khi `timeScale` = 0 thì `useUnscaledTime` · `SetRelative` → tính đích lúc tạo, khi không gì dời vật trước lúc tween chạy · Join/Append/Insert → `Sequence.Insert(atTime, …)` thời điểm tường minh; hai tween nối bằng `OnComplete` → `Chain` · `Kill` → `Stop` · `SetEase(OutBack, s)` → `Easing.Overshoot(s / 1.70158f)`; InBack có tham số → `Tween.Custom` với `t*t*((s+1)*t - s)` · punch, shake không có bản khớp đúng: theo adapter DOTween của package, `frequency = (int)(vibrato * 1.35f)`, `asymmetryFactor = 1 - elasticity` · truyền giá trị đầu tường minh, đã ở đích thì bỏ tween (`async-presentation` › tra cứu › Stack async).

### Sequence lặp nháy nhiều gấp đôi hoặc nửa số định
`2026-11` · vấp 1 · đọc tài liệu · đúng với PrimeTween
**Thấy:** đặt `cycles` theo số lần nháy muốn thấy mà ra gấp đôi hoặc nửa. **Vì:** `CycleMode.Restart` đếm mỗi vòng là một lần nháy; `Yoyo` đếm đi và về là hai vòng.
**Làm:** đổi số lần mắt thấy ra `cycles` theo đúng chế độ lặp, kiểm bằng `stat` của trace (`feel-tune` › Chọn công cụ).

### Key Addressables là chuỗi đến từ remote hay save
`2026-11` · vấp 1 · đã đo
**Thấy:** tên asset cần nạp đến từ server nên không kéo được `AssetReference`; nạp bằng chuỗi thì tên sai ném `InvalidKeyException`, đổi tên asset làm giá trị trên console trỏ vào khoảng không. **Vì:** address không đổi theo tên asset, chuỗi chỉ hỏng lúc chạy.
**Làm:** tên hợp lệ lấy theo label, tên lạ `LogError` rồi về bản mặc định là `AssetReference` kéo thả · nạp bản mới xong mới `Release` bản cũ, mọi bản chung một group Pack Together (bật GUID in Catalog cho bản mặc định) để phụ thuộc chung không nhân bản, không bị nhả lúc đổi · phép kiểm authoring canh address = tên, label, group, schema (`async-presentation` › tra cứu › Stack async).

### FPS tụt hẳn sau khi app Android quay lại từ nền, không tự hồi
`2026-11` · vấp 1 · đọc tài liệu · đúng với Android màn 90/120Hz, Unity 6, Optimized Frame Pacing bật
**Thấy:** `targetFrameRate` vẫn 60, nằm nền vài giây rồi quay lại thì bộ đếm báo 30–45 FPS tới khi tắt app. **Vì:** nhịp FPS trên Android là ước số của tần số quét (120Hz → 60/40/30, 90Hz → 45); sau resume Unity và Swappy có thể chốt chu kỳ vsync khác — lỗi Unity còn mở "targetFrameRate changes after locking/unlocking the screen". Gán lại đúng số cũ có thể bị bỏ qua.
**Làm:** một chủ giữ frame rate, khi về foreground trả `-1` một frame rồi xin lại số mục tiêu (`Plans/Horcrux_FrameRate_Plan.md`, chờ gõ); vẫn tụt thì thử tắt Optimized Frame Pacing. Cách xin lại chưa kiểm trên máy: nghiệm thu trên máy 90/120Hz bằng bộ đếm FPS.
