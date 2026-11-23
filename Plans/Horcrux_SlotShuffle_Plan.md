# Plan — `SlotShuffle`: xếp lại n vật vào chính n chỗ của chúng, dồn theo nhóm, không vật nào đứng yên khi tránh được

Ngày 2026-11-20. Nguồn: category-jam `Assets/_CategoryJam/Scripts/Utilities/SlotShuffle.cs` (bản tạm, code GĐ1–3 nguyên văn trừ hàm
xáo còn private), caller `Assets/_CategoryJam/Scripts/Director/ShuffleBooster.cs` (`ScrambleAwayFromBoxes`, `RestoreScrambledBoard`),
đang chạy trong dự án. Item `i` đứng ở slot `i`; hàm trả hoán vị đưa tối đa item về nhóm slot chúng ưa, rồi gỡ item đứng yên bằng
những phép đổi giữ nguyên số đó; áp hoán vị lên chỗ chứa chỉ tráo được bằng tráo dọc từng vòng.

**Phân công:** GĐ1–3 developer gõ, agent viết test theo bảng case rồi compile · GĐ4 (tài liệu) · GĐ5 (dự án chuyển sang bản Horcrux,
xoá bản tạm, xoá Plan) agent làm.
**Chạy được, thấy được:** GĐ1–3 compile sạch + bảng case xanh trong `Tests/EditMode/FisherYatesTests.cs`,
`Tests/EditMode/SlotShuffleTests.cs`; thấy trong game ở GĐ5.
**Lệnh kiểm** (chạy từ gốc dự án): `bash "Assets/Horcrux/Editor/CompileCheck/Tools~/verify.sh" --csproj
com.horcrux.runtime.csproj,CategoryJam.Gameplay.csproj --tests Horcrux.EditModeTests,CategoryJam.EditModeTests`.
**Commit:** GĐ1–4 trong `Assets/Horcrux`; GĐ5 ở dự án kèm con trỏ submodule.
**Phụ thuộc:** không. **Chung file:** `Runtime/Utilities/Permutation/FisherYates.cs` — `Horcrux_AudioVariants_Plan.md` (`ShuffleBag`)
gọi `FisherYates.Shuffle`, gõ sau GĐ1 Plan này.
**Đã kiểm trước khi ghi:** csc (Roslyn của Rider, `-langversion:9`, `netstandard.dll` 2.1 của Unity 6000.0.73f1) compile sạch
`FisherYates.cs` và `SlotShuffle.cs` bản cuối GĐ3. Bản mẫu C# chạy trên mono của Unity, nguồn `System.Random(seed).Next`: vét cạn `n`
0–6 × mọi tổ hợp dấu × ưa thích × 20 seed = 109 220 lượt, ① ② ③ của `§0` đúng ở mọi lượt, kết quả trùng từng phần tử với bản tạm (hàm
xáo private) ở 109 220/109 220 lượt; đối chứng bỏ bước gỡ thì mốc `[1, 2, 0]` sai ở 150/200 seed. `FisherYates` `n` 3 × 6 000 seed: sáu
hoán vị 998–1 003 lần; đối chứng gọi `nextIndex(n)` thay `nextIndex(i + 1)`: 665–1 339. `ApplyBySwaps`: bản Python vét cạn mọi hoán vị
`n ≤ 7` (5 914 hoán vị), áp đúng và round-trip 0 sai. `§0`: có — ghép theo chỉ số và bước gỡ cần chứng minh tối đa, đứng yên bắt buộc.

## Ngữ cảnh đã chốt

- **Người dùng:** code gameplay cần **xáo lại** một tập vật đang đứng ở các chỗ cố định (vật trên kệ, lá bài trên bàn, quân ở điểm
  spawn) sao cho mỗi vật đổi chỗ, và một loại vật dồn về một loại chỗ; có khi phải trả về đúng bố cục cũ sau đó. Caller giữ dữ liệu
  của item, hàm chỉ trả chỉ số · code cần xáo một dãy bằng nguồn ngẫu nhiên riêng (`FisherYates.Shuffle`).
- **Mục tiêu:** hàm thuần trên `Span<int>`, `ReadOnlySpan<bool>` và `Func<int, int>`, test không dựng gì. `Assign` có ba bảo đảm theo
  thứ tự ưu tiên: ① kết quả là hoán vị · ② số item vào đúng nhóm slot ưa thích là **tối đa** · ③ item chỉ đứng yên ở slot cũ khi
  **mọi** cách xếp đạt ② đều giữ nó ở đó. Cùng input, cùng nguồn ngẫu nhiên thì cùng kết quả. `Invert`, `ApplyBySwaps` đưa kết quả lên
  chỗ chứa chỉ tráo được và trả về bố cục gốc.
- **Ranh giới:** nguồn ngẫu nhiên do caller đưa — `nextIndex(n)` trả số trong `[0, n)`; caller cần kết quả cố định truyền
  `new System.Random(seed).Next`. Hàm không chạm `UnityEngine.Random`, không làm lệch chuỗi ngẫu nhiên của hệ khác.
- **Chỗ đặt, tên:** `Runtime/Utilities/Permutation/`, namespace `Horcrux.Runtime.Utilities.Permutation`. Không đặt `Random` vào tên
  namespace: code trong namespace anh em gõ `Random.` sẽ trỏ vào nó. `SlotShuffle`, `Assign`, `Invert`, `ApplyBySwaps`, `FisherYates`,
  "dấu" (`isSlotMarked`, `prefersMarked`) — tên chốt khi gõ.
- **Cố ý không làm:** nhiều hơn hai nhóm (người dùng đầu cần đúng hai) · ràng buộc từng cặp item–slot `Func<int, int, bool>` (bài toán
  ghép cặp tổng quát, đắt hơn hẳn nhu cầu) · phân phối đều tuyệt đối trên mọi cách xếp hợp lệ (gameplay cần "trông ngẫu nhiên"; bước gỡ
  đứng yên lệch nhẹ về phía slot đứng sau điểm bắt đầu quét).
- **Quyết định trái trực giác:** ưu thích thắng đổi chỗ. Khi chỉ có một cách đạt ② mà item phải đứng yên, item đứng yên. Đảo ưu tiên
  thì caller "dồn loại A về vùng B" nhận về bàn có loại A ở sai vùng chỉ để mọi item nhúc nhích.
- **Mở rộng sau:** overload `Assign` nhận `ReadOnlySpan<int>` nhóm khi có caller cần hơn hai nhóm; bản hai nhóm giữ.

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| bản tạm category-jam `Scripts/Utilities/SlotShuffle.cs` | toàn bộ GĐ1–3 | hàm xáo private trong `SlotShuffle`: `ShuffleBag` cũng xáo, nên tách `FisherYates` (GĐ1) |
| Horcrux `Runtime/Utilities/` | chỗ đặt, style hàm thuần (`CountdownFormatter`) | chưa có hàm xáo hay hoán vị nào |
| `Horcrux_ProgressDifficulty_Plan.md` › `RandomPick` (`Utilities/Picking/`) | khuôn `Func<int, int> nextIndex`; namespace không chứa `Random` | chọn một phần tử, không xáo |
| `FeelingPlan.md` › `RandomHelper` (lộ trình) | "shuffle in-place, zero-GC" là cùng nhu cầu | chưa có Plan, chưa API; `FisherYates.Shuffle` là bản đó |
| `Horcrux_AudioVariants_Plan.md` › `ShuffleBag` | người gọi thứ hai của `FisherYates.Shuffle` | — |
| category-jam `Utilities/Extensions/ListExtensions.cs` `KShuffle` | Fisher–Yates | bản có seed gọi `UnityEngine.Random.InitState`, đè trạng thái ngẫu nhiên toàn cục; 3 chỗ gọi trong `LevelLoader` chủ ý seed lại luồng đó theo level, đổi nguồn là đổi bố cục mọi level — dự án giữ |
| category-jam `Director/ShuffleBooster.cs` `RotateTileIdsRandomly` | ý "mỗi tile nhận id của tile khác" | hoán vị một vòng bằng đổi cặp kề nhau, không có khái niệm nhóm; đổi sang hàm này là đổi phân phối của booster đang chạy |
| `System.Buffers.ArrayPool<T>` | mảng tạm (`int` ở `Assign`, `bool` ở `ApplyBySwaps`) | — |

## Luồng

```
caller có n item, item i đứng ở slot i; mỗi slot có dấu hay không; mỗi item ưa slot có dấu hay không
                              ▼
      xếp slot: có dấu trước, không dấu sau, FisherYates từng nhóm
      xếp item: ưa có dấu trước, ưa không dấu sau, FisherYates từng nhóm
      ghép theo chỉ số → số item đúng nhóm tối đa; phần tràn chọn ngẫu nhiên                 GĐ1
                              ▼
      slot còn giữ item cũ đổi với một slot cùng dấu, hoặc đang chứa item cùng ưa thích     GĐ2
                              ▼
      itemOfSlot[j] = item slot j nhận → caller ghi dữ liệu item vào slot
                              ▼
      chỗ chứa chỉ tráo được: ApplyBySwaps tráo dọc từng vòng; trả về: Invert rồi ApplyBySwaps        GĐ3
```

## Bản đồ hệ cuối

```
  caller (một lần mỗi lần xáo)
     │ itemOfSlot (n ô ghi ra), isSlotMarked, prefersMarked, nextIndex
     ▼
  SlotShuffle.Assign
     ├─ FillMarkedFirstShuffled ×2 ──► FisherYates.Shuffle (nextIndex) ◄── ShuffleBag (Horcrux_AudioVariants_Plan.md)
     ├─ ghép theo chỉ số
     └─ MoveItemsOffOwnSlot ─────────► nextIndex (một lần mỗi slot còn giữ item cũ)
     ▼
  itemOfSlot ──► caller: slot j nhận dữ liệu của item itemOfSlot[j]
     │
     ├─ SlotShuffle.ApplyBySwaps(itemOfSlot, owner, swap) ──► swap(owner, a, b) của caller
     └─ SlotShuffle.Invert(itemOfSlot, slotOfItem) ──► ApplyBySwaps: chỗ chứa về gốc
```

---

## §0 — Vì sao ghép theo chỉ số đạt tối đa, vì sao bước gỡ không làm hỏng

**Hiện tượng.** `n` slot, `n` item, item `i` đang ở slot `i`. `b` slot có dấu, `a` item ưa slot có dấu. Cần một hoán vị: càng nhiều
item vào đúng nhóm slot ưa thích càng tốt, rồi càng ít item đứng yên càng tốt.

| Ký hiệu | Là gì |
|---|---|
| `M(j)` | slot `j` có dấu |
| `P(i)` | item `i` ưa slot có dấu |
| `σ(j)` | item slot `j` nhận (`itemOfSlot[j]`) |
| đúng nhóm | `P(σ(j)) = M(j)` |

**Trần của số đúng nhóm.** Item ưa có dấu chỉ đúng nhóm khi vào slot có dấu: nhiều nhất `min(a, b)`. Tương tự phía không dấu:
`min(n − a, n − b)`. Trần là tổng hai số.

**① Ghép theo chỉ số chạm trần.** Dãy slot xếp `b` slot có dấu trước; dãy item xếp `a` item ưa có dấu trước; ghép phần tử thứ `k`
của hai dãy. Với `a ≤ b`:

| Khúc `k` | Item | Slot | Đúng nhóm |
|---|---|---|---|
| `[0, a)` | ưa có dấu | có dấu | ✓ |
| `[a, b)` | ưa không dấu | có dấu | tràn |
| `[b, n)` | ưa không dấu | không dấu | ✓ |

Số đúng nhóm `a + (n − b) = min(a, b) + min(n − a, n − b)`. Ca `a > b` đối xứng: khúc giữa là item ưa có dấu tràn sang slot không
dấu. Xáo từng nhóm trước khi ghép nên item nào tràn, slot nào nhận phần tràn đều ngẫu nhiên.

**② Đổi hai slot giữ nguyên số đúng nhóm khi** hai slot cùng dấu (mỗi item sang slot cùng loại với slot cũ của nó), hoặc hai item
cùng ưa thích (hai cặp (ưa thích, dấu) chỉ đổi chủ). Slot `j` đang giữ item `j` đổi với slot `j' ≠ j`: slot `j` nhận `σ(j')`, khác
`j` vì item `j` đang ở slot `j`; slot `j'` nhận item `j`, khác `j'`. Phép đổi **không tạo** item đứng yên mới, nên một lượt quét
từ đầu tới cuối là đủ.

**③ Item còn đứng yên là bắt buộc.** Bước gỡ bỏ qua slot `j` chỉ khi mọi slot khác vừa khác dấu với `j`, vừa giữ item khác ưa
thích với item `j`: tức `j` là slot duy nhất loại `M(j)` và item `j` là item duy nhất ưa `P(j)`. Nếu `P(j) = M(j)`: mọi item khác ưa
loại kia, mọi slot khác thuộc loại kia, nên muốn đạt trần thì item `j` phải ở slot `j`. Nếu `P(j) ≠ M(j)` với `n ≥ 2`: một cách xếp
cho item `j` đứng yên có 0 item đúng nhóm, trong khi trần là 2 — cách xếp đã chạm trần ở ① không thể rơi vào ca này.

**Kiểm chứng:** bản mẫu C# cùng code GĐ1–2, vét cạn `n ≤ 6`, mọi tổ hợp dấu × ưa thích, 20 seed mỗi tổ hợp (109 220 lượt): ① ② ③
đúng ở mọi lượt, với ③ so bằng cách liệt kê mọi hoán vị đạt trần. Đối chứng: bỏ bước gỡ thì ca `[1, 2, 0]` bên dưới sai ở 150/200 seed.

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| dấu `[T, F, F]`, ưa `[T, T, F]` | luôn `[1, 2, 0]`: slot 0 phải nhận item ưa có dấu mà không phải item 0; item 2 không về slot 2 | cứu bằng item cùng ưa thích |
| dấu `[T, F, F]`, ưa `[T, F, F]` | luôn `[0, 2, 1]`: item 0 buộc đứng yên, hai item còn lại đổi nhau | đứng yên bắt buộc |
| `n` 1 | `[0]` | đứng yên bắt buộc |
| dấu 5/8, ưa 3/8 | đúng nhóm `3 + 3 = 6` | `a < b` |
| dấu 2/8, ưa 6/8 | đúng nhóm `2 + 2 = 4` | `a > b` |

---

## GĐ1 — Ghép đạt số đúng nhóm tối đa; hàm xáo dùng chung

**Mục tiêu:** caller đưa dấu slot, ưa thích của item và nguồn ngẫu nhiên, nhận một hoán vị đạt số đúng nhóm tối đa. Xong GĐ này
caller "dồn loại A về vùng B" đã dùng được (item có thể còn đứng yên); `FisherYates.Shuffle` sẵn cho người gọi thứ hai (`ShuffleBag`).

**Vì sao bây giờ:** ② là bảo đảm caller mua hàm này để có; bước gỡ đứng yên ở GĐ2 dựa trên một hoán vị đã đạt ②.

**Sơ đồ sau GĐ1** (`+` là phần mới)

```
  caller ──itemOfSlot, isSlotMarked, prefersMarked, nextIndex──► SlotShuffle.Assign+ ──► FillMarkedFirstShuffled+ ×2
                                                                        │                        └──► FisherYates.Shuffle+
                                                                        │ ghép theo chỉ số
                                                                        ▼
                                                                    itemOfSlot ──► caller
```

### `Runtime/Utilities/Permutation/SlotShuffle.cs` — file mới

```csharp
using System;
using System.Buffers;

namespace Horcrux.Runtime.Utilities.Permutation
{
    /// <summary>
    /// Xếp lại n item vào chính n slot chúng đang đứng (item i ở slot i). Slot có dấu hoặc không, item ưa một trong hai loại:
    /// số item vào đúng loại ưa thích là tối đa, phần tràn chọn ngẫu nhiên theo nguồn caller đưa.
    /// </summary>
    public static class SlotShuffle
    {
        /// <summary>
        /// Ghi vào <paramref name="itemOfSlot"/>[j] chỉ số item slot j nhận. Số item vào đúng loại slot ưa thích là tối đa;
        /// item phải tràn sang loại kia và slot nhận chúng chọn ngẫu nhiên. Cùng input, cùng chuỗi nextIndex thì cùng kết quả.
        /// </summary>
        /// <param name="isSlotMarked">Slot j có dấu không.</param>
        /// <param name="prefersMarked">Item i (đang ở slot i) ưa slot có dấu không.</param>
        /// <param name="nextIndex">Nhận n ≥ 1, trả số nguyên trong [0, n).</param>
        public static void Assign(Span<int> itemOfSlot, ReadOnlySpan<bool> isSlotMarked, ReadOnlySpan<bool> prefersMarked,
            Func<int, int> nextIndex)
        {
            int count = itemOfSlot.Length;
            if (isSlotMarked.Length != count || prefersMarked.Length != count)
                throw new ArgumentException("[SlotShuffle]: itemOfSlot, isSlotMarked, prefersMarked phải dài bằng nhau.");
            if (nextIndex == null) throw new ArgumentNullException(nameof(nextIndex));
            if (count == 0) return;

            var slotBuffer = ArrayPool<int>.Shared.Rent(count);
            var itemBuffer = ArrayPool<int>.Shared.Rent(count);
            try
            {
                var slots = slotBuffer.AsSpan(0, count);
                var items = itemBuffer.AsSpan(0, count);
                FillMarkedFirstShuffled(slots, isSlotMarked, nextIndex);
                FillMarkedFirstShuffled(items, prefersMarked, nextIndex);
                // Hai dãy cùng đặt nhóm có dấu lên đầu: đầu và cuối dãy khớp ưa thích, khúc giữa là phần tràn.
                for (int k = 0; k < count; k++)
                    itemOfSlot[slots[k]] = items[k];
            }
            finally
            {
                ArrayPool<int>.Shared.Return(slotBuffer);
                ArrayPool<int>.Shared.Return(itemBuffer);
            }
        }

        /// <summary>Ghi chỉ số có cờ trước, không cờ sau, rồi xáo riêng từng nhóm.</summary>
        private static void FillMarkedFirstShuffled(Span<int> order, ReadOnlySpan<bool> isMarked, Func<int, int> nextIndex)
        {
            int markedCount = 0;
            for (int i = 0; i < isMarked.Length; i++)
                if (isMarked[i]) order[markedCount++] = i;

            int next = markedCount;
            for (int i = 0; i < isMarked.Length; i++)
                if (!isMarked[i]) order[next++] = i;

            FisherYates.Shuffle(order.Slice(0, markedCount), nextIndex);
            FisherYates.Shuffle(order.Slice(markedCount), nextIndex);
        }
    }
}
```

### `Runtime/Utilities/Permutation/FisherYates.cs` — file mới

```csharp
using System;

namespace Horcrux.Runtime.Utilities.Permutation
{
    /// <summary>Xáo tại chỗ theo nguồn ngẫu nhiên của caller: nextIndex(n) trả số trong [0, n).</summary>
    public static class FisherYates
    {
        /// <summary>
        /// Từ cuối về đầu, gọi nextIndex đúng length − 1 lần. Nguồn đều thì mọi hoán vị đồng xác suất; cùng chuỗi nextIndex thì
        /// cùng kết quả.
        /// </summary>
        public static void Shuffle<T>(Span<T> values, Func<int, int> nextIndex)
        {
            for (int i = values.Length - 1; i > 0; i--)
            {
                int j = nextIndex(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }
    }
}
```

| Consumes | Produces |
|---|---|
| `System.Buffers.ArrayPool<int>`, `nextIndex` của caller | `SlotShuffle.Assign` · `FisherYates.Shuffle<T>` (public; người gọi thứ hai: `ShuffleBag`) |

**Quyết định** — trả `itemOfSlot` (slot nhận item nào), không `slotOfItem`: caller ghi dữ liệu vào từng slot, đọc thẳng
`data[itemOfSlot[j]]` không cần đảo mảng. — Mảng tạm thuê `ArrayPool`, không `new int[n]`: hàm thuần không biết caller gọi ở nhịp nào;
thuê và trả chỉ thêm hai dòng. — Lệch độ dài ném `ArgumentException`: lỗi của code caller, nổ ngay lần gọi đầu. — `count == 0` thoát
trước khi thuê mảng. — Không xáo tại chỗ trên `itemOfSlot`: dãy slot và dãy item phải cùng có mặt lúc ghép, mảng ghi ra đánh chỉ số
theo slot.

**Quyết định** — hàm xáo là class riêng `FisherYates`, public: `ShuffleBag` (`Horcrux_AudioVariants_Plan.md`) xáo cùng cách; một bản
cho cả hai giữ bất biến "gọi nguồn đúng n − 1 lần" ở một chỗ. Phương án bỏ: `SlotShuffle.Shuffle` public — class xếp slot mang thêm
hàm xáo chung là hai lý do thay đổi. — `FisherYates.Shuffle` không chặn `nextIndex` null: dãy dài từ 2 nổ `NullReferenceException` ngay
lần gọi đầu; `Assign` chặn ở cửa của nó vì báo đúng tên tham số. — Generic `<T>`: thân không phụ thuộc kiểu phần tử, `ShuffleBag` xáo
`int`, caller sau xáo kiểu khác không cần overload.

**Editor setup:** tạo thư mục `Runtime/Utilities/Permutation` và hai file trong Project window (để có `.meta`).

**Kiểm:** compile sạch. Nguồn ngẫu nhiên `new System.Random(seed).Next`. `SlotShuffleTests` đếm "đúng nhóm" bằng
`prefersMarked[itemOfSlot[j]] == isSlotMarked[j]`.

`Tests/EditMode/FisherYatesTests.cs`:

| Case | Input | Kỳ vọng |
|---|---|---|
| rỗng, một phần tử | độ dài 0, 1 | không đổi, `nextIndex` không được gọi |
| số lần gọi nguồn | độ dài 2…8 | đúng `length − 1` lần |
| là hoán vị | độ dài 8, 200 seed | đủ mặt 0..7, không trùng |
| tất định | cùng dãy, hai `System.Random(7)` | hai kết quả trùng |
| phân phối | `[0, 1, 2]`, seed 0…5 999 | mỗi hoán vị trong 6 nằm trong [900, 1 100] lần (bản mẫu: 998–1 003). Đối chứng: thân gọi `nextIndex(n)` thay `nextIndex(i + 1)` ra 665–1 339, case đỏ |

`Tests/EditMode/SlotShuffleTests.cs`:

| Case | Input | Kỳ vọng |
|---|---|---|
| rỗng | `n` 0 | không ném, không gọi `nextIndex` |
| lệch độ dài | `itemOfSlot` 3, `isSlotMarked` 2 | `ArgumentException` |
| thiếu nguồn | `nextIndex` null | `ArgumentNullException` |
| là hoán vị | `n` 8, dấu và ưa ngẫu nhiên, 200 seed | mỗi item xuất hiện đúng một lần |
| `a < b` | dấu 5/8, ưa 3/8, 200 seed | đúng nhóm 6 |
| `a > b` | dấu 2/8, ưa 6/8, 200 seed | đúng nhóm 4 |
| `a = b` | dấu 4/8, ưa 4/8 ở chỗ khác | đúng nhóm 8 |
| không slot nào có dấu | dấu 0/8, ưa 3/8 | đúng nhóm 5 |
| tất định | cùng input, hai `System.Random(7)` | hai kết quả trùng |
| phần tràn ngẫu nhiên | dấu `[T×5, F×3]`, ưa `[T×3, F×5]`, 1000 seed | gộp mọi lượt, item ưa không dấu từng nằm ở slot có dấu là đủ `{3, 4, 5, 6, 7}` |
| không cấp phát ở lần gọi thứ hai | `n` 8, gọi một lần làm nóng pool, nguồn dựng trước | `Is.Not.AllocatingGCMemory()`. Đối chứng: cùng khung bọc một lời gọi dựng lambda bắt biến — constraint bắt được |

**Mốc commit:** `feat(permutation): SlotShuffle.Assign, FisherYates.Shuffle`.

---

## GĐ2 — Không item nào đứng yên khi tránh được

**Mục tiêu:** item chỉ còn ở slot cũ khi mọi cách xếp đạt tối đa đều buộc vậy. Xong GĐ này bàn xáo trông khác hẳn bàn gốc kể cả ở
vùng không dính tới dấu.

**Vì sao bây giờ:** cần hoán vị đã đạt tối đa của GĐ1 làm nền: bước gỡ chỉ đổi những cặp giữ nguyên số đúng nhóm (`§0` ②).

**Sơ đồ sau GĐ2**

```
  caller ──► SlotShuffle.Assign ──► FillMarkedFirstShuffled ×2 ──► ghép ──► MoveItemsOffOwnSlot+ ──► itemOfSlot ──► caller
```

### `SlotShuffle.cs` › `Assign` › sau vòng ghép, trong `try`

```csharp
                for (int k = 0; k < count; k++)
                    itemOfSlot[slots[k]] = items[k];

                MoveItemsOffOwnSlot(itemOfSlot, isSlotMarked, prefersMarked, nextIndex);
            }
```

Sửa `<summary>` của `Assign`, thêm câu sau "…chọn ngẫu nhiên.": `Item chỉ ở lại slot cũ khi mọi cách xếp đạt tối đa đều giữ nó ở
đó.`

### `SlotShuffle.cs` › thêm sau `FillMarkedFirstShuffled`

```csharp
        /// <summary>
        /// Slot còn giữ item cũ đổi với slot khác cùng dấu, hoặc đang chứa item cùng ưa thích: số đúng nhóm giữ nguyên, phép đổi
        /// không tạo item đứng yên mới. Không có slot như vậy thì item buộc phải ở lại.
        /// </summary>
        private static void MoveItemsOffOwnSlot(Span<int> itemOfSlot, ReadOnlySpan<bool> isSlotMarked,
            ReadOnlySpan<bool> prefersMarked, Func<int, int> nextIndex)
        {
            int count = itemOfSlot.Length;
            for (int slot = 0; slot < count; slot++)
            {
                if (itemOfSlot[slot] != slot) continue;

                // Quét vòng từ điểm ngẫu nhiên: slot đổi cùng không cố định ở đầu mảng.
                int start = nextIndex(count);
                for (int step = 0; step < count; step++)
                {
                    int other = (start + step) % count;
                    if (other == slot) continue;
                    bool isSameMark = isSlotMarked[other] == isSlotMarked[slot];
                    bool isSamePreference = prefersMarked[itemOfSlot[other]] == prefersMarked[slot];
                    if (!isSameMark && !isSamePreference) continue;

                    (itemOfSlot[slot], itemOfSlot[other]) = (itemOfSlot[other], itemOfSlot[slot]);
                    break;
                }
            }
        }
```

`prefersMarked[slot]` là ưa thích của item đang đứng yên: item đó là item `slot`.

| Consumes | Produces |
|---|---|
| GĐ1 (`itemOfSlot` đạt tối đa) | `MoveItemsOffOwnSlot` (private) |

**Quyết định** — một lượt quét, không lặp tới khi hết đứng yên: phép đổi không tạo đứng yên mới (`§0` ②). — Nhận cả nhánh "cùng ưa
thích", không chỉ "cùng dấu": thiếu nhánh này thì slot có dấu duy nhất rơi trúng item cũ của nó sẽ đứng yên dù có item khác cùng ưa
thích nhận thay được (mốc `[1, 2, 0]`). — Phương án bỏ: thuật toán sinh hoán vị không điểm bất động ngay từ đầu (Sattolo) — chỉ đúng
khi không có nhóm; ghép với ràng buộc nhóm thì mất bảo đảm tối đa.

**Editor setup:** không.

**Kiểm:** compile sạch; bảng GĐ1 vẫn xanh (số đúng nhóm không đổi qua bước gỡ).

| Case | Input | Kỳ vọng |
|---|---|---|
| ai cũng rời chỗ | dấu = ưa = `[T×3, F×5]`, 500 seed | không `j` nào `itemOfSlot[j] == j`. Không dùng mảng ngẫu nhiên: mảng có đúng một `T` (hay một `F`) rơi vào ca đứng yên bắt buộc |
| cứu bằng item cùng ưa thích | dấu `[T, F, F]`, ưa `[T, T, F]`, 200 seed | luôn `[1, 2, 0]` (đỏ khi thiếu bước gỡ: sai ở 150/200 seed) |
| đứng yên bắt buộc | dấu `[T, F, F]`, ưa `[T, F, F]`, 200 seed | luôn `[0, 2, 1]` |
| một phần tử | `n` 1 | `[0]` |
| mọi slot có dấu, mọi item ưa có dấu | `n` 5, 200 seed | không đứng yên; đúng nhóm 5 |
| vét cạn | `n` 0–6, mọi tổ hợp dấu × ưa, 20 seed mỗi tổ hợp | hoán vị · đúng nhóm = trần `§0` · tập slot đứng yên = tập slot mà **mọi** hoán vị đạt trần đều giữ item cũ (liệt kê mọi hoán vị) |

**Mốc commit:** `feat(permutation): SlotShuffle gỡ item đứng yên`.

---

## GĐ3 — Áp hoán vị bằng tráo cặp, hoán vị ngược

**Mục tiêu:** caller có chỗ chứa chỉ đổi được bằng tráo cặp (object giữ cả state lẫn view, không chép dữ liệu ra được) áp kết quả
`Assign` lên chỗ chứa đó, và trả về trạng thái gốc bằng hoán vị ngược — không tự viết vòng tráo. Xong GĐ này "xáo rồi trả về đúng bàn
gốc" là ba lời gọi.

**Vì sao bây giờ:** caller đầu tiên cần đúng hai việc này: vật của nó chỉ đổi được bằng tráo cặp, nên bản tạm đã phải viết vòng tráo
theo chu trình và vòng đảo mảng; hai thứ đó không mang domain nào.

**Sơ đồ sau GĐ3**

```
  caller ──► SlotShuffle.Assign ──► itemOfSlot ──► SlotShuffle.ApplyBySwaps+ ──swap(owner, a, b)──► chỗ chứa của caller
                                        │
                                        └──► SlotShuffle.Invert+ ──► slotOfItem ──► ApplyBySwaps+ ──► chỗ chứa về gốc
```

Dùng ở caller (chỗ chứa `pieces`, `SwapWith` là thao tác tráo duy nhất nó có):

```csharp
SlotShuffle.Assign(lookFrom, isSlotMarked, prefersMarked, new System.Random(seed).Next);
SlotShuffle.ApplyBySwaps(lookFrom, pieces, static (pieces, a, b) => pieces[a].SwapWith(pieces[b]));
// … xong lượt cần bàn xáo:
SlotShuffle.Invert(lookFrom, restoreFrom);
SlotShuffle.ApplyBySwaps(restoreFrom, pieces, static (pieces, a, b) => pieces[a].SwapWith(pieces[b]));
```

### `SlotShuffle.cs` › thêm sau `Assign`

```csharp
        /// <summary>Ghi vào <paramref name="slotOfItem"/>[i] slot đang nhận item i: hoán vị ngược của <paramref name="itemOfSlot"/>.</summary>
        public static void Invert(ReadOnlySpan<int> itemOfSlot, Span<int> slotOfItem)
        {
            if (slotOfItem.Length != itemOfSlot.Length)
                throw new ArgumentException("[SlotShuffle]: itemOfSlot, slotOfItem phải dài bằng nhau.");

            for (int slot = 0; slot < itemOfSlot.Length; slot++)
                slotOfItem[itemOfSlot[slot]] = slot;
        }

        /// <summary>
        /// Slot j nhận thứ đang ở slot <paramref name="itemOfSlot"/>[j], chỉ bằng tráo cặp: cho chỗ chứa không chép dữ liệu ra
        /// được. Tráo dọc từng vòng của hoán vị, vòng dài k tốn k − 1 lần tráo.
        /// </summary>
        /// <param name="swap">Tráo thứ đang ở hai slot; nhận lại owner để caller truyền lambda static.</param>
        public static void ApplyBySwaps<TOwner>(ReadOnlySpan<int> itemOfSlot, TOwner owner, Action<TOwner, int, int> swap)
        {
            if (swap == null) throw new ArgumentNullException(nameof(swap));
            int count = itemOfSlot.Length;
            if (count == 0) return;

            var placedBuffer = ArrayPool<bool>.Shared.Rent(count);
            try
            {
                var isPlaced = placedBuffer.AsSpan(0, count);
                // Không phải hoán vị thì vòng tráo không khép, vòng lặp treo: chặn trước khi đụng dữ liệu caller.
                isPlaced.Clear();
                foreach (int item in itemOfSlot)
                {
                    if ((uint)item >= (uint)count || isPlaced[item])
                        throw new ArgumentException("[SlotShuffle]: itemOfSlot phải là hoán vị của 0..n-1.");
                    isPlaced[item] = true;
                }

                isPlaced.Clear();
                for (int start = 0; start < count; start++)
                {
                    if (isPlaced[start]) continue;

                    // Slot nhận thứ ở nguồn của nó; thứ của start dời tới nguồn, đi tiếp tới khi vòng khép về start.
                    int slot = start;
                    while (itemOfSlot[slot] != start)
                    {
                        isPlaced[slot] = true;
                        swap(owner, slot, itemOfSlot[slot]);
                        slot = itemOfSlot[slot];
                    }

                    isPlaced[slot] = true;
                }
            }
            finally
            {
                ArrayPool<bool>.Shared.Return(placedBuffer);
            }
        }
```

**Vì sao vòng tráo đúng.** Đi từ `start`: tráo `slot` với nguồn `s = itemOfSlot[slot]` cho `slot` đúng thứ của `s` (nguồn chưa bị
đụng trong vòng này), còn `s` nhận thứ của `start` — quy nạp, thứ của `start` luôn nằm ở slot vừa nhận. Vòng khép khi nguồn của
`slot` là `start`: `slot` đang giữ đúng thứ của `start`. Mỗi slot của vòng đúng chỗ sau một lần tráo, slot cuối không cần tráo.
**Trả về:** sau khi áp `σ`, slot `j` giữ thứ gốc của `σ(j)`; thứ gốc của `k` nằm ở slot `σ⁻¹(k)`, nên áp `σ⁻¹` đưa mọi thứ về chỗ.
**Kiểm chứng:** bản Python cùng vòng tráo, vét cạn mọi hoán vị `n ≤ 7` (5 914 hoán vị): áp đúng và round-trip 0 sai; đối chứng của
bảng case ra `"bcaefd"` sau 8 lần tráo.

| Consumes | Produces |
|---|---|
| `itemOfSlot` của `Assign` hoặc bất kỳ hoán vị nào | `SlotShuffle.Invert`, `SlotShuffle.ApplyBySwaps` |

**Quyết định** — `swap` nhận lại `owner`: lambda `static` không bắt biến, không cấp phát closure. — Kiểm hoán vị trước khi tráo: mảng
trùng phần tử làm vòng `while` không khép, game treo thay vì nổ; một lượt `O(n)` trên cùng bộ đệm. — `Invert` không kiểm hoán vị: chỉ
số ngoài dải tự ném, đầu vào thường là kết quả `Assign`. — Phương án bỏ: trả danh sách cặp tráo cho caller tự chạy — thêm một bộ đệm
caller phải thuê, caller nào cũng viết lại vòng chạy.

**Editor setup:** không.

**Kiểm:** compile sạch; bảng GĐ1–2 vẫn xanh. Test dùng mảng `char` làm chỗ chứa, `swap` tráo hai ô.

| Case | Input | Kỳ vọng |
|---|---|---|
| áp đúng | chỗ chứa `"abcdef"`, `itemOfSlot` `[2, 0, 1, 5, 3, 4]` | `"cabfde"` (slot j giữ ký tự gốc ở slot `itemOfSlot[j]`) |
| số lần tráo | như trên (hai vòng dài 3) | 4 lần gọi `swap` |
| hoán vị đồng nhất | `[0, 1, 2, 3]` | 0 lần gọi `swap` |
| round-trip | `n` 8, `Assign` 200 seed, áp rồi áp `Invert` | chỗ chứa về đúng ban đầu |
| `Invert` | `[2, 0, 1]` | `[1, 2, 0]` |
| không phải hoán vị | `[0, 0, 1]` | `ArgumentException`, 0 lần gọi `swap` |
| ngoài dải | `[0, 3, 1]` | `ArgumentException` |
| thiếu `swap` | `swap` null | `ArgumentNullException` |
| lệch độ dài `Invert` | 3 và 2 | `ArgumentException` |
| rỗng | `n` 0 | không ném, không gọi `swap` |
| đối chứng | bỏ dòng `isPlaced[slot] = true` cuối vòng | case "áp đúng" đỏ: `"bcaefd"`, 8 lần tráo (vòng đã khép bị đi lại) |

**Mốc commit:** `feat(permutation): SlotShuffle.Invert, ApplyBySwaps`.

---

## GĐ4 — Tài liệu `docs/Runtime/SlotShuffle/SlotShuffle.md` và `SlotShuffle.html` (agent)

`.md` mở đầu `Code: Runtime/Utilities/Permutation`. Mục: ba bảo đảm và thứ tự ưu tiên · `§0` (trần, ghép theo chỉ số, bước gỡ, ca
đứng yên bắt buộc, bảng mốc) · bảng API `Assign`, `Invert`, `ApplyBySwaps`, `FisherYates.Shuffle` · "Design decisions" gom "Cố ý không
làm", "Quyết định trái trực giác" và các **Quyết định** của GĐ1–3 · "Dùng": caller giữ dữ liệu item theo chỉ số slot gốc, ghi
`data[itemOfSlot[j]]` vào slot `j`; chỗ chứa chỉ tráo được thì `ApplyBySwaps`; trả về trạng thái gốc thì ghi lại bản gốc đã giữ, hoặc
`Invert` rồi `ApplyBySwaps` · nguồn ngẫu nhiên: `System.Random(seed).Next` cho kết quả cố định, không dùng
`UnityEngine.Random.InitState`. Ví dụ tên trung tính (`Piece`, `Spot`). Không có "Trước khi chạy": hàm thuần, không wire. `.html` chọn
từ `.md`: kịch bản (dồn vật đặc biệt vào vùng khuất rồi trả về sau lượt dạy; xáo lại điểm spawn mỗi đợt, quân mạnh ra xa; chia lại bài,
lá chủ chốt úp xuống), cách dùng, flow ghép theo chỉ số, lưu ý (ưa thích thắng đổi chỗ; độ dài ba mảng; seed cố định).

**Mốc commit:** `docs(permutation): SlotShuffle.md, FisherYates`.

---

## GĐ5 — category-jam dùng bản Horcrux, xoá bản tạm, xoá Plan (agent)

Chạy sau khi GĐ1–3 compile sạch và bảng case xanh. Caller duy nhất là `ShuffleBooster`
(`Assets/_CategoryJam/Scripts/Director/ShuffleBooster.cs`), bản tạm đã gọi đủ `Assign`, `Invert`, `ApplyBySwaps`:

| Ô | Giá trị trong code |
|---|---|
| slot | tile hàng trước có ID, rồi tile hàng sau có ID — chép ra `_scrambledTiles` vì `FrontLayerTiles`, `BackLayerTiles` là bộ đệm dùng chung |
| item `i` | cặp (ID, variant) đang ở slot `i` |
| `isSlotMarked[j]` | `j >= frontCount || tile.IsHidden` — chỗ người chơi không nhặt được ID vào box |
| `prefersMarked[i]` | `ActiveBoxTileIdsAndAmount.ContainsKey(tile.ID)` |
| `nextIndex` | `new System.Random(seed).Next`, seed là số level |
| `swap` của `ApplyBySwaps` | `tiles[a].SwapIdWith(tiles[b])` — cách duy nhất đổi (ID, variant) mà giữ ảnh trong tập có sẵn |

**Việc:** thêm `using Horcrux.Runtime.Utilities.Permutation;` vào `ShuffleBooster.cs` (giữ `using CategoryJam.Gameplay.Utilities;`:
`KShuffle` của luồng Shuffle thường nằm ở đó) · xoá `Assets/_CategoryJam/Scripts/Utilities/SlotShuffle.cs` và `.meta` (static class,
không asset nào giữ GUID của nó) · sửa dòng "Mở rộng sau" về bản tạm trong `Assets/_CategoryJam/Scripts/Tutorial/Tutorial.md` thành
trỏ `docs/Runtime/SlotShuffle/` · xoá Plan này (và `.meta`).

**Kiểm:** grep `CategoryJam.Gameplay.Utilities.SlotShuffle` và đường dẫn bản tạm ra 0 · lệnh kiểm ở đầu Plan xanh.

**Kịch bản chơi thử** (developer):

| Vào đâu | Làm gì | Nhìn gì | Khác trước | Dấu hiệu hỏng |
|---|---|---|---|---|
| level mở khoá booster Shuffle, save mới | chờ tắt loading | bàn lộn xộn; hàng trước không có tile của box đang mở trừ khi bàn không đủ chỗ | không: cùng seed level ra cùng bàn như bản tạm (bản mẫu trùng 109 220/109 220 lượt) | bàn y như Level Editor (không xáo); tile của box đang mở nằm hàng trước khi bàn còn chỗ |
| như trên | Show me! rồi bấm nút Shuffle Free | bàn về đúng như Level Editor | không | một tile sai ảnh so với Level Editor |
| như trên | thoát trước Show me! rồi vào lại | bàn xáo giống hệt lần trước | không | bàn xáo khác lần trước |

**Mốc commit (dự án):** `refactor(shuffle): dùng SlotShuffle của Horcrux, xoá bản tạm` kèm con trỏ submodule.

## File → GĐ

| File | GĐ1 | GĐ2 | GĐ3 | GĐ4 | GĐ5 |
|---|---|---|---|---|---|
| `Runtime/Utilities/Permutation/SlotShuffle.cs` | tạo, `Assign`, `FillMarkedFirstShuffled` | `MoveItemsOffOwnSlot`, gọi trong `Assign`, sửa summary | `Invert`, `ApplyBySwaps` | | |
| `Runtime/Utilities/Permutation/FisherYates.cs` | tạo | | | | |
| `Tests/EditMode/FisherYatesTests.cs`, `Tests/EditMode/SlotShuffleTests.cs` (agent) | tạo | thêm case | thêm case | | |
| `docs/Runtime/SlotShuffle/SlotShuffle.md`, `.html` (agent) | | | | tạo | |
| dự án: `ShuffleBooster.cs`, bản tạm `SlotShuffle.cs`, `Tutorial.md`; Plan này (agent) | | | | | thêm `using`, xoá bản tạm, sửa dòng Mở rộng sau, xoá Plan |
