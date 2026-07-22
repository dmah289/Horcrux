# PhysXHelper — Pending Utilities

Danh sách các static utility class dự kiến bổ sung cho `Horcrux.Runtime.Utilities.PhysXHelper`.
Tất cả phải tuân thủ: **zero-GC** (thuần tính toán `float`/`struct`, không alloc/LINQ/closure trong hot path), self-documenting naming, XML doc đầy đủ, SOLID.

Đã có:
- `HarmonicOscillator` (dao động điều hòa đơn giản, Sin/Cos).
- `Easing` — ✅ **đã triển khai** dưới dạng `Easer` ở namespace riêng `Horcrux.Runtime.Tweening.Easing` (10 họ Quad…Bounce × In/Out/InOut + Linear; entry point `Easer.Evaluate(EaseType, t)`). Mọi tham chiếu `← Easing` phía dưới trỏ tới class này — **không** làm lại trong `PhysXHelper`.

---

## Nhóm 1 — Dao động & chuyển động tuần hoàn

### `DampedOscillator` — dao động tắt dần
- Công thức: `x = A · e^(−λt) · cos(ω·t + ϕ)`.
- Ứng dụng: UI pop-in, camera shake giảm dần, vật thể rung khi va chạm.

### `SpringDamper` — lò xo số học (semi-implicit / analytic spring)  ⭐ ưu tiên cao
- Kéo một giá trị về target mượt theo vật lý lò xo (stiffness + damping).
- Dùng nhiều nhất trong game thực tế: follow camera, drag UI, procedural animation.
- Thay thế `Mathf.SmoothDamp` với kiểm soát tốt hơn.

### `Pendulum` — con lắc
- Góc dao động theo thời gian.

---

## Nhóm 2 — Chuyển động ném / đạn đạo

### `Projectile` / `Ballistics`
- Vị trí tại thời điểm `t`: `p = p0 + v0·t + ½·g·t²`.
- Tính launch velocity để trúng target (giải phương trình đạn đạo), thời gian bay, đỉnh parabol, tầm xa.
- Ứng dụng: ném lựu đạn, cung tên, dự đoán quỹ đạo AI.

---

## Nhóm 3 — Easing & nội suy

### ~~`Easing`~~ — ✅ ĐÃ XONG (xem `Horcrux.Runtime.Tweening.Easing.Easer`)
- Bộ hàm easing đầy đủ 10 họ (Quad…Bounce × In/Out/InOut) + Linear; đầu vào `t∈[0,1]`, đầu ra đã cong (Back/Elastic có thể overshoot → dùng `LerpUnclamped`).
- Đã tách file riêng theo họ trong `Tweening/Easings/Curves/`, entry point `Easer.Evaluate(EaseType, t)`. **Không** cần bản sao trong `PhysXHelper`.

### `InterpolationHelper`  ⭐ ưu tiên cao
- `Remap(value, inMin, inMax, outMin, outMax)`, `SmoothStep`, `SmootherStep`.
- Spring-lerp, exponential decay lerp độc lập framerate: `1 − e^(−k·dt)`.

---

## Nhóm 4 — Toán hình học / vector

### `GeometryHelper`
- Điểm gần nhất trên đoạn thẳng, khoảng cách điểm↔line, giao điểm 2 đoạn, point-in-polygon, project vector lên plane.

### `AngleHelper`
- Chuẩn hóa góc về `[−180, 180]`, `ShortestAngleDelta`, xoay vector 2D, direction↔angle.

---

## Nhóm 5 — Ngẫu nhiên có chủ đích

### `RandomHelper`
- Random point trong/trên hình cầu-đĩa, weighted random, gaussian random, jitter, shuffle in-place (zero-GC).

---

## Nhóm 6 — Game Juice / ASMR Feel

Mục tiêu: tạo cảm giác "đã tay, đã mắt, đã tai" (satisfying feedback). Nhiều class ở đây tái sử dụng `HarmonicOscillator`, `DampedOscillator`, `SpringDamper`, `Easing`.

### A. Juice thị giác

#### `SquashStretch` ⭐ ưu tiên cao — biến dạng nén/giãn
- Giữ nguyên thể tích: nén theo Y thì phình theo X (`scaleX = 1/√scaleY`).
- Ứng dụng: nhân vật nhảy/đáp đất, nút bấm, item pickup. Bí quyết "sống động" như jelly.

#### `Wobble` / `Jelly` ⭐ ưu tiên cao — rung rinh như thạch
- Kết hợp `DampedOscillator` tạo hiệu ứng lắc lư tắt dần sau khi chạm/thả. Rất ASMR.

#### `Overshoot` — vọt lố rồi ổn định
- Animation chạy quá target một chút rồi bật về (EaseOutBack). Cảm giác "bén", có lực.

#### `Pulse` / `Breathing` — nhịp thở
- Scale/alpha dao động nhẹ liên tục (dùng `HarmonicOscillator`). Làm UI/collectible "thở", hút mắt.

#### `ColorFlash` — nháy màu khi hit
- Blend nhanh về trắng rồi trả lại. Feedback va chạm tức thì.

### B. Juice chuyển động

#### `Recoil` / `Kickback` — giật lùi
- Đẩy nhanh theo hướng ngược rồi spring về (dùng `SpringDamper`). Súng bắn, đấm, đẩy.

#### `Shake` ⭐ ưu tiên cao — rung màn hình / vật thể
- Rung theo **Perlin noise** (mượt, không giật cục), biên độ tắt dần.
- **Trauma-based shake** (biên độ = trauma²) — kỹ thuật kinh điển của game feel.

#### `Anticipation` — lấy đà
- Lùi nhẹ trước khi bung ra (co người trước khi nhảy). Tạo "trọng lượng".

### C. Juice thời gian

#### `Hitstop` / `FreezeFrame` ⭐ ưu tiên cao — khựng thời gian khi va chạm
- Dừng/làm chậm cực ngắn (vài chục ms) lúc đòn trúng. Tạo cảm giác "nặng đô" nhất trong combat.

#### `TimeScaleHelper` — slow-mo / ramp
- Ease timeScale mượt vào/ra slow motion.

### D. Juice âm thanh (ASMR thực thụ)

#### `AudioPitchHelper` ⭐ ưu tiên cao — biến điệu cao độ
- **Pitch ramp**: chuỗi hành động liên tiếp tăng dần pitch (combo counter, nhặt coin liên hoàn) — gây nghiện.
- Random pitch nhẹ (±semitone) chống lặp âm nhàm chán.

#### `AudioFeedback` — mapping cường độ va chạm → âm lượng/pitch.

### Combo "ASMR đã nhất"
Kết hợp 3 thứ cùng lúc trong 1 sự kiện:
> **SquashStretch + Hitstop + AudioPitchHelper (pitch ramp)**

Ví dụ nhặt coin liên hoàn: coin nảy squash-stretch → khựng 30ms → tiếng "ting" pitch tăng dần. Công thức juice kinh điển.

---

## Nhóm 7 — Puzzle ASMR Feel

ASMR đặc thù cho game puzzle: đến từ **tactile (chạm), order (trật tự), release (giải tỏa)**. Đặc biệt hợp hướng `falling_sand`. Nhiều class tái sử dụng `SpringDamper`, `Easing`, `DampedOscillator`, `AudioPitchHelper`.

### E. Tactile — chạm & đặt

#### `MagneticSnap` ⭐ ưu tiên cao — hút dính vào ô/lưới
- Khi piece đến gần slot đúng, lực hút tăng phi tuyến kéo nó "khục" vào chỗ (`SpringDamper` + easing). Cảm giác nam châm cực đã.

#### `GridSnapFeedback` — phản hồi lúc snap
- Micro squash + micro shake + tick sound đúng khoảnh khắc chạm ô. "Click" tactile.

#### `DragResistance` / `ElasticDrag` — kéo có độ trễ
- Piece theo ngón tay hơi trễ với spring (như kéo qua gel). Cảm giác "có trọng lượng".

#### `RubberBandPull` — kéo căng dây
- Đường nối căng/chùng theo tension. ASMR cho puzzle nối (connect-the-dots).

### F. Cascade & chuỗi phản ứng

#### `StaggerHelper` / `RippleDelay` ⭐ ưu tiên cao — làm trễ theo sóng
- `delay = dist × factor` từ tâm. Hiệu ứng lan tỏa gợn sóng khi clear cụm. Xương sống của mọi cascade satisfying.

#### `ChainReaction` ⭐ ưu tiên cao — combo lan truyền
- Chuỗi kích hoạt liên tiếp + pitch ramp tăng dần (nối `AudioPitchHelper`). Cảm giác đổ domino gây nghiện.

#### `Cascade` / `FallSettle` — rơi & lắng xuống
- Vật rơi lấp chỗ trống, đáp đất với squash + wobble tắt dần. Rất hợp `falling_sand`.

### G. Falling sand / granular (đặc sản branch hiện tại)

#### `GranularSettle` ⭐ ưu tiên cao — hạt lắng đọng
- Cát/hạt trượt và ổn định dần thành đống. Micro-jitter giảm dần theo thời gian.

#### `FlowFeedback` — dòng chảy hạt
- Density → âm lượng/pitch tiếng "rào rào". Càng nhiều hạt chảy, tiếng càng đầy. Cực ASMR.

#### `PileGrowth` — đống lớn dần
- Feedback theo độ cao/khối lượng đống hạt.

### H. Giải tỏa & trật tự (dopamine của puzzle)

#### `SatisfyingClear` ⭐ ưu tiên cao — khoảnh khắc clear
- Combo: flash → stagger pop → suck-in về tâm → burst. Đỉnh điểm giải tỏa khi hoàn thành hàng/cụm.

#### `ProgressPop` — nảy khi tiến triển
- Thanh progress/counter nảy nhẹ (overshoot) mỗi bước. Cảm giác "đang tiến".

#### `SortSettle` — sắp xếp về đúng chỗ
- Khi phân loại đúng, các phần tử trượt mượt về hàng ngay ngắn (stagger + ease). ASMR "gọn gàng".

#### `CompletionSequence` — chuỗi thắng màn
- Dàn feedback tuần tự khi giải xong: sáng dần, âm thanh crescendo, particle.

### Combo "ASMR puzzle đã nhất"
> **MagneticSnap + GridSnapFeedback (tick) + StaggerHelper (clear chuỗi) + pitch ramp**

Ví dụ đặt piece hoàn thành hàng: piece hút "khục" vào ô → tick → cả hàng pop lan sóng từ điểm đặt → pitch tăng dần → suck-in giải tỏa.

---

## Nhóm 8 — Camera / Haptic / VFX / Ambient Feel

Các mảng bổ sung mở rộng cảm giác ra ngoài đối tượng: camera, xúc giác (mobile), VFX động, số liệu, môi trường nền. Nhiều class tái sử dụng `SpringDamper`, `HarmonicOscillator`, `Easing`, `AudioPitchHelper`.

### I. Camera Feel

#### `CameraPunch` / `ZoomPunch` ⭐ ưu tiên cao — giật zoom
- Zoom vào/ra nhanh rồi spring về khi impact. "Đấm" vào cảm giác cực mạnh.

#### `LookAhead` / `CameraLead` — nhìn trước hướng di chuyển
- Camera lệch nhẹ theo hướng player đi (spring). Cảm giác "có dự đoán".

#### `CameraFollowSmooth` — bám mượt có deadzone
- Dùng `SpringDamper`, có vùng chết để không rung khi đứng yên.

#### `DollyZoom` / `FOVKick` — hiệu ứng Vertigo, đổi FOV theo tốc độ.

### J. Haptic / Rung (ASMR xúc giác trên mobile)

#### `HapticHelper` ⭐ ưu tiên cao — rung theo ngữ cảnh
- Wrap các pattern rung (light/medium/heavy/success/warning). Đồng bộ haptic + visual + audio = ASMR đa giác quan hoàn chỉnh trên điện thoại. Mảnh còn thiếu quan trọng nhất.

#### `HapticPattern` — chuỗi rung nhịp điệu
- Pitch ramp phiên bản xúc giác: combo rung tăng dần cường độ.

### K. VFX động (thuần toán, feed cho shader/particle)

#### `TrailFeedback` / `AfterImage` — vệt mờ theo tốc độ
- Độ đậm/độ dài vệt tỉ lệ với vận tốc. Cảm giác "phóng".

#### `RippleEffect` ⭐ ưu tiên cao — gợn sóng lan tỏa
- Sóng tròn lan từ điểm chạm (`sin(dist − t)` giảm dần). Nước, click, impact ground. Rất ASMR.

#### `DissolveFeedback` — tan biến/hiện dần theo noise threshold.

#### `GradientCycle` — chuyển màu tuần hoàn mượt (dùng `HarmonicOscillator` cho hue).

### L. Số liệu & phản hồi thông tin (dopamine trực tiếp)

#### `CountUpAnimator` ⭐ ưu tiên cao — số nhảy tăng dần
- Score/coin đếm lên mượt (ease-out) thay vì nhảy phựt. Cảm giác "tích lũy" gây nghiện.

#### `FloatingText` — số bay lên rồi tan
- Damage/điểm bật lên với overshoot + fade. Feedback tức thì.

#### `ComboMeter` — thanh combo phồng/co theo streak.

### M. Môi trường sống động (ambient juice — nền ASMR)

#### `ProceduralSway` ⭐ ưu tiên cao — đung đưa tự nhiên
- Cây cỏ/vật thể lắc theo noise nhiều tần số (không lặp cứng). Nền sống động, thư giãn.

#### `IdleBreathe` — vật thể/nhân vật "thở" khi đứng yên (nâng cấp `Pulse`).

#### `ParallaxHelper` — lớp nền trôi theo camera tạo chiều sâu.

#### `AmbientDrift` — trôi lững lờ (mây, bụi, bong bóng) bằng noise 2D.

### Combo "đa giác quan hoàn hảo"
> **CameraPunch + HapticHelper + RippleEffect + AudioPitchHelper**

Một cú chạm mà màn hình giật zoom + điện thoại rung + sóng lan ra + âm thanh đồng bộ trong ~50ms → đỉnh cao game feel.

---

## Roadmap triển khai theo tầng phụ thuộc

Nguyên tắc: **mỗi tầng chỉ phụ thuộc các tầng dưới nó**. Làm đúng thứ tự này → tầng sau luôn tái sử dụng tầng trước, không phải quay lại sửa nền. Ký hiệu `←` = "phụ thuộc / tái sử dụng".

### Tầng 0 — Nền toán học thuần (zero dependency)
Làm trước tiên vì mọi thứ khác đều gọi tới. Thuần `float`/`struct`, không phụ thuộc nhau.
1. `Easing` — ✅ **đã xong** (`Tweening.Easing.Easer`). Nền của mọi animation.
2. `HarmonicOscillator` — ✅ đã có.
3. **`InterpolationHelper`** ⭐ — `Remap`, `SmoothStep`, exp-decay lerp độc lập framerate. **Item nền còn lại được dùng nhiều nhất → làm kế tiếp.**
4. **`RandomHelper`** — gaussian, weighted, jitter, shuffle (dùng cho shake/granular).
5. **`GeometryHelper`** — khoảng cách, closest-point, giao điểm (dùng cho stagger/snap).
6. **`AngleHelper`** — chuẩn hóa góc, shortest-delta, xoay vector 2D.

### Tầng 1 — Nguyên hàm vật lý (chỉ ← Tầng 0)
Các "động cơ" chuyển động mà lớp juice sẽ nhờ tới.
7. **`SpringDamper`** ⭐ — động cơ lò xo. Được dùng lại nhiều nhất (camera, snap, drag, recoil).
8. **`DampedOscillator`** — dao động tắt dần ← `HarmonicOscillator`. Nền của wobble/granular.
9. **`Projectile` / `Ballistics`** — đạn đạo (độc lập, ← toán Tầng 0).
10. **`Pendulum`** ← `HarmonicOscillator`.

### Tầng 2 — Juice nguyên tử (← Tầng 0–1)
Hiệu ứng đơn lẻ, là "viên gạch" cho các combo tầng trên.
11. **`SquashStretch`** ⭐ ← `Easing`. Viên gạch thị giác dùng khắp nơi.
12. **`Overshoot`** ← `Easing` (EaseOutBack). Dùng cho pop/progress/floating text.
13. **`Wobble` / `Jelly`** ← `DampedOscillator`.
14. **`Pulse` / `Breathing`** ← `HarmonicOscillator`.
15. **`Shake`** ⭐ (trauma-based) ← `RandomHelper`/Perlin + `DampedOscillator`.
16. **`ColorFlash`** ← `InterpolationHelper`.
17. **`TimeScaleHelper`** ← `Easing`.
18. **`AudioPitchHelper`** ⭐ (pitch ramp) ← `InterpolationHelper`.
19. **`HapticHelper`** ⭐ — wrapper rung (độc lập, mảnh đa giác quan còn thiếu).
20. **`StaggerHelper` / `RippleDelay`** ⭐ ← `GeometryHelper` (delay theo khoảng cách).

### Tầng 3 — Hành vi tổng hợp (← Tầng 0–2)
Mỗi class ghép vài viên gạch tầng 2 thành một hành vi hoàn chỉnh.
21. **`Hitstop` / `FreezeFrame`** ⭐ ← `TimeScaleHelper`.
22. **`CameraFollowSmooth`** ← `SpringDamper` (+ deadzone).
23. **`CameraPunch` / `ZoomPunch`** ⭐ ← `SpringDamper`/`Overshoot`.
24. **`LookAhead`** ← `SpringDamper`.
25. **`MagneticSnap`** ⭐ ← `SpringDamper` + `Easing`.
26. **`GridSnapFeedback`** ← `SquashStretch` + `Shake` + `AudioPitchHelper`/`HapticHelper` (tick).
27. **`DragResistance` / `ElasticDrag`** ← `SpringDamper`.
28. **`RubberBandPull`** ← `SpringDamper` (tension).
29. **`Recoil` / `Kickback`** ← `SpringDamper`.
30. **`GranularSettle`** ⭐ ← `DampedOscillator` + `RandomHelper` (jitter tắt dần). Đặc sản falling_sand.
31. **`Cascade` / `FallSettle`** ← `SquashStretch` + `Wobble` + `StaggerHelper`.
32. **`RippleEffect`** ⭐ ← `HarmonicOscillator` + `InterpolationHelper`.
33. **`CountUpAnimator`** ⭐ ← `Easing`/`InterpolationHelper`.
34. **`FloatingText`** ← `Overshoot` + fade.
35. **`ProgressPop`** ← `Overshoot`.
36. **`SortSettle`** ← `StaggerHelper` + `Easing`.
37. **`ProceduralSway`** ⭐ ← noise nhiều tần số (Perlin).
38. **`IdleBreathe`** ← `Pulse`.
39. **`HapticPattern`** ← `HapticHelper` (chuỗi rung).
40. Phụ trợ VFX/ambient: `TrailFeedback`, `DissolveFeedback`, `GradientCycle`, `FlowFeedback`, `PileGrowth`, `ParallaxHelper`, `AmbientDrift`, `Anticipation`, `DollyZoom`, `ComboMeter`.

### Tầng 4 — Orchestrator (← mọi tầng dưới)
Dàn dựng nhiều hiệu ứng thành "sequence" — làm cuối cùng vì cần tất cả nguyên liệu.
41. **`ChainReaction`** ⭐ ← `StaggerHelper` + `AudioPitchHelper` (+ pitch ramp).
42. **`SatisfyingClear`** ⭐ ← `ColorFlash` + `StaggerHelper` + suck-in (`Easing`) + burst.
43. **`CompletionSequence`** ← nhiều class: crescendo âm thanh + particle + camera + haptic.

---

### Đường đi ngắn nhất tới "bản demo ASMR đã tay" cho falling_sand
Nếu muốn thấy kết quả sớm nhất, làm theo lát cắt dọc này (mỗi bước đều chạy được):
~~`Easing`~~ ✅ → **`SpringDamper`** (bước kế tiếp) → `SquashStretch` → `Shake` → `AudioPitchHelper` + `HapticHelper` → `StaggerHelper` → `MagneticSnap` → `GranularSettle` → `SatisfyingClear`.
