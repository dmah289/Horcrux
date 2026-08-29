# Bootstrap & Lifecycle Implementation Plan

> **Loại tài liệu:** Plan — developer tự code lại để nắm logic. `.md` thiết kế + `.html` viết **sau** khi có source.
>
> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development hoặc superpowers:executing-plans. Steps dùng checkbox (`- [ ]`).

**Goal:** MỘT con đường khởi tạo duy nhất cho cả game — `BootstrapRunner` sort các `BootStep` theo `Order`, await tuần tự, **fail-open** (một bước throw không treo splash), cấp **token vòng đời** (reload level = huỷ sạch mọi loop async của level trước), fan-out hook pause/quit theo **thứ tự ngược**.

**Architecture:** 2 tầng, tổng **7 file** (4 contract + 1 runner + 2 demo).

```
Contract (BootStep, IBootstrapService)   thứ tự + 2 nhịp + 2 hook app · trạng thái "init xong chưa"
Runner   (BootstrapRunner)               sort ổn định · await tuần tự · fail-open · token · progress event
Game     (các bước cụ thể)               kế thừa BootStep, wire vào list của runner trong Inspector
```

**Tech Stack:** C#, UniTask, `Sisus.Init` (`[Service]`). **Không** Addressables, không toán.

## Global Constraints

| Ràng buộc | Giá trị |
|---|---|
| Namespace | `Horcrux.Runtime.Abstractions.Bootstrap` · `…Implementations.Bootstrap` (riêng `IOptionalService`: `Horcrux.Runtime.Abstractions` — cạnh `IService`) |
| Hiệu năng | Boot chạy **một lần**, reinit chạy **mỗi lần load level** — không hot path ⇒ chọn bản dễ đọc nhất. `BootProgress` là `readonly struct`; `ProgressChanged` là `event Action<T>` — hợp lệ vì thưa (SystemPlan §0.4b) |
| SOLID | Runner chỉ biết contract `BootStep`, không biết bước làm gì (D) · bước không biết nhau và không biết runner (S) · không type nào mang ngữ nghĩa game (SystemPlan §0.1) |
| Editor-first | Danh sách bước + giá trị `Order` là **cấu hình**, gán trong Inspector; code chỉ lo hành vi chạy |
| An toàn | Fail-open từng bước · try/catch quanh **từng** callback (SystemPlan §0.4a) · `CancellationToken` propagate xuống mọi bước · `OnDestroy` huỷ token + nhả consumer đang await |
| Bất biến | ① chiều ưu tiên khai ở **đúng một chỗ** (`BootStep.Order`: **số nhỏ chạy trước**) ② trùng `Order` ⇒ thứ tự **xác định** (sort ổn định theo index gốc) ③ hai vòng init/reinit **không bao giờ chạy chồng** |

## Ngữ cảnh đã chốt

Nguồn thiết kế: `SystemPlan.md` mục 1 (đã duyệt 2026-08-29). Nguồn extract: `color-loop` — `BaseManager.cs` (contract 2 nhịp + AfterReinitialize) · `GameManager.cs` (RefreshGameToken, fan-out hook) · `ServiceInit.cs`/`StartGame.cs`/`GameInitializer.cs` (phản ví dụ: 3 điểm init rời rạc, 2 chiều sort ngược nhau).

| Nhóm | Chốt |
|---|---|
| **Ai gọi** | Entry scene của game gọi `InitializeAsync()` đúng một lần lúc cold start, rồi `ReinitializeAsync()` cho level đầu và mỗi lần reload level (thường từ hook MidTransition của Scene Flow) · splash của game subscribe `ProgressChanged` (reference kéo thả trong scene) · hệ ngoài (LiveOps Host §20 sau này) hỏi qua `IBootstrapService.TryGet` + `UntilInitialized` |
| **Mục tiêu** | Nhìn log boot kể lại được đúng thứ tự init · một bước throw giữa cold start vẫn vào được game · reinit huỷ sạch loop của level trước |
| **Ngân sách** | Cold start 1 lần + reinit mỗi level. Không hot path — không tối ưu gì, ưu tiên đọc hiểu |
| **Ranh giới** | SDK: contract + runner (sort, await, token, progress, hook). Game: các bước cụ thể, giá trị `Order`, thời điểm gọi 2 vòng. Runner **không tự chạy** trong `Start()` — game quyết thời điểm (phải phối hợp với splash/ATT prompt) |
| **Hướng mở rộng thật** (đều additive) | Manifest SO (overload nhận config asset) · chạy song song bước cùng pha (cờ trên contract + `WhenAll`) · nhóm bước theo scene |
| **Cố ý KHÔNG làm + lý do** (*xoá đi thì hỏng ở đâu*) | ① **Manifest data-driven (SO)** — chưa ai cần đổi thứ tự bước mà không compile. ② **Parallel-in-phase** — cold start chưa đo được là chậm. ③ **Auto-discovery** (quét scene/reflection tìm bước) — magic khó debug; danh sách trong Inspector lộ đủ. ④ **Expose token ra property public** — v1 mọi bước nhận token qua tham số 2 nhịp; hệ ngoài chưa ai cần. ⑤ **Reset `IsInitialized` khi reinit** — nó là latch "cold boot xong" một chiều; LiveOps cần đúng nghĩa đó |

**Hai quyết định user đã chốt (2026-08-29, đã đồng bộ vào SystemPlan mục 1):**

1. Phase event hiện thực bằng **`BootProgress` theo bước** (index + count + tên bước), **không** enum phase cứng. Lý do: tên phase là nội dung riêng từng game; bước đã tự mang tên hiển thị được; enum cứng bắt mọi game map bước→phase — một tri thức trùng phải giữ khớp ở hai nơi. Splash vẫn đủ hiển thị (ratio + label). Cần enum phase thật thì thêm sau là additive.
2. Hook pause: `isPaused == true` đi **ngược** (pause là "quit không hẹn trước" trên Android — hệ trên ghi vào hệ nền xong, hệ nền mới chốt sổ), `isPaused == false` đi **xuôi** như init (resume là "init-nhẹ" — hệ nền tỉnh trước, hệ trên tính toán dựa vào nó sau).

**Khảo sát tái sử dụng:** `IService<T>` đã có — dùng lại. `EventBus` (Utilities) có nhưng không dùng cho `ProgressChanged`: đây là event nội bộ một-service, listener wire trực tiếp, không cần bus xuyên module. `MonoSingleton` không dùng — đăng ký qua `[Service]` như tiền lệ `HapticService`. `IOptionalService<T>` **chưa có trên đĩa** (SystemPlan §0.2 chỉ có contract mẫu; file thuộc plan Ticker đã bị xoá) — Task 1 tạo, Ticker sau này dùng lại.

---

## §0. Bốn ràng buộc thật

Không có toán. Bốn sự thật của nền tảng quyết định hình dạng code — đọc trước khi viết.

### 0.1. Unity gọi magic method trên MỌI MonoBehaviour trùng tên — hook phải đổi tên

Unity gọi `OnApplicationPause`/`OnApplicationQuit` trên **mọi** MonoBehaviour có method trùng tên, bất kể access modifier. Bản color-loop đặt hook virtual tên `OnApplicationPause` ngay trên `BaseManager` ⇒ mỗi manager bị gọi **hai lần**: Unity gọi thẳng + `GameManager` fan-out. *Đã sai một lần — color-loop, sai âm thầm vì phần lớn handler idempotent.*

**Hệ quả lên API:** hook trên `BootStep` tên `OnAppPause(bool)` / `OnAppQuit()` — Unity không biết tên này, chỉ runner gọi. Cái sai **không thể xảy ra**, không phải "nhớ đừng override nhầm".

**Phép kiểm tái lập:** thêm `Debug.Log` vào `OnAppPause` của một `DemoBootStep`, chạy demo, bấm pause trong Editor — log hiện đúng **một** lần mỗi bước.

### 0.2. `List<T>.Sort` không ổn định — trùng `Order` là thứ tự đổi giữa các lần chạy

.NET dùng introsort (không stable). Hai bước trùng `Order` có thể đổi chỗ nhau giữa hai lần chạy — bug "lúc được lúc không" khó tái lập nhất. *Đã sai một lần — color-loop: `EntitiesManager` và `BoosterManager` cùng Priority 0, thứ tự không xác định.*

**Hệ quả lên code:** sort theo khoá kép `(Order, index gốc trong Inspector)` — trùng `Order` thì phần tử đứng trước trong list chạy trước, lặp lại y hệt mọi lần chạy.

| Input | Kỳ vọng |
|---|---|
| `[A(0), B(10), C(0)]` (theo thứ tự Inspector) | chạy `A → C → B`, mọi lần chạy đều vậy |

### 0.3. `UniTask` chỉ await được MỘT lần — task "chờ init xong" phải `Preserve()`

`UniTask` mặc định dùng nguồn pooled, await lần hai là undefined behavior. `UntilInitialized()` sẽ bị nhiều consumer await ⇒ cache **một** bản `initializedSource.Task.Preserve()` (bản cho phép await nhiều lần) ngay ở `Awake`, mọi call trả về bản đó.

Cùng ràng buộc này loại cách "lưu UniTask của vòng đang chạy rồi await nó khi vòng mới bắt đầu" — thay bằng cờ `isRoundRunning` + vòng chờ `UniTask.Yield()` (§0.4).

### 0.4. Huỷ vòng ≠ lỗi bước — hai loại exception, hai cách xử

Trong một vòng init có hai loại exception **khác bản chất**, nuốt chung một `catch` là mất phân biệt:

| Exception | Nghĩa | Xử |
|---|---|---|
| `OperationCanceledException` khi token của vòng đã huỷ | vòng mới tiếp quản (reload trong lúc đang init) | **dừng êm cả vòng** — không log như lỗi, không chạy bước kế |
| Mọi exception khác | bước hỏng thật (mất mạng, config lỗi…) | **fail-open**: log rõ tên bước + exception, **đi tiếp** bước kế — chơi được offline tốt hơn không mở được app |

*Đã sai một lần — foods_jam:* `.Forget()` trần nuốt exception ⇒ loading treo vĩnh viễn, có comment thừa nhận trong code. Fail-open + log là câu trả lời cấu trúc.

Vòng bị huỷ giữa chừng còn một hệ quả: **hai vòng không được chạy chồng** (bước 5 vòng cũ chạy song song bước 0 vòng mới là state đan xen). Trình tự bắt buộc khi mở vòng mới: ① huỷ token cũ → ② chờ `isRoundRunning == false` (vòng cũ thoát hẳn ở await kế tiếp của nó) → ③ mới chạy.

---

## Bản đồ triển khai

| Task | File | Nội dung |
|---|---|---|
| 1 | `Abstractions/Foundations/IOptionalService.cs` · `Abstractions/Foundations/Bootstrap/` — `BootStep.cs` · `BootProgress.cs` · `IBootstrapService.cs` | 4 contract |
| 2 | `Implementations/Foundations/Bootstrap/BootstrapRunner.cs` | runner |
| 3 | `Implementations/Foundations/Bootstrap/Demo/` — `DemoBootStep.cs` · `DemoBootDriver.cs` + scene demo + cập nhật `SystemPlan.md` | nghiệm thu |

Thứ tự: **1 → 2 → 3**.

---

### Task 1: 4 contract

**Files:** `Assets/Horcrux/Runtime/Abstractions/Foundations/IOptionalService.cs` + 3 file trong `Assets/Horcrux/Runtime/Abstractions/Foundations/Bootstrap/`

**Interfaces:**
- Consumes: `IService<T>` (đã có) · `Sisus.Init.Service` · UniTask.
- Produces: `IOptionalService<T>` (1 member static) · `abstract class BootStep : MonoBehaviour` (`Order` + 3 nhịp + 2 hook) · `readonly struct BootProgress` (3 field + 2 property suy ra) · `IBootstrapService : IOptionalService<>` (2 member).

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `BootStep` là **abstract MonoBehaviour**, không interface | Bước phải serialize được vào `List<>` trong Inspector (Editor-first) — interface cần thêm máy móc `InterfaceReference` chưa tồn tại. Tiền lệ: `PoolableBehaviour` cũng là MonoBehaviour nằm trong `Abstractions/` |
| `Order` chỉ có **một nguồn**: field serialize, property **không virtual** | color-loop cho override `Priority` ở code *đè* giá trị scene ⇒ hai nguồn sự thật, CLAUDE.md của nó phải chép bảng "Priority thật". Không cho override là cái sai không xảy ra được |
| Chiều ưu tiên (**số nhỏ chạy trước**) khai ở XML doc của `Order` — một chỗ duy nhất | Bất biến ① — *đã sai một lần:* color-loop có 2 entry point sort **ngược chiều nhau** trên cùng `Priority` (`ServiceInit` giảm dần, `GameManager` tăng dần) |
| `ReinitializeAsync`/`AfterReinitialize`/2 hook là `virtual` rỗng, chỉ `InitializeAsync` abstract | Bước chỉ-init-một-lần (Firebase, Ads) là ca phổ biến nhất — không ép implement nhịp không dùng (ISP) |
| Hook tên `OnAppPause`/`OnAppQuit` | §0.1 |
| `IBootstrapService` là `IOptionalService` (không có accessor `Service` throw) | Dự án không dùng SDK bootstrap vẫn hợp lệ — consumer (LiveOps §20) buộc phải viết nhánh degrade, compiler chặn (SystemPlan §0.2) |
| `IBootstrapService` chỉ **2 member**, `ProgressChanged` KHÔNG nằm trên interface | ISP theo consumer thật: LiveOps chỉ cần "xong chưa / chờ xong"; splash nằm cùng scene với runner, wire thẳng reference concrete — nhận vào một class cụ thể vẫn là "nhận vào", không cần interface |
| `BootProgress.Ratio01` là property trên struct | Splash nào cũng cần đúng phép chia này — viết một lần, đo–vẽ suy từ một nguồn (§3.8) |

- [ ] **Step 1: `IOptionalService.cs`** — nội dung khớp SystemPlan §0.2 (nguồn quyết định); Ticker khôi phục sau này **dùng lại file này**, không tạo bản thứ hai.

```csharp
namespace Horcrux.Runtime.Abstractions
{
    /// <summary>Service TUỲ CHỌN: thiếu là hợp lệ, consumer phải degrade — cố tình KHÔNG có accessor throw.</summary>
    /// <remarks>
    /// Đối ngẫu của <see cref="IService{T}"/>: bắt buộc thì thiếu phải throw sớm (lỗi cấu hình),
    /// tuỳ chọn thì thiếu phải chạy tiếp. Ép ở tầng type — không có <c>Service</c> để mà gọi,
    /// consumer không thể viết nhánh throw dù muốn.
    /// </remarks>
    public interface IOptionalService<out T>
    {
        public static bool TryGet(out T service) => Sisus.Init.Service.TryGet(out service);
    }
}
```

- [ ] **Step 2: `BootStep.cs`**

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Bootstrap
{
    /// <summary>Một bước trong chuỗi khởi tạo game. Kế thừa, implement nhịp cần, wire vào BootstrapRunner.</summary>
    /// <remarks>
    /// Logic khởi tạo đặt trong <see cref="InitializeAsync"/>, KHÔNG đặt trong <c>Awake</c> —
    /// <c>Awake</c> không có thứ tự đảm bảo giữa các object, đó chính là bài toán hệ này giải.
    ///
    /// Hook app cố tình KHÔNG trùng tên magic method của Unity (<c>OnApplicationPause</c>…):
    /// trùng tên là Unity gọi thẳng lên từng bước + runner fan-out lần nữa = chạy hai lần (plan §0.1).
    /// </remarks>
    public abstract class BootStep : MonoBehaviour
    {
        [SerializeField, Tooltip("Số NHỎ chạy trước. Trùng nhau: bước đứng trước trong list của runner chạy trước.")]
        private int order;

        /// <summary>Thứ tự chạy — SỐ NHỎ CHẠY TRƯỚC. Nguồn duy nhất là field Inspector, không override được.</summary>
        public int Order => order;

        /// <summary>Chạy MỘT lần lúc cold start. Throw ⇒ runner log rồi đi tiếp bước kế (fail-open).</summary>
        /// <param name="cancellationToken">Token vòng đời — huỷ khi có vòng init/reinit mới. Propagate xuống mọi await.</param>
        public abstract UniTask InitializeAsync(CancellationToken cancellationToken);

        /// <summary>Chạy mỗi nhịp load level, sau khi token của level trước đã bị huỷ.</summary>
        /// <param name="cancellationToken">Token vòng đời MỚI — mọi loop <c>.Forget()</c> của level nhận token này.</param>
        public virtual UniTask ReinitializeAsync(CancellationToken cancellationToken) => UniTask.CompletedTask;

        /// <summary>Pha sync chạy khi MỌI bước đã xong <see cref="ReinitializeAsync"/> — đọc state bước khác an toàn.</summary>
        public virtual void AfterReinitialize(CancellationToken cancellationToken) { }

        /// <summary>Runner gọi khi app pause/resume. Pause đi NGƯỢC thứ tự init, resume đi XUÔI.</summary>
        public virtual void OnAppPause(bool isPaused) { }

        /// <summary>Runner gọi khi app quit, theo thứ tự NGƯỢC init. Chỗ flush cuối cùng.</summary>
        public virtual void OnAppQuit() { }
    }
}
```

- [ ] **Step 3: `BootProgress.cs` + `IBootstrapService.cs`**

```csharp
// ── BootProgress.cs ───────────────────────────────────────────────────────
namespace Horcrux.Runtime.Abstractions.Bootstrap
{
    /// <summary>Tiến độ một vòng init/reinit — splash đọc <see cref="Ratio01"/> và <see cref="StepName"/>.</summary>
    public readonly struct BootProgress
    {
        /// <summary>Chỉ số bước SẮP chạy (0-based); bằng <see cref="StepCount"/> ở nhịp báo "vòng đã xong".</summary>
        public readonly int StepIndex;

        /// <summary>Tổng số bước của vòng.</summary>
        public readonly int StepCount;

        /// <summary>Tên GameObject của bước — nhãn hiển thị; rỗng ở nhịp cuối.</summary>
        public readonly string StepName;

        public BootProgress(int stepIndex, int stepCount, string stepName)
        {
            StepIndex = stepIndex;
            StepCount = stepCount;
            StepName = stepName;
        }

        /// <summary>Tiến độ [0..1] cho progress bar — phép chia viết MỘT lần ở đây, mọi splash dùng chung.</summary>
        public float Ratio01 => StepCount <= 0 ? 1f : (float)StepIndex / StepCount;

        /// <summary>Nhịp cuối của vòng (mọi bước đã chạy xong).</summary>
        public bool IsFinished => StepIndex >= StepCount;
    }
}

// ── IBootstrapService.cs ──────────────────────────────────────────────────
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Horcrux.Runtime.Abstractions.Bootstrap
{
    /// <summary>Cửa hỏi "cold boot xong chưa" cho hệ ngoài — tuỳ chọn: project không dùng SDK bootstrap vẫn hợp lệ.</summary>
    /// <remarks>
    /// <see cref="IsInitialized"/> là latch MỘT CHIỀU của cold start — Reinitialize theo level không reset nó.
    /// Consumer: LiveOps Host (SystemPlan §20) chờ init xong mới kích hoạt module.
    /// </remarks>
    public interface IBootstrapService : IOptionalService<IBootstrapService>
    {
        /// <summary>Cold start đã chạy trọn chuỗi bước chưa (bước fail-open vẫn tính là đã chạy).</summary>
        bool IsInitialized { get; }

        /// <summary>Chờ tới khi cold start xong; đã xong thì trả về ngay. Await được nhiều lần, nhiều consumer.</summary>
        /// <param name="cancellationToken">Token của CONSUMER — huỷ việc chờ, không huỷ boot.</param>
        UniTask UntilInitialized(CancellationToken cancellationToken = default);
    }
}
```

- [ ] **Step 4: Kiểm chứng** — compile sạch; `new BootProgress(0, 4, "A").Ratio01 == 0f` · `(2, 4, …) → 0.5f` · `(4, 4, "") → 1f, IsFinished == true` · `(0, 0, …) → 1f` (không chia 0).

- [ ] **Step 5: Commit** — `feat(sdk): add bootstrap contracts + IOptionalService`

---

### Task 2: `BootstrapRunner`

**Files:**
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Bootstrap/BootstrapRunner.cs`

**Interfaces:**
- Consumes: `BootStep` · `BootProgress` · `IBootstrapService` (Task 1) · `[Service]` của Sisus.Init.
- Produces: `BootstrapRunner : MonoBehaviour, IBootstrapService` — `UniTask InitializeAsync()` · `UniTask ReinitializeAsync()` · `event Action<BootProgress> ProgressChanged` · (từ interface) `IsInitialized` + `UntilInitialized(ct)`.

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| Hai vòng dùng chung **một** thân `RunStepsAsync` qua delegate static cached | Fail-open + progress + cancel là **một tri thức** — hai bản copy sẽ lệch, nên cửa hẹp gọi vào thân cửa rộng. Delegate `static` cached ⇒ không closure alloc |
| `BeginRoundAsync`: huỷ token cũ → chờ `isRoundRunning == false` → mới chạy | Bất biến ③ hai vòng không chồng (§0.4). Không await task vòng cũ vì UniTask await-một-lần (§0.3) |
| `catch (OperationCanceledException) when (token.IsCancellationRequested)` tách khỏi `catch (Exception)` | §0.4 — huỷ vòng dừng êm, lỗi thật fail-open. Filter `when` để OCE do bước tự ném sai token vẫn bị coi là lỗi thật (lộ ra, không nuốt) |
| Log thứ tự chạy ngay đầu mỗi vòng | Nghiệm thu "nhìn log kể lại thứ tự" + làm trùng-`Order`-ổn-định **nhìn thấy được** |
| `RaiseProgress` fan-out qua `GetInvocationList` + try/catch từng listener | Splash vỡ không được kéo boot chết (SystemPlan §0.4a). Alloc của `GetInvocationList` chấp nhận được: chỉ chạy lúc boot |
| `initializedSource.Task.Preserve()` cache một lần ở `Awake` | §0.3 |
| `OnDestroy`: `TrySetCanceled` + huỷ/dispose token | Consumer đang `UntilInitialized` không treo vĩnh viễn khi runner bị destroy (SystemPlan §0.6 ④) |
| `OnApplicationQuit`: fan-out hook **xong** mới huỷ token | Bước còn cần token sống để flush; huỷ trước là hook chạy trên token chết |
| Slot null trong list ⇒ `LogError` + bỏ qua, không throw | Không giấu thứ có thật, nhưng cũng không chặn cả game vì một slot trống |
| Hook app chỉ fan-out khi `IsInitialized` | Android bắn `pause(false)` ngay lúc mở app; quit được giữa cold boot — bước đang init dở nhận hook là chạy trên state nửa vời (biên "frame đầu tiên"). Token vẫn được huỷ ở quit dù chưa init xong |

**Editor setup — bước thật:**

1. Scene entry của game: tạo GameObject `[Bootstrap]` → add `BootstrapRunner`.
2. Mỗi bước của game là một GameObject con (tên = nhãn hiển thị trên splash) mang component kế thừa `BootStep`, set `Order` trong Inspector.
3. Kéo thả các bước vào list `steps` của runner.
4. Splash controller của game giữ `[SerializeField] BootstrapRunner` — kéo thả, subscribe `ProgressChanged`.

- [ ] **Step 1: `BootstrapRunner.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Bootstrap
{
    /// <summary>Con đường khởi tạo DUY NHẤT của game: sort bước, await tuần tự, fail-open, token vòng đời.</summary>
    /// <remarks>
    /// Game gọi <see cref="InitializeAsync"/> một lần lúc cold start rồi <see cref="ReinitializeAsync"/>
    /// cho level đầu và mỗi lần reload level. Runner KHÔNG tự chạy — thời điểm boot phải phối hợp
    /// với splash/consent prompt, việc đó thuộc game.
    ///
    /// Mở vòng mới khi vòng cũ đang chạy là hợp lệ: token cũ bị huỷ, vòng cũ dừng êm ở await kế
    /// tiếp, vòng mới chờ nó thoát hẳn rồi mới chạy (plan §0.4). KHÔNG gọi 2 vòng từ trong một
    /// BootStep — chờ chính mình là deadlock.
    /// </remarks>
    [Service(typeof(IBootstrapService), FindFromScene = true)]
    public sealed class BootstrapRunner : MonoBehaviour, IBootstrapService
    {
        [SerializeField, Tooltip("Mọi BootStep của game. Trùng Order: bước đứng trước chạy trước.")]
        private List<BootStep> steps = new();

        // Thứ tự chạy đã chốt (sort ổn định một lần ở Awake) — nguồn sự thật duy nhất về thứ tự.
        private readonly List<BootStep> ordered = new();

        // Hai vòng dùng chung MỘT thân RunStepsAsync; delegate static cached để không closure alloc.
        private static readonly Func<BootStep, CancellationToken, UniTask> InitializeStep =
            static (step, ct) => step.InitializeAsync(ct);
        private static readonly Func<BootStep, CancellationToken, UniTask> ReinitializeStep =
            static (step, ct) => step.ReinitializeAsync(ct);

        private readonly UniTaskCompletionSource initializedSource = new();
        private UniTask initializedTask;                 // bản Preserve() — await được nhiều lần (§0.3)
        private CancellationTokenSource lifecycleSource;
        private bool isRoundRunning;

        public bool IsInitialized { get; private set; }

        /// <summary>Bắn trước mỗi bước + một nhịp cuối khi vòng xong. Splash subscribe qua reference scene.</summary>
        public event Action<BootProgress> ProgressChanged;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            initializedTask = initializedSource.Task.Preserve();
            BuildOrderedList();
        }

        private void OnDestroy()
        {
            initializedSource.TrySetCanceled();          // consumer đang await không treo vĩnh viễn
            if (lifecycleSource == null) return;
            lifecycleSource.Cancel();
            lifecycleSource.Dispose();
            lifecycleSource = null;
        }

        public UniTask UntilInitialized(CancellationToken cancellationToken = default)
        {
            if (IsInitialized) return UniTask.CompletedTask;
            return cancellationToken.CanBeCanceled
                ? initializedTask.AttachExternalCancellation(cancellationToken)
                : initializedTask;
        }

        /// <summary>Cold start — game gọi đúng MỘT lần, trước <see cref="ReinitializeAsync"/> đầu tiên.</summary>
        public async UniTask InitializeAsync()
        {
            var token = await BeginRoundAsync("Initialize");
            try
            {
                await RunStepsAsync(InitializeStep, "Initialize", token);
                if (token.IsCancellationRequested) return;

                IsInitialized = true;                    // latch một chiều — Reinitialize không reset
                initializedSource.TrySetResult();
            }
            finally { isRoundRunning = false; }
        }

        /// <summary>Mỗi nhịp load level. Token của vòng trước bị huỷ TRƯỚC khi vòng này chạy.</summary>
        public async UniTask ReinitializeAsync()
        {
            var token = await BeginRoundAsync("Reinitialize");
            try
            {
                await RunStepsAsync(ReinitializeStep, "Reinitialize", token);
                if (token.IsCancellationRequested) return;

                // Pha 2 sync: chạy khi MỌI bước xong pha async — bước sau đọc state bước trước mới chắc đúng.
                foreach (var step in ordered)
                {
                    try { step.AfterReinitialize(token); }
                    catch (Exception e) { LogStepFailure(step, "AfterReinitialize", e); }
                }
            }
            finally { isRoundRunning = false; }
        }

        private async UniTask<CancellationToken> BeginRoundAsync(string roundName)
        {
            RefreshLifecycleToken();                     // vòng cũ thấy token huỷ ở await kế tiếp của nó
            while (isRoundRunning) await UniTask.Yield();// chờ nó thoát hẳn — hai vòng không bao giờ chồng (§0.4)
            isRoundRunning = true;
            LogRoundOrder(roundName);
            return lifecycleSource.Token;
        }

        private async UniTask RunStepsAsync(
            Func<BootStep, CancellationToken, UniTask> runStep, string phaseName, CancellationToken token)
        {
            for (int i = 0; i < ordered.Count; i++)
            {
                if (token.IsCancellationRequested) return;

                var step = ordered[i];
                RaiseProgress(new BootProgress(i, ordered.Count, step.name));
                try
                {
                    await runStep(step, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;                              // vòng mới tiếp quản — dừng êm, không phải lỗi (§0.4)
                }
                catch (Exception e)
                {
                    LogStepFailure(step, phaseName, e);  // fail-open: chơi được offline tốt hơn không mở được app
                }
            }
            RaiseProgress(new BootProgress(ordered.Count, ordered.Count, string.Empty));
        }

        private void RefreshLifecycleToken()
        {
            if (lifecycleSource != null)
            {
                lifecycleSource.Cancel();
                lifecycleSource.Dispose();
            }
            lifecycleSource = new CancellationTokenSource();
        }

        // Unity gọi magic method này trên runner — nơi DUY NHẤT trong hệ nhận nó (§0.1).
        // Pause đi NGƯỢC (hệ trên flush trước khi hệ nền dọn), resume đi XUÔI như init.
        private void OnApplicationPause(bool isPaused)
        {
            if (!IsInitialized) return;              // Android bắn pause(false) ngay lúc mở app — bước chưa init xong không được nhận hook

            if (isPaused)
                for (int i = ordered.Count - 1; i >= 0; i--) SafePause(ordered[i], true);
            else
                for (int i = 0; i < ordered.Count; i++) SafePause(ordered[i], false);
        }

        private void OnApplicationQuit()
        {
            if (IsInitialized)                           // quit giữa cold boot: không flush bước đang init dở
            {
                for (int i = ordered.Count - 1; i >= 0; i--)
                {
                    try { ordered[i].OnAppQuit(); }
                    catch (Exception e) { LogStepFailure(ordered[i], "OnAppQuit", e); }
                }
            }
            lifecycleSource?.Cancel();                   // hook chạy XONG mới huỷ — bước còn cần token để flush
        }

        private void SafePause(BootStep step, bool isPaused)
        {
            try { step.OnAppPause(isPaused); }
            catch (Exception e) { LogStepFailure(step, "OnAppPause", e); }
        }

        private void BuildOrderedList()
        {
            var indexed = new List<(BootStep step, int index)>(steps.Count);
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] == null)
                {
                    Debug.LogError($"[Bootstrap] Slot {i} trong danh sách bước đang trống — gán bước hoặc xoá slot.", this);
                    continue;
                }
                indexed.Add((steps[i], i));
            }

            // List.Sort không stable (§0.2) ⇒ khoá kép (Order, index gốc): trùng Order thì giữ thứ tự Inspector.
            indexed.Sort(static (a, b) => a.step.Order != b.step.Order
                ? a.step.Order.CompareTo(b.step.Order)   // SỐ NHỎ CHẠY TRƯỚC — chiều khai ở BootStep.Order
                : a.index.CompareTo(b.index));

            ordered.Clear();
            foreach (var (step, _) in indexed) ordered.Add(step);
        }

        private void RaiseProgress(in BootProgress progress)
        {
            var handlers = ProgressChanged;
            if (handlers == null) return;

            // Cô lập lỗi từng listener — splash vỡ không được kéo boot chết theo (SystemPlan §0.4a).
            foreach (Action<BootProgress> handler in handlers.GetInvocationList())
            {
                try { handler(progress); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
        }

        private void LogRoundOrder(string roundName)
        {
            var builder = new StringBuilder("[Bootstrap] ").Append(roundName).Append(" order: ");
            for (int i = 0; i < ordered.Count; i++)
            {
                if (i > 0) builder.Append(" → ");
                builder.Append(ordered[i].name).Append('(').Append(ordered[i].Order).Append(')');
            }
            Debug.Log(builder.ToString(), this);
        }

        private void LogStepFailure(BootStep step, string phaseName, Exception exception)
        {
            Debug.LogError($"[Bootstrap] Bước '{step.name}' lỗi ở {phaseName} — bỏ qua, đi tiếp (fail-open).", step);
            Debug.LogException(exception, step);
        }
    }
}
```

- [ ] **Step 2: Kiểm chứng** (bảng input → kỳ vọng; chưa kèm code test — Task 3 nghiệm thu bằng demo + log):

| Input | Kỳ vọng |
|---|---|
| List `[A(0), B(10), C(0)]` | log `Initialize order: A(0) → C(0) → B(10)`, mọi lần chạy y hệt |
| Bước B throw trong `InitializeAsync` | log lỗi nêu tên B, C vẫn chạy, `IsInitialized == true`, splash nhận nhịp cuối |
| `ReinitializeAsync()` gọi khi vòng trước đang chạy | vòng trước dừng êm (không log lỗi), vòng sau chạy trọn, không có bước nào chạy chồng |
| `UntilInitialized()` gọi từ 2 consumer, trước VÀ sau khi init xong | cả hai return đúng, không exception await-twice |
| `UntilInitialized(ct)` với ct huỷ giữa chừng | consumer nhận cancel, boot **không** bị ảnh hưởng |
| Destroy runner khi đang có consumer chờ | consumer nhận cancel, không treo |
| Slot null trong `steps` | `LogError` chỉ đúng slot đó, các bước còn lại chạy bình thường |
| Pause/quit bắn TRƯỚC khi cold boot xong | không bước nào nhận hook; quit vẫn huỷ token |

- [ ] **Step 3: Commit** — `feat(sdk): add BootstrapRunner (single boot path, fail-open, lifecycle token)`

---

### Task 3: Demo + nghiệm thu chơi thử

**Files:**
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Bootstrap/Demo/DemoBootStep.cs`
- Create: `Assets/Horcrux/Runtime/Implementations/Foundations/Bootstrap/Demo/DemoBootDriver.cs`
- Scene demo (Editor setup dưới) — không commit vào SDK nếu project có quy ước riêng về scene demo.

**Interfaces:**
- Consumes: `BootStep` · `BootstrapRunner` · `BootProgress` (Task 1–2).
- Produces: chỉ demo — không hệ nào phụ thuộc.

- [ ] **Step 1: `DemoBootStep.cs`**

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Bootstrap.Demo
{
    /// <summary>Bước giả lập để nghiệm thu runner bằng log — không dùng trong game thật.</summary>
    public sealed class DemoBootStep : BootStep
    {
        [SerializeField, Min(0f), Tooltip("Giả lập thời gian một bước init thật (giây).")]
        private float workSeconds = 0.3f;

        [SerializeField, Tooltip("Bật để kiểm fail-open: bước này throw, boot vẫn phải đi tiếp.")]
        private bool throwOnInitialize;

        public override async UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(workSeconds), DelayType.Realtime,
                cancellationToken: cancellationToken);
            if (throwOnInitialize)
                throw new InvalidOperationException($"'{name}' cố ý throw để kiểm fail-open.");
            Debug.Log($"[DemoBootStep] '{name}' Initialize xong.", this);
        }

        public override async UniTask ReinitializeAsync(CancellationToken cancellationToken)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(workSeconds), DelayType.Realtime,
                cancellationToken: cancellationToken);
            Debug.Log($"[DemoBootStep] '{name}' Reinitialize xong.", this);
        }

        public override void AfterReinitialize(CancellationToken cancellationToken)
            => Debug.Log($"[DemoBootStep] '{name}' AfterReinitialize.", this);

        public override void OnAppPause(bool isPaused)
            => Debug.Log($"[DemoBootStep] '{name}' OnAppPause({isPaused}).", this);

        public override void OnAppQuit()
            => Debug.Log($"[DemoBootStep] '{name}' OnAppQuit.", this);
    }
}
```

- [ ] **Step 2: `DemoBootDriver.cs`**

```csharp
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Bootstrap.Demo
{
    /// <summary>Driver demo: cold start + một nhịp Reinitialize, in progress đúng cách splash sẽ làm.</summary>
    /// <remarks><c>.Forget()</c> ở đây an toàn: exception bước đã bị runner nuốt có log (fail-open),
    /// exception còn lại UniTask tự log qua unobserved handler.</remarks>
    public sealed class DemoBootDriver : MonoBehaviour
    {
        [SerializeField] private BootstrapRunner runner;

        private void Start() => RunDemoAsync().Forget();

        private void OnDestroy()
        {
            if (runner != null) runner.ProgressChanged -= LogProgress;
        }

        private async UniTaskVoid RunDemoAsync()
        {
            runner.ProgressChanged += LogProgress;

            await runner.InitializeAsync();
            Debug.Log($"[Demo] IsInitialized = {runner.IsInitialized}");

            await runner.ReinitializeAsync();
            Debug.Log("[Demo] Xong nhịp reinit đầu. Chuột phải component này để chạy các ca kiểm còn lại.");
        }

        [ContextMenu("Reinitialize")]
        private void ReinitializeFromMenu() => runner.ReinitializeAsync().Forget();

        [ContextMenu("Reinitialize x2 (kiểm hai vòng không chồng)")]
        private void ReinitializeTwice()
        {
            runner.ReinitializeAsync().Forget();
            runner.ReinitializeAsync().Forget();   // vòng 1 phải dừng êm, vòng 2 chạy trọn
        }

        private void LogProgress(BootProgress progress)
            => Debug.Log($"[Demo] progress {progress.Ratio01:P0} — {(progress.IsFinished ? "xong" : progress.StepName)}");
    }
}
```

- [ ] **Step 3: Editor setup scene demo** (bước thật):

1. Scene mới `BootstrapDemo` → GameObject `[Bootstrap]` + `BootstrapRunner`.
2. 4 GameObject con: `Save(order 0)` · `RemoteConfig(order 10, throwOnInitialize ✓)` · `Ads(order 10)` · `Audio(order 0)` — mỗi cái một `DemoBootStep`, tên và `order` đúng như ghi. Kéo cả 4 vào `steps` **theo thứ tự hierarchy trên**.
3. GameObject `[Demo]` + `DemoBootDriver`, kéo runner vào.

- [ ] **Step 4: Kịch bản chơi thử** (nghiệm thu này cần Play mode, developer chạy):

| Mục | Nội dung |
|---|---|
| Vào đâu | Scene `BootstrapDemo`, bấm Play |
| Làm gì | ① nhìn log boot · ② dừng Play, Play lại lần nữa · ③ chuột phải driver → "Reinitialize x2" · ④ bấm nút Pause của Editor rồi bỏ |
| Nhìn cái gì | ① `Initialize order: Save(0) → Audio(0) → RemoteConfig(10) → Ads(10)` — trùng 0 và trùng 10 đều theo thứ tự kéo thả; lỗi đỏ nêu tên `RemoteConfig` kèm chữ "fail-open"; sau đó `Ads` vẫn init; progress lên tới 100%; `IsInitialized = True` · ② thứ tự y hệt lần trước · ③ đúng MỘT chuỗi reinit chạy trọn, không log lỗi từ vòng bị huỷ, không bước nào in chồng nhau · ④ mỗi bước in `OnAppPause(True)` đúng **một lần**, theo thứ tự ngược (`Ads` trước `Save`) |
| Khác trước ra sao | So bản color-loop: một bước throw là treo splash; ở đây game vẫn vào được |
| Dấu hiệu hỏng | `OnAppPause` in 2 lần một bước (§0.1 tái phát) · thứ tự đổi giữa 2 lần Play (§0.2) · vòng reinit x2 in xen kẽ 2 chuỗi (bất biến ③ vỡ) · lỗi `RemoteConfig` làm `Ads` không chạy (fail-open vỡ) |

- [ ] **Step 5: Commit** — `feat(sdk): add bootstrap demo + acceptance scene`

> `SystemPlan.md` đã được cập nhật cùng lần viết plan này (bảng "Hệ đã có plan chi tiết", 📄 hàng 1, dòng `IOptionalService` ở §0.2) — không còn việc tài liệu nào trong task.

---

## Ghi chú thực thi

- **Nghiệm thu cuối = kịch bản Task 3 Step 4** — map 1-1 với 4 mục Nghiệm thu của SystemPlan mục 1. Ba mục đầu quan sát bằng log trong Play mode; riêng "reinit không rò task" nhìn qua: sau "Reinitialize x2" không còn log nào của vòng bị huỷ xuất hiện muộn.
- **Sau khi implement xong:** viết `Bootstrap.md` (tài liệu thiết kế §5.1) cạnh `Implementations/Foundations/Bootstrap/` — điều kiện ⑤ của "Xong" (SystemPlan §0.6). Chuyển 2 dòng "đã sai một lần" (§0.1, §0.2 của plan này) vào mục quyết định thiết kế của nó.
- **Hệ dùng tiếp:** mọi hệ Tầng 1 còn lại là ứng viên `BootStep` ở phía game (Persistence flush ở `OnAppPause(true)`/`OnAppQuit`, Remote Config fetch ở `InitializeAsync`…). SDK không tự wire — game quyết bước nào tồn tại.
- **Ticker (khôi phục sau):** Task 1 của nó trùng file `IOptionalService.cs` — dùng file của plan này, bỏ task trùng.
- **Mở rộng sau** (đều additive, không đổi chữ ký đang có): manifest SO — overload `InitializeAsync(BootManifest)` · parallel-in-phase — cờ `AllowParallel` trên `BootStep` + `WhenAll` nhóm cùng `Order` · nhóm bước theo scene · expose `CancellationToken` vòng đời thành property khi có consumer ngoài bước đầu tiên.
