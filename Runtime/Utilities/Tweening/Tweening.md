# Hệ thống Tween Horcrux — Implementation Plan (v1 xương sống)

> **For agentic workers:** REQUIRED SUB-SKILL: dùng `superpowers:subagent-driven-development` (khuyến nghị) hoặc `superpowers:executing-plans` để triển khai plan theo từng task. Các step dùng checkbox (`- [ ]`) để theo dõi.

**Goal:** Xây hệ Tween zero-GC, awaitable, stop-được-per-object, chạy trên hệ Ease có sẵn (`Easer.Evaluate`), thay thế DOTween/LitMotion cho cả UI và gameplay.

**Architecture:** Hybrid data-oriented — mọi tween là một `struct TweenState` sống trong mảng grow-only, được một `TweenRunner` (MonoBehaviour, auto-bootstrap + `[Service]`) tick mỗi frame. Giá trị mọi kiểu (float/Vector2/Vector3/Color/Quaternion) đóng gói vào `float4` (Unity.Mathematics). Mỗi "kiểu chuyển động" là một `TweenApplier` stateless singleton đăng ký trong registry theo id — thêm loại tween mới = thêm applier, **không sửa lõi** (Open/Closed). Developer chỉ cầm một `readonly struct TweenHandle {slot, version}` để control/await/stop an toàn.

**Tech Stack:** C# (Unity), Unity.Mathematics (`float4`, `math`), UniTask (await zero-GC), InitArgs/`Sisus.Init` (DI qua `[Service]`), hệ Ease Horcrux có sẵn.

## Global Constraints

Mọi task đều ngầm chịu các ràng buộc sau (copy nguyên văn từ quyết định thiết kế):

- **Namespace:** gốc `Horcrux.Runtime.Tweening`; ease có sẵn ở `Horcrux.Runtime.Tweening.Easing`.
- **Assembly:** `com.horcrux.runtime`, `allowUnsafeCode: false` → **cấm** `unsafe`/pointer; dùng `Unity.Mathematics` thay thế. Đã có ref: `InitArgs`, `InitArgs.Services`, `UniTask`, `Unity.Mathematics`. **Cần thêm** `Unity.ugui` (Task 6) cho `Image`/`Graphic`.
- **Zero-GC hot path:** từ sau `Create()`, vòng tick **không** `new` ref-type, **không** LINQ, **không** closure, **không** string concat, **không** boxing. `struct` + `in`/`ref` cho data. Pool grow-only, tái dùng slot.
- **OOP/SOLID:** mỗi file 1 trách nhiệm; mở rộng qua applier mới (không sửa `TweenRunner`); phụ thuộc abstraction (`ITweenRunner`) qua InitArgs.
- **Self-document:** tên nói rõ mục đích (`TweenPosition` ≠ `DoMove`); boolean là câu hỏi (`IsActive`); XML doc + "tại sao" ở API public; comment chỉ nói *tại sao*.
- **Try/catch per callback:** 1 listener lỗi không kill listener khác, không kill vòng tick.
- **Verify style (MY_SKILL §5.3):** mỗi task kết bằng **bảng kiểm chứng input→kỳ vọng** để tự đối chiếu khi triển khai; **không** kèm code test (dự án chưa có Unity Test Framework).

**Phạm vi v1 (chốt):** value types {float, Vector2, Vector3, Color, Quaternion}; adapter {position, localPosition, anchoredPosition, scale, localEuler, rotation(quat), spriteColor, graphicColor, canvasGroupAlpha, fillAmount}; jump parabol + **đích động** (follow Transform); ease = `EaseType`; loop {Yoyo, Restart, Incremental} hữu hạn & vô hạn; delay + relative; timescale scaled/unscaled; tick Update/Late/Fixed; await UniTask + `TweenStopBehaviour`; events {Start, Update, Complete, Stop, StepComplete}.

**Hoãn (note phát triển sau — thiết kế phải chừa chỗ, không sửa lõi khi thêm):** Sequence object (Append/Join/Insert); AnimationCurve-as-ease; punch/shake/bouncy-scale presets; tách hot/cold state; dense-packing slot; `.From()` tường minh; TMP color applier.

---

# §0 — Dẫn giải cơ chế (đọc trước khi code)

Hệ này không có lõi toán nặng như solver dao động, nhưng có **4 cơ chế nền** mà mọi task xây trên. Hiểu 4 cái này trước thì code các task chỉ là diễn đạt lại.

## §0.1 — Nội suy: bản chất một tween

**Bản chất:** một tween biến giá trị từ `from` → `to` theo thời gian, "cong" lại bằng hàm ease.

| Thành phần | Vai trò |
|---|---|
| `elapsed` | thời gian đã trôi kể từ khi tween bắt đầu chạy thật (sau delay) |
| `duration` | tổng thời gian tween |
| `t` | tiến trình chuẩn hoá `= elapsed / duration`, kẹp `[0,1]` |
| `k = Easer.Evaluate(ease, t)` | hệ số đã "cong", có thể vượt `[0,1]` (Back/Elastic/Bounce) |
| `value = lerp(from, to, k)` | giá trị áp vào đối tượng frame này |

**Công thức chốt:**

$$t = \text{elapsed} \times \text{invDuration}, \quad k = \text{Easer}(t), \quad v = \text{from} + (\text{to} - \text{from}) \times k$$

**Lý do `invDuration` thay vì chia:** chia (`/`) tốn ~20–40 chu kỳ CPU, nhân (`*`) tốn ~4–5. Tween tick mỗi frame × hàng trăm tween → precompute `invDuration = 1/duration` **một lần** lúc `Create()`, hot path chỉ nhân. (MY_SKILL §3.5: "chia→nhân".)

**Kiểm mốc:**

| Mốc | Kỳ vọng | Vì sao |
|---|---|---|
| `t=0` | `v = from` | `k=Easer(0)=0` với mọi ease → `v = from + 0` |
| `t=1` | `v = to` | `k=Easer(1)=1` → `v = from + (to-from)` |
| `t` vượt do ease | `v` có thể vượt `[from,to]` | Back/Elastic cố ý overshoot → phải dùng **lerp unclamped** (`math.lerp` không kẹp) |

> ⚠️ Dùng `math.lerp` (unclamped), **không** `Vector3.Lerp`/`Color.Lerp` (kẹp `[0,1]`) — nếu kẹp thì OutBack/Elastic mất overshoot. Đây là lý do `Easer` doc ghi "phải dùng unclamped interpolation".

## §0.2 — Đóng gói float4: một storage cho mọi kiểu

**Vấn đề:** runner phải tick chung tween `float` lẫn `Vector3` lẫn `Color` trong một mảng, mà **không boxing** (boxing = rác).

**Giải pháp:** mọi kiểu giá trị nhét vừa `float4` (16 byte, 4×float). `TweenState` là `struct` **non-generic** chứa `float4 from, to`. Mỗi kiểu quy ước packing riêng:

| Kiểu | Pack vào float4 | Unpack |
|---|---|---|
| `float` | `(x, 0, 0, 0)` | `v.x` |
| `Vector2` | `(x, y, 0, 0)` | `v.xy` |
| `Vector3` | `(x, y, z, 0)` | `v.xyz` |
| `Color` | `(r, g, b, a)` | `new Color(v.x,v.y,v.z,v.w)` |
| `Quaternion` | `(x, y, z, w)` | `new Quaternion(...)` |

**Vì sao float4 (Unity.Mathematics) mà không phải 4 field float rời:** `math.lerp(float4, float4, float)` chạy SIMD (1 lệnh cho 4 lane) — nhanh hơn 4 phép lerp scalar; và `float4` khớp layout để mở rộng Job/Burst sau này. Dự án đã ref `Unity.Mathematics`.

**Trade-off (nêu rõ):** packing gộp mọi kiểu vào 1 struct → kém "type-safe" hơn generic-per-type; bù lại storage đồng nhất, tick một vòng lặp phẳng (cache-friendly nhất), ít type để đọc/paste. Type-safety được che hoàn toàn ở API bề mặt (extension method nhận đúng kiểu Unity).

## §0.3 — Applier registry: mở rộng không sửa lõi (Open/Closed)

**Vấn đề:** runner không được biết "position ghi thế nào", "color ghi thế nào" — nếu biết thì thêm kiểu mới phải sửa runner (vi phạm O).

**Giải pháp:** một `abstract TweenApplier` định nghĩa 3 việc: `Capture` (đọc giá trị hiện tại của target → float4), `Apply` (ghi float4 → target), `Interpolate` (nội suy, mặc định `math.lerp`, override cho slerp/jump). Mỗi applier là **một singleton stateless** (tạo 1 lần lúc bootstrap, không cấp phát per-tween), đăng ký vào registry và nhận một `int id`. `TweenState` giữ `int kind = id` (để đăng ký/debug) và **cache thẳng `TweenApplier applier`** lúc `Create` — tick dùng `s.applier` trực tiếp, KHÔNG tra registry mỗi frame (fix CPU).

```
   TweenState {kind=3, applier=ScaleApplier(cached), target=Transform, from, to}
        │  tick dùng s.applier trực tiếp (registry chỉ tra 1 lần lúc Create)
        ▼
   ScaleApplier.Interpolate(in s, t, k) → ScaleApplier.Apply(target, v)
```

**Vì sao stateless singleton:** target cụ thể (Transform này/Image kia) sống trong `TweenState.target` (`object`, chỉ là reference — gán không boxing vì class). Applier không giữ state → 1 instance phục vụ mọi tween cùng kiểu → zero cấp phát. Thêm "màu HSV"/"shader property" sau = viết applier mới + `Register(...)`, **không đụng runner** (Open/Closed).

## §0.4 — Handle + version: chống "stale handle" giết nhầm

**Vấn đề:** slot trong pool được tái dùng. Tween A xong → slot 5 trả về pool → tween B chiếm slot 5. Nếu code còn giữ handle cũ của A rồi gọi `Stop()`, nó sẽ **giết nhầm B**.

**Giải pháp:** mỗi slot có một `version` (int, tăng mỗi lần tái dùng). `TweenHandle` giữ `{slot, version}`. Mọi thao tác qua handle so `handle.version == states[slot].version`:

| Tình huống | version handle | version slot | Kết quả |
|---|---|---|---|
| Tween còn sống | 12 | 12 | thao tác thực thi |
| Tween A đã xong, slot tái dùng cho B | 12 | 13 | **bỏ qua an toàn** (không giết B) |

**Công thức "sống":** `IsActive ⇔ states[slot].inUse && states[slot].version == handle.version`.

> ⚠️ **Bất biến version ≥ 1 (chống `default(TweenHandle)` khớp nhầm):** slot MỚI cấp phát phải bắt đầu ở `version = 1`, KHÔNG phải 0. Lý do: `default(TweenHandle)` = `{slot=0, version=0}` (mọi handle rỗng/chưa khởi tạo). Nếu slot 0 — tween ĐẦU TIÊN của game — mang `version=0`, thì `IsAlive(0,0)` trả `true` → một handle rỗng vô tình `Stop()`/mutate được tween thật. Giữ `version 0` làm giá trị "vô hiệu" dành riêng cho default handle. `Return` luôn `version++` nên slot tái dùng không bao giờ quay lại 0. (Xem `Rent()` Task 9.)

Đây là thứ thay 3 kiểu định danh lộn xộn ở 4 dự án khảo sát (`DOKill` / `Kill(id)` / `SetId`) bằng **một** mô hình duy nhất.

## §0.5 — Vòng đời một tween (data flow tổng)

```
 API extension: transform.TweenPosition(to, dur)
      │  runner.Create(kind, target, to, dur) → Rent() slot từ free-list
      ▼  trả TweenHandle{slot, version}; add slot vào Update bucket
 .SetEase(OutBack).SetLoops(2,Yoyo).OnComplete(cb)   (fluent trên handle, mutate _states[slot])
      │  (SetTickPhase → MoveBucket dời sang bucket khác nếu cần)
      ▼─────────── mỗi frame (Update / LateUpdate / FixedUpdate) ───────────
 TickBucket: snapshot (slot,version) vào _tickBuffer → duyệt buffer:
   if !IsAlive(slot,version): continue                 // đã bị stop giữa frame
   StepTween(slot, version, dt):
     truy cập qua _states[slot] (index, KHÔNG giữ ref xuyên callback)
     if !hasStarted:  trừ delay; nếu hết → from=Capture(target); resolve relative; onStart(); kiểm IsAlive
     if dynamicSource: to = dynamicSource(); kiểm IsAlive          // đích động
     elapsed += (unscaled? unscaledDt : dt)
     t = min(1, elapsed*invDuration);  k = Easer(ease,t)
     v = applier.Interpolate(in _states[slot], t, k);  applier.Apply(target, v)
     onUpdate(t); kiểm IsAlive
     if t>=1:  OnCycleEnd → onStepComplete(); loop tiếp | Finish()
      │
      ▼
 Finish: gỡ bucket + Return(slot) TRƯỚC (version++), rồi bắn onComplete/onStop + signal awaiter
 await handle → UniTask (source pooled, 0 GC ngoài source) → chạy logic sau khi xong
 handle.Stop() / target.StopTweens() → version-check → Finish → callback + release
```

---

# Bản đồ file (khoá quyết định decompose)

```
Runtime/Utilities/Tweening/
├─ Easings/                         ← ĐÃ CÓ (EaseType, Easer, Curves)
├─ Config/
│   ├─ LoopMode.cs                  ← enum Restart/Yoyo/Incremental
│   ├─ TimeMode.cs                  ← enum Scaled/Unscaled
│   ├─ TickPhase.cs                 ← enum Update/LateUpdate/FixedUpdate
│   └─ TweenStopBehaviour.cs        ← enum khi await bị Stop
├─ Core/
│   ├─ TweenCallback.cs             ← struct callback (Action + state overload, try/catch)
│   ├─ TweenState.cs                ← struct state 1 tween (float4 packing) + TweenPack
│   ├─ TweenApplier.cs              ← abstract base + static TweenAppliers (registry)
│   ├─ TweenHandle.cs               ← readonly struct {slot,version} + fluent config/control/await
│   ├─ ITweenRunner.cs              ← abstraction (DI qua Service.Set)
│   └─ TweenRunner.cs               ← MonoBehaviour: pool+bucket+tick+await, auto-bootstrap
├─ Appliers/
│   ├─ PositionApplier.cs  LocalPositionApplier.cs  ScaleApplier.cs
│   ├─ LocalEulerApplier.cs  RotationApplier.cs      (Transform)
│   ├─ AnchoredPositionApplier.cs  CanvasGroupAlphaApplier.cs
│   ├─ SpriteColorApplier.cs  GraphicColorApplier.cs  FillApplier.cs   (UI/SpriteRenderer)
│   ├─ JumpApplier.cs               ← position + arc parabol, đích động
│   └─ LambdaApplier.cs             ← cửa Tween.To (float getter/setter, chấp nhận 1 alloc)
├─ Api/
│   ├─ TransformTweenExtensions.cs  ← transform.TweenPosition/Scale/Rotate/Jump...
│   ├─ UITweenExtensions.cs         ← Image/CanvasGroup/SpriteRenderer/RectTransform
│   └─ Tween.cs                     ← factory tĩnh: Tween.To(getter,setter,to,dur)
└─ Tweening.md                      ← tài liệu này
```

**Thứ tự phụ thuộc (nền trước, dùng lại sau):** Config → TweenCallback → TweenState → TweenApplier(+registry) → Appliers → TweenHandle → ITweenRunner → TweenRunner → Api. Mỗi task chỉ dùng thứ đã có ở task trước. (Await nằm TRONG `TweenRunner`, không phải file riêng.)

> **Điều chỉnh so với phần thiết kế:** (1) bỏ `TweenBuilder` riêng — config (SetEase/SetLoops...) là **method fluent trên `TweenHandle`**, mutate trực tiếp state đang sống (tween tạo ở `hasStarted=false`, config trước frame đầu); API giống DOTween (`transform.TweenPosition(...).SetEase(...)` trả handle luôn). (2) Await xử lý **trong** `TweenRunner` (dict + completion source), không tách file `Await/`. (3) `Tween.To` chỉ nhận **float** getter/setter ở v1 (không generic `<T>`) — case hiếm cho shader/audio/scalar. (4) Stop-per-target dùng **linear scan** ở v1 — dictionary Transform→slots hoãn sang bản sau.

---

### Task 1: Config enums

**Files:**
- Create: `Runtime/Utilities/Tweening/Config/LoopMode.cs`
- Create: `Runtime/Utilities/Tweening/Config/TimeMode.cs`
- Create: `Runtime/Utilities/Tweening/Config/TickPhase.cs`
- Create: `Runtime/Utilities/Tweening/Config/TweenStopBehaviour.cs`

**Interfaces:**
- Consumes: —
- Produces: `LoopMode {Restart, Yoyo, Incremental}`, `TimeMode {Scaled, Unscaled}`, `TickPhase {Update, LateUpdate, FixedUpdate}`, `TweenStopBehaviour {CancelAwaitSilently, CompleteImmediately, ThrowCancellation}` (CancelAwaitSilently ở vị trí 0 — default an toàn).

- [ ] **Step 1: Tạo 4 file enum**

```csharp
// LoopMode.cs
namespace Horcrux.Runtime.Tweening
{
    /// <summary>Cách một tween lặp lại sau mỗi chu kỳ.</summary>
    public enum LoopMode
    {
        /// <summary>Nhảy về from, chạy lại từ đầu (A→B, A→B, ...).</summary>
        Restart,
        /// <summary>Đảo chiều mỗi chu kỳ (A→B, B→A, ...). Dùng cho idle nhấp nhô / warning flash.</summary>
        Yoyo,
        /// <summary>Cộng dồn: mỗi chu kỳ dịch from/to thêm (to-from). Dùng cho spin tích lũy &gt;360°.</summary>
        Incremental
    }
}
```

```csharp
// TimeMode.cs
namespace Horcrux.Runtime.Tweening
{
    /// <summary>Dòng thời gian tween chạy theo.</summary>
    public enum TimeMode
    {
        /// <summary>Theo Time.deltaTime — bị slow-mo/pause (Time.timeScale) ảnh hưởng. Mặc định gameplay.</summary>
        Scaled,
        /// <summary>Theo Time.unscaledDeltaTime — chạy cả khi game pause. Mặc định UI popup/button.</summary>
        Unscaled
    }
}
```

```csharp
// TickPhase.cs
namespace Horcrux.Runtime.Tweening
{
    /// <summary>Giai đoạn trong vòng đời frame mà tween được tick.</summary>
    public enum TickPhase
    {
        /// <summary>Update — mặc định, đa số tween.</summary>
        Update,
        /// <summary>LateUpdate — sau layout/animation (vd bám camera, sau khi Animator ghi Transform).</summary>
        LateUpdate,
        /// <summary>FixedUpdate — bám vật lý.</summary>
        FixedUpdate
    }
}
```

```csharp
// TweenStopBehaviour.cs
namespace Horcrux.Runtime.Tweening
{
    /// <summary>
    /// Hành vi của lời gọi <c>await</c> khi tween bị Stop() giữa chừng (không chạy hết).
    /// Chỉ ảnh hưởng khi Stop; tween chạy xong tự nhiên luôn trả về bình thường.
    /// </summary>
    public enum TweenStopBehaviour
    {
        /// <summary>await trả về bình thường, giữ nguyên giá trị hiện tại. MẶC ĐỊNH (giá trị 0 — an toàn nhất, không cần try/catch).</summary>
        CancelAwaitSilently,
        /// <summary>Áp giá trị cuối (t=1) ngay rồi await trả về bình thường.</summary>
        CompleteImmediately,
        /// <summary>Ném OperationCanceledException — theo chuẩn CancellationToken .NET.</summary>
        ThrowCancellation
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Runtime/Utilities/Tweening/Config
git commit -m "feat(tween): config enums (LoopMode, TimeMode, TickPhase, StopBehaviour)"
```

**Kiểm chứng:**

| Input | Kỳ vọng |
|---|---|
| Compile 4 enum | Không lỗi; 4 enum public trong `Horcrux.Runtime.Tweening` |
| `default(TimeMode)` | `Scaled` (giá trị 0 — mặc định gameplay hợp lý) |
| `default(LoopMode)` | `Restart` (chu kỳ lặp cơ bản nhất) |
| `default(TweenStopBehaviour)` | `CancelAwaitSilently` (giá trị 0 — an toàn nhất; kể cả khi ai đó dựa `default(enum)` cũng không crash) |

---

### Task 2: TweenCallback — event zero-GC-friendly

**Files:**
- Create: `Runtime/Utilities/Tweening/Core/TweenCallback.cs`

**Interfaces:**
- Consumes: —
- Produces:
  - `struct TweenCallback` — `Set(Action)`, `Set(object,Action<object>)`, `Clear()`, `Invoke()`, `bool HasValue`.
  - `struct TweenUpdateCallback` — `Set(Action<float>)`, `Set(object,Action<object,float>)`, `Clear()`, `Invoke(float t)`, `bool HasValue`.

Giải: hai biến thể ứng với 2 chữ ký sự kiện — không tham số (Start/Complete/Stop/StepComplete) và một `float t` (Update). Mỗi struct giữ **cả** đường `Action` (tiện) lẫn đường `state + static delegate` (zero-GC hot path) — chỉ một trong hai được set. `Invoke` bọc try/catch (MY_SKILL §3.4: 1 listener lỗi không kill vòng tick).

- [ ] **Step 1: Viết TweenCallback.cs**

```csharp
using System;
using UnityEngine;

namespace Horcrux.Runtime.Tweening
{
    /// <summary>
    /// Callback không tham số cho tween (OnStart/OnComplete/OnStop/OnStepComplete).
    /// Hỗ trợ 2 đường: Action tiện dụng, hoặc (state + static delegate) để tránh closure alloc ở hot path.
    /// </summary>
    public struct TweenCallback
    {
        private Action _plain;
        private Action<object> _withState;
        private object _state;

        public bool HasValue => _plain != null || _withState != null;

        public void Set(Action callback)
        {
            _plain = callback; _withState = null; _state = null;
        }

        /// <summary>Zero-GC: truyền state riêng + delegate static (không bắt biến).</summary>
        public void Set(object state, Action<object> callback)
        {
            _withState = callback; _state = state; _plain = null;
        }

        public void Clear()
        {
            _plain = null; _withState = null; _state = null;
        }

        /// <summary>Gọi callback. Nuốt exception để 1 listener lỗi không kill vòng tick runner.</summary>
        public void Invoke()
        {
            try
            {
                _plain?.Invoke();
                if (_withState != null) _withState(_state);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    /// <summary>Callback nhận tiến trình chuẩn hoá t∈[0,1] (OnUpdate).</summary>
    public struct TweenUpdateCallback
    {
        private Action<float> _plain;
        private Action<object, float> _withState;
        private object _state;

        public bool HasValue => _plain != null || _withState != null;

        public void Set(Action<float> callback)
        {
            _plain = callback; _withState = null; _state = null;
        }

        public void Set(object state, Action<object, float> callback)
        {
            _withState = callback; _state = state; _plain = null;
        }

        public void Clear()
        {
            _plain = null; _withState = null; _state = null;
        }

        public void Invoke(float t)
        {
            try
            {
                _plain?.Invoke(t);
                if (_withState != null) _withState(_state, t);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Runtime/Utilities/Tweening/Core/TweenCallback.cs
git commit -m "feat(tween): TweenCallback structs with try/catch guard"
```

**Kiểm chứng:**

| Input | Kỳ vọng |
|---|---|
| `Set(A)` rồi `Invoke()` | A chạy 1 lần |
| `Set(state, B)` rồi `Invoke()` | B(state) chạy; không closure alloc |
| callback ném exception | `Invoke` không rethrow; log qua `Debug.LogException` |
| chưa Set, `HasValue` | `false`; `Invoke()` no-op |
| `Set(A)` rồi `Set(state,B)` | chỉ đường state active (đường plain bị null) |

---

### Task 3: TweenState — struct state một tween (float4 packing)

**Files:**
- Create: `Runtime/Utilities/Tweening/Core/TweenState.cs`

**Interfaces:**
- Consumes: `TweenCallback`, `TweenUpdateCallback` (Task 2); `LoopMode/TimeMode/TickPhase` (Task 1); `EaseType` (có sẵn).
- Produces: `struct TweenState` (public field, dùng bởi runner qua `ref`); static helper `Pack`/`Unpack` cho float4 (xem §0.2).

- [ ] **Step 1: Viết TweenState.cs**

```csharp
using System;
using Unity.Mathematics;
using Horcrux.Runtime.Tweening.Easing;

namespace Horcrux.Runtime.Tweening
{
    /// <summary>
    /// Toàn bộ trạng thái một tween, nằm liền kề trong mảng của runner (§0.5).
    /// Là struct để zero-GC + cache-friendly; runner truy cập qua `ref` (không copy).
    /// Giá trị mọi kiểu đóng gói vào float4 (§0.2).
    /// </summary>
    public struct TweenState
    {
        // --- Lifecycle / identity (§0.4) ---
        public bool inUse;        // slot đang giữ tween sống?
        public int version;       // tăng mỗi lần tái dùng slot — chống stale handle
        public int nextFree;      // liên kết free-list khi !inUse
        public int bucketIndex;   // vị trí slot này trong active-list của phase (để swap-remove O(1))

        // --- Cấu hình nội suy (§0.1) ---
        public int kind;          // id applier trong registry (dùng cho đăng ký / debug)
        public TweenApplier applier; // cache resolved từ kind lúc Create — tick dùng thẳng, BỎ lookup registry mỗi frame (fix CPU #2)
        public object target;     // UnityEngine.Object (Transform/Image/...) — reference, không boxing
        public float4 from;
        public float4 to;
        public bool relative;     // true → 'to' đang giữ offset, cộng from lúc start
        public Func<float4> dynamicSource; // null = đích tĩnh; khác null = đọc lại mỗi frame (§0 đích động)
        public EaseType ease;
        public float duration;
        public float invDuration; // = 1/duration, precompute (§0.1)
        public float delay;       // còn lại trước khi bắt đầu chạy thật
        public float elapsed;     // thời gian đã chạy (sau delay)
        public bool hasStarted;   // đã qua delay & capture from chưa
        public bool isPaused;

        // --- Loop ---
        public LoopMode loopMode;
        public int loopCount;      // TỔNG số chu kỳ sẽ chạy (>=1); bỏ qua khi isInfiniteLoop
        public bool isInfiniteLoop;
        public int completedLoops; // số chu kỳ đã hoàn tất; loop tiếp khi completedLoops < loopCount

        // --- Thời gian / phase ---
        public TimeMode timeMode;
        public TickPhase tickPhase;

        // --- Jump ---
        public float jumpPower;

        // --- Callbacks ---
        public TweenCallback onStart;
        public TweenUpdateCallback onUpdate;
        public TweenCallback onComplete;
        public TweenCallback onStop;
        public TweenCallback onStepComplete;

        /// <summary>Xoá mọi tham chiếu để tránh giữ sống object sau khi tween chết (memory leak).</summary>
        public void ResetReferences()
        {
            target = null;
            applier = null;
            dynamicSource = null;
            onStart.Clear();
            onUpdate.Clear();
            onComplete.Clear();
            onStop.Clear();
            onStepComplete.Clear();
        }
    }

    /// <summary>Đóng gói/giải nén giá trị Unity ↔ float4 theo quy ước §0.2.</summary>
    public static class TweenPack
    {
        public static float4 Float(float v) => new float4(v, 0f, 0f, 0f);
        public static float4 Vec2(UnityEngine.Vector2 v) => new float4(v.x, v.y, 0f, 0f);
        public static float4 Vec3(UnityEngine.Vector3 v) => new float4(v.x, v.y, v.z, 0f);
        public static float4 Col(UnityEngine.Color c) => new float4(c.r, c.g, c.b, c.a);
        public static float4 Quat(UnityEngine.Quaternion q) => new float4(q.x, q.y, q.z, q.w);

        public static float UnpackFloat(in float4 v) => v.x;
        public static UnityEngine.Vector2 UnpackVec2(in float4 v) => new UnityEngine.Vector2(v.x, v.y);
        public static UnityEngine.Vector3 UnpackVec3(in float4 v) => new UnityEngine.Vector3(v.x, v.y, v.z);
        public static UnityEngine.Color UnpackCol(in float4 v) => new UnityEngine.Color(v.x, v.y, v.z, v.w);
        public static UnityEngine.Quaternion UnpackQuat(in float4 v) => new UnityEngine.Quaternion(v.x, v.y, v.z, v.w);
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Runtime/Utilities/Tweening/Core/TweenState.cs
git commit -m "feat(tween): TweenState struct + float4 packing helpers"
```

**Kiểm chứng:**

| Input | Kỳ vọng |
|---|---|
| `TweenPack.Vec3((1,2,3))` → `UnpackVec3` | `(1,2,3)` (round-trip, w bỏ qua) |
| `TweenPack.Col(white)` → `UnpackCol` | `(1,1,1,1)` — 4 lane dùng hết |
| `TweenPack.Quat(identity)` → `UnpackQuat` | `(0,0,0,1)` |
| `sizeof` ước lượng struct | data thuần ~ vài chục byte + refs; là struct → 0 heap alloc khi nằm trong mảng |
| `ResetReferences()` | `target==null`, mọi callback `HasValue==false` |

---

### Task 4: TweenApplier — base + registry (Open/Closed core)

**Files:**
- Create: `Runtime/Utilities/Tweening/Core/TweenApplier.cs`

**Interfaces:**
- Consumes: `TweenState` (Task 3), `float4`/`math` (Unity.Mathematics).
- Produces:
  - `abstract class TweenApplier` — `int Id`, `abstract float4 Capture(object target)`, `abstract void Apply(object target, in float4 value)`, `virtual float4 Interpolate(in TweenState s, float t, float k)` (mặc định `math.lerp(from,to,k)`).
  - `static class TweenAppliers` — giữ singleton mọi applier + `Register`, `Get(int id)`, `Count`. (Các applier cụ thể được thêm ở Task 5–6; task này để danh sách rỗng + cơ chế đăng ký.)

Giải (§0.3): applier là **singleton stateless** — target cụ thể nằm trong `TweenState.target`, nên 1 instance phục vụ mọi tween cùng kiểu → 0 cấp phát per-tween. `Interpolate` nhận cả `t` (thô, cho arc parabol) lẫn `k` (đã ease) để Jump/Rotation override linh hoạt (§0.1, §0.2).

- [ ] **Step 1: Viết TweenApplier.cs**

```csharp
using System.Collections.Generic;
using Unity.Mathematics;

namespace Horcrux.Runtime.Tweening
{
    /// <summary>
    /// Định nghĩa "một kiểu chuyển động" áp vào target: đọc giá trị hiện tại (Capture),
    /// ghi giá trị (Apply), và nội suy (Interpolate). Stateless singleton — target sống
    /// trong TweenState.target, nên một instance phục vụ mọi tween cùng kiểu (§0.3).
    /// Thêm kiểu tween mới = kế thừa class này + đăng ký, KHÔNG sửa runner (Open/Closed).
    /// </summary>
    public abstract class TweenApplier
    {
        /// <summary>Id trong registry, gán lúc Register. TweenState.kind giữ giá trị này.</summary>
        public int Id { get; internal set; }

        /// <summary>Đọc giá trị hiện tại của target để làm 'from' (gọi 1 lần lúc tween bắt đầu chạy).</summary>
        public abstract float4 Capture(object target);

        /// <summary>Ghi giá trị đã nội suy vào target (gọi mỗi frame). Không alloc.</summary>
        public abstract void Apply(object target, in float4 value);

        /// <summary>
        /// Nội suy giá trị frame này. Mặc định lerp tuyến tính (unclamped — giữ overshoot của Back/Elastic, §0.1).
        /// t = tiến trình thô [0,1] (cho arc parabol); k = t đã qua ease.
        /// </summary>
        public virtual float4 Interpolate(in TweenState s, float t, float k) => math.lerp(s.from, s.to, k);
    }

    /// <summary>
    /// Registry singleton của mọi applier. Gán Id theo thứ tự đăng ký. Runner tra <see cref="Get"/>
    /// bằng TweenState.kind. Extension API tham chiếu instance để lấy Id lúc tạo tween.
    /// </summary>
    public static class TweenAppliers
    {
        private static readonly List<TweenApplier> _all = new List<TweenApplier>(32);

        public static int Count => _all.Count;

        /// <summary>Đăng ký 1 applier, gán Id = chỉ số. Gọi 1 lần cho mỗi applier lúc khởi tạo static.</summary>
        public static int Register(TweenApplier applier)
        {
            applier.Id = _all.Count;
            _all.Add(applier);
            return applier.Id;
        }

        public static TweenApplier Get(int id) => _all[id];
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Runtime/Utilities/Tweening/Core/TweenApplier.cs
git commit -m "feat(tween): TweenApplier base + registry (Open/Closed extension point)"
```

**Kiểm chứng:**

| Input | Kỳ vọng |
|---|---|
| `Register(a)` lần đầu | trả `0`; `a.Id==0`; `Count==1` |
| `Register(b)` tiếp | trả `1`; `Get(1)==b` |
| base `Interpolate` với from=0,to=(10,0,0,0),k=0.5 | `(5,0,0,0)` (lerp) |
| base `Interpolate` k=1.2 (OutBack overshoot) | `(12,0,0,0)` — **không kẹp**, giữ overshoot |

---

### Task 5: Transform appliers

**Files:**
- Create: `Runtime/Utilities/Tweening/Appliers/PositionApplier.cs`
- Create: `Runtime/Utilities/Tweening/Appliers/LocalPositionApplier.cs`
- Create: `Runtime/Utilities/Tweening/Appliers/ScaleApplier.cs`
- Create: `Runtime/Utilities/Tweening/Appliers/LocalEulerApplier.cs`
- Create: `Runtime/Utilities/Tweening/Appliers/RotationApplier.cs`

**Interfaces:**
- Consumes: `TweenApplier` (Task 4), `TweenPack` (Task 3), `math`/`float4`/`quaternion`.
- Produces: 5 singleton applier, mỗi cái expose `public static readonly XApplier Instance` với `Id` đã đăng ký. Dùng bởi extension API (Task 10) và runner.

Giải: mỗi applier target là `Transform`. `Capture` đọc thuộc tính hiện tại; `Apply` ghi. `RotationApplier` override `Interpolate` dùng **slerp** (quaternion nội suy đúng phải slerp, không lerp thẳng — lerp 4 thành phần rồi normalize méo tốc độ góc).

- [ ] **Step 1: PositionApplier.cs (world position)**

```csharp
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Tweening
{
    /// <summary>Tween Transform.position (world). Giá trị Vector3 pack vào xyz (§0.2).</summary>
    public sealed class PositionApplier : TweenApplier
    {
        public static readonly PositionApplier Instance = new PositionApplier();
        static PositionApplier() { TweenAppliers.Register(Instance); }
        private PositionApplier() { }

        public override float4 Capture(object target)
            => TweenPack.Vec3(((Transform)target).position);

        public override void Apply(object target, in float4 value)
            => ((Transform)target).position = TweenPack.UnpackVec3(value);
    }
}
```

- [ ] **Step 2: LocalPositionApplier.cs**

```csharp
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Tweening
{
    /// <summary>Tween Transform.localPosition. Vector3 → xyz.</summary>
    public sealed class LocalPositionApplier : TweenApplier
    {
        public static readonly LocalPositionApplier Instance = new LocalPositionApplier();
        static LocalPositionApplier() { TweenAppliers.Register(Instance); }
        private LocalPositionApplier() { }

        public override float4 Capture(object target)
            => TweenPack.Vec3(((Transform)target).localPosition);

        public override void Apply(object target, in float4 value)
            => ((Transform)target).localPosition = TweenPack.UnpackVec3(value);
    }
}
```

- [ ] **Step 3: ScaleApplier.cs**

```csharp
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Tweening
{
    /// <summary>Tween Transform.localScale. Vector3 → xyz.</summary>
    public sealed class ScaleApplier : TweenApplier
    {
        public static readonly ScaleApplier Instance = new ScaleApplier();
        static ScaleApplier() { TweenAppliers.Register(Instance); }
        private ScaleApplier() { }

        public override float4 Capture(object target)
            => TweenPack.Vec3(((Transform)target).localScale);

        public override void Apply(object target, in float4 value)
            => ((Transform)target).localScale = TweenPack.UnpackVec3(value);
    }
}
```

- [ ] **Step 4: LocalEulerApplier.cs (rotate qua euler — cho spin/Incremental >360°)**

```csharp
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Tweening
{
    /// <summary>
    /// Tween Transform.localEulerAngles. Vector3 độ → xyz. Nội suy tuyến tính trên euler,
    /// cho phép giá trị vượt 360° (spin tích lũy Incremental) — khác RotationApplier (slerp, không tích luỹ).
    /// <para>
    /// ⚠️ KHÔNG đi đường ngắn nhất (no shortest-path): Capture đọc localEulerAngles luôn ở [0,360). Nếu vật
    /// đang ở y=350 và tween tới y=10, nội suy tuyến tính đi 350→180→10 (−340°, đường DÀI), không phải +20°.
    /// Applier này DÀNH cho spin tích lũy / giá trị euler tuyệt đối tăng đơn điệu (vd 0→720). Cần xoay theo
    /// cung ngắn nhất → dùng <see cref="RotationApplier"/> (quaternion slerp) qua <c>TweenRotation</c>.
    /// </para>
    /// </summary>
    public sealed class LocalEulerApplier : TweenApplier
    {
        public static readonly LocalEulerApplier Instance = new LocalEulerApplier();
        static LocalEulerApplier() { TweenAppliers.Register(Instance); }
        private LocalEulerApplier() { }

        public override float4 Capture(object target)
            => TweenPack.Vec3(((Transform)target).localEulerAngles);

        public override void Apply(object target, in float4 value)
            => ((Transform)target).localEulerAngles = TweenPack.UnpackVec3(value);
    }
}
```

- [ ] **Step 5: RotationApplier.cs (quaternion — slerp)**

```csharp
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Tweening
{
    /// <summary>
    /// Tween Transform.rotation (world) bằng quaternion slerp. Quaternion → xyzw (§0.2).
    /// Override Interpolate: slerp giữ tốc độ góc đều — lerp 4 thành phần rồi normalize sẽ méo.
    /// </summary>
    public sealed class RotationApplier : TweenApplier
    {
        public static readonly RotationApplier Instance = new RotationApplier();
        static RotationApplier() { TweenAppliers.Register(Instance); }
        private RotationApplier() { }

        public override float4 Capture(object target)
            => TweenPack.Quat(((Transform)target).rotation);

        public override void Apply(object target, in float4 value)
        {
            // Chuẩn hoá phòng sai số tích luỹ trước khi gán.
            quaternion q = math.normalize(new quaternion(value));
            ((Transform)target).rotation = q;
        }

        // k đã ease; slerp theo k (không dùng lerp mặc định của base).
        public override float4 Interpolate(in TweenState s, float t, float k)
        {
            quaternion a = new quaternion(s.from);
            quaternion b = new quaternion(s.to);
            quaternion r = math.slerp(a, b, k);
            return r.value; // quaternion.value là float4
        }
    }
}
```

- [ ] **Step 6: Commit**

```bash
git add Runtime/Utilities/Tweening/Appliers/PositionApplier.cs Runtime/Utilities/Tweening/Appliers/LocalPositionApplier.cs Runtime/Utilities/Tweening/Appliers/ScaleApplier.cs Runtime/Utilities/Tweening/Appliers/LocalEulerApplier.cs Runtime/Utilities/Tweening/Appliers/RotationApplier.cs
git commit -m "feat(tween): Transform appliers (position/localPos/scale/euler/rotation-slerp)"
```

**Kiểm chứng:**

| Input | Kỳ vọng |
|---|---|
| `PositionApplier.Instance.Id` | int ≥0, duy nhất; `TweenAppliers.Get(Id)==Instance` |
| Capture position của tf ở (1,2,3) | `float4(1,2,3,0)` |
| Apply `(4,5,6,0)` vào tf | `tf.position == (4,5,6)` |
| RotationApplier Interpolate from=identity, to=90°Y, k=0.5 | quaternion ≈ 45°Y (slerp) — góc đúng nửa, không méo |
| LocalEuler Apply `(0,720,0,0)` | `localEulerAngles.y` nội suy tới 720 mượt (spin 2 vòng) |

> **Lưu ý static ctor:** applier đăng ký trong `static XApplier()`. Static ctor chỉ chạy khi type được chạm lần đầu. Runner (Task 8) phải "chạm" mọi applier lúc bootstrap để Id ổn định — xem Task 8 "warm appliers".

---

### Task 6: UI/SpriteRenderer appliers + asmdef ref

**Files:**
- Modify: `Runtime/com.horcrux.runtime.asmdef` (thêm `"Unity.ugui"` vào `references`)
- Create: `Runtime/Utilities/Tweening/Appliers/AnchoredPositionApplier.cs`
- Create: `Runtime/Utilities/Tweening/Appliers/CanvasGroupAlphaApplier.cs`
- Create: `Runtime/Utilities/Tweening/Appliers/SpriteColorApplier.cs`
- Create: `Runtime/Utilities/Tweening/Appliers/GraphicColorApplier.cs`
- Create: `Runtime/Utilities/Tweening/Appliers/FillApplier.cs`

**Interfaces:**
- Consumes: `TweenApplier`, `TweenPack`.
- Produces: 5 singleton applier (anchoredPos, canvasGroupAlpha, spriteColor, graphicColor, fill).

> `Image`, `Graphic` nằm trong assembly `Unity.ugui` — không ref sẽ không compile. `SpriteRenderer`, `CanvasGroup`, `RectTransform` nằm trong core UnityEngine (không cần thêm ref) nhưng gom chung task cho gọn.

- [ ] **Step 1: Thêm ref Unity.ugui vào asmdef**

Sửa `Runtime/com.horcrux.runtime.asmdef`, khối `references` thành:

```json
{
    "name": "com.horcrux.runtime",
    "rootNamespace": "Horcrux.Runtime",
    "references": [
        "InitArgs",
        "InitArgs.Services",
        "Unity.Addressables",
        "Unity.ResourceManager",
        "UniTask",
        "UniTask.Addressables",
        "Unity.Mathematics",
        "Unity.ugui"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: AnchoredPositionApplier.cs (RectTransform UI)**

```csharp
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Tweening
{
    /// <summary>Tween RectTransform.anchoredPosition (UI). Vector2 → xy (§0.2).</summary>
    public sealed class AnchoredPositionApplier : TweenApplier
    {
        public static readonly AnchoredPositionApplier Instance = new AnchoredPositionApplier();
        static AnchoredPositionApplier() { TweenAppliers.Register(Instance); }
        private AnchoredPositionApplier() { }

        public override float4 Capture(object target)
            => TweenPack.Vec2(((RectTransform)target).anchoredPosition);

        public override void Apply(object target, in float4 value)
            => ((RectTransform)target).anchoredPosition = TweenPack.UnpackVec2(value);
    }
}
```

- [ ] **Step 3: CanvasGroupAlphaApplier.cs**

```csharp
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Tweening
{
    /// <summary>Tween CanvasGroup.alpha (fade panel/popup). float → x.</summary>
    public sealed class CanvasGroupAlphaApplier : TweenApplier
    {
        public static readonly CanvasGroupAlphaApplier Instance = new CanvasGroupAlphaApplier();
        static CanvasGroupAlphaApplier() { TweenAppliers.Register(Instance); }
        private CanvasGroupAlphaApplier() { }

        public override float4 Capture(object target)
            => TweenPack.Float(((CanvasGroup)target).alpha);

        public override void Apply(object target, in float4 value)
            => ((CanvasGroup)target).alpha = value.x;
    }
}
```

- [ ] **Step 4: SpriteColorApplier.cs**

```csharp
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Tweening
{
    /// <summary>Tween SpriteRenderer.color (đổi màu/fade sprite gameplay). Color → rgba.</summary>
    public sealed class SpriteColorApplier : TweenApplier
    {
        public static readonly SpriteColorApplier Instance = new SpriteColorApplier();
        static SpriteColorApplier() { TweenAppliers.Register(Instance); }
        private SpriteColorApplier() { }

        public override float4 Capture(object target)
            => TweenPack.Col(((SpriteRenderer)target).color);

        public override void Apply(object target, in float4 value)
            => ((SpriteRenderer)target).color = TweenPack.UnpackCol(value);
    }
}
```

- [ ] **Step 5: GraphicColorApplier.cs (Image/RawImage/Text — base Graphic)**

```csharp
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Tweening
{
    /// <summary>Tween Graphic.color (Image/RawImage/Text UI). Color → rgba.</summary>
    public sealed class GraphicColorApplier : TweenApplier
    {
        public static readonly GraphicColorApplier Instance = new GraphicColorApplier();
        static GraphicColorApplier() { TweenAppliers.Register(Instance); }
        private GraphicColorApplier() { }

        public override float4 Capture(object target)
            => TweenPack.Col(((Graphic)target).color);

        public override void Apply(object target, in float4 value)
            => ((Graphic)target).color = TweenPack.UnpackCol(value);
    }
}
```

- [ ] **Step 6: FillApplier.cs (Image.fillAmount — progress bar)**

```csharp
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Tweening
{
    /// <summary>Tween Image.fillAmount (thanh tiến độ/cooldown). float → x, kẹp [0,1] khi ghi.</summary>
    public sealed class FillApplier : TweenApplier
    {
        public static readonly FillApplier Instance = new FillApplier();
        static FillApplier() { TweenAppliers.Register(Instance); }
        private FillApplier() { }

        public override float4 Capture(object target)
            => TweenPack.Float(((Image)target).fillAmount);

        public override void Apply(object target, in float4 value)
            => ((Image)target).fillAmount = math.saturate(value.x); // fillAmount hợp lệ chỉ [0,1]
    }
}
```

- [ ] **Step 7: Commit**

```bash
git add Runtime/com.horcrux.runtime.asmdef Runtime/Utilities/Tweening/Appliers
git commit -m "feat(tween): UI/SpriteRenderer appliers + Unity.ugui asmdef ref"
```

**Kiểm chứng:**

| Input | Kỳ vọng |
|---|---|
| Compile sau khi thêm `Unity.ugui` | Không lỗi "Image not found" |
| AnchoredPos Capture của RT ở (100,50) | `float4(100,50,0,0)` |
| CanvasGroupAlpha Apply `(0.3,...)` | `cg.alpha == 0.3` |
| Fill Apply `(1.5,...)` | `image.fillAmount == 1` (saturate kẹp) |
| GraphicColor Apply đỏ trong suốt | `image.color == (1,0,0,0)` |

---

### Task 7: JumpApplier (đích động) + LambdaApplier (generic)

**Files:**
- Create: `Runtime/Utilities/Tweening/Appliers/JumpApplier.cs`
- Create: `Runtime/Utilities/Tweening/Appliers/LambdaApplier.cs`

**Interfaces:**
- Consumes: `TweenApplier`, `TweenState`, `TweenPack`, `math`.
- Produces: `JumpApplier.Instance` (world position + arc parabol; hỗ trợ đích động qua `TweenState.dynamicSource`); `LambdaApplier.Instance` + `LambdaApplier.FloatChannel` (giữ getter/setter cho `Tween.To` — cửa case hiếm, chấp nhận closure).

**§ Toán jump parabol (dẫn giải):** di chuyển từ `from` → `to` theo đường thẳng, cộng thêm một "vòng cung" độ cao. Độ cao cung tại tiến trình `t`:

$$\text{arc}(t) = \text{power} \times 4t(1-t)$$

**Kiểm mốc:** `t=0 → 0`, `t=1 → 0` (chạm đất hai đầu), `t=0.5 → power` (đỉnh giữa). Hệ số `4` chuẩn hoá để đỉnh đúng bằng `power`. Đây là "khuôn" jump mà cả 4 dự án tự viết lặp lại — nay gói một chỗ.

Vị trí cuối: nội suy tuyến tính từ→đến theo `k` (đã ease) + Y cộng arc theo `t` (thô, để cung đối xứng bất kể ease).

- [ ] **Step 1: JumpApplier.cs**

```csharp
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Tweening
{
    /// <summary>
    /// Nhảy parabol tới đích (world position). Đường thẳng from→to nội suy theo k (ease),
    /// cộng vòng cung độ cao arc(t)=power·4t(1-t) theo t thô (§ Toán jump). Đỉnh cung = power tại t=0.5.
    /// Hỗ trợ đích động: nếu TweenState.dynamicSource != null, 'to' đã được runner cập nhật mỗi frame
    /// (điểm phân biệt vs DOTween — bám target di chuyển).
    /// </summary>
    public sealed class JumpApplier : TweenApplier
    {
        public static readonly JumpApplier Instance = new JumpApplier();
        static JumpApplier() { TweenAppliers.Register(Instance); }
        private JumpApplier() { }

        public override float4 Capture(object target)
            => TweenPack.Vec3(((Transform)target).position);

        public override void Apply(object target, in float4 value)
            => ((Transform)target).position = TweenPack.UnpackVec3(value);

        public override float4 Interpolate(in TweenState s, float t, float k)
        {
            float4 flat = math.lerp(s.from, s.to, k); // đường nền đi theo ease
            float arc = s.jumpPower * 4f * t * (1f - t); // cung đối xứng theo t thô
            flat.y += arc;
            return flat;
        }
    }
}
```

- [ ] **Step 2: LambdaApplier.cs (cửa generic — case hiếm shader/audio/scalar)**

```csharp
using System;
using Unity.Mathematics;

namespace Horcrux.Runtime.Tweening
{
    /// <summary>
    /// Cửa tween giá trị tuỳ ý qua getter/setter (shader property, audio volume, scalar bất kỳ).
    /// KHÁC các applier khác: target là một FloatChannel (đóng gói closure) → chấp nhận 1 alloc
    /// lúc tạo tween. Chỉ dùng cho case hiếm không có applier chuyên biệt (§ setter phương án C).
    /// Chỉ hỗ trợ float ở v1 (pack vào x).
    /// </summary>
    public sealed class LambdaApplier : TweenApplier
    {
        public static readonly LambdaApplier Instance = new LambdaApplier();
        static LambdaApplier() { TweenAppliers.Register(Instance); }
        private LambdaApplier() { }

        /// <summary>Box chứa getter/setter, đặt vào TweenState.target.</summary>
        public sealed class FloatChannel
        {
            public Func<float> getter;
            public Action<float> setter;
        }

        public override float4 Capture(object target)
            => TweenPack.Float(((FloatChannel)target).getter());

        public override void Apply(object target, in float4 value)
            => ((FloatChannel)target).setter(value.x);
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add Runtime/Utilities/Tweening/Appliers/JumpApplier.cs Runtime/Utilities/Tweening/Appliers/LambdaApplier.cs
git commit -m "feat(tween): JumpApplier (parabola + dynamic target) + LambdaApplier (generic gate)"
```

**Kiểm chứng:**

| Input | Kỳ vọng |
|---|---|
| arc tại t=0, t=1 | `0` (chạm đất hai đầu) |
| arc tại t=0.5, power=2 | `2` (đỉnh = power) |
| Jump Interpolate from=(0,0,0), to=(10,0,0), power=2, t=k=0.5 | `(5, 2, 0)` — giữa đường, đỉnh cung |
| Jump từ (0,0,0) tới (10,0,0), t=k=1 | `(10,0,0)` — tới đích, arc=0 |
| LambdaApplier Capture với getter ()=>3.5 | `float4(3.5,0,0,0)` |
| LambdaApplier Apply (7,..) | setter nhận 7 |

---

### Task 8: ITweenRunner + TweenHandle

**Files:**
- Create: `Runtime/Utilities/Tweening/Core/ITweenRunner.cs`
- Create: `Runtime/Utilities/Tweening/Core/TweenHandle.cs`

**Interfaces:**
- Consumes: `TweenState` (Task 3), `TweenStopBehaviour`/`LoopMode`/`TimeMode`/`TickPhase` (Task 1), `EaseType` (có sẵn), UniTask.
- Produces:
  - `interface ITweenRunner` — hợp đồng runner mà handle & API gọi (DI qua `[Service]`).
    - `TweenHandle Create(int kind, object target, float4 to, float duration)` — tạo tween, trả handle.
    - `ref TweenState GetState(int slot)` — runner nội bộ; handle dùng để mutate config.
    - `bool IsAlive(int slot, int version)`; `void Stop(int slot, int version, bool complete)`; `void Pause/Resume(int slot, int version)`.
    - `void MoveBucket(int slot, int version, TickPhase newPhase)` — dời bucket tick (cho `SetTickPhase`, giữ DIP).
    - `int StopAllOnTarget(object target)` — stop-per-target (linear scan v1).
    - `UniTask AwaitTween(int slot, int version, TweenStopBehaviour behaviour, CancellationToken ct)`.
    - `static ITweenRunner Current { get; }` — accessor bootstrap (gán bởi TweenRunner Task 9).
  - `readonly struct TweenHandle` — fluent config (`SetEase/SetDelay/SetLoops/SetRelative/SetTimeMode/SetTickPhase/SetJumpPower/SetDynamicTarget`) + events (mỗi sự kiện có **2 overload**: `Action` tiện dụng và `(object state, Action<object>)`/`(object state, Action<object,float>)` zero-GC hot path — `OnStart/OnUpdate/OnComplete/OnStop/OnStepComplete`) + control (`Stop/Complete/Pause/Resume`, `IsActive`) + await (`GetAwaiter`, `ToUniTask`).

Giải (§0.4): handle chỉ giữ `{slot, version}` + tham chiếu runner (qua `ITweenRunner.Current`). Config methods lấy `ref TweenState` rồi mutate — chỉ hợp lệ khi `hasStarted==false` (trước frame đầu). Mọi thao tác đều check version → an toàn stale handle.

- [ ] **Step 1: ITweenRunner.cs**

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;

namespace Horcrux.Runtime.Tweening
{
    /// <summary>
    /// Hợp đồng của cỗ máy tween trung tâm. Handle & extension API phụ thuộc abstraction này
    /// (SOLID-D), lấy instance qua <see cref="Current"/> (gán khi TweenRunner bootstrap).
    /// </summary>
    public interface ITweenRunner
    {
        /// <summary>Tạo tween mới ở trạng thái chưa chạy (hasStarted=false). Trả handle version-safe.</summary>
        TweenHandle Create(int kind, object target, float4 to, float duration);

        /// <summary>Truy cập state theo slot để mutate config trước khi chạy. Chỉ gọi khi version còn khớp.</summary>
        ref TweenState GetState(int slot);

        bool IsAlive(int slot, int version);

        /// <summary>Dừng tween. complete=true → nhảy tới giá trị cuối (t=1) rồi bắn OnComplete; false → dừng tại chỗ, bắn OnStop.</summary>
        void Stop(int slot, int version, bool complete);

        void Pause(int slot, int version);
        void Resume(int slot, int version);

        /// <summary>Dời tween sang bucket tick khác (gọi bởi TweenHandle.SetTickPhase — giữ handle độc lập concrete runner, DIP).</summary>
        void MoveBucket(int slot, int version, TickPhase newPhase);

        /// <summary>Dừng mọi tween đang chạy trên target (OnDisable/OnDestroy). Trả số tween đã dừng.</summary>
        int StopAllOnTarget(object target);

        UniTask AwaitTween(int slot, int version, TweenStopBehaviour behaviour, CancellationToken ct);

        /// <summary>Instance hiện hành, gán bởi TweenRunner lúc bootstrap. Null nếu chưa khởi tạo.</summary>
        static ITweenRunner Current { get; internal set; }
    }
}
```

> **Yêu cầu C# version:** `static` interface member + `internal set` cần **C# 11 (Unity 2022.2+/Roslyn)** — đã chốt dùng trực tiếp. `TweenRunner.Bootstrap` gán `ITweenRunner.Current = runner`; handle/extension đọc qua `ITweenRunner.Current` (zero-lookup, không qua Service resolve mỗi lần).

- [ ] **Step 2: TweenHandle.cs**

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using Horcrux.Runtime.Tweening.Easing;

namespace Horcrux.Runtime.Tweening
{
    /// <summary>
    /// Tay cầm nhẹ (8 byte data) tới một tween. Copy thoải mái, không GC. Mọi thao tác kiểm version
    /// nên handle cũ trỏ vào slot đã tái dùng sẽ bị bỏ qua an toàn (§0.4). Đây là API DUY NHẤT
    /// developer chạm để config/await/stop một tween.
    /// </summary>
    public readonly struct TweenHandle
    {
        private readonly int _slot;
        private readonly int _version;

        public TweenHandle(int slot, int version)
        {
            _slot = slot;
            _version = version;
        }

        private static ITweenRunner Runner => ITweenRunner.Current;

        /// <summary>Tween còn sống và handle còn đúng slot?</summary>
        public bool IsActive => Runner != null && Runner.IsAlive(_slot, _version);

        // ---------- Config (chỉ hiệu lực trước frame đầu; trả this để chain) ----------

        public TweenHandle SetEase(EaseType ease)
        {
            if (IsActive) Runner.GetState(_slot).ease = ease;
            return this;
        }

        public TweenHandle SetDelay(float seconds)
        {
            if (IsActive) Runner.GetState(_slot).delay = seconds;
            return this;
        }

        /// <summary>Đặt tổng số chu kỳ. <paramref name="loops"/>=2 → chạy đúng 2 lần; &lt;0 → vô hạn; 1 → như không loop.</summary>
        public TweenHandle SetLoops(int loops, LoopMode mode = LoopMode.Restart)
        {
            if (IsActive)
            {
                ref TweenState s = ref Runner.GetState(_slot);
                s.loopMode = mode;
                s.isInfiniteLoop = loops < 0;
                s.loopCount = loops < 1 ? 1 : loops; // tổng số chu kỳ (>=1); vô hạn xử lý riêng qua isInfiniteLoop
            }
            return this;
        }

        /// <summary>'to' được hiểu là offset cộng vào giá trị hiện tại lúc bắt đầu (nhấp nhô/wobble).</summary>
        public TweenHandle SetRelative(bool relative = true)
        {
            if (IsActive) Runner.GetState(_slot).relative = relative;
            return this;
        }

        public TweenHandle SetTimeMode(TimeMode mode)
        {
            if (IsActive) Runner.GetState(_slot).timeMode = mode;
            return this;
        }

        public TweenHandle SetTickPhase(TickPhase phase)
        {
            // Qua interface (MoveBucket) — KHÔNG cast concrete TweenRunner (giữ DIP). Dời bucket + set tickPhase nội bộ.
            if (IsActive) Runner.MoveBucket(_slot, _version, phase);
            return this;
        }

        public TweenHandle SetJumpPower(float power)
        {
            if (IsActive) Runner.GetState(_slot).jumpPower = power;
            return this;
        }

        /// <summary>Đích động: runner đọc lại getter mỗi frame để bám target di chuyển (§ đích động).</summary>
        public TweenHandle SetDynamicTarget(Func<float4> targetGetter)
        {
            if (IsActive) Runner.GetState(_slot).dynamicSource = targetGetter;
            return this;
        }

        // ---------- Events ----------

        public TweenHandle OnStart(Action cb)
        { if (IsActive) Runner.GetState(_slot).onStart.Set(cb); return this; }

        public TweenHandle OnStart(object state, Action<object> cb)
        { if (IsActive) Runner.GetState(_slot).onStart.Set(state, cb); return this; }

        public TweenHandle OnUpdate(Action<float> cb)
        { if (IsActive) Runner.GetState(_slot).onUpdate.Set(cb); return this; }

        /// <summary>Zero-GC hot path: state riêng + static delegate (không bắt biến). t truyền vào tham số 2.</summary>
        public TweenHandle OnUpdate(object state, Action<object, float> cb)
        { if (IsActive) Runner.GetState(_slot).onUpdate.Set(state, cb); return this; }

        public TweenHandle OnComplete(Action cb)
        { if (IsActive) Runner.GetState(_slot).onComplete.Set(cb); return this; }

        public TweenHandle OnComplete(object state, Action<object> cb)
        { if (IsActive) Runner.GetState(_slot).onComplete.Set(state, cb); return this; }

        public TweenHandle OnStop(Action cb)
        { if (IsActive) Runner.GetState(_slot).onStop.Set(cb); return this; }

        /// <summary>Zero-GC hot path overload (đối xứng OnComplete để khôi phục trạng thái khi bị kill).</summary>
        public TweenHandle OnStop(object state, Action<object> cb)
        { if (IsActive) Runner.GetState(_slot).onStop.Set(state, cb); return this; }

        public TweenHandle OnStepComplete(Action cb)
        { if (IsActive) Runner.GetState(_slot).onStepComplete.Set(cb); return this; }

        public TweenHandle OnStepComplete(object state, Action<object> cb)
        { if (IsActive) Runner.GetState(_slot).onStepComplete.Set(state, cb); return this; }

        // ---------- Control ----------

        /// <summary>Dừng tween tại chỗ, bắn OnStop. No-op nếu handle đã hết hiệu lực.</summary>
        public void Stop() { if (Runner != null) Runner.Stop(_slot, _version, complete: false); }

        /// <summary>Nhảy tới giá trị cuối rồi bắn OnComplete.</summary>
        public void Complete() { if (Runner != null) Runner.Stop(_slot, _version, complete: true); }

        public void Pause() { if (Runner != null) Runner.Pause(_slot, _version); }
        public void Resume() { if (Runner != null) Runner.Resume(_slot, _version); }

        // ---------- Await ----------

        /// <summary>await mặc định: im lặng khi bị Stop (không exception).</summary>
        public UniTask.Awaiter GetAwaiter()
            => ToUniTask(TweenStopBehaviour.CancelAwaitSilently, default).GetAwaiter();

        public UniTask ToUniTask(
            TweenStopBehaviour behaviour = TweenStopBehaviour.CancelAwaitSilently,
            CancellationToken ct = default)
        {
            if (Runner == null || !Runner.IsAlive(_slot, _version))
                return UniTask.CompletedTask; // đã xong/không hợp lệ → await trả ngay
            return Runner.AwaitTween(_slot, _version, behaviour, ct);
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add Runtime/Utilities/Tweening/Core/ITweenRunner.cs Runtime/Utilities/Tweening/Core/TweenHandle.cs
git commit -m "feat(tween): ITweenRunner abstraction + TweenHandle (fluent config/control/await)"
```

**Kiểm chứng:**

| Input | Kỳ vọng |
|---|---|
| `default(TweenHandle).IsActive` | `false` — default = `{slot0, version0}`; slot mới bắt đầu `version=1` (Rent) nên không tween nào mang version 0 → `IsAlive(0,0)` false. Đây là lý do version phải khởi từ 1 (§0.4). |
| `SetEase` trên handle đã hết hạn | no-op, không throw (guard `IsActive`) |
| chain `SetEase().SetLoops().OnComplete()` | trả cùng handle, mỗi call mutate state 1 field |
| `GetAwaiter` khi tween đã xong | await trả ngay (CompletedTask) |
| `Stop()` với version lệch | runner bỏ qua (Task 9 đảm bảo) |

---

### Task 9: TweenRunner — engine (pool + bucket + tick + await), Service, bootstrap

**Files:**
- Create: `Runtime/Utilities/Tweening/Core/TweenRunner.cs`

**Interfaces:**
- Consumes: mọi thứ trên — `TweenState`, `TweenAppliers`, `ITweenRunner`, `TweenHandle`, config enums, `Easer`, UniTask, Unity.Mathematics.
- Produces: `class TweenRunner : MonoBehaviour, ITweenRunner`; auto-bootstrap qua `RuntimeInitializeOnLoadMethod` + tự đăng ký `Service.Set<ITweenRunner>(this)` (KHÔNG dùng attribute `[Service]` — sẽ double-create, xem remarks trong code).

Giải (§0.5): runner giữ mảng `TweenState[]` grow-only + free-list (slot chết tái dùng). 3 bucket active-list (Update/Late/Fixed) chứa chỉ số slot đang chạy; mỗi frame **snapshot** bucket vào buffer tái dùng rồi tick (an toàn với mutation reentrant từ callback). Await: một `Dictionary<long, AutoResetUniTaskCompletionSource>` khoá theo `(slot,version)` gộp thành `long` — chỉ tạo source khi có ai await (đa số tween không await → 0 alloc await).

**An toàn reentrancy (trụ cột đúng-đắn):** callback do developer viết (OnComplete tạo tween kế, OnStop dọn dẹp, dynamicTarget đọc Transform khác...) có thể gọi lại runner ngay giữa tick. Ba biện pháp: (1) tick trên **snapshot** bucket, kiểm `IsAlive` mỗi slot; (2) trong `StepTween`/`Finish` **không giữ `ref TweenState` xuyên callback** — truy cập qua `_states[slot]` (index) và re-kiểm `IsAlive` sau mỗi Invoke; (3) `Finish` **gỡ bucket + trả slot TRƯỚC**, bắn callback SAU → chống double-finish (mọi Stop/Complete tái nhập thành no-op). Đây là điều kiện tiên quyết để engine không hỏng free-list / dangling ref khi dùng thật.

- [ ] **Step 1: Khung runner — fields, bootstrap, Service, pool**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sisus.Init;
using Unity.Mathematics;
using UnityEngine;
using Horcrux.Runtime.Tweening.Easing;

namespace Horcrux.Runtime.Tweening
{
    /// <summary>
    /// Cỗ máy tween trung tâm: giữ mọi TweenState trong mảng grow-only, tick theo phase mỗi frame,
    /// quản lý pool (free-list), stop-per-target, và bridge await (§0.5). Auto-bootstrap — không cần
    /// kéo vào scene. Tự đăng ký qua Service.Set để hệ khác inject ITweenRunner (SOLID-D).
    /// </summary>
    /// <remarks>
    /// KHÔNG dùng attribute [Service] ở đây: attribute đó khiến Sisus.Init TỰ khởi tạo type (với MonoBehaviour
    /// là tự AddComponent lên GameObject riêng), sẽ XUNG ĐỘT với auto-bootstrap bên dưới → double-create.
    /// Vì runner tự tạo instance, ta đăng ký thủ công bằng Service.Set&lt;ITweenRunner&gt;(this) — consumer vẫn
    /// resolve/inject bình thường qua Service&lt;ITweenRunner&gt;.Instance (SOLID-D). (§0.3 driver)
    /// </remarks>
    public sealed class TweenRunner : MonoBehaviour, ITweenRunner
    {
        private const int InitialCapacity = 64;

        private TweenState[] _states = new TweenState[InitialCapacity];
        private int _count;              // số slot đã cấp phát (đỉnh cao nhất dùng tới)
        private int _freeHead = -1;      // đầu free-list; -1 = rỗng

        // Active-list mỗi phase: chứa slot index đang chạy. Swap-remove O(1) qua bucketIndex.
        private readonly List<int> _updateBucket = new List<int>(InitialCapacity);
        private readonly List<int> _lateBucket = new List<int>(InitialCapacity);
        private readonly List<int> _fixedBucket = new List<int>(InitialCapacity);

        // Await sources, chỉ tạo khi có người await. Key = pack(slot,version).
        // Giá trị giữ CẢ source LẪN behaviour: Finish cần biết awaiter muốn gì khi tween bị Stop
        // (im lặng / ném OCE) — quyết định theo LÝ DO kết thúc (stopped vs completed), KHÔNG theo ct (fix A).
        private struct AwaitEntry
        {
            public AutoResetUniTaskCompletionSource source;
            public TweenStopBehaviour behaviour;
        }
        private readonly Dictionary<long, AwaitEntry> _awaits = new Dictionary<long, AwaitEntry>(16);

        public static ITweenRunner Current => ITweenRunner.Current;

        // ---- Bootstrap: tạo GameObject ẩn, DontDestroyOnLoad, gán Current + Service, warm appliers ----
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (ITweenRunner.Current != null) return;

            var go = new GameObject("[TweenRunner]");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            var runner = go.AddComponent<TweenRunner>();

            ITweenRunner.Current = runner;                 // accessor nhanh cho handle/extension (zero-lookup)
            Service.Set<ITweenRunner>(runner);             // đăng ký DI để consumer inject (Sisus.Init)

            WarmAppliers(); // chạm mọi applier để static ctor đăng ký, Id ổn định (Task 5–7)
        }

        private static void WarmAppliers()
        {
            _ = PositionApplier.Instance;      _ = LocalPositionApplier.Instance;
            _ = ScaleApplier.Instance;         _ = LocalEulerApplier.Instance;
            _ = RotationApplier.Instance;      _ = AnchoredPositionApplier.Instance;
            _ = CanvasGroupAlphaApplier.Instance; _ = SpriteColorApplier.Instance;
            _ = GraphicColorApplier.Instance;  _ = FillApplier.Instance;
            _ = JumpApplier.Instance;          _ = LambdaApplier.Instance;
        }

        private static long Key(int slot, int version) => ((long)version << 32) | (uint)slot;

        // ---- Pool: cấp/ trả slot ----
        private int Rent()
        {
            if (_freeHead >= 0)
            {
                int slot = _freeHead;
                _freeHead = _states[slot].nextFree; // slot tái dùng: version đã >=1 (Return đã ++)
                return slot;
            }
            if (_count == _states.Length)
                Array.Resize(ref _states, _states.Length * 2); // grow-only (mảng mới zero-fill → version=0)
            int newSlot = _count++;
            // Slot MỚI phải bắt đầu version=1: giữ version 0 làm giá trị "vô hiệu" dành riêng cho
            // default(TweenHandle){0,0}. Nếu để version=0, tween đầu tiên ở slot 0 sẽ khớp default handle
            // → default(handle).IsActive==true, một handle rỗng vô tình Stop/mutate tween thật (§0.4).
            _states[newSlot].version = 1;
            return newSlot;
        }

        private void Return(int slot)
        {
            _states[slot].inUse = false;
            _states[slot].version++;          // vô hiệu hoá mọi handle cũ (§0.4)
            _states[slot].ResetReferences();  // tránh giữ sống target/callback
            _states[slot].nextFree = _freeHead;
            _freeHead = slot;
        }

        public ref TweenState GetState(int slot) => ref _states[slot];

        public bool IsAlive(int slot, int version)
            => slot >= 0 && slot < _states.Length && _states[slot].inUse && _states[slot].version == version;
    }
}
```

- [ ] **Step 2: Create() — khởi tạo tween**

Thêm vào trong class `TweenRunner`:

```csharp
        public TweenHandle Create(int kind, object target, float4 to, float duration)
        {
            int slot = Rent();
            ref TweenState s = ref _states[slot];

            s.inUse = true;
            s.kind = kind;
            s.applier = TweenAppliers.Get(kind); // resolve 1 lần lúc tạo — tick không lookup nữa (fix CPU #2)
            s.target = target;
            s.to = to;
            s.duration = duration <= 0f ? 0.0001f : duration; // tránh chia 0
            s.invDuration = 1f / s.duration;

            // reset cấu hình về mặc định (slot có thể vừa tái dùng)
            s.from = default;
            s.relative = false;
            s.dynamicSource = null;
            s.ease = EaseType.Linear;
            s.delay = 0f;
            s.elapsed = 0f;
            s.hasStarted = false;
            s.isPaused = false;
            s.loopMode = LoopMode.Restart;
            s.loopCount = 1;              // mặc định chạy đúng 1 chu kỳ
            s.isInfiniteLoop = false;
            s.completedLoops = 0;
            s.timeMode = TimeMode.Scaled;
            s.tickPhase = TickPhase.Update;
            s.jumpPower = 0f;

            AddToBucket(slot, TickPhase.Update); // mặc định; SetTickPhase sẽ dời nếu cần

            return new TweenHandle(slot, s.version);
        }

        private List<int> BucketOf(TickPhase phase) => phase switch
        {
            TickPhase.LateUpdate => _lateBucket,
            TickPhase.FixedUpdate => _fixedBucket,
            _ => _updateBucket
        };

        private void AddToBucket(int slot, TickPhase phase)
        {
            List<int> bucket = BucketOf(phase);
            _states[slot].bucketIndex = bucket.Count;
            bucket.Add(slot);
        }

        private void RemoveFromBucket(int slot)
        {
            List<int> bucket = BucketOf(_states[slot].tickPhase);
            int idx = _states[slot].bucketIndex;
            int last = bucket.Count - 1;
            bucket[idx] = bucket[last];           // swap-remove O(1)
            _states[bucket[idx]].bucketIndex = idx;
            bucket.RemoveAt(last);
        }
```

`MoveBucket` là method public của `ITweenRunner` (fix DIP #3 — handle không cast concrete). `Create` luôn add vào Update bucket; `SetTickPhase` dời sang bucket đích qua đây:

```csharp
        public void MoveBucket(int slot, int version, TickPhase newPhase)
        {
            if (!IsAlive(slot, version)) return;
            if (_states[slot].tickPhase == newPhase) return;
            RemoveFromBucket(slot);
            _states[slot].tickPhase = newPhase;
            AddToBucket(slot, newPhase);
        }
```

- [ ] **Step 3: Tick core — hàm StepTween + 3 Unity callback**

Thêm vào class:

```csharp
        private void Update()
        {
            TickBucket(_updateBucket, TickPhase.Update, Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
            TickBucket(_lateBucket, TickPhase.LateUpdate, Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void FixedUpdate()
        {
            TickBucket(_fixedBucket, TickPhase.FixedUpdate, Time.fixedDeltaTime, Time.fixedUnscaledDeltaTime);
        }

        // Buffer snapshot tái dùng (grow-only, zero steady-state GC) — xem giải thích dưới.
        private readonly List<long> _tickBuffer = new List<long>(InitialCapacity);

        /// <summary>
        /// Tick mọi tween trong bucket AN TOÀN với mutation từ callback.
        /// Vì callback (onComplete/onUpdate...) có thể tạo/停 BẤT KỲ tween nào trong CÙNG bucket, duyệt trực
        /// tiếp trên bucket (kể cả duyệt ngược + swap-remove) sẽ double-tick/skip khi callback đụng tween khác.
        /// Giải: SNAPSHOT (slot,version) hiện tại vào _tickBuffer, rồi duyệt buffer; mỗi slot KIỂM IsAlive(slot,version)
        /// trước khi tick → tween đã bị stop giữa frame (version đổi) tự động bị bỏ qua; tween mới tạo trong frame
        /// (không có trong snapshot) chỉ chạy từ frame sau. Đúng đắn tuyệt đối, zero-GC (buffer tái dùng).
        /// <para>
        /// GUARD PHASE (fix C): callback có thể gọi SetTickPhase → MoveBucket dời một tween SANG bucket khác
        /// GIỮA frame. Nếu dời Update→LateUpdate, tween đó vẫn nằm trong snapshot của Update (version chưa đổi →
        /// IsAlive vẫn true) VÀ sẽ có trong _lateBucket khi LateUpdate chạy CÙNG frame → tick 2 lần. Chặn bằng
        /// cách bỏ qua entry mà tickPhase hiện tại KHÁC phase của bucket đang duyệt — tween đã dời chỉ tick ở
        /// bucket đích, đúng 1 lần.
        /// </para>
        /// </summary>
        private void TickBucket(List<int> bucket, TickPhase phase, float scaledDt, float unscaledDt)
        {
            _tickBuffer.Clear();
            for (int i = 0; i < bucket.Count; i++)
            {
                int slot = bucket[i];
                _tickBuffer.Add(Key(slot, _states[slot].version)); // đóng gói (slot,version) — chống slot tái dùng trong frame
            }

            for (int i = 0; i < _tickBuffer.Count; i++)
            {
                long key = _tickBuffer[i];
                int slot = (int)(uint)key;        // giải nén: 32 bit thấp = slot
                int version = (int)(key >> 32);   // 32 bit cao = version
                if (!IsAlive(slot, version)) continue;        // đã bị stop/finish giữa frame → bỏ qua an toàn
                if (_states[slot].tickPhase != phase) continue; // đã bị MoveBucket dời sang phase khác giữa frame (fix C)

                // FIX F: bọc tick 1 slot. Nếu target bị Destroy mà không gọi StopTweens(), applier.Apply/Capture
                // ném MissingReferenceException MỖI frame → tween kẹt trong bucket, KHÔNG bao giờ tới Finish →
                // await treo vĩnh viễn + source rò rỉ. Lưu ý: target kiểu `object` nên `== null` KHÔNG dùng overload
                // fake-null của Unity → không thể tự phát hiện destroyed; try/catch là cách bắt chắc chắn. Lỗi →
                // Finish(stopped, fault:e): gỡ bucket + trả slot + đánh thức awaiter bằng CHÍNH exception (fix vòng 6,
                // Finding 2) — crash KHÔNG bị nuốt thành OCE/silent-success; onStop vẫn bắn để dọn dẹp.
                try
                {
                    StepTween(slot, version, scaledDt, unscaledDt);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    if (IsAlive(slot, version)) Finish(slot, stopped: true, fault: e);
                }
            }
        }

        // QUY TẮC REENTRANCY (đọc kỹ — nền tảng đúng đắn của cả engine):
        // Callback (onStart/onUpdate/onComplete/...) có thể GỌI LẠI runner: tạo tween mới (→ Array.Resize
        // đổi mảng _states), Stop() chính tween này, hoặc StopAllOnTarget. Vì vậy:
        //   1. KHÔNG giữ `ref TweenState` xuyên qua bất kỳ callback nào (ref sẽ trỏ mảng cũ sau Resize).
        //      Sau mỗi Invoke, nếu còn dùng state phải RE-FETCH và KIỂM version còn khớp.
        //   2. Callback được bắn ở CUỐI, sau khi state đã nhất quán (elapsed/loop/hasStarted set xong).
        //   3. Kết thúc (complete/stop) đi qua Finish() DUY NHẤT, có cờ chống double-finish.
        // version truyền từ TickBucket (đã kiểm IsAlive) — dùng để phát hiện tween bị stop bởi callback reentrant.
        private void StepTween(int slot, int version, float scaledDt, float unscaledDt)
        {
            if (_states[slot].isPaused) return;

            float dt = _states[slot].timeMode == TimeMode.Unscaled ? unscaledDt : scaledDt;
            TweenApplier applier = _states[slot].applier; // cached lúc Create (fix CPU #2)

            // --- Pha delay + capture from lúc bắt đầu chạy thật (§0.1) ---
            if (!_states[slot].hasStarted)
            {
                if (_states[slot].delay > 0f)
                {
                    _states[slot].delay -= dt;
                    if (_states[slot].delay > 0f) return;
                    dt = -_states[slot].delay; // phần dt tràn qua sau khi hết delay
                    _states[slot].delay = 0f;
                }
                _states[slot].from = applier.Capture(_states[slot].target);
                if (_states[slot].relative)
                    _states[slot].to = _states[slot].from + _states[slot].to; // 'to' đang giữ offset
                _states[slot].hasStarted = true;

                // onStart có thể reentrant → bắn CUỐI pha khởi động, rồi kiểm sống trước khi đi tiếp.
                if (_states[slot].onStart.HasValue)
                {
                    _states[slot].onStart.Invoke();
                    if (!IsAlive(slot, version)) return; // callback đã Stop/Complete tween này
                }
            }

            // --- Đích động: đọc lại mỗi frame (§ đích động). dynamicSource là delegate → có thể reentrant. ---
            if (_states[slot].dynamicSource != null)
            {
                float4 dyn = _states[slot].dynamicSource();
                if (!IsAlive(slot, version)) return;
                _states[slot].to = dyn;
            }

            // --- Tiến trình (thuần tính toán, KHÔNG callback → an toàn giữ ref cục bộ ngắn) ---
            _states[slot].elapsed += dt;
            float t = _states[slot].elapsed * _states[slot].invDuration;
            if (t > 1f) t = 1f;

            float k = Easer.Evaluate(_states[slot].ease, t);
            float4 v = applier.Interpolate(in _states[slot], t, k);
            applier.Apply(_states[slot].target, v); // Apply ghi Transform/UI — không reentrant vào runner

            // onUpdate reentrant → bắn sau Apply, kiểm sống trước khi xử lý kết thúc.
            if (_states[slot].onUpdate.HasValue)
            {
                _states[slot].onUpdate.Invoke(t);
                if (!IsAlive(slot, version)) return;
            }

            if (t >= 1f)
                OnCycleEnd(slot, version);
        }
```

> **Vì sao dùng `_states[slot]` lặp lại thay vì `ref s`:** truy cập qua index luôn đọc mảng _hiện tại_ của field `_states` (đã trỏ mảng mới nếu Resize xảy ra giữa chừng). Giữ `ref s` thì ref "đóng băng" vào mảng tại thời điểm lấy — sau Resize là dangling. JIT vẫn tối ưu tốt chuỗi `_states[slot]` (bounds-check hoisting) và các đoạn KHÔNG có callback ở giữa không mất mát đáng kể. Đây là đánh đổi rõ ràng: nhường một chút micro-perf lấy đúng-đắn tuyệt đối — bắt buộc cho pool + callback.

- [ ] **Step 4: Loop + kết thúc + stop + await resolve**

Thêm vào class:

```csharp
        /// <summary>Xử lý khi một chu kỳ chạm t=1: loop tiếp hoặc hoàn tất. version = version lúc StepTween bắt đầu.</summary>
        private void OnCycleEnd(int slot, int version)
        {
            _states[slot].completedLoops++;

            // onStepComplete reentrant → bắn trước, kiểm sống, rồi mới quyết loop/finish.
            if (_states[slot].onStepComplete.HasValue)
            {
                _states[slot].onStepComplete.Invoke();
                if (!IsAlive(slot, version)) return;
            }

            // Còn chu kỳ? (vô hạn, hoặc chưa đạt tổng loopCount). Fix off-by-one: so completedLoops < loopCount.
            bool hasMoreLoops = _states[slot].isInfiniteLoop || _states[slot].completedLoops < _states[slot].loopCount;
            if (hasMoreLoops)
            {
                // FIX D: carry phần dư (elapsed - duration) sang chu kỳ sau thay vì reset 0 — nếu reset 0 thì mỗi
                // loop boundary vứt tới 1 frame overshoot → chu kỳ chậm dần, drift tích lũy (dt=0.6/dur=1 → chậm ~20%).
                // Carry giữ tổng thời gian đúng: N chu kỳ tốn đúng N×duration bất kể dt.
                _states[slot].elapsed -= _states[slot].duration;
                // Chặn runaway: nếu dt > duration (duration cực nhỏ / lag spike lớn), phần dư có thể vẫn > duration.
                // v1 tick TỐI ĐA 1 chu kỳ / frame (không fast-forward nhiều loop trong 1 tick — giống Unity animation
                // dưới frame drop). Clamp phần dư về [0, duration] để elapsed không phình vô hạn; frame sau rút tiếp.
                if (_states[slot].elapsed > _states[slot].duration)
                    _states[slot].elapsed = _states[slot].duration;
                else if (_states[slot].elapsed < 0f)
                    _states[slot].elapsed = 0f;
                switch (_states[slot].loopMode)
                {
                    case LoopMode.Restart:
                        break; // from/to giữ nguyên; chạy lại
                    case LoopMode.Yoyo:
                        (_states[slot].from, _states[slot].to) = (_states[slot].to, _states[slot].from); // đảo chiều
                        break;
                    case LoopMode.Incremental:
                        float4 delta = _states[slot].to - _states[slot].from;
                        _states[slot].from = _states[slot].to;
                        _states[slot].to = _states[slot].to + delta; // cộng dồn (spin >360°)
                        break;
                }
                return;
            }

            Finish(slot, stopped: false);
        }

        public void Stop(int slot, int version, bool complete)
        {
            if (!IsAlive(slot, version)) return; // version-safe (§0.4)

            if (complete)
            {
                // Nếu Complete() gọi khi tween CÒN trong delay (chưa hasStarted): from chưa capture &
                // (nếu relative) 'to' vẫn đang giữ offset. Phải resolve TRƯỚC khi áp giá trị cuối, nếu không
                // OnComplete bắn mà vật không tới đích (và tween relative sẽ nhảy tới offset thô — sai).
                // Capture/Apply đọc-ghi Transform/UI, KHÔNG reentrant vào runner → an toàn giữ trong Stop.
                if (!_states[slot].hasStarted)
                {
                    _states[slot].from = _states[slot].applier.Capture(_states[slot].target);
                    if (_states[slot].relative)
                        _states[slot].to = _states[slot].from + _states[slot].to; // resolve offset → đích tuyệt đối
                    _states[slot].hasStarted = true;
                }

                // FIX B: fast-forward các chu kỳ CÒN LẠI để 'to' khớp giá trị nghỉ TỰ NHIÊN của cả tween.
                // Không làm bước này thì Complete() một tween loop chỉ áp cuối chu kỳ HIỆN TẠI (sai vị trí):
                // Yoyo×2 nghỉ tự nhiên ở from nhưng Complete() lại ra to; Incremental×3 nghỉ ở 3B-2A nhưng ra B.
                // Ở t=1 mọi applier (lerp/slerp/jump-arc=0) đều trả 'to' → CHỈ cần 'to' đúng. dynamicSource != null
                // (đích động) không có ngữ nghĩa "final" cố định → bỏ qua fast-forward, để snapshot động quyết.
                if (_states[slot].dynamicSource == null)
                {
                    // remaining = số chu kỳ còn phải chạy (kể cả chu kỳ đang dở). Tween còn sống giữa chừng nên
                    // với hữu hạn: completedLoops < loopCount ⇒ remaining >= 1. Vô hạn: không có final ⇒ chốt chu kỳ hiện tại.
                    int remaining = _states[slot].isInfiniteLoop
                        ? 1
                        : _states[slot].loopCount - _states[slot].completedLoops;
                    if (remaining < 1) remaining = 1;

                    switch (_states[slot].loopMode)
                    {
                        case LoopMode.Restart:
                            break; // 'to' không đổi qua các chu kỳ → đã đúng
                        case LoopMode.Yoyo:
                            if ((remaining & 1) == 0) // chẵn chu kỳ còn lại → nghỉ ở 'from' hiện tại
                                (_states[slot].from, _states[slot].to) = (_states[slot].to, _states[slot].from);
                            break;
                        case LoopMode.Incremental:
                            float4 delta = _states[slot].to - _states[slot].from;
                            _states[slot].to = _states[slot].to + delta * (remaining - 1); // dồn tới đích cuối
                            break;
                    }
                    _states[slot].completedLoops = _states[slot].loopCount; // đánh dấu đã xong mọi chu kỳ
                }

                // Snapshot đích động rồi áp giá trị cuối (t=1). Không callback ở đây → an toàn.
                if (_states[slot].dynamicSource != null)
                    _states[slot].to = _states[slot].dynamicSource();
                float4 v = _states[slot].applier.Interpolate(in _states[slot], 1f, Easer.Evaluate(_states[slot].ease, 1f));
                _states[slot].applier.Apply(_states[slot].target, v);
            }

            Finish(slot, stopped: !complete);
        }

        /// <summary>
        /// Chốt kết thúc một tween DUY NHẤT một lần: gỡ bucket + trả slot TRƯỚC, rồi mới bắn callback/awaiter.
        /// Thứ tự này chống double-finish (callback gọi Stop() lại → IsAlive=false → no-op) và reentrancy an toàn.
        /// </summary>
        private void Finish(int slot, bool stopped) => Finish(slot, stopped, fault: null);

        /// <param name="fault">
        /// != null khi tween bị kết thúc do EXCEPTION thật trong tick (target Destroy, applier/dynamicSource ném — fix F).
        /// Khi có fault, awaiter nhận CHÍNH exception đó (TrySetException(fault)) bất kể behaviour — để crash KHÔNG bị
        /// nuốt thành OCE (ThrowCancellation) hay silent-success (CancelAwaitSilently). Kết thúc bình thường: fault=null.
        /// </param>
        private void Finish(int slot, bool stopped, Exception fault)
        {
            int version = _states[slot].version;

            // Trích callback ra local TRƯỚC khi Return xoá chúng — để vẫn bắn được sau khi slot đã "chết".
            TweenCallback done = stopped ? _states[slot].onStop : _states[slot].onComplete;
            long key = Key(slot, version);
            bool hasAwaiter = _awaits.TryGetValue(key, out AwaitEntry awaitEntry);
            if (hasAwaiter) _awaits.Remove(key);

            // GỠ + TRẢ SLOT NGAY (version++): từ đây IsAlive(slot,version)=false → mọi Stop/Complete tái nhập là no-op.
            RemoveFromBucket(slot);
            Return(slot); // inUse=false, version++, ResetReferences

            // Bây giờ mới bắn callback + đánh thức awaiter. Chúng có thể tạo/停 tween khác an toàn (slot này đã sạch).
            done.Invoke();          // TweenCallback.Invoke đã bọc try/catch (Task 2)

            // Đánh thức await — signal ĐÚNG 1 lần (source pooled, fix #1). Loại signal quyết định theo
            // LÝ DO kết thúc + behaviour đã lưu lúc AwaitTween (fix A) — KHÔNG dựa ct.IsCancellationRequested
            // (đường StopTweens()/Stop() thủ công không có ct nhưng vẫn phải tôn trọng ThrowCancellation).
            if (hasAwaiter)
            {
                if (fault != null)
                    awaitEntry.source.TrySetException(fault); // crash thật → propagate nguyên exception (fix vòng 6, Finding 2)
                // stopped=false (hoàn tất tự nhiên HOẶC Complete()/CompleteImmediately đã áp t=1) → luôn trả bình thường.
                // stopped=true (Stop thật, chưa áp giá trị cuối) → theo behaviour: ThrowCancellation ném OCE, còn lại im lặng.
                else if (stopped && awaitEntry.behaviour == TweenStopBehaviour.ThrowCancellation)
                    awaitEntry.source.TrySetException(new OperationCanceledException());
                else
                    awaitEntry.source.TrySetResult();
            }
        }

        public void Pause(int slot, int version) { if (IsAlive(slot, version)) _states[slot].isPaused = true; }
        public void Resume(int slot, int version) { if (IsAlive(slot, version)) _states[slot].isPaused = false; }

        // Buffer riêng cho StopAllOnTarget — KHÔNG tái dùng _tickBuffer: StopTweens() có thể gọi TỪ TRONG
        // một callback giữa TickBucket (vd OnComplete → other.StopTweens()) → tái dùng _tickBuffer sẽ clobber
        // vòng duyệt tick đang chạy. Buffer riêng, grow-only, zero steady-state GC.
        // DÙNG NHƯ STACK (append + truncate), KHÔNG Clear()+refill: StopAllOnTarget có thể TỰ TÁI NHẬP
        // (Finish → onStop / awaiter continuation chạy đồng bộ → gọi other.StopTweens()). Clear() ở lần lồng
        // sẽ xóa snapshot của lần ngoài → lần ngoài leak tween + await treo. Mỗi lần chiếm vùng [start,end) riêng.
        private readonly List<long> _stopBuffer = new List<long>(16);

        public int StopAllOnTarget(object target)
        {
            if (target == null) return 0;

            // FIX E: SNAPSHOT các slot khớp TRƯỚC, rồi Finish theo snapshot. Nếu Finish ngay trong lúc scan,
            // onStop callback có thể Create tween mới trên CÙNG target, Rent trúng một slot đã free có index
            // THẤP hơn vị trí scan → scan tới đó lại Finish nhầm tween vừa tạo phản ứng (chưa hề "on target"
            // lúc gọi). Snapshot (slot,version) cô lập tập cần dừng; tween tạo trong cascade không có trong snapshot.
            // FIX (vòng 6): append vào cuối _stopBuffer (không Clear) → an toàn khi StopAllOnTarget tái nhập chính nó.
            int start = _stopBuffer.Count;
            for (int slot = 0; slot < _count; slot++)
            {
                if (_states[slot].inUse && ReferenceEquals(_states[slot].target, target))
                    _stopBuffer.Add(Key(slot, _states[slot].version));
            }
            int end = _stopBuffer.Count;

            int stopped = 0;
            try
            {
                for (int i = start; i < end; i++)
                {
                    long key = _stopBuffer[i];
                    int slot = (int)(uint)key;
                    int version = (int)(key >> 32);
                    if (!IsAlive(slot, version)) continue; // callback trước đã dừng/tái dùng slot này → bỏ qua an toàn
                    // FIX (vòng 6, Finding 3): guard từng Finish — 1 slot lỗi (bucketIndex hỏng, continuation ném...)
                    // KHÔNG được abort cả batch (sẽ leak các tween còn lại + await treo). "1 listener lỗi không kill vòng".
                    try { Finish(slot, stopped: true); stopped++; }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }
            finally
            {
                // Cắt vùng của lần gọi này; lần lồng (nếu có) đã tự cắt vùng của nó trước khi trả về.
                _stopBuffer.RemoveRange(start, _stopBuffer.Count - start);
            }
            return stopped;
        }
```

- [ ] **Step 5: Await bridge**

Thêm vào class:

```csharp
        public async UniTask AwaitTween(int slot, int version, TweenStopBehaviour behaviour, CancellationToken ct)
        {
            if (!IsAlive(slot, version)) return; // đã xong

            long key = Key(slot, version);
            if (_awaits.ContainsKey(key))
            {
                // GIỚI HẠN v1: 1 awaiter / tween. Hai nơi cùng await một handle → source thứ 2 sẽ ném
                // InvalidOperationException mờ từ sâu trong UniTask ("can not await twice"). Fail RÕ tại call-site.
                // FIX (vòng 6, Finding 3): KHÔNG `return` trơn — `return` từ async UniTask = completed-success NGAY
                // → awaiter thứ 2 chạy continuation GIỮA animation (tưởng tween đã xong). Thay vào đó CHỜ THẬT bằng
                // poll IsAlive (không tôn trọng behaviour cho awaiter phụ — chấp nhận ở v1; multi-awaiter ghi bảng Hoãn).
                // Closure chỉ alloc trên đường misuse hiếm này. Case đúng: UniTask.WhenAll trên các handle KHÁC NHAU.
                Debug.LogError("[Tween] Một tween chỉ hỗ trợ 1 awaiter ở v1. Awaiter thứ 2 sẽ chờ tween kết thúc (dùng UniTask.WhenAll trên các handle KHÁC NHAU).");
                await UniTask.WaitWhile(() => IsAlive(slot, version));
                return;
            }
            var source = AutoResetUniTaskCompletionSource.Create();
            _awaits[key] = new AwaitEntry { source = source, behaviour = behaviour };

            // FIX #1: ct hủy KHÔNG tự signal source (source pooled — signal 2 lần sẽ đánh thức nhầm tween khác
            // sau khi source được tái dùng). Thay vào đó, ct hủy → CancelAwait → Stop → Finish, và CHỈ Finish
            // signal source đúng 1 lần rồi remove khỏi dict. Lần Stop thứ hai (nếu có) là no-op (IsAlive=false).
            CancellationTokenRegistration reg = default;
            if (ct.CanBeCanceled)
            {
                // state là ValueTuple → 1 box nhỏ CHỈ trên await-có-ct (await không ct = 0 alloc thêm ngoài source).
                reg = ct.Register(static boxed =>
                {
                    var (runner, sl, ver, beh) = ((TweenRunner, int, int, TweenStopBehaviour))boxed;
                    runner.CancelAwait(sl, ver, beh);
                }, (this, slot, version, behaviour));
            }

            try
            {
                // CHỈ Finish() signal → source dùng đúng 1 lần, an toàn với pool.
                // Fix A: nếu tween bị Stop thật + behaviour=ThrowCancellation, Finish gọi TrySetException(OCE)
                // → await NÀY tự ném OperationCanceledException (bất kể do ct hay Stop()/StopTweens() thủ công).
                // Hoàn tất tự nhiên / Complete() → TrySetResult → trả bình thường. Không còn nhánh throw-post-await.
                await source.Task;
            }
            finally
            {
                reg.Dispose();
            }
        }

        /// <summary>ct hủy await: dừng tween qua đường Finish (signal source 1 lần). Áp behaviour khi dừng.</summary>
        internal void CancelAwait(int slot, int version, TweenStopBehaviour behaviour)
        {
            if (!IsAlive(slot, version)) return; // tween đã tự xong trước khi ct kịp hủy
            // CompleteImmediately → Stop(complete:true): áp giá trị cuối rồi Finish(stopped:false) → TrySetResult.
            // ThrowCancellation / CancelAwaitSilently → Stop(complete:false): Finish(stopped:true) → Finish tự
            // quyết signal (OCE cho ThrowCancellation, TrySetResult cho Silent) theo behaviour đã lưu (fix A).
            bool complete = behaviour == TweenStopBehaviour.CompleteImmediately;
            Stop(slot, version, complete);
        }
```

> **Ghi chú await bridge (fix #1 — signal đúng 1 lần):** `AutoResetUniTaskCompletionSource` **tự trả về pool** sau khi await hoàn tất. Nếu signal 2 lần (một từ ct-register, một từ `Finish`), lần thứ hai sẽ đánh thức nhầm một await khác đã tái dùng cùng source từ pool — bug ẩn. Cách sửa: **chỉ `Finish` được signal source** (đúng 1 lần: `Finish` trích entry ra local + remove khỏi dict TRƯỚC khi trả slot, rồi signal ở cuối). ct hủy đi qua `CancelAwait → Stop → Finish` cùng đường đó. `reg.Dispose()` trong `finally` hủy đăng ký ct sau khi await xong.
>
> **Ghi chú fix A — behaviour tôn trọng MỌI đường Stop, không chỉ ct:** trước đây `throw OperationCanceledException` nằm SAU `await` và điều kiện là `ct.IsCancellationRequested`. Sai: `handle.Stop()` / `target.StopTweens()` (pattern hủy phổ biến nhất — gọi trong `OnDisable`) KHÔNG có ct, nên await trả về **im lặng như hoàn tất bình thường** dù tween bị cắt giữa chừng → continuation chạy nhầm (phát thưởng, chuyển state...). Sửa: `Finish` quyết loại signal theo **lý do kết thúc** (`stopped`) + `behaviour` đã lưu trong `AwaitEntry`: `stopped && ThrowCancellation` → `TrySetException(OCE)` (await tự ném, bất kể do ct hay Stop thủ công); còn lại → `TrySetResult` (im lặng). `CompleteImmediately` đi qua `Stop(complete:true)` → áp t=1 → `Finish(stopped:false)` → `TrySetResult` (giá trị cuối ĐÃ áp). Không còn nhánh throw-post-await dựa ct.
>
> **Alloc await:** await **không** ct → 1 completion source pooled (0 alloc thêm). Await **có** ct → thêm 1 box ValueTuple `(runner,slot,version,behaviour)` cho register (nhỏ, chỉ khi thực sự truyền ct). Tween không await → 0 alloc await hoàn toàn.
>
> **Giới hạn v1 — 1 awaiter / tween (chờ thật, không false-complete):** `_awaits` giữ **một** source cho mỗi `(slot,version)`; `AutoResetUniTaskCompletionSource` chỉ phục vụ **1 consumer**. Nếu HAI nơi cùng `await` một handle: `AwaitTween` phát hiện key đã tồn tại → `Debug.LogError` + awaiter thứ 2 **chờ thật** bằng `UniTask.WaitWhile(() => IsAlive(...))`. KHÔNG `return` trơn: `return` từ `async UniTask` = completed-success NGAY → awaiter thứ 2 chạy continuation GIỮA animation (tưởng tween xong) — còn tệ hơn `InvalidOperationException` mà nó thay (fix vòng 6, Finding 3). Awaiter phụ không tôn trọng behaviour (chấp nhận v1). Đúng cách: `UniTask.WhenAll(a.ToUniTask(), b.ToUniTask())` trên các handle **khác nhau**. Multi-awaiter cùng handle: **bảng Hoãn**.
>
> **Ghi chú fix F — crash trong tick KHÔNG bị nuốt (Finding 2 vòng 6):** khi target bị Destroy / applier / dynamicSource ném exception thật trong `StepTween`, catch ở `TickBucket` gọi `Finish(slot, stopped:true, fault:e)`. `Finish` khi `fault != null` → `TrySetException(fault)` (chính exception đó) **bất kể behaviour** — nếu không, `CancelAwaitSilently` (default) sẽ khiến await trả về như hoàn tất thành công (nuốt crash → continuation chạy nhầm), còn `ThrowCancellation` sẽ ngụy trang crash thành OCE. onStop vẫn bắn để dọn dẹp. Exception cũng đã `Debug.LogException` để có stack thật.
>
> **Ghi chú fix (Finding 1/3 vòng 6) — `StopAllOnTarget` re-entrancy + batch-guard:** `_stopBuffer` dùng như **stack** (append vùng `[start,end)` + `RemoveRange` trong `finally`), KHÔNG `Clear()`+refill — vì `Finish→onStop`/awaiter-continuation chạy đồng bộ có thể gọi `StopTweens()` lồng nhau; `Clear()` sẽ xóa snapshot lần ngoài → leak tween + await treo. Mỗi `Finish` trong batch còn được bọc try/catch riêng để 1 slot lỗi không abort cả batch (leak phần còn lại).
>
> **Edge case đã xử lý đúng:** nếu `ct` đã hủy sẵn lúc `ct.Register`, callback chạy đồng bộ ngay → `CancelAwait→Finish` signal source trước khi `await` → `await source.Task` trả về (hoặc ném OCE nếu ThrowCancellation) ngay (không deadlock, single-thread).

- [ ] **Step 6: Commit**

```bash
git add Runtime/Utilities/Tweening/Core/TweenRunner.cs
git commit -m "feat(tween): TweenRunner engine (pool, bucket tick, loop, stop, await bridge)"
```

**Kiểm chứng:**

| Input | Kỳ vọng |
|---|---|
| `Create` lần đầu | slot 0, **version 1** (không phải 0 — §0.4), bucket Update có 1 phần tử |
| tick tween dur=1, dt=0.5, Linear, pos 0→10 | sau 1 tick pos=5; sau 2 tick pos=10 rồi Finish, slot trả pool |
| tween xong, `Create` lại | tái dùng slot 0, version=2 (Return đã ++ từ 1); handle cũ `{0,1}` `IsActive==false` |
| **loop off-by-one:** `SetLoops(2)`, dur=1 | chạy **đúng 2 chu kỳ** rồi Finish (completedLoops: 1<2 → loop, 2<2 sai → finish). `SetLoops(1)` = 1 chu kỳ (không loop) |
| Yoyo `SetLoops(2)`, sau chu kỳ 1 | from/to hoán đổi, elapsed **carry** (−=duration, clamp), chạy chiều về; hết chu kỳ 2 thì Finish. Số chu kỳ chẵn → nghỉ ở A; lẻ → nghỉ ở B |
| Incremental sau chu kỳ | from=to cũ, to=to+delta (spin dồn) |
| **loop timing (fix D):** dur=1, dt=0.6, Restart | mỗi chu kỳ tốn đúng ~1.0s (carry dư 0.2 sang chu kỳ sau), KHÔNG chậm ~20% như reset-0 |
| **loop dt lớn (fix D):** dur=0.1, dt=0.35, SetLoops(5) | v1 tick tối đa 1 chu kỳ/frame; elapsed clamp về [0,duration] không phình; các chu kỳ còn lại rút dần frame sau |
| delay=0.3, tick dt=0.5 | frame đầu: hết delay, dt tràn 0.2 áp vào elapsed, capture from đúng lúc này |
| `StopAllOnTarget(tf)` với 2 tween trên tf | cả 2 Finish(stopped), trả 2 |
| **reentrancy — OnComplete tạo tween mới** (case phổ biến nhất) | tween cũ đã Return trước khi callback chạy; tween mới vào frame sau; nếu Resize xảy ra, không dangling ref (dùng `_states[slot]` theo index) |
| **reentrancy — OnComplete gọi `Stop()` chính nó** | lần Stop thứ 2 no-op (IsAlive=false sau Return); free-list KHÔNG hỏng (Finish chỉ chạy 1 lần) |
| **reentrancy — OnComplete gọi `StopAllOnTarget`** (stop tween khác cùng bucket) | snapshot `_tickBuffer` + kiểm `IsAlive` mỗi slot → tween bị stop giữa frame bị bỏ qua, không double-tick |
| **reentrancy (fix E) — onStop tạo tween mới trên cùng target** | `StopAllOnTarget` snapshot `_stopBuffer` (riêng, không đụng `_tickBuffer`) trước khi Finish → tween tạo phản ứng KHÔNG bị dừng nhầm |
| **reentrancy (vòng 6) — onStop gọi `other.StopTweens()` (StopAllOnTarget lồng nhau)** | `_stopBuffer` dùng như stack (vùng [start,end) + RemoveRange finally) → lần lồng KHÔNG xóa snapshot lần ngoài; mọi tween lần ngoài vẫn bị dừng, count đúng |
| **robustness (vòng 6) — 1 slot ném lỗi giữa batch StopAllOnTarget** | try/catch mỗi Finish → slot lỗi bị log, các slot còn lại VẪN dừng (không abort batch, không leak) |
| **reentrancy (fix C) — callback đổi SetTickPhase tween khác Update→Late** | guard `tickPhase != phase` trong TickBucket → tween đã dời chỉ tick ở bucket đích, không double-tick cùng frame |
| **robustness (fix F) — target bị Destroy không gọi StopTweens()** | `StepTween` throw MissingReferenceException → catch trong TickBucket → `Finish(stopped, fault:e)` gỡ slot + đánh thức awaiter; engine KHÔNG kẹt, await KHÔNG treo, source không rò rỉ |
| **crash await (vòng 6, Finding 2) — target Destroy khi đang await (mọi behaviour)** | `Finish(fault:e)` → awaiter nhận CHÍNH exception (`TrySetException(e)`), KHÔNG bị nuốt thành silent-success (CancelAwaitSilently) hay ngụy trang OCE (ThrowCancellation); có Debug.LogException |
| **multi-awaiter (vòng 6, Finding 3) — 2 nơi await cùng handle** | awaiter thứ 2: LogError + `UniTask.WaitWhile(IsAlive)` chờ THẬT tới khi tween xong, KHÔNG false-complete ngay giữa animation |
| `await handle` tween chạy xong | await trả về sau khi OnComplete bắn |
| `await handle.ToUniTask(ct)` rồi ct hủy giữa chừng | `CancelAwait→Stop→Finish` signal source **đúng 1 lần**; source pooled không bị signal kép (fix #1) |
| **await (fix A) — `ThrowCancellation` rồi `handle.Stop()`/`StopTweens()` (KHÔNG ct)** | await ném `OperationCanceledException` (Finish→TrySetException theo behaviour đã lưu), KHÔNG trả im lặng như hoàn tất |
| **await (fix A) — `CompleteImmediately` rồi `Stop()` thủ công** | Stop(complete:true) áp t=1 rồi TrySetResult → await trả bình thường, vật ĐÃ ở đích cuối |
| `MoveBucket` sang LateUpdate | slot rời _updateBucket, vào _lateBucket; bucketIndex cập nhật đúng |
| applier cache | `s.applier == TweenAppliers.Get(kind)`; tick không gọi `Get` |
| `Complete()` khi tween CÒN trong delay (chưa hasStarted) | resolve from=Capture + relative offset trước, rồi áp đích cuối → vật TỚI đích, OnComplete bắn đúng (không nhảy tới offset thô) |
| **`Complete()` (fix B) — Yoyo `SetLoops(2)` giữa chu kỳ 1** | fast-forward: remaining=2 chẵn → swap → nghỉ ở A (đúng giá trị nghỉ tự nhiên), không phải B |
| **`Complete()` (fix B) — Incremental `SetLoops(3)` giữa chu kỳ 1** | fast-forward: to = B + (B−A)×2 = 3B−2A (đích cuối thật), không phải B |
| **`Complete()` (fix B) — tween đích động (dynamicSource)** | bỏ qua fast-forward loop; áp snapshot đích động hiện tại (không có "final" cố định) |
| `Stop()` version lệch | `IsAlive` false → no-op |

---

### Task 10: Transform + UI extension API (mặt tiền chính)

**Files:**
- Create: `Runtime/Utilities/Tweening/Api/TransformTweenExtensions.cs`
- Create: `Runtime/Utilities/Tweening/Api/UITweenExtensions.cs`

**Interfaces:**
- Consumes: `ITweenRunner` (Task 8), các applier `.Instance.Id` (Task 5–7), `TweenPack` (Task 3), `TweenHandle`.
- Produces: extension method self-document trả `TweenHandle` để chain. Đây là API developer dùng 95% thời gian.

Giải: mỗi extension chỉ là "pack giá trị + gọi `Runner.Create(applierId, target, to, dur)`". Không logic — mọi thứ nằm ở runner. Tên method = thuộc tính (`TweenPosition`), self-document hơn `DOMove`.

- [ ] **Step 1: TransformTweenExtensions.cs**

```csharp
using UnityEngine;
using Horcrux.Runtime.Tweening;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    /// <summary>Extension tween cho Transform (gameplay world-space). Mọi method trả TweenHandle để chain.</summary>
    public static class TransformTweenExtensions
    {
        private static ITweenRunner R => ITweenRunner.Current;

        /// <summary>Tween Transform.position (world) tới <paramref name="to"/> trong <paramref name="duration"/> giây.</summary>
        public static TweenHandle TweenPosition(this Transform t, Vector3 to, float duration)
            => R.Create(PositionApplier.Instance.Id, t, TweenPack.Vec3(to), duration);

        public static TweenHandle TweenLocalPosition(this Transform t, Vector3 to, float duration)
            => R.Create(LocalPositionApplier.Instance.Id, t, TweenPack.Vec3(to), duration);

        public static TweenHandle TweenScale(this Transform t, Vector3 to, float duration)
            => R.Create(ScaleApplier.Instance.Id, t, TweenPack.Vec3(to), duration);

        /// <summary>Scale đều 3 trục về cùng một hệ số.</summary>
        public static TweenHandle TweenScale(this Transform t, float uniform, float duration)
            => R.Create(ScaleApplier.Instance.Id, t, TweenPack.Vec3(new Vector3(uniform, uniform, uniform)), duration);

        /// <summary>
        /// Tween localEulerAngles (cho phép vượt 360° với Incremental — spin). KHÔNG đi cung ngắn nhất:
        /// nội suy tuyến tính trên euler, near-wrap (vd 350°→10°) sẽ đi đường dài. Xoay theo cung ngắn nhất
        /// dùng <see cref="TweenRotation"/> (quaternion slerp).
        /// </summary>
        public static TweenHandle TweenLocalRotation(this Transform t, Vector3 eulerTo, float duration)
            => R.Create(LocalEulerApplier.Instance.Id, t, TweenPack.Vec3(eulerTo), duration);

        /// <summary>Tween rotation (world) bằng quaternion slerp.</summary>
        public static TweenHandle TweenRotation(this Transform t, Quaternion to, float duration)
            => R.Create(RotationApplier.Instance.Id, t, TweenPack.Quat(to), duration);

        /// <summary>
        /// Nhảy parabol (world position) tới <paramref name="to"/>, đỉnh cung cao <paramref name="jumpPower"/>.
        /// Kết hợp .SetDynamicTarget(...) để bám target di chuyển (§ đích động).
        /// </summary>
        public static TweenHandle TweenJump(this Transform t, Vector3 to, float jumpPower, float duration)
            => R.Create(JumpApplier.Instance.Id, t, TweenPack.Vec3(to), duration).SetJumpPower(jumpPower);

        /// <summary>Dừng mọi tween đang chạy trên transform này (gọi trong OnDisable/OnDestroy).</summary>
        public static int StopTweens(this Transform t) => R?.StopAllOnTarget(t) ?? 0;
    }
}
```

- [ ] **Step 2: UITweenExtensions.cs**

```csharp
using UnityEngine;
using UnityEngine.UI;
using Horcrux.Runtime.Tweening;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    /// <summary>Extension tween cho UI/SpriteRenderer. Mọi method trả TweenHandle để chain.</summary>
    public static class UITweenExtensions
    {
        private static ITweenRunner R => ITweenRunner.Current;

        /// <summary>Tween RectTransform.anchoredPosition (UI).</summary>
        public static TweenHandle TweenAnchoredPosition(this RectTransform rt, Vector2 to, float duration)
            => R.Create(AnchoredPositionApplier.Instance.Id, rt, TweenPack.Vec2(to), duration);

        /// <summary>Fade CanvasGroup.alpha (panel/popup). Nhớ .SetTimeMode(Unscaled) nếu chạy khi game pause.</summary>
        public static TweenHandle TweenAlpha(this CanvasGroup cg, float to, float duration)
            => R.Create(CanvasGroupAlphaApplier.Instance.Id, cg, TweenPack.Float(to), duration);

        public static TweenHandle TweenColor(this SpriteRenderer sr, Color to, float duration)
            => R.Create(SpriteColorApplier.Instance.Id, sr, TweenPack.Col(to), duration);

        /// <summary>Chỉ tween alpha của SpriteRenderer, giữ nguyên RGB hiện tại tại thời điểm bắt đầu.</summary>
        public static TweenHandle TweenFade(this SpriteRenderer sr, float toAlpha, float duration)
        {
            Color c = sr.color; c.a = toAlpha;
            return R.Create(SpriteColorApplier.Instance.Id, sr, TweenPack.Col(c), duration);
        }

        /// <summary>Tween color của Graphic (Image/RawImage/Text).</summary>
        public static TweenHandle TweenColor(this Graphic g, Color to, float duration)
            => R.Create(GraphicColorApplier.Instance.Id, g, TweenPack.Col(to), duration);

        /// <summary>Tween Image.fillAmount (thanh tiến độ/cooldown).</summary>
        public static TweenHandle TweenFill(this Image img, float to, float duration)
            => R.Create(FillApplier.Instance.Id, img, TweenPack.Float(to), duration);

        public static int StopTweens(this Component c) => R?.StopAllOnTarget(c) ?? 0;
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add Runtime/Utilities/Tweening/Api/TransformTweenExtensions.cs Runtime/Utilities/Tweening/Api/UITweenExtensions.cs
git commit -m "feat(tween): Transform + UI extension API (fluent, self-documenting)"
```

**Kiểm chứng:**

| Input | Kỳ vọng |
|---|---|
| `transform.TweenPosition(v, 0.5f)` | trả TweenHandle hợp lệ; tween chạy sau 1 frame |
| `transform.TweenScale(1.2f, 0.3f)` | scale đều tới (1.2,1.2,1.2) |
| `img.TweenFill(1f, 0.25f)` | fillAmount chạy tới 1 |
| `cg.TweenAlpha(0,0.2f).SetTimeMode(TimeMode.Unscaled)` | fade chạy cả khi `Time.timeScale=0` |
| `transform.TweenJump(hole,2f,0.5f).SetDynamicTarget(()=>TweenPack.Vec3(hole.position))` | nhảy bám hole di chuyển |
| `transform.StopTweens()` | dừng mọi tween trên transform |

---

### Task 11: Tween.To factory (generic gate) + Tweening.md hoàn tất

**Files:**
- Create: `Runtime/Utilities/Tweening/Api/Tween.cs`

**Interfaces:**
- Consumes: `ITweenRunner`, `LambdaApplier` (Task 7), `TweenPack`.
- Produces: `static class Tween` — `Tween.To(Func<float> getter, Action<float> setter, float to, float duration)` cho giá trị tuỳ ý (shader/audio/scalar). Cửa case hiếm (§ setter phương án C) — chấp nhận 1 alloc `FloatChannel`.

- [ ] **Step 1: Tween.cs**

```csharp
using System;
using Horcrux.Runtime.Tweening;

namespace Horcrux.Runtime.Tweening
{
    /// <summary>
    /// Điểm vào tĩnh cho tween giá trị tuỳ ý không gắn thuộc tính Unity chuẩn
    /// (shader property, audio volume, biến scalar...). Dùng khi không có extension chuyên biệt.
    /// Chấp nhận 1 alloc (FloatChannel giữ getter/setter) — chỉ cho case hiếm (§ setter phương án C).
    /// Case phổ biến (position/scale/color/fill) DÙNG extension trong ExtensionMethods để zero-GC.
    /// </summary>
    public static class Tween
    {
        private static ITweenRunner R => ITweenRunner.Current;

        /// <summary>
        /// Tween một float tuỳ ý từ giá trị hiện tại (getter đọc lúc bắt đầu) tới <paramref name="to"/>.
        /// </summary>
        public static TweenHandle To(Func<float> getter, Action<float> setter, float to, float duration)
        {
            var channel = new LambdaApplier.FloatChannel { getter = getter, setter = setter };
            return R.Create(LambdaApplier.Instance.Id, channel, TweenPack.Float(to), duration);
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Runtime/Utilities/Tweening/Api/Tween.cs
git commit -m "feat(tween): Tween.To generic gate for arbitrary float values"
```

**Kiểm chứng:**

| Input | Kỳ vọng |
|---|---|
| `Tween.To(()=>mat.GetFloat(id), v=>mat.SetFloat(id,v), 1f, 0.5f)` | shader float chạy 0→1 |
| getter đọc lúc start | from = giá trị hiện tại tại frame đầu (sau delay) |
| chain `.SetEase().OnComplete()` | hoạt động như mọi tween khác (trả TweenHandle) |

---

## Bảng tổng kết API (tra cứu nhanh)

**Tạo tween** (mọi method trả `TweenHandle`):

| Gọi | Áp vào | Kiểu |
|---|---|---|
| `transform.TweenPosition(v3, dur)` | Transform.position | Vector3 |
| `transform.TweenLocalPosition(v3, dur)` | localPosition | Vector3 |
| `transform.TweenScale(v3\|float, dur)` | localScale | Vector3 |
| `transform.TweenLocalRotation(euler, dur)` | localEulerAngles (spin >360°) | Vector3 |
| `transform.TweenRotation(quat, dur)` | rotation (slerp) | Quaternion |
| `transform.TweenJump(v3, power, dur)` | position + arc parabol | Vector3 |
| `rect.TweenAnchoredPosition(v2, dur)` | anchoredPosition | Vector2 |
| `cg.TweenAlpha(f, dur)` | CanvasGroup.alpha | float |
| `sr.TweenColor(c, dur)` / `.TweenFade(a, dur)` | SpriteRenderer.color | Color |
| `graphic.TweenColor(c, dur)` | Graphic.color | Color |
| `img.TweenFill(f, dur)` | Image.fillAmount | float |
| `Tween.To(get, set, f, dur)` | float tuỳ ý (shader/audio) | float |

**Cấu hình** (chain, trước frame đầu): `.SetEase(EaseType)` · `.SetDelay(s)` · `.SetLoops(n, LoopMode)` · `.SetRelative()` · `.SetTimeMode(TimeMode)` · `.SetTickPhase(TickPhase)` · `.SetJumpPower(p)` · `.SetDynamicTarget(Func<float4>)`.

**Sự kiện:** `.OnStart` · `.OnUpdate(Action<float>)` · `.OnComplete` · `.OnStop` · `.OnStepComplete` (mỗi cái có overload `(state, staticDelegate)` cho hot path).

**Điều khiển:** `.Stop()` · `.Complete()` · `.Pause()` · `.Resume()` · `.IsActive` · `transform.StopTweens()`.

**Await:** `await handle;` (im lặng khi Stop) hoặc `await handle.ToUniTask(TweenStopBehaviour, ct);` · nhiều tween: `await UniTask.WhenAll(a.ToUniTask(), b.ToUniTask());`.

## Performance (bảng metrics)

| Hạng mục | Giá trị | Cơ chế |
|---|---|---|
| Alloc khi tạo tween (case chuẩn) | **0 B** | struct state trong mảng pooled; extension pack float4 trên stack |
| Alloc khi tạo tween (Tween.To generic) | 1 × `FloatChannel` | closure gate, chỉ case hiếm |
| Alloc mỗi frame tick | **0 B** | ref array element, applier **cached trong state** (không lookup registry), không boxing/LINQ/closure |
| Alloc khi await (không ct) | 1 × completion source pooled | `AutoResetUniTaskCompletionSource`; tween không await = 0 |
| Alloc khi await (có ct) | + 1 box ValueTuple cho ct.Register | chỉ khi truyền CancellationToken |
| Chi phí tick 1 tween | 1 lerp float4 (SIMD) + 1 Apply + 1 ease eval; **0 lookup** applier | `invDuration` precompute (chia→nhân); applier cached lúc Create |
| Remove tween khỏi bucket | O(1) | swap-remove qua bucketIndex |
| Cấp/trả slot | O(1) | free-list |
| Grow mảng | amortized O(1), grow-only | `Array.Resize` ×2, không co |
| Stop-per-target (v1) | O(n) linear scan | **hoãn tối ưu:** dictionary Transform→slots ở bản sau |
| Stale handle safety | version check O(1) | slot version tăng mỗi lần tái dùng |

## Hoãn phát triển sau (thiết kế đã chừa chỗ — không sửa lõi khi thêm)

| Hạng mục | Cách thêm mà không sửa lõi |
|---|---|
| **Sequence** (Append/Join/Insert timeline) | Class mới điều phối nhiều `TweenHandle` + await; hoặc applier "group". Runner không đổi. |
| **AnimationCurve làm ease** | Thêm field `AnimationCurve` vào TweenState + nhánh trong tính `k`; hoặc EaseSource struct. |
| **Punch / Shake / BouncyScale** | Preset = tổ hợp tween cơ bản + Sequence; hoặc applier chuyên biệt (dao động/random). |
| **Stop-per-target O(1)** | Thêm `Dictionary<object,List<int>>` trong runner; API không đổi. |
| **TMP color / material property applier** | Thêm applier mới + Register + extension. Open/Closed. |
| **Tách hot/cold state, dense packing** | Refactor nội bộ runner; API & handle không đổi. |
| **.From() tường minh** | Thêm overload extension + field `hasExplicitFrom` trong state. |
| **Tách `TweenAwaitRegistry` (SRP)** | Await hiện nằm trong `TweenRunner` (dict + completion source). Có thể tách thành class riêng để runner chỉ lo pool+tick (đúng "không monolithic" MY_SKILL §3.2); `Finish` ủy thác signal qua nó. Quyết lúc triển khai — API & handle không đổi. |
| **Multi-awaiter / tween** | v1 giới hạn 1 awaiter mỗi tween (1 `AutoResetUniTaskCompletionSource`/slot). Cần nhiều nơi await cùng 1 tween: đổi value dict thành list source, hoặc dùng `UniTaskCompletionSource` non-autoreset. |

---

*Tài liệu này là nguồn sự thật (MY_SKILL §5.1/§5.3). Mỗi khi sửa hệ thống phải cập nhật file này.*
