# Haptic System Implementation Plan

> **Loại tài liệu:** Plan — developer tự code lại để nắm logic. `.md` thiết kế + `.html` viết **sau** khi có source.
>
> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development hoặc superpowers:executing-plans. Steps dùng checkbox (`- [ ]`).

**Goal:** `IHapticService` — rung với **biên độ điều khiển được** (điều kiện bắt buộc để haptic ramp của combo *cảm* được), no-op im lặng trên thiết bị/nền tảng không hỗ trợ, và đổi lib rung chỉ sửa **1 file**.

**Architecture:** 2 tầng, tổng **6 file** (3 contract + 2 backend + 1 core).

```
Core (HapticService)      gate theo IsEnabled · chọn backend · KHÔNG chạm nền tảng
Backend (IHapticBackend)  2 member: IsSupported + PlayOneShot. Đổi lib = viết 1 file.
```

**Tech Stack:** C#, `AndroidJavaObject`, `Sisus.Init`, `Unity.Mathematics`. **Không** UniTask/Addressables.

## Global Constraints

| Ràng buộc | Giá trị |
|---|---|
| Namespace | `Horcrux.Runtime.Abstractions.Haptics` · `…Implementations.Haptics` |
| Zero-GC | `HapticPattern` là `readonly struct` truyền `in`; `AndroidJavaObject` cache **một lần** ở ctor |
| SOLID | Core không biết nền tảng (D) · backend không biết gate (S) · không type nào mang ngữ nghĩa game |
| Editor-first | Hệ này **không có** số cảm giác nào: mọi biên độ/thời lượng do caller (`HapticRampChannel`) truyền vào, và ở đó chúng đã phơi ra Inspector |
| An toàn | Không hỗ trợ → **no-op**, không throw. Mọi lệnh JNI bọc `try/catch` |
| Rung là **sự kiện** | Không có API rung-mỗi-frame |

## Ngữ cảnh đã chốt

| Nhóm | Chốt |
|---|---|
| **Ai gọi** | `FeedbackSystem.md` → `HapticRampChannel`: nhận cue có `Step` (= số combo), gọi `PlayCustom` với biên độ tăng dần. **Caller duy nhất.** |
| **Mục tiêu** | Combo phải **cảm** được, không chỉ nghe/thấy. Nghiệm thu: trên Android thật, combo 1 và combo 10 rung **khác nhau rõ rệt** khi cầm máy. |
| **Ngân sách** | Theo sự kiện, ~1–10 lần/giây lúc combo dồn. **Không** hot path CPU ⇒ chọn bản dễ đọc nhất, không tối ưu gì. Chi phí thật là **pin + cảm giác nhoè** nếu rung dày ⇒ throttle ở tầng gọi. |
| **Ranh giới** | Service **nhận** biên độ, không **tính** biên độ. Backend chỉ dịch số sang API nền tảng: không logic, không gate, không biết "preset" là gì. |
| **Hướng mở rộng thật** | Chắc chắn cần: backend NiceVibrations/iOS xịn → `IHapticBackend` + `SetBackend` đã sẵn, chỉ thêm 1 file. |
| **Cố ý KHÔNG làm + lý do** (NT 6: *xóa đi thì hỏng ở đâu*) | ① **`EHapticPreset` + `Play(preset)` + bảng preset** — đường preset **chết trong v1**: nhánh duy nhất gọi nó là `UseAmplitudeRamp = false`, ca đó chỉ xảy ra trên iOS, mà ở đó backend là `Null` nên không rung gì cả. Cắt nó cắt luôn 1 file enum + 1 class entry + `BuildPresetLookup` + `OnValidate` + 9 dòng Inspector phải điền. ② **`IHapticSettings`** — 0 implementation; `IsEnabled` vẫn còn (field), chỉ mất seam *lưu bền*, mà game set trực tiếp là đủ. ③ `BeginContinuous`/`EndContinuous`/`StopAll` + ref-count + vòng pulse + UniTask — combo không rung liên tục. ④ `HapticPattern.Frequency`/`PauseSeconds` — chỉ có nghĩa với waveform/rung liên tục. ⑤ Impl vendor — ngoài phạm vi SDK. |

> ⚠️ **Xung đột tên đã giải:** `HapticPattern` (file này) = **một** cú rung. Thứ mà `Pendings.md` Nhóm 8-J gọi là `HapticPattern` (*chuỗi* rung tăng dần theo combo) đổi tên thành `HapticRampChannel`, thuộc `FeedbackSystem.md`.

---

## §0. Hai ràng buộc thật

Không có toán, không có số cảm giác.

### 0.1. Motor rung có thời gian lên/tắt — hệ quả lên API

Motor (ERM hoặc LRA) cần một khoảng để đạt biên độ và một khoảng để tắt. Không có con số chuẩn (khác nhau theo máy), nhưng ba hệ quả thì chắc:

| Hiện tượng | Hệ quả lên API |
|---|---|
| Rung quá ngắn gần như không cảm được | `HapticPattern` có `DurationSeconds`, backend kẹp sàn 1ms |
| Hai cú rung quá gần **nhập lại thành một** | Ramp dày là tốn pin mà không thêm cảm giác ⇒ throttle ở `HapticRampChannel`, không ở đây |
| Rung liên tục tốn pin + nóng | Không có API nhận biên độ mỗi frame |

**Phép kiểm tái lập** (cần máy thật): `PlayCustom(new HapticPattern(1f, 0.005f))` — hầu như không cảm thấy; đổi `0.02f` — cảm rõ. Đó là cách xác định sàn cho **máy của bạn**, không phải hằng số phổ quát.

### 0.2. Android nhận biên độ là số nguyên `1..255`

`VibrationEffect.createOneShot(ms, amplitude)`: `amplitude` phải trong `1..255`. `0` **không hợp lệ**; `-1` là `DEFAULT_AMPLITUDE` ("để OS tự chọn") — không phải điều ta muốn khi đang điều khiển ramp.

```
amplitude255 = clamp(round(a * 255), 1, 255)      // a ∈ [0,1]
```

| Mốc | `a = 1` | `a = 0.5` | `a = 0.002` | `a = 5` |
|---|---|---|---|---|
| Kỳ vọng | `255` | `128` | `1` (**không** `0`) | `255` |

Ca `a = 0` bị chặn **trước** bởi `IsSilent` ở core — nó nghĩa là "không rung", không phải "rung nhẹ nhất".

Thang biên độ **tuyến tính** có chủ ý: cảm nhận cường độ rung thực tế phi tuyến, nhưng đường cong đó phụ thuộc thiết bị ⇒ việc nắn nó thuộc caller, backend là bộ dịch số.

> Dưới API 26 không có biên độ ⇒ rơi về `vibrate(ms)`: ramp chỉ đổi **thời lượng**, không đổi cường độ. Giới hạn nền tảng, không phải lỗi.
> iOS bằng API thuần Unity chỉ có `Handheld.Vibrate()` (buzz cố định) ⇒ ramp **không** cảm được. Muốn có, game cắm backend riêng — seam đã sẵn.

---

## Bản đồ triển khai

| Task | File | Nội dung |
|---|---|---|
| 1 | `Abstractions/Foundations/Haptics/` — `HapticPattern.cs` · `IHapticBackend.cs` · `IHapticService.cs` | contract |
| 2 | `Implementations/Foundations/Haptics/HapticService.cs` + `Backends/NullHapticBackend.cs` + `Backends/AndroidHapticBackend.cs` | core + 2 backend |

Thứ tự: **1 → 2**.

---

### Task 1: 3 contract

**Files:** 3 file trong `Assets/Horcrux/Runtime/Abstractions/Foundations/Haptics/`

**Interfaces:**
- Consumes: `IService<T>`.
- Produces: `readonly struct HapticPattern` (2 field) · `IHapticBackend` (2 member) · `IHapticService : IService<>` (3 member)

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `HapticPattern` chỉ **2 field** | Đủ cho ramp — caller duy nhất cần đúng (biên độ, thời lượng) |
| Kẹp giá trị **trong ctor** | Bất biến "pattern luôn hợp lệ" đúng ở **mọi** điểm dùng, không phải mọi backend |
| `IsSilent` là property của struct | Guard "không rung" viết một lần ở đây thay vì `amplitude <= 0f` rải ở core + mọi backend |
| `IHapticBackend` chỉ **2 member** | Backend càng mỏng thì viết backend vendor càng rẻ. Không có `PlayPreset` (không caller), không có `Stop` (không có gì đang phát để dừng khi chỉ rung một-nhịp) |
| `IHapticService` **3 member** | `IsSupported` cho UI biết có nên hiện toggle rung; `IsEnabled` cho toggle đó; `PlayCustom` cho caller |

- [ ] **Step 1: `HapticPattern.cs`**

```csharp
using Unity.Mathematics;

namespace Horcrux.Runtime.Abstractions.Haptics
{
    /// <summary>Mô tả MỘT cú rung.</summary>
    /// <remarks>
    /// Đây KHÔNG phải "chuỗi rung tăng dần theo combo" — cái đó là <c>HapticRampChannel</c> ở hệ
    /// Feedback, nơi biết ngữ cảnh để tính biên độ. Struct này chỉ chở tham số.
    ///
    /// Giá trị đã KẸP trong ctor nên mọi consumer nhận về một pattern luôn hợp lệ — không phải kẹp
    /// lại ở từng backend.
    /// </remarks>
    public readonly struct HapticPattern
    {
        /// <summary>Cường độ, miền [0,1] — thang trung tính; backend đổi sang đơn vị nền tảng.</summary>
        public readonly float Amplitude;

        /// <summary>Thời lượng, đơn vị GIÂY. Quá ngắn thì gần như không cảm được (§0.1).</summary>
        public readonly float DurationSeconds;

        /// <param name="amplitude">Cường độ; tự kẹp về [0,1]. Ai cấp: <c>HapticRampChannel</c>.</param>
        /// <param name="durationSeconds">Thời lượng (giây); tự kẹp về ≥ 0.</param>
        public HapticPattern(float amplitude, float durationSeconds = 0.02f)
        {
            Amplitude = math.saturate(amplitude);
            DurationSeconds = math.max(durationSeconds, 0f);
        }

        /// <summary>Biên độ 0 ⇒ không có gì để rung; core thoát sớm, không gọi xuống backend.</summary>
        public bool IsSilent => Amplitude <= 0f;
    }
}
```

- [ ] **Step 2: `IHapticBackend.cs` + `IHapticService.cs`**

```csharp
// ── IHapticBackend.cs ─────────────────────────────────────────────────────
namespace Horcrux.Runtime.Abstractions.Haptics
{
    /// <summary>Bộ dịch mỏng từ tham số trung tính sang API nền tảng/lib. 2 member, KHÔNG logic.</summary>
    /// <remarks>
    /// Seam duy nhất chạm nền tảng: đổi sang NiceVibrations nghĩa là viết một file implement interface
    /// này rồi gọi <c>HapticService.SetBackend</c>, không sửa dòng nào của core.
    ///
    /// Backend KHÔNG được đọc cờ bật/tắt của người chơi — việc đó thuộc core (S trong SOLID); nếu
    /// backend cũng làm thì có hai nguồn sự thật.
    /// </remarks>
    public interface IHapticBackend
    {
        /// <summary>Thiết bị/nền tảng này rung được không. <c>false</c> ⇒ core no-op sớm.</summary>
        bool IsSupported { get; }

        /// <param name="pattern">Đã được core kẹp và lọc <c>IsSilent</c> — backend không cần kiểm lại.</param>
        void PlayOneShot(in HapticPattern pattern);
    }
}

// ── IHapticService.cs ─────────────────────────────────────────────────────
namespace Horcrux.Runtime.Abstractions.Haptics
{
    /// <summary>Facade rung duy nhất mà game/SDK gọi. Không nơi nào khác được chạm API nền tảng.</summary>
    /// <remarks>
    /// Kiểm cờ bật/tắt MỘT lần ở đầu entry point — rải khắp call-site là chắc chắn có một chỗ rung dù
    /// người chơi đã tắt.
    ///
    /// Muốn lưu bền cờ này: game đọc setting của nó rồi set <see cref="IsEnabled"/> lúc bootstrap.
    /// SDK cố tình không sở hữu hệ save.
    /// </remarks>
    public interface IHapticService : IService<IHapticService>
    {
        /// <summary>Thiết bị rung được không. Dùng để quyết định có hiện toggle rung trong UI settings.</summary>
        bool IsSupported { get; }

        /// <summary>Cờ người chơi. Mặc định <c>true</c>; game set lại từ setting của nó lúc bootstrap.</summary>
        bool IsEnabled { get; set; }

        /// <summary>Rung một nhịp — đường dùng của haptic ramp (biên độ theo bậc combo).</summary>
        void PlayCustom(in HapticPattern pattern);
    }
}
```

- [ ] **Step 3: Kiểm chứng** — `new HapticPattern(1.7f, -1f)` → `Amplitude == 1`, `DurationSeconds == 0`. `new HapticPattern(0f).IsSilent == true`.

- [ ] **Step 4: Commit** — `feat(sdk): add haptic contracts`

---

### Task 2: Core + 2 backend

**Files:**
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Haptics/Backends/NullHapticBackend.cs`
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Haptics/Backends/AndroidHapticBackend.cs`
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Haptics/HapticService.cs`

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `NullHapticBackend` no-op, **luôn tồn tại** | Null Object: core không bao giờ phải kiểm `_backend == null` ⇒ đọc code thẳng một mạch, không có nhánh null nào |
| Cache `AndroidJavaObject` **một lần** trong ctor | `new AndroidJavaClass(...)` mỗi lần rung là alloc + attach JNI. Đây là chỗ **duy nhất** trong hệ đáng tối ưu |
| Kiểm `SDK_INT >= 26` một lần → `bool` | `VibrationEffect` chỉ có từ API 26; đọc `SDK_INT` mỗi lần rung là gọi JNI vô ích |
| `#if UNITY_ANDROID && !UNITY_EDITOR` | Editor không có JNI ⇒ compile được nhưng runtime throw. Bọc rồi trả `IsSupported = false` là hành vi đúng |
| `try/catch` quanh JNI | ROM cắt vibrator vẫn không được crash game vì một cú rung |
| `SetBackend` public | Đây là **cách duy nhất** game cắm NiceVibrations mà không sửa file SDK — xoá nó là xoá mục tiêu "đổi lib sửa 1 file". `null` → rơi về no-op |
| **Không** `LazyInit` | Khởi tạo JNI lúc cần rung lần đầu sẽ hitch đúng khoảnh khắc cần mượt |

**Editor setup — bước thật:**

1. Tạo GameObject `[Haptic]` ở scene bootstrap → add `HapticService`.
2. Không có field nào cần gán — hệ này không có số cảm giác (chúng ở `HapticCueTable` bên `FeedbackSystem.md`).

- [ ] **Step 1: 2 backend**

```csharp
// ── NullHapticBackend.cs ──────────────────────────────────────────────────
using Horcrux.Runtime.Abstractions.Haptics;

namespace Horcrux.Runtime.Implementations.Haptics
{
    /// <summary>Backend no-op — dùng khi nền tảng không hỗ trợ hoặc game chưa cắm backend thật.</summary>
    /// <remarks>
    /// Null Object pattern: nhờ nó, <c>HapticService</c> KHÔNG bao giờ phải kiểm
    /// <c>_backend == null</c> ⇒ code đọc thẳng một mạch.
    /// </remarks>
    public sealed class NullHapticBackend : IHapticBackend
    {
        public bool IsSupported => false;
        public void PlayOneShot(in HapticPattern pattern) { }
    }
}

// ── AndroidHapticBackend.cs ───────────────────────────────────────────────
using Horcrux.Runtime.Abstractions.Haptics;
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Haptics
{
    /// <summary>
    /// Backend Android dùng <c>VibrationEffect</c> (API 26+) để điều khiển BIÊN ĐỘ — điều kiện bắt
    /// buộc để haptic ramp cảm nhận được.
    /// </summary>
    /// <remarks>
    /// Dưới API 26 rơi về <c>vibrate(ms)</c>: ramp chỉ đổi thời lượng, không đổi cường độ (§0.2).
    /// iOS: Unity chỉ cho <c>Handheld.Vibrate()</c> (buzz cố định) ⇒ ramp không cảm được bằng API
    /// thuần Unity; game cắm <see cref="IHapticBackend"/> riêng dùng Core Haptics/NiceVibrations.
    /// </remarks>
    public sealed class AndroidHapticBackend : IHapticBackend
    {
        private const int AmplitudeControlApiLevel = 26;

        private readonly AndroidJavaObject _vibrator;
        private readonly AndroidJavaClass _effectClass;
        private readonly bool _hasAmplitudeControl;

        public bool IsSupported { get; }

        public AndroidHapticBackend()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                _hasAmplitudeControl = version.GetStatic<int>("SDK_INT") >= AmplitudeControlApiLevel;

                if (_hasAmplitudeControl) _effectClass = new AndroidJavaClass("android.os.VibrationEffect");

                IsSupported = _vibrator != null && _vibrator.Call<bool>("hasVibrator");
            }
            catch (System.Exception e)
            {
                // ROM cắt vibrator: no-op còn tốt hơn crash game vì một cú rung.
                Debug.LogWarning("[Haptic] Android backend init failed, haptics disabled.");
                Debug.LogException(e);
                IsSupported = false;
            }
#else
            IsSupported = false;      // Editor/nền tảng khác: không có JNI
#endif
        }

        public void PlayOneShot(in HapticPattern pattern)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsSupported) return;

            int milliseconds = ToMilliseconds(pattern.DurationSeconds);
            int amplitude255 = ToAmplitude255(pattern.Amplitude);

            try
            {
                if (_hasAmplitudeControl)
                {
                    using AndroidJavaObject effect = _effectClass.CallStatic<AndroidJavaObject>(
                        "createOneShot", (long)milliseconds, amplitude255);
                    _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", (long)milliseconds);   // API < 26: chỉ có thời lượng
                }
            }
            catch (System.Exception e) { Debug.LogException(e); }
#endif
        }

        /// <summary>Biên độ [0,1] → thang Android 1..255 (§0.2).</summary>
        /// <remarks>
        /// Kẹp SÀN ở 1, không 0: Android coi 0 là không hợp lệ. Ca amplitude = 0 đã bị chặn từ trước
        /// bởi <c>IsSilent</c> ở core.
        /// </remarks>
        private static int ToAmplitude255(float amplitude)
            => (int)math.clamp(math.round(amplitude * 255f), 1f, 255f);

        /// <summary>Giây → ms, kẹp sàn 1: <c>vibrate(0)</c> là no-op im lặng, rất khó lần.</summary>
        private static int ToMilliseconds(float seconds)
            => (int)math.max(math.round(seconds * 1000f), 1f);
    }
}
```

- [ ] **Step 2: `HapticService.cs`**

```csharp
using Horcrux.Runtime.Abstractions.Haptics;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Haptics
{
    /// <summary>Core rung: gate theo cờ người chơi + chọn backend. KHÔNG chạm nền tảng.</summary>
    /// <remarks>
    /// Mọi thứ phụ thuộc nền tảng nằm sau <see cref="IHapticBackend"/> (2 member), nên toàn bộ logic
    /// ở đây đúng như nhau trên Android/iOS/Editor, và đổi lib rung không sửa file này.
    ///
    /// Game cắm lib xịn bằng <see cref="SetBackend"/> trong bước bootstrap của nó, TRƯỚC lần rung đầu.
    /// </remarks>
    [Service(typeof(IHapticService), FindFromScene = true)]
    public sealed class HapticService : MonoBehaviour, IHapticService
    {
        private IHapticBackend _backend = new NullHapticBackend();   // không bao giờ null

        public bool IsSupported => _backend.IsSupported;

        /// <summary>Mặc định bật; game set lại từ setting của nó lúc bootstrap.</summary>
        public bool IsEnabled { get; set; } = true;

        private void Awake()
        {
            DontDestroyOnLoad(this);

#if UNITY_ANDROID && !UNITY_EDITOR
            _backend = new AndroidHapticBackend();
#else
            // iOS bằng API thuần Unity chỉ có Handheld.Vibrate (không biên độ) ⇒ ramp không cảm được.
            // Giữ no-op để hành vi TRUNG THỰC: muốn haptic trên iOS thì cắm backend riêng.
#endif
        }

        /// <summary>Cắm backend do game cấp. Gọi trong bootstrap, trước lần rung đầu.</summary>
        /// <param name="backend"><c>null</c> ⇒ rơi về no-op, KHÔNG để field null.</param>
        public void SetBackend(IHapticBackend backend) => _backend = backend ?? new NullHapticBackend();

        public void PlayCustom(in HapticPattern pattern)
        {
            // Gate DUY NHẤT của hệ. Rải kiểm tra ở nhiều entry point là chắc chắn quên một chỗ.
            if (!IsEnabled || !_backend.IsSupported || pattern.IsSilent) return;

            _backend.PlayOneShot(pattern);
        }
    }
}
```

- [ ] **Step 3: Kiểm chứng**

| Input | Kỳ vọng |
|---|---|
| `PlayCustom(...)` khi `IsEnabled = false` | không gọi backend |
| `PlayCustom(...)` trên Editor | không throw, no-op (`IsSupported == false`) |
| `PlayCustom(new HapticPattern(0f))` | không gọi backend |
| `SetBackend(null)` | rơi về no-op, không NRE ở lần `PlayCustom` sau |
| `ToAmplitude255(1f)` / `(0.5f)` / `(0.002f)` / `(5f)` | `255` / `128` / `1` / `255` |
| `ToMilliseconds(0.02f)` / `(0.0001f)` | `20` / `1` (không `0`) |

- [ ] **Step 4: Cập nhật `PendingSystems.md` §9** — trỏ plan này, ghi rõ contract ở §9 có `EHapticPreset`/`BeginContinuous` mà plan này cắt.

- [ ] **Step 5: Commit** — `feat(sdk): add HapticService (amplitude-controlled one-shot)`

---

## Ghi chú thực thi

- **Kiểm thật trên máy:** haptic **không** kiểm được trong Editor. Bắt buộc build 1 APK và cảm bằng tay: ramp combo 1 → 10 phải cảm thấy leo. Tune biên độ ở `HapticCueTable` (`FeedbackSystem.md`).
- **Hệ dùng tiếp:** `FeedbackSystem.md` → `HapticRampChannel`.
- **Mở rộng sau** (đều **additive** — thêm file/method/field, không đổi chữ ký hiện có): `EHapticPreset` + `Play(preset)` + bảng preset trong Inspector (khi có nhu cầu rung không-ramp thật, vd nút bấm) · `BeginContinuous`/`EndContinuous` + ref-count + vòng pulse · `IHapticBackend.PlayPreset` (khi backend vendor muốn dùng preset native của nó) · waveform nhiều nhịp · backend iOS Core Haptics / NiceVibrations · `IHapticSettings` để lưu bền cờ `IsEnabled`.
