# Feedback Orchestrator Implementation Plan

> **Loại tài liệu:** Plan (`DOCS_SKILL` Phần C). `.md` thiết kế (Phần A) + `.html` (Phần B) viết **sau** khi có source.
>
> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development hoặc superpowers:executing-plans. Steps dùng checkbox (`- [ ]`).

**Goal:** `IFeedbackDispatcher` — nhận **một** *cue* (một sự kiện đáng ăn mừng) rồi dàn phản hồi **đồng thời** ở 4 kênh: **thính giác** (pitch ramp) · **xúc giác** (haptic ramp) · **thời gian** (hitstop) · **thị giác** (trauma shake).

**Architecture:** **12 file** (1 toán + 5 contract + 2 hạ tầng + 4 kênh). Tất cả tham số cảm giác nằm trên chính kênh (Inspector), không có asset trung gian.

```
Utilities/PhysXHelper/
└── TraumaShake.cs                       toán thuần, dùng lại được ngoài hệ này
Abstractions/Composites/Feedback/
├── FeedbackCueId · FeedbackCue          cue = {Id, Step} — KHÔNG mang thông tin giác quan
├── IFeedbackChannel                     1 method
├── IFeedbackDispatcher                  1 method, IOptionalService — combo sống thiếu được
└── IFeedbackCamera                      1 method, seam camera do game/rig implement
Implementations/Composites/Feedback/
├── FeedbackDispatcher                   fan-out + try/catch từng kênh
├── FeedbackCameraRig                    impl mặc định của IFeedbackCamera
└── Channels/                            4 kênh, mỗi kênh tự serialize bảng cue của nó
```

**Tech Stack:** C#, `Unity.Mathematics` (`noise.cnoise`), `Easer` ✅, `ITicker`, `IAudioService`/`IHapticService` *(optional)*, `Sisus.Init`.

## Phân loại: `Composite`

Ghép 3 Foundation: **Ticker** (bắt buộc — 2 kênh cần nhịp mỗi frame) · **Audio** · **Haptics** (cả hai optional — thiếu cái nào thì kênh tương ứng tự tắt).

## Global Constraints

| Ràng buộc | Giá trị |
|---|---|
| Namespace | `Horcrux.Runtime.Abstractions.Feedback` · `…Implementations.Feedback` · `…Utilities.PhysXHelper` |
| Zero-GC | `FeedbackCue`/`FeedbackCueId` là `readonly struct` truyền `in`; kênh cache tham chiếu ở `Awake`; không alloc trong `Raise` |
| SOLID | Dispatcher **không biết** kênh nào tồn tại (D) · mỗi kênh 1 giác quan (S) · thêm kênh = thêm component (O) · mọi interface 1 method (I) |
| Trung tính | Cue là `int` có kiểu, **không** enum trong SDK |
| Thời gian | Mọi đo đạc bằng **unscaled** — hitstop đặt `timeScale = 0`; đo bằng scaled là **treo vĩnh viễn** (§0.3) |
| An toàn | `try/catch` quanh **từng** kênh · `timeScale` phải phục hồi ở `OnDisable` · kênh thiếu service → tự vô hiệu |
| Editor-first (§C.1) | Kênh **gán trong Inspector**, không `GetComponents`. Camera **gán trong Inspector**, không `GetComponentInChildren`. Mọi số cảm giác trên Inspector của kênh (§0.5) |

## Ngữ cảnh đã chốt

| Nhóm | Chốt |
|---|---|
| **Ai gọi** | `ComboSystem.md` → `ComboFeedbackBridge`. **Caller duy nhất.** Hệ này tồn tại thay vì để ComboSystem gọi thẳng 4 service, vì `SatisfyingClear`/`GridSnapFeedback`/`Cascade` (`Pendings.md`) sẽ dùng **cùng** dàn kênh — hai bộ dàn dựng song song là anti-pattern #4. |
| **Mục tiêu** | Một nhịp combo đến **cùng lúc** ở 3 giác quan trong ~50ms. Nghiệm thu: bịt một giác quan bất kỳ, hai giác quan còn lại **vẫn** truyền tải được đà combo. |
| **Ngân sách** | `Raise`: ~10–20 lần/giây lúc cascade ⇒ hot path. `OnTick`: 2 kênh × mỗi frame. `Raise` phải **0 B alloc**. |
| **Ranh giới** | Dispatcher chỉ **fan-out**. Kênh **dịch** cue → tham số giác quan của nó. "Cue nào ứng với sự kiện gì" thuộc caller. "Tham số cụ thể" thuộc Inspector. Ba tầng không được lẫn. |
| **Cố ý KHÔNG làm + lý do** (NT 6: *xóa đi thì hỏng ở đâu*) | ① **4 `CueTableSO` + `FeedbackCueLookup`** — không chia sẻ bảng giữa nhiều scene/prefab, mà đó là lợi ích **duy nhất** của SO ở đây. Entry serialize thẳng trên kênh thì 4 `CueId` **cùng nằm một Inspector** ⇒ đúng chỗ tôi từng phải cảnh báo là "dễ sai nhất". Cắt được 5 file + 4 bước tạo asset + 4 `OnEnable`/`OnValidate`. ② **`TimeScaleHelper.cs`** — không có user thứ hai; nội dung là lerp 2 pha ⇒ inline vào `HitstopChannel`. ③ **`FeedbackCue.Intensity`** — không nơi nào truyền ≠ 1; `Step` đã chở đủ thông tin cường độ. ④ **`IFeedbackCamera.ApplyZoom`** — có cách phòng xa **tốt hơn**: khi cần zoom thì thêm `IFeedbackCameraZoom` riêng (ISP) ⇒ không sửa interface cũ, không breaking ai. ⑤ **`AddChannel`/`RemoveChannel`** — không caller; kênh cố định ⇒ dispatcher dùng mảng thường, bỏ luôn tombstone. ⑥ Zoom punch driver, kênh particle/floating-text/ripple. |

---

## §0. Năm điều cần biết trước khi code

### 0.1. Vì sao "cue" chứ không phải gọi 4 service

Cảm giác "đã" nằm ở **sự đồng thời**, không ở từng giác quan: ba phản hồi yếu cùng lúc đọc mạnh hơn một phản hồi mạnh đơn lẻ, và ngưỡng "cùng lúc" của tri giác người là khoảng **±50ms**.

| Nếu caller gọi 4 service | Với cue + dispatcher |
|---|---|
| Caller phụ thuộc 4 interface | Caller phụ thuộc **1** |
| Caller phải biết tham số từng giác quan | Caller chỉ nói "chuyện gì vừa xảy ra" |
| Thêm giác quan thứ 5 = sửa mọi caller | Thêm 1 component |
| Mỗi caller tự tune → 10 chỗ lệch nhau | Tune ở **Inspector**, một nguồn sự thật |

Cue là một câu ngắn: *"sự kiện `Id`, bậc `Step`"* — không mang bất kỳ thông tin giác quan nào. Đó là cách nó giữ được tính trung tính.

### 0.2. Cường độ chuẩn hoá theo bậc — một công thức cho 2 kênh

```
u = saturate(Step / StepsToFull)                    // StepsToFull ≤ 0 ⇒ u = 1
strength = lerp(min, max, u)
```

Hai quyết định trong đó, mỗi cái chặn một lỗi:

| Quyết định | Chặn lỗi |
|---|---|
| `min > 0`, không `min = 0` | Nội suy từ 0 làm **nhịp đầu tiên không có phản hồi gì** — đúng lúc người chơi cần biết combo đã bắt đầu. Sàn `min` giữ mọi nhịp đều cảm được, ramp chỉ làm nó *rõ dần* |
| `StepsToFull ≤ 0 ⇒ u = 1` | Cue không có bậc phải đầy sức ngay. Guard này biến "không ramp" thành ca hợp lệ thay vì chia 0 |

| `Step` | `StepsToFull` | `u` | `strength` (min 0.3, max 1) |
|---|---|---|---|
| 0 | 10 | 0 | **0.3** — vẫn cảm được |
| 5 | 10 | 0.5 | 0.65 |
| 50 | 10 | 1 | 1.0 — **bão hoà**, không vọt |
| bất kỳ | 0 | 1 | 1.0 |

> Kênh audio **không** dùng `u`: cao độ có đơn vị *cộng* riêng (semitone) và trần riêng (`MaxSemitones`); thêm một lớp chuẩn hoá là hai lớp kẹp chồng nhau, rất khó tune.

### 0.3. Hitstop: đo bằng **unscaled**, nếu không là treo game vĩnh viễn

Lịch 2 pha, `t` = giây **thực** từ lúc bắt đầu:

| Pha | Điều kiện | `timeScale` |
|---|---|---|
| Đóng băng | `t < D` | `s_freeze` (thường 0) |
| Hồi phục | `D ≤ t < D+R` | `lerp(s_freeze, s_base, ease((t−D)/R))` |
| Xong | `t ≥ D+R` | `s_base` |

⚠️ **Bẫy #1.** `Time.deltaTime = Time.unscaledDeltaTime × Time.timeScale`. Ở pha đóng băng `timeScale = 0` ⇒ `deltaTime = 0` ⇒ bộ đếm dùng `deltaTime` **không bao giờ tiến** ⇒ điều kiện `t < D` luôn đúng ⇒ game đứng **mãi mãi**. Hệ quả đại số trực tiếp, nhưng cực dễ viết ra vì `Time.deltaTime` là phản xạ.

⚠️ **Bẫy #2.** `s_base` **không** hardcode `1`: game có thể đang slow-mo `0.3`; phục hồi về 1 là đổi tốc độ game vô cớ. Chụp `Time.timeScale` **một lần, chỉ khi chưa có hitstop nào chạy** — chụp lúc đang băng sẽ chụp được `0` rồi "phục hồi" về 0 ⇒ cũng đóng băng vĩnh viễn. Đó là lý do yêu cầu mới trong lúc đang chạy bị **bỏ qua** thay vì hợp nhất.

⚠️ **Bẫy #3.** Khựng ở **mỗi** nhịp combo (5 nhịp/giây × 40ms = game đứng 20% thời gian) đọc thành *lag*, không thành *lực*. Entry có `MinStep` — guard **thiết kế**, nên nó nằm ở dữ liệu.

### 0.4. Trauma shake: biên độ = `trauma²`, hướng lệch bằng Perlin

"Trauma" là **một** biến trạng thái `[0,1]`: cộng dồn khi bị hit (kẹp trần 1), giảm tuyến tính theo thời gian. Không giữ danh sách các cú shake — đó là lý do nhiều hit liên tiếp tự cộng dồn mà không cần quản lý gì.

```
amplitude = maxAmplitude * trauma * trauma
offset.x  = amplitude * clamp(cnoise(seedX, t * frequency), -1, 1)      // seedY khác seedX
```

> ⚠️ **Số mũ 2 là lựa chọn CẢM GIÁC, không dẫn ra từ vật lý** (NT 7). Đừng đi tìm cơ sở lý thuyết — nó được chọn bằng cách nhìn trên máy. Hai thứ *quan sát được* khiến nó thắng số mũ 1:
>
> | Quan sát | Phép kiểm tái lập |
> |---|---|
> | Tắt êm hơn, không thấy khoảnh khắc shake "đứng" | Chạy cùng cue với `trauma^1` rồi `trauma^2`, quay video 60fps, xem 10 frame cuối: bản `^1` biên độ còn thấy được ở frame áp cuối rồi về 0; bản `^2` đã gần 0 từ trước |
> | Rung nhỏ gần như không thấy ⇒ chỉ cú **đáng** mới làm màn hình rung | `trauma = 0.2` → biên độ `= 0.2² = 4%` của max. Số học thuần, tự kiểm được |
>
> Số mũ 3 cũng chấp nhận được. Nếu đổi thì sửa **một** dòng trong `TraumaShake.GetAmplitude` — không có gì phụ thuộc vào việc nó đúng bằng 2.

Ba lựa chọn còn lại **có** lý do kiểm được:

| Lựa chọn | Lý do + phép kiểm |
|---|---|
| Perlin theo **thời gian**, không `Random` mỗi frame | `Random` là nhiễu trắng: mỗi frame nhảy sang chỗ vô quan hệ ⇒ đọc như *lỗi render*, và hình dạng phụ thuộc FPS. **Kiểm:** chạy cùng cue ở `targetFrameRate = 30` rồi `60` — Perlin cho cùng quỹ đạo, `Random` cho hai kết quả khác nhau |
| **Hai** hạt giống khác nhau cho 2 trục | Cùng hạt giống ⇒ `offset.x == offset.y` mọi frame ⇒ rung theo đúng một đường chéo 45°. **Kiểm:** log `offset` vài frame, thấy `x == y` là sai |
| Hạt giống **không nguyên** | `noise.cnoise` trả **0** tại toạ độ nguyên của lưới ⇒ seed nguyên làm trục đó bị kéo về 0 mỗi khi `phase` chạm số nguyên, shake "hụt" theo chu kỳ. **Kiểm:** `noise.cnoise(new float2(13f, 7f))` ≈ 0 |
| `clamp` kết quả noise về `[−1,1]` | `maxAmplitude` là **hợp đồng** "tối đa" với người thiết kế; `cnoise` chỉ trả *xấp xỉ* `[−1,1]` |

### 0.5. Số cảm giác — chọn bằng mắt/tai, tune ở Inspector

Theo NT 7, những số dưới đây **không** có dẫn giải. Cách tune: nhấn Play, sửa số, xem/nghe lại. **Không** sửa code (§C.1).

| Số | Khởi đầu | Tune ở đâu |
|---|---|---|
| Semitone mỗi bậc / trần | +1 / 12 | `AudioPitchRampChannel` |
| Biên độ rung min → max, `StepsToFull` | 0.3 → 1.0, 10 | `HapticRampChannel` |
| Cửa sổ throttle rung | 0.05s | `HapticRampChannel` |
| Đóng băng / hồi phục / `MinStep` | 0.05s / 0.08s / 5 | `HitstopChannel` |
| Trauma cộng mỗi cue (min → max) | 0.15 → 0.5 | `CameraShakeChannel` |
| Biên độ shake, độ nghiêng, tần số | 0.35 / 2° / 18 | `CameraShakeChannel` |
| Tốc độ hồi phục trauma | 1.5 /giây | `CameraShakeChannel` |
| Số mũ trauma | 2 | hằng trong `TraumaShake` — không phải trục tune hằng ngày |

---

## Bản đồ triển khai

| Task | File | Nội dung |
|---|---|---|
| 1 | `Utilities/PhysXHelper/TraumaShake.cs` | toán shake (§0.4) |
| 2 | `Abstractions/Composites/Feedback/` — 4 file | cue + 3 interface |
| 3 | `Implementations/Composites/Feedback/FeedbackDispatcher.cs` | fan-out |
| 4 | `Channels/AudioPitchRampChannel.cs` · `HapticRampChannel.cs` | thính giác + xúc giác |
| 5 | `Channels/HitstopChannel.cs` · `CameraShakeChannel.cs` + `FeedbackCameraRig.cs` | thời gian + thị giác |

Thứ tự: **1 → 2 → 3 → 4 → 5**.

---

### Task 1: `TraumaShake`

**Files:** Create `Assets/Horcrux/Runtime/Utilities/PhysXHelper/TraumaShake.cs`

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `static`, **không** giữ state | Trauma là state của *người dùng* (mỗi camera một giá trị); nhồi vào helper là ép mọi consumer chia sẻ một shake |
| Bình phương **cố định**, không tham số `power` | `p = 2` là quyết định (§0.4); tham số hoá là mở một trục tune chưa ai cần và thêm một nhánh `math.pow` vào đường chạy mỗi frame |
| `noise.cnoise` thay `Mathf.PerlinNoise` | Cùng họ `Unity.Mathematics` như phần còn lại của `PhysXHelper`; và `cnoise` đã trả ~`[−1,1]` nên khỏi phép `*2−1` |
| Thoát sớm `trauma <= 0` | Không shake là ca **phổ biến nhất** ⇒ trả `Vector2.zero` ngay, khỏi 2 lần gọi noise |
| `GetRoll` tách khỏi `GetOffset` | Không phải camera nào cũng muốn nghiêng; ép trả 3 giá trị là buộc caller nhận thứ nó không dùng (ISP) |
| `[AggressiveInlining]` cho 3 wrapper mỏng | Chạy mỗi frame khi đang shake ⇒ hot path; thân 1–2 phép tính nên nội tuyến khỏi phí gọi hàm |

- [ ] **Step 1: `TraumaShake.cs`**

```csharp
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.PhysXHelper
{
    /// <summary>Rung tích lũy theo "trauma": MỘT biến trạng thái [0,1] thay vì danh sách các cú shake.</summary>
    /// <remarks>
    /// Biên độ = maxAmplitude · trauma² (§0.4). Số mũ 2 là lựa chọn CẢM GIÁC, không suy ra từ vật lý:
    /// nó tắt êm hơn số mũ 1, và làm rung nhỏ gần như không thấy (trauma 0.2 ⇒ 4%) nên chỉ cú ĐÁNG
    /// mới làm màn hình rung.
    ///
    /// Hướng lệch dùng Perlin theo thời gian, KHÔNG Random mỗi frame: Random là nhiễu trắng, mỗi frame
    /// nhảy sang chỗ vô quan hệ ⇒ đọc như lỗi render, và hình dạng phụ thuộc FPS.
    ///
    /// Class KHÔNG giữ state. Khuôn dùng:
    /// <code>
    /// _trauma = TraumaShake.AddTrauma(_trauma, amount);                    // khi bị hit
    /// _trauma = TraumaShake.DecayTrauma(_trauma, decay, unscaledDelta);    // mỗi frame
    /// Vector2 offset = TraumaShake.GetOffset(_trauma, maxAmp, _time, freq);
    /// </code>
    /// </remarks>
    public static class TraumaShake
    {
        // Hạt giống KHÁC nhau và KHÔNG nguyên: cùng hạt giống thì hai trục lắc y hệt (rung theo đúng
        // một đường chéo); seed nguyên làm noise bị kéo về 0 theo chu kỳ vì cnoise = 0 tại lưới nguyên.
        private const float SeedX = 13.731f;
        private const float SeedY = 271.117f;
        private const float SeedRoll = 547.913f;

        /// <summary>Cộng dồn trauma khi bị hit, kẹp trần 1.</summary>
        /// <remarks>Cộng dồn (không gán) để nhiều hit liên tiếp làm shake mạnh dần; trần chặn vô hạn.</remarks>
        /// <param name="trauma">Giá trị hiện tại, miền [0,1].</param>
        /// <param name="amount">Lượng cộng thêm; kết quả tự kẹp về [0,1]. Số cảm giác (§0.5).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AddTrauma(float trauma, float amount) => math.saturate(trauma + amount);

        /// <summary>Giảm trauma tuyến tính: <c>max(0, trauma − k·dt)</c>.</summary>
        /// <remarks>Tuyến tính là cách đơn giản nhất và tất định; đủ dùng cho cảm giác cần (§0.4).</remarks>
        /// <param name="decayPerSecond">Tốc độ hồi phục (trauma/giây); ≤ 0 ⇒ không giảm. Số cảm giác (§0.5).</param>
        /// <param name="deltaTime">Phải là thời gian THỰC (unscaled) — hitstop đặt timeScale = 0.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DecayTrauma(float trauma, float decayPerSecond, float deltaTime)
            => decayPerSecond <= 0f ? trauma : math.max(trauma - decayPerSecond * deltaTime, 0f);

        /// <summary>Biên độ ứng với trauma hiện tại: <c>maxAmplitude · trauma²</c>.</summary>
        /// <param name="trauma">Miền [0,1]; ≤ 0 ⇒ trả 0 chính xác.</param>
        /// <param name="maxAmplitude">
        /// Biên độ khi <paramref name="trauma"/> = 1, đơn vị do caller định (world unit hoặc độ).
        /// Là HỢP ĐỒNG "tối đa" — kết quả không bao giờ vượt nó.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetAmplitude(float trauma, float maxAmplitude)
            => trauma <= 0f ? 0f : maxAmplitude * trauma * trauma;

        /// <summary>Độ lệch 2 trục, không tương quan, trơn theo thời gian.</summary>
        /// <param name="trauma">Miền [0,1]. Ai cấp: caller giữ state này qua Add/Decay.</param>
        /// <param name="maxAmplitude">Biên độ khi trauma = 1, đơn vị WORLD.</param>
        /// <param name="time">
        /// Giây thực tích lũy — lấy mẫu theo t (không theo frame) ⇒ độc lập FPS. Caller nên dùng bộ
        /// đếm RIÊNG và reset khi hết shake, không dùng <c>Time.unscaledTime</c> (số đó lớn dần vô hạn
        /// ⇒ mất chính xác float và noise bị lấy mẫu ở vùng rất xa gốc).
        /// </param>
        /// <param name="frequency">Tần số lắc; càng cao càng "rát". Số cảm giác (§0.5).</param>
        public static Vector2 GetOffset(float trauma, float maxAmplitude, float time, float frequency)
        {
            float amplitude = GetAmplitude(trauma, maxAmplitude);

            // Không shake là ca phổ biến nhất → thoát trước 2 lần gọi noise.
            if (amplitude <= 0f) return Vector2.zero;

            float phase = time * frequency;

            // clamp: maxAmplitude là HỢP ĐỒNG "tối đa", còn cnoise chỉ trả XẤP XỈ [−1,1].
            float x = math.clamp(noise.cnoise(new float2(SeedX, phase)), -1f, 1f);
            float y = math.clamp(noise.cnoise(new float2(SeedY, phase)), -1f, 1f);

            return new Vector2(x * amplitude, y * amplitude);
        }

        /// <summary>Độ nghiêng (roll) theo cùng cơ chế, hạt giống thứ ba.</summary>
        /// <remarks>Tách khỏi <see cref="GetOffset"/> vì không phải camera nào cũng muốn nghiêng (ISP).</remarks>
        /// <param name="maxDegrees">Độ nghiêng khi trauma = 1, đơn vị ĐỘ.</param>
        public static float GetRoll(float trauma, float maxDegrees, float time, float frequency)
        {
            float amplitude = GetAmplitude(trauma, maxDegrees);
            if (amplitude <= 0f) return 0f;

            float n = math.clamp(noise.cnoise(new float2(SeedRoll, time * frequency)), -1f, 1f);
            return n * amplitude;
        }
    }
}
```

- [ ] **Step 2: Kiểm chứng**

| Input | Kỳ vọng |
|---|---|
| `GetAmplitude(1, 10)` / `(0.5f, 10)` / `(0.2f, 10)` / `(0, 10)` | `10` / `2.5` / `0.4` / `0` chính xác |
| `AddTrauma(0.6f, 0.7f)` | `1` (kẹp) |
| `DecayTrauma(1, 2, 0.5f)` / `(1, 2, 1f)` / `(1, 0, 0.5f)` | `0` / `0` (không âm) / `1` (không giảm) |
| `GetOffset(0, 10, t, 20)` | `(0,0)` chính xác |
| `GetOffset(1, 10, 5f, 20)` gọi 2 lần | **cùng** kết quả (hàm thuần) |
| `.x` vs `.y` với mọi `t` thử | **khác nhau** (2 hạt giống) |
| `abs(offset.x) ≤ 10` với 1000 mốc `t` | luôn đúng |

- [ ] **Step 3: Cập nhật `Pendings.md`** Nhóm 6-B `Shake` — thêm nhãn trỏ plan này (giữ item ở lại, là utility dùng chung).

- [ ] **Step 4: Commit** — `feat(sdk): add TraumaShake math`

---

### Task 2: 4 contract

**Files:** `Assets/Horcrux/Runtime/Abstractions/Composites/Feedback/` — `FeedbackCueId.cs` · `FeedbackCue.cs` · `IFeedbackChannel.cs` · `IFeedbackDispatcher.cs` · `IFeedbackCamera.cs`

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `FeedbackCueId` wrap `int`, **không** enum trong SDK | Enum ở đây khoá danh sách cue của **mọi** game (cùng lý lẽ `RewardData.TypeId` §14c) |
| `FeedbackCue` chỉ **2 field** (`Id`, `Step`) | `Intensity` bị cắt: không nơi nào truyền ≠ 1, và `Step` đã chở đủ thông tin cường độ |
| `GetNormalizedStrength` là method **của cue** | 2 kênh cần đúng phép tính đó; 2 bản sao thì một bản sẽ quên guard chia 0 |
| Mọi interface **1 method** | Thêm `Stop`/`Reset` là buộc mọi kênh viết method rỗng (ISP). Kênh nào cần dọn thì tự làm trong `OnDisable` |
| `IFeedbackDispatcher` là `IOptionalService` | `ComboSystem` **phải** chạy khi chưa có hệ Feedback — đó là cả điểm của việc tách hai hệ |
| `IFeedbackCamera` chỉ có `ApplyShake` | Khi cần zoom: thêm **interface thứ hai** `IFeedbackCameraZoom` (ISP) ⇒ không sửa interface này, không breaking implementer nào. Tốt hơn hẳn việc khai sẵn một method chưa ai gọi |
| `Raise` không trả gì | Cue là *thông báo*, không phải *truy vấn*. Trả `bool` mời gọi caller phân nhánh theo "có kênh nào nghe không" — coupling ngược |

- [ ] **Step 1: `FeedbackCueId.cs` + `FeedbackCue.cs`**

```csharp
// ── FeedbackCueId.cs ───────────────────────────────────────────────────────
using System;

namespace Horcrux.Runtime.Abstractions.Feedback
{
    /// <summary>Định danh một loại sự kiện cần phản hồi. Wrap <c>int</c> để có kiểu ở call-site.</summary>
    /// <remarks>
    /// Vì sao KHÔNG <c>enum</c> trong SDK: enum ở đây khoá danh sách cue của MỌI game — một module
    /// event/liveops không thể thêm cue riêng mà không sửa file SDK.
    /// Game khai <c>enum GameCue { ComboBeat = 101, ComboTierUp = 102 }</c> rồi truyền <c>(int)</c>.
    /// </remarks>
    public readonly struct FeedbackCueId : IEquatable<FeedbackCueId>
    {
        public readonly int Value;

        /// <param name="value">Giá trị định danh; <c>0</c> dành riêng cho "chưa gán".</param>
        public FeedbackCueId(int value) => Value = value;

        public static implicit operator FeedbackCueId(int value) => new(value);

        /// <summary><c>0</c> = chưa gán — dùng để bắt field quên điền trong Inspector.</summary>
        public bool IsValid => Value != 0;

        public bool Equals(FeedbackCueId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is FeedbackCueId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
    }
}

// ── FeedbackCue.cs ────────────────────────────────────────────────────────
using Unity.Mathematics;

namespace Horcrux.Runtime.Abstractions.Feedback
{
    /// <summary>Một "câu" mô tả sự kiện vừa xảy ra — KHÔNG mang thông tin giác quan nào.</summary>
    /// <remarks>
    /// Đây là cách hệ giữ tính trung tính: cue nói "chuyện gì, bậc mấy"; việc nó thành tiếng gì / rung
    /// ra sao là của từng kênh, đọc từ bảng cue trên Inspector của kênh đó.
    /// </remarks>
    public readonly struct FeedbackCue
    {
        public readonly FeedbackCueId Id;

        /// <summary>Bậc ramp (ví dụ: số combo). <c>0</c> ở cue một-lần là bình thường.</summary>
        public readonly int Step;

        /// <param name="step">Bậc ramp; tự kẹp về ≥ 0. Ai cấp: caller (bridge của combo).</param>
        public FeedbackCue(FeedbackCueId id, int step = 0)
        {
            Id = id;
            Step = math.max(step, 0);
        }

        /// <summary>Cường độ chuẩn hoá theo bậc: <c>saturate(Step / stepsToFull)</c> (§0.2).</summary>
        /// <remarks>Đặt ở đây (không ở từng kênh) vì 2 kênh cần đúng phép tính này.</remarks>
        /// <param name="stepsToFull">
        /// Số bậc để đạt cường độ tối đa. <c>≤ 0</c> ⇒ trả <c>1</c>: cue KHÔNG ramp phải đầy sức ngay,
        /// và đó là cách guard chia 0.
        /// </param>
        public float GetNormalizedStrength(int stepsToFull)
            => stepsToFull <= 0 ? 1f : math.saturate((float)Step / stepsToFull);
    }
}
```

- [ ] **Step 2: 3 interface**

```csharp
// ── IFeedbackChannel.cs ───────────────────────────────────────────────────
namespace Horcrux.Runtime.Abstractions.Feedback
{
    /// <summary>Một giác quan / một trục phản hồi. Đúng MỘT method.</summary>
    /// <remarks>
    /// Thêm <c>Stop()</c>/<c>Reset()</c> vào đây là buộc mọi kênh viết method rỗng (ISP). Kênh nào
    /// cần dọn dẹp thì tự làm trong <c>OnDisable</c> của chính nó.
    /// Kênh KHÔNG biết dispatcher, không biết kênh khác, không biết ai phát cue.
    /// </remarks>
    public interface IFeedbackChannel
    {
        void Play(in FeedbackCue cue);
    }
}

// ── IFeedbackDispatcher.cs ────────────────────────────────────────────────
namespace Horcrux.Runtime.Abstractions.Feedback
{
    /// <summary>Phát một cue tới mọi kênh đang gán. Không biết kênh nào tồn tại.</summary>
    /// <remarks>
    /// Là <see cref="IOptionalService{T}"/> có chủ ý: <c>ComboSystem</c> phải chạy được khi chưa có
    /// hệ Feedback — đó là toàn bộ lý do hai hệ được tách ra.
    ///
    /// <c>Raise</c> KHÔNG trả gì: cue là thông báo, không phải truy vấn.
    /// </remarks>
    public interface IFeedbackDispatcher : IOptionalService<IFeedbackDispatcher>
    {
        void Raise(in FeedbackCue cue);
    }
}

// ── IFeedbackCamera.cs ────────────────────────────────────────────────────
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Feedback
{
    /// <summary>Seam camera: SDK sinh ra ĐỘ LỆCH, game/rig quyết định áp nó thế nào.</summary>
    /// <remarks>
    /// Không có interface này thì SDK phải biết camera của game là gì (Cinemachine? camera con của
    /// player?) ⇒ mất tính port ngay. Optional vì game không cần hiệu ứng camera vẫn phải chạy được.
    ///
    /// Giá trị là ĐỘ LỆCH so với trạng thái nền, không phải giá trị tuyệt đối — nhờ vậy nó cộng được
    /// lên trên logic follow của game mà không giằng nhau.
    ///
    /// Khi cần zoom punch: khai một interface RIÊNG (<c>IFeedbackCameraZoom</c>) và cho rig implement
    /// thêm. KHÔNG thêm method vào interface này — thêm method vào interface là breaking cho mọi
    /// implementer, kể cả rig do game tự viết.
    /// </remarks>
    public interface IFeedbackCamera : IOptionalService<IFeedbackCamera>
    {
        /// <param name="localOffset">Độ lệch vị trí (world unit) so với vị trí nền.</param>
        /// <param name="rollDegrees">Độ nghiêng quanh trục nhìn (độ) so với góc nền.</param>
        void ApplyShake(Vector2 localOffset, float rollDegrees);
    }
}
```

- [ ] **Step 3: Kiểm chứng (§0.2)**

| Input | Kỳ vọng |
|---|---|
| `new FeedbackCue(1, 0).GetNormalizedStrength(10)` | `0` |
| `new FeedbackCue(1, 5).GetNormalizedStrength(10)` | `0.5` |
| `new FeedbackCue(1, 50).GetNormalizedStrength(10)` | `1` (bão hoà) |
| `new FeedbackCue(1, 5).GetNormalizedStrength(0)` / `(-3)` | `1`, không throw |
| `new FeedbackCue(1, -5)` | `Step == 0` (kẹp ctor) |

- [ ] **Step 4: Commit** — `feat(sdk): add feedback cue contracts`

---

### Task 3: `FeedbackDispatcher`

**Files:** Create `Assets/Horcrux/Runtime/Implementations/Composites/Feedback/FeedbackDispatcher.cs`

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| Kênh **gán trong Inspector** (`MonoBehaviour[]`), không `GetComponents` | §C.1: tìm thứ vốn tồn tại lúc authoring là đặt sai chỗ. Gán sẵn cho 3 lợi ích: thiếu kênh lộ ra **ô trống**; **thứ tự** phát do author quyết; kênh đặt được ở **GameObject khác** |
| Field là `MonoBehaviour[]`, cast ở `Awake` | Unity không serialize field kiểu interface, và `InterfaceReference<T>` chưa có trong SDK. `OnValidate` báo ngay nếu kéo vào component không phải kênh |
| Mảng `IFeedbackChannel[]` cố định sau `Awake` | Kênh không đổi lúc runtime (`AddChannel` đã cắt vì không caller) ⇒ không cần `DeferredList`/tombstone |
| `try/catch` quanh **từng** kênh | Kênh camera lỗi (thiếu rig) không được chặn kênh âm thanh — đây chính là điều kiện để "thiếu một giác quan vẫn chạy" là **thật** |
| Không log khi không có kênh nào | "Chưa gắn kênh" là trạng thái hợp lệ (scene test) |

**Editor setup (§C.1) — làm sau Task 5, khi đã có 4 kênh:**

1. Tạo GameObject `[Feedback]` ở scene bootstrap → add `FeedbackDispatcher`.
2. Add 4 component kênh (Task 4–5) lên **cùng** GameObject này.
3. Kéo 4 component kênh vào mảng `Channels`. Đặt `HitstopChannel` **đầu** để `timeScale` đổi trước khi các kênh khác đọc thời gian.
4. Kiểm: Console không có error "không phải IFeedbackChannel"; mảng không còn ô `None`.

- [ ] **Step 1: `FeedbackDispatcher.cs`**

```csharp
using System;
using Horcrux.Runtime.Abstractions.Feedback;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Feedback
{
    /// <summary>Nhận cue rồi phát tới mọi kênh. KHÔNG biết kênh nào tồn tại, không biết tham số của kênh.</summary>
    /// <remarks>
    /// Kênh được GÁN TRONG INSPECTOR (§C.1), không đi tìm bằng <c>GetComponents</c>. Ba lợi ích: thiếu
    /// kênh lộ ra ô trống lúc authoring; thứ tự phát do author quyết; kênh đặt được ở GameObject khác.
    ///
    /// Field phải là <c>MonoBehaviour[]</c> vì Unity không serialize field kiểu interface.
    /// <c>OnValidate</c> báo ngay nếu kéo vào một component không phải kênh.
    ///
    /// <c>try/catch</c> quanh TỪNG kênh là điều kiện để "thiếu một giác quan vẫn chạy" là thật:
    /// kênh camera lỗi vì chưa có rig không được chặn kênh âm thanh.
    /// </remarks>
    [Service(typeof(IFeedbackDispatcher), FindFromScene = true)]
    public sealed class FeedbackDispatcher : MonoBehaviour, IFeedbackDispatcher
    {
        [Tooltip("Các kênh phản hồi, theo thứ tự phát. Đặt HitstopChannel đầu tiên.")]
        [SerializeField] private MonoBehaviour[] channels = Array.Empty<MonoBehaviour>();

        private IFeedbackChannel[] _resolved = Array.Empty<IFeedbackChannel>();

        private void Awake()
        {
            DontDestroyOnLoad(this);

            _resolved = new IFeedbackChannel[channels.Length];

            for (int i = 0; i < channels.Length; i++)
            {
                // Ô trống / component sai kiểu: OnValidate đã báo lúc authoring → ở đây để null và skip.
                _resolved[i] = channels[i] as IFeedbackChannel;
            }
        }

        public void Raise(in FeedbackCue cue)
        {
            if (!cue.Id.IsValid) return;            // cue chưa gán id: bỏ qua thay vì phát rác

            for (int i = 0; i < _resolved.Length; i++)
            {
                IFeedbackChannel channel = _resolved[i];
                if (channel == null) continue;

                try { channel.Play(cue); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

#if UNITY_EDITOR
        /// <summary>Kéo sai component vào mảng thì lộ ra NGAY lúc authoring, không im lặng ở runtime.</summary>
        private void OnValidate()
        {
            for (int i = 0; i < channels.Length; i++)
            {
                MonoBehaviour component = channels[i];
                if (component == null || component is IFeedbackChannel) continue;

                Debug.LogError($"[Feedback] {name}: slot #{i} ({component.GetType().Name}) không phải " +
                               $"IFeedbackChannel — cue sẽ không tới nó.", this);
            }
        }
#endif
    }
}
```

- [ ] **Step 2: Kiểm chứng**

| Input | Kỳ vọng |
|---|---|
| 4 kênh gán trong mảng | `Raise` gọi cả 4, **theo đúng thứ tự** trong mảng |
| Kênh #2 throw | kênh #3, #4 **vẫn** được gọi |
| Kéo một component **không** phải kênh vào mảng | log error **ngay lúc chỉnh Inspector** |
| Ô trống trong mảng | bỏ qua im lặng ở runtime (đã báo lúc authoring) |
| `Raise` với `Id = 0` | không kênh nào được gọi |
| Profiler: 20 `Raise`/giây, 4 kênh | **0 B** GC Alloc |

- [ ] **Step 3: Commit** — `feat(sdk): add FeedbackDispatcher`

---

### Task 4: Kênh thính giác + xúc giác

**Files:** `Channels/AudioPitchRampChannel.cs` · `Channels/HapticRampChannel.cs`

**Bản đồ toán → code:** pitch ramp = `AudioPitchHelper.GetRampedPitch(cue.Step, …)` (**không** viết lại `2^(n/12)`) · biên độ rung = `lerp(min, max, cue.GetNormalizedStrength(stepsToFull))` (§0.2).

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| Entry serialize **trên chính kênh**, không SO riêng | Lợi ích duy nhất của SO là chia sẻ giữa nhiều scene/prefab — **chưa cần**. Trên kênh thì 4 `CueId` cùng nằm một Inspector ⇒ dễ giữ đồng bộ, và bớt 5 file |
| Tra bằng **quét tuyến tính**, không `Dictionary` | Số entry thực tế 1–5. Quét 5 phần tử rẻ hơn dựng + tra Dictionary, và không cần `OnEnable` rebuild. Khi vượt ~10 entry thì đổi sang Dictionary (ghi ở "Mở rộng sau") |
| Pitch dùng `cue.Step` **trực tiếp**, không qua `u` | Cao độ có đơn vị *cộng* riêng (semitone) và trần riêng; thêm một lớp chuẩn hoá là hai lớp kẹp chồng nhau |
| Haptic dùng `u` | Biên độ rung không có thang tự nhiên kiểu nhạc lý ⇒ nội suy min→max trên `u` là cách diễn đạt trực tiếp nhất |
| Kênh `TryGet` service ở `Awake`, cache | `TryGet` mỗi cue là tra service-locator ở 20 lần/giây |
| Thiếu service ⇒ `enabled = false` + **1** warning | Không có audio là hợp lệ; nhưng lặng hoàn toàn thì người tích hợp mất hàng giờ để hiểu vì sao không có tiếng |
| Haptic có `MinIntervalSeconds` **riêng** | Rung dày hơn ~50ms nhập lại thành một (§0.1 của `HapticSystem.md`) ⇒ ngưỡng khác âm thanh |
| Haptic throttle dùng **một** mốc chung, không per-cue | Chỉ có 1–2 cue bắn rung; giữ Dictionary per-cue là cấu trúc thừa. Một `float _lastPlayTime` là đủ |

**Editor setup (§C.1):**

1. Add `AudioPitchRampChannel` + `HapticRampChannel` lên GameObject `[Feedback]`.
2. `AudioPitchRampChannel` → mảng `Entries`, 1 dòng: `CueId = 101` · `AudioId = 1` (khớp catalog ở `AudioSystem.md`) · `SemitonesPerStep = 1` · `MaxSemitones = 12` · `VolumeScale = 1`.
3. `HapticRampChannel` → mảng `Entries`, 1 dòng: `CueId = 101` · `MinAmplitude = 0.3` · `MaxAmplitude = 1` · `StepsToFull = 10` · `DurationSeconds = 0.02` · `MinIntervalSeconds = 0.05`.
4. **Cùng `CueId = 101` ở cả hai** là cách một nhịp combo tới hai giác quan cùng lúc (§0.1). Ghi lại số đó — `ComboFeedbackBridge` dùng nó làm `beatCueId`.

- [ ] **Step 1: `AudioPitchRampChannel.cs`**

```csharp
using System;
using Horcrux.Runtime.Abstractions.Audio;
using Horcrux.Runtime.Abstractions.Feedback;
using Horcrux.Runtime.Utilities.AudioHelper;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Feedback
{
    /// <summary>THÍNH GIÁC: mỗi bậc cue phát tiếng cao hơn bậc trước, chạm trần rồi giữ.</summary>
    /// <remarks>
    /// Xương sống thính giác của combo. Cao độ tính bằng <c>AudioPitchHelper.GetRampedPitch</c> —
    /// công thức đã hiện thực và kiểm mốc ở đó, KHÔNG viết lại.
    ///
    /// Vì sao pitch dùng <c>cue.Step</c> trực tiếp thay vì đi qua <c>u</c> của §0.2: cao độ có đơn vị
    /// CỘNG riêng (semitone) và đã có trần riêng; thêm một lớp chuẩn hoá là hai lớp kẹp chồng nhau.
    /// </remarks>
    public sealed class AudioPitchRampChannel : MonoBehaviour, IFeedbackChannel
    {
        /// <summary>Tham số thính giác cho một cue. Số cảm giác — tune ở Inspector (§0.5).</summary>
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private int cueId;
            [SerializeField] private int audioId;

            [Tooltip("Mỗi bậc ramp cộng bao nhiêu semitone. 0 = không ramp cao độ.")]
            [SerializeField] private float semitonesPerStep = 1f;

            [Tooltip("Trần tổng semitone. 12 = tối đa một quãng tám.")]
            [SerializeField] private float maxSemitones = 12f;

            [Range(0f, 1f)]
            [SerializeField] private float volumeScale = 1f;

            public int CueId => cueId;
            public AudioId AudioId => new(audioId);
            public float SemitonesPerStep => semitonesPerStep;
            public float MaxSemitones => maxSemitones;
            public float VolumeScale => volumeScale;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private IAudioService _audio;

        private void Awake()
        {
            // TryGet mỗi cue là tra service-locator ở 20 lần/giây → cache một lần.
            if (IAudioService.TryGet(out _audio)) return;

            // Không có audio là HỢP LỆ, nhưng lặng hoàn toàn thì người tích hợp mất hàng giờ để hiểu
            // vì sao không có tiếng. Một dòng, một lần.
            Debug.LogWarning("[Feedback] Không tìm thấy IAudioService — kênh âm thanh tự tắt.", this);
            enabled = false;
        }

        public void Play(in FeedbackCue cue)
        {
            if (!enabled) return;

            // Quét tuyến tính: 1–5 entry, rẻ hơn dựng + tra Dictionary.
            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                if (entry == null || entry.CueId != cue.Id.Value) continue;

                float pitchScale = AudioPitchHelper.GetRampedPitch(
                    cue.Step, entry.SemitonesPerStep, entry.MaxSemitones);

                _audio.PlaySfx(entry.AudioId, entry.VolumeScale, pitchScale);
                return;
            }
        }
    }
}
```

- [ ] **Step 2: `HapticRampChannel.cs`**

```csharp
using System;
using Horcrux.Runtime.Abstractions.Feedback;
using Horcrux.Runtime.Abstractions.Haptics;
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Feedback
{
    /// <summary>XÚC GIÁC: cường độ rung tăng theo bậc cue — "pitch ramp phiên bản xúc giác".</summary>
    /// <remarks>
    /// Đây là class mà <c>Pendings.md</c> Nhóm 8-J gọi là <c>HapticPattern</c>. Đổi tên để không trùng
    /// struct <c>HapticPattern</c> của §9 — một cú rung ≠ một chuỗi rung tăng dần.
    ///
    /// Throttle RIÊNG (không dùng ngưỡng của audio): hai cú rung cách nhau dưới ~50ms NHẬP lại thành
    /// một vì motor cần thời gian tắt ⇒ rung dày hơn thế là tốn pin mà không thêm cảm giác.
    /// </remarks>
    public sealed class HapticRampChannel : MonoBehaviour, IFeedbackChannel
    {
        /// <summary>Tham số xúc giác cho một cue. Số cảm giác — tune ở Inspector (§0.5).</summary>
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private int cueId;

            [Tooltip("Biên độ ở bậc 0. Phải > 0 — nhịp đầu combo cũng cần cảm được (§0.2).")]
            [Range(0f, 1f)]
            [SerializeField] private float minAmplitude = 0.3f;

            [Range(0f, 1f)]
            [SerializeField] private float maxAmplitude = 1f;

            [Tooltip("Số bậc để đạt biên độ tối đa. 0 = không ramp (luôn tối đa).")]
            [SerializeField] private int stepsToFull = 10;

            [SerializeField] private float durationSeconds = 0.02f;

            public int CueId => cueId;
            public float MinAmplitude => minAmplitude;
            public float MaxAmplitude => maxAmplitude;
            public int StepsToFull => stepsToFull;
            public float DurationSeconds => durationSeconds;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        [Tooltip("Cửa sổ tối thiểu giữa 2 lần rung (giây). Dày hơn ~50ms thì các cú rung NHẬP lại thành một.")]
        [SerializeField] private float minIntervalSeconds = 0.05f;

        private IHapticService _haptic;
        private float _lastPlayUnscaledTime = float.NegativeInfinity;

        private void Awake()
        {
            if (IHapticService.TryGet(out _haptic)) return;

            Debug.LogWarning("[Feedback] Không tìm thấy IHapticService — kênh rung tự tắt.", this);
            enabled = false;
        }

        public void Play(in FeedbackCue cue)
        {
            if (!enabled) return;

            // unscaled: hitstop đặt timeScale = 0 nhưng rung vẫn phải đúng nhịp thật.
            float now = Time.unscaledTime;
            if (now - _lastPlayUnscaledTime < minIntervalSeconds) return;

            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                if (entry == null || entry.CueId != cue.Id.Value) continue;

                float u = cue.GetNormalizedStrength(entry.StepsToFull);                 // §0.2
                float amplitude = math.lerp(entry.MinAmplitude, entry.MaxAmplitude, u);

                _haptic.PlayCustom(new HapticPattern(amplitude, entry.DurationSeconds));
                _lastPlayUnscaledTime = now;
                return;
            }
        }
    }
}
```

- [ ] **Step 3: Kiểm chứng**

| # | Input | Kỳ vọng |
|---|---|---|
| 1 | Không có `IAudioService` | kênh `enabled = false`, 1 warning, dispatcher **vẫn** chạy kênh khác |
| 2 | Cue không có trong `entries` | không phát gì, không log |
| 3 | `Step = 0`, `perStep = 1`, `max = 12` | `pitchScale == 1` |
| 4 | `Step = 12`, `perStep = 1`, `max = 12` | `pitchScale == 2` (một quãng tám) |
| 5 | `Step = 50` | `pitchScale == 2` (bão hoà, **không** vọt) |
| 6 | Haptic `Step = 0`, `min = 0.3` | biên độ `0.3` — **không** im |
| 7 | Haptic `stepsToFull = 0` | `u = 1` ⇒ biên độ `max` ngay |
| 8 | Haptic 2 cue cách 0.02s, `minInterval = 0.05` | cú thứ hai bị bỏ |

- [ ] **Step 4: Commit** — `feat(sdk): add audio + haptic ramp channels`

---

### Task 5: Kênh thời gian + thị giác

**Files:** `Channels/HitstopChannel.cs` · `Channels/CameraShakeChannel.cs` · `FeedbackCameraRig.cs`

**Bản đồ toán → code:** lịch 2 pha ở §0.3 inline trong `HitstopChannel.OnTick` · `_elapsed += unscaledDeltaTime` (**không bao giờ** `Time.deltaTime`) · shake dùng `TraumaShake` (Task 1).

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `HitstopChannel` là **chủ sở hữu duy nhất** của `Time.timeScale` | Hai nơi cùng ghi là bug không thể lần ra |
| Lịch 2 pha **inline**, không tách `TimeScaleHelper` | Nội dung là một lerp 2 pha, và **không có user thứ hai**. Tách file cho một hàm 8 dòng chưa ai dùng lại là thừa; khi có slow-mo dài thì tách (ghi ở "Mở rộng sau") |
| Phục hồi `timeScale` trong `OnDisable` | Rời scene giữa lúc đóng băng = game **đứng vĩnh viễn**. Phải chặn bằng code, không bằng kỷ luật |
| Yêu cầu mới lúc đang chạy → **bỏ qua** | Cắt hẳn khối hợp nhất (`min`/`max` + thời lượng còn lại), và đồng thời chặn bẫy `_baseline` nhiễm bẩn (§0.3 #2) vì `_baseline` chỉ được chụp ở nhánh "chưa chạy" |
| `MinStep` trong entry | §0.3 #3 — guard **thiết kế** nên thuộc dữ liệu |
| `IFeedbackCamera` optional + SDK ship rig mặc định | Không có seam thì SDK phải biết camera của game (mất tính port); không có impl mặc định thì "thị giác" chỉ là lời hứa |
| Rig áp offset lên **transform của chính nó** | Rig là con của thứ đang follow player ⇒ shake **không** giằng với logic follow (lỗi kinh điển: shake ghi `camera.position` rồi bị follow ghi đè, thành nửa rung nửa không) |
| Kênh là `ITickable`, huỷ đăng ký ở `OnDisable` | Một nguồn tick; không unregister = ticker giữ reference chết |
| `_shakeTime` tích lũy **riêng**, không `Time.unscaledTime` | `unscaledTime` lớn dần vô hạn ⇒ mất chính xác float và noise lấy mẫu ở vùng rất xa gốc. Bộ đếm riêng reset khi hết shake |
| Áp offset `(0,0)` **một lần** khi vừa hết | Không có bước này thì camera đứng lại ở offset cuối, lệch vĩnh viễn |
| Thoát sớm khi `trauma == 0` / `!_isRunning` | Trạng thái phổ biến nhất ⇒ `OnTick` phải rẻ gần bằng 0 |

**Editor setup (§C.1) — cấu trúc camera là bước THẬT:**

1. Dựng hierarchy: `CameraFollow` (thứ đang bám player) → **`FeedbackCameraRig`** (GameObject mới) → `Camera`.
   Rig **phải** là con của thứ follow. Nếu đặt rig **là** thứ follow thì logic follow ghi `position` mỗi frame và ghi đè shake ⇒ "nửa rung nửa không".
2. Trên rig: add `FeedbackCameraRig`, kéo `Camera` con vào field `Target Camera`.
3. Add `HitstopChannel` + `CameraShakeChannel` lên `[Feedback]`.
4. `HitstopChannel` → `Entries`, 1 dòng: `CueId = 102` · `MinStep = 5` · `FreezeSeconds = 0.05` · `RecoverSeconds = 0.08` · `FreezeScale = 0` · `RecoverEase = OutQuad`.
   ⚠️ `CueId` này **khác** cue nhịp (101): hitstop chỉ nên bắn ở mốc lên tier, không mỗi nhịp (§0.3 #3).
5. `CameraShakeChannel` → `Entries`, 1 dòng: `CueId = 101` (cùng cue nhịp) · `TraumaMin = 0.15` · `TraumaMax = 0.5` · `StepsToFull = 10`.
6. Quay lại Task 3 bước 3: kéo cả 4 kênh vào mảng `Channels`.

- [ ] **Step 1: `FeedbackCameraRig.cs`**

```csharp
using Horcrux.Runtime.Abstractions.Feedback;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Feedback
{
    /// <summary>Impl mặc định của <see cref="IFeedbackCamera"/>: áp offset lên transform của CHÍNH NÓ.</summary>
    /// <remarks>
    /// Cách gắn: object này là CON của thứ đang follow player, và Camera là con của nó. Nhờ vậy shake
    /// không bao giờ giằng với logic follow — lỗi kinh điển là shake ghi <c>camera.position</c> rồi bị
    /// follow ghi đè ngay frame sau, cho ra "nửa rung nửa không".
    ///
    /// Vị trí/góc NỀN chụp ở <c>Awake</c>; mọi lệnh Apply cộng lên nền đó, nên
    /// <c>ApplyShake(Vector2.zero, 0)</c> luôn trả về đúng trạng thái ban đầu.
    ///
    /// <c>targetCamera</c> hiện chưa được dùng cho shake (shake chỉ đổi transform). Nó có mặt để
    /// interface zoom sau này (<c>IFeedbackCameraZoom</c>) không phải đi tìm camera lúc runtime.
    /// </remarks>
    [Service(typeof(IFeedbackCamera), FindFromScene = true)]
    public sealed class FeedbackCameraRig : MonoBehaviour, IFeedbackCamera
    {
        [Tooltip("Camera con của rig này. Chưa dùng cho shake; cần cho zoom punch sau này.")]
        [SerializeField] private Camera targetCamera;

        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;

        private void Awake()
        {
            _baseLocalPosition = transform.localPosition;
            _baseLocalRotation = transform.localRotation;

            // KHÔNG GetComponentInChildren: camera là thứ tồn tại lúc authoring ⇒ phải gán ở Inspector
            // (§C.1). Thiếu thì báo một lần ở đây, không đi tìm ngầm rồi lặng lẽ chọn sai camera.
            if (targetCamera == null)
                Debug.LogWarning("[Feedback] FeedbackCameraRig chưa gán Target Camera.", this);
        }

        public void ApplyShake(Vector2 localOffset, float rollDegrees)
        {
            transform.localPosition = _baseLocalPosition + new Vector3(localOffset.x, localOffset.y, 0f);

            // Nghiêng quanh trục NHÌN → nhân vào góc nền, không gán tuyệt đối.
            transform.localRotation = _baseLocalRotation * Quaternion.Euler(0f, 0f, rollDegrees);
        }
    }
}
```

- [ ] **Step 2: `HitstopChannel.cs`**

```csharp
using System;
using Horcrux.Runtime.Abstractions.Feedback;
using Horcrux.Runtime.Abstractions.Ticker;
using Horcrux.Runtime.Tweening.Easing;
using Sisus.Init;
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Feedback
{
    /// <summary>THỜI GIAN: đóng băng game vài chục ms đúng lúc "trúng", rồi hồi phục có easing.</summary>
    /// <remarks>
    /// Class này là CHỦ SỞ HỮU DUY NHẤT của <c>Time.timeScale</c> trong SDK.
    ///
    /// Hai điều tuyệt đối không được sai (§0.3):
    ///  ① Đo bằng <b>unscaled</b>. <c>deltaTime = unscaledDeltaTime × timeScale</c>, nên ở pha đóng
    ///    băng <c>deltaTime = 0</c> ⇒ bộ đếm dùng deltaTime không bao giờ tiến ⇒ đóng băng VĨNH VIỄN.
    ///  ② Chụp <c>_baselineScale</c> CHỈ khi chưa chạy. Đây là lý do yêu cầu mới trong lúc đang chạy
    ///    bị BỎ QUA: nếu xử lý nó mà chụp lại nền thì sẽ chụp được 0 (đang băng) ⇒ "phục hồi" về 0
    ///    ⇒ cũng đóng băng vĩnh viễn.
    /// </remarks>
    public sealed class HitstopChannel : MonoBehaviour<ITicker>, IFeedbackChannel, ITickable
    {
        /// <summary>Tham số thời gian cho một cue. Số cảm giác — tune ở Inspector (§0.5).</summary>
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private int cueId;

            [Tooltip("Chỉ khựng từ bậc này trở lên. Khựng MỖI nhịp đọc thành lag, không thành lực (§0.3).")]
            [SerializeField] private int minStep;

            [Tooltip("Thời lượng đóng băng (giây thực). 0.03–0.08 là khoảng dùng được.")]
            [SerializeField] private float freezeSeconds = 0.05f;

            [Tooltip("Thời lượng hồi phục. 0 = bật lại tức thì (cú khựng 'cứng').")]
            [SerializeField] private float recoverSeconds = 0.08f;

            [Tooltip("timeScale lúc đóng băng. 0 = đứng hẳn; 0.1 = 'nặng' thay vì 'đứng'.")]
            [Range(0f, 1f)]
            [SerializeField] private float freezeScale;

            [SerializeField] private EaseType recoverEase = EaseType.OutQuad;

            public int CueId => cueId;
            public int MinStep => minStep;
            public float FreezeSeconds => math.max(freezeSeconds, 0f);
            public float RecoverSeconds => math.max(recoverSeconds, 0f);
            public float FreezeScale => freezeScale;
            public EaseType RecoverEase => recoverEase;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private ITicker _ticker;

        private bool _isRunning;
        private float _elapsedUnscaled;
        private float _freezeSeconds;
        private float _recoverSeconds;
        private float _freezeScale;
        private float _baselineScale = 1f;
        private EaseType _recoverEase = EaseType.OutQuad;

        protected override void Init(ITicker ticker) => _ticker = ticker;

        private void OnEnable() => _ticker.AddTickListener(this);

        private void OnDisable()
        {
            _ticker?.RemoveTickListener(this);

            // Rời scene / tắt component giữa pha đóng băng = game ĐỨNG VĨNH VIỄN.
            RestoreBaseline();
        }

        public void Play(in FeedbackCue cue)
        {
            // Đang chạy thì bỏ qua: cắt hẳn khối hợp nhất, và chặn luôn bẫy _baseline nhiễm bẩn (§0.3).
            if (_isRunning) return;

            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                if (entry == null || entry.CueId != cue.Id.Value) continue;

                // Guard THIẾT KẾ: khựng mỗi nhịp đọc thành lag, không thành lực.
                if (cue.Step < entry.MinStep) return;

                // Chụp nền MỘT lần, lúc chắc chắn sạch. Không hardcode 1: game có thể đang slow-mo.
                _baselineScale = Time.timeScale;
                _freezeScale = entry.FreezeScale;
                _freezeSeconds = entry.FreezeSeconds;
                _recoverSeconds = entry.RecoverSeconds;
                _recoverEase = entry.RecoverEase;

                _elapsedUnscaled = 0f;
                _isRunning = true;
                Time.timeScale = _freezeScale;       // áp ngay frame này — khựng phải tức thì
                return;
            }
        }

        public void OnTick(float unscaledDeltaTime)
        {
            if (!_isRunning) return;                 // trạng thái phổ biến nhất → thoát rẻ nhất

            _elapsedUnscaled += unscaledDeltaTime;   // ① UNSCALED — xem remarks

            // Lịch 2 pha (§0.3), inline vì không có user thứ hai.
            if (_elapsedUnscaled < _freezeSeconds) return;        // pha 1: giữ nguyên _freezeScale

            if (_recoverSeconds <= 0f)                             // hồi tức thì (+ chặn chia 0)
            {
                RestoreBaseline();
                return;
            }

            float t = (_elapsedUnscaled - _freezeSeconds) / _recoverSeconds;

            if (t >= 1f)
            {
                RestoreBaseline();                                 // pha 3: giá trị CHÍNH XÁC
                return;
            }

            float k = Easer.Evaluate(_recoverEase, t);             // pha 2
            Time.timeScale = _freezeScale + (_baselineScale - _freezeScale) * k;
        }

        private void RestoreBaseline()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _elapsedUnscaled = 0f;
            Time.timeScale = _baselineScale;     // giá trị CHÍNH XÁC, không để sai số float đọng lại
        }
    }
}
```

- [ ] **Step 3: `CameraShakeChannel.cs`**

```csharp
using System;
using Horcrux.Runtime.Abstractions.Feedback;
using Horcrux.Runtime.Abstractions.Ticker;
using Horcrux.Runtime.Utilities.PhysXHelper;
using Sisus.Init;
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Feedback
{
    /// <summary>THỊ GIÁC: rung màn hình theo trauma tích lũy.</summary>
    /// <remarks>
    /// Toàn bộ toán ở <see cref="TraumaShake"/> (§0.4). Class này chỉ giữ state (trauma + bộ đếm thời
    /// gian) và bơm kết quả sang <see cref="IFeedbackCamera"/>.
    ///
    /// Bộ đếm thời gian RIÊNG (không dùng <c>Time.unscaledTime</c>): số đó lớn dần vô hạn ⇒ mất chính
    /// xác float, và noise bị lấy mẫu ở vùng rất xa gốc. Bộ đếm riêng reset khi hết shake.
    /// </remarks>
    public sealed class CameraShakeChannel : MonoBehaviour<ITicker>, IFeedbackChannel, ITickable
    {
        /// <summary>Tham số thị giác cho một cue. Số cảm giác — tune ở Inspector (§0.5).</summary>
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private int cueId;

            [Tooltip("Trauma cộng thêm ở bậc 0. Cộng dồn qua nhiều cue, kẹp trần 1.")]
            [Range(0f, 1f)]
            [SerializeField] private float traumaMin = 0.15f;

            [Range(0f, 1f)]
            [SerializeField] private float traumaMax = 0.5f;

            [Tooltip("Số bậc để đạt trauma tối đa. 0 = không ramp.")]
            [SerializeField] private int stepsToFull = 10;

            public int CueId => cueId;
            public float TraumaMin => traumaMin;
            public float TraumaMax => traumaMax;
            public int StepsToFull => stepsToFull;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        [Header("Shake (số cảm giác — §0.5)")]
        [Tooltip("Biên độ dịch chuyển (world unit) khi trauma = 1.")]
        [SerializeField] private float maxAmplitude = 0.35f;

        [Tooltip("Độ nghiêng tối đa (độ) khi trauma = 1.")]
        [SerializeField] private float maxRollDegrees = 2f;

        [Tooltip("Tần số lắc. Cao = 'rát', thấp = 'lắc lư'.")]
        [SerializeField] private float frequency = 18f;

        [Tooltip("Tốc độ hồi phục trauma (trauma/giây). 1.5 ⇒ trauma đầy tắt sau ~0.67s.")]
        [SerializeField] private float traumaDecayPerSecond = 1.5f;

        private ITicker _ticker;
        private IFeedbackCamera _camera;

        private float _trauma;
        private float _shakeTime;

        protected override void Init(ITicker ticker) => _ticker = ticker;

        private void OnEnable()
        {
            if (!IFeedbackCamera.TryGet(out _camera))
            {
                Debug.LogWarning("[Feedback] Không tìm thấy IFeedbackCamera — kênh thị giác tự tắt.", this);
                enabled = false;
                return;
            }

            _ticker.AddTickListener(this);
        }

        private void OnDisable()
        {
            _ticker?.RemoveTickListener(this);

            _trauma = 0f;
            _shakeTime = 0f;

            // Không trả về 0 thì camera đứng lại ở offset cuối, lệch vĩnh viễn một chút.
            _camera?.ApplyShake(Vector2.zero, 0f);
        }

        public void Play(in FeedbackCue cue)
        {
            if (!enabled) return;

            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                if (entry == null || entry.CueId != cue.Id.Value) continue;

                float u = cue.GetNormalizedStrength(entry.StepsToFull);                 // §0.2
                float amount = math.lerp(entry.TraumaMin, entry.TraumaMax, u);

                _trauma = TraumaShake.AddTrauma(_trauma, amount);                        // cộng dồn, kẹp 1
                return;
            }
        }

        public void OnTick(float unscaledDeltaTime)
        {
            if (_trauma <= 0f) return;          // trạng thái phổ biến nhất → thoát rẻ nhất

            _shakeTime += unscaledDeltaTime;
            _trauma = TraumaShake.DecayTrauma(_trauma, traumaDecayPerSecond, unscaledDeltaTime);

            Vector2 offset = TraumaShake.GetOffset(_trauma, maxAmplitude, _shakeTime, frequency);
            float roll = TraumaShake.GetRoll(_trauma, maxRollDegrees, _shakeTime, frequency);
            _camera.ApplyShake(offset, roll);

            if (_trauma > 0f) return;

            _shakeTime = 0f;                            // reset → noise không trôi xa gốc
            _camera.ApplyShake(Vector2.zero, 0f);       // chốt về nền CHÍNH XÁC
        }
    }
}
```

- [ ] **Step 4: Kiểm chứng**

| # | Input | Kỳ vọng |
|---|---|---|
| 1 | hitstop `(D=.05, R=.08, s=0)` ở `timeScale=1` | 0 trong 0.05s → ease lên 1 trong 0.08s → **đúng** 1 |
| 2 | Game đang `timeScale = 0.3`, hitstop rồi xong | phục hồi về **0.3** |
| 3 | hitstop A rồi B sau 0.02s | B **bị bỏ qua**; A kết thúc bình thường về baseline |
| 4 | `cue.Step = 3`, `MinStep = 5` | **không** khựng |
| 5 | `RecoverSeconds = 0` | băng xong nhảy thẳng về baseline |
| 6 | Disable component giữa pha băng | `timeScale` trở về baseline **ngay** |
| 7 | Không có `IFeedbackCamera` | kênh shake tắt + warning; hitstop/audio/haptic **vẫn** chạy |
| 8 | 1 cue trauma `0.5`, decay `1.5` | shake tắt hoàn toàn sau ~0.33s, camera về đúng nền |
| 9 | 3 cue liên tiếp trauma `0.5` | trauma kẹp `1`, không vọt |
| 10 | Không có cue nào đang chạy | `OnTick` thoát ngay, **0 B** alloc |
| 11 | 30 FPS vs 60 FPS, cùng cue | quỹ đạo shake **như nhau** |

- [ ] **Step 5: Cập nhật `Pendings.md`** — Nhóm 6-C (`Hitstop`, `TimeScaleHelper`) và Nhóm 8-I (`CameraPunch`): ghi rõ phần nào có plan ở đây, phần nào (slow-mo dài, zoom punch) vẫn chưa làm.

- [ ] **Step 6: Cập nhật `PendingSystems.md`** — thêm dòng §22 vào bảng tổng quan trỏ plan này.

- [ ] **Step 7: Commit** — `feat(sdk): add hitstop + camera shake channels`

---

## Ghi chú thực thi

- **Cần trước:** `TickerSystem.md` (toàn bộ), `HapticSystem.md` + `AudioSystem.md` (để nghiệm thu; kênh vẫn compile được nếu thiếu).
- **Editor setup:** nằm trong từng task (Task 3 · 4 · 5) — làm theo thứ tự task, bước cuối là Task 3 bước 3 (kéo 4 kênh vào mảng).
- **Tune:** mọi số cảm giác đã liệt kê ở **§0.5** kèm nơi tune. Không sửa code để tune.
- **Hệ dùng tiếp:** `ComboSystem.md` → `ComboFeedbackBridge`.
- **Mở rộng sau** (đều thêm file/component/interface mới, không sửa cái cũ):

| Mục | Cách thêm |
|---|---|
| Zoom punch | Interface **mới** `IFeedbackCameraZoom { void ApplyZoom(float) }`; rig implement thêm; driver dùng `DampedOscillator` với `WaveStyle.Cos` (biên độ đầy ngay `t=0` = cú đấm tức thì) |
| `FeedbackCue.Intensity` (hệ số cường độ của caller) | Thêm field + ctor overload; nhân vào 2 chỗ tính `strength` |
| Bảng cue chia sẻ giữa nhiều scene | Rút `Entry[]` của kênh ra `ScriptableObject`; kênh giữ 1 reference |
| Entry vượt ~10 dòng | Đổi quét tuyến tính sang `Dictionary` dựng ở `Awake` |
| Kênh mới (particle, floating text, ripple, color flash) | Thêm component implement `IFeedbackChannel`, kéo vào mảng `Channels` |
| Slow-mo dài (khác hitstop: có vào/ra chủ động) | Tách lịch 2 pha ra `TimeScaleHelper` ở `PhysXHelper` rồi dùng chung; API `Begin/End` ref-count thay vì cue một-lần |
| Kênh thêm/bớt lúc runtime | Thêm `AddChannel`/`RemoveChannel` + đổi mảng sang `DeferredList` |
