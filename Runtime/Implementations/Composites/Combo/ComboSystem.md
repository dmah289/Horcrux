# Combo System Implementation Plan

> **Loại tài liệu:** Plan (`DOCS_SKILL` Phần C). `.md` thiết kế (Phần A) + `.html` (Phần B) viết **sau** khi có source — lúc đó `SKILL.md` quy tắc 4 mới coi là đủ.
>
> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development hoặc superpowers:executing-plans. Steps dùng checkbox (`- [ ]`).

**Goal:** `IComboSystem` — lõi combo **thuần logic** (đếm streak · luật đứt cắm được · bậc · hệ số nhân) phát 3 event; toàn bộ trải nghiệm **thính + thị + xúc giác** đến từ một cầu nối mỏng sang hệ Feedback. Thả vào project trống là chạy; thiếu mọi service phụ trợ vẫn không crash.

**Architecture:** **11 file** (3 contract + 3 policy + 5 impl), 4 tầng, mỗi tầng không biết tầng trên nó.

```
① ComboTracker          C# thuần — không Unity, không FX. Toàn bộ luật. Unit-test được.
② ComboSystem           MonoBehaviour: bơm tick từ ITicker, đóng băng khi app pause.
③ ComboFeedbackBridge   Dịch 2 event → FeedbackCue. Xoá file này thì combo vẫn chạy.
④ ComboMeter            UI: thanh cửa sổ + pop scale + màu theo bậc.
```

**Tech Stack:** C#, `Unity.Mathematics`, `Easer` ✅, `SquashStretch` ✅, `ITicker`, `IFeedbackDispatcher` *(optional)*, `UnityEngine.UI.Image`, `Sisus.Init`.

## Phân loại: `Composite`

Ghép **Ticker** (Foundation, **bắt buộc**) + **Feedback** (Composite, **tuỳ chọn**). Đặt ở `Composites/Combo/` — khớp vị trí sẵn có của `IComboSystem.cs`/`ComboSystem.cs`.

## Global Constraints

| Ràng buộc | Giá trị |
|---|---|
| Namespace | `Horcrux.Runtime.Abstractions.Combo` · `…Implementations.Combo` — theo quy ước hiện có của SDK (**không** mang `Composites` vào namespace, giống `Abstractions.Pooling`) |
| Zero-GC | 3 DTO là `readonly struct`; không alloc trong `Push`/`Tick`; tracker không alloc sau ctor |
| SOLID | Lõi không biết FX (S) · luật đứt sau 1 interface 1-method (I + O) · Unity chỉ ở tầng ②③④ (D) |
| Trung tính | Không type/enum nào mang tên mechanic game |
| Thời gian | `unscaledDeltaTime` — hitstop đặt `timeScale = 0` nhưng cửa sổ combo **không** đóng băng theo |
| Pause | App vào background phải **đóng băng** cửa sổ (nếu không: combo chết trong lúc người chơi không hề chơi) |
| Text/TMP | ⚠️ `com.horcrux.runtime.asmdef` **không** reference TextMeshPro (đã kiểm). Game bind chữ thẳng vào `IComboSystem.Beat`; SDK không chạm chữ |
| Editor-first (§C.1) | Luật đứt + hệ số nhân + mốc bậc đều là **tham số trong Inspector**; màu/thanh/pop gán ở Inspector. Không hằng số feel nào hardcode (§0.5) |

## Ngữ cảnh đã chốt

| Nhóm | Chốt |
|---|---|
| **Ai gọi** | **Chưa có** — SDK-first. Vì vậy plan có **Task 6: `ComboDemoDriver`** làm caller đầu tiên và thật. Không có nó thì mọi thứ ở đây là code chưa ai gọi. |
| **Mục tiêu** | Người chơi **cảm** được đà combo bằng 3 giác quan, và **thấy** cửa sổ đang cạn để có áp lực giữ chuỗi. Nghiệm thu: ① bịt một giác quan, hai giác quan còn lại vẫn truyền tải được đà · ② nhìn meter là biết còn bao lâu · ③ combo 1 và 10 khác nhau rõ ở tiếng, hình, rung. |
| **Ngân sách** | `Push` theo sự kiện, cao điểm ~10–20 lần/giây (cascade). `Tick` mỗi frame, **một** listener. Cả hai phải **0 B alloc**. |
| **Ranh giới** | SDK giữ **counter · luật đứt · bậc · hệ số nhân**. SDK **không** giữ: điểm số (chỉ trả `Multiplier`) · kỷ lục · nhãn bậc · asset. |
| **Cơ chế đứt** | Cả 3 (cửa sổ thời gian · cascade · theo move) gộp về **một** hàm `GetWindowSeconds(count)` (§0.2). |
| **Số track** | **Một** combo global. Multi-track thêm sau chỉ là `Dictionary<id, ComboTracker>` ở tầng ② — state đã nằm gọn trong tracker. |
| **Cố ý KHÔNG làm + lý do** (NT 6: *xóa đi thì hỏng ở đâu*) | ① **`ComboTier` + `IComboTierTable` + `ComboTierTableSO`** — `LabelKey`/`Multiplier` của bậc không ai đọc (nhãn là content của game; hệ số nhân dùng đường Linear), nên `ComboTier` co lại thành một `int` ⇒ cả interface + SO vô nghĩa. Bậc combo giờ là **`int[] tierMinCounts`** trên Inspector. ② **`IComboMultiplierCurve` + 2 class multiplier + enum mode** — chỉ còn **một** implementation (Linear); interface cần implementation thứ hai mới đáng tồn tại (§C.2) ⇒ công thức vào thẳng tracker với 2 tham số. ③ **3 hook `protected virtual` của `ComboMeter`** — game bind chữ thẳng vào `IComboSystem.Beat` **đơn giản hơn** kế thừa; cắt xong `ComboMeter` thành `sealed`. ④ **`SetStrategies`** — không caller; enum trong Inspector đã phủ cấu hình. ⑤ **`brokenCueId` ở bridge** — demo để 0; feedback lúc đứt là meter tắt, không cần cue. ⑥ Lưu best-combo + telemetry (cần §2/§12; `ComboSummary` đã mang đủ dữ liệu để game tự lo) · `IComboSystem.Score` (điểm là kinh tế của game) · `ChainReaction` (xem "Giai đoạn 2"). |

---

## Nguyên liệu đã chuyển từ `Pendings.md`

Ba item **combo-exclusive** đã **xoá khỏi** `Pendings.md` và chuyển về đây. Item **dùng chung** thì để lại, chỉ trỏ link.

| Item gốc | Nhà mới |
|---|---|
| Nhóm 8-L · **`ComboMeter`** — *"thanh combo phồng/co theo streak"* | **Task 5** — thanh co theo cửa sổ còn lại + pop mỗi nhịp + đổi màu theo bậc |
| Nhóm 8-J · **`HapticPattern`** — *"pitch ramp phiên bản xúc giác"* | `HapticRampChannel` (`FeedbackSystem.md` Task 4) — **đổi tên** để không trùng struct `HapticPattern` của §9 |
| Nhóm 7-F · **`ChainReaction`** — *"combo lan truyền + pitch ramp"* | **"Giai đoạn 2"** ở cuối file này (chưa vào phạm vi) |

Hai "công thức" ASMR trong `Pendings.md` cũng thuộc combo:

| Công thức | Hiện thực |
|---|---|
| **SquashStretch + Hitstop + pitch ramp** | Cue nhịp (`101`) có mặt ở `AudioPitchRampChannel` + `ComboMeter` (pop); cue lên-bậc (`102`) ở `HitstopChannel` |
| **CameraShake + Haptic + pitch ramp** | Cue `101` có mặt thêm ở `CameraShakeChannel` + `HapticRampChannel` |

**Nguyên liệu dùng chung** — vẫn ở `Pendings.md`, hệ này chỉ *tiêu thụ*: `Easer` ✅ · `SquashStretch` ✅ · `AudioPitchHelper` ✅ · `TraumaShake` (plan ở `FeedbackSystem.md`) · `Overshoot`/`ColorFlash`/`CountUpAnimator`/`StaggerHelper` (chưa làm).

---

## §0. Năm luật của hệ

### 0.1. Chỉ có MỘT state thật

| Đại lượng | Bản chất | Tính từ đâu |
|---|---|---|
| `count` | **state** duy nhất | tăng ở `Push`, về 0 ở `Break` |
| `windowSeconds` | hàm thuần của `count` | `IComboWindowPolicy` |
| `multiplier` | hàm thuần của `count` | `min(1 + s·count, max)` |
| `tierIndex` | hàm thuần của `count` | quét ngược `tierMinCounts` |

Ba cái dưới là **hàm thuần của `count`** — tính chất quan trọng nhất của thiết kế: **không có state trùng lặp** (không thể có ca "multiplier lệch với count").

```
game ──Push(steps)──► count += steps ; elapsed = 0
                      ├─► window / multiplier / tier  ← tính lại ở ĐÚNG MỘT chỗ
                      ├─► Beat        (mọi nhịp)
                      └─► TierChanged (chỉ khi bậc đổi)

ITicker ──OnTick(dt)──► elapsed += dt ; elapsed ≥ window → Break() → Broken(summary) ; count = 0
game ──Break()────────► đứt chủ động (move sai, thua)
game ──Reset()────────► count = 0, KHÔNG phát event (đổi level)
```

### 0.2. Ba cơ chế đứt gộp về một hàm

```
w(n) = lerp(w0, wMin, saturate(n / nMin))        // nMin ≤ 0 ⇒ dùng wMin ngay
```

Chọn dạng nội suy-theo-bậc (không phải hệ số trừ `k` giây/nhịp, không phải hàm mũ `λ`) vì **cả ba tham số đều là câu nói được**: *"nhịp đầu cho 1.2s, siết dần, tới nhịp 10 thì còn 0.4s"*. Và nó dùng lại đúng idiom `u` của `FeedbackSystem.md` §0.2 — nhất quán giữa các hệ là một tính năng.

Vì sao **co dần**: cửa sổ cố định làm combo thành bài kiểm tra tốc độ gõ — giữ được một lần là giữ được mãi ⇒ chuỗi dài vô hạn và hệ số nhân vỡ kinh tế. Co dần tạo đường cong căng, chuỗi **tự** kết thúc ở mức tuỳ kỹ năng.

Ba cơ chế, một hàm:

| Cơ chế | `GetWindowSeconds(n)` trả về | Vì sao đúng |
|---|---|---|
| Cửa sổ thời gian | `w(n)` co dần | trực tiếp |
| **Cascade** (một đợt resolve) | hằng số nhỏ, `Fixed(0.12)` | nhịp trong cùng đợt nổ cách nhau vài frame ⇒ nối được; đợt sau cách xa hơn ⇒ đứt. **Lõi không cần biết gì về board** |
| Theo move/lượt | `float.PositiveInfinity` | không timeout ⇒ chỉ đứt khi game gọi `Break()` |

`+∞` là giá trị **hợp lệ và có nghĩa** ("cửa sổ dài vô hạn"), không phải sentinel bẩn — trong `Tick` một phép `float.IsPositiveInfinity` là đủ để bỏ nhánh timeout.

| Mốc (`w0=1.2, wMin=0.4, nMin=10`) | Kỳ vọng |
|---|---|
| `n = 0` / `5` / `10` / `100` | `1.2` / `0.8` / `0.4` / `0.4` (không xuống dưới sàn) |
| `nMin = 0` | `0.4` ngay từ nhịp 0 |
| `wMin > w0` (khai ngược) | cửa sổ **giãn** dần — hợp lệ, có ca dùng |

### 0.3. Hệ số nhân: bất biến `m(0) = 1`, và trần là bắt buộc

```
m(n) = min(1 + perStep · n, maxMultiplier)
```

| Chi tiết | Vì sao |
|---|---|
| Số hạng hằng là **1** | Bất biến `m(0) = 1`: không có combo thì không có thưởng. Viết `s·n` (thiếu số 1) làm **mọi điểm ở trạng thái không-combo thành 0** |
| Trần là tham số **thiết kế kinh tế** | Không trần thì một chuỗi dài cho điểm nhiều hơn cả phần còn lại của trận cộng lại, làm mọi cân bằng khác vô nghĩa |
| Kẹp `maxMultiplier ≥ 1` | Trần dưới 1 khiến combo **giảm** điểm — chắc chắn lỗi khai, và phá bất biến `m(0) = 1` |

| Mốc (`perStep = 0.1`, `max = 5`) | `n = 0` | `10` | `40` | `1000` |
|---|---|---|---|---|
| Kỳ vọng | **`1.0`** | `2.0` | `5.0` | `5.0` |

### 0.4. Phân giải bậc: quét ngược `tierMinCounts`

Bậc hiện tại = mốc **cuối cùng** có `tierMinCounts[i] ≤ count`; không có thì `−1`.

```
for (i = length − 1 ; i ≥ 0 ; i--)
    if (count >= tierMinCounts[i]) return i
return -1
```

| Quyết định | Lý do |
|---|---|
| Quét **ngược** | Bậc cao thoả cũng thoả mọi bậc thấp ⇒ lần khớp đầu tiên khi đi từ cuối về là đúng; quét xuôi phải đi hết mảng |
| Tuyến tính, **không** binary search | Số bậc thực tế 3–6; binary search trên 5 phần tử chậm hơn và nhiều code hơn |
| Trả `−1`, không `0` | `0` là *bậc đầu tiên*, một trạng thái thật |
| Bậc là `int[]`, **không** có `ComboTier`/SO | Nhãn bậc là content của game (game tự map `tierIndex` → chữ đã dịch); hệ số nhân dùng đường Linear ⇒ mọi field khác của một "tier" đều không có người đọc |

**Bất biến `tierMinCounts` tăng dần là điều kiện đúng đắn**, không phải khuyến nghị: mảng `{6,3,10}` cho `count=5` sẽ khớp mốc `3` ở index 1 ⇒ trả bậc **sai** mà không có lỗi nào. `OnValidate` phải cảnh báo.

| Mốc (`{3, 6, 10}`) | `0` | `2` | `3` | `5` | `6` | `100` | mảng rỗng |
|---|---|---|---|---|---|---|---|
| Kỳ vọng | `−1` | `−1` | `0` | `0` | `1` | `2` | `−1` |

### 0.5. Số cảm giác + năm bất biến vòng đời

Số cảm giác — **không** dẫn ra từ đâu, chọn bằng cách chơi thử (NT 7):

| Số | Khởi đầu | Tune ở đâu |
|---|---|---|
| Cửa sổ ở nhịp 0 / sàn / số nhịp chạm sàn | 1.2s / 0.4s / 10 | Inspector `ComboSystem` |
| Cửa sổ cascade (mode `Fixed`) | ~0.12s ≈ 7 frame @60FPS | Inspector `ComboSystem` |
| Hệ số nhân mỗi nhịp / trần | +0.1 / 5 | Inspector `ComboSystem` |
| Mốc bậc | 3 / 6 / 10 | Inspector `ComboSystem` |
| Thời lượng pop / độ nén pop | 0.22s / 0.8 | Inspector `ComboMeter` |
| Màu từng bậc | — | Inspector `ComboMeter` |

Năm bất biến phải luôn đúng:

| # | Bất biến | Vì sao |
|---|---|---|
| 1 | `IsActive ⟺ count > 0` | Không có cờ `_isActive` riêng ⇒ không thể lệch với `count` |
| 2 | `tierIndex` khớp `count` mọi lúc | Tính lại ở đúng một chỗ (`Push`), reset cùng `count` |
| 3 | `WindowProgress ∈ [0,1]`, `1` khi cửa sổ `+∞` | UI không cần biết `elapsed`, và không bao giờ chia cho vô cực |
| 4 | `Broken` phát **SAU** khi state đã reset | Listener của `Broken` thường đọc `Count` để cập nhật UI. Phát *trước* ⇒ nó đọc giá trị cũ rồi UI bị reset ngay sau ⇒ nhấp nháy. Dữ liệu cần đã nằm trong `ComboSummary` |
| 5 | `Beat` phát **TRƯỚC** `TierChanged` | Nhịp *gây ra* việc lên bậc; đảo thứ tự làm cue "lên hạng" đến trước cue "nhịp" ⇒ nghe sai nhân quả |

Hai ca biên xử lý tường minh:

| Ca | Xử lý | Vì sao |
|---|---|---|
| `Break()` khi `count == 0` | **không** phát `Broken` | Không có combo thì không có gì "đứt"; phát ra sẽ khiến game hiện UI "combo đứt!" vô cớ |
| `Push(steps ≤ 0)` | no-op | Cascade **rỗng** không được reset cửa sổ — nếu reset thì đợt rỗng lại *gia hạn* combo |

---

## Bản đồ triển khai

| Task | File | Nội dung |
|---|---|---|
| 1 | `Abstractions/Composites/Combo/` — `ComboEvents.cs` · `IComboWindowPolicy.cs` · `IComboSystem.cs` (**ghi đè** stub) | contract |
| 2 | `Implementations/…/Combo/Windows/` — `Fixed` · `Shrinking` · `Manual` | 3 window policy |
| 3 | `ComboTracker.cs` | **toàn bộ luật**, C# thuần |
| 4 | `ComboSystem.cs` (**ghi đè** stub — namespace hiện tại bị lặp, sai) | tầng Unity |
| 5 | `ComboFeedbackBridge.cs` · `ComboMeter.cs` | nối 3 giác quan + UI |
| 6 | `Demo/ComboDemoDriver.cs` | **caller đầu tiên** + nghiệm thu |

Thứ tự: **1 → 2 → 3 → 4 → 5 → 6**.

---

### Task 1: Contract

**Files:** `Assets/Horcrux/Runtime/Abstractions/Composites/Combo/` — `ComboEvents.cs` · `IComboWindowPolicy.cs` · **ghi đè** `IComboSystem.cs`

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| Namespace bỏ `Composites` | Quy ước hiện có: `Abstractions/Foundations/Pooling/` → `Abstractions.Pooling`. Stub hiện tại lệch với mọi file khác |
| **3** event struct riêng, không một `ComboChanged` chung | Ba sự kiện có ba **tập dữ liệu** khác nhau (nhịp cần `Steps`; lên bậc cần bậc cũ; đứt cần thời lượng). Gộp = struct có field vô nghĩa ở 2/3 ca, và listener phải `if` để biết chuyện gì xảy ra |
| `event Action<T>` với `T` là struct | Đăng ký/huỷ ở đây **thưa** (meter + bridge sống suốt màn) ⇒ `event` là đủ; `Action<T>` generic không boxing struct. Ngược lại `ITicker` dùng interface vì đăng ký/huỷ ở đó là hot path |
| `ComboSummary.Count`, không `MaxCount` | `count` chỉ tăng cho tới khi đứt ⇒ tại lúc đứt nó **là** đỉnh. Hai tên cho cùng một số là mời gọi hiểu sai |
| `IComboSystem.WindowProgress` (không lộ `elapsed`/`window`) | Mọi UI cần đúng tỉ lệ đó; để mỗi UI tự chia là N bản sao và chắc chắn một bản quên ca `+∞` |
| `Reset()` tách khỏi `Break()` | Đổi level phải xoá combo mà **không** kích feedback "combo đứt!". Một hàm với cờ `bool silent` là chữ ký khó đọc ở call-site |
| `Push(int steps = 1)` một hàm | Cascade nhiều nhịp và nhịp đơn là cùng một phép |

- [ ] **Step 1: `ComboEvents.cs`**

```csharp
namespace Horcrux.Runtime.Abstractions.Combo
{
    /// <summary>Một nhịp combo vừa xảy ra.</summary>
    /// <remarks>
    /// Ba event của combo là ba struct RIÊNG (không một <c>ComboChanged</c> chung) vì chúng có ba tập
    /// dữ liệu khác nhau. Gộp lại là struct có field vô nghĩa ở 2/3 trường hợp, và listener phải
    /// <c>if</c> để biết chuyện gì vừa xảy ra.
    /// </remarks>
    public readonly struct ComboBeat
    {
        /// <summary>Số nhịp SAU khi đã tăng.</summary>
        public readonly int Count;

        /// <summary>Số nhịp cộng trong lần này (cascade có thể &gt; 1).</summary>
        public readonly int Steps;

        /// <summary>Bậc hiện tại; <c>-1</c> = chưa đạt bậc nào.</summary>
        public readonly int TierIndex;

        public readonly float Multiplier;

        public ComboBeat(int count, int steps, int tierIndex, float multiplier)
        {
            Count = count;
            Steps = steps;
            TierIndex = tierIndex;
            Multiplier = multiplier;
        }
    }

    /// <summary>Bậc combo vừa đổi.</summary>
    public readonly struct ComboTierChange
    {
        public readonly int PreviousTierIndex;
        public readonly int TierIndex;
        public readonly int Count;

        /// <summary>Lên bậc (chứ không phải tụt do reset) — quyết định nên ăn mừng hay không.</summary>
        public bool IsUpgrade => TierIndex > PreviousTierIndex;

        public ComboTierChange(int previousTierIndex, int tierIndex, int count)
        {
            PreviousTierIndex = previousTierIndex;
            TierIndex = tierIndex;
            Count = count;
        }
    }

    /// <summary>Tổng kết một chuỗi combo vừa kết thúc.</summary>
    /// <remarks>
    /// Có <c>Count</c> chứ không <c>MaxCount</c>: <c>count</c> chỉ tăng cho tới khi đứt, nên tại thời
    /// điểm này nó CHÍNH LÀ đỉnh.
    ///
    /// Đây là toàn bộ dữ liệu game cần để lưu kỷ lục / bắn telemetry — nên combo KHÔNG phụ thuộc hệ
    /// save hay hệ analytics.
    /// </remarks>
    public readonly struct ComboSummary
    {
        public readonly int Count;
        public readonly int TierIndex;

        /// <summary>Thời lượng chuỗi (giây thực, từ nhịp đầu).</summary>
        public readonly float DurationSeconds;

        /// <summary><c>true</c> = game gọi <c>Break()</c>; <c>false</c> = hết cửa sổ.</summary>
        public readonly bool WasManualBreak;

        public ComboSummary(int count, int tierIndex, float durationSeconds, bool wasManualBreak)
        {
            Count = count;
            TierIndex = tierIndex;
            DurationSeconds = durationSeconds;
            WasManualBreak = wasManualBreak;
        }
    }
}
```

- [ ] **Step 2: `IComboWindowPolicy.cs`**

```csharp
namespace Horcrux.Runtime.Abstractions.Combo
{
    /// <summary>Luật đứt combo — MỘT method gộp cả ba cơ chế thường dùng.</summary>
    /// <remarks>
    /// Ba cơ chế và cách chúng gộp lại (§0.2):
    ///  • <b>Cửa sổ thời gian</b> → trả cửa sổ co dần theo <paramref name="currentCount"/>.
    ///  • <b>Cascade một đợt resolve</b> → trả hằng số nhỏ (~0.12s): nhịp trong cùng đợt nổ cách nhau
    ///    vài frame nên nối được, đợt sau cách xa hơn nên đứt. Lõi KHÔNG cần biết gì về board.
    ///  • <b>Theo move/lượt</b> → trả <c>float.PositiveInfinity</c>: chỉ đứt khi game gọi <c>Break()</c>.
    ///
    /// <c>+∞</c> là giá trị HỢP LỆ và có nghĩa, không phải sentinel bẩn.
    /// </remarks>
    public interface IComboWindowPolicy
    {
        /// <param name="currentCount">Số nhịp SAU khi đã tăng.</param>
        /// <returns>Giây tối đa được phép giữa nhịp này và nhịp kế. <c>+∞</c> = không tự đứt.</returns>
        float GetWindowSeconds(int currentCount);
    }
}
```

- [ ] **Step 3: Ghi đè `IComboSystem.cs`**

```csharp
using System;

namespace Horcrux.Runtime.Abstractions.Combo
{
    /// <summary>Lõi combo: đếm nhịp liên tiếp, tự đứt theo luật cắm được, phát bậc + hệ số nhân.</summary>
    /// <remarks>
    /// Hệ này là LOGIC THUẦN — không phát âm, không rung, không vẽ. Toàn bộ trải nghiệm đa giác quan
    /// đến từ <c>ComboFeedbackBridge</c> dịch event thành cue của hệ Feedback. Nhờ vậy combo chạy được
    /// trong project không có audio/haptic/camera.
    ///
    /// SDK KHÔNG giữ điểm: chỉ trả <see cref="Multiplier"/>, game tự nhân.
    /// SDK KHÔNG giữ nhãn bậc: chỉ trả <see cref="TierIndex"/>, game tự map sang chữ đã dịch.
    ///
    /// Một track global. Multi-track là mở rộng ở tầng <c>ComboSystem</c> — state đã nằm gọn trong
    /// <c>ComboTracker</c> nên thêm sau chỉ là một Dictionary, không đập lõi.
    /// </remarks>
    public interface IComboSystem : IService<IComboSystem>
    {
        /// <summary>Số nhịp liên tiếp hiện tại. <c>0</c> = không có combo.</summary>
        int Count { get; }

        /// <summary>Bậc hiện tại; <c>-1</c> = chưa đạt bậc nào.</summary>
        int TierIndex { get; }

        /// <summary>Hệ số nhân điểm hiện tại. Luôn <c>1</c> khi <see cref="Count"/> là 0.</summary>
        float Multiplier { get; }

        /// <summary>
        /// Tỉ lệ cửa sổ còn lại [0,1] — <c>1</c> ngay sau một nhịp, <c>0</c> khi sắp đứt.
        /// Trả <c>1</c> khi cửa sổ là <c>+∞</c> hoặc không có combo.
        /// </summary>
        /// <remarks>
        /// Ở đây (không lộ <c>elapsed</c>/<c>window</c> để UI tự chia) vì mọi UI cần đúng tỉ lệ này,
        /// và chắc chắn sẽ có một UI quên xử lý ca <c>+∞</c>.
        /// </remarks>
        float WindowProgress { get; }

        bool IsActive { get; }

        /// <summary>Cộng nhịp. <paramref name="steps"/> &gt; 1 cho cascade trả nhiều nhịp cùng frame.</summary>
        /// <remarks><c>steps ≤ 0</c> là no-op — cascade RỖNG không được gia hạn combo.</remarks>
        void Push(int steps = 1);

        /// <summary>Đứt chủ động (move sai, thua màn). Không làm gì nếu đang không có combo.</summary>
        void Break();

        /// <summary>Xoá combo IM LẶNG — không phát event nào. Dùng khi đổi level/scene.</summary>
        void Reset();

        /// <summary>Mỗi nhịp. Phát TRƯỚC <see cref="TierChanged"/> (nhịp gây ra việc lên bậc).</summary>
        event Action<ComboBeat> Beat;

        event Action<ComboTierChange> TierChanged;

        /// <summary>Chuỗi kết thúc. Phát SAU khi state đã reset — listener đọc <c>Count</c> thấy 0.</summary>
        event Action<ComboSummary> Broken;
    }
}
```

- [ ] **Step 4: Kiểm chứng** — `new ComboTierChange(0, 1, 6).IsUpgrade == true` · `new ComboTierChange(2, -1, 0).IsUpgrade == false` · project biên dịch được khi không có folder `Abstractions/Composites/Feedback` (kiểm bằng cách tạm rename nó).

- [ ] **Step 5: Commit** — `feat(sdk): add combo contracts`

---

### Task 2: 3 window policy

**Files:** `Implementations/Composites/Combo/Windows/` — `FixedComboWindow.cs` · `ShrinkingComboWindow.cs` · `ManualComboWindow.cs`

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| 3 class nhỏ thay 1 class có `enum mode` | `switch` trên mode = mỗi luật mới phải sửa file đang chạy ổn (vi phạm O), và class đó dần mang tham số của mọi luật |
| Giữ `IComboWindowPolicy` dù có thể gộp 3 công thức | Có **3 implementation** ⇒ interface đáng tồn tại (§C.2). Và đây là abstraction duy nhất user chỉ định rõ ("3 cơ chế cắm được") — cắt nó là cắt vào mục đích ban đầu |
| Tham số `readonly` gán ở ctor | Policy **bất biến** sau khi dựng ⇒ không thể bị đổi giữa chuỗi combo (nguồn bug: cửa sổ đổi mà `elapsed` không reset) |
| `ManualComboWindow` là singleton `Instance` | Không state ⇒ một instance dùng chung; tránh alloc ở mọi lần dựng |
| Precompute `_span = wMin − w0` | Bỏ một phép trừ khỏi mỗi `Push`; biểu thức còn lại là một `mad` |
| Kẹp `≥ 0` ở ctor | Cửa sổ âm cho ra combo đứt **ngay** ở nhịp đầu — lỗi cấu hình rất khó lần |

- [ ] **Step 1: 3 file**

```csharp
// ── FixedComboWindow.cs ───────────────────────────────────────────────────
using Horcrux.Runtime.Abstractions.Combo;
using Unity.Mathematics;

namespace Horcrux.Runtime.Implementations.Combo
{
    /// <summary>Cửa sổ không đổi theo số nhịp.</summary>
    /// <remarks>
    /// Hai ca dùng:
    ///  • Game thư giãn (combo không phải trục thử thách) — giá trị ~1s.
    ///  • <b>Cascade một đợt resolve</b> — giá trị ~0.12s (≈7 frame @60FPS): nhịp trong cùng đợt nổ
    ///    nối được, đợt sau cách xa hơn nên combo đứt đúng lúc đợt kết thúc (§0.2).
    ///
    /// Nếu combo LÀ trục thử thách thì dùng <see cref="ShrinkingComboWindow"/>: cửa sổ cố định làm
    /// chuỗi dài vô hạn và hệ số nhân vỡ kinh tế.
    /// </remarks>
    public sealed class FixedComboWindow : IComboWindowPolicy
    {
        private readonly float _windowSeconds;

        /// <param name="windowSeconds">
        /// Giây tối đa giữa 2 nhịp; tự kẹp về ≥ 0 (cửa sổ âm ⇒ đứt ngay nhịp đầu).
        /// Số cảm giác — ~1s cho combo thường, ~0.12s cho cascade (§0.5).
        /// </param>
        public FixedComboWindow(float windowSeconds)
            => _windowSeconds = math.max(windowSeconds, 0f);

        public float GetWindowSeconds(int currentCount) => _windowSeconds;
    }
}

// ── ShrinkingComboWindow.cs ───────────────────────────────────────────────
using Horcrux.Runtime.Abstractions.Combo;
using Unity.Mathematics;

namespace Horcrux.Runtime.Implementations.Combo
{
    /// <summary>Cửa sổ co dần theo số nhịp — càng combo cao càng khó giữ.</summary>
    /// <remarks>
    /// Formula: <c>w(n) = lerp(w₀, w_min, saturate(n / n_min))</c> (§0.2).
    ///
    /// Ba tham số đều là câu nói được: "nhịp đầu cho <c>w₀</c> giây, siết dần, tới nhịp <c>n_min</c>
    /// thì còn <c>w_min</c>". Đó là lý do chọn dạng nội suy-theo-bậc thay vì hệ số trừ (giây/nhịp —
    /// trừu tượng) hay hàm mũ (không bao giờ thật sự chạm sàn).
    ///
    /// Khai <c>w_min &gt; w₀</c> là hợp lệ: cửa sổ GIÃN dần ("dễ dần").
    /// </remarks>
    public sealed class ShrinkingComboWindow : IComboWindowPolicy
    {
        private readonly float _startSeconds;
        private readonly float _span;          // = w_min − w₀, precompute: bỏ 1 phép trừ mỗi Push
        private readonly int _stepsToMin;

        /// <param name="startSeconds">w₀ — cửa sổ (giây) ở nhịp 0; tự kẹp về ≥ 0. Số cảm giác (§0.5).</param>
        /// <param name="minSeconds">
        /// w_min — cửa sổ (giây) sau khi đạt <paramref name="stepsToMin"/>; tự kẹp về ≥ 0.
        /// Lớn hơn <paramref name="startSeconds"/> là hợp lệ (cửa sổ giãn dần).
        /// </param>
        /// <param name="stepsToMin">n_min — số nhịp để chạm sàn. ≤ 0 ⇒ dùng sàn ngay từ nhịp 0.</param>
        public ShrinkingComboWindow(float startSeconds, float minSeconds, int stepsToMin)
        {
            _startSeconds = math.max(startSeconds, 0f);
            _span = math.max(minSeconds, 0f) - _startSeconds;
            _stepsToMin = stepsToMin;
        }

        public float GetWindowSeconds(int currentCount)
        {
            // n_min ≤ 0 ⇒ u = 1: biến "không có đường co" thành ca hợp lệ thay vì chia 0.
            float u = _stepsToMin <= 0 ? 1f : math.saturate((float)currentCount / _stepsToMin);
            return _startSeconds + _span * u;
        }
    }
}

// ── ManualComboWindow.cs ──────────────────────────────────────────────────
using Horcrux.Runtime.Abstractions.Combo;

namespace Horcrux.Runtime.Implementations.Combo
{
    /// <summary>Không bao giờ tự đứt — chỉ đứt khi game gọi <c>Break()</c> (combo theo move/lượt).</summary>
    /// <remarks>
    /// <c>+∞</c> là giá trị HỢP LỆ: nó nói đúng nghĩa "cửa sổ dài vô hạn", và trong <c>Tick</c> một
    /// phép <c>float.IsPositiveInfinity</c> là đủ để bỏ nhánh timeout (§0.2).
    ///
    /// Không state ⇒ dùng <see cref="Instance"/>, khỏi cấp phát ở mọi lần dựng.
    /// </remarks>
    public sealed class ManualComboWindow : IComboWindowPolicy
    {
        public static readonly ManualComboWindow Instance = new();

        private ManualComboWindow() { }

        public float GetWindowSeconds(int currentCount) => float.PositiveInfinity;
    }
}
```

- [ ] **Step 2: Kiểm chứng**

| Policy | Input | Kỳ vọng |
|---|---|---|
| `Fixed(1.0)` | `n = 0, 5, 999` | `1.0` mọi lần |
| `Fixed(-1)` | `n = 3` | `0` (kẹp) |
| `Shrinking(1.2, 0.4, 10)` | `n = 0 / 5 / 10 / 100` | `1.2 / 0.8 / 0.4 / 0.4` |
| `Shrinking(1.2, 0.4, 0)` | `n = 0` | `0.4` |
| `Shrinking(0.4, 1.2, 10)` | `n = 10` | `1.2` (giãn dần — hợp lệ) |
| `Manual.Instance` | `n` bất kỳ | `float.PositiveInfinity` |

- [ ] **Step 3: Commit** — `feat(sdk): add 3 combo window policies`

---

### Task 3: `ComboTracker` — toàn bộ luật, C# thuần

**Files:** Create `Implementations/Composites/Combo/ComboTracker.cs`

**Bản đồ toán → code:**

| §0 | Code |
|---|---|
| §0.1 3 đại lượng dẫn xuất | `ResolveWindow`/`ResolveMultiplier`/`ResolveTier` gọi **cùng một chỗ** trong `Push` |
| §0.2 `+∞` | `float.IsPositiveInfinity(_windowSeconds)` → `return` sớm trong `Tick` |
| §0.3 hệ số nhân | `math.min(1f + _multiplierPerStep * count, _maxMultiplier)` |
| §0.4 quét ngược bậc | vòng `for` giảm dần trên `_tierMinCounts` |
| §0.5 #4 | `Break`: dựng `summary` → `ResetState()` → **rồi mới** `Broken?.Invoke` |
| §0.5 #5 | `Push`: `Beat?.Invoke` **trước** `TierChanged?.Invoke` |

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| **C# thuần**, không MonoBehaviour | Toàn bộ luật test được không cần scene (cùng khuôn `StackStateMachine` §5). Và multi-track sau này chỉ là `Dictionary<id, ComboTracker>` ở tầng trên |
| Nhận `int[] tierMinCounts` thay `IComboTierTable` | Interface + SO + struct `ComboTier` đều chỉ để chở **một** `int` mỗi bậc ⇒ không gọi được tên chỗ hỏng nếu xóa (§C.2) |
| Nhận 2 `float` cho hệ số nhân thay `IComboMultiplierCurve` | Chỉ còn một implementation (Linear) ⇒ interface không đáng tồn tại |
| Không cờ `_isActive` | `IsActive => _count > 0` — bất biến #1 không thể lệch vì không có bản sao |
| `_duration` chỉ cộng khi `count > 0` | Thời lượng của "không có combo" là vô nghĩa; nó phải đếm từ nhịp **đầu** |
| `_wasManualBreak` là field tạm | Truyền qua tham số làm `Break()` public có tham số nội bộ — call-site game sẽ thấy và gọi sai |
| `SetSuspended` thay `IDisposable Scope()` | Nguồn duy nhất gọi nó là `ComboSystem` khi app pause (một nguồn ⇒ không cần ref-count) |
| Guard `float.IsNaN` khi resolve window | Policy do game viết có thể trả `NaN`; mọi so sánh với `NaN` là `false` nên combo sẽ *không bao giờ đứt* — sai im lặng. Đổi thành `+∞` tường minh và log |
| Fallback policy `null` → `+∞` | Tracker phải dựng được với cấu hình tối thiểu; `+∞` là fallback **an toàn**, khác `0` (đứt ngay) |
| Không `try/catch` quanh listener của 3 event | Khác `ITicker`: ở đây listener là **của SDK/game, số ít, biết trước** (meter + bridge). `try/catch` sẽ che lỗi trong bridge — thứ ta muốn thấy ngay |

- [ ] **Step 1: `ComboTracker.cs`**

```csharp
using System;
using Horcrux.Runtime.Abstractions.Combo;
using Unity.Mathematics;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Combo
{
    /// <summary>Toàn bộ luật combo — C# THUẦN, không MonoBehaviour, không FX.</summary>
    /// <remarks>
    /// Chỉ có MỘT state thật: <c>_count</c>. Cửa sổ, hệ số nhân và bậc đều là HÀM THUẦN của nó (§0.1)
    /// — nhờ vậy không thể có ca "multiplier lệch với count".
    ///
    /// Tách khỏi Unity (cùng khuôn <c>StackStateMachine</c> §5): test được không cần scene, và
    /// multi-track sau này chỉ là <c>Dictionary&lt;id, ComboTracker&gt;</c> ở tầng <c>ComboSystem</c>.
    ///
    /// Chủ sở hữu phải gọi <see cref="Tick"/> với thời gian THỰC (unscaled).
    /// </remarks>
    public sealed class ComboTracker
    {
        public const int NoTier = -1;

        private readonly IComboWindowPolicy _windowPolicy;
        private readonly int[] _tierMinCounts;          // có thể null/rỗng ⇒ TierIndex luôn NoTier
        private readonly float _multiplierPerStep;
        private readonly float _maxMultiplier;

        private int _count;
        private int _tierIndex = NoTier;
        private float _multiplier = 1f;
        private float _windowSeconds = float.PositiveInfinity;
        private float _elapsedSinceLastBeat;
        private float _durationSeconds;
        private bool _isSuspended;
        private bool _wasManualBreak;

        /// <param name="windowPolicy">Luật đứt. <c>null</c> ⇒ combo không bao giờ tự đứt.</param>
        /// <param name="tierMinCounts">
        /// Mốc số nhịp của từng bậc, PHẢI tăng dần (§0.4). <c>null</c>/rỗng ⇒ <c>TierIndex</c> luôn −1.
        /// Ai cấp: <c>ComboSystem</c> từ Inspector.
        /// </param>
        /// <param name="multiplierPerStep">Hệ số nhân cộng thêm mỗi nhịp; tự kẹp ≥ 0. Số cảm giác (§0.5).</param>
        /// <param name="maxMultiplier">Trần hệ số nhân; tự kẹp ≥ 1 (§0.3).</param>
        public ComboTracker(IComboWindowPolicy windowPolicy, int[] tierMinCounts = null,
                            float multiplierPerStep = 0.1f, float maxMultiplier = 5f)
        {
            _windowPolicy = windowPolicy;
            _tierMinCounts = tierMinCounts;
            _multiplierPerStep = math.max(multiplierPerStep, 0f);
            _maxMultiplier = math.max(maxMultiplier, 1f);
        }

        #region State
        public int Count => _count;
        public int TierIndex => _tierIndex;
        public float Multiplier => _multiplier;

        /// <summary>Không có cờ riêng: bất biến <c>IsActive ⟺ count &gt; 0</c> không thể lệch (§0.5 #1).</summary>
        public bool IsActive => _count > 0;

        /// <summary>Tỉ lệ cửa sổ còn lại [0,1]; <c>1</c> khi cửa sổ <c>+∞</c> hoặc không có combo.</summary>
        public float WindowProgress
        {
            get
            {
                if (!IsActive) return 1f;
                if (!float.IsFinite(_windowSeconds) || _windowSeconds <= 0f) return 1f;

                return math.saturate(1f - _elapsedSinceLastBeat / _windowSeconds);
            }
        }
        #endregion

        #region Events
        public event Action<ComboBeat> Beat;
        public event Action<ComboTierChange> TierChanged;
        public event Action<ComboSummary> Broken;
        #endregion

        #region Commands
        /// <summary>Cộng nhịp và tính lại 3 đại lượng dẫn xuất.</summary>
        /// <remarks>
        /// <paramref name="steps"/> ≤ 0 là no-op: một cascade RỖNG không được gia hạn combo (§0.5).
        /// </remarks>
        public void Push(int steps = 1)
        {
            if (steps <= 0) return;

            int previousTier = _tierIndex;

            _count += steps;
            _elapsedSinceLastBeat = 0f;

            // Ba đại lượng dẫn xuất, tính ở ĐÚNG MỘT chỗ (§0.1).
            _windowSeconds = ResolveWindow(_count);
            _multiplier = ResolveMultiplier(_count);
            _tierIndex = ResolveTier(_count);

            // Beat TRƯỚC TierChanged: nhịp gây ra việc lên bậc; đảo thứ tự làm cue "lên hạng" đến
            // trước cue "nhịp" ⇒ nghe sai nhân quả (§0.5 #5).
            Beat?.Invoke(new ComboBeat(_count, steps, _tierIndex, _multiplier));

            if (_tierIndex != previousTier)
                TierChanged?.Invoke(new ComboTierChange(previousTier, _tierIndex, _count));
        }

        /// <summary>Đứt chủ động. No-op khi đang không có combo (không có gì để "đứt").</summary>
        public void Break()
        {
            _wasManualBreak = true;
            BreakInternal();
        }

        /// <summary>Xoá combo IM LẶNG — không phát event nào.</summary>
        public void Reset() => ResetState();

        /// <param name="isSuspended"><c>true</c> = đóng băng cửa sổ (app ở background).</param>
        public void SetSuspended(bool isSuspended) => _isSuspended = isSuspended;

        /// <param name="unscaledDeltaTime">
        /// Giây THỰC. Không dùng scaled: hitstop đặt <c>timeScale = 0</c> nhưng cửa sổ combo phải tiếp
        /// tục cạn theo thời gian thật.
        /// </param>
        public void Tick(float unscaledDeltaTime)
        {
            // Đóng băng hoặc không có combo → thoát rẻ nhất. Đây là trạng thái phổ biến nhất.
            if (_isSuspended || _count <= 0) return;

            _durationSeconds += unscaledDeltaTime;

            // Cửa sổ vô hạn (combo theo move) → không có nhánh timeout (§0.2).
            if (float.IsPositiveInfinity(_windowSeconds)) return;

            _elapsedSinceLastBeat += unscaledDeltaTime;
            if (_elapsedSinceLastBeat < _windowSeconds) return;

            _wasManualBreak = false;
            BreakInternal();
        }
        #endregion

        #region Internals
        private void BreakInternal()
        {
            if (_count <= 0)
            {
                _wasManualBreak = false;
                return;                     // Break trùng / Break khi chưa có combo: im lặng
            }

            var summary = new ComboSummary(_count, _tierIndex, _durationSeconds, _wasManualBreak);

            // Reset TRƯỚC khi phát: listener của Broken thường đọc Count để cập nhật UI. Phát trước
            // khi reset ⇒ nó đọc giá trị cũ rồi UI bị reset ngay sau ⇒ nhấp nháy (§0.5 #4).
            ResetState();

            Broken?.Invoke(summary);
        }

        private void ResetState()
        {
            _count = 0;
            _tierIndex = NoTier;
            _multiplier = 1f;
            _windowSeconds = float.PositiveInfinity;
            _elapsedSinceLastBeat = 0f;
            _durationSeconds = 0f;
            _wasManualBreak = false;
        }

        private float ResolveWindow(int count)
        {
            // Fallback +∞ (không đứt), KHÔNG 0 (đứt ngay): tracker phải dựng được với cấu hình tối thiểu.
            if (_windowPolicy == null) return float.PositiveInfinity;

            float window = _windowPolicy.GetWindowSeconds(count);

            // Policy do game viết có thể trả NaN (chia 0). Mọi so sánh với NaN là false ⇒ combo sẽ
            // KHÔNG BAO GIỜ đứt, một lỗi im lặng. Đổi thành +∞ tường minh và log để lộ ra.
            if (float.IsNaN(window))
            {
                Debug.LogError("[Combo] IComboWindowPolicy trả NaN — coi như vô hạn. Kiểm tra impl policy.");
                return float.PositiveInfinity;
            }

            return math.max(window, 0f);
        }

        /// <remarks>Formula: <c>min(1 + perStep·n, max)</c> — số hạng hằng 1 giữ bất biến m(0)=1 (§0.3).</remarks>
        private float ResolveMultiplier(int count)
            => math.min(1f + _multiplierPerStep * count, _maxMultiplier);

        /// <remarks>Quét NGƯỢC: bậc cao thoả cũng thoả mọi bậc thấp ⇒ khớp đầu tiên là đúng (§0.4).</remarks>
        private int ResolveTier(int count)
        {
            if (_tierMinCounts == null) return NoTier;

            for (int i = _tierMinCounts.Length - 1; i >= 0; i--)
            {
                if (count >= _tierMinCounts[i]) return i;
            }

            return NoTier;
        }
        #endregion
    }
}
```

- [ ] **Step 2: Kiểm chứng** — cấu hình `ShrinkingComboWindow(1.0, 0.4, 10)`, `tierMinCounts = {3, 6}`, `perStep = 0.1`, `max = 5`:

| # | Chuỗi | Kỳ vọng |
|---|---|---|
| 1 | ban đầu | `Count=0`, `TierIndex=-1`, `Multiplier=1`, `IsActive=false`, `WindowProgress=1` |
| 2 | `Push()` | `Count=1`, `Multiplier=1.1`, `Beat` 1 lần, **không** `TierChanged` |
| 3 | `Push(3)` từ 0 | `Count=3`, `Beat.Steps=3`, `TierChanged(-1→0)` phát **sau** `Beat` |
| 4 | `Push(0)` | không event, `Count` không đổi, cửa sổ **không** reset |
| 5 | `Push()`, `Tick(0.5)` | `WindowProgress ≈ 0.5` |
| 6 | `Push()`, `Tick(1.1)` | `Broken(Count=1, WasManualBreak=false)`, sau đó `Count==0` |
| 7 | `Push()`, `Break()` | `Broken(WasManualBreak=true)` |
| 8 | `Break()` khi `Count==0` | **không** event |
| 9 | `Push()`, `Break()`, `Break()` | `Broken` phát **1** lần |
| 10 | `Push()`×3, `Reset()` | `Count==0`, **không** event nào |
| 11 | trong handler `Broken`, đọc `tracker.Count` | thấy **`0`** |
| 12 | `Push()`, `SetSuspended(true)`, `Tick(60)` | combo **còn**, `WindowProgress` không đổi |
| 13 | policy `Manual`, `Push()`, `Tick(3600)` | combo **còn**, `WindowProgress == 1` |
| 14 | policy trả `NaN` | 1 log error, combo không đứt |
| 15 | `new ComboTracker(null)` rồi `Push()` | `Count=1`, không đứt, không NRE |
| 16 | `tierMinCounts = null`, `Push()`×99 | `TierIndex == -1`, không NRE |
| 17 | `Push()`×20 | `Multiplier == 3.0`; cửa sổ `== 0.4` (đã chạm sàn) |
| 18 | Profiler: 20 `Push` + 60 `Tick`/giây | **0 B** GC Alloc |

- [ ] **Step 3: Commit** — `feat(sdk): add ComboTracker (pure C# combo rules)`

---

### Task 4: `ComboSystem` — tầng Unity

**Files:** **Ghi đè** `Implementations/Composites/Combo/ComboSystem.cs` *(stub có namespace lặp `Horcrux.Runtime.Horcrux.Runtime.Implementations.Composites.Combo` — sai)*

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| `MonoBehaviour<ITicker>` (InitArgs), không `TryGet` | `ITicker` là **bắt buộc** ⇒ thiếu nó phải throw sớm để lộ lỗi cấu hình. `MonoBehaviour<T>` cho đúng ngữ nghĩa đó tại tầng type |
| **Enum + tham số** trong Inspector | §C.1: mọi thứ quyết được lúc authoring thì để ở Inspector. `InterfaceReference<T>` chưa tồn tại nên serialize policy trực tiếp là không khả thi |
| `tierMinCounts` là `int[]` trên Inspector | Xem §0.4 — không cần `ComboTier`/interface/SO nào |
| `OnValidate` cảnh báo mốc bậc không tăng dần | Bất biến này là **điều kiện đúng đắn** của quét ngược (§0.4); mảng `{6,3,10}` cho bậc sai mà không có lỗi nào |
| **Không** tự sắp xếp mảng | Tự sắp làm thứ tự nhảy dưới tay người đang gõ — rất khó chịu và dễ mất dữ liệu vừa nhập |
| Đăng ký ticker ở `OnEnable`, huỷ ở `OnDisable` | Vòng đời đúng của Unity; không huỷ ⇒ ticker giữ reference chết |
| `IPauseAware` → `SetSuspended` | Cửa sổ đo bằng `unscaled` nên nếu không đóng băng, app pause 60s **giết combo** trong lúc người chơi không hề chơi |
| **Không** dùng `offlineSeconds` | Đóng băng nghĩa là *không tính* thời gian đó. Cộng nó vào chính là thứ ta đang tránh |
| Uỷ quyền add/remove event cho tracker | Handler chuyển tiếp là một delegate được alloc + một lớp phải debug qua |

**Editor setup (§C.1):**

1. Tạo GameObject `[Combo]` ở scene bootstrap → add `ComboSystem`.
2. `Window Mode = Shrinking` · `Window Seconds = 1.2` · `Min Window Seconds = 0.4` · `Steps To Min Window = 10`.
3. `Multiplier Per Step = 0.1` · `Max Multiplier = 5`.
4. `Tier Min Counts` = `[3, 6, 10]` (**tăng dần** — sai thứ tự sẽ có warning).
5. Kiểm: vào Play Mode không throw. Nếu throw "no service ITicker" ⇒ chưa đặt `[Ticker]` vào scene (`TickerSystem.md` Task 3).

- [ ] **Step 1: Ghi đè `ComboSystem.cs`**

```csharp
using System;
using Horcrux.Runtime.Abstractions.Combo;
using Horcrux.Runtime.Abstractions.Ticker;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Combo
{
    /// <summary>Cách khai cửa sổ combo trong Inspector — phủ 3 cơ chế đứt của §0.2.</summary>
    public enum EComboWindowMode
    {
        /// <summary>Cửa sổ không đổi. Dùng ~0.12s cho combo kiểu cascade một đợt resolve.</summary>
        Fixed,

        /// <summary>Co dần theo số nhịp — mặc định khi combo là trục thử thách.</summary>
        Shrinking,

        /// <summary>Không tự đứt — chỉ đứt khi game gọi <c>Break()</c> (combo theo move/lượt).</summary>
        Manual
    }

    /// <summary>
    /// Tầng Unity của combo: bơm nhịp từ <see cref="ITicker"/> vào <see cref="ComboTracker"/>,
    /// đóng băng khi app pause, lộ <see cref="IComboSystem"/>.
    /// </summary>
    /// <remarks>
    /// Class này KHÔNG chứa luật combo — toàn bộ luật ở <see cref="ComboTracker"/>. Ở đây chỉ có ba
    /// việc: dựng tracker từ cấu hình Inspector, chuyển tick, xử lý pause.
    ///
    /// Vì sao <c>MonoBehaviour&lt;ITicker&gt;</c> chứ không <c>TryGet</c>: ticker là dependency BẮT
    /// BUỘC, thiếu nó là lỗi cấu hình ⇒ phải throw sớm để lộ ra, không degrade im lặng.
    /// </remarks>
    [Service(typeof(IComboSystem), FindFromScene = true)]
    public sealed class ComboSystem : MonoBehaviour<ITicker>, IComboSystem, ITickable, IPauseAware
    {
        [Header("Cửa sổ combo (số cảm giác — §0.5)")]
        [SerializeField] private EComboWindowMode windowMode = EComboWindowMode.Shrinking;

        [Tooltip("Fixed: cửa sổ cố định (~0.12s cho cascade). Shrinking: cửa sổ ở nhịp 0.")]
        [SerializeField] private float windowSeconds = 1.2f;

        [Tooltip("Shrinking: cửa sổ sau khi đã đạt 'Steps To Min Window'.")]
        [SerializeField] private float minWindowSeconds = 0.4f;

        [Tooltip("Shrinking: số nhịp để cửa sổ chạm sàn. 0 = dùng sàn ngay.")]
        [SerializeField] private int stepsToMinWindow = 10;

        [Header("Hệ số nhân")]
        [Tooltip("Cộng thêm mỗi nhịp (0.1 = +10%).")]
        [SerializeField] private float multiplierPerStep = 0.1f;

        [Tooltip("Trần — tham số THIẾT KẾ KINH TẾ, không phải guard an toàn (§0.3).")]
        [SerializeField] private float maxMultiplier = 5f;

        [Header("Bậc combo")]
        [Tooltip("Mốc số nhịp của từng bậc, PHẢI tăng dần. Rỗng ⇒ TierIndex luôn -1.")]
        [SerializeField] private int[] tierMinCounts = { 3, 6, 10 };

        private ITicker _ticker;
        private ComboTracker _tracker;

        protected override void Init(ITicker ticker) => _ticker = ticker;

        #region Unity Callbacks
        private void Awake()
            => _tracker = new ComboTracker(CreateWindowPolicy(), tierMinCounts,
                                           multiplierPerStep, maxMultiplier);

        private void OnEnable()
        {
            _ticker.AddTickListener(this);
            _ticker.AddPauseListener(this);
        }

        private void OnDisable()
        {
            _ticker?.RemoveTickListener(this);
            _ticker?.RemovePauseListener(this);
        }
        #endregion

        private IComboWindowPolicy CreateWindowPolicy() => windowMode switch
        {
            EComboWindowMode.Fixed => new FixedComboWindow(windowSeconds),
            EComboWindowMode.Shrinking => new ShrinkingComboWindow(windowSeconds, minWindowSeconds, stepsToMinWindow),
            _ => ManualComboWindow.Instance
        };

        #region IComboSystem
        public int Count => _tracker.Count;
        public int TierIndex => _tracker.TierIndex;
        public float Multiplier => _tracker.Multiplier;
        public float WindowProgress => _tracker.WindowProgress;
        public bool IsActive => _tracker.IsActive;

        public void Push(int steps = 1) => _tracker.Push(steps);
        public void Break() => _tracker.Break();
        public void Reset() => _tracker.Reset();

        // Uỷ quyền add/remove thẳng cho event của tracker: KHÔNG dựng handler chuyển tiếp (mỗi handler
        // như vậy là một delegate được alloc, và thêm một lớp phải debug qua).
        public event Action<ComboBeat> Beat
        {
            add => _tracker.Beat += value;
            remove => _tracker.Beat -= value;
        }

        public event Action<ComboTierChange> TierChanged
        {
            add => _tracker.TierChanged += value;
            remove => _tracker.TierChanged -= value;
        }

        public event Action<ComboSummary> Broken
        {
            add => _tracker.Broken += value;
            remove => _tracker.Broken -= value;
        }
        #endregion

        #region Ticker callbacks
        public void OnTick(float unscaledDeltaTime) => _tracker.Tick(unscaledDeltaTime);

        /// <remarks>
        /// KHÔNG dùng <paramref name="offlineSeconds"/>: đóng băng nghĩa là *không tính* thời gian đó.
        /// Cộng nó vào chính là thứ ta đang tránh — combo sẽ chết vì người chơi mở app khác 1 phút.
        /// </remarks>
        public void OnPauseChanged(bool isPaused, float offlineSeconds) => _tracker.SetSuspended(isPaused);
        #endregion

#if UNITY_EDITOR
        /// <summary>Mốc bậc không tăng dần cho ra bậc SAI mà không có lỗi nào (§0.4) ⇒ báo lúc authoring.</summary>
        /// <remarks>KHÔNG tự sắp xếp: thứ tự nhảy dưới tay người đang gõ là rất khó chịu.</remarks>
        private void OnValidate()
        {
            for (int i = 1; i < tierMinCounts.Length; i++)
            {
                if (tierMinCounts[i] > tierMinCounts[i - 1]) continue;

                Debug.LogWarning($"[Combo] {name}: Tier Min Counts phải TĂNG dần. Phần tử #{i} " +
                                 $"({tierMinCounts[i]}) không lớn hơn #{i - 1} ({tierMinCounts[i - 1]}) " +
                                 $"— bậc sẽ phân giải sai.", this);
            }
        }
#endif
    }
}
```

- [ ] **Step 2: Kiểm chứng**

| # | Input | Kỳ vọng |
|---|---|---|
| 1 | Scene không có `TickerService` | throw ngay lúc init (lỗi cấu hình **lộ ra**) |
| 2 | `Manual`, `Push`, chờ 10s | combo **còn** |
| 3 | `Shrinking(1.2/0.4/10)`, `Push`, chờ 1.5s | `Broken` phát |
| 4 | `tierMinCounts` rỗng | `TierIndex == -1`, không NRE |
| 5 | `tierMinCounts = {6, 3, 10}` | warning **ngay lúc chỉnh Inspector** |
| 6 | Disable rồi enable component | tick vẫn đúng, không đăng ký trùng (dup-guard của `DeferredList`) |
| 7 | App background 60s giữa combo | combo **còn**, `WindowProgress` như trước khi pause |
| 8 | `timeScale = 0` rồi chờ 1s | cửa sổ **vẫn** cạn 1s |

- [ ] **Step 3: Commit** — `feat(sdk): add ComboSystem behaviour (tick + pause-aware)`

---

### Task 5: `ComboFeedbackBridge` + `ComboMeter`

**Files:** Create `ComboFeedbackBridge.cs` · `ComboMeter.cs`

**Quyết định thiết kế — Bridge:**

| Quyết định | Lý do |
|---|---|
| **File riêng**, không nhồi vào `ComboSystem` | Đây là toàn bộ điểm của kiến trúc: xoá file này thì combo **vẫn** chạy, chỉ mất phản hồi. Nhồi vào lõi là kéo `Abstractions.Feedback` thành dependency bắt buộc |
| `TryGet` ở `OnEnable`, cache | Optional service phải có nhánh không-có; và `TryGet` mỗi nhịp là tra service-locator 20 lần/giây |
| Không có dispatcher ⇒ **warning một lần**, không error | Chạy không có hệ Feedback là **hợp lệ**; nhưng lặng hoàn toàn thì người tích hợp mất giờ đi tìm |
| Cả 2 cue mang `Step = Count` | Ramp phải nằm ở **kênh** (nơi người thiết kế tune), không ở code bridge. Nhờ đó bridge không cần biết gì về bậc/tổng số bậc |
| **Không** có `brokenCueId` | Demo để 0; phản hồi lúc đứt là meter tắt. Xoá không hỏng gì (NT 6) |
| Huỷ đăng ký ở `OnDisable` | Combo là service sống xuyên scene; bridge có thể chết trước |

**Quyết định thiết kế — Meter:**

| Quyết định | Lý do |
|---|---|
| ⚠️ **Không** chạm chữ/TMP, và **không** có hook kế thừa | `com.horcrux.runtime.asmdef` không reference TextMeshPro ⇒ `TMP_Text` không biên dịch. Và game bind chữ thẳng vào `IComboSystem.Beat` **đơn giản hơn** kế thừa 3 hook: `_combo.Beat += b => countText.text = $"x{b.Count}";`. Cắt hook ⇒ class `sealed` |
| Bám `WindowProgress` của service | Bất biến "một nguồn sự thật"; ba UI cùng chia thì có một chỗ quên ca `+∞` |
| Dùng `SquashStretch` thay tự viết pop | Pop **giữ thể tích** (nén Y thì phình X) là thứ làm nó "sống"; hàm đã có và đã kiểm |
| `EaseType.OutBack` cho pop | `BackEase.Out` vọt trên 1 rồi lắng — cú nảy **có lực**. `OutQuad` chỉ phình mượt, đọc thành *chậm* |
| Màu bậc là `Color[]` **trên component** | Màu là *trình bày*, thuộc game |
| Ẩn cả object khi `Count == 0` | Meter rỗng hút mắt vô ích; `SetActive(false)` cắt luôn chi phí layout/render của cả nhánh |
| Trả `Vector3.one` **chính xác** khi pop xong | Không có bước này thì scale đọng ở giá trị gần-1 và lệch tích lũy sau nhiều nhịp |

**Editor setup (§C.1):**

**Bridge** — add `ComboFeedbackBridge` lên GameObject `[Combo]`:
- `Beat Cue Id = 101` (khớp `AudioPitchRampChannel` + `HapticRampChannel` + `CameraShakeChannel` — `FeedbackSystem.md` Task 4–5).
- `Tier Up Cue Id = 102` (khớp `HitstopChannel`).

**Meter** — trong Canvas:
1. `ComboMeter` (GameObject) → con: một `Image` với `Image Type = Filled`, `Fill Method = Horizontal`.
2. Add `ComboMeter` lên GameObject cha, kéo `Image` vào field `Fill Image`.
3. `Visual Root` / `Pop Target` để **trống** = dùng chính GameObject này (đủ cho bản đầu).
4. `Tier Colors`: 3 phần tử khớp `Tier Min Counts` ở `ComboSystem` (vd vàng → cam → đỏ). Thiếu phần tử ⇒ giữ màu cũ, không lỗi.
5. Chữ: SDK **không** render chữ. Game viết 1 dòng ở script của nó:
   `IComboSystem.Service.Beat += b => countText.text = $"x{b.Count}";`

- [ ] **Step 1: `ComboFeedbackBridge.cs`**

```csharp
using Horcrux.Runtime.Abstractions.Combo;
using Horcrux.Runtime.Abstractions.Feedback;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Combo
{
    /// <summary>Dịch event combo thành <see cref="FeedbackCue"/> — chỗ DUY NHẤT nối combo với giác quan.</summary>
    /// <remarks>
    /// Xoá file này thì <c>ComboSystem</c> VẪN chạy đúng, chỉ mất phản hồi. Đó là toàn bộ điểm của
    /// việc tách lõi khỏi FX: lõi không phụ thuộc <c>Abstractions.Feedback</c>.
    ///
    /// Ramp nằm ở KÊNH (nơi người thiết kế tune), không ở đây: bridge chỉ chở <c>Step</c> sang và để
    /// mỗi kênh tự quyết bậc đó nghĩa là bao nhiêu semitone / bao nhiêu biên độ rung.
    /// </remarks>
    public sealed class ComboFeedbackBridge : MonoBehaviour<IComboSystem>
    {
        [Tooltip("Cue bắn ở MỖI nhịp combo. Kênh dùng Step để ramp. 0 = không bắn.")]
        [SerializeField] private int beatCueId = 101;

        [Tooltip("Cue bắn khi LÊN bậc. Nên khác cue nhịp để hitstop chỉ khựng ở mốc bậc. 0 = không bắn.")]
        [SerializeField] private int tierUpCueId = 102;

        private IComboSystem _combo;
        private IFeedbackDispatcher _dispatcher;

        protected override void Init(IComboSystem combo) => _combo = combo;

        private void OnEnable()
        {
            // Optional service: không có là HỢP LỆ. Warning một lần để người tích hợp không mất giờ
            // đi tìm "vì sao không có tiếng".
            if (!IFeedbackDispatcher.TryGet(out _dispatcher))
                Debug.LogWarning("[Combo] Không tìm thấy IFeedbackDispatcher — combo chạy không có phản hồi.", this);

            _combo.Beat += OnBeat;
            _combo.TierChanged += OnTierChanged;
        }

        private void OnDisable()
        {
            _combo.Beat -= OnBeat;
            _combo.TierChanged -= OnTierChanged;

            _dispatcher = null;
        }

        private void OnBeat(ComboBeat beat)
        {
            if (_dispatcher == null || beatCueId == 0) return;

            _dispatcher.Raise(new FeedbackCue(beatCueId, beat.Count));
        }

        private void OnTierChanged(ComboTierChange change)
        {
            if (_dispatcher == null || tierUpCueId == 0) return;
            if (!change.IsUpgrade) return;                 // tụt bậc (do reset) thì không ăn mừng

            _dispatcher.Raise(new FeedbackCue(tierUpCueId, change.Count));
        }
    }
}
```

- [ ] **Step 2: `ComboMeter.cs`**

```csharp
using Horcrux.Runtime.Abstractions.Combo;
using Horcrux.Runtime.Abstractions.Ticker;
using Horcrux.Runtime.Implementations.Utilities.Common;
using Horcrux.Runtime.Tweening.Easing;
using Horcrux.Runtime.Utilities.PhysXHelper;
using Sisus.Init;
using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Implementations.Combo
{
    /// <summary>Thanh combo: co theo cửa sổ còn lại · nảy mỗi nhịp · đổi màu theo bậc.</summary>
    /// <remarks>
    /// Đây là item <c>ComboMeter</c> chuyển từ <c>Pendings.md</c> Nhóm 8-L.
    ///
    /// ⚠️ Class này CỐ TÌNH không chạm chữ (<c>com.horcrux.runtime</c> không reference TextMeshPro, và
    /// nhãn bậc là content của game). Game bind chữ THẲNG vào event, không cần kế thừa gì:
    /// <code>
    /// IComboSystem.Service.Beat += b => countText.text = $"x{b.Count}";
    /// </code>
    /// </remarks>
    public sealed class ComboMeter : MonoBehaviour<IComboSystem, ITicker>, ITickable
    {
        [Header("Tham chiếu")]
        [Tooltip("Image kiểu Filled — fillAmount bám cửa sổ còn lại.")]
        [SerializeField] private Image fillImage;

        [Tooltip("Object bật/tắt theo việc có combo hay không. Trống = dùng chính object này.")]
        [SerializeField] private GameObject visualRoot;

        [Tooltip("Transform nhận cú nảy mỗi nhịp. Trống = dùng chính object này.")]
        [SerializeField] private Transform popTarget;

        [Header("Pop (số cảm giác — §0.5)")]
        [SerializeField] private float popDuration = 0.22f;

        [Tooltip("Độ nén ban đầu. 0.8 = nén 20% rồi bung vọt lên trên 1.")]
        [SerializeField] private float popMinScale = 0.8f;

        [Tooltip("Trục nén chính. Nén Y thì phình X (giữ thể tích) — cái làm nó 'sống'.")]
        [SerializeField] private AxisType popAxis = AxisType.Y;

        [Header("Màu theo bậc")]
        [Tooltip("Màu theo index bậc, khớp 'Tier Min Counts' của ComboSystem. Thiếu ⇒ giữ màu hiện tại.")]
        [SerializeField] private Color[] tierColors = System.Array.Empty<Color>();

        [SerializeField] private Color noTierColor = Color.white;

        private IComboSystem _combo;
        private ITicker _ticker;

        private Transform _popTransform;
        private GameObject _root;

        private float _popElapsed;
        private bool _isPopping;
        private bool _isVisible;

        protected override void Init(IComboSystem combo, ITicker ticker)
        {
            _combo = combo;
            _ticker = ticker;
        }

        #region Unity Callbacks
        private void Awake()
        {
            // Cache: .transform là property gọi xuống engine.
            _popTransform = popTarget != null ? popTarget : transform;
            _root = visualRoot != null ? visualRoot : gameObject;
        }

        private void OnEnable()
        {
            _combo.Beat += OnBeat;
            _combo.TierChanged += OnTierChanged;
            _combo.Broken += OnBroken;

            _ticker.AddTickListener(this);

            SetVisible(_combo.IsActive);     // có thể enable giữa lúc combo đang chạy
        }

        private void OnDisable()
        {
            _combo.Beat -= OnBeat;
            _combo.TierChanged -= OnTierChanged;
            _combo.Broken -= OnBroken;

            _ticker?.RemoveTickListener(this);

            ResetPop();
        }
        #endregion

        #region Combo callbacks
        private void OnBeat(ComboBeat beat)
        {
            SetVisible(true);

            _popElapsed = 0f;
            _isPopping = true;
        }

        private void OnTierChanged(ComboTierChange change) => ApplyTierColor(change.TierIndex);

        private void OnBroken(ComboSummary summary)
        {
            SetVisible(false);
            ResetPop();
            ApplyTierColor(ComboTracker.NoTier);
        }
        #endregion

        public void OnTick(float unscaledDeltaTime)
        {
            // Trạng thái phổ biến nhất là "không có combo, không đang nảy" → thoát rẻ nhất.
            if (!_isVisible && !_isPopping) return;

            if (_isVisible && fillImage != null)
            {
                // Bám WindowProgress của service, KHÔNG tự chia: một nguồn sự thật, và service đã lo
                // ca cửa sổ +∞ (trả 1) mà mọi UI tự tính đều dễ quên.
                fillImage.fillAmount = _combo.WindowProgress;
            }

            if (!_isPopping) return;

            _popElapsed += unscaledDeltaTime;
            float t = popDuration > 0f ? _popElapsed / popDuration : 1f;

            if (t >= 1f)
            {
                ResetPop();
                return;
            }

            // OutBack vọt trên 1 rồi lắng ⇒ cú nảy CÓ LỰC. GetSquashStretch giữ thể tích: nén Y thì
            // phình X — thứ làm hiệu ứng "sống".
            _popTransform.localScale = SquashStretch.GetSquashStretch(
                t, EaseType.OutBack, popMinScale, popAxis, CoordinateSystem.XY);
        }

        #region Internals
        private void SetVisible(bool isVisible)
        {
            if (_isVisible == isVisible) return;      // tránh SetActive lặp (mỗi lần là một lần dirty layout)

            _isVisible = isVisible;
            _root.SetActive(isVisible);
        }

        private void ResetPop()
        {
            _isPopping = false;
            _popElapsed = 0f;

            // Vector3.one CHÍNH XÁC: không có bước này thì scale đọng ở giá trị gần-1 và lệch tích lũy.
            _popTransform.localScale = Vector3.one;
        }

        private void ApplyTierColor(int tierIndex)
        {
            if (fillImage == null) return;

            if (tierIndex < 0)
            {
                fillImage.color = noTierColor;
                return;
            }

            // Thiếu phần tử ⇒ giữ màu hiện tại: im lặng hợp lý hơn nhảy về màu mặc định giữa combo.
            if (tierIndex < tierColors.Length) fillImage.color = tierColors[tierIndex];
        }
        #endregion
    }
}
```

- [ ] **Step 3: Kiểm chứng**

| # | Input | Kỳ vọng |
|---|---|---|
| 1 | Không có `FeedbackDispatcher` | 1 warning, combo + meter **vẫn** chạy đúng |
| 2 | `Push()` với `beatCueId = 101` | `Raise(Id=101, Step=1)` |
| 3 | `Push(3)` lên bậc 0 | 2 lần `Raise`: nhịp (`101`) **trước**, lên-bậc (`102`) **sau** |
| 4 | `Reset()` (tụt bậc) | **không** cue lên-bậc |
| 5 | `beatCueId = 0` | không cue nhịp; cue lên-bậc vẫn chạy |
| 6 | Destroy bridge rồi `Push()` | không NRE |
| 7 | `Count == 0` lúc khởi động | `visualRoot` **tắt** |
| 8 | `Push()` | root bật, `fillAmount == 1`, pop bắt đầu |
| 9 | Nửa cửa sổ đã trôi | `fillAmount ≈ 0.5` |
| 10 | Policy `Manual` | `fillAmount == 1` liên tục |
| 11 | Pop xong | `localScale == Vector3.one` **chính xác** |
| 12 | `Push()` liên tiếp 5 lần nhanh | pop khởi động lại mỗi lần, không cộng dồn scale |
| 13 | Lên bậc 1, `tierColors` có 1 phần tử | giữ màu hiện tại, **không** throw |
| 14 | Không gán `fillImage` | không NRE, pop **vẫn** chạy |
| 15 | Profiler: không có combo | `OnTick` thoát sớm, **0 B** alloc |

- [ ] **Step 4: Cập nhật `Pendings.md`** — Nhóm 8-L: xoá `ComboMeter`, thay bằng dòng trỏ plan này.

- [ ] **Step 5: Commit** — `feat(sdk): add combo feedback bridge + meter`

---

### Task 6: `ComboDemoDriver` — caller đầu tiên

**Files:** Create `Implementations/Composites/Combo/Demo/ComboDemoDriver.cs`

**Quyết định thiết kế:**

| Quyết định | Lý do |
|---|---|
| Tồn tại **vì** chưa có game caller | `DOCS_SKILL` §C.2: mọi hàm phải có caller thật. Không có Task này thì Task 1–5 là code chưa ai gọi |
| `Button` + `[ContextMenu]`, **không** `Input.GetKeyDown` | Project có `InputSystem_Actions` ⇒ legacy `Input` có thể bị tắt. `Button` và `ContextMenu` **luôn** hoạt động |
| `[ContextMenu]` để test **không cần dựng UI** | Kiểm ngay trong Play Mode bằng chuột phải vào component |
| Log ở `Broken` | Đây là code demo nên log là **mục đích**, và bảng nghiệm thu đọc nó |
| `Demo/` là thư mục riêng | Xoá cả thư mục là xoá sạch phần demo khi game đã có caller thật |

**Editor setup (§C.1):**

1. Trong Canvas, tạo 4 `Button`: `Push 1` · `Push cascade` · `Break` · `Reset`.
2. Add `ComboDemoDriver` lên một GameObject trong Canvas, kéo 4 `Button` vào 4 field tương ứng.
3. **Không** cần nối `onClick` trong Inspector — driver tự `AddListener` ở `OnEnable`.
4. Nếu chưa muốn dựng UI: bỏ trống 4 field, dùng **chuột phải vào component → `Push 1` / `Break` / …** trong lúc Play.

- [ ] **Step 1: `ComboDemoDriver.cs`**

```csharp
using Horcrux.Runtime.Abstractions.Combo;
using Sisus.Init;
using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Implementations.Combo
{
    /// <summary>Bộ kích tay để kiểm chuỗi combo → cue → 3 giác quan → meter, khi chưa có gameplay.</summary>
    /// <remarks>
    /// Class này tồn tại VÌ hệ combo chưa có caller thật. Không có nó, mọi thứ trong plan là code chưa
    /// ai gọi — đúng thứ §C.2 của DOCS_SKILL cấm.
    ///
    /// Không dùng <c>Input.GetKeyDown</c>: project có <c>InputSystem_Actions</c> nên legacy Input có
    /// thể đã bị tắt. <c>Button</c> và <c>[ContextMenu]</c> luôn hoạt động ở mọi cấu hình input.
    ///
    /// Xoá cả thư mục <c>Demo/</c> khi game đã có caller thật.
    /// </remarks>
    public sealed class ComboDemoDriver : MonoBehaviour<IComboSystem>
    {
        [SerializeField] private Button pushButton;
        [SerializeField] private Button pushCascadeButton;
        [SerializeField] private Button breakButton;
        [SerializeField] private Button resetButton;

        [Tooltip("Số nhịp mà nút 'cascade' cộng một lần — mô phỏng một đợt resolve.")]
        [SerializeField] private int cascadeSteps = 4;

        private IComboSystem _combo;

        protected override void Init(IComboSystem combo) => _combo = combo;

        private void OnEnable()
        {
            if (pushButton != null) pushButton.onClick.AddListener(PushOne);
            if (pushCascadeButton != null) pushCascadeButton.onClick.AddListener(PushCascade);
            if (breakButton != null) breakButton.onClick.AddListener(BreakCombo);
            if (resetButton != null) resetButton.onClick.AddListener(ResetCombo);

            _combo.Broken += OnBroken;
        }

        private void OnDisable()
        {
            if (pushButton != null) pushButton.onClick.RemoveListener(PushOne);
            if (pushCascadeButton != null) pushCascadeButton.onClick.RemoveListener(PushCascade);
            if (breakButton != null) breakButton.onClick.RemoveListener(BreakCombo);
            if (resetButton != null) resetButton.onClick.RemoveListener(ResetCombo);

            _combo.Broken -= OnBroken;
        }

        [ContextMenu("Push 1")]
        public void PushOne() => _combo.Push();

        [ContextMenu("Push cascade")]
        public void PushCascade() => _combo.Push(cascadeSteps);

        [ContextMenu("Break")]
        public void BreakCombo() => _combo.Break();

        [ContextMenu("Reset (silent)")]
        public void ResetCombo() => _combo.Reset();

        // Log là MỤC ĐÍCH ở đây: nghiệm thu cần thấy Count/Duration/nguyên nhân đứt thật.
        private void OnBroken(ComboSummary summary)
            => Debug.Log($"[ComboDemo] đứt — count={summary.Count} tier={summary.TierIndex} " +
                         $"duration={summary.DurationSeconds:F2}s manual={summary.WasManualBreak}");
    }
}
```

- [ ] **Step 2: Dựng scene nghiệm thu**

Bức tranh tổng — chi tiết từng bước ở mục **Editor setup** của từng task:

```
[Ticker]      TickerService                                   TickerSystem.md   Task 3
[Haptic]      HapticService                                   HapticSystem.md   Task 2
[Audio]       AudioService   + AudioCatalog + 12 voice        AudioSystem.md    Task 2, 3
[Feedback]    FeedbackDispatcher + 4 kênh (entry ở Inspector) FeedbackSystem.md Task 3, 4, 5
CameraFollow  └─ FeedbackCameraRig └─ Camera                  FeedbackSystem.md Task 5
[Combo]       ComboSystem + ComboFeedbackBridge               ComboSystem.md    Task 4, 5
Canvas        ComboMeter + 4 Button + ComboDemoDriver         ComboSystem.md    Task 5, 6
```

**Sợi dây nối cả 5 hệ là `CueId`.** Cue nhịp (`101`) phải xuất hiện ở **3** kênh; cue lên-bậc (`102`) chỉ ở hitstop:

| CueId | `AudioPitchRampChannel` | `HapticRampChannel` | `CameraShakeChannel` | `HitstopChannel` | Bridge |
|---|:--:|:--:|:--:|:--:|---|
| `101` nhịp | ✓ | ✓ | ✓ | — | `Beat Cue Id` |
| `102` lên bậc | — | — | — | ✓ (`MinStep`) | `Tier Up Cue Id` |

Lệch một số ở bất kỳ ô nào = mất đúng một giác quan, **không** có lỗi nào báo. Đây là chỗ dễ sai nhất khi setup — và là lý do 4 kênh cùng nằm trên **một** GameObject `[Feedback]`: 4 `CueId` hiện cùng lúc trong một Inspector.

Bảng nghiệm thu — **định nghĩa "xong"** của cả 5 plan:

| # | Thao tác | Phải thấy/nghe/cảm |
|---|---|---|
| 1 | "Push 1" một lần | meter hiện, nảy, `fillAmount` bắt đầu cạn; nghe 1 tiếng |
| 2 | Bấm liên tục 10 lần | cao độ **leo dần** rồi bão hoà; meter nảy mỗi lần; màu đổi ở mốc bậc |
| 3 | Ngừng bấm | `fillAmount` cạn về 0 rồi meter tắt; Console log `đứt … manual=False` |
| 4 | "Push cascade" | `Beat.Steps == 4`, một cú nảy, cao độ nhảy 4 bậc |
| 5 | "Break" giữa chuỗi | meter tắt ngay; log `manual=True` |
| 6 | "Reset" giữa chuỗi | meter tắt; **không** log |
| 7 | Đạt bậc có `MinStep` của hitstop | game **khựng** rất ngắn rồi bung ra; camera rung nhẹ |
| 8 | Trên Android thật | **cảm** được rung mạnh dần theo bậc |
| 9 | App background 60s giữa chuỗi | quay lại combo **vẫn còn**, cửa sổ như trước |
| 10 | Xoá GameObject `[Feedback]` rồi chạy lại | 1 warning; combo + meter **vẫn** chạy đúng |
| 11 | Xoá `[Audio]` (giữ Feedback) | mất tiếng; rung + hitstop + shake + meter **vẫn** chạy |
| 12 | Profiler suốt phiên | 0 B GC Alloc/frame khi idle; không spike lúc dồn nhịp |

- [ ] **Step 3: Cập nhật `Pendings.md`** — Nhóm 7-F: xoá `ChainReaction` → trỏ "Giai đoạn 2" của file này. Xoá 2 hộp *"Combo ASMR đã nhất"* / *"Combo đa giác quan hoàn hảo"* → trỏ § *Nguyên liệu đã chuyển*. Cập nhật Roadmap mục 39/40/41.

- [ ] **Step 4: Cập nhật `PendingSystems.md`** — thêm dòng §23 vào bảng tổng quan trỏ plan này.

- [ ] **Step 5: Commit** — `feat(sdk): add combo demo driver + acceptance scene`

---

## Giai đoạn 2 — `ChainReaction` (chuyển từ `Pendings.md` 7-F, **chưa** vào phạm vi)

**Bài toán.** Cascade trong puzzle không nổ **cùng lúc** — nó lan từ điểm kích ra ngoài. Nếu mọi ô clear trong cùng một frame thì mất hoàn toàn cảm giác domino, và pitch ramp không có chỗ leo (20 nhịp trong 1 frame = throttle bỏ 19).

**Mô hình.**

| Thành phần | Vai trò |
|---|---|
| `IStaggerPolicy` | `float GetDelay(float distanceFromOrigin)` — `delay = dist × factor`, có trần |
| `ChainReactionSequencer` | Nhận danh sách (vị trí, dữ liệu) + gốc lan → xếp theo delay → mỗi mốc gọi `IComboSystem.Push(1)` |
| `ITicker` | Nhịp để xả hàng đợi theo mốc thời gian |

**Vì sao chưa làm** (3 lý do, mỗi cái đủ để hoãn):
1. **Thiếu `StaggerHelper`/`GeometryHelper` phần khoảng cách** — `GeometryHelper` hiện chỉ có `RandomPointInAnnulus`.
2. **Không có caller thật** — chưa có board nào sinh ra danh sách ô để lan.
3. **Chữ ký không bị đóng cứng** — sequencer chỉ **gọi** `Push`, đã tồn tại ⇒ thêm sau là **thêm file**, không sửa file cũ.

**Điều kiện bắt đầu:** có `StaggerHelper` + một board thật gọi được. Lúc đó viết plan riêng `ChainReaction.md` cạnh file này.

---

## Ghi chú thực thi

- **Điều kiện tiên quyết:**

| Plan | Cần tới đâu |
|---|---|
| `TickerSystem.md` | **toàn bộ** — Task 4 không compile nếu thiếu |
| `FeedbackSystem.md` | Task 2–3 (cue + dispatcher) cho Task 5; Task 4–5 để nghiệm thu 3 giác quan |
| `AudioSystem.md`, `HapticSystem.md` | cần cho Task 6 nghiệm thu; **không** cần để combo compile |

- **Ranh giới cuối cùng:** SDK trả `Multiplier` — **game** nhân vào điểm. SDK trả `TierIndex` — **game** map sang chữ. SDK phát `Broken(summary)` — **game** lưu kỷ lục và bắn telemetry.
- **Mở rộng sau:**

| Mục | Cách thêm | Breaking? |
|---|---|---|
| Nhãn / hệ số nhân / cue riêng theo bậc | Thay `int[] tierMinCounts` bằng một SO + `IComboTierTable`; chỉ đổi **ctor của tracker** (nội bộ), `IComboSystem.TierIndex` không đổi | Không |
| Đường nhân điểm khác (bậc thang, hàm mũ) | Tách lại `IComboMultiplierCurve`, đổi ctor tracker | Không |
| Multi-track (`GetTrack(id)`) | `Dictionary<id, ComboTracker>` ở tầng ② | Không |
| Lưu best-combo · telemetry | Game subscribe `Broken` — `ComboSummary` đã đủ dữ liệu | Không |
| Cắm policy tự viết (`SetStrategies`) | Thêm 1 method vào `ComboSystem` | Không |
| Cue lúc combo đứt | Thêm 1 field + 1 handler vào bridge | Không |
| `ComboMeter` dùng `CountUpAnimator`/`Overshoot`/`ColorFlash` | 3 helper chưa hiện thực; thay thân `OnTick` | Không |
| `ChainReaction` | 3 lý do ở "Giai đoạn 2" | Không |
