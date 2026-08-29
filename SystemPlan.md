# Horcrux SDK — SystemPlan (tài liệu tư duy trước khi phát triển)

> **Phạm vi:** chỉ **runtime**. Editor tooling nằm ngoài SDK (Phụ lục C).
> **Đã có trong SDK, không làm lại:** Object Pooling · EventBus · Remote Config · Tweening/Easing · PhysXHelper · MonoSingleton/SingletonSO · `IService<T>`.

## Ngữ cảnh đã chốt (2026-08-29)

- **Người dùng tài liệu:** agent — đọc đúng mục của hệ cần làm rồi sinh plan chi tiết (MY_SKILL §5.3) · developer — đọc để quyết thứ tự làm, và tự code lại theo plan để nắm logic hệ.
- **Mục tiêu:** SDK mang qua mọi dự án puzzle Unity. Mặt trận hiện tại là **Tầng 1** — phát triển chắc Tầng 1 trước, các tầng khác chưa động.
- **Hai vùng của tài liệu:** **TẦNG 1 đã polish** ở mức tư tưởng thiết kế — khảo sát re-verify trực tiếp trên code 4 dự án (`color-loop` · `foods_jam` · `Goods-Jam` · `water-flow`) ngày 2026-08-29, không chứa contract C# (contract thuộc plan chi tiết từng hệ). **TẦNG 2–4 + Phụ lục giữ nguyên trạng** bản trước (khuôn 8 phần cũ, có contract C#), chưa re-verify — polish tầng nào thì re-verify tầng đó cùng cách.
- **Quyết định user đã chốt (MY_SKILL §5.4 — chỉ user đổi):** Stack State Machine **ở Tầng 1** dù re-verify cho thấy 1/4 repo có và bản đó không caller runtime — đổi lại, nghiệm thu buộc có caller thật · #4 tách đôi: **4a Ticker ở Tầng 1** (phụ thuộc bắt buộc của Audio/Haptics/Feedback/Combo), **4b Time Service xuống Tầng 3** làm ngay trước Economy (#14) · Safe-Area **tự viết + banner inset**, không wrap NotchSolution · Bootstrap v1 **không** data-driven · Save v1 **không** cloud/crypto · Scene Flow v1 **không** `ITransition` — ba thứ này đều để "Mở rộng sau" với hình dạng đã nghĩ sẵn.
- **ID hệ giữ nguyên vĩnh viễn.** Tham chiếu `§4` trong vùng nguyên trạng (Tầng 2–4): nói về *nhịp tick* → đọc **4a**; nói về *giờ/lịch/countdown/offline* → đọc **4b**.

## Cách đọc

| Bước | Đọc gì | Vì sao |
|---|---|---|
| 1 | **§0** — nguyên tắc chung | Quy ước DI, lưu trữ, zero-GC, hình dạng API được định nghĩa **một lần** ở đây; các mục sau chỉ trỏ về, không giải lại |
| 2 | **Bảng tổng quan** + đồ thị phụ thuộc | Biết hệ đang cần nằm ở tầng nào, phụ thuộc gì, làm sau cái gì |
| 3 | **Mục của hệ cần làm** | Tự chứa — đủ để viết plan chi tiết, không cần nguồn khác |
| 4 | Chỉ khi hệ có toán | §19 dẫn giải từ trực giác → công thức, đọc tuần tự §19.1→§19.7 |

**Khuôn mục theo vùng.** Tầng 1 (đã polish): `Bài toán` → `Use case` → `Tư tưởng cốt lõi` → `Ranh giới SDK/game` → `Phạm vi v1` (gồm gì · cố ý không · mở rộng sau) → `Nghiệm thu` → `Khảo sát (re-verify)` → `Cạm bẫy`. Tầng 2–4 (nguyên trạng): `Bài toán` → `Use case` → `Mô hình` → `Contract` → `Luồng` → `Quyết định thiết kế` → `Cạm bẫy` → `Xong khi` — code trong vùng này là *contract* (chữ ký + ràng buộc kiểu, thân để trống), không phải implementation.

---

# §0 — Nguyên tắc chung (định nghĩa 1 lần, các hệ sau chỉ trỏ về đây)

## 0.1 Ranh giới SDK ↔ game

| SDK sở hữu | Game sở hữu |
|---|---|
| **Khung** (lifecycle, queue, stack, state machine) | **Nội dung** (clip nào, popup nào, event nào) |
| **Contract** (interface, abstract, struct DTO) | **Impl vendor** (MAX, Firebase, Adjust, NiceVibrations) |
| **Thuật toán generic** (rating math, distribution, pacing rule) | **Mapping vào mechanic** (rating → số moves, màu, item) |
| **Vocabulary trung tính** (`Light/Medium/Heavy/Success`) | **Ngữ nghĩa game** (`Grind`, `PickBox`, `OrderCompleted`) |

**Luật kiểm tra nhanh:** nếu một type trong SDK cần biết tên/enum riêng của một game → sai chỗ. Đẩy nó ra sau một interface do game implement.

## 0.2 Quy ước DI

```csharp
// Đăng ký: implementation tự khai báo, không có bảng đăng ký trung tâm
[Service(typeof(IAudioService), FindFromScene = true, LazyInit = true)]
sealed class AudioService : MonoBehaviour, IAudioService { }

// Tiêu thụ (ưu tiên): inject qua constructor-style
sealed class BarCoin : MonoBehaviour<ICurrencyService, ITicker>
{
    protected override void Init(ICurrencyService currency, ITicker ticker) { … }
}

// Tiêu thụ (chấp nhận cho service tra cứu chéo rời rạc): static accessor trên interface
ICurrencyService.Service.Add(100, Placements.LevelWin);   // const string, không gõ tay (§0.4b)
```

**Bắt buộc vs tuỳ chọn — phân biệt ở tầng type, không ở tài liệu.** Hai loại có nhu cầu ngược nhau: service bắt buộc thiếu là lỗi cấu hình (**phải** throw sớm để lộ ra), service tuỳ chọn thiếu là hợp lệ (**phải** degrade, không throw). Cách ép: **service tuỳ chọn không có accessor throw để mà gọi.**

```csharp
namespace Horcrux.Runtime.Abstractions
{
    // Đã có: Foundations/IService.cs — service BẮT BUỘC. Có cả 2 accessor.
    public interface IService<out T>
    {
        public static T Service => Sisus.Init.Service.Get<T>();                 // thiếu → throw
        public static bool TryGet(out T service) => Sisus.Init.Service.TryGet(out service);
    }

    // Thêm mới: service TUỲ CHỌN — cố tình KHÔNG có `Service`.
    // 📄 Đã có plan: TickerSystem.md Task 1 (file Abstractions/Foundations/IOptionalService.cs).
    public interface IOptionalService<out T>
    {
        public static bool TryGet(out T service) => Sisus.Init.Service.TryGet(out service);
    }
}
```

| Quyết định | Vì sao |
|---|---|
| Optional **không** khai `Service` | Consumer không thể viết nhánh throw dù muốn — compiler chặn, không phải code-review chặn |
| Không làm `Service => TryGet(…) ? s : default` | Trả `null` im lặng còn tệ hơn throw: NRE nổ ở chỗ khác, xa nguyên nhân |
| Không dùng `IService<T>` cho cả hai rồi ghi chú | Ghi chú không ép được gì; đây là điều kiện tiên quyết để module cắm-rút (§20) chạy được khi thiếu service |

## 0.3 Quy ước lưu trữ

Mọi hệ có state đều lưu qua **save-unit riêng** của mình (§2), **không** dùng chung một blob toàn cục. Hệ chỉ khai báo model + implement `ISaveUnit`; registry lo dirty/autosave/crypto.

## 0.4 Hiệu năng — luật áp cho mọi hệ dưới đây

**(a) Hành vi runtime**

| Luật | Cụ thể |
|---|---|
| Không allocate trong hot path | Không `new` ref-type, LINQ, closure, string-concat trong `Update`/vòng lặp/handler tick |
| Buffer tái dùng | `List<T>` field + `.Clear()`, không tạo list mới mỗi lần |
| 1 nguồn tick | Mọi listener bám **một** `Update` trung tâm (§4), không mỗi component một `Update` |
| Event-driven thay polling | Dirty flag + gộp cuối frame; tính lại chỉ khi nguồn đổi |
| Cache thay tính lại | Kết quả không đổi giữa 2 lần gọi thì lưu field, không gọi `GetComponent`/sort/format lại |
| Try/catch quanh **từng** callback | 1 listener lỗi không kill listener còn lại |
| Track Addressables handle | Lưu handle → `Release()` khi pop/destroy (§A.2) |

**(b) Hình dạng API — quyết định lúc thiết kế contract, sửa sau là breaking change**

| Luật | Không dùng | Dùng | Vì sao |
|---|---|---|---|
| Tham số collection | `IEnumerable<T>` | `IReadOnlyList<T>`, hoặc `ReadOnlySpan<T>` khi hàm sync | `foreach` qua interface cấp phát enumerator; với `T` là struct thì boxing thêm mỗi phần tử. `Span` không đi qua `await` được ⇒ API async lấy `IReadOnlyList<T>` |
| Buffer đầu ra | trả `List<T>`/`T[]` mới | caller cấp `List<T>`, hàm `Clear()` rồi ghi vào | Không alloc theo số lần gọi; caller sở hữu buffer nên tái dùng được |
| Khối bytes | `byte[]` | `ReadOnlyMemory<byte>` (vào) · `IBufferWriter<byte>` (ra) | Cho phép buffer pool; `byte[]` bắt buộc copy + alloc mỗi lần |
| DTO | `class` | `readonly struct` + truyền `in` | Event/config/result sống rất ngắn; `in` tránh copy khi ≥16 byte |
| Callback tần số cao (mỗi frame/giây) | `event Action<T>` | `Register(IListener)` + `List<IListener>` | Mỗi `+=`/`-=` cấp phát mảng invocation-list mới; đăng ký/huỷ liên tục (UI mở-đóng) là hot path thật. Gọi qua interface không alloc |
| Callback thưa (đổi setting, hết event) | — | `event Action` | Đủ rẻ, và tiện hơn hẳn |
| Khoá logic ở call-site | `string` | `enum`/`readonly struct` wrap `int` | Typo chỉ nổ lúc runtime, không refactor-rename được |
| **Định danh ổn định ra ngoài** | — | `string` là **đúng** | save key, remote-config key, tên event analytics, placement id, module id: phải khớp với server/dashboard/asset ⇒ đổi tên type không được đổi khoá. Khai bằng `const string`, không gõ tay ở call-site |

## 0.5 Phân loại Foundation / Composite

Theo `MY_SKILL.md` §3.2 (hệ độc lập / hệ kết hợp): **Foundation** = chạy độc lập, port sang dự án khác không cần hệ nào khác trong SDK. **Composite** = ghép từ 2+ Foundation. Cột "Loại" ở bảng dưới đã phân sẵn — dùng nó để đặt folder đúng nhánh `Abstractions/{Foundations|Composites}/` + `Implementations/{Foundations|Composites}/`.

## 0.6 "Xong" nghĩa là gì

Mỗi hệ chỉ coi là xong khi đủ 5 điều: ① contract tách khỏi impl · ② không có type game-specific rò vào SDK · ③ zero-GC ở hot path đã kiểm · ④ huỷ sạch (`CancellationToken` + `Release()` handle + unsubscribe) · ⑤ có file `.md` cạnh `Implementations/` theo `MY_SKILL.md` §5.

## 0.7 Ba idiom dùng lại ở nhiều hệ — định nghĩa ở đây, nơi khác chỉ trỏ về

| Idiom | Luật | Định nghĩa đầy đủ |
|---|---|---|
| **Ref-count + scope** | Nhiều nguồn cùng yêu cầu một trạng thái bật/tắt (chặn input §3, rung liên tục §9) ⇒ đếm tham chiếu, **không** dùng `bool`: nguồn đầu tiên nhả sẽ tắt sớm. Luôn kèm `IDisposable …Scope()` để `try/finally` không rò | §3 `IInteractionBlocker` |
| **Thời gian = UTC qua `ITimeService`** | Không hệ nào được gọi `DateTime.Now`/`Time.time` để tính logic. Lưu `long` Unix-UTC; đổi sang local **chỉ** ở tầng hiển thị. Ngoại lệ duy nhất có chủ ý: Day Active (§21) | §4 |
| **Dirty + gộp cuối frame** | Nguồn đổi → `MarkDirty()`; tính lại một lần ở `PlayerLoopTiming.LastUpdate`. N lần đổi trong 1 frame = 1 lần tính | §11b (cây badge), §2 (autosave) |

---

# Bảng tổng quan

| # | Hệ thống | Loại | Phổ quát | Tầng | Phụ thuộc SDK |
|---|---|---|---|:--:|---|
| 1 | Bootstrap & Lifecycle | Foundation | Bắt buộc | 1 | — |
| 2 | Persistence (save-unit + cloud) | Foundation | Bắt buộc | 1 | — |
| 3 | Scene Flow & Loading | Foundation | Bắt buộc | 1 | Pooling *(tuỳ chọn: warm-up)* |
| 4a | Ticker (nguồn tick trung tâm) | Foundation | Bắt buộc | 1 | — |
| 4b | Time Service (server time + countdown) | Foundation | Bắt buộc | 3 | 4a *(nhịp)* |
| 5 | Stack State Machine | Foundation | Cao | 1 | — |
| 6 | Safe-Area & Responsive Canvas | Foundation | Bắt buộc | 1 | — |
| 7 | **UI Navigator** (Page/Popup/Sheet) | Composite | Bắt buộc | 2 | Pooling, Tweening |
| 8 | Audio (music/SFX) | Composite | Bắt buộc | 2 | Pooling, §2 |
| 9 | Haptics | Foundation | Cao | 2 | §2 *(qua interface)* |
| 10 | Interactive Button | Composite | Cao | 2 | §8, §9, §12 *(tất cả optional)* |
| 11 | Toast & Notification Badge | Composite | Bắt buộc | 2 | §7, Pooling, EventBus |
| 12 | Analytics (contract + taxonomy) | Foundation | Bắt buộc | 2 | — |
| 13 | Monetization Boundary | Foundation | Bắt buộc | 2 | — |
| 14 | Economy (Currency/Lives/Reward) | Composite | Bắt buộc | 3 | §2, 4a, 4b, §7, Tweening |
| 15 | Level Library (runtime) | Composite | Bắt buộc | 3 | §2, RemoteConfig |
| 16 | Tutorial / FTUE | Composite | Bắt buộc | 3 | §7, Tweening, RemoteConfig |
| 17 | Tab Navigation / Scroll-Snap | Composite | Trung bình | 3 | Tweening |
| 18 | In-Game Rating | Composite | Cao | 3 | §7, §2 |
| 19 | 💎 Adaptive Difficulty (Glicko-2) | Foundation *(lõi toán)* + Composite *(áp dụng)* | Thấp — **IP cao** | 4 | §2, 4b, §15 |
| 20 | 💎 LiveOps Module Host | Composite | Bắt buộc | 4 | §2, 4b, §7, §12, §13, §14 |
| 21 | 💎 Ads Pacing & Monetization Scenario | Composite | Thấp — **port dễ** | 4 | §13, 4b, RemoteConfig |
| 22 | **Feedback Orchestrator** (cue → đa giác quan) 📄 | Composite | Cao | 3 | 4a, §8, §9 *(2 sau là optional)* |
| 23 | **Combo** (streak · tier · multiplier) 📄 | Composite | Trung bình | 4 | 4a · §22 *(optional)* |

💎 = giá trị IP cao, đáng nhân rộng dù ít nơi dùng. 📄 = **đã có plan triển khai chi tiết**, xem bảng dưới.

## Hệ đã có plan chi tiết

Năm hệ dưới đây đã được viết plan theo `MY_SKILL.md` §5.3 (tự chứa, có code dán-được). Plan cố ý **thu hẹp phạm vi chỉ đủ cho Combo** — phần còn lại của mục tương ứng trong tài liệu này **vẫn còn hiệu lực** và chưa được lên plan.

| Hệ | File plan | Trong plan | **Ngoài** plan (vẫn ở tài liệu này) |
|---|---|---|---|
| 4a Ticker (+4b Time) | `Implementations/Foundations/Ticker/TickerSystem.md` | `ITicker` + **2 nhịp** (`ITickable`, `IPauseAware`) + 1 `Update` duy nhất, `IOptionalService<T>` (§0.2), `DeferredList<T>`. 6 file | **nhịp 1 Hz** (`ISecondTickable`), `Destroyed` event, `ITimeService`, chống tua giờ, `Countdown`, `TimeFormatter` |
| §9 Haptics | `Implementations/Foundations/Haptics/HapticSystem.md` | `PlayCustom(HapticPattern)` + `IHapticBackend` (**2 member**) + backend Android có **biên độ**. 6 file | **bộ preset** (`EHapticPreset` + `Play(preset)`), rung liên tục (`Begin/End` + ref-count), waveform, impl vendor, `IHapticSettings` |
| §8 Audio | `Implementations/Foundations/Audio/AudioSystem.md` | ⚠️ **SFX 2D + `pitchScale`** (xem cảnh báo ở §8), catalog SO, throttle theo clip, voice gán ở Inspector. 6 file | **SFX 3D** (`PlaySfxAt`), music + crossfade, `PauseAll/ResumeAll`, `EAudioSelectMode`, `IAudioSettings`, mixer group |
| §22 Feedback | `Implementations/Composites/Feedback/FeedbackSystem.md` | `FeedbackCue {Id, Step}` + dispatcher + 4 kênh (audio · haptic · hitstop · **shake**); **kèm** `TraumaShake`. Tham số cue serialize **trên chính kênh** — không asset trung gian. 12 file | zoom punch (thêm `IFeedbackCameraZoom` **riêng** theo ISP — không sửa interface cũ), `FeedbackCue.Intensity`, bảng cue dạng SO, kênh particle/text/ripple, slow-mo dài, kênh thêm lúc runtime |
| §23 Combo | `Implementations/Composites/Combo/ComboSystem.md` | `ComboTracker` (C# thuần), 3 window policy, hệ số nhân Linear, bậc = `int[]` trên Inspector, bridge, `ComboMeter`, demo driver. 11 file | nhãn/hệ số/cue riêng theo bậc (thay `int[]` bằng SO + interface), đường nhân điểm khác, multi-track, lưu kỷ lục, telemetry, `ChainReaction` |

> ⚠️ **2/5 file plan đã bị xoá trong commit `clean` của repo Horcrux** — `TickerSystem.md` và
> `FeedbackSystem.md` (3 plan còn trên đĩa: Haptics, Audio, Combo). Nội dung khôi phục được từ git
> (`git show 245adb7^:Runtime/Implementations/Foundations/Ticker/TickerSystem.md` và tương tự cho
> Feedback); khôi phục hoặc viết lại khi bắt đầu hệ tương ứng, đối chiếu với mục 4a/§22.
>
> **Nguyên tắc phạm vi của 5 plan này** — luật `MY_SKILL.md` NT6 *"xóa nó đi thì hỏng ở đâu"*: mọi mục ở cột "ngoài plan" đều **không gọi được tên chỗ hỏng** ở bản đầu, và thêm lại đều **additive** (thêm file / method / interface mới, hoặc đổi ctor nội bộ). Không mục nào đòi đổi chữ ký **public** đang có.
>
> Chỉ **một** chỗ cố ý phòng xa vì sửa sau là breaking thật: `PlaySfx(…, pitchScale)` — thêm tham số sau nghĩa là sửa mọi call-site. Hai chỗ từng phòng xa nhưng đã bỏ vì tìm được cách tốt hơn: `FeedbackCue.Intensity` (`Step` đã đủ) và `IFeedbackCamera.ApplyZoom` (dùng interface thứ hai theo ISP thì không breaking implementer nào).

## Thứ tự phụ thuộc

```
TẦNG 1  ┌─ 1 Bootstrap ── 2 Persistence ── 3 SceneFlow ── 4a Ticker ── 5 StateMachine ── 6 SafeArea
        │      (nhỏ, zero-coupling, ROI cao nhất → làm trước)
        ▼
TẦNG 2  ┌─ 7 UI Navigator ◄── 8 Audio ── 9 Haptics ── 12 Analytics ── 13 Monet Boundary
        │        └─► 10 Button ── 11 Toast/Badge
        ▼
TẦNG 3  ┌─ 4b Time ── 14 Economy ── 15 Level Library ── 16 Tutorial ── 17 TabNav ── 18 Rating
        │  └─ 22 Feedback Orchestrator ◄── 4a (bắt buộc) + §8, §9 (optional)
        ▼
TẦNG 4  └─ 19 Adaptive Difficulty ── 20 LiveOps Host ── 21 Ads Pacing
        │  (tiêu thụ hầu hết tầng dưới qua interface → làm cuối)
        └─ 23 Combo ◄── 4a (bắt buộc) + §22 (optional)
```

**Lát cắt dọc "combo đa giác quan"** — 5 hệ đã có plan, chạy được độc lập với 21 hệ còn lại:

```
4a Ticker ──┬─► §9 Haptics ─┐
            └─► §8 Audio ───┴─► §22 Feedback ──► §23 Combo
```

**Đọc bảng này trước khi bắt đầu bất kỳ hệ nào.** Làm ngược thứ tự = phải quay lại sửa nền.

---

# TẦNG 1 — Foundation

Sáu mục dưới đây theo khuôn tư tưởng (xem "Cách đọc"). Nhãn *bản chất* của mỗi hệ nói thẳng nó là
**extract** (đã có bản chạy tốt để chưng cất) hay **thiết kế mới** (use case thật nhưng chưa nơi nào làm
đúng) — plan chi tiết sau này đừng đi tìm "bản gốc" không tồn tại.

**Hiện trạng Horcrux (re-verify 2026-08-29):** cả 6 chủ đề Tầng 1 **chưa có gì trong SDK** — chỉ có
`IService<T>` (wrapper mỏng trên Sisus.Init) và `MonoSingleton`. Không có nguy cơ viết trùng.

## 1. Bootstrap & Lifecycle — `Foundation` · *extract + làm sạch*

**Bài toán.** Unity không đảm bảo thứ tự `Awake()` giữa các object, nhưng khởi tạo game **có thứ tự bắt
buộc**: load save → fetch remote config → init ads/audio → vào màn đầu. Sai thứ tự = crash hoặc sai logic.
Bằng chứng 4 repo còn cho thấy vấn đề thứ hai: **không repo nào có MỘT con đường khởi tạo** — color-loop
có 3 điểm init rời rạc, foods_jam init IAP đồng bộ *trước* chuỗi async tạo ràng buộc thứ tự ẩn. Cần một
bộ điều phối **chủ động, duy nhất**, không phó mặc Unity.

**Use case.**
- Cold start: chạy tuần tự N bước async, splash chờ tới khi xong; một bước throw → không được treo splash mãi.
- Vào lại scene/level mới: `Reinitialize` toàn bộ bước theo level mới, **huỷ mọi loop async của level trước**.
- `OnApplicationPause` / `OnApplicationQuit`: hook dọn dẹp theo **thứ tự ngược**.
- Hệ ngoài cần biết "init xong chưa" để bám vào (LiveOps Host §20 cần đúng cái này).

**Tư tưởng cốt lõi.**
- Contract bước init **nhỏ**: thứ tự (`Order`) + `InitializeAsync(ct)`; vòng đời hai nhịp — init một lần
  lúc boot, reinit mỗi nhịp load level; hook pause/quit. Runner chỉ biết contract, không biết bước làm gì.
- **Reinit hai pha**: pha async tuần tự → pha sync "after" chạy khi *mọi* bước đã reinit xong — bước sau
  đọc state của bước trước mới chắc đúng.
- **Token theo vòng đời**: runner cấp `CancellationToken`, refresh (cancel cũ, tạo mới) mỗi vòng init —
  mọi loop `.Forget()` của các hệ nhận token này, reload là huỷ sạch.
- **Fail-open**: bước throw → log rõ + đi tiếp, không treo splash (chơi được offline tốt hơn không mở được
  app); tuyệt đối không `.Forget()` trần nuốt exception.
- Runner phát **phase event** (WaitForNetwork/Services/PlayerData/Content/Finished) cho splash hiển thị,
  và một service *tuỳ chọn* cho hệ ngoài đăng ký callback "sau init" (`IOptionalService` — §0.2).
- **Bất biến (MY_SKILL §3.8):** ① chiều ưu tiên định nghĩa ở **đúng một chỗ**, ghi rõ số nhỏ hay số lớn
  chạy trước — *đã sai một lần:* color-loop có 2 entry point sort **ngược chiều nhau** trên cùng
  `BaseManager.Priority`. ② hai bước trùng `Order` phải có thứ tự **xác định** (sort ổn định hoặc cấm
  trùng kèm assert) — *đã sai một lần:* color-loop dùng `List.Sort` không stable.

**Ranh giới SDK / game.**

| SDK sở hữu | Game điền |
|---|---|
| Contract bước init + runner (sort, await, token, phase event) | Các bước/manager cụ thể |
| Service "đã init xong" (optional) | Giá trị `Order` + wire danh sách bước trong scene/Inspector |

**Phạm vi v1.**
- Contract + runner một entry point + phase event + token vòng đời + hook pause/quit duyệt ngược.
- **Cố ý KHÔNG làm ở v1:** *manifest data-driven (SO)* — bản thiết kế trước từng chọn nó (lý do
  Open/Closed), hạ xuống mở rộng sau theo NT6: chưa ai cần đổi thứ tự bước mà không compile; thêm sau là
  additive (một overload nhận config asset) · *chạy song song các bước cùng pha* — cold start chưa đo được
  là chậm (NT9); thêm sau additive (cờ trên contract bước + `WhenAll` trong runner) · *auto-discovery* —
  magic khó debug.
- **Mở rộng sau:** manifest SO · parallel-in-phase · nhóm bước theo scene.

**Nghiệm thu.**
- Nhìn log boot kể lại được đúng thứ tự init, không cần đọc code.
- Một bước throw giữa cold start: vẫn vào được game, lỗi hiện rõ trong log, splash không treo.
- Reinit 2 lần liên tiếp không rò task (đếm instance/log token cancel).
- Hai bước trùng `Order`: thứ tự lặp lại y hệt giữa các lần chạy.

**Khảo sát (re-verify 2026-08-29).** 4 repo = 3 hình khác nhau, pattern **chưa hội tụ**:
- `color-loop` — gần hình đã chọn nhất: `Assets/_TheGame/Runtime/_Core/Scripts/GamePlay/BaseManager.cs`
  (25 LOC) + `GameManager.cs` (112, có Reinitialize + AfterReinitialize + RefreshGameToken) +
  `Runtime/Game/Scripts/ServiceInit.cs` (54). UniTask + CT + Priority đều thật. Mìn: 3 điểm khởi tạo rời
  rạc (GameInitializer / ServiceInit / StartGame) và 2 chiều sort ngược nhau.
- `foods_jam` / `Goods-Jam` — `GameBoostrap.cs` (~150/214 LOC): chuỗi `await` tuần tự hard-code theo enum
  phase (WaitForOnline→InitServices→InitPlayerData→CheckNewVersion→Finished), không contract/registry;
  từng gãy thật vì `.Forget()` nuốt exception treo loading (có comment thừa nhận); IAP init đồng bộ trước
  chuỗi async.
- `water-flow` — không orchestrator: thứ tự từ scene `Services` + Sisus `Initializer<,>`; mỏng nhưng thứ
  tự async ngầm định, không chờ được chuỗi có kiểm soát.

**Cạm bẫy.** Thứ tự ngầm qua `Awake()` · `.Forget()` không bọc try/catch (đã sai một lần — foods_jam) ·
init đồng bộ chen trước chuỗi async · lạm dụng service-locator lấy bước rồi gọi lẫn nhau (coupling ẩn —
ưu tiên DI) · quit duyệt xuôi thứ tự init (dọn nền trước khi hệ trên dọn xong).

---

## 2. Persistence — `Foundation` · *thiết kế mới trên bằng chứng*

**Bài toán.** Lưu tiến độ người chơi phải **có kiểu**, **không mất khi app bị kill**, và **không biến
thành god-blob** mọi hệ cùng thò tay vào. Bằng chứng cho thấy cả 4 repo **chưa có bản đúng tư tưởng nào
đang sống**: khung "sạch" của color-loop đã chết (không ai đăng ký, autosave chạy no-op), state thật dồn
vào god-blob `GameData` 25+ field lưu bằng JsonUtility + PlayerPrefs; 2 repo khác giao cả cho thư viện
ngoài (RCore). Hệ này **thiết kế mới**, lấy nguyên liệu tốt nhất từ mỗi repo.

**Use case.**
- Lưu level, coin, booster, settings, lives, streak, tiến độ event — mỗi cụm một đơn vị, module nào sở hữu cụm đó.
- Autosave định kỳ; app bị kill đột ngột không mất quá 1 chu kỳ.
- Đổi format serialize (MemoryPack ↔ JSON) không đụng logic game.
- Đọc/ghi lẻ một giá trị nhỏ (bool "đã rate chưa") không cần dựng model — typed-prefs (§A.3).
- Đồng bộ cloud giữa thiết bị *(mở rộng sau — xem Phạm vi v1)*.

**Tư tưởng cốt lõi.**
- **Nhiều save-unit độc lập** đăng ký vào một registry — một sự thật một chủ sở hữu (MY_SKILL §3.8); thêm
  save-unit mới **không sửa SDK, không sửa unit khác** (Open/Closed). Blob = mọi hệ coupling vào cùng
  object, đổi 1 field dirty cả blob.
- Mỗi unit: typed, dirty flag, on-change; API đọc/ghi kiểu "một property `Value`" — học bản sống khỏe duy
  nhất đang có (`KPrefs<T>` của water-flow: cache đọc một lần, set thì ghi + phát on-change).
- Registry: vòng autosave gom unit dirty + `FlushAll` ở pause/quit; serialize **chỉ trong autosave loop**
  (không trong hot path mỗi lần coin đổi), ghi vào buffer dùng chung.
- `ISerializer` thay được — abstraction đủ điều kiện **ngay v1** vì đã có hai implementation thật trong hệ
  sinh thái (MemoryPack ở color-loop, JSON/Newtonsoft ở water-flow), không phải phòng xa.
- Key của unit là **const string tường minh**, không `typeof(T).Name` — đổi tên type không được làm mất
  save (khoá wire format là hợp đồng, MY_SKILL §3.7).
- Load fail (miss/corrupt) → model default + log, **không throw** — save hỏng không được chặn người chơi
  vào game.
- **Dirty reset SAU khi ghi thành công** — reset trước mà I/O lỗi là mất dữ liệu im lặng.

**Ranh giới SDK / game.**

| SDK sở hữu | Game điền |
|---|---|
| Contract save-unit (typed, dirty, on-change) + registry (autosave, flush) | Model dữ liệu + khai báo unit + key |
| `ISerializer` + 2 impl (MemoryPack, JSON) · typed-prefs (§A.3) | Chọn serializer; nhịp autosave/flush theo game |

**Phạm vi v1.**
- Unit + registry (dirty/autosave/flush) + `ISerializer` 2 impl + on-change + typed-prefs.
- **Cố ý KHÔNG làm ở v1:**
  - **Crypto** — cả 4 repo đều không dùng thật (Goods-Jam có Rijndael/TripleDES nhưng dead code, key còn
    placeholder). Hình dạng đã nghĩ sẵn: **decorator quanh `ISerializer`** — thêm sau không sửa call-site.
  - **Cloud** — nhu cầu thật (foods_jam/Goods-Jam sync qua RCore `DownloadThenImportProfile`) nhưng backend
    chưa chuẩn chung. Hình dạng đã nghĩ sẵn: **`ICloudSyncable` tách riêng theo ISP** (không nhồi vào
    contract unit — unit cục bộ không bị buộc implement rồi `return null`) + snapshot dictionary trên
    registry; kèm rule merge (version/level/timestamp) chống "thiết bị mới data rỗng đè thiết bị cũ".
- **Mở rộng sau:** crypto decorator · cloud sync + merge rule · migration version cho model.

**Nghiệm thu.**
- Thêm một save-unit mới: không sửa dòng nào trong SDK và unit khác.
- Kill app bất kỳ lúc nào: mất tối đa một chu kỳ autosave; file corrupt vẫn vào được game.
- Đổi serializer: round-trip mọi unit về đúng giá trị (MY_SKILL §4.3), không sửa code unit.
- **Không có đường "chạy no-op âm thầm":** registry rỗng, unit quên đăng ký, key trùng — đều lộ ra
  (log/assert), không im lặng như khung cũ của color-loop.

**Khảo sát (re-verify 2026-08-29).**
- `water-flow` — **nguyên liệu API tốt nhất, đang sống:** `Assets/_Core/Kelsey/Core/Runtime/Prefs/KPrefs.cs`
  — `KPrefs<T>.Value`, cache lazy, on-change, server-save hook, `ForceRefresh()`. Lưu ý: `AESEncryption`
  tồn tại nhưng tách rời (KPrefs không mã hoá) và bị copy trùng 2 nơi.
- `color-loop` — **nguyên liệu khung, nhưng khung đã chết:** `PlayerSaveLoadService.cs` (122 LOC,
  MemoryPack, autosave 100ms, dirty) còn nguyên cấu trúc mà `AssignService()` không có caller → autosave
  no-op. State thật: `GameDataManager.cs` god-blob `GameData` (JsonUtility + PlayerPrefs, partial
  `.Lives/.Resources/.Settings`). *Phản ví dụ trung tâm của hệ này.*
- `foods_jam` / `Goods-Jam` — save qua RCore `JObjectDBManagerV2` (UPM ngoài); bằng chứng nhu cầu
  cloud-sync là thật; crypto Goods-Jam là dead code.

**Cạm bẫy.** God-blob (đã sai một lần — color-loop) · khung không ai đăng ký chạy no-op âm thầm (đã sai
một lần — color-loop) · autosave loop không nhận `CancellationToken` → `while(true)` sống sau destroy ·
serialize trong hot path → GC · cloud ghi đè local mù quáng · key trùng giữa 2 unit → unit sau đè unit
trước (registry phải log khi Register).

---

## 3. Scene Flow & Loading — `Foundation` · *extract từ Goods-Jam (verified nguyên vẹn)*

**Bài toán.** Chuyển scene phải: che bằng loading screen, progress **mượt** (không nhảy 0→100), **chặn
input** suốt transition, và có **thời gian hiển thị tối thiểu** để không nháy khi load nhanh. Không có hệ
chung, mỗi màn tự chế một kiểu loading và double-tap mở hai màn chồng nhau.

**Use case.**
- Splash → Home → Gameplay → Home, loading che asset đang tải.
- "Fake loading" khi thực ra không cần load gì (reload level cùng scene) nhưng UX cần nhịp nghỉ.
- Chặn double-tap/spam suốt transition — tiện ích dùng lại được **ngoài** scene-load (mở popup, chờ ads).
- Progress = tổng hợp nhiều task: load scene + preload asset + warm pool + fetch data.
- Hook 3 điểm: **trước** khi đổi scene · **giữa** lúc màn che kín · **sau** khi scene hiện.

**Tư tưởng cốt lõi.**
- Service điều phối load (Addressables-first, UniTask + CT), **không vẽ gì**; loading screen **chỉ nhận
  progress qua contract**, không bị service gọi thẳng vào method UI (điểm sửa so với bản Goods-Jam —
  coupling ngược chiều).
- Progress: **gộp nhiều task có trọng số → smooth (lerp theo `unscaledDeltaTime`) → quantize** — Addressables
  trả progress giật cục (0 → 0.9 → 1), mắt người đọc thanh nhảy là "lag". Progress **không bao giờ lùi**.
- **Min-time per-scene** (config asset): load nhanh mà không có sàn thời gian → loading nháy 1 frame, tệ
  hơn không có. Fake-progress và min-time là **quyết định UX có chủ đích** — ghi rõ để người tối ưu sau
  không "sửa cho đúng".
- Hook **MidTransition** là *cửa sổ duy nhất* swap state mà người chơi không thấy nháy — lý do nó tồn tại
  như một khái niệm riêng.
- **Blocker đếm tham chiếu + scope** (idiom §0.7): nhiều nguồn block đồng thời, `bool` sẽ mở khoá sớm khi
  nguồn đầu tiên nhả; kèm scope `IDisposable` để nhánh exception không rò refCount.
- `unscaledDeltaTime` — transition phải chạy khi `timeScale = 0`.

**Ranh giới SDK / game.**

| SDK sở hữu | Game điền |
|---|---|
| Service load + aggregator progress + min-time/fake + 3 hook | Danh sách scene, số min/fake per-scene, nội dung preload |
| Blocker (tiện ích độc lập) + contract cho loading screen | Loading screen cụ thể (UI, skin) |

**Phạm vi v1.**
- Đủ bộ đã kiểm chứng: load async (single + additive, track handle) + progress gộp/smooth/quantize +
  min-time/fake + 3 hook + blocker.
- **Cố ý KHÔNG làm ở v1:** `ITransition` (fade/slide) — chưa repo nào có nhiều hơn một kiểu transition
  thật; abstraction chưa có implementation thứ hai (MY_SKILL §2.4). Hình dạng đã nghĩ sẵn: interface 2
  method vào/ra — thêm sau additive.
- **Mở rộng sau:** `ITransition` · preload theo manifest khai báo.

**Nghiệm thu.**
- Spam nút play trong transition: không load 2 lần (guard ngay đầu), không mở được gì.
- Exception giữa load: blocker vẫn được nhả (scope), app không khoá cứng.
- Progress không bao giờ giảm; đạt 1.0 rồi mới activate scene.
- Load thật nhanh hơn min-time: màn loading vẫn hiển thị đủ min-time, không nháy.

**Khảo sát (re-verify 2026-08-29).**
- `Goods-Jam` — **nguồn chính, CONFIRMED đúng từng con số:** `Assets/_Asssets/Scripts/Common/Manager/SceneLoader/`
  — `SceneManager.cs` (204) + `SceneLoaderScreen` (192) + `SceneFakeLoaderScreen` (162) +
  `ScreenInteractionBlocker` (53, MonoSingleton + counter lồng nhau) + 2 file data = 657 LOC. UniTask,
  `Addressables.LoadSceneAsync`, progress lerp + snap, `sceneLoadingMinTime`, hook Before/Middle/After.
- `color-loop` — biến thể event-driven gọn: `LoadingManager` (27 LOC) + `GameLoadContext` (50 LOC, mảng
  `Func<IProgress<float>, UniTask>`) — nguồn của tư tưởng "progress gộp nhiều task".
- `water-flow` — `LoadingController` dùng `allowSceneActivation=false`, drive progress tới 1 rồi mới
  activate; không có blocker riêng.
- `foods_jam` — `InitializerLoadingPanel` (274 LOC) monolith: panel tự load scene + fake progress — phản
  ví dụ "loading screen ôm logic load".

**Cạm bẫy.** Loading screen tự nó cần load asset → đặt ở scene bootstrap siêu nhẹ hoặc preload trước ·
guard chống gọi lồng phải nằm **ở đầu** service · `Addressables` scene additive không release → rò ·
quên nhả blocker ở nhánh exception → khoá cứng vĩnh viễn · `Time.deltaTime` khi pause = 0 → progress
đóng băng.

---

## 4a. Ticker — `Foundation` · *thiết kế mới (plan cũ khôi phục được từ git)*

**Bài toán.** Hàng chục UI countdown, hiệu ứng, hệ theo nhịp — mỗi cái tự chạy `Update()`/vòng
`UniTask.Delay` riêng là GC, phí CPU và không kiểm soát được. Cần **một nguồn tick trung tâm** (luật
§0.4a). Bằng chứng: **0/4 repo có** — Goods-Jam là bản khá nhất cũng để mỗi counter tự chạy một vòng
`UniTask.Delay(1s)` với CTS riêng.

**Use case.**
- Nhiều label countdown cùng refresh 1 lần/giây — 1 tick, N listener.
- Hiệu ứng/logic cần nhịp mỗi frame nhưng không muốn MonoBehaviour riêng.
- App background → resume: mọi hệ theo thời gian cần biết **đã offline bao lâu** — con số tính **một
  nơi**, phát cho tất cả (tính lại ở từng hệ = lặp + lệch nhau).

**Tư tưởng cốt lõi.**
- **Một `Update` duy nhất** phát 3 nhịp: mỗi frame · 1 Hz · pause-changed. Nhịp 1 Hz cho countdown UI —
  60 Hz cho một dòng text "02:31" là vô nghĩa, giảm 60× công format.
- **3 nhịp = 3 interface 1-method** (ISP): listener chỉ implement nhịp nó cần, không ai viết method rỗng.
- **Listener là interface + list, không `event Action<T>`**: đăng ký/huỷ xảy ra liên tục theo popup
  mở-đóng — mỗi `+=`/`-=` cấp phát invocation-list mới; interface + `List<T>` là 0 byte (§0.4b). Method
  đăng ký đặt tên theo nhịp, không overload chung — một class implement 2 nhịp sẽ làm overload nhập nhằng.
- Nhịp pause mang `offlineSeconds` — v1 tính bằng device UTC (đủ đúng cho *khoảng* offline); khi Time
  Service (4b) xuất hiện, đổi nguồn giờ **bên trong** ticker, consumer không đổi.
- Mặc định `unscaledDeltaTime` — countdown/event không được đóng băng khi pause gameplay.
- Cô lập lỗi: try/catch quanh **từng** callback (§0.4a) — 1 listener throw không kill vòng tick.

**Ranh giới SDK / game.**

| SDK sở hữu | Game điền |
|---|---|
| Ticker + 3 contract nhịp + event Destroyed | Listener cụ thể; tự Remove trong OnDestroy |

**Phạm vi v1.**
- Đúng phần trên. **Cố ý KHÔNG làm ở v1:** nhịp interval tuỳ ý (chưa có nhu cầu thật; thêm sau additive) ·
  mọi thứ thuộc *giờ/lịch* (`ITimeService`, `Countdown`, `TimeFormatter`) — đó là **4b**, Tầng 3.
- **Ghi chú plan:** plan chi tiết `TickerSystem.md` (ITicker + 2 nhịp + `IOptionalService` + `DeferredList`)
  từng tồn tại, **đã bị xoá trong commit `clean`** — khôi phục từ git khi bắt đầu làm, đối chiếu lại với
  mục này (mục này là nguồn tư tưởng, plan là nguồn chi tiết).

**Nghiệm thu.**
- 20 label countdown chạy đồng thời: 0 byte GC alloc mỗi giây (Profiler).
- Đăng ký/huỷ listener 1000 lần: không alloc.
- Resume sau N giờ background: `offlineSeconds` đúng (±1s).
- 1 listener throw: các listener còn lại vẫn nhận tick, lỗi được log.

**Khảo sát (re-verify 2026-08-29).** 0/4 repo có nguồn tick trung tâm. Goods-Jam
(`TimeCounter`/`TimeRemainElement`): mỗi instance một vòng `UniTask.Delay(1s, ignoreTimeScale)` + CTS
riêng, và `TimeRemainElement` duplicate gần nguyên logic `TimeCounter` — đúng hai bệnh hệ này chữa.

**Cạm bẫy.** Đăng ký tick mà không remove trong `OnDestroy` → ticker giữ reference chết · sửa list
listener trong lúc đang phát tick (add/remove từ trong callback) → cần deferred add/remove · để logic
nặng trong nhịp frame khi nhịp 1 Hz là đủ (MY_SKILL §3.5 — khai đúng nhịp).

---

## 5. Stack State Machine — `Foundation` · *giữ theo quyết định user; viết mới thuần C#*

> **Ghi chú phạm vi:** re-verify cho thấy 1/4 repo có (chỉ color-loop) và bản đó **không có caller
> runtime** (chỉ 2 file sample). User quyết giữ ở Tầng 1 vì tin đây là utility nền — quyết định user, chỉ
> user đổi. Hệ quả ràng buộc: v1 **bắt buộc có caller thật** ở game đầu tiên tích hợp mới tính là xong.

**Bài toán.** Nhiều flow cần "quay lại trạng thái trước": pause rồi resume, tutorial tạm chiếm control
rồi trả lại. FSM phẳng buộc mỗi state phải **biết** state trước nó là gì — ngăn xếp thì không.

**Use case.**
- Gameplay: `Playing` → push `Paused` → pop → về đúng `Playing` với **nguyên state** (không rebuild).
- Tutorial push `TutorialControl`, chặn input gameplay, pop xong trả lại.
- Cutscene chiếm quyền tạm thời; undo-flow nông (mở nhiều lớp, đóng lần lượt).

**Tư tưởng cốt lõi.**
- Vòng đời chia **hai nhóm hook** — đây là giá trị thật của stack so với FSM phẳng: `Push/Pop` (sinh–diệt
  của chính state) tách khỏi `Suspend/Resume` (bị đè–được trả lại, quan hệ với lân cận). `Paused` push lên
  `Playing` thì `Playing` **Suspend** (giữ nguyên state), không **Pop** (mất state).
- **Thuần C#, không MonoBehaviour** — state là logic, test được không cần scene; chủ sở hữu cấp tick
  (qua Ticker 4a hoặc tự gọi).
- Chỉ **đỉnh** stack nhận update — state bị che không tiêu CPU.
- State độc lập, **không biết nhau**, không giữ tham chiếu chéo — machine điều phối.
- **Không transition table** — ngăn xếp không cần bảng chuyển; thêm bảng/blackboard/visual editor là biến
  thành hệ khác (~150 LOC là đúng cỡ của nó).

**Ranh giới SDK / game.**

| SDK sở hữu | Game điền |
|---|---|
| Contract state (push/pop/suspend/resume/update/reset) + machine | Các state cụ thể; ai cấp tick |

**Phạm vi v1.**
- Contract + machine thuần C# (API tham chiếu bản color-loop: push/pop/pop-nhiều/pop-hết/reset).
- **Cố ý KHÔNG làm ở v1:** FSM phẳng kèm theo (chưa có nhu cầu thật; nếu cần là biến thể cùng contract,
  thêm sau additive) · adapter MonoBehaviour (chưa có người dùng đòi kéo thả Inspector; bản component cũ
  của color-loop chỉ làm tham khảo API).
- **Mở rộng sau:** FSM phẳng cùng contract · adapter component.

**Nghiệm thu.**
- Unit test thuần C# phủ push/pop/suspend/resume/pop-nhiều-tầng/pop-stack-rỗng — chạy không cần scene.
- Push/pop/reset không alloc sau lần khởi tạo; `Playing→Paused→Playing` giữ nguyên state.
- **Có ít nhất một caller thật** (ví dụ pause flow) trong game đầu tiên tích hợp — không caller thì hệ
  chưa xong, dù code hoàn chỉnh.

**Khảo sát (re-verify 2026-08-29).**
- `color-loop` — bản duy nhất: `Assets/_TheGame/_AlienCode/Unity Extensions/Extra/Runtime/StackStateMachine/`
  — `IStackState.cs` (41 LOC: 6 hook đúng mô hình trên) + `StackStateMachineComponent.cs` (165 LOC, kế
  thừa `ScriptableComponent` — **không** thuần C# như khảo sát cũ ghi). Caller: chỉ 2 file sample UI.
- `foods_jam` / `Goods-Jam` / `water-flow` — GONE. Thứ gần nhất là stack của UI navigator (Push/Pop
  popup) — cùng semantics nhưng là hệ khác (§7).

**Cạm bẫy.** Over-engineer (transition table/visual editor) · `Push`/`Pop` từ bên trong `OnPush`/`OnPop`
→ sửa list đang duyệt (guard DEBUG + hàng đợi nếu cần) · `Pop` trên stack rỗng → NRE (guard/`bool`) ·
tích luỹ thời gian state bằng `float` mất chính xác sau vài giờ (dùng `double`).

---

## 6. Safe-Area & Responsive Canvas — `Foundation` · *tự viết trên bộ code đã hội tụ ở 3 repo*

**Bài toán.** UI mobile phải đúng trên mọi tỉ lệ + notch + home-bar **và cả banner ads chiếm đáy màn
hình**. Vùng an toàn **đổi lúc runtime** (xoay máy, banner hiện/ẩn) → tính một lần lúc `Start()` là sai.
Bằng chứng còn cho thấy vấn đề thứ hai không phải "thiếu" mà là **thừa**: mỗi repo đang chạy 2–4 bản
safe-area song song — SDK tồn tại để mỗi dự án chỉ còn **một đường duy nhất**.

**Use case.**
- Nút không lọt dưới notch/home-bar/camera đục lỗ; layout co giãn theo aspect (tablet 4:3 ↔ phone 20:9).
- Banner hiện → padding đáy tăng, UI đẩy lên; banner ẩn → trả lại.
- Vùng **ngược lại**: background phải phủ kín cả notch, không co vào safe-area.
- Board gameplay canh giữa vùng an toàn (quy đổi sang world bounds cho camera).
- Preview notch trong Editor khi làm UI.

**Tư tưởng cốt lõi.**
- Contract **một hàm** `UpdateRect()` — mọi thứ phản ứng với vùng an toàn đều implement được (ISP cực gọn).
- **Padding bằng anchor, không offset**: anchor là tỉ lệ → tự đúng khi canvas scale/đổi resolution;
  offset là pixel → lệch. Cạnh bám khai bằng `[Flags]` (thực tế mỗi panel chỉ bám 1–2 cạnh).
- **Watcher so sánh rồi mới update** (dirty): `Screen.safeArea` đổi rất ít; rebuild layout mỗi frame là
  phí lớn nhất của UGUI.
- **Inset ngoài OS qua provider tuỳ chọn** (`IOptionalService` — §0.2): chiều cao banner chỉ hệ ads biết —
  SDK safe-area không được phụ thuộc ads; banner chỉ là một nguồn inset nữa cộng vào padding đáy.
- **Vùng ngược là component riêng** — nhu cầu ngược hẳn, nhồi cờ vào một class là if/else lồng.
- Quy đổi vùng an toàn → world bounds (bounder) cho board/camera — cùng nguồn sự thật với UI.

**Công thức padding** (`safeArea` gốc trái-dưới, cùng hệ pixel với `width`/`height`):

```
paddingTop    = height − (safeArea.y + safeArea.height)
paddingBottom = safeArea.y            (+ inset banner nếu có)
paddingLeft   = safeArea.x
paddingRight  = width  − (safeArea.x + safeArea.width)

anchorMin = ( paddingLeft / width          , paddingBottom / height )
anchorMax = ( (width − paddingRight)/width , (height − paddingTop)/height )
sizeDelta = anchoredPosition = 0        ← anchor gánh toàn bộ, không dùng offset
```

**Kiểm mốc**

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| Không notch (`safeArea == full screen`) | 4 padding = 0 → `anchorMin=(0,0)`, `anchorMax=(1,1)` | full-stretch |
| Chỉ bám `Top`, notch cao 100px, `height=2400` | `anchorMax.y = 2300/2400` | chừa đúng 100px |
| Banner 150px, có cờ `Bottom` | `anchorMin.y = 150/2400` | UI đẩy lên đúng chiều cao banner |
| Xoay ngang | `safeArea` đổi → watcher phát hiện → update | không cần restart |

**Ranh giới SDK / game.**

| SDK sở hữu | Game điền |
|---|---|
| Contract + base + component padding/vùng-ngược + watcher + bounder | Áp component vào canvas/layout của nó |
| Contract inset provider (optional) | Hệ ads của game implement provider (báo chiều cao banner) |

**Phạm vi v1.**
- Đủ bộ trên (contract · base · padding `[Flags]` · vùng ngược · watcher · inset provider · bounder).
- **Cố ý KHÔNG làm ở v1:** *wrap NotchSolution* — phương án đã cân hai lần và loại: không cover banner
  inset, và bằng chứng cho thấy các team vẫn tự viết song song dù đã có nó; NotchSolution vẫn dùng được
  như công cụ preview notch trong Editor ở dự án sẵn có — **không là dependency runtime của SDK** · *simulator
  proxy riêng* — bộ cũ có (reflection vào Device Simulator), thêm sau nếu cần preview không qua NotchSolution.
- **Mở rộng sau:** simulator proxy · grid/layout helper bám vùng an toàn khi có board thật cần.

**Nghiệm thu.**
- Xoay máy / banner hiện-ẩn: UI đúng ngay, không restart; công thức cho full-stretch khi không notch
  (bảng kiểm mốc trên).
- Zero rebuild khi vùng an toàn không đổi (watcher chỉ phát khi khác).
- Dự án tích hợp xong: grep chỉ còn **một** đường safe-area — các bản song song cũ bị thay, không chạy kèm.

**Khảo sát (re-verify 2026-08-29).**
- Bộ code hội tụ (`SafeAreaBase` ~171–176 LOC + `SafeAreaComponent` ~92–96 + `RuntimeSafeAreaUpdater` +
  `ISafeAreaUpdatable` + simulator proxy) xuất hiện ở **3 repo** — một tổ tiên chung bị copy: foods_jam
  (namespace `Squirrel.UGUI`, KHÔNG phải lib riêng như khảo sát cũ ghi — nằm lẫn trong Gameplay/Utilities),
  Goods-Jam, và bản đầy đủ nhất ở water-flow (`Kelsey/UGUI/SafeArea~`, có prefab post-processor) **đã bị
  tắt** — folder hậu tố `~` nên Unity bỏ qua; bản đang chạy ở đó là `SafeAreaFilter/SafeArea.cs` đơn giản hơn.
- NotchSolution: 3/4 repo (đều vendored trong Assets; water-flow không có) — khảo sát cũ ghi "cả 4" là sai
  — và luôn chạy **song song** với bản tự viết, chưa nơi nào là đường duy nhất.
- Bệnh chung: foods_jam 4 bản safe-area song song · color-loop 3 · Goods-Jam 2.

**Cạm bẫy.** Tính một lần trong `Start()` → sai khi xoay máy/banner hiện · trộn safe-area với anchor thủ
công trên **cùng** `RectTransform` → giằng nhau (component chiếm trọn một RectTransform, con của nó tự do)
· quên vùng ngược → viền đen quanh notch · quên inset banner → nút bị banner đè · tích hợp mà không gỡ
bản song song cũ là tái tạo đúng cái bệnh SDK sinh ra để chữa.

---

# TẦNG 2 — Services

> ⚠️ **Từ đây đến hết Phụ lục: nguyên trạng bản trước, chưa polish, chưa re-verify** (khuôn 8 phần cũ, có
> contract C#) — ngoại lệ duy nhất: mục **4b Time Service** ở Tầng 3 đã re-verify 2026-08-29. Đường
> dẫn/LOC khảo sát trong vùng này có thể đã trôi; khi polish tầng nào thì re-verify tầng đó như đã làm
> với Tầng 1.

## 7. UI Navigator (Page / Popup / Sheet) — `Composite` ⭐ **hệ UI quan trọng nhất**

**Bài toán.** Mọi game F2P đều cần: mở popup có dữ liệu **có kiểu**, chồng popup và đóng về đúng lớp dưới, animation vào/ra **await được**, load prefab qua Addressables (không nhồi hết vào scene), backdrop, và **không mở 2 popup khi spam tap**.

**Use case**
- `Push<SettingsPopup>()` · `Push<RewardPopup, RewardData>(data)` — truyền dữ liệu type-safe, không cast.
- Chồng 3 popup, back button đóng đúng cái trên cùng.
- Chờ popup đóng rồi tiếp tục flow: `await popup.WaitForCloseAsync()`.
- Popup mở thường xuyên (shop, settings) → **recycle**; popup mở 1 lần (win/lose) → destroy + release handle.
- Tap backdrop để đóng (có popup cho, có popup không).
- 3 hình thái cùng vòng đời: **Page** (toàn màn, 1 tại 1 thời điểm) · **Popup** (đè, xếp chồng) · **Sheet** (trượt từ mép).

**Mô hình**

| Thành phần | Vai trò |
|---|---|
| `View` (abstract) | Vòng đời + animation vào/ra; base của cả 3 hình thái |
| `ViewT<TData>` / `PopupT<TData>` | Thêm `SetData(TData)` — typed data, không `object` |
| `Page` : `View` | Toàn màn; container giữ **1** page |
| `Popup` : `View` | Thêm backdrop + cờ recycle + Addressables handle |
| `Sheet` : `View` | Trượt từ mép; chỉ khác `ITransition` mặc định |
| `IPageContainer` / `IPopupContainer` | Quản stack + load/instantiate/recycle; **không** biết nội dung view |
| `NavigatorDatabase` (SO) | Animation vào/ra mặc định cho từng hình thái + clip âm mở/đóng |
| `PopupBackdrop` | Dim + nhận tap; 1 backdrop/popup, sibling index ngay dưới popup |

**Vòng đời `View`** — 6 điểm, 3 sync 3 async:

```
Enter():  guard(IsAnimating‖IsShowing) ─► blocksRaycasts=false ─► SetActive(true)
          ─► WillEnter()  (sync, set giá trị đầu)
          ─► WhenAll(enterAnimations)  hoặc  default từ NavigatorDatabase
          ─► DidEnter()   (sync)      ─► blocksRaycasts=true
Exit():   guard ─► blocksRaycasts=false ─► WillExit()
          ─► WhenAll(exitAnimations)  ─► SetActive(false) ─► DidExit()
                                          └─ recycle? giữ instance : Destroy + handle.Release()
```

**Contract**

```csharp
// KHÔNG trùng với ITransition (§3): §3 che toàn màn hình giữa 2 scene và do
// ISceneFlowService sở hữu; cái này animate MỘT RectTransform của một view.
public interface ITransitionAnimation
{
    void SetUp(RectTransform target);
    void Prepare();                                     // set trạng thái đầu (alpha 0, scale 0.8…)
    UniTask PlayAsync(CancellationToken ct);
}

public interface IPopupContainer : IService<IPopupContainer>
{
    bool IsBusy { get; }                                // đang transition hoặc có popup mở
    int StackCount { get; }

    UniTask<TPopup> PushAsync<TPopup>(CancellationToken ct = default)
        where TPopup : Popup;
    UniTask<TPopup> PushAsync<TPopup, TData>(TData data, CancellationToken ct = default)
        where TPopup : PopupT<TData>;

    UniTask PopAsync(CancellationToken ct = default);   // đóng đỉnh
    UniTask PopAllAsync(CancellationToken ct = default);
    bool TryGet<TPopup>(out TPopup popup) where TPopup : Popup;
}

public interface IPageContainer : IService<IPageContainer>
{
    UniTask<TPage> EnterAsync<TPage>(CancellationToken ct = default) where TPage : Page;
    UniTask ExitAsync(CancellationToken ct = default);
}

public abstract class View : MonoBehaviour
{
    protected bool IsShowing { get; }
    protected bool IsAnimating { get; }
    public UniTask WaitForCloseAsync(CancellationToken ct = default);

    protected virtual UniTask InitializeAsync() => UniTask.CompletedTask;   // 1 lần duy nhất
    protected virtual void WillEnter() { }
    protected virtual void DidEnter()  { }
    protected virtual void WillExit()  { }
    protected virtual void DidExit()   { }
    protected virtual ITransitionAnimation DefaultEnterAnimation { get; }   // ← NavigatorDatabase
    protected virtual ITransitionAnimation DefaultExitAnimation  { get; }
}
```

**Luồng push popup**

```
PushAsync<TPopup, TData>(data)
  ├─ if (IsBusy) return null                         ← chống double-open do spam tap
  ├─ resolve prefab:
  │     recyclePool.Find<TPopup>()  →  hit: dùng lại instance
  │                                 →  miss: Addressables.LoadAssetAsync → Instantiate
  │                                        + AssignHandle(handle)     ← để Release đúng lúc
  ├─ if (popup.ShowBackdrop) instantiate/lấy backdrop, SetAsLastSibling()
  ├─ popup.SetAsLastSibling()                        ← đưa lên trên cùng
  ├─ await popup.InitializeAsync()                   ← chỉ lần đầu
  ├─ popup.SetData(data)                             ← TRƯỚC Enter, để WillEnter đọc được
  ├─ await popup.Enter()
  └─ push vào stack, trả popup
```

**Quyết định thiết kế**

| Quyết định | Vì sao | Đánh đổi |
|---|---|---|
| Container tách hẳn khỏi View | Container quản stack/load/pool; View chỉ lo nội dung (S trong SOLID) | 2 khái niệm |
| `PopupT<TData>` generic thay `object data` | Type-safe lúc compile; không cast, không boxing struct | 1 class con cho mỗi arity dữ liệu |
| `SetData` **trước** `Enter()` | `WillEnter()` thường cần dữ liệu để set giá trị đầu của animation | Không đổi data khi đang mở (cần `Refresh()` riêng) |
| 4 hook vòng đời (Will/Did × Enter/Exit) | Will = set trạng thái đầu · Did = bật interaction/phát âm. Gộp lại thì không có chỗ chạy tween vào/ra đúng nhịp | 4 override |
| `IsBusy` guard **ở container** | Guard ở từng nút = quên chỗ này chỗ kia. Chặn tại 1 điểm là chặn triệt để | Không mở được 2 popup **cố ý** đồng thời (rất ít khi cần) |
| `blocksRaycasts=false` suốt animation | Bấm vào popup đang bay vào = state hỏng | — |
| Cờ `recycle` **per-prefab** | Popup hay mở (shop) recycle lợi; popup mở 1 lần recycle = giữ RAM vô ích | Người thiết kế prefab phải quyết |
| Animation: mảng `ITransitionAnimation` + fallback SO | Nhiều animation chạy song song (`WhenAll`: fade + scale + slide); không khai thì lấy default toàn cục → nhất quán | — |
| Handle Addressables gắn **vào instance** | Release đúng thời điểm destroy instance đó, không sớm không muộn | Phải truyền handle vào popup |
| `WaitForCloseAsync()` | Flow tuần tự (`await` popup rồi làm tiếp) dễ đọc hơn callback lồng | — |

**Cạm bẫy**
- **Double-open**: spam tap mở 2 instance cùng popup → `IsBusy` guard + `blocksRaycasts` (§10 khoá thêm 1 lớp ở nút).
- **Rò Addressables**: destroy popup mà không `Release()` handle → bundle giữ RAM mãi. Track handle → release trong `DidExit` nhánh không-recycle.
- `Resources.Load` trộn với Addressables mà không đồng nhất → 2 đường load, 2 kiểu rò. Chọn **Addressables-first**, `Resources` chỉ cho prefab siêu nhẹ (backdrop).
- `GetComponentsInChildren<Popup>()` mỗi lần hỏi "có popup nào mở không" → cấp phát array + duyệt hierarchy. Giữ `List<Popup>` stack rõ ràng.
- Popup recycle **không reset state** → lần mở sau còn dữ liệu cũ. `WillEnter()` phải set lại **mọi** field hiển thị.
- Animation `catch { }` rỗng nuốt lỗi → tween sai mà không ai biết. Log ở `catch`.
- Popup bị destroy giữa lúc `await` → tiếp tục truy cập là NRE. Truyền `CancellationToken` từ `GetCancellationTokenOnDestroy()`.
- `UniTask.WhenAll(animations)` cấp phát mảng + promise mỗi lần mở view. Mở view là **cold path** (vài lần/phút) nên chấp nhận — nhưng đừng bê khuôn này vào hiệu ứng chạy mỗi frame.

**Xong khi:** §0.6 + spam 10 tap chỉ mở 1 popup · mở/đóng 100 lần không tăng RAM (handle release) · popup recycle mở lần 2 sạch state · back button đóng đúng đỉnh stack.

---

## 8. Audio (music / SFX) — `Composite`

> 📄 **Đã có plan (phần SFX):** `Implementations/Foundations/Audio/AudioSystem.md`. Ba điểm **lệch có chủ ý** so với contract dưới đây, mỗi điểm có lý do ghi rõ trong plan:
> 1. ⚠️ **`PlaySfx` có thêm tham số `pitchScale`.** Contract dưới đây **không** có nó ⇒ *pitch ramp* — xương sống thính giác của combo (§22, §23) — không gọi được. Thêm sau là đổi chữ ký ở mọi call-site, nên nó phải có ngay từ đầu.
> 2. **Không dùng Object Pooling của SDK** cho `AudioSource`. `IPoolManager.Get<T>` **throw** khi pool chưa cấu hình, và pool đó là pool *prefab Addressables* — quá nặng cho một `AudioSource` trần. Thay bằng **vòng voice cấp phát sẵn** (mảng cố định, không `Get/Return`, không prefab).
> 3. Vì (2) + settings đi qua `IOptionalService`, bản thu hẹp **không phụ thuộc hệ nào trong SDK** ⇒ **phân loại lại thành `Foundation`** và đặt ở `Foundations/Audio/`. Khi bổ sung music + pooling thì phân loại lại thành Composite.
>
> **Chưa lên plan, vẫn ở mục này:** music + crossfade, `PauseAll`/`ResumeAll`, **SFX 3D** (`PlaySfxAt`/`PlaySfxAttached` + `spatialBlend`), `RandomWeighted`. Bản thu hẹp chỉ phát **2D** — nhờ đó voice không cần GameObject riêng, bớt được cả một class.

**Bài toán.** Nhạc nền cần crossfade và **một** source; SFX cần **nhiều** source đồng thời (pool) và **giới hạn tần suất** cùng một clip (10 item nổ cùng lúc phát 10 lần cùng clip = chói + clip qua nhau). Hai nhu cầu ngược nhau → hai controller.

**Use case**
- Nhạc nền theo scene, crossfade khi chuyển; fade-out khi mở popup quan trọng.
- SFX click/match/win nhiều cái cùng lúc.
- Throttle: cùng clip không phát lại trong <120ms.
- Setting music/sfx on/off lưu bền (§2) và áp **ngay** khi đổi.
- SFX theo vị trí 3D (particle nổ tại điểm A) và SFX 2D (UI click).
- Chọn clip trong nhóm: tuần tự · random · random-không-lặp-liền · random-có-trọng-số.
- Danh mục clip do **game** khai, SDK không biết clip nào.

**Mô hình**

| Thành phần | Vai trò |
|---|---|
| `IAudioService` | Facade duy nhất game gọi |
| `MusicController` | 1–2 `AudioSource`, crossfade, loop |
| `SoundEffectController` | Pool `AudioSource`, throttle same-clip, 2D/3D |
| `AudioCatalog` (SO) | `id → clip[] + volume/pitch range + selectNextMode`; **game điền** |
| `EAudioSelectMode` | `Sequential` · `Random` · `RandomNoRepeat` · `RandomWeighted` |
| `IAudioSettings` | *(qua interface)* đọc/ghi cờ music/sfx on-off — không coupling §2 |
| `AudioSourcePool` | Dùng Object Pool có sẵn của SDK; **không** viết pool riêng |

**Contract**

```csharp
public interface IAudioService : IService<IAudioService>
{
    // Music
    UniTask PlayMusicAsync(AudioId id, float fadeSeconds = 0.3f, CancellationToken ct = default);
    UniTask StopMusicAsync(float fadeSeconds = 0.5f, CancellationToken ct = default);

    // SFX
    void PlaySfx(AudioId id, float volumeScale = 1f);
    void PlaySfxAt(AudioId id, Vector3 worldPosition, float volumeScale = 1f);
    void PlaySfxAttached(AudioId id, Transform follow, float volumeScale = 1f);

    // Settings
    bool IsMusicOn { get; set; }
    bool IsSfxOn   { get; set; }
    void PauseAll();      // mất focus / mở video ads
    void ResumeAll();
}

public interface IAudioSettings                 // game nối vào save-unit của nó (§2)
{
    bool IsMusicOn { get; set; }
    bool IsSfxOn   { get; set; }
    event Action Changed;
}

// AudioId: struct wrap int/enum của game — KHÔNG dùng string ở call-site
public readonly struct AudioId { public readonly int Value; }
```

**Throttle same-clip**

```
PlaySfx(id):
  if (!IsSfxOn) return
  clip = catalog.Resolve(id)                                 ← theo selectNextMode
  now  = Time.unscaledTime
  if (lastPlayByClip.TryGetValue(clip, out t) && now − t < minInterval) return   ← bỏ qua
  lastPlayByClip[clip] = now
  source = pool.Rent(); source.clip = clip; source.Play()
  ReturnAfter(source, clip.length)                           ← trả pool khi xong
```

**Quyết định thiết kế**

| Quyết định | Vì sao | Đánh đổi |
|---|---|---|
| Tách `MusicController` / `SoundEffectController` | Nhu cầu ngược nhau (1 source + crossfade ↔ N source + throttle); gộp = if/else khắp nơi | 2 class |
| Catalog là SO, controller generic | SDK không được biết clip nào; game điền asset, không sửa code SDK | Game phải maintain catalog |
| `AudioId` là struct wrap, không `string` | String = typo runtime, không refactor được, alloc khi concat | Cần bước map id ↔ clip |
| Throttle theo **clip**, không theo id | Cùng id có thể chọn ra clip khác nhau → chỉ clip trùng mới chói | Dictionary theo clip ref |
| Dùng Object Pool sẵn có của SDK | 1 nguồn sự thật cho pooling; đừng có pool thứ hai | — |
| `IAudioSettings` là interface | Audio không được phụ thuộc trực tiếp hệ save (D trong SOLID) | 1 lớp gián tiếp |
| `unscaledTime` cho throttle | SFX phải throttle đúng cả khi `timeScale=0` (pause, slow-mo) | — |
| `PauseAll/ResumeAll` tách khỏi on/off | Pause (tạm, do ads/focus) khác Off (do người chơi) — trộn 2 cái là bug kinh điển: hết ads nhạc không trở lại | 2 cặp API |

**Cạm bẫy**
- Trộn "pause vì ads" với "off vì setting": hết ads bật lại theo cờ pause → ghi đè setting của người chơi.
- Catalog nhồi vào controller = coupling. Nếu game có nhóm clip riêng, dùng **partial/asset riêng**, không sửa file SDK.
- Pool `AudioSource` trả về **quá sớm** (theo `clip.length` mà clip có tail/reverb) → cắt tiếng. Kiểm `!source.isPlaying`.
- `PlayOneShot` trên 1 source dùng chung: không kiểm soát được stop/volume từng tiếng.
- Fade nhạc mà không cancel fade trước → 2 coroutine tranh volume. Giữ 1 CTS cho fade, cancel trước khi fade mới.
- SFX 3D mà `spatialBlend` để mặc định 0 → nghe như 2D; đặt tường minh trong catalog.

**Xong khi:** §0.6 + 20 SFX cùng frame không giật, không alloc · toggle setting áp ngay · sau ads nhạc trở lại đúng theo setting · throttle chặn được clip trùng.

---

## 9. Haptics — `Foundation`

> 📄 **Đã có plan:** `Implementations/Foundations/Haptics/HapticSystem.md` — thu hẹp còn `Play(preset)` + `PlayCustom(pattern)`, cộng `IHapticBackend` (seam nền tảng) + `NullHapticBackend` + `AndroidHapticBackend` có **điều khiển biên độ** (điều kiện bắt buộc để haptic ramp cảm nhận được).
> **Ngoài plan:** `BeginContinuous`/`EndContinuous`/`StopAll` + ref-count + vòng pulse (contract dưới đây có, plan cắt đi vì combo không cần — thêm sau chỉ là thêm 2 method); `HapticPattern.Frequency`/`PauseSeconds`; impl vendor.
> ⚠️ **Xung đột tên đã giải:** `HapticPattern` (struct dưới đây) = **một** cú rung. Thứ mà `Pendings.md` Nhóm 8-J gọi là `HapticPattern` (*chuỗi* rung tăng dần theo combo) đã đổi tên thành `HapticRampChannel`, thuộc §22.

**Bài toán.** Rung là phản hồi xúc giác quan trọng trên mobile, nhưng API là **vendor-specific** (Taptic iOS ≠ Android VibrationEffect ≠ lib bên thứ 3). Nếu call-site gọi trực tiếp API vendor thì đổi lib = sửa cả trăm chỗ. Và **quan trọng hơn**: nếu enum preset mang ngữ nghĩa game (`PickBox`, `OrderCompleted`) thì hệ này không port sang game khác được.

**Use case**
- Rung nhẹ khi tap · "success" khi hoàn thành · "warning" khi sai · "selection" khi kéo qua từng ô.
- Rung **liên tục** khi giữ hành động (kéo, mài, sạc) — bắt đầu/kết thúc theo cặp, có **đếm tham chiếu** vì nhiều nguồn cùng yêu cầu.
- Setting vibration on/off lưu bền.
- Thiết bị không hỗ trợ → no-op im lặng, không throw.

**Mô hình**

| Thành phần | Vai trò |
|---|---|
| `EHapticPreset` | **Vocabulary trung tính**: `Selection · Light · Medium · Heavy · Rigid · Soft · Success · Warning · Failure` |
| `IHapticService` | `Play(preset)` · `PlayCustom(pattern)` · `BeginContinuous/EndContinuous` |
| `HapticPattern` (struct) | `amplitude · frequency · pulseSeconds · pauseSeconds` — cho rung tuỳ biến/liên tục |
| `IHapticSettings` | on/off (giống §8, qua interface) |
| Impl vendor | Map `EHapticPreset` → API cụ thể; **game** chọn lib |

**Contract**

```csharp
public enum EHapticPreset
{
    Selection, Light, Medium, Heavy, Rigid, Soft, Success, Warning, Failure
}

public readonly struct HapticPattern
{
    public readonly float Amplitude;      // 0..1
    public readonly float Frequency;      // 0..1 (thiết bị không đổi tần thì bỏ qua)
    public readonly float PulseSeconds;
    public readonly float PauseSeconds;
}

public interface IHapticService : IService<IHapticService>
{
    bool IsSupported { get; }
    bool IsEnabled { get; set; }
    void Play(EHapticPreset preset);
    void PlayCustom(in HapticPattern pattern);

    void BeginContinuous(in HapticPattern pattern);   // đếm tham chiếu
    void EndContinuous();
    void StopAll();
}
```

**Rung liên tục** — ref-count theo §0.7, khác ở chỗ phải có vòng pulse:

```
BeginContinuous(p): if (++refCount == 1) { cts = new(); PulseLoop(p, cts.Token).Forget(); }
EndContinuous():    if (--refCount <= 0) { refCount = 0; cts.Cancel(); vendor.Stop(); }

PulseLoop(p, ct):   while (!ct.IsCancellationRequested)
                    { vendor.PlayConstant(p); await Delay(p.Pulse + p.Pause, ct); }
```

**Quyết định thiết kế**

| Quyết định | Vì sao | Đánh đổi |
|---|---|---|
| Enum **ngữ nghĩa chung**, không tên hành động game | `Heavy` port được sang mọi game; `PickBox` thì không. Đây là điểm chết người nhất của hệ này | Game phải tự map ngữ cảnh → preset |
| Wrapper mỏng quanh vendor | Đổi lib = sửa 1 file impl | 1 lớp gián tiếp |
| Ref-count cho rung liên tục (§0.7) | Nhiều nguồn cùng yêu cầu rung một lúc | Rò refCount nếu quên `End` → `StopAll()` cứu |
| `IsSupported` → no-op | Thiết bị không rung không được crash | Consumer nên kiểm nếu muốn thay bằng feedback khác |
| `HapticPattern` là struct | Zero-GC, truyền `in` | — |
| Không tự đọc save | Giống §8 — qua interface, không coupling §2 | — |

**Cạm bẫy**
- **Enum game-specific** → hệ mất tính port. Nếu thấy mình muốn thêm `EHapticPreset.Grind`, dừng: đó là mapping của game.
- Rung liên tục không có CTS → loop sống mãi sau khi rời scene.
- Rung mỗi frame trong `Update` → nóng máy, hết pin, và cảm giác nhoè. Rung là **sự kiện**, không phải trạng thái.
- Không tôn trọng setting ở **mọi** entry point → có chỗ rung dù đã tắt. Kiểm ở đầu `Play*` một lần, không rải rác.

**Xong khi:** §0.6 + không có tên gameplay nào trong enum · thiết bị không hỗ trợ vẫn chạy · Begin/End lệch cặp vẫn tắt được rung.

---

## 10. Interactive Button — `Composite`

**Bài toán.** Ba việc **luôn** phải làm ở mọi nút và **luôn** bị quên ở đâu đó: ① chống spam-tap · ② phát âm click · ③ rung click. Cộng thêm ④ scale-press feedback và ⑤ track sự kiện click. Nếu để từng call-site tự lo thì đó là 5 thứ × N nút cơ hội để sai — và spam-tap chính là nguyên nhân gốc của lỗi "mở 2 popup" ở §7.

> **Vì sao vào SDK dù nhỏ:** đây là **điểm chặn duy nhất** biến §8/§9/§12 thành thứ prefab UI không cần biết đến. Không có nó, mọi prefab nút phải nối tay 3 service.

**Use case**
- Nút bất kỳ: bấm → scale xuống 0.9 → nhả → về 1.0, kèm tick + rung nhẹ.
- Cooldown 0.5s giữa 2 lần click cùng nút (chống double-submit).
- Nút "im lặng" (trong tutorial): **không** sfx, **không** rung — cùng một class, khác cấu hình.
- Nút mở URL / đóng view / chuyển tab: biến thể kế thừa, không copy code.
- Nút cần track → thêm feedback tracking, **không** sửa class nút.

**Contract**

```csharp
// Hiệu ứng là component cắm thêm, KHÔNG phải cờ trong nút → thêm loại hiệu ứng
// mới không sửa InteractiveButton (Open/Closed). Tách 2 interface vì press/release
// là một cặp còn click là chuyện khác (ISP).
public interface IButtonPressFeedback { void OnPressed(); void OnReleased(); }
public interface IButtonClickFeedback { void OnClicked(); }

[RequireComponent(typeof(RectTransform))]
public class InteractiveButton : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [SerializeField] private float clickCooldown = 0.5f;

    public bool Interactable { get; set; }
    public event Action Clicked;                    // 1 lần/tap → event là đúng chỗ (§0.4b)

    protected virtual void OnClicked() { }          // điểm mở rộng cho lớp con
}

// SDK cấp sẵn, mỗi cái ~15 dòng, đều dùng IOptionalService (§0.2):
//   ButtonScaleFeedback     : IButtonPressFeedback     — tween scale, unscaled
//   ButtonSfxFeedback       : IButtonClickFeedback     — §8
//   ButtonHapticFeedback    : IButtonClickFeedback     — §9
//   ButtonAnalyticsFeedback : IButtonClickFeedback     — §12, [SerializeField] placement/name
// Biến thể nút: ButtonOpenUrl · ButtonCloseView · ButtonToggle · ButtonTab (§17)
```

**Luồng click**

```
Awake         : press[] = GetComponents<IButtonPressFeedback>()   ← cache 1 lần, click sau không alloc
                click[] = GetComponents<IButtonClickFeedback>()
OnPointerDown : if (!Interactable) return ; foreach press[i].OnPressed()
OnPointerUp   : foreach press[i].OnReleased()
OnPointerClick:
   ├─ if (!Interactable) return
   ├─ if (unscaledTime < lastClick + cooldown) return        ← chống spam
   ├─ lastClick = unscaledTime
   ├─ foreach click[i].OnClicked()        ← sfx · haptic · analytics, mỗi cái tự TryGet service
   └─ OnClicked() ; Clicked?.Invoke()
```

**Quyết định thiết kế**

| Quyết định | Vì sao | Đánh đổi |
|---|---|---|
| Hiệu ứng là **component cắm thêm**, không phải cờ `bool` trong nút | 5 cờ ⇒ nút phải biết 3 service và tự if/else; thêm loại hiệu ứng thứ 4 là sửa class đang chạy ổn (vi phạm S và O). Tách ra: nút chỉ còn "input + cooldown + phát event" | Prefab phải add component, không tick checkbox |
| Cache mảng feedback ở `Awake` | `GetComponents` mỗi click là alloc + duyệt hierarchy | Add component lúc runtime phải gọi lại `Refresh()` |
| Không kế thừa `UnityEngine.UI.Button` | `Button` mang theo `Selectable`/navigation/transition không dùng tới | Mất navigation bàn phím (mobile không cần) |
| Cooldown ở **nút**, `IsBusy` ở container (§7) | Hai lớp phòng thủ ở hai tầng: nút chặn theo thời gian, container chặn theo trạng thái | — |
| Mọi feedback dùng `IOptionalService` | Nút phải chạy trong scene chưa có audio/haptic/analytics (prefab preview, test) | Thiếu service thì im lặng — cố ý |
| Tween ở `unscaledTime` | Nút trong popup pause phải còn phản hồi | — |

**Cạm bẫy**
- Tween scale mà **không** lưu `originalScale` lúc `Awake` → sai nếu prefab có scale ≠ 1.
- Cooldown bằng `Time.time` → sai khi `timeScale = 0`. Dùng `unscaledTime`.
- Kill tween trong `OnDestroy`; `SetActive(false)` giữa lúc animate → tween treo, scale đứng ở 0.9.

**Xong khi:** §0.6 + spam 10 tap chỉ 1 click qua · chạy được trong scene không có audio/haptic/analytics · thêm 1 loại feedback mới không sửa `InteractiveButton`.

---

## 11. Toast & Notification Badge — `Composite`

Hai tính năng nhỏ, khác bản chất, gộp một mục vì cùng là "thông báo không chặn":

- **Toast** — thông điệp tức thời, tự tắt: "Không đủ coin", "+100 coin".
- **Badge (chấm đỏ)** — trạng thái "có gì mới" bám vào nút: chấm đỏ trên Shop khi có offer, trên Daily khi chưa nhận.

### 11a. Toast

**Use case**
- Bắn toast từ **bất cứ đâu** (kể cả tầng logic không có tham chiếu UI) → qua EventBus.
- 3 toast liên tiếp: xếp hàng hoặc xếp chồng, không đè chữ lên nhau.
- Toast tại vị trí thế giới (nổi lên từ item vừa nhặt) hoặc cố định giữa màn.

**Contract**

```csharp
public readonly struct ToastRequest : IEvent          // publish qua EventBus có sẵn
{
    public readonly string MessageKey;                // key localization, KHÔNG chuỗi cứng
    public readonly float  Seconds;
    public readonly Vector3? WorldPosition;           // null = vị trí mặc định
}

public interface IToastService : IService<IToastService>
{
    void Show(string messageKey, float seconds = 1.5f);
    void ShowAt(Vector3 worldPosition, string messageKey, float seconds = 1.5f);
}
```

**Luồng**

```
bất kỳ đâu ──► EventBus.Publish(new ToastRequest{…})
                        │
ToastService (subscribe) ──► pool.Rent<ToastBar>()  ← Object Pool của SDK
                        ──► bar.Show(msg): set text → fade-in + move-up (Tweening §SDK)
                        ──► hết thời gian: fade-out → pool.Return(bar)
                        └─► queue nếu số toast đang hiện ≥ maxConcurrent
```

**Quyết định thiết kế**

| Quyết định | Vì sao | Đánh đổi |
|---|---|---|
| Event-driven (`ToastRequest`) | Tầng logic bắn toast mà không tham chiếu UI (giảm coupling triệt để) | Gián tiếp, khó lần khi debug → log placement |
| Pool `ToastBar` | Toast bắn rất thường xuyên; `Instantiate`/`Destroy` mỗi lần = GC + hitch | Giữ vài instance |
| **Prefab**, không dựng UI bằng code | Dựng `GameObject` + `TextMeshProUGUI` + `LayoutGroup` runtime mỗi toast = hàng chục alloc + `ForceRebuildLayoutImmediate` (rất đắt), lại không reskin được | Cần prefab trong Addressables |
| Queue + `maxConcurrent` | 5 toast cùng lúc = không đọc được gì | Toast sau hiện trễ |
| Nhận **key** localization, không chuỗi đã dịch | Toast bắn từ tầng logic — nơi đó không nên biết ngôn ngữ hiện tại; SDK cũng không sở hữu bảng dịch (Phụ lục C). Giống `textKey` ở §16 | Cần bảng localization của game |

**Cạm bẫy**
- Dựng UI toast bằng code (`new GameObject` + add component + `ContentSizeFitter` + rebuild layout) — đắt, alloc nhiều, không đổi skin được.
- Toast dùng `Destroy` thay pool → GC spike đúng lúc gameplay dồn dập.
- Toast không dùng canvas riêng `sortingOrder` cao → bị popup che.
- Tween không `Kill` khi trả pool → instance tái dùng còn tween cũ.

### 11b. Notification Badge (red-dot)

**Bài toán.** Chấm đỏ là **cây điều kiện**: node lá có điều kiện riêng ("chưa nhận daily"), node cha đỏ nếu **bất kỳ** con đỏ (nút Shop đỏ vì tab Offer bên trong đỏ). Tính lại toàn cây mỗi frame là phí; tính 1 lần lúc `Start()` thì không cập nhật.

**Contract**

```csharp
public interface INotificationCondition
{
    bool HasNotification { get; }
    event Action Changed;                  // ← phát khi state nguồn đổi (event-driven)
}

public interface INotificationNode
{
    string Id { get; }
    bool HasNotification { get; }          // = điều kiện của mình ‖ bất kỳ con
    void AddChild(INotificationNode child);
    event Action Changed;
}

public interface INotificationTree : IService<INotificationTree>
{
    void Register(string id, INotificationCondition condition, string parentId = null);
    void Unregister(string id);
    bool HasNotification(string id);
    void Invalidate(string id);            // đánh dấu bẩn, gộp tính lại cuối frame
}

// Component gắn lên nút: bật/tắt icon chấm đỏ theo node
public sealed class NotificationBadge : MonoBehaviour<INotificationTree> { }
```

**Luồng bubble-up**

```
state đổi (nhận daily) ──► condition.Changed
                       ──► tree.Invalidate("daily")
                       ──► đánh dấu bẩn "daily" + mọi tổ tiên
                       ──► cuối frame (LastUpdate): tính lại CHỈ node bẩn từ lá lên gốc
                       ──► node nào đổi giá trị → phát Changed → badge.SetActive()
```

**Quyết định thiết kế**

| Quyết định | Vì sao | Đánh đổi |
|---|---|---|
| Event-driven + dirty, gộp cuối frame | Polling toàn cây mỗi frame là phí thuần; nhiều `Invalidate` trong 1 frame chỉ tính 1 lần | Trễ 1 frame (không ai thấy) |
| Id/parentId là `string` | Node cha/con đăng ký rời rạc từ nhiều module biên dịch độc lập (§20) ⇒ đúng ca "định danh ổn định ra ngoài" của §0.4b, không phải khoá logic. Khai `const string`, không gõ tay ở call-site | Sai id thì cây rời rạc mà không lỗi → `Register` phải log khi `parentId` chưa tồn tại |
| Điều kiện là interface do game/module cấp | SDK không biết "chưa nhận daily" nghĩa là gì | — |
| Tính từ **lá lên gốc** | Cha phụ thuộc con; ngược lại là tính 2 lần | — |

**Cạm bẫy**
- Polling `Time.frameCount % 60` để refresh badge: rẻ hơn mỗi frame nhưng vẫn là polling, và trễ tới 1 giây. Event-driven + dirty đúng hơn và rẻ hơn.
- Node không `Unregister` khi module bị dỡ (§20) → cây giữ reference chết, badge đỏ vĩnh viễn.
- Vòng lặp cha–con (A là cha B, B là cha A) → stack overflow. Kiểm chu trình lúc `Register`.

**Xong khi:** §0.6 + zero alloc/giây khi không có gì đổi · badge cập nhật đúng 1 frame sau khi state đổi · toast liên tiếp không đè chữ.

---

## 12. Analytics — contract + taxonomy — `Foundation`

**Bài toán.** Analytics là nơi coupling vendor nặng nhất và cũng là nơi **string bừa bãi** gây hại nhất (typo tên event = mất dữ liệu, phát hiện sau 2 tuần). SDK **chỉ** nên sở hữu **contract + hình dạng event + composite/filter**. Impl (Firebase/Adjust/…) và **key cụ thể** thuộc game.

**Use case**
- Log `level_track` với `action_type`/`action_name`/`level`/`result`/`duration`/`is_use_booster`…
- Một event → **nhiều** backend đồng thời (Firebase + Adjust + custom server).
- Event chỉ được log **1 lần trong đời** (first_open, tutorial_done) vs mỗi lần.
- Đặt user property (segment, scenario) để phân tích sau.
- Tắt hẳn tracking khi build dev; whitelist/blacklist event khi cần giảm quota.

**Mô hình**

| Thành phần | Vai trò |
|---|---|
| `IAnalyticsEvent` | Hình dạng 1 event: `EventName` + cách xuất tham số. Event là `readonly struct` |
| `Track()` extension | `evt.Track()` — tự tìm service, tự bỏ qua nếu chưa init |
| `ITracker` | **Một** backend. Impl thuộc game |
| `IAnalyticsService` | Composite: fan-out tới mọi `ITracker`, áp filter |
| `IEventFilter` | `bool IsAllowed(string eventName)` — dedup once-per-lifetime, quota, dev-off |
| Fluent `With…` | `WithLevel(n).WithDuration(s)` — trả **bản copy** của struct, không mutate |

**Contract**

```csharp
public interface IAnalyticsEvent
{
    string EventName { get; }
    void WriteParameters(IAnalyticsParameterWriter writer);   // tránh Dictionary alloc
}

public interface IAnalyticsParameterWriter
{
    void Write(string key, string value);
    void Write(string key, long value);
    void Write(string key, double value);
}

public interface ITracker
{
    string Id { get; }
    bool IsReady { get; }
    void Log<TEvent>(in TEvent evt) where TEvent : struct, IAnalyticsEvent;
    void SetUserProperty(string key, string value);
}

public interface IEventFilter { bool IsAllowed(string eventName); }

public interface IAnalyticsService : IService<IAnalyticsService>
{
    void AddTracker(ITracker tracker);
    void AddFilter(IEventFilter filter);
    void Log<TEvent>(in TEvent evt) where TEvent : struct, IAnalyticsEvent;   // filter + fan-out
    void SetUserProperty(string key, string value);
}

public static class AnalyticsEventExtensions
{
    // Ràng buộc `struct` + `in` ⇒ gọi WriteParameters qua constrained-call: KHÔNG boxing.
    public static void Track<TEvent>(this in TEvent evt) where TEvent : struct, IAnalyticsEvent
    {
        if (IAnalyticsService.TryGet(out var s)) s.Log(evt);
    }
}

// Event của game (không ở SDK): readonly struct, With… trả bản copy
public readonly struct Event_LevelTrack : IAnalyticsEvent
{
    public string EventName => Names.LevelTrack;             // const string
    public Event_LevelTrack WithDuration(int seconds);
    public void WriteParameters(IAnalyticsParameterWriter w);
}
// dùng: new Event_LevelTrack(level, ELevelResult.Win).WithDuration(30).Track();
```

**Luồng**

```
gameplay ──► new Event_LevelTrack(level, Win).WithDuration(30).Track()
                     │
IAnalyticsService.Log ─┬─ for i in filters:  !IsAllowed(name) → return    ← for, KHÔNG LINQ (§0.4a)
                       ├─ for i in trackers: try { Log(in evt) } catch { log, đi tiếp }
                       └─ tracker chưa IsReady → buffer (boxing 1 lần, cold path), flush khi ready
```

**Quyết định thiết kế**

| Quyết định | Vì sao | Đánh đổi |
|---|---|---|
| **Chỉ** extract contract, không extract impl | Impl coupling vendor + key riêng từng game → SDK-hoá là nợ, không phải tài sản | Game phải viết impl (ngắn) |
| Event là **type có kiểu**, không `Log(string, Dictionary)` | Typo bị compiler bắt; refactor rename được; enum giá trị hợp lệ | Nhiều file event |
| `WriteParameters(writer)` thay `Dictionary` | `Dictionary` mới mỗi event = GC ở nơi log rất dày | Verbose hơn |
| `Log<TEvent>(in TEvent) where TEvent : struct` | Đây là chỗ **duy nhất** khiến event zero-alloc thật: ràng buộc `struct` cho phép constrained-call ⇒ không boxing khi gọi `WriteParameters`. Nhận `IAnalyticsEvent` thì mỗi lần log là 1 lần boxing, ở nơi log dày nhất | Mỗi type event sinh một bản generic riêng (IL lớn hơn); nhánh buffer vẫn phải box 1 lần |
| Fluent `With…` trả **copy**, không `this` | Struct mutable dùng chung là nguồn sai dữ liệu khi log lồng/async; copy 1 struct nhỏ rẻ hơn nhiều một lần alloc | Không tái dùng được instance (không cần) |
| Composite + try/catch **từng** tracker | 1 backend lỗi/chưa init không được chặn backend còn lại | Lỗi bị nuốt → phải log |
| Buffer khi tracker chưa ready | Event lúc cold start (first_open) quý nhất mà backend chưa init xong | Bộ đệm có giới hạn, phải drop có log |
| Filter là interface, cắm nhiều cái | Once-per-lifetime, dev-off, quota là 3 mối quan tâm khác nhau (I trong SOLID) | — |

**Cạm bẫy**
- `Log("level_" + n, …)` — string concat trong hot path + tên event động = dashboard không gộp được. Tên event **hằng**, số vào **tham số**.
- Enum giá trị serialize ra `int` trong khi dashboard chờ string (`_win`/`_lose`) → dữ liệu vô nghĩa. Quy ước rõ: enum member đặt tên đúng chuỗi cần gửi, xuất bằng `nameof`/`ToString()` đã cache.
- Event once-per-lifetime lưu cờ trong RAM → reinstall/relaunch log lại. Cờ phải nằm ở §2.
- Event khai là `class` (hoặc nhận qua `IAnalyticsEvent`) → alloc/boxing mỗi lần log. `readonly struct` + generic là bắt buộc, không phải tuỳ chọn.

**Xong khi:** §0.6 + không có tên vendor nào trong SDK · log 1000 event không alloc dictionary · 1 tracker throw không chặn tracker khác.

---

## 13. Monetization Boundary (Ads / IAP) — `Foundation`

**Bài toán.** SDK ads/IAP của bên thứ 3 là thứ **đổi thường xuyên nhất** (đổi mediation, thêm network, đổi lib IAP) và **tangled nhất** (impl hay thò tay vào UI/toast/liveops). Cách duy nhất giữ được game sạch: cô lập chúng **sau contract của mình**, và **chỉ** đưa contract vào SDK.

**Use case**
- `ShowRewarded(placement, onReward, onFail)` · `ShowInterstitial(placement, onClose)` · banner show/hide/height.
- Đổi vendor / thêm network → chỉ sửa impl, game không biết.
- Remove-ads (IAP) tắt banner + inter, giữ rewarded.
- IAP: mua, khôi phục, lấy giá đã format theo tiền tệ máy.
- Gate theo level (banner từ level N, inter từ level M) và theo **pacing** (§21 xây trên đây).
- Anti-cheat / device-id / IDFA cho attribution.

**Mô hình — interface segregation triệt để**

| Interface | Trách nhiệm | Vì sao tách |
|---|---|---|
| `IRewardedAds` | rewarded | Nhiều UI chỉ cần rewarded; không nên thấy banner API |
| `IInterstitialAds` | inter + `ForceCompleteInterval()` | Pacing chỉ liên quan inter |
| `IBannerAds` | show/hide/`HeightPixels` + `HeightChanged` | §6 cần đúng chiều cao, không cần gì khác |
| `IRemoveAdsService` | trạng thái + kích hoạt | Nhiều hệ chỉ cần hỏi "đã mua chưa" |
| `IIapService` | mua/khôi phục/giá | — |
| `IAdPlacement` | id + loại + gate level | Placement là **dữ liệu**, không hardcode string ở call-site |
| `IAttributionService` | Adjust/campaign/device-id | Tách khỏi ads |
| `IAntiCheatService` | phát hiện can thiệp | Tách hẳn |

**Contract**

```csharp
public enum EAdType { Rewarded, Interstitial, Banner, AppOpen }

public readonly struct AdPlacement                 // struct, zero-GC
{
    public readonly string Id;                     // "revive", "x2_coin", "level_end"
    public readonly EAdType Type;
}

public interface IRewardedAds : IService<IRewardedAds>
{
    bool IsReady { get; }
    void Show(in AdPlacement placement, Action onRewarded, Action onFailed = null);
    UniTask<bool> ShowAsync(AdPlacement placement, CancellationToken ct = default);
}

public interface IInterstitialAds : IService<IInterstitialAds>
{
    bool IsReady { get; }
    bool CanShow(in AdPlacement placement);        // gate + pacing (§21 quyết định)
    void Show(in AdPlacement placement, Action onClosed = null);
    void ForceCompleteInterval();                  // cho phép show ngay lần tới
}

public interface IBannerAds : IService<IBannerAds>
{
    bool IsVisible { get; }
    float HeightPixels { get; }                    // §6 IScreenInsetProvider lấy từ đây
    event Action HeightChanged;
    void Show();
    void Hide();
    void Destroy();
}

public interface IRemoveAdsService : IService<IRemoveAdsService>
{
    bool IsRemoved { get; }
    void Apply();                                  // sau IAP hoặc reward
    event Action Changed;
}

public interface IIapService : IService<IIapService>
{
    bool IsInitialized { get; }
    string GetLocalizedPrice(string sku);
    UniTask<bool> PurchaseAsync(string sku, CancellationToken ct = default);
    UniTask RestoreAsync(CancellationToken ct = default);
    event Action<string> PurchaseSucceeded;        // sku
}
```

**Quyết định thiết kế**

| Quyết định | Vì sao | Đánh đổi |
|---|---|---|
| **Chỉ** interface vào SDK, impl per-game | Impl vendor đổi liên tục và luôn tangled; SDK-hoá impl là nhận nợ | Mỗi game viết impl |
| Tách 5+ interface nhỏ thay 1 `IAdsService` to | Consumer chỉ phụ thuộc đúng phần nó dùng (I trong SOLID); test/mock dễ | Nhiều file interface |
| `AdPlacement` là struct dữ liệu | String rải rác = typo + không đối soát được với dashboard | Cần bảng placement |
| `CanShow` **tách khỏi** `Show` | UI cần biết trước để ẩn/hiện nút; và §21 gắn rule pacing vào đúng `CanShow` | Consumer phải hỏi 2 bước |
| Cả callback **và** `UniTask` overload | Callback cho code UI cũ; `await` cho flow tuần tự | 2 API cùng nghĩa |
| Banner lộ `HeightPixels` + event | §6 cần đúng số này; hardcode 150px là sai trên tablet | — |
| `IRemoveAdsService` riêng, có event | Rất nhiều hệ cần hỏi/nghe trạng thái này | — |

**Cạm bẫy**
- Impl ads gọi trực tiếp vào UI (mở toast, đóng popup, cộng reward) → không port, không test. Impl **chỉ** được phát callback/event; ai muốn làm gì thì subscribe.
- Rewarded `onRewarded` gọi 2 lần (một số mediation callback trùng) → cấp thưởng 2 lần. Impl phải guard idempotent theo phiên show.
- Không xử lý nhánh **fail/không có mạng** → người chơi bấm rồi treo. `onFailed` là bắt buộc, không optional.
- Gate level hardcode trong code → không A/B được. Ngưỡng phải từ remote config (§21).
- `Show` khi đang có popup/transition → chồng UI. Kết hợp `IInteractionBlocker` (§3).

**Xong khi:** §0.6 + không có tên vendor trong SDK · mock impl chạy được toàn bộ flow game · rewarded fail không treo UI · banner height đúng trên tablet.

---

# TẦNG 3 — Puzzle / F2P features

## 4b. Time Service (server time + countdown) — `Foundation` · *tách từ mục Time & Ticker cũ; làm ngay trước Economy (§14)*

> **Nhãn bản chất (re-verify 2026-08-29): thiết kế mới, không phải extract.** Khảo sát cũ tin "Goods-Jam
> trọn vẹn nhất"; sự thật: **0/4 repo có server-time offset** — Goods-Jam `TimeManager.Now => DateTime.UtcNow`
> thuần (311 LOC 4 file), không chống chỉnh giờ; water-flow/foods_jam chỉ có static UtcNow helper;
> color-loop lưu `UtcTicks` thẳng trong `GameData`. Consumer thật đầu tiên là Lives/Daily của Economy
> (§14) → user quyết chuyển hệ này xuống Tầng 3, làm ngay trước Economy. **Phần nguồn tick (`ITicker`,
> 3 nhịp) đã tách lên Tầng 1 — mục 4a**; mục này chỉ còn phần *giờ/lịch*.

**Bài toán.** Nguồn thời gian **tin được** — chống người chơi tua đồng hồ máy để nhận thưởng sớm — và bộ
countdown/format dùng chung cho mọi tính năng theo thời gian.

**Use case**
- Lives refill sau X phút · daily reset nửa đêm · event live-ops đếm ngược · cooldown booster.
- Chống cheat: tua giờ máy tiến 1 ngày không được nhận daily.
- App background 3 tiếng → resume phải resync giờ (chống tua giờ lúc background); `offlineSeconds` do
  ticker (4a) phát, nguồn giờ lấy từ đây.
- Đếm ngược chạy cả khi `timeScale = 0` (qua nhịp unscaled của 4a).

**Mô hình**

| Thành phần | Vai trò |
|---|---|
| `ITimeService` | `UtcNow` đã hiệu chỉnh = deviceUtc + offset; offset = serverTime − deviceTime |
| `IServerTimeProvider` | *(optional — §0.2)* nguồn giờ thật: Firebase/GameServer/HTTP `Date` header |
| `Countdown` (struct) | Đếm tới `endUnixSeconds`, phát `Finished`; **không** MonoBehaviour |
| `CountdownLabel` | Binding UI: `Countdown` → text qua nhịp 1 Hz của 4a; **chỉ** format, không giữ logic |
| `TimeFormatter` | `long seconds → string` (auto-detect `d/h/m/s`) + cờ warning; **zero-GC** |

**Contract**

```csharp
public interface ITimeService : IService<ITimeService>
{
    DateTime UtcNow { get; }            // đã + offset
    long UtcNowUnixSeconds { get; }
    bool IsServerTimeTrusted { get; }   // false = đang fallback device clock
    TimeSpan ServerOffset { get; }
    UniTask ResyncAsync(CancellationToken ct);      // gọi ở resume + sau khi có mạng
    event Action OffsetChanged;
}

public interface IServerTimeProvider : IOptionalService<IServerTimeProvider>
{
    UniTask<DateTime?> FetchUtcNowAsync(CancellationToken ct);   // null = không lấy được
}
```

**Luồng**

```
Bootstrap ──► IServerTimeProvider?.FetchUtcNowAsync()
               ├─ ok    → offset = server − deviceUtc ; IsServerTimeTrusted = true
               └─ fail  → offset = 0 ; IsServerTimeTrusted = false  (log + chặn cấp thưởng nhạy cảm)

OnApplicationPause(false)  ← resume (nhịp pause của 4a)
   ├─ ResyncAsync()                             ← chống tua giờ lúc background
   └─ consumer nhận offlineSeconds từ 4a        → §14 refill lives, §20 trừ timer event
```

**Quyết định thiết kế**

| Quyết định | Vì sao | Đánh đổi |
|---|---|---|
| Offset thay vì lưu server time | Fetch 1 lần rồi cộng offset; không gọi mạng mỗi query | Drift nếu device clock chạy sai tốc độ (rất nhỏ) |
| `IsServerTimeTrusted` **lộ ra ngoài** | Offline phải chơi được, nhưng **không** cấp thưởng theo giờ khi chưa tin được giờ. Consumer tự quyết | Consumer xử lý 2 nhánh |
| `Countdown` là **struct**, không MonoBehaviour | Countdown là dữ liệu; zero-GC, dùng được trong logic thuần | Phải có chủ sở hữu gọi tick |
| Mốc epoch cố định (Unix 1970 UTC) | Lưu `long` seconds gọn, so sánh rẻ, không lệ thuộc timezone | Mọi API là UTC — đổi sang local chỉ ở tầng hiển thị |

**Cạm bẫy**
- **Tin device clock cho phần thưởng** = cheat trong 10 giây. Mọi cấp thưởng theo giờ phải kiểm `IsServerTimeTrusted`.
- Không resync khi resume → tua giờ lúc background vẫn ăn được thưởng.
- Countdown dựa `deltaTime` tích luỹ (thay vì `endTime − now`) → sai dần và **sai hẳn** sau background.
- `DateTime.Now` (local) lẫn với `UtcNow` → sai theo timezone. **Chỉ dùng UTC** trong toàn hệ.
- Mỗi UI một `Update()` + `string.Format` mỗi frame — đã có 4a + `TimeFormatter` zero-GC, đừng tự chế lại.

**Xong khi:** §0.6 + tua giờ máy tiến/lùi không đổi được kết quả countdown · resume sau 3h resync xong và
consumer nhận đúng `offlineSeconds` (phối hợp 4a) · countdown label không sinh GC alloc/giây (phối hợp
4a + `TimeFormatter`).

---

## 14. Economy: Currency · Lives · Reward — `Composite`

Ba module riêng biệt nhưng **cùng một khuôn**: `Balance + Config + SaveUnit + UI binding`. Gộp một mục vì chúng dùng chung khuôn đó và Reward là nơi cả ba gặp nhau.

**Khuôn chung**

```
IXxxService  (đọc/ghi + event Changed)
      ├── XxxConfig      (SO hoặc remote: max, giá, interval)
      ├── XxxSaveUnit    (§2 — save-unit độc lập của riêng module)
      └── UI binding     (component subscribe Changed → cập nhật text/bar; KHÔNG giữ state)
```

### 14a. Currency

**Use case**: kiếm/tiêu coin · thanh coin + animation coin bay vào ví · kiểm "đủ tiền không", thiếu thì toast + mở shop · mọi giao dịch có `placement` để đối soát với §12.

```csharp
public interface ICurrencyService : IService<ICurrencyService>
{
    int Balance { get; }
    void Add(int amount, string placement);
    void Consume(int amount, string placement);
    bool TryConsume(int amount, string placement);        // false = không đủ (KHÔNG tự mở UI)
    event Action<int, int> Changed;                       // (cũ, mới) → UI tween từ cũ→mới
}

public interface ICurrencyCollector                        // animation gom coin
{
    UniTask CollectAsync(int amount, Vector2 screenPoint, string placement,
                         CancellationToken ct = default);
}
```

**Quyết định:** `TryConsume` **không** tự mở toast/shop. Lý do: hành vi "thiếu tiền thì làm gì" khác nhau theo ngữ cảnh (im lặng ở nơi này, mở shop ở nơi khác) và nếu nhồi vào service thì service phụ thuộc §7 + §11. Đặt helper ở tầng game:

```csharp
// tầng game, KHÔNG trong SDK
static bool RequireCoin(int amount, string placement)
{
    if (ICurrencyService.Service.TryConsume(amount, placement)) return true;
    IToastService.Service.Show(LocKeys.NotEnoughCoin);
    IShopService.Service.Open();
    return false;
}
```

**Cạm bẫy:** `Changed(cũ, mới)` là bắt buộc — UI cần **cả hai** để tween số nhảy; chỉ có "mới" thì phải tự cache, mỗi UI cache một bản → lệch nhau.

### 14b. Lives / Energy

**Bài toán.** Lives là **hàm của thời gian**, không phải biến độc lập: khi app đóng 2 giờ thì lúc mở lại phải đã hồi đủ. Nên **không** lưu "còn mấy tim" đơn thuần, mà lưu `(count, lastRefillUtc)` rồi **suy ra**.

**Use case**: mất 1 tim khi thua · hồi 1 tim mỗi X phút, trần Y · "vô hạn tim" trong khoảng thời gian (từ IAP/event) · popup đề nghị nạp tim khi hết · đếm ngược "tim tiếp theo sau 04:12".

```csharp
public enum ELiveState { NotEnough, Enough, Full, Infinity }

public interface ILivesService : IService<ILivesService>
{
    ELiveState State { get; }
    int Count { get; }
    int MissingToMax { get; }
    bool HasAny { get; }                        // = Infinity ‖ Count ≥ 1
    bool IsFull { get; }
    TimeSpan TimeToNext { get; }

    bool IsInfinity { get; }
    TimeSpan InfinityTimeLeft { get; }
    void GrantInfinity(TimeSpan duration);      // cộng dồn nếu đang infinity

    void Add(int amount);
    void Consume();
    void RefillToMax();
    event Action Changed;
}
```

**Thuật toán hồi tim** (chạy mỗi giây qua §4 `ITicker`, **và** một lần khi resume với `offlineSeconds`):

```
now = ITimeService.UtcNow                      ← KHÔNG dùng device clock trực tiếp (§4)
while (now − lastRefillUtc > interval  &&  !IsFull)
{
    count++;  Clamp();
    lastRefillUtc += interval;                 ← CỘNG DỒN interval, không gán = now
}
if (IsFull) lastRefillUtc = now;               ← đầy thì không tích luỹ nợ
timeToNext = lastRefillUtc + interval − now;
```

| Chi tiết | Vì sao |
|---|---|
| `lastRefillUtc += interval` (không `= now`) | Offline 3 giờ với interval 30 phút phải hồi **6** tim, không phải 1. Gán `= now` là mất phần dư |
| `if (IsFull) lastRefillUtc = now` | Nếu không, khi đầy vẫn tích luỹ "nợ" → tiêu 1 tim là hồi lại tức thì |
| `Clamp()` mỗi lần đổi | Trần có thể **giảm** do remote config → phải kẹp lại, không để tồn tim lậu |
| Infinity lưu **thời điểm hết**, không "còn bao lâu" | "Còn bao lâu" phải tick liên tục và sai sau khi offline |
| Trần từ config, không hằng số | A/B test max lives là đòn tune retention phổ biến |

**Cạm bẫy:** kiểm `IsInfinity` bằng `DateTime.Now` (local) trong khi lưu UTC → sai đúng bằng timezone (§0.7).

### 14c. Reward

**Bài toán.** "Thưởng" xuất hiện khắp nơi (win level, daily, chest, event, IAP, rewarded ads) với nhiều loại (coin, booster, tim, remove-ads, item event). Cần **một** kiểu dữ liệu thưởng + **một** đường trao + **nhiều** cách trình bày, và thêm loại mới **không sửa** code đã có.

```csharp
public readonly struct RewardData
{
    public readonly int TypeId;                 // int, KHÔNG enum cứng trong SDK (xem bảng dưới)
    public readonly int Amount;
}

public interface IRewardHandler                 // 1 handler / 1 loại thưởng
{
    int TypeId { get; }
    UniTask GrantAsync(in RewardData reward, Vector2 screenPoint, string placement,
                       CancellationToken ct = default);
}

public interface IRewardService : IService<IRewardService>
{
    void Register(IRewardHandler handler);                       // Open/Closed
    // IReadOnlyList, KHÔNG IEnumerable: RewardData là struct ⇒ foreach qua IEnumerable
    // cấp phát enumerator + box từng phần tử (§0.4b). Async nên không dùng được Span.
    UniTask GrantAsync(IReadOnlyList<RewardData> rewards, Vector2 screenPoint, string placement,
                       CancellationToken ct = default);
    UniTask ShowClaimPopupAsync(string titleKey, IReadOnlyList<RewardData> rewards, string placement,
                                CancellationToken ct = default);
    UniTask FlyUpAsync(IReadOnlyList<RewardData> rewards, Vector2 screenPoint, string placement,
                       float sizeScale = 1f, bool randomOffset = false, bool grant = true,
                       CancellationToken ct = default);
}

public interface IRewardIconProvider : IService<IRewardIconProvider>
{
    Sprite GetIcon(in RewardData reward);
    string FormatAmount(in RewardData reward);   // "x3" · "1200" · "30m" (infinity lives)
}
```

| Quyết định | Vì sao | Đánh đổi |
|---|---|---|
| **Registry handler** thay `switch (type)` | `switch` = mỗi loại thưởng mới phải sửa file trung tâm (vi phạm Open/Closed) và file đó dần phụ thuộc mọi hệ | Phải đăng ký handler lúc bootstrap |
| `TypeId` là `int` (không enum trong SDK) | Enum trong SDK = danh sách loại thưởng bị SDK khoá; module event (§20) không thêm loại riêng được | Mất type-safety → game định nghĩa enum riêng rồi cast, và log lỗi khi không có handler |
| Icon/format qua `IRewardIconProvider` | SDK không giữ asset; game/module cấp sprite | 1 lớp gián tiếp |
| 3 cách trình bày tách rời (`Grant` · `ClaimPopup` · `FlyUp`) | Grant thầm lặng ≠ popup long trọng ≠ bay vào ví. Ép 1 API là mất kiểm soát UX | 3 entry point |
| `FlyUpAsync(grant: false)` | Có lúc cần **chỉ** animation (đã cộng trước, hoặc preview) | Dễ dùng sai → tên tham số phải rõ |

**Cạm bẫy**
- Không có handler cho `TypeId` → thưởng **mất im lặng**. Bắt buộc log error (và ở dev thì throw).
- Cộng thưởng **trong** animation fly-up: animation bị cancel giữa đường (đổi scene) = mất thưởng. Cộng **trước**, animation chỉ là hình ảnh — hoặc cộng ở `finally`.
- Fly-up 50 icon cùng lúc không pool → GC spike ngay khoảnh khắc "sướng nhất". Pool icon.
- Reward state gom vào blob chung với economy → xem §0.3.

**Xong khi:** §0.6 + thêm loại thưởng mới không sửa file SDK nào · offline 3h hồi đúng số tim · đổi scene giữa fly-up không mất thưởng · 3 module economy là 3 save-unit độc lập.

---

## 15. Level Library (runtime) — `Composite`

**Bài toán.** Quản lý **catalog level lúc runtime**: nạp cấu hình level N, xử lý khi vượt số level có sẵn, cho phép remote override để A/B test, gắn nhãn độ khó, và phân phối nội dung theo trọng số — tất cả **mà không** biết format level của game (mỗi game một schema hoàn toàn khác).

**Use case**
- `GetConfig(levelIndex)` → cấu hình level.
- Người chơi vượt level cuối → **loop** từ mốc L trở đi thay vì hết game (`index = loopStart + (n − loopStart) mod cycle`), và loop phải **tất định** theo level (cùng level ⇒ cùng nội dung, mọi thiết bị).
- Remote override: đổi bộ level / thứ tự / nội dung **không update app** (A/B test).
- Nhiều "collection" level song song, remote chọn collection nào.
- Level đóng gói nén (payload lớn) → giải nén khi nạp.
- Nhãn độ khó (Easy/Normal/Hard/Boss) để §19 và UI dùng.
- **Distribution**: sinh danh sách phần tử/mục tiêu theo trọng số hoặc chia đều, **bake trước**, không random lúc chơi.

**Mô hình**

| Thành phần | Vai trò |
|---|---|
| `ILevelConfig` | **Marker rỗng** — game khai schema của nó |
| `ILevelLibrary<TConfig>` | `GetConfig(level)` + loop + override; generic theo schema game |
| `LevelDatabase<TConfig>` (SO) | Mảng config + `loopStartLevel`; nguồn cục bộ |
| `ILevelSource<TConfig>` | Nguồn nạp: SO cục bộ · remote payload · streaming asset — cắm được |
| `ILevelCollectionSelector` | Remote chọn collection id |
| `IDifficultyTagProvider` | `level → EDifficultyTag` |
| `IDistributionStrategy` | `Bake(input) → (elements, targets)`; strategy cắm được |
| `ILevelProgressData` | *(§2)* level hiện tại, level cao nhất, số lần thử |

**Contract**

```csharp
public interface ILevelConfig { }                      // game tự khai field

public interface ILevelLibrary<TConfig> : IService<ILevelLibrary<TConfig>>
    where TConfig : class, ILevelConfig
{
    int Count { get; }
    TConfig GetConfig(int levelIndex);                 // 1-based; tự xử lý vượt trần
    int ResolveSourceIndex(int levelIndex);            // level → index thật (sau loop)
    UniTask ReloadAsync(CancellationToken ct = default);
    void SetOverride(TConfig config);                  // test/cheat/remote-1-level
    void ClearOverride();
}

public interface ILevelSource<TConfig> where TConfig : class, ILevelConfig
{
    int Priority { get; }                              // cao thắng: remote > local
    UniTask<IReadOnlyList<TConfig>> LoadAsync(CancellationToken ct);
}

public interface ILevelCollectionSelector : IService<ILevelCollectionSelector>
{
    int CollectionId { get; }                          // từ remote config
    event Action Changed;
}

public enum EDifficultyTag { Tutorial, Easy, Normal, Hard, VeryHard, Boss }

public interface IDifficultyTagProvider : IService<IDifficultyTagProvider>
{
    EDifficultyTag GetTag(int levelIndex);
}

public readonly struct DistributionInput<TElement>
{
    public readonly TElement[] Pool;
    public readonly int TotalTargets;
}

public interface IDistributionStrategy<TElement>
{
    void Bake(in DistributionInput<TElement> input,
              List<TElement> outElements, List<TElement> outTargets);   // buffer do caller cấp
}
```

**Công thức loop**

```
loopStart = clamp(loopStartLevel − 1, 0, Count − 1)      ← 0-based
cycle     = Count − loopStart
index     = level ≤ Count ? level − 1
                          : loopStart + ((level − 1 − loopStart) mod cycle)
```

**Kiểm mốc**

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| `Count = 100`, `level = 50` | index = 49 | trong tầm → dùng trực tiếp |
| `Count = 100`, `loopStart = 19` (level 20), `level = 101` | `19 + ((100 − 19) mod 81) = 19 + 0 = 19` | quay đúng về level 20 |
| `level = 181` | `19 + (180 − 19) mod 81 = 19 + 80 = 99` | hết vòng 2 tại level cuối |
| `loopStart = Count − 1` | `cycle = 1` → mọi level vượt trần đều ra level cuối | không chia 0 |

**Luồng nạp**

```
Bootstrap(§1) ──► ReloadAsync
    ├─ sources đã sort giảm dần theo Priority ngay lúc Register  ← sort 1 lần, không LINQ mỗi Reload
    ├─ source đầu tiên trả list non-empty → dùng   (remote thắng local)
    ├─ payload nén? → base64 → decompress → deserialize
    ├─ list rỗng/parse lỗi → fallback source kế tiếp (log warning, KHÔNG throw)
    └─ với mỗi config: strategy.Bake(...) 1 lần   ← bake trước, chơi không random
```

**Quyết định thiết kế**

| Quyết định | Vì sao | Đánh đổi |
|---|---|---|
| `ILevelConfig` là marker rỗng, library generic | Schema level là thứ **khác nhau nhất** giữa các game; SDK định nghĩa field là chết ngay | Game phải khai type, library không đọc được nội dung |
| Nguồn level là **danh sách có Priority** | Remote override, local fallback, streaming — cùng một cơ chế, thêm nguồn không sửa library | Phải nghĩ thứ tự priority |
| Loop bằng modulo có `loopStart` | Level 1–19 là tutorial/onboarding, không nên loop lại; chỉ loop phần "nội dung thường" | Cần chọn `loopStart` đúng |
| Loop **tất định** theo level index | Random theo seed thay đổi ⇒ cùng level 2 thiết bị khác nội dung ⇒ không so sánh/không debug được | Người chơi nhận ra chu kỳ |
| Distribution **bake trước** | Random lúc chơi = không tái lập được bug, và alloc giữa gameplay | Bake tốn thời gian lúc nạp |
| Buffer `outElements/outTargets` do caller cấp | Strategy không alloc list mới mỗi lần bake (zero-GC) | API hơi verbose |
| Difficulty tag là **provider riêng**, không field trong config | §19 và UI cần tag; nhồi vào config buộc mọi game phải có field đó | 1 service nữa |

**Cạm bẫy**
- **Trộn format vào manager**: manager biết "ô", "màu", "mechanic" → không port được sang game khác. Manager chỉ được biết `GetConfig(level)`.
- Đặt `Random.InitState(seed)` để loop tất định rồi **quên trả lại** trạng thái random toàn cục → mọi random sau đó bị lệch. Dùng instance `System.Random` riêng, đừng chạm random toàn cục.
- Giải nén/deserialize trên main thread lúc chuyển scene → hitch. Làm trong bootstrap hoặc `UniTask.RunOnThreadPool`.
- Override level (cheat/test) không được clear → build release vẫn bị ghim 1 level.
- Config là SO **mutable** mà bake ghi thẳng vào SO → dirty asset trong editor, và state rò giữa các lần chơi. Bake ra buffer runtime.

**Xong khi:** §0.6 + không có tên mechanic nào trong library · level 101 với `loopStart=20` ra đúng level 20 · remote payload lỗi vẫn chơi được bằng local · bake không alloc sau lần đầu.

---

## 16. Tutorial / FTUE — `Composite`

**Bài toán.** Tutorial là **chuỗi bước**, mỗi bước = "hiện gợi ý + chờ một điều kiện". Điều kiện thì đa dạng (tap bất kỳ · tap đúng nút · một hành động gameplay xảy ra · chờ hết thời gian). Nếu viết tay từng tutorial thì mỗi cái là một mớ coroutine không tái dùng; và nếu SDK biết "hành động gameplay" là gì thì mất tính port.

**Use case**
- Bước 1: tay chỉ vào ô, dim phần còn lại, chờ tap. Bước 2: highlight nút, chờ bấm đúng nút đó. Bước 3: chờ "đã ghép xong 1 cặp".
- Overlay dim che phần khác, chỉ vùng đang dạy nhận được tương tác.
- Hai môi trường: **Canvas** (UI, dùng anchor) và **World** (gameplay, dùng world→screen).
- Nhiều tutorial được kích cùng lúc → **queue**, có cái ưu tiên chen lên đầu.
- Chờ UI home xong animation mới bắt đầu (không dạy khi màn hình đang bay vào).
- Bật/tắt/đổi thứ tự tutorial qua remote config.
- Đã hoàn thành thì không dạy lại (lưu §2).

**Mô hình**

| Thành phần | Vai trò |
|---|---|
| `TutorialStepConfig` | Data thuần: text · có dim không · object cần highlight · vị trí tay · loại điều kiện · callback bắt đầu/kết thúc |
| `ETutorialGate` | Loại điều kiện: `AnyTap` · `TargetButton` · `GameSignal` · `Delay` |
| `ITutorialStep` | `OnStart` · `OnTick(dt)` · `OnEnd`; phát `Completed` |
| `ITutorialStepFactory` | `config → ITutorialStep`; **cắm được** → thêm loại gate không sửa handler |
| `TutorialHandler` (abstract) | Queue step, dim overlay, highlight, hiện tay/text; 2 impl: `Canvas` / `World` |
| `ITutorialService` | Queue nhiều sequence, ưu tiên, `IsBusy`, chặn/mở scroll bên ngoài |
| `ITutorialSignalBus` | **Ranh giới quan trọng**: game phát `Raise(signalId)`; SDK chỉ so id |
| `ITutorialProgressData` | *(§2)* sequence nào đã xong |

**Contract**

```csharp
public enum ETutorialGate { AnyTap, TargetButton, GameSignal, Delay }

// Đích tap của gate TargetButton — interface, KHÔNG `Component`: SDK không được biết
// nút của game là type gì. InteractiveButton (§10) implement sẵn.
public interface ITutorialTapTarget { event Action Tapped; }

[Serializable]
public sealed class TutorialStepConfig
{
    // `[SerializeField] private` + property get-only: config do tác giả điền trong inspector,
    // runtime chỉ ĐỌC. Public field cho phép mọi nơi ghi vào → mất encapsulation.
    [SerializeField] private ETutorialGate gate;
    [SerializeField] private string textKey;                     // key localization
    [SerializeField] private bool showDimmer = true;
    [SerializeField] private List<GameObject> highlightTargets;   // nâng lên trên dimmer
    [SerializeField] private GameObject handTarget;               // anchor, KHÔNG toạ độ cứng
    [SerializeField] private bool mirrorHand;
    [SerializeField] private int requiredTapCount = 1;                         // gate = AnyTap
    [SerializeField] private InterfaceReference<ITutorialTapTarget> tapTarget;  // gate = TargetButton (§A.1)
    [SerializeField] private int[] signalIds;                                  // gate = GameSignal
    [SerializeField] private float delaySeconds;                               // gate = Delay
    [SerializeField] private UnityEvent onStepStarted, onStepEnded;

    public ETutorialGate Gate                        => gate;
    public string TextKey                            => textKey;
    public bool ShowDimmer                           => showDimmer;
    public IReadOnlyList<GameObject> HighlightTargets => highlightTargets;
    public GameObject HandTarget                     => handTarget;
    public bool MirrorHand                           => mirrorHand;
    public int RequiredTapCount                      => requiredTapCount;
    public ITutorialTapTarget TapTarget              => tapTarget.Value;
    public ReadOnlySpan<int> SignalIds               => signalIds;      // duyệt không alloc
    public float DelaySeconds                        => delaySeconds;
    public UnityEvent OnStepStarted                  => onStepStarted;
    public UnityEvent OnStepEnded                    => onStepEnded;
}

public interface ITutorialStep
{
    TutorialStepConfig Config { get; }
    event Action Completed;
    void OnStart();
    void OnTick(float deltaTime);
    void OnEnd();
}

public interface ITutorialStepFactory
{
    bool CanCreate(ETutorialGate gate);
    ITutorialStep Create(TutorialStepConfig config);
}

public interface ITutorialSignalBus : IService<ITutorialSignalBus>
{
    void Raise(int signalId);                  // game gọi khi hành động xảy ra
    event Action<int> Raised;
}

public interface ITutorialService : IService<ITutorialService>
{
    bool IsBusy { get; }
    void Enqueue(TutorialStepConfig[] sequence, ETutorialSurface surface, bool prioritized = false);
    void StopAll();
    void SetText(string content, Transform anchor = null);      // đổi text giữa bước
    void SetHandTarget(GameObject target);
    void Highlight(GameObject target);
    event Action Started, Ended;
}

public enum ETutorialSurface { Canvas, World }
```

**Luồng**

```
Enqueue(sequence, Canvas)
  ├─ nếu đang busy → nằm chờ trong queue (prioritized: chen lên đầu)
  ├─ chờ điều kiện ngoài: UI home hết animation, không có popup mở (§7 IsBusy)
  ├─ handler.Start(sequence): factory tạo step cho từng config → Queue<ITutorialStep>
  └─ vòng bước:
        step.OnStart()  → dim on/off · highlight targets · spawn tay · set text
        ITicker(§4) → step.OnTick(dt)         ← gate kiểm ở đây
        gate thoả → Completed → step.OnEnd() → dọn highlight/tay → bước kế
        hết bước → CleanupAll() → Ended → lưu "đã xong" (§2) → chạy sequence kế trong queue
```

**Quyết định thiết kế**

| Quyết định | Vì sao | Đánh đổi |
|---|---|---|
| **Step + Handler + Factory** thay coroutine tay | Step tái dùng, thứ tự là data; thêm loại gate = thêm factory (Open/Closed) | 3 khái niệm |
| Gate `GameSignal` mang **`int` id**, không enum hành động | Đây là điểm sống–chết của tính port: SDK biết "chờ signal 7", **không** biết 7 là "ghép xong cặp" | Game phải quản bảng id (nên có hằng) |
| `ITutorialSignalBus` là bus riêng, không dùng chung EventBus | Tutorial chỉ cần "một id vừa xảy ra"; đi qua event bus tổng làm nó phụ thuộc mọi event type của game | 1 bus nhỏ nữa |
| Đích tap là `ITutorialTapTarget`, không `Component`/`Button` | `Component` là "type gì cũng được" ⇒ phải `as`/`GetComponent` rồi đoán ở runtime. Interface 1 event là hợp đồng tối thiểu đúng nghĩa (I + D) | Nút phải implement interface (§10 có sẵn) |
| Tay/highlight bám **anchor/transform** | Toạ độ cứng vỡ ngay khi đổi layout hoặc đổi tỉ lệ màn (§6) | Phải có object đích trong scene |
| `textKey` (localization) không chuỗi cứng | Tutorial là nơi chữ nhiều nhất; hardcode = không dịch được | Cần bảng localization của game |
| Queue + cờ `prioritized` | Nhiều nguồn kích tutorial cùng lúc (level win + unlock feature + event) | Ưu tiên sai thứ tự thì rối → chỉ dùng `prioritized` cho ép buộc |
| **Một** hệ tutorial cho cả gameplay và meta/liveops | Hai impl song song là nợ: sửa bug 2 lần, hành vi lệch nhau | Phải trừu tượng `ETutorialSurface` |
| Highlight bằng override sorting + raycaster tạm | Nâng object lên trên dimmer mà vẫn nhận input, không cần đổi parent (đổi parent làm hỏng layout) | Phải dọn sạch component tạm khi xong |
| `OnTick` qua `ITicker` (§4) | Không thêm `Update` riêng | — |

**Cạm bẫy**
- **Hai impl tutorial song song** (gameplay và liveops) — hợp nhất ngay từ đầu.
- Highlight thêm `Canvas`/`GraphicRaycaster` runtime mà không destroy khi xong → object giữ sorting sai vĩnh viễn, và tương tác lạ ở màn khác.
- Bắt đầu tutorial khi UI đang animate hoặc popup đang mở → tay chỉ sai chỗ. Phải chờ (§7 `IsBusy`, hook animation home).
- Step throw giữa chuỗi → tutorial treo, dimmer che vĩnh viễn = app hỏng. `try/catch` quanh `OnStart`/`OnTick`, lỗi thì `StopAll()` và dọn overlay.
- Tutorial chạy trong autoplay/test mode → kẹt bot. Có cờ tắt.
- Quên lưu "đã hoàn thành" → dạy lại mỗi lần mở app.

**Xong khi:** §0.6 + không có enum hành động game nào trong SDK · exception giữa tutorial vẫn dọn được overlay · 2 sequence kích cùng lúc chạy tuần tự đúng · đổi layout không lệch tay chỉ.

---

## 17. Tab Navigation / Scroll-Snap — `Composite`

**Bài toán.** Màn home meta kiểu "3–5 tab dưới, vuốt ngang giữa các trang, snap vào trang" là UX chuẩn nhưng khó làm đúng: phải đồng bộ **hai chiều** giữa tab bar và scroll rect, xử lý fast-swipe vs drag chậm, và nested scroll (danh sách dọc bên trong trang trượt ngang).

**Ưu tiên thấp hơn các hệ khác** — chỉ game có meta-map/home nhiều tab mới cần. Game gameplay-only thì vài nút là đủ; **đừng** ép mọi home vào pattern này.

**Use case**
- Vuốt ngang hoặc tap tab để chuyển trang, snap vào giữa.
- Fast-swipe (nhanh + ngắn) → sang 1 trang; drag chậm → snap về trang gần nhất.
- Con trượt (selector) dưới tab bar chạy mượt theo tiến độ scroll, không nhảy.
- Tab đang chọn phóng to/đổi màu; tab khác thu về.
- Mở home rồi tự nhảy sang tab X (từ deep-link, từ nút "xem shop").
- Nested scroll: list dọc trong trang không được ăn cắp drag ngang.
- Responsive: số tab và bề rộng khác nhau theo màn (§6).

**Mô hình**

| Thành phần | Vai trò |
|---|---|
| `ScrollSnapBehaviour` | Lõi: drag → chọn trang → lerp tới vị trí; phát 4 event |
| `ScrollSnapItem` | Một trang; biết index của mình |
| `ITabBar` / `TabBarItem` | Hiển thị tab + selector; **đồng bộ 2 chiều** với scroll |
| `ITabSwitchFeedback` | Hiệu ứng lúc đổi tab (scale/màu/âm) — cắm được, reskin không sửa lõi |
| `NestedScrollRelay` | Chuyển drag ngược hướng từ scroll con lên scroll cha |
| `ITabNavigator` | API ngoài: `GoTo(index)`, `CurrentIndex`, `Changed` |

**Contract**

```csharp
public interface ITabNavigator : IService<ITabNavigator>
{
    int CurrentIndex { get; }
    int PageCount { get; }
    bool IsSnapping { get; }
    void GoTo(int index, bool animated = true);
    event Action<int> PageChanged;              // chốt trang
    event Action<float> ScrollPercentChanged;   // [0,1] — selector/parallax bám cái này
}

public interface ITabSwitchFeedback
{
    void ApplySelected(TabBarItem item, float weight);   // weight ∈ [0,1] → blend mượt
}
```

**Luồng**

```
OnBeginDrag ──► IsSnapping = false ; ScrollStarted
OnDrag      ──► ScrollPercentChanged(p)  ──► selector.anchoredPosition = Lerp(...)
                                         ──► feedback.ApplySelected(item, weight)   ← blend, không bật/tắt
OnEndDrag   ──► fastSwipe = (Δt < thresholdTime && |Δx| > thresholdDistance)
                 ├─ fastSwipe → target = current ± 1
                 └─ ngược lại → chờ velocity < limit → target = trang gần nhất
            ──► lerp content tới target (decelerationRate) ──► PageChanged(target)
tap tab i   ──► GoTo(i) ──► cùng đường lerp ở trên (một đường code duy nhất)
```

**Quyết định thiết kế**

| Quyết định | Vì sao | Đánh đổi |
|---|---|---|
| Tap tab và vuốt đi **cùng một** đường snap | 2 đường code = 2 hành vi lệch nhau (và 2 chỗ sửa bug) | — |
| `ScrollPercentChanged` là float liên tục, không chỉ event "đổi trang" | Selector và feedback phải **blend** theo tiến độ; bật/tắt theo trang tạo cảm giác giật | Listener phải rẻ (gọi mỗi frame khi drag) |
| Lõi generic + skin qua `ITabSwitchFeedback` | Reskin từng game mà không fork lõi | 1 interface nữa |
| Fast-swipe theo **cả** thời gian và khoảng cách | Chỉ khoảng cách: drag chậm dài cũng bị coi là swipe. Chỉ thời gian: chạm nhẹ cũng đổi trang | 2 ngưỡng phải tune |
| `NestedScrollRelay` tường minh | Unity không tự chuyển drag giữa scroll lồng ngược hướng | Phải khai danh sách scroll con |
| Chờ `velocity < limit` khi không fast-swipe | Snap ngay lúc nhả tay làm mất cảm giác quán tính | Snap trễ hơn |

**Cạm bẫy**
- Lõi được viết bằng **public field** + tooltip (kiểu asset store) — giữ nguyên thì vi phạm encapsulation của SDK. Chuyển sang `[SerializeField] private` + property, và **không** để logic layout trong `Update` khi không drag.
- Tính lại `LayoutRebuilder` mỗi frame khi drag → tụt frame. Bố cục tính 1 lần lúc setup + khi số tab/kích thước đổi.
- Selector dùng `sizeDelta` pixel cứng → sai trên màn khác. Bám anchor của tab tương ứng (§6).
- `Coroutine` setup nhiều `yield return null` để chờ layout ổn định → mong manh. Dùng `Canvas.ForceUpdateCanvases()` một lần rồi tính.
- Trang bên trong tiếp tục chạy logic/animation khi bị cuộn ra ngoài → phí CPU. Tắt theo `PageChanged`.

**Xong khi:** §0.6 + tap tab và vuốt cho cùng kết quả · nested scroll không ăn cắp drag · zero rebuild layout khi idle · đúng trên 4:3 và 20:9.

---

## 18. In-Game Rating — `Composite`

**Bài toán.** Native review chỉ được gọi **giới hạn số lần** trên mỗi máy (OS quota) — nên chỉ được gọi khi khả năng nhận đánh giá tốt là cao. Cách làm chuẩn: **lọc trước** bằng popup của mình (chọn sao), 4–5 sao mới mở native review, dưới đó chuyển sang kênh feedback nội bộ.

**Use case**
- Sau khi win level mốc "vui" (không phải sau khi thua) → hỏi "thích game không?".
- 5 sao → native review; ≤4 sao → popup xin feedback → cảm ơn.
- Đã đánh giá thì không hỏi lại; đã từ chối thì giãn ra (mốc level sau).
- iOS/Android khác API; native review fail → fallback mở store URL.

**Mô hình**

| Thành phần | Vai trò |
|---|---|
| `IRatingFlowService` | `CanAsk()` · `AskAsync()`; điều phối chuỗi popup |
| `IRatingTrigger` | Quyết định "lúc này có nên hỏi không" — **game cấp**, remote-driven |
| `INativeReviewService` | Bọc native review + fallback store URL |
| `IRatingProgressData` | *(§2)* đã rate chưa · đã hỏi mấy lần · lần cuối hỏi ở level nào |
| `RatingStepPopup 1/2/3` | Chọn sao → feedback → cảm ơn (dùng §7) |

**Contract**

```csharp
public interface IRatingTrigger : IService<IRatingTrigger>
{
    bool ShouldAsk(int currentLevel, int askedCount, int lastAskedLevel);
}

public interface INativeReviewService : IService<INativeReviewService>
{
    UniTask<bool> RequestAsync(CancellationToken ct = default);   // false = fail → caller fallback
    void OpenStorePage();
}

public interface IRatingFlowService : IService<IRatingFlowService>
{
    bool HasRated { get; }
    bool CanAsk(int currentLevel);
    UniTask AskAsync(int currentLevel, CancellationToken ct = default);   // hoàn tất khi flow đóng
}
```

**Luồng**

```
AskAsync(level)
  ├─ if (!CanAsk) return
  ├─ askedCount++ ; lastAskedLevel = level      ← lưu NGAY, trước khi hỏi
  ├─ iOS: gọi thẳng native review (Apple đã tự lọc + quota) → xong
  └─ Android:
        Step1 (chọn sao)
          ├─ 5 sao  → HasRated = true → NativeReview.RequestAsync()
          │             └─ fail → OpenStorePage()
          └─ ≤4 sao → Step2 (feedback) → Step3 (cảm ơn)
        đóng ở bất kỳ bước → flow kết thúc, không hỏi lại trong phiên này
```

**Quyết định thiết kế**

| Quyết định | Vì sao | Đánh đổi |
|---|---|---|
| Lọc bằng popup mình trước native | Native review có quota cứng; gọi bừa là tiêu quota vào người chơi đang không vui | Thêm 3 popup |
| Trigger là interface do game/remote cấp | "Lúc nào là lúc vui" tuỳ game và tuỳ A/B test | 1 interface |
| Lưu `askedCount`/`lastAskedLevel` **trước** khi hiện | App bị kill giữa flow không được reset về "chưa hỏi" → spam | Có thể "mất" 1 lượt hỏi |
| iOS đi đường ngắn (gọi native luôn) | Apple đã tự lọc và tự quota; chèn popup của mình là thêm ma sát vô ích | 2 nhánh code |
| `RequestAsync` trả `bool` | Native review có thể fail im lặng (chưa init, hết quota) → phải fallback được | Caller phải xử lý |
| Native review sau **animation đóng popup** | Gọi native lúc popup đang bay ra → 2 lớp UI chồng nhau | Trễ ~0.3s |

**Cạm bẫy**
- **Hỏi sai thời điểm** (sau khi thua, giữa gameplay) → rating thấp, và đó là thiệt hại vĩnh viễn.
- Không tôn trọng "đã hỏi" → spam, người chơi 1 sao vì bị làm phiền.
- Native review fail mà không fallback → nút "đánh giá" không làm gì cả.
- Gọi native review trong editor → không có gì xảy ra, tưởng lỗi. Có nhánh mô phỏng ở editor.

**Xong khi:** §0.6 + không bao giờ hỏi sau khi thua · kill app giữa flow không reset trạng thái · native fail vẫn mở được store · đã rate không hỏi lại.

---

# TẦNG 4 — 💎 Viên ngọc (IP giá trị cao)

## 19. 💎 Adaptive Difficulty — rating kỹ năng Glicko-2

**Loại:** lõi toán = `Foundation` (thuần `double`, không phụ thuộc gì) · tầng áp dụng = `Composite`.
**Phụ thuộc:** §2 (lưu rating), §4 (rating period theo thời gian), §15 (catalog + difficulty tag).

**Bài toán.** Độ khó cố định theo level là sai với **mọi** người chơi: người giỏi thấy nhàm, người kém thấy tường và churn. Muốn điều chỉnh theo kỹ năng thì phải **ước lượng được kỹ năng** — mà kỹ năng không quan sát trực tiếp được, chỉ suy ra từ chuỗi kết quả nhiễu (thắng nhờ may, thua vì mất tập trung). Cần một bộ ước lượng biết **nó đang chắc chắn tới đâu**.

> Đây là phần **cần hiểu sâu trước khi code**. Mục §19.1–§19.6 dẫn từ trực giác tới công thức chốt, không nhảy bước; §19.7 mới là kiến trúc.

### 19.1 Bản chất — kỹ năng là một phân bố, không phải một con số

| Thành phần | Ký hiệu | Vai trò |
|---|---|---|
| Rating | `r` | Ước lượng điểm giữa của kỹ năng (thang hiển thị, gốc 1500) |
| Rating deviation | `RD` | **Độ không chắc chắn** của ước lượng đó (khoảng tin cậy) |
| Volatility | `σ` | Kỹ năng dao động **nhanh** cỡ nào (người mới học, phong độ thất thường) |

Mỗi lần chơi một level = một **"trận đấu"** giữa `player` và `level`: level cũng có `(r, RD, σ)` riêng. Thắng level khó ⇒ kỹ năng cao; thua level dễ ⇒ kỹ năng thấp. Cả hai bên đều có thể được cập nhật.

**Vì sao không dùng Elo.** Elo cập nhật `r' = r + K(s − E)` với `K` **cố định**. Nghĩa là: người mới (chưa biết gì về họ) và người chơi 500 màn (biết rất rõ) nhận bước nhảy **bằng nhau**. Sai cả hai đầu — người mới cần hội tụ nhanh, người cũ cần ổn định. Glicko-2 sửa đúng chỗ đó: **bước nhảy tỉ lệ với độ không chắc chắn**, và độ không chắc chắn **tự phình ra khi không chơi** (mô hình hoá "lâu rồi không biết họ còn giỏi không").

| | Elo | Glicko-2 |
|---|---|---|
| Bước cập nhật | `K` cố định | ✓ tỉ lệ `RD²` — chắc chắn nhiều thì bước nhỏ |
| Không chắc chắn | không mô hình | ✓ `RD` là biến trạng thái |
| Nghỉ lâu | không đổi | ✓ `RD` phình theo `σ` và thời gian |
| Phong độ thất thường | không mô hình | ✓ `σ` tự điều chỉnh |
| Đối thủ không chắc chắn | tính như nhau | ✓ giảm trọng số bằng `g(φ)` |

### 19.2 Đổi thang — vì sao có hằng số 173.7178

Thang hiển thị (gốc 1500, mỗi 400 điểm ≈ gấp 10 lần cơ hội thắng) tiện cho người đọc nhưng dùng **log cơ số 10**. Toán bên trong dùng hàm logistic **cơ số `e`**. Đổi thang là chia đi đúng phần lệch cơ số:

$$\mu = \frac{r - 1500}{q}, \qquad \phi = \frac{RD}{q}, \qquad q = \frac{400}{\ln 10} \approx 173.7178$$

**Phép kiểm tái lập:** `400 / ln(10) = 400 / 2.302585 = 173.7178`. ✓ Con số này không phải magic number — nó là hệ số đổi cơ số.

Cuối cùng đổi ngược lại để lưu/hiển thị:

$$r' = 1500 + q\,\mu', \qquad RD' = q\,\phi'$$

### 19.3 Hai hàm nền

**① Hệ số suy giảm `g(φ)` — "đối thủ mơ hồ thì kết quả nói được ít"**

Trực giác trước: nếu ta **không biết rõ** level khó cỡ nào (`φ` lớn), thì thắng/thua nó chẳng nói lên nhiều về kỹ năng người chơi. Cần một hệ số làm **nhoè** ảnh hưởng, tiến tới 0 khi `φ → ∞`:

$$g(\phi) = \frac{1}{\sqrt{1 + 3\phi^2/\pi^2}}$$

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| `φ = 0` (biết chính xác level) | không suy giảm ⇒ `g = 1` | $1/\sqrt{1+0} = 1$ ✓ |
| `φ → ∞` (mù tịt về level) | không học được gì ⇒ `g → 0` | mẫu $\to \infty$ ✓ |
| `φ` tăng | `g` giảm đơn điệu | mẫu tăng đơn điệu ✓ |

**② Kỳ vọng thắng `E` — logistic có suy giảm**

$$E(\mu, \mu_j, \phi_j) = \frac{1}{1 + e^{-g(\phi_j)\,(\mu - \mu_j)}}$$

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| `μ = μ_j` (ngang sức) | 50/50 | $1/(1+e^0) = 0.5$ ✓ |
| `μ ≫ μ_j` | gần như chắc thắng | $e^{-\infty}\to 0 \Rightarrow E \to 1$ ✓ |
| `μ ≪ μ_j` | gần như chắc thua | $E \to 0$ ✓ |
| `φ_j → ∞` | `g → 0` ⇒ mọi chênh lệch bị nhoè về 50/50 | $E \to 0.5$ ✓ |

### 19.4 Suy ra công thức cập nhật

**③ Phương sai ước lượng `v` — "trận nào cho biết nhiều thông tin nhất?"**

Trực giác: một trận **50/50** là trận nói lên nhiều nhất (kết quả nào cũng đáng ngạc nhiên một nửa). Một trận **chắc thắng** thì thắng chẳng nói gì. Đại lượng đo đúng tính chất đó là phương sai Bernoulli `E(1−E)`: cực đại `0.25` tại `E = 0.5`, tiến `0` ở hai đầu. Cộng dồn thông tin từ mọi trận, có trọng số `g²` (theo ①), rồi lấy nghịch đảo để ra **phương sai** (nhiều thông tin ⇒ phương sai nhỏ):

$$v = \left[\sum_j g(\phi_j)^2 \, E_j \,(1 - E_j)\right]^{-1}$$

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| Trận 50/50, `φ_j = 0` | thông tin tối đa ⇒ `v` nhỏ nhất | $v = 1/0.25 = 4$ ✓ |
| `E → 0` hoặc `E → 1` | không học được gì ⇒ `v → ∞` | $E(1-E) \to 0$ ✓ |
| Không có trận nào | tổng `= 0` ⇒ `v = ∞` | ⚠️ chia 0 — **phải guard** |

**④ Độ lệch kỳ vọng `Δ` — "kết quả lệch dự đoán bao nhiêu, tính theo đơn vị thông tin"**

`(s_j − E_j)` là **độ ngạc nhiên** có dấu. Nhân trọng số `g`, cộng dồn, rồi nhân `v` để đưa về thang rating:

$$\Delta = v \sum_j g(\phi_j)\,(s_j - E_j)$$

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| `s = E` (đúng như dự đoán) | không có gì để học ⇒ `Δ = 0` | ✓ |
| Thắng khi `E` thấp | ngạc nhiên lớn, `Δ > 0` lớn | ✓ |

**⑤ Volatility mới `σ'` — giải phương trình bằng phương pháp Illinois**

Câu hỏi: độ ngạc nhiên `Δ²` **lớn hơn** mức mô hình dự trù (`φ² + v`) thì nên tăng `σ` (người này đang biến động); nhỏ hơn thì nên giảm. Nhưng không được để `σ` nhảy loạn theo một trận ⇒ cần một **hằng số kìm** `τ`. Đặt `x = ln(σ²)`, `a = ln(σ²)` ban đầu, ta cần nghiệm của:

$$f(x) = \frac{e^x\,(\Delta^2 - \phi^2 - v - e^x)}{2\,(\phi^2 + v + e^x)^2} - \frac{x - a}{\tau^2} = 0$$

Đọc hai số hạng:
- **Số hạng 1** đổi dấu tại `e^x = Δ² − φ² − v`: nếu ngạc nhiên vượt dự trù thì đẩy `x` lên, ngược lại đẩy xuống.
- **Số hạng 2** là lực kéo về giá trị cũ `a`, mạnh `1/τ²`. `τ` nhỏ ⇒ bảo thủ (σ khó đổi); `τ` lớn ⇒ nhạy. `τ ≈ 0.5` là mặc định hợp cho puzzle.

Thuật toán (Illinois — secant có sửa nửa bước, hội tụ chắc chắn vì luôn giữ hai đầu kẹp nghiệm):

```
A = a
B = (Δ² > φ² + v) ? ln(Δ² − φ² − v)
                  : a − k·τ  với k nhỏ nhất sao cho f(a − k·τ) ≥ 0     ← nới xuống tới khi kẹp được
fA = f(A) ;  fB = f(B)
while (|B − A| > ε  &&  iter < maxIter)          // ε = 1e-6, maxIter = 1000
{
    if (|fB − fA| < ε) break                     // gradient ~0 → dừng, tránh chia 0
    C  = A + (A − B)·fA / (fB − fA)              // secant
    fC = f(C)
    if (fC·fB ≤ 0) { A = B ;  fA = fB; }         // C,B kẹp nghiệm → dịch A sang B
    else           { fA = fA / 2; }              // sửa Illinois: giảm nửa fA, chống hội tụ một phía
    B = C ;  fB = fC
}
σ' = e^(A/2)                                      // vì x = ln(σ²) ⇒ σ = e^(x/2)
```

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| `Δ² = φ² + v` (đúng dự trù) | số hạng 1 ≈ 0 ⇒ nghiệm ≈ `a` ⇒ `σ' ≈ σ` | ✓ |
| `Δ²` rất lớn | `x` bị đẩy lên ⇒ `σ' > σ` | ✓ |
| `τ → 0` | lực kéo về `a` vô hạn ⇒ `σ' → σ` | ✓ |
| `v = ∞` hoặc `NaN` | không giải được | ⚠️ **trả `σ` cũ**, không throw |
| không tìm được `B` sau maxIter | input bệnh | ⚠️ trả `σ` cũ + log |

**⑥ Phình `RD` theo thời gian nghỉ, rồi cập nhật**

Chưa gặp lại người chơi trong `t` chu kỳ rating ⇒ ước lượng cũ đi, độ không chắc chắn phình theo `σ'`:

$$\phi^{*} = \sqrt{\phi^2 + \sigma'^2 \, t}$$

Kết hợp thông tin cũ (`1/φ*²`) với thông tin mới từ trận (`1/v`) — đây là cộng **độ chính xác** (nghịch đảo phương sai), quy tắc chuẩn của cập nhật Bayes:

$$\phi' = \frac{1}{\sqrt{\dfrac{1}{\phi^{*2}} + \dfrac{1}{v}}}$$

$$\mu' = \mu + \phi'^2 \sum_j g(\phi_j)(s_j - E_j) = \mu + \phi'^2 \, \frac{\Delta}{v}$$

**Đối chiếu số hạng ↔ code.** Đẳng thức cuối là chỗ dễ sai nhất: theo ④ thì $\Delta = v \sum_j g(s_j - E_j)$, nên $\sum_j g(s_j-E_j) = \Delta/v$. Vì vậy code viết `newMu = mu + phiPrime*phiPrime * (delta / v)` là **đúng**, không cần giữ riêng tổng `Σg(s−E)`.

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| `Δ = 0` | rating không đổi | $\mu' = \mu$ ✓ |
| `t` lớn (nghỉ lâu) | `φ*` lớn ⇒ `φ'` lớn ⇒ bước `μ` dài hơn | "quên" ⇒ học lại nhanh ✓ |
| `v → ∞` (trận vô nghĩa) | `1/v → 0` ⇒ `φ' ≈ φ*`; `Δ/v` nhỏ ⇒ gần như không cập nhật | ✓ |
| `φ` rất nhỏ (biết rất rõ) | `φ'` nhỏ ⇒ `φ'²` nhỏ ⇒ bước ngắn | ổn định ✓ |
| `RD` sau nhiều trận | giảm dần rồi bão hoà | `1/φ'² = 1/φ*² + 1/v` luôn tăng ⇒ `φ'` giảm ✓ |

**Phình `RD` khi offline (không có trận nào)** — dùng riêng khi mở lại app sau `offlineSeconds` (§4):

$$\phi_{\text{new}} = \sqrt{\phi^2 + \sigma^2 \left\lfloor \frac{\text{offlineSeconds}}{T_{\text{period}}} \right\rfloor}$$

`T_period` là **chu kỳ rating** — độ dài thời gian mà ta coi là "một đợt". Với puzzle mobile, một đợt ≈ vài giờ (ví dụ 3 giờ) hợp hơn 1 tháng như cờ vua, vì người chơi mobile chơi nhiều phiên ngắn trong ngày.

### 19.5 Điểm số `s` — chỗ cố ý lệch khỏi Glicko-2 chuẩn

Glicko-2 gốc định nghĩa `s ∈ {0, 0.5, 1}` = thua/hoà/thắng. Puzzle không có "hoà", nhưng có thứ quý hơn: **mức trợ giúp người chơi phải dùng**. Thắng sạch khác hẳn thắng sau 3 lần revive. Nên map:

| Kết quả | `s` | Vì sao |
|---|---|---|
| Thắng, không revive/continue | `1.0` | Thắng thật |
| Thắng, dùng **1** revive/add-move | `0.5` | Đúng nghĩa "hoà" — vừa đủ sức |
| Thắng, dùng **≥2** trợ giúp | `0.0` | Không thắng bằng kỹ năng |
| Thua | `0.0` | — |

Kèm một chỉ số phụ **không** thuộc Glicko-2, dùng cho tầng áp dụng:

$$\text{stress} = \min\!\left(0.1\,n_{\text{booster}} + 0.2\,n_{\text{revive}},\ \text{stressMax}\right)$$

`stress` đo "người chơi phải vật lộn cỡ nào" — dùng để **reset nhịp độ khó** (§19.6), không dùng để cập nhật rating.

> ⚠️ **Nêu rõ vì đây là lệch có chủ ý:** ai đọc paper Glicko-2 rồi xem code sẽ thắc mắc tại sao `s` không phải win/lose thuần. Lý do là encode mức trợ giúp; muốn bản đúng chuẩn thì map `s = isWon ? 1 : 0` và bỏ nhánh revive.

### 19.6 Tầng áp dụng — biến rating thành độ khó thực tế

Rating tự nó **không** làm gì. Cần một quy tắc chọn "level tiếp theo nên khó cỡ nào". Mô hình dùng **deficit** (khoảng thiếu hụt) tạo nhịp răng cưa:

```
levelRating = playerRating + deficit           ← deficit < 0 ⇒ level DỄ hơn kỹ năng người chơi

khởi tạo:  deficit = deficitMin  (vd −400)      ← bắt đầu dễ, cho cảm giác thắng
mỗi lần thắng:  deficit += gainPerWin  (vd +100)  ← siết dần, căng lên
reset về deficitMin khi:
   ① deficit ≥ deficitMax (vd +400)             ← đã căng hết → xả
   ② stress ≥ stressMax                          ← người chơi vật lộn quá → xả ngay
```

Đường cong này là **tension–release**: 8 màn dễ dần khó lên, tới đỉnh thì thả về dễ. Người chơi cảm nhận "có lúc dễ có lúc khó" thay vì đơn điệu — và không bao giờ bị kẹt tường lâu (nhánh ② cứu).

**5 level đầu dùng rating cố định** (bảng hằng số), không adaptive: lúc đó `RD` còn ~350 (mù tịt về người chơi) nên cập nhật sẽ nhảy loạn; và onboarding cần trải nghiệm **giống nhau** cho mọi người để đo funnel.

**Ranh giới bắt buộc:** SDK dừng ở "trả ra một target rating / difficulty scalar". Việc dịch số đó thành **số moves · số booster gợi ý · phân bố màu/item · ngưỡng spawn** là logic mechanic của game — nếu để rò vào SDK thì hệ này chỉ dùng được cho đúng một game.

### 19.7 Kiến trúc

```
┌─ Foundation: THUẦN TOÁN (không Unity, không state, test bằng unit test) ─┐
│  RatingState (struct: rating, ratingDeviation, volatility)              │
│  Glicko2Solver (static): G · Expected · Variance · Delta                │
│                          SolveVolatility · UpdateRating · DecayDeviation│
└─────────────────────────────────────────────────────────────────────────┘
                                    ▲
┌─ Composite: ÁP DỤNG ──────────────┴────────────────────────────────────┐
│  IPlayerRatingStore      (§2 lưu RatingState + deficit)                │
│  IMatchOutcome           (s, stress, duration) ← game điền             │
│  IDifficultyPolicy       deficit → target rating ; reset rule          │
│  IAdaptiveDifficultyService  điều phối: sau mỗi màn gọi 1 lần          │
└────────────────────────────────────────────────────────────────────────┘
                                    ▲
                      game: rating → moves/booster/spawn   (KHÔNG vào SDK)
```

```csharp
// ── Foundation ──────────────────────────────────────────────────────────
public struct RatingState
{
    public double Rating;              // thang hiển thị, mặc định 1500
    public double RatingDeviation;     // mặc định 350
    public double Volatility;          // mặc định 0.06

    public double Mu  => (Rating - Glicko2Solver.Scale) / Glicko2Solver.Q;   // Scale = 1500
    public double Phi => RatingDeviation / Glicko2Solver.Q;
}

public readonly struct MatchResult
{
    public readonly RatingState Opponent;    // rating của level
    public readonly double Score;            // s ∈ [0,1] — xem §19.5
}

public static class Glicko2Solver              // zero-GC: không alloc, không LINQ
{
    public const double Q     = 173.7178;      // 400 / ln(10)  — §19.2
    public const double Scale = 1500.0;
    public const double DefaultTau = 0.5;

    public static double G(double phi);                                  // §19.3①
    public static double Expected(double mu, double oppMu, double oppPhi);// §19.3②
    public static double Variance(in RatingState p, ReadOnlySpan<MatchResult> ms);   // §19.4③
    public static double Delta(in RatingState p, ReadOnlySpan<MatchResult> ms, double v); // ④
    public static double SolveVolatility(in RatingState p, double v, double delta,
                                        double tau = DefaultTau);        // §19.4⑤
    public static RatingState UpdateRating(in RatingState p, double v, double delta,
                                           double newVolatility, double elapsedPeriods); // ⑥
    public static RatingState DecayDeviation(in RatingState p, double elapsedPeriods);    // ⑥ offline
}

// ── Composite ───────────────────────────────────────────────────────────
public interface IDifficultyPolicy : IService<IDifficultyPolicy>
{
    double GetTargetLevelRating(double playerRating, int deficit);
    int    AdvanceDeficit(int currentDeficit, double stress);   // trả deficit mới (có reset)
    bool   IsFixedRatingLevel(int levelIndex, out RatingState fixedRating);
}

public interface IAdaptiveDifficultyService : IService<IAdaptiveDifficultyService>
{
    RatingState PlayerRating { get; }
    double NextLevelTargetRating { get; }             // = playerRating + deficit
    void SubmitResult(int levelIndex, bool isWon, int boostersUsed, int revivesUsed,
                      float durationSeconds);        // gọi ĐÚNG MỘT LẦN sau mỗi màn
    void ApplyOfflineDecay(float offlineSeconds);     // gọi từ §4 OnPauseChanged(false, …)
}
```

**Luồng một màn**

```
kết thúc màn ──► SubmitResult(level, isWon, boosters, revives, duration)
   ├─ policy.IsFixedRatingLevel(level)? → dùng rating cố định làm "đối thủ"
   │                                    → CẬP NHẬT player, KHÔNG đổi deficit
   ├─ ngược lại: opponent = RatingState{ NextLevelTargetRating, RD=200, σ=0.06 }
   ├─ s      = map(isWon, revives)                        ← §19.5
   ├─ v      = Variance(player, [opponent])
   ├─ Δ      = Delta(player, [opponent], v)
   ├─ σ'     = SolveVolatility(player, v, Δ)
   ├─ player = UpdateRating(player, v, Δ, σ', elapsedPeriods)
   ├─ deficit = policy.AdvanceDeficit(deficit, stress)     ← §19.6
   └─ store.Save(player, deficit)                          ← §2, save-unit riêng

resume app ──► §4 OnPauseChanged(false, offlineSeconds) ──► ApplyOfflineDecay
                    └─ player = DecayDeviation(player, offlineSeconds / T_period)
```

**Quyết định thiết kế**

| Quyết định | Vì sao | Đánh đổi |
|---|---|---|
| **Extract phẫu thuật**: chỉ lấy lõi toán vào SDK | Phần "áp rating vào mechanic" dính chặt booster/item/màu của một game cụ thể; kéo cả cụm vào SDK là kéo cả một game vào | Game phải tự viết tầng mapping |
| `Glicko2Solver` là `static` + `struct` + `ReadOnlySpan` | Gọi 1 lần/màn nhưng phải test được bằng unit test không cần Unity; zero-GC là hệ quả tự nhiên | Không mở rộng bằng kế thừa (không cần) |
| `RatingState` giữ thang **hiển thị**, đổi thang trong property | Lưu/log/debug đọc được số 1500 quen thuộc; đổi thang là chi tiết nội bộ | 2 phép chia mỗi lần đọc (rẻ) |
| Level `RD = 200` (không 350) | Level là thực thể ta **thiết kế**, biết rõ hơn người chơi; `RD` nhỏ ⇒ `g` lớn ⇒ kết quả có trọng số cao | Con số cần tune theo dữ liệu |
| Cố định rating 5 level đầu | `RD ≈ 350` lúc đầu ⇒ cập nhật nhảy loạn; onboarding cần đồng nhất để đo funnel | 5 level không adaptive |
| `deficit` răng cưa, reset 2 điều kiện | Tension–release; nhánh `stress` là **van an toàn** chống kẹt tường | Người chơi có thể nhận ra nhịp |
| Guard mọi nhánh bệnh (`v=∞`, không hội tụ) → giữ giá trị cũ | Một `NaN` rò vào rating là **hỏng vĩnh viễn** save của người chơi | Im lặng bỏ 1 lần cập nhật (phải log) |
| Log rating qua §12 mỗi màn | Không có telemetry thì không tune được `τ`, `deficit`, `RD` level | Tốn quota event |

**Cạm bẫy**
- ⚠️ **`elapsedPeriods` phải là thời gian NGHỈ, không phải thời lượng màn chơi.** Số hạng $\phi^* = \sqrt{\phi^2 + \sigma'^2 t}$ mô hình hoá "ước lượng cũ đi vì lâu không quan sát". Truyền `durationSeconds / T_period` vào đó là sai ngữ nghĩa: màn chơi càng dài lại càng làm `RD` phình ⇒ rating nhảy mạnh hơn ở màn dài. Quyết định **tường minh** ngay từ đầu: `elapsedPeriods` = khoảng cách kể từ lần `SubmitResult` trước (hoặc 0 nếu chơi liên tục). Đây là lỗi rất dễ trôi qua vì kết quả vẫn "trông hợp lý".
- `NaN`/`Infinity` rò vào save: `Variance` chia 0 khi không có trận; `SolveVolatility` chia 0 khi `fB ≈ fA`. Guard cả hai, và **validate trước khi ghi** (`double.IsFinite`).
- `SubmitResult` gọi 2 lần cho một màn (win callback + analytics callback) → rating nhảy đôi. Idempotent theo `(levelIndex, attemptId)`.
- Cập nhật rating của **level** theo kết quả người chơi: level là đối tượng thiết kế, để nó tự trôi thì mất kiểm soát và không so sánh được giữa người chơi. Chỉ cập nhật player (trừ khi có hệ tuning offline riêng).
- Tin `RD` sau 2–3 màn: `RD` còn rất lớn ⇒ target rating nhảy mạnh. Chờ `RD` xuống dưới ngưỡng mới cho adaptive chi phối (hoặc dùng nhánh fixed 5 level).
- Người chơi cheat/AFK làm `s = 0` liên tục → rating rơi tự do, level dễ đến mức vô nghĩa. Kẹp `rating` trong `[min, max]`.

**Xong khi:** §0.6 + `Glicko2Solver` có unit test đối chiếu từng mốc ở §19.3–§19.4 · `rating`/`RD`/`σ` không bao giờ ghi `NaN` vào save · lõi toán không tham chiếu `UnityEngine` · không có tên mechanic game nào trong SDK · telemetry đủ để tune `τ` và `deficit`.

**Ưu tiên:** giá trị cao nhất trong 21 hệ, nhưng làm **sau** §15 (cần catalog + difficulty tag để vận hành) và **sau** §12 (không có telemetry thì không tune được).

---

## 20. 💎 LiveOps Module Host — `Composite`

**Bài toán.** Live-ops là chuỗi event thay nhau theo mùa (battle pass → tournament → chest event). Viết xuyên vào lõi game thì: thêm event = sửa lõi, bỏ event = phải dò xoá, và không đem sang game khác được. Cần biến event thành **module cắm-rút**: thả thư mục vào là chạy, xoá thư mục là hết.

**Làm cuối cùng** — hệ này tiêu thụ hầu hết tầng dưới qua interface: §2 lưu tiến độ · §4 đếm thời gian · §7 popup · §11 badge · §12 tracking · §13 ads · §14 reward.

**Use case**
- Thêm event mới: 1 thư mục tự chứa (UI + config + data + localization + tracking riêng) → thả vào, khai `#define`, xong.
- Bật/tắt/lên lịch event từ remote config, không update app.
- Event lịch tuần hoàn: chu kỳ 28 ngày, mỗi chu kỳ có các khoảng chạy (vd ngày 0–3, 7–10, 14–17, 21–24).
- Event có thể **kích hoạt** ở nhiều thời điểm: sau khi game init xong · sau khi fetch remote config · sau khi thắng level.
- Module thiếu một service (game này không có haptic) → **degrade gracefully**, không crash.
- App đóng khi event đang chạy, mở lại sau khi event đã hết → phải kết thúc sạch, UI không đọc thời gian âm.
- Người chơi chưa xác nhận nhận thưởng cuối event → giữ trạng thái "đã hết nhưng chưa xác nhận" cho tới khi họ mở popup.

**Mô hình — 4 trục**

| Trục | Thành phần | Vai trò |
|---|---|---|
| **Vòng đời** | `LiveOpsModuleBase<TData, TConfig>` | Điều phối: check-active → start → tick → end → player-confirm |
| **Dữ liệu** | `ILiveOpsEventData` + `LiveOpsEventDataBase<T>` | `isActive` · `isPendingConfirm` · `endDateUnix`; lưu qua §2 |
| **Lịch** | `ILiveOpsOperationConfig` + `EventSchedule[]` | remote: bật/tắt · level unlock/tease · các khoảng chạy trong chu kỳ |
| **Service** | `IService<T>` (bắt buộc) / `IOptionalService<T>` (tuỳ chọn) | Ranh giới module ↔ game (§0.2) |

**3 trạng thái event** — phải phân biệt rạch ròi, đây là nguồn bug nhiều nhất:

| Trạng thái | `isActive` | `isPendingConfirm` | Nghĩa |
|---|:--:|:--:|---|
| `Inactive` | false | false | Chưa tới lượt / đã xong hẳn |
| `Running` | true | true | Đang chạy, người chơi đang tham gia |
| `Finished` | false | true | Hết thời gian nhưng **chưa** xác nhận nhận thưởng cuối |

Không có `Finished` thì hết event là mất thưởng của người chơi — và họ sẽ khiếu nại.

**Contract**

```csharp
// ── Lịch ────────────────────────────────────────────────────────────────
public readonly struct EventSchedule
{
    public readonly int StartDay, EndDay;          // chỉ số ngày trong chu kỳ, EndDay inclusive
    public static readonly EventSchedule[] Always = { new(0, CycleDays - 1) };
}

public interface ILiveOpsOperationConfig                 // remote-driven
{
    bool IsEnabled { get; }
    int  LevelTease  { get; }                      // hiện teaser từ level này
    int  LevelUnlock { get; }                      // mở thật từ level này
    EventSchedule[] Schedules { get; }
}

// ── Dữ liệu ─────────────────────────────────────────────────────────────
public interface ILiveOpsEventData
{
    bool IsActive { get; }
    bool IsPendingConfirm { get; }
    DateTime EndDateUtc { get; }
    long EndDateUnix { get; }

    void Initialize(ILiveOpsModule owner);
    bool IsWithinSchedule();
    bool CanActivate(int currentLevel, int levelUnlock);
    bool TryActivate(int currentLevel, int levelUnlock);
    void EndEvent();                               // hết thời gian → Finished
    void ConfirmFinished();                        // người chơi đã nhận → Inactive
    void ForceExpire();                            // cheat/test
}

// ── Vòng đời ────────────────────────────────────────────────────────────
[Flags]
public enum EActivationTiming
{
    OnGameInitialized  = 1 << 0,
    OnRemoteConfigDone = 1 << 1,
    OnLevelWin         = 1 << 2,
}

public interface ILiveOpsModule
{
    string ModuleId { get; }
    ILiveOpsOperationConfig OperationConfig { get; }
    bool IsReady { get; }
    bool IsEnabled { get; }
    bool IsRunning { get; }
    bool IsUnlocked { get; }
    int  SecondsLeft { get; }
    int  ProgressPercent { get; }

    event Action Started;
    event Action<int> TimeTicked;                  // giây còn lại
    event Action Ended;                            // hết thời gian
    event Action PlayerConfirmed;                  // đã nhận thưởng cuối
}

public interface ILiveOpsHost : IService<ILiveOpsHost>
{
    void Register(ILiveOpsModule module);
    void Unregister(ILiveOpsModule module);
    bool TryGet(string moduleId, out ILiveOpsModule module);
    IReadOnlyList<ILiveOpsModule> RunningModules { get; }
}
```

**Vòng đời chi tiết**

```
Initialize (lúc bootstrap §1)
  ├─ data.Initialize(this)                         ← nạp save-unit của module (§2)
  ├─ data.IsActive?
  │    ├─ SecondsLeft ≤ 0  → data.EndEvent()       ← ⚠️ event hết hạn lúc app đóng:
  │    │                                              phải end NGAY, đừng để UI đọc số âm
  │    └─ còn thời gian    → PreloadAssets() + ticker.AddSecondListener(this)
  ├─ đăng ký theo activationTiming:
  │    OnGameInitialized  → IInitializationService.RegisterPostInitialization(CheckActive)
  │    OnRemoteConfigDone → IRemoteConfigService.Fetched += CheckActive
  │    OnLevelWin         → ILevelProgressService.LevelWon += OnLevelWin
  └─ IsReady = true

CheckActive()
  ├─ !IsEnabled (remote off) → false
  ├─ data.CanActivate(currentLevel, LevelUnlock) → false nếu: đang active ‖ pending ‖ chưa đủ level ‖ ngoài lịch
  └─ data.TryActivate → PreloadAssets · ticker.AddSecondListener · Started

Tick (qua §4 ITicker, tần số THÍCH ỨNG)
  secondsLeft > 90000  → nghỉ 3600s trước lần cập nhật kế   ← >~25h: cập nhật 1 giờ/lần
  secondsLeft > 3660   → nghỉ 60s                            ← >~1h : 1 phút/lần
  secondsLeft > 0      → nghỉ 1s                             ← <1h  : 1 giây/lần
  secondsLeft ≤ 0      → EndEvent

EndEvent()          : data.EndEvent() → Ended → ticker.RemoveSecondListener → CheckActive() (chu kỳ kế?)
PlayerConfirm()     : data.ConfirmFinished() → PlayerConfirmed → CheckActive()
OnPause(resume, off): reset bộ đếm nghỉ về 0 → cập nhật ngay, không chờ hết chu kỳ cũ
OnDestroy()         : unregister TẤT CẢ (ticker, level, remote, cheat-time)
```

**Lịch tuần hoàn** — chu kỳ `CycleDays` (vd 28) kể từ một mốc gốc cố định:

```
dayIndex = ((today − epochDate).Days mod CycleDays + CycleDays) mod CycleDays   ← mod dương
tìm schedule đầu tiên có dayIndex ≤ EndDay:
      start = today.AddDays(schedule.StartDay − dayIndex)
      end   = today.AddDays(schedule.EndDay   − dayIndex + 1)      ← +1: EndDay inclusive
không có (đã qua hết trong chu kỳ này):
      nextCycle = today.AddDays(CycleDays − dayIndex)
      start = nextCycle.AddDays(schedules[0].StartDay)
      end   = nextCycle.AddDays(schedules[0].EndDay + 1)
```

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| `dayIndex = 1`, schedule `(0,3)` | đang trong khoảng chạy | `1 ≤ 3` ⇒ start = hôm qua, end = hôm nay+3 ✓ |
| `dayIndex = 5`, schedules `(0,3),(7,10)` | chờ khoảng kế | `5 > 3` → xét `(7,10)`: `5 ≤ 10` ⇒ start = +2 ngày ✓ |
| `dayIndex = 26`, schedules hết ở `(21,24)` | nhảy sang chu kỳ sau | `nextCycle = +2 ngày`, start = `+2+0` ✓ |
| `(today − epoch)` âm (đồng hồ lùi) | vẫn cho index hợp lệ | `mod` dương hai lần ✓ |

**Quyết định thiết kế**

| Quyết định | Vì sao | Đánh đổi |
|---|---|---|
| Bắt buộc / tuỳ chọn phân biệt **ở tầng type** (§0.2) | Điều kiện tiên quyết để cắm-rút: module khai `IOptionalService` cho mọi thứ nó sống thiếu được, và compiler chặn luôn nhánh throw | Hai interface song song |
| Base class **2** type param (`TData`, `TConfig`), không 4 | Asset-bundle và operation-config **không** cần là type param: module giữ chúng bằng composition (`ILiveOpsAssets`, `ILiveOpsOperationConfig`) vì host không bao giờ cần biết type cụ thể của chúng. Mỗi type param thêm vào là một chữ ký nữa mà mọi module con phải khai lại | Truy cập asset qua property, không có kiểu cụ thể sẵn |
| `IOptionalExternalConfigProvider` + default provider | Game có thể override config của module; không override thì dùng default trong module → module chạy được **ngay** khi vừa thả vào | 1 lớp gián tiếp |
| Module tự chứa (UI, localization, tracking, asset riêng) | Xoá thư mục = xoá sạch event, không để lại rác trong lõi | Trùng lặp nhẹ giữa các module |
| `#define` per-module (`LIVEOPS_MODULE_X`) | Module không compile vào build khi không dùng → không tốn size, không tốn init | Phải maintain define |
| Tick **thích ứng theo thời gian còn lại** | Event 7 ngày mà tick 1 Hz là 604800 lần cập nhật vô ích cho một dòng text "6d 23h" | Logic tần số 4 nhánh |
| Trạng thái `Finished` (pending confirm) riêng | Không có nó thì hết event là mất thưởng người chơi | 1 cờ + 1 bước xác nhận |
| Xử lý "hết hạn lúc app đóng" **ngay trong Initialize** | Nếu không, UI hiển thị thời gian âm và mọi tính toán progress ra số vô nghĩa | Vài dòng guard |
| Lịch dùng **chỉ số ngày trong chu kỳ**, không ngày tuyệt đối | PM đổi lịch bằng cách sửa vài số nguyên trên remote; không phải đổi ngày mỗi mùa | Không lên lịch được sự kiện một-lần |
| `activationTiming` là `[Flags]` | Event khác nhau cần kích ở thời điểm khác nhau; có event cần nhiều điểm | Phải hiểu rõ từng timing |
| `Started/Ended/TimeTicked/PlayerConfirmed` là event | UI/badge/tracking bám vào, module không biết ai đang nghe | Phải nhớ unsubscribe |
| Asset qua `AddressableObjectReference<T>` (§A.2) | Event có nhiều asset nặng; chỉ tải khi event chạy, release khi hết | Phải nhớ `Unload()` |

**Cạm bẫy**
- **Extract nội dung event vào SDK.** Chỉ extract **khung host + contract**. Một event cụ thể (chest, battle pass) là nội dung game — kể cả khi nó "trông generic".
- Event hết hạn khi app đóng mà không end lúc `Initialize` → `SecondsLeft` âm → progress ra số vô nghĩa, thanh tiến độ vỡ.
- Không `Unregister` khi module bị dỡ → ticker/level service giữ reference chết, và `Ended` bắn vào module đã chết.
- Module phụ thuộc **thứ tự** khởi tạo với module khác → coupling ẩn. Module chỉ được biết service, không biết module khác.
- `DateTime.Now` (local) trong tính lịch → event lệch ngày theo timezone người chơi (§0.7).
- Cheat-time (dịch giờ để test) không thông báo cho module → module vẫn dùng cache cũ. Có `IExternalTimeProvider` với hook `CheatTimeChanged` → reset bộ đếm nghỉ.
- Module đăng ký badge (§11) mà không unregister → chấm đỏ vĩnh viễn sau khi event hết.

**Xong khi:** §0.6 + xoá thư mục 1 module không làm vỡ build · module chạy được khi thiếu mọi optional service · event hết hạn lúc app đóng vẫn end sạch · event 7 ngày không tick 1 Hz · dịch giờ máy để test cho kết quả đúng.

---

## 21. 💎 Ads Pacing & Monetization Scenario — `Composite`

**Bài toán.** "Bao lâu thì cho xem inter một lần" không phải một con số cố định. Người chơi **ngày đầu** cần ít quảng cáo (giữ họ lại), người chơi **ngày thứ 10** chịu được nhiều hơn. Người **đã mua IAP** không nên thấy ads bán booster. Và toàn bộ mấy ngưỡng đó phải **điều chỉnh từ remote** mà không update app.

**Vì sao đây là viên ngọc:** hệ này **hoàn toàn không phụ thuộc type nào của game** — nó chỉ đọc remote config + đếm ngày + hỏi §13. Port sang game khác gần như copy nguyên. Rất ít studio làm bài bản, mà tác động doanh thu trực tiếp.

**Use case**
- Interval giữa 2 inter phụ thuộc **Day Active**: ngày 0–2 = 120s, ngày 2–7 = 100s, ngày 7+ = 90s.
- Gate theo level cho từng loại: banner từ level A, inter-win từ level B, inter-lose từ level C, rewarded-revive từ level D.
- **Kịch bản theo phân khúc**: `Blended` / `Ads` / `IapCamp` / `Purchase` — mỗi kịch bản mở/đóng các placement khác nhau (vd rewarded-booster **chỉ** ở kịch bản `Ads`).
- Chuyển kịch bản theo thời gian: ở `Blended` quá N ngày mà **không** mua gì → chuyển sang `Ads`. Mua IAP thật → chuyển ngay sang `Purchase`.
- Mọi ngưỡng và cả bảng range từ **một JSON remote**; chưa fetch được thì dùng bản cache + default.
- Giá revive, coin/level, bundle booster cũng nằm trong cùng config (kinh tế và ads điều chỉnh cùng nhịp).

**Ba khối tách rời**

| Khối | Trách nhiệm | Phụ thuộc |
|---|---|---|
| `DayActiveTracker` | Đếm **số ngày lịch** kể từ ngày cài | chỉ prefs — **zero dependency** |
| `MonetizationConfig` | Toàn bộ ngưỡng/giá/range, nạp từ remote JSON + cache | RemoteConfig |
| `MonetizationRules` | Trả lời `CanShowX(...)` từ config + kịch bản + level | 2 khối trên |
| `ScenarioFlow` | Phân kịch bản lần đầu + chuyển theo ngày/IAP | §2, §4 |

**Day Active — vì sao không phải "số ngày đã mở app"**

```
Touch(nowLocal):                              ← gọi mỗi lần app mở / resume
  today = nowLocal.Date
  chưa có installDate → lưu installDate = today, daysActive = 0, return
  elapsed = max(0, (today − installDate).Days)
  daysActive = max(daysActive, elapsed)       ← CHỈ TĂNG, không bao giờ giảm
  lưu lastSeen = today
```

| Chi tiết | Vì sao |
|---|---|
| Đếm **ngày lịch trôi qua**, không "số ngày có mở app" | "Day 3" nghĩa là 3 ngày sau khi cài, kể cả người chơi bỏ 2 ngày. Đây là định nghĩa PM/UA dùng để đọc cohort |
| `max(cũ, mới)` — chỉ tăng | Người chơi lùi đồng hồ máy để "trở về ngày 0" (ít ads) — không cho giảm là chặn được |
| Ngày **local**, không UTC | "Ngày" theo cảm nhận người chơi là ngày ở múi giờ của họ. Đây là ngoại lệ **có chủ ý** so với §4 |
| Format `yyyy-MM-dd` + `InvariantCulture` | Lưu bằng `ToString()` mặc định sẽ parse lỗi khi người chơi đổi locale máy |
| Chỉ dùng prefs, không type nào của game | Chính là lý do khối này port được nguyên vẹn |

**Contract**

```csharp
public interface IDayActiveTracker : IService<IDayActiveTracker>
{
    int CurrentDayActive { get; }        // Day 0 = ngày cài
    void Touch();                        // app mở / resume
}

public enum EMonetizationScenario { Unassigned, Blended, Ads, IapCamp, Purchase }

public interface IUserSegmentProvider : IService<IUserSegmentProvider>
{
    EUserSegment Segment { get; }        // từ attribution/CDP — game cấp
}

public interface IMonetizationConfig : IService<IMonetizationConfig>
{
    // ads gate
    int BannerUnlockLevel { get; }
    int InterstitialWinMinLevel { get; }
    int InterstitialLoseMinLevel { get; }
    int RewardedReviveMinLevel { get; }
    int RewardedBoosterMinLevel { get; }
    int MinInterstitialIntervalSeconds { get; }

    // pacing theo day-active
    bool TryGetIntervalByDayActive(int dayActive, out int seconds);

    // scenario transition
    int DaysBlendedToAds { get; }
    int DaysIapCampToAds { get; }

    // kinh tế đi kèm
    int ReviveCoinPrice { get; }
    int CoinPerLevel { get; }
    int StartingCoin { get; }

    event Action Changed;                // sau khi remote JSON apply thành công
}

public interface IMonetizationRules : IService<IMonetizationRules>
{
    bool CanShowBanner(int currentLevel);
    bool CanShowInterstitial(in AdPlacement placement, int gateLevel);
    bool CanShowRewarded(in AdPlacement placement, int currentLevel);
    int  EffectiveInterstitialIntervalSeconds { get; }   // day-active override ‖ mặc định
}

public interface IMonetizationScenarioService : IService<IMonetizationScenarioService>
{
    EMonetizationScenario Scenario { get; }
    void EnsureInitialized();                     // phân kịch bản lần đầu từ segment
    void TickDayTransitions();                    // gọi ở mỗi lần mở app / sang ngày
    void NotifyRealMoneyPurchase();               // → Purchase, cập nhật mốc thời gian
    event Action<EMonetizationScenario> Changed;
}
```

**Tra interval theo Day Active** — range `[MinDay, MaxDay)`, cận trên **mở**:

```json
{"enabled": true, "ranges": [
  {"minDay": 0, "maxDay": 2,      "seconds": 120},
  {"minDay": 2, "maxDay": 7,      "seconds": 100},
  {"minDay": 7, "maxDay": 999999, "seconds": 90}]}
```

```
TryGetIntervalByDayActive(day):
  !enabled ‖ ranges rỗng → false            ← false = dùng interval mặc định, KHÔNG override
  duyệt theo thứ tự, range ĐẦU TIÊN chứa day thắng
  trả seconds > 0 ? true : false
```

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| `day = 0` | rơi `[0,2)` → 120 | ✓ |
| `day = 2` | `[0,2)` loại (cận trên mở) → `[2,7)` → 100 | ✓ không nhập nhằng ở biên |
| `day = 500` | `[7,999999)` → 90 | "7+" biểu diễn bằng `maxDay` rất lớn ✓ |
| `enabled = false` | `false` → dùng `MinInterstitialIntervalSeconds` | tắt feature không cần xoá ranges ✓ |

**Chuyển kịch bản**

```
EnsureInitialized():
  đã init → return
  scenario = map(IUserSegmentProvider.Segment)          ← Blended→Blended, Ads→Ads, IAP→IapCamp, …
  scenarioSinceUtc = now ;  initialized = true          ← lưu §2

TickDayTransitions():
  days = (now − scenarioSinceUtc).TotalDays
  noIapSinceEntered = lastIapUtc < scenarioSinceUtc     ← ⚠️ so với mốc VÀO kịch bản, không phải "có IAP bao giờ chưa"
  Blended  && days ≥ DaysBlendedToAds && noIapSinceEntered → Ads
  IapCamp  && days ≥ DaysIapCampToAds && noIapSinceEntered → Ads

NotifyRealMoneyPurchase():
  lastIapUtc = now
  scenario ∈ {Blended, Ads, IapCamp} → Purchase, scenarioSinceUtc = now
```

**Luồng áp interval vào §13**

```
Bootstrap ──► tracker.Touch() ──► config.LoadCachedJson()      ← chạy được TRƯỚC khi remote về
          ──► scenario.EnsureInitialized() + TickDayTransitions()
          ──► Resync()

RemoteConfig fetched ──► config.ApplyRemoteJson(json)
                            ├─ parse fail → giữ config cũ + log (KHÔNG rơi về default)
                            └─ ok → cache lại prefs → Changed → Resync()

resume (§4 OnPauseChanged) ──► tracker.Touch() ──► TickDayTransitions() ──► Resync()

Resync(): interval = TryGetIntervalByDayActive(day) ? s : MinInterstitialIntervalSeconds
          IInterstitialAds impl nhận interval mới      ← qua contract §13, KHÔNG gọi vendor
```

**Quyết định thiết kế**

| Quyết định | Vì sao | Đánh đổi |
|---|---|---|
| Xây **TRÊN** §13, không gọi vendor | Rule là logic sản phẩm; vendor là chi tiết. Trộn vào nhau là mất cả hai | 1 lớp gián tiếp |
| `MonetizationRules` impl **thuần hàm** (không state) nhưng vẫn sau interface | Rule chỉ là hàm của (config, scenario, level) ⇒ không có state để hỏng, test không cần scene. Không làm `static class`: static thì không mock/không thay rule theo game được (D trong SOLID) | Phải truyền tham số vào mỗi lần gọi |
| Rule **tách khỏi** impl ads | Impl ads đã đủ phức tạp; nhồi rule vào đó là chỗ tangled kinh điển | — |
| **Một** JSON cho cả ads gate + interval + kinh tế | PM tune "ads nhiều hơn nhưng coin cũng nhiều hơn" trong một lần sửa, không lệch nhịp | JSON to, cần validate |
| Cache JSON vào prefs | Cold start **trước** khi Firebase fetch xong vẫn có config đúng của lần trước; không dùng default sai | Config trễ 1 phiên khi PM đổi |
| Parse fail → **giữ config cũ** | Rơi về default khi PM gõ sai JSON = tụt doanh thu toàn bộ user cho tới lúc sửa | Sai JSON có thể không được phát hiện → phải log + alert |
| `TryGetIntervalByDayActive` trả `bool` | `false` nghĩa "không override", khác hẳn "override bằng 0" | Caller phải xử lý 2 nhánh |
| Range cận trên **mở** `[min, max)` | Cận đóng cả hai đầu ⇒ `day = 2` khớp 2 range, hành vi phụ thuộc thứ tự | Phải nhớ quy ước |
| `noIapSinceEntered` so với mốc **vào kịch bản** | Người mua 1 lần từ 3 tháng trước không nên bị khoá khỏi chuyển kịch bản mãi mãi | Cần lưu 2 mốc thời gian |
| Day Active dùng ngày **local** | Ngoại lệ có chủ ý so với §4 — xem bảng trên | Cheat được bằng đổi timezone, nhưng chỉ giảm ads vài ngày |

**Cạm bẫy**
- Hardcode ngưỡng trong code → không A/B được, mà A/B chính là mục đích duy nhất của hệ này.
- Rơi về default khi remote fail (thay vì giữ cache) → mất doanh thu im lặng.
- `PlayerPrefs.Save()` mỗi lần `Touch()` → I/O mỗi lần resume. Chỉ save khi giá trị thực sự đổi.
- Áp interval bằng cách `FindObjectOfType<AdsManager>()` → coupling vào impl + chi phí tìm kiếm. Đi qua interface §13.
- Quên `Touch()` ở resume → Day Active đứng yên trong suốt phiên dài.
- Đếm Day Active bằng UTC trong khi PM đọc báo cáo theo local → lệch cohort 1 ngày.
- Chuyển kịch bản không phát event → UI vẫn hiện nút rewarded của kịch bản cũ.

**Xong khi:** §0.6 + không tham chiếu type gameplay nào · JSON sai không làm tụt config · lùi đồng hồ máy không giảm được Day Active · đổi kịch bản áp ngay lên mọi placement · cold start offline vẫn dùng đúng config phiên trước.

---

# Phụ lục A — Utilities nền mà nhiều hệ ở trên cần

Không phải "hệ thống", nhưng thiếu chúng thì §7, §15, §20 phải tự xoay. Nhỏ, đặt ở `Runtime/Utilities/`.

## A.1 `InterfaceReference<T>` — serialize interface trong inspector

**Bài toán.** Unity không serialize được field kiểu interface. Nhưng §7 cần khai `ITransitionAnimation[]` trên prefab, §16 cần `ITutorialTapTarget`, §17 cần `ITabSwitchFeedback`. Không có nó thì buộc phải khai class cụ thể ⇒ mất Dependency Inversion **đúng ở tầng prefab**, nơi vi phạm khó phát hiện nhất.

```csharp
[Serializable]
public struct InterfaceReference<T> where T : class
{
    [SerializeField] private UnityEngine.Object underlying;
    public T Value => underlying as T;
    public bool IsValid => underlying is T;
}
```

Phần runtime là ~15 dòng. (Property drawer để lọc object hợp lệ trong inspector là editor-only — không thuộc phạm vi tài liệu này, nhưng runtime **phải** tự kiểm `IsValid` và log khi gán sai.)

## A.2 `AddressableObjectReference<T>` — handle có chủ

**Bài toán.** `CLAUDE.md` yêu cầu track handle để `Release()`. Nhưng trên thực tế handle bị rải khắp nơi và quên release là rò RAM âm thầm — đặc biệt ở §7 (popup) và §20 (asset event).

```csharp
[Serializable]
public class AddressableObjectReference<T> where T : UnityEngine.Object
{
    [SerializeField] private AssetReferenceT<T> reference;
    public bool IsLoading { get; }
    public bool IsLoaded  { get; }
    public T Asset { get; }                              // null + log error nếu chưa load

    public UniTask<T> LoadAsync(IProgress<float> progress = null, CancellationToken ct = default);
    public void Unload();                                // Release handle + Asset = null
}
```

| Quyết định | Vì sao |
|---|---|
| Handle nằm **trong** wrapper, không ở call-site | Một chủ sở hữu duy nhất ⇒ chỗ release rõ ràng |
| `LoadAsync` idempotent (đã load thì trả luôn) | Nhiều nơi cùng cần asset, không load 2 lần |
| Handle `Failed` → release rồi thử lại | Handle lỗi giữ mãi thì retry không bao giờ được |
| Biến thể có key (`enum`/`int`) | Dựng bảng `key → asset` trong SO mà vẫn lazy-load |

**Cạm bẫy:** `Asset` truy cập trước khi load xong → null. Không tự động load ngầm trong getter (sẽ thành async ẩn khó lần) — log error và để caller sửa.

## A.3 Typed prefs — `Prefs<T>`

Cho giá trị lẻ không đáng dựng cả model (§2 dùng cho model lớn): "đã rate chưa", "đã xem tutorial X", "lần cuối hỏi ở level nào".

```csharp
public sealed class Prefs<T>
{
    public Prefs(string key, T defaultValue, bool syncToServer = false);
    public string Key { get; }
    public T Value { get; set; }                 // get: cache; set: ghi + phát Changed
    public bool HasValue { get; }
    public void Delete();
    public event Action<T> Changed;
}
// Chuyên biệt: PrefsInt · PrefsBool · PrefsFloat · PrefsString · PrefsDateTime
```

| Quyết định | Vì sao |
|---|---|
| Cache giá trị sau lần đọc đầu | Đọc prefs là I/O; đọc trong `Update` là bug hiệu năng phổ biến |
| `Changed` event | UI bám thẳng vào prefs không cần lớp trung gian |
| `syncToServer` cờ ở ctor | Đăng ký vào snapshot cloud (§2) ngay lúc khai báo, không phải nhớ ở chỗ khác |
| Chuyên biệt theo type thay chỉ generic JSON | `PrefsInt` dùng `GetInt` trực tiếp — không serialize JSON cho một số nguyên |

**Cạm bẫy:** dùng `Prefs<T>` cho **model lớn** ⇒ serialize/deserialize mỗi lần đọc + không có dirty/autosave. Model lớn thuộc §2. Và: `ForceRefresh` toàn cục (invalidate mọi cache) là cần thiết sau khi apply cloud snapshot — nếu không, cache RAM ghi đè dữ liệu vừa nhận từ server.

---

# Phụ lục B — Anti-pattern: 5 lỗi kiến trúc lặp lại

Đây là những lỗi **đã trả giá**; SDK tồn tại để xoá chúng. Mỗi lần thiết kế một hệ mới, đối chiếu lại danh sách này.

| # | Anti-pattern | Vì sao chết | Cách đúng |
|---|---|---|---|
| 1 | **God-blob state** — một object gom hết coin/level/settings/booster | Mọi hệ coupling vào cùng object; sửa 1 field dirty cả blob; không tách module được | Nhiều save-unit độc lập (§0.3, §2) |
| 2 | **Impl vendor tangled** — `AdsManager` thò tay vào popup/toast/liveops | Không port, không test, không mock; đổi vendor = sửa cả UI | Impl **chỉ** phát event; ai muốn làm gì thì subscribe (§13) |
| 3 | **Enum game-specific trong hệ generic** — `Vibration.PickBox`, `GameAction.MergeDone` | Hệ mất tính port ngay lập tức, dù code sạch | Vocabulary trung tính + `int`/interface do game map (§9, §16, §14c) |
| 4 | **Hai impl song song cùng một việc** — 2 pool, 2 event bus, 2 tutorial | Sửa bug 2 lần, hành vi lệch nhau, người mới không biết dùng cái nào | 1 nguồn sự thật; hợp nhất **trước** khi thêm tính năng (§16) |
| 5 | **Polling thay event** — quét toàn cây badge/UI mỗi frame | Chi phí thuần, và vẫn trễ | Event-driven + dirty flag + gộp cuối frame (§0.4, §11b) |

Hai lỗi phụ, nhỏ hơn nhưng rất hay gặp:

- **String làm khoá logic ở call-site** (gõ tay id clip, tên event, id placement mỗi lần dùng) → typo chỉ nổ lúc runtime, không refactor-rename được. Dùng `enum`/struct wrap `int` (§8, §12, §13). Phân biệt với **định danh ổn định ra ngoài** (save key, remote key, tên event trên dashboard) — ở đó `string` là đúng, nhưng phải là `const` (§0.4b).
- **Tính một lần trong `Start()`** cho thứ đổi lúc runtime (safe-area, banner height, remote config) → sai ngay khi điều kiện đổi. Nghe event (§6, §21).

---

# Phụ lục C — Ngoài phạm vi SDK (và vì sao)

| Hạng mục | Vì sao không đưa vào Horcrux |
|---|---|
| **Editor tooling** (level painter, JSON→SO, quick-access, viewer) | Schema mỗi game khác hẳn nhau; tool chỉ hữu ích khi bám sát schema đó. SDK-hoá phần editor là nhận nợ maintain cho N schema. Tool sống ở từng dự án |
| **Impl vendor** (MAX/AdMob, Firebase, Adjust, IAP, NiceVibrations) | Đổi thường xuyên, license riêng, coupling cao. SDK chỉ giữ contract (§13, §12, §9) |
| **Localization** | Đã có giải pháp trưởng thành sẵn; và bảng chữ là nội dung game. SDK chỉ dùng **key** (§16 `textKey`), không sở hữu bảng dịch |
| **Gameplay mechanic** (board, match, physics của một game cụ thể) | Định nghĩa của game, không phải hạ tầng |
| **Nội dung event live-ops cụ thể** (chest, battle pass) | Nội dung; SDK giữ **khung host** (§20) |
| **Mapping rating → tham số mechanic** | Của game; SDK dừng ở lõi toán + target rating (§19.6) |
| **Push notification / deep-link** | Cấu hình per-app nặng (cert, entitlement, store console); có thể thêm sau ở tầng contract mỏng nếu thực sự lặp lại |

---

*Tài liệu thiết kế. Khi bắt đầu hiện thực một hệ: đọc §0 → đọc mục của hệ đó → viết spec/plan riêng theo `MY_SKILL.md` §5.3, đặt file `.md` cạnh `Implementations/` của hệ (MY_SKILL §5), rồi cập nhật dòng tương ứng ở bảng tổng quan.*




