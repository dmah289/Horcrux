# PhysXHelper — Pending Utilities

Danh sách các static utility class dự kiến bổ sung cho `Horcrux.Runtime.Utilities.PhysXHelper`.
Tất cả phải tuân thủ: **zero-GC** (thuần tính toán `float`/`struct`, không alloc/LINQ/closure trong hot path), self-documenting naming, XML doc đầy đủ, SOLID.

> **Item combo-exclusive đã chuyển đi:** `ComboMeter` · `ChainReaction` · `HapticPattern` (ramp) và 2 hộp công thức "Combo ASMR" → xem `Runtime/Implementations/Composites/Combo/ComboSystem.md` § *Nguyên liệu đã chuyển từ Pendings.md*. Các item **dùng chung** vẫn ở đây, chỉ thêm nhãn trỏ plan.

Đã có:
- `HarmonicOscillator` (dao động điều hòa đơn giản, Sin/Cos).
- `SquashStretch` — ✅ **đã triển khai** (`Horcrux.Runtime.Utilities.PhysXHelper.SquashStretch`): `GetVolumePreservingScale`, `GetSquashFromImpact`, `GetDirectionalStretch`, `GetSquashStretch`.
- `AudioPitchHelper` — ✅ **đã triển khai** (`Horcrux.Runtime.Utilities.AudioHelper`): `SemitonesToRatio`, `GetRampedPitch`, `GetDetunedPitch`.
- `DampedOscillator` — ✅ **đã triển khai** (envelope · displacement · velocity · settling-time, bản `decay` và bản `halfLife`).
- `GeometryHelper` — ⚠️ **một phần**: hiện chỉ có `RandomPointInAnnulus`/`RandomPointIn3DAnnulus`. Phần khoảng cách/closest-point (cần cho `StaggerHelper`) **chưa có**.
- `Easing` — ✅ **đã triển khai** dưới dạng `Easer` ở namespace riêng `Horcrux.Runtime.Tweening.Easing` (10 họ Quad…Bounce × In/Out/InOut + Linear; entry point `Easer.Evaluate(EaseType, t)`). Mọi tham chiếu `← Easing` phía dưới trỏ tới class này — **không** làm lại trong `PhysXHelper`.
- `InterpolationHelper` — ✅ **đã triển khai** dưới dạng `Interpolator` (`Horcrux.Runtime.Utilities.PhysXHelper`). Đã có: `InverseLerpUnclamped(+Precomputed)`, `Remap(+Precomputed)`, `SmootherStep` (quintic 6t⁵−15t⁴+10t³), `ExpDecay`/`DecayFactor` (`1−e^(−k·dt)`), `ExpDecayHalfLife(+Precomputed)`. Còn thiếu (bổ sung sau nếu cần): `SmoothStep` cubic (3t²−2t³). Mọi tham chiếu `← InterpolationHelper` phía dưới trỏ tới class này.

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

### ~~`InterpolationHelper`~~ — ✅ ĐÃ XONG (xem `Interpolator`)
- `Remap(+Precomputed)`, `InverseLerpUnclamped(+Precomputed)`, `SmootherStep` (quintic), exp-decay lerp độc lập framerate `1 − e^(−k·dt)` (`ExpDecay`/`DecayFactor`/`ExpDecayHalfLife`).
- Còn thiếu: `SmoothStep` cubic (`3t²−2t³`) — bổ sung sau nếu cần.

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

#### ~~`SquashStretch`~~ — ✅ **ĐÃ XONG** (`PhysXHelper/SquashStretch.cs`)
- Giữ nguyên thể tích: nén theo Y thì phình theo X (`scaleX = 1/√scaleY`).
- Ứng dụng: nhân vật nhảy/đáp đất, nút bấm, item pickup. Bí quyết "sống động" như jelly.
- Đang được dùng bởi: `ComboMeter` (cú nảy mỗi nhịp) — `ComboSystem.md` Task 8.

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
- 📄 **Đã có plan:** toán ở `TraumaShake.cs`, driver ở `CameraPunchChannel` → `Implementations/Composites/Feedback/FeedbackSystem.md` (Task 1, Task 6).

#### `Anticipation` — lấy đà
- Lùi nhẹ trước khi bung ra (co người trước khi nhảy). Tạo "trọng lượng".

### C. Juice thời gian

#### `Hitstop` / `FreezeFrame` ⭐ ưu tiên cao — khựng thời gian khi va chạm
- Dừng/làm chậm cực ngắn (vài chục ms) lúc đòn trúng. Tạo cảm giác "nặng đô" nhất trong combat.
- 📄 **Đã có plan:** `HitstopChannel` → `FeedbackSystem.md` Task 5. Lịch `timeScale` 2 pha **inline trong kênh** (chưa tách file — chưa có user thứ hai).

#### `TimeScaleHelper` — slow-mo / ramp
- Ease timeScale mượt vào/ra slow motion.
- ⏳ **Chưa làm.** Phần lịch 2 pha đã có sẵn (inline trong `HitstopChannel`); khi làm slow-mo dài thì **tách nó ra đây** rồi cho cả hai dùng chung, cộng API `Begin/End` ref-count (hitstop là cue một-lần, slow-mo là trạng thái có vào/ra).

### D. Juice âm thanh (ASMR thực thụ)

#### ~~`AudioPitchHelper`~~ — ✅ **ĐÃ XONG** (`Utilities/AudioHelper/AudioPitchHelper.cs`)
- **Pitch ramp**: `GetRampedPitch(step, semitonesPerStep, maxSemitones)` — chuỗi hành động liên tiếp tăng dần pitch.
- Random pitch nhẹ: `GetDetunedPitch(signedUnit, rangeSemitones)` chống lặp âm nhàm chán.
- Đang được dùng bởi: `AudioPitchRampChannel` (`FeedbackSystem.md` Task 5), là **xương sống thính giác của combo**.

#### `AudioFeedback` — mapping cường độ va chạm → âm lượng/pitch.
- Một phần đã có: `AudioCatalog` khai `VolumeRange`/`PitchSemitoneRange` per-entry → `Implementations/Foundations/Audio/AudioSystem.md`.

### ~~Combo "ASMR đã nhất"~~ — ✅ đã chuyển
> Công thức **SquashStretch + Hitstop + pitch ramp** đã chuyển sang `Implementations/Composites/Combo/ComboSystem.md` § *Nguyên liệu đã chuyển từ Pendings.md* — nơi nó được hiện thực bằng **một cue duy nhất** có mặt trong 3 bảng cue.

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

#### ~~`ChainReaction`~~ — ✅ **đã chuyển sang `ComboSystem.md`** § *Giai đoạn 2*
- Chuỗi kích hoạt liên tiếp + pitch ramp tăng dần. Cảm giác đổ domino gây nghiện.
- Thiết kế đầy đủ (`IStaggerPolicy` + `ChainReactionSequencer`) và **3 lý do hoãn** đã ghi ở đó. Điều kiện bắt đầu: có `StaggerHelper` + một board thật làm caller.

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
>
> *(Giữ ở đây vì trọng tâm là **snap/đặt piece**, không phải combo. Phần "clear chuỗi + pitch ramp" đi qua `ComboSystem.md` § Giai đoạn 2.)*

Ví dụ đặt piece hoàn thành hàng: piece hút "khục" vào ô → tick → cả hàng pop lan sóng từ điểm đặt → pitch tăng dần → suck-in giải tỏa.

---

## Nhóm 8 — Camera / Haptic / VFX / Ambient Feel

Các mảng bổ sung mở rộng cảm giác ra ngoài đối tượng: camera, xúc giác (mobile), VFX động, số liệu, môi trường nền. Nhiều class tái sử dụng `SpringDamper`, `HarmonicOscillator`, `Easing`, `AudioPitchHelper`.

### I. Camera Feel

#### `CameraPunch` / `ZoomPunch` ⭐ ưu tiên cao — giật zoom
- Zoom vào/ra nhanh rồi spring về khi impact. "Đấm" vào cảm giác cực mạnh.
- 📄 **Phần shake đã có plan:** `CameraShakeChannel` (← `TraumaShake`) + `IFeedbackCamera`/`FeedbackCameraRig` → `FeedbackSystem.md` Task 6.
- ⏳ **Phần zoom punch chưa làm** (cố ý cắt khỏi bản đầu — shake đã đủ trục thị giác). Nhưng `IFeedbackCamera.ApplyZoom` **đã có sẵn** trong interface, nên thêm driver về sau chỉ là thêm code trong một class, không breaking. Dùng `DampedOscillator` với `WaveStyle.Cos` (biên độ đầy ngay tại `t=0` = cú đấm tức thì).

#### `LookAhead` / `CameraLead` — nhìn trước hướng di chuyển
- Camera lệch nhẹ theo hướng player đi (spring). Cảm giác "có dự đoán".

#### `CameraFollowSmooth` — bám mượt có deadzone
- Dùng `SpringDamper`, có vùng chết để không rung khi đứng yên.

#### `DollyZoom` / `FOVKick` — hiệu ứng Vertigo, đổi FOV theo tốc độ.

### J. Haptic / Rung (ASMR xúc giác trên mobile)

#### `HapticHelper` ⭐ ưu tiên cao — rung theo ngữ cảnh
- Wrap các pattern rung (light/medium/heavy/success/warning). Đồng bộ haptic + visual + audio = ASMR đa giác quan hoàn chỉnh trên điện thoại. Mảnh còn thiếu quan trọng nhất.
- 📄 **Đã có plan:** `IHapticService.PlayCustom` + `IHapticBackend` (**rung một nhịp có biên độ điều khiển được**) → `Implementations/Foundations/Haptics/HapticSystem.md`. 4 file, backend 2 member.
- ⏳ Cố ý cắt khỏi bản đầu, thêm lại đều **additive**: bộ **preset trung tính** (`EHapticPreset` + `Play(preset)`) — đường preset chết trong v1 vì caller duy nhất dùng ramp biên độ; **rung liên tục** (`Begin/End` + ref-count + vòng pulse).

#### ~~`HapticPattern`~~ — ✅ **đã chuyển**, và **đổi tên** thành `HapticRamp`
- Pitch ramp phiên bản xúc giác: combo rung tăng dần cường độ.
- Hiện thực: `HapticRampChannel` → `FeedbackSystem.md` Task 5.
- ⚠️ **Vì sao đổi tên:** `PendingSystems.md` §9 đã dùng tên `HapticPattern` cho *struct mô tả MỘT cú rung*. Hai thứ khác nhau — một cú rung ≠ một chuỗi rung tăng dần.

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

#### ~~`ComboMeter`~~ — ✅ **đã chuyển sang `ComboSystem.md`** (Task 8)
- Thanh combo phồng/co theo streak. Bản hiện thực: thanh co theo **cửa sổ combo còn lại** + nảy (`SquashStretch` + `EaseType.OutBack`) mỗi nhịp + đổi màu theo bậc.

### M. Môi trường sống động (ambient juice — nền ASMR)

#### `ProceduralSway` ⭐ ưu tiên cao — đung đưa tự nhiên
- Cây cỏ/vật thể lắc theo noise nhiều tần số (không lặp cứng). Nền sống động, thư giãn.

#### `IdleBreathe` — vật thể/nhân vật "thở" khi đứng yên (nâng cấp `Pulse`).

#### `ParallaxHelper` — lớp nền trôi theo camera tạo chiều sâu.

#### `AmbientDrift` — trôi lững lờ (mây, bụi, bong bóng) bằng noise 2D.

### ~~Combo "đa giác quan hoàn hảo"~~ — ✅ đã chuyển
> Công thức **CameraPunch + Haptic + Ripple + pitch ramp** đã chuyển sang `ComboSystem.md` § *Nguyên liệu đã chuyển từ Pendings.md* — nơi nó được hiện thực bằng **một cue duy nhất** có mặt trong 4 bảng cue của `FeedbackSystem.md`. Ngưỡng đồng thời ~50ms và lý do kiến trúc dẫn giải ở `FeedbackSystem.md` §0.1. (`RippleEffect` chưa có kênh — mở rộng sau của hệ Feedback.)

---

## Roadmap triển khai theo tầng phụ thuộc

Nguyên tắc: **mỗi tầng chỉ phụ thuộc các tầng dưới nó**. Làm đúng thứ tự này → tầng sau luôn tái sử dụng tầng trước, không phải quay lại sửa nền. Ký hiệu `←` = "phụ thuộc / tái sử dụng".

### Tầng 0 — Nền toán học thuần (zero dependency)
Làm trước tiên vì mọi thứ khác đều gọi tới. Thuần `float`/`struct`, không phụ thuộc nhau.
1. `Easing` — ✅ **đã xong** (`Tweening.Easing.Easer`). Nền của mọi animation.
2. `HarmonicOscillator` — ✅ đã có.
3. `InterpolationHelper` — ✅ **đã xong** (`Interpolator`). `Remap`, `SmootherStep`, exp-decay lerp độc lập framerate.
4. **`RandomHelper`** — gaussian, weighted, jitter, shuffle (dùng cho shake/granular). **Item nền còn lại → làm kế tiếp.**
5. **`GeometryHelper`** — ⚠️ **một phần**: đã có random-point-in-annulus. Còn thiếu **khoảng cách, closest-point, giao điểm** — là **chặn** của `StaggerHelper` (mục 20) và do đó của `ChainReaction`.
6. **`AngleHelper`** — chuẩn hóa góc, shortest-delta, xoay vector 2D.

### Tầng 1 — Nguyên hàm vật lý (chỉ ← Tầng 0)
Các "động cơ" chuyển động mà lớp juice sẽ nhờ tới.
7. **`SpringDamper`** ⭐ — động cơ lò xo. Được dùng lại nhiều nhất (camera, snap, drag, recoil).
8. **`DampedOscillator`** — dao động tắt dần ← `HarmonicOscillator`. Nền của wobble/granular.
9. **`Projectile` / `Ballistics`** — đạn đạo (độc lập, ← toán Tầng 0).
10. **`Pendulum`** ← `HarmonicOscillator`.

### Tầng 2 — Juice nguyên tử (← Tầng 0–1)
Hiệu ứng đơn lẻ, là "viên gạch" cho các combo tầng trên.
11. `SquashStretch` — ✅ **đã xong**. Viên gạch thị giác dùng khắp nơi (đang dùng ở `ComboMeter`).
12. **`Overshoot`** ← `Easing` (EaseOutBack). Dùng cho pop/progress/floating text. *(Plan: `PhysXHelper/2026-07-25-overshoot.md`)*
13. **`Wobble` / `Jelly`** ← `DampedOscillator`.
14. **`Pulse` / `Breathing`** ← `HarmonicOscillator`.
15. **`Shake`** ⭐ (trauma-based) — 📄 plan: `TraumaShake.cs` ở `FeedbackSystem.md` Task 1. Dùng `noise.cnoise`, **không** cần `RandomHelper`.
16. **`ColorFlash`** ← `InterpolationHelper`. *(Plan: `PhysXHelper/2026-07-25-colorflash.md`)*
17. **`TimeScaleHelper`** ← `Easing` — 📄 plan (phần hitstop): `FeedbackSystem.md` Task 2.
18. `AudioPitchHelper` — ✅ **đã xong** (pitch ramp). Xương sống thính giác của combo.
19. **`HapticHelper`** ⭐ — 📄 plan: `Foundations/Haptics/HapticSystem.md` (`IHapticService` + `IHapticBackend`).
20. **`StaggerHelper` / `RippleDelay`** ⭐ ← `GeometryHelper` (delay theo khoảng cách).

### Tầng 3 — Hành vi tổng hợp (← Tầng 0–2)
Mỗi class ghép vài viên gạch tầng 2 thành một hành vi hoàn chỉnh.
21. **`Hitstop` / `FreezeFrame`** ⭐ ← `TimeScaleHelper` — 📄 plan: `HitstopChannel` ở `FeedbackSystem.md` Task 6.
22. **`CameraFollowSmooth`** ← `SpringDamper` (+ deadzone).
23. **`CameraPunch` / `ZoomPunch`** ⭐ — 📄 plan: `CameraPunchChannel` ở `FeedbackSystem.md` Task 6. Dùng `DampedOscillator` (đã có) thay `SpringDamper`/`Overshoot` (chưa có).
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
39. ~~`HapticPattern`~~ → **đổi tên `HapticRamp`**, đã chuyển: `HapticRampChannel` ở `FeedbackSystem.md` Task 5.
40. Phụ trợ VFX/ambient: `TrailFeedback`, `DissolveFeedback`, `GradientCycle`, `FlowFeedback`, `PileGrowth`, `ParallaxHelper`, `AmbientDrift`, `Anticipation`, `DollyZoom`. *(`ComboMeter` đã chuyển sang `ComboSystem.md` Task 8.)*

### Tầng 4 — Orchestrator (← mọi tầng dưới)
Dàn dựng nhiều hiệu ứng thành "sequence" — làm cuối cùng vì cần tất cả nguyên liệu.

> ⚠️ **Tầng này giờ có một nhà chung.** Việc "dàn dựng nhiều hiệu ứng cùng lúc" đã được SDK-hoá thành hệ **Feedback Orchestrator** (`Implementations/Composites/Feedback/FeedbackSystem.md`): một *cue* → fan-out 4 kênh (audio · haptic · hitstop · camera). Ba orchestrator dưới đây **không** nên tự gọi service trực tiếp nữa — chúng chỉ nên bắn cue. Hai bộ dàn dựng song song là anti-pattern #4 của `PendingSystems.md`.

41. ~~`ChainReaction`~~ — ✅ đã chuyển sang `ComboSystem.md` § *Giai đoạn 2* (chưa làm: chặn bởi `StaggerHelper` + chưa có board caller).
42. **`SatisfyingClear`** ⭐ ← `ColorFlash` + `StaggerHelper` + suck-in (`Easing`) + burst. **Nên** bắn cue qua `IFeedbackDispatcher` thay vì gọi audio/haptic trực tiếp.
43. **`CompletionSequence`** ← crescendo âm thanh + particle + camera + haptic. Cùng ghi chú với mục 42.

---

### Đường đi ngắn nhất tới "bản demo ASMR đã tay" cho falling_sand
Nếu muốn thấy kết quả sớm nhất, làm theo lát cắt dọc này (mỗi bước đều chạy được):
~~`Easing`~~ ✅ → ~~`SquashStretch`~~ ✅ → ~~`AudioPitchHelper`~~ ✅ → **`SpringDamper`** → `Shake` → `HapticHelper` → `StaggerHelper` → `MagneticSnap` → `GranularSettle` → `SatisfyingClear`.

### Lát cắt dọc thứ hai — đã có plan đầy đủ: "combo đa giác quan"
Không cần `SpringDamper`, chạy được ngay và cho ra thứ **cảm được bằng 3 giác quan**. 5 plan, làm theo đúng thứ tự:

1. `Implementations/Foundations/Ticker/TickerSystem.md` — 1 nguồn tick + `IOptionalService` + `DeferredList`
2. `Implementations/Foundations/Haptics/HapticSystem.md` — rung có biên độ (điều kiện của haptic ramp)
3. `Implementations/Foundations/Audio/AudioSystem.md` — SFX + **pitch** (điều kiện của pitch ramp)
4. `Implementations/Composites/Feedback/FeedbackSystem.md` — cue → 4 kênh; **kèm** hiện thực `TraumaShake` + `TimeScaleHelper`
5. `Implementations/Composites/Combo/ComboSystem.md` — lõi combo + `ComboMeter` + demo nghiệm thu
