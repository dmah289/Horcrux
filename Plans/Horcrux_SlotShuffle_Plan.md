# Plan — `SlotShuffle`: xếp lại n vật vào chính n chỗ của chúng, dồn theo nhóm, không vật nào đứng yên khi tránh được

Bản tạm: category-jam `Assets/_CategoryJam/Scripts/Utilities/SlotShuffle.cs` (code GĐ1–3 nguyên văn, hàm xáo còn private), caller `Director/ShuffleBooster.cs` › `ScrambleAwayFromBoxes`, `RestoreScrambledBoard`.
Không phụ thuộc Plan nào. Chung `Runtime/Utilities/Permutation/FisherYates.cs` với `Horcrux_AudioVariants_Plan.md` (`ShuffleBag` gọi `FisherYates.Shuffle`): Plan đó gõ sau GĐ1 Plan này.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,CategoryJam.Gameplay.csproj --tests Horcrux.EditModeTests,CategoryJam.EditModeTests`.

## Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| n item ở n slot, slot có dấu hay không, item ưa có dấu hay không | xếp slot và item theo nhóm, xáo từng nhóm, ghép theo chỉ số | `itemOfSlot` là hoán vị; số item vào đúng nhóm ưa thích là tối đa, phần tràn ngẫu nhiên |
| item rời được slot cũ mà không giảm số đúng nhóm | đổi với slot cùng dấu hoặc đang chứa item cùng ưa thích | không item nào ở slot cũ |
| slot `j` là slot duy nhất loại của nó, item `j` là item duy nhất ưa loại đó | giữ nguyên | item `j` đứng yên: dấu `[T, F, F]`, ưa `[T, F, F]` luôn ra `[0, 2, 1]` |
| cùng input, cùng chuỗi `nextIndex` | tất định | cùng kết quả |
| `n` 0 | thoát trước khi thuê mảng | không ném, không gọi `nextIndex` |
| ba mảng lệch độ dài · `nextIndex` null | ném ở cửa `Assign` | `ArgumentException` · `ArgumentNullException` |
| chỗ chứa chỉ tráo được (object giữ cả state lẫn view) | `ApplyBySwaps` tráo dọc từng vòng của hoán vị | slot `j` giữ thứ gốc của slot `itemOfSlot[j]`; vòng dài k tốn k − 1 lần tráo |
| trả về bố cục gốc | `Invert` rồi `ApplyBySwaps` | chỗ chứa về đúng ban đầu |
| `itemOfSlot` không phải hoán vị | `ApplyBySwaps` kiểm trước khi tráo | `ArgumentException`, 0 lần gọi `swap` |
| xáo một dãy bằng nguồn riêng | `FisherYates.Shuffle` gọi nguồn đúng length − 1 lần | nguồn đều thì mọi hoán vị đồng xác suất |

## Flow

```
caller (một lần mỗi lần xáo): itemOfSlot (n ô ghi ra), isSlotMarked, prefersMarked, nextIndex
   │
   ▼
SlotShuffle.Assign                                                                       SlotShuffle.cs
   ├─ FillMarkedFirstShuffled ×2: có dấu trước, không dấu sau
   │     └─► FisherYates.Shuffle (từng nhóm, nextIndex)                                  FisherYates.cs
   │              ▲
   │              └── ShuffleBag                                     ShuffleBag.cs (Horcrux_AudioVariants_Plan.md)
   ├─ ghép theo chỉ số: itemOfSlot[slots[k]] = items[k]
   └─ MoveItemsOffOwnSlot ──► nextIndex (một lần mỗi slot còn giữ item cũ)
   ▼
itemOfSlot ──► caller: slot j nhận dữ liệu của item itemOfSlot[j]
   ├─ SlotShuffle.ApplyBySwaps(itemOfSlot, owner, swap) ──► swap(owner, a, b) của caller     SlotShuffle.cs
   └─ SlotShuffle.Invert(itemOfSlot, slotOfItem) ──► ApplyBySwaps: chỗ chứa về gốc           SlotShuffle.cs
```

## Thuật toán

`n` slot, `n` item, item `i` đứng ở slot `i`; `b` slot có dấu, `a` item ưa có dấu. Ưu tiên: ① kết quả là hoán vị · ② số đúng nhóm tối đa · ③ item chỉ đứng yên khi **mọi** cách đạt ② đều giữ nó ở đó.

| Ký hiệu | Là gì |
|---|---|
| `M(j)` | slot `j` có dấu |
| `P(i)` | item `i` ưa slot có dấu |
| `σ(j)` | item slot `j` nhận (`itemOfSlot[j]`) |
| đúng nhóm | `P(σ(j)) = M(j)` |

**Trần** số đúng nhóm = `min(a, b) + min(n − a, n − b)`.

**Ghép theo chỉ số chạm trần.** Dãy slot có dấu trước, dãy item ưa có dấu trước, ghép phần tử thứ `k`. Với `a ≤ b` (ca `a > b` đối xứng, khúc giữa là item ưa có dấu tràn sang slot không dấu):

| Khúc `k` | Item | Slot | Đúng nhóm |
|---|---|---|---|
| `[0, a)` | ưa có dấu | có dấu | ✓ |
| `[a, b)` | ưa không dấu | có dấu | tràn |
| `[b, n)` | ưa không dấu | không dấu | ✓ |

Tổng `a + (n − b)` = trần. Xáo từng nhóm trước khi ghép nên item tràn và slot nhận phần tràn đều ngẫu nhiên.

**Bước gỡ.** Slot `j` đang giữ item `j` (nên ưa thích của item đó là `prefersMarked[j]`) đổi với slot `j' ≠ j`:

| Điều kiện đổi | Số đúng nhóm giữ nguyên vì |
|---|---|
| `M(j') = M(j)` | mỗi item sang slot cùng loại với slot cũ của nó |
| `P(σ(j')) = P(j)` | hai cặp (ưa thích, dấu) chỉ đổi chủ |

Sau đổi slot `j` nhận `σ(j') ≠ j`, slot `j'` nhận item `j ≠ j'`: không tạo đứng yên mới, một lượt quét là đủ. Bước gỡ bỏ qua `j` chỉ khi `j` là slot duy nhất loại `M(j)` và item `j` là item duy nhất ưa `P(j)`: `P(j) = M(j)` thì đạt trần buộc item `j` ở slot `j`; `P(j) ≠ M(j)` với `n ≥ 2` thì cách xếp đó có 0 đúng nhóm, dưới trần 2, ghép theo chỉ số không rơi vào.

**Vòng tráo** (`ApplyBySwaps`): từ `start`, tráo `slot` với nguồn `s = itemOfSlot[slot]` — `slot` nhận đúng thứ của `s`, `s` giữ thứ của `start`; vòng khép khi nguồn của `slot` là `start`. **Trả về:** sau khi áp `σ`, thứ gốc của `k` nằm ở slot `σ⁻¹(k)`, nên áp `σ⁻¹` đưa mọi thứ về chỗ.

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| dấu `[T, F, F]`, ưa `[T, T, F]` | luôn `[1, 2, 0]`: slot 0 phải nhận item ưa có dấu mà không phải item 0; item 2 không về slot 2 | cứu bằng item cùng ưa thích |
| dấu `[T, F, F]`, ưa `[T, F, F]` | luôn `[0, 2, 1]`: item 0 buộc đứng yên, hai item còn lại đổi nhau | đứng yên bắt buộc |
| `n` 1 | `[0]` | đứng yên bắt buộc |
| dấu 5/8, ưa 3/8 | đúng nhóm `3 + 3 = 6` | `a < b` |
| dấu 2/8, ưa 6/8 | đúng nhóm `2 + 2 = 4` | `a > b` |

Bản mẫu: C# cùng code GĐ1–2 vét `n` 0–6 × mọi tổ hợp dấu × ưa × 20 seed (109 220 lượt) ① ② ③ đúng, đối chứng bỏ bước gỡ thì `[1, 2, 0]` sai 150/200 seed · vòng tráo bản Python vét mọi hoán vị `n ≤ 7` (5 914) áp đúng, round-trip 0 sai.

---

## GĐ1 — xong thì caller dồn được loại A về vùng B với số đúng nhóm tối đa; `ShuffleBag` có hàm xáo để gọi

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

**Editor setup**

1. Project window: tạo thư mục `Runtime/Utilities/Permutation` (chưa có).
2. Tạo `SlotShuffle.cs`, `FisherYates.cs` trong đó bằng Project window để có `.meta`.

`Tests/EditMode/FisherYatesTests.cs` (agent), nguồn `new System.Random(seed).Next`:

| Case | Input | Kỳ vọng |
|---|---|---|
| rỗng, một phần tử | độ dài 0, 1 | không đổi, `nextIndex` không được gọi |
| số lần gọi nguồn | độ dài 2…8 | đúng `length − 1` lần |
| là hoán vị | độ dài 8, 200 seed | đủ mặt 0..7, không trùng |
| tất định | cùng dãy, hai `System.Random(7)` | hai kết quả trùng |
| phân phối | `[0, 1, 2]`, seed 0…5 999 | mỗi hoán vị trong 6 nằm trong [900, 1 100] lần (bản mẫu: 998–1 003). Đối chứng: thân gọi `nextIndex(n)` thay `nextIndex(i + 1)` ra 665–1 339, case đỏ |

`Tests/EditMode/SlotShuffleTests.cs` (agent), đúng nhóm đếm bằng `prefersMarked[itemOfSlot[j]] == isSlotMarked[j]`:

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

---

## GĐ2 — xong thì không item nào đứng yên khi tránh được, bàn xáo khác hẳn bàn gốc cả ở vùng không dính dấu

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

Sửa `<summary>` của `Assign`, thêm câu sau "…chọn ngẫu nhiên.": `Item chỉ ở lại slot cũ khi mọi cách xếp đạt tối đa đều giữ nó ở đó.`

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

`Tests/EditMode/SlotShuffleTests.cs` (agent), thêm; bảng GĐ1 vẫn xanh:

| Case | Input | Kỳ vọng |
|---|---|---|
| ai cũng rời chỗ | dấu = ưa = `[T×3, F×5]`, 500 seed | không `j` nào `itemOfSlot[j] == j`. Không dùng mảng ngẫu nhiên: mảng có đúng một `T` (hay một `F`) rơi vào ca đứng yên bắt buộc |
| cứu bằng item cùng ưa thích | dấu `[T, F, F]`, ưa `[T, T, F]`, 200 seed | luôn `[1, 2, 0]` (đỏ khi thiếu bước gỡ: sai ở 150/200 seed) |
| đứng yên bắt buộc | dấu `[T, F, F]`, ưa `[T, F, F]`, 200 seed | luôn `[0, 2, 1]` |
| một phần tử | `n` 1 | `[0]` |
| mọi slot có dấu, mọi item ưa có dấu | `n` 5, 200 seed | không đứng yên; đúng nhóm 5 |
| vét cạn | `n` 0–6, mọi tổ hợp dấu × ưa, 20 seed mỗi tổ hợp | hoán vị · đúng nhóm = trần ở Thuật toán · tập slot đứng yên = tập slot mà **mọi** hoán vị đạt trần đều giữ item cũ (liệt kê mọi hoán vị) |

---

## GĐ3 — xong thì "xáo rồi trả về đúng bàn gốc" trên chỗ chứa chỉ tráo được là ba lời gọi

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

`Tests/EditMode/SlotShuffleTests.cs` (agent), thêm; bảng GĐ1–2 vẫn xanh; chỗ chứa là mảng `char`, `swap` tráo hai ô:

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

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/SlotShuffle/SlotShuffle.md`, `.html` | tạo, `Code: Runtime/Utilities/Permutation`: ba bảo đảm và thứ tự ưu tiên · Thuật toán và bảng mốc · API `Assign`, `Invert`, `ApplyBySwaps`, `FisherYates.Shuffle` · cách dùng: dữ liệu item theo chỉ số slot gốc, ghi `data[itemOfSlot[j]]` vào slot `j`, chỗ chứa chỉ tráo được thì `ApplyBySwaps`, trả về thì `Invert` rồi `ApplyBySwaps` · nguồn `System.Random(seed).Next`; ví dụ tên trung tính (`Piece`, `Spot`); `.html`: kịch bản, cách dùng, flow ghép theo chỉ số, lưu ý (ưa thích thắng đổi chỗ, độ dài ba mảng, seed cố định) |
| `Assets/_CategoryJam/Scripts/Director/ShuffleBooster.cs` | thêm `using Horcrux.Runtime.Utilities.Permutation;`; giữ `using CategoryJam.Gameplay.Utilities;` (`KShuffle` của luồng Shuffle thường) |
| bản tạm `Assets/_CategoryJam/Scripts/Utilities/SlotShuffle.cs` | xoá cùng `.meta` (static class, không asset nào giữ GUID) |
| `Assets/_CategoryJam/Scripts/Tutorial/Tutorial.md` | dòng Mở rộng sau về bản tạm trỏ `docs/Runtime/SlotShuffle/` |
| Plan này | xoá cùng `.meta` |
| kiểm | grep `CategoryJam.Gameplay.Utilities.SlotShuffle` và đường dẫn bản tạm ra 0 · lệnh kiểm ở dòng mở xanh |
| chơi thử | level mở khoá booster Shuffle, save mới → bàn lộn xộn, cùng seed level ra cùng bàn như bản tạm, hàng trước không có tile của box đang mở trừ khi bàn thiếu chỗ → Show me! rồi Shuffle Free: bàn về đúng như Level Editor → thoát trước Show me! rồi vào lại: bàn xáo giống hệt lần trước; hỏng: bàn không xáo, một tile sai ảnh so với Level Editor, bàn xáo khác lần trước |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | đổi chỗ thắng ưa thích | caller "dồn loại A về vùng B" nhận bàn có loại A sai vùng chỉ để mọi item nhúc nhích |
| bỏ | hơn hai nhóm | người dùng đầu cần đúng hai |
| bỏ | ràng buộc từng cặp item–slot `Func<int, int, bool>` | bài toán ghép cặp tổng quát, đắt hơn hẳn nhu cầu |
| bỏ | phân phối đều tuyệt đối trên mọi cách xếp hợp lệ | gameplay cần trông ngẫu nhiên; bước gỡ lệch nhẹ về slot đứng sau điểm bắt đầu quét |
| bỏ | trả `slotOfItem` thay `itemOfSlot` | caller ghi dữ liệu vào từng slot, đọc thẳng `data[itemOfSlot[j]]` không cần đảo mảng |
| bỏ | `new int[n]` cho mảng tạm | hàm thuần không biết caller gọi ở nhịp nào; thuê và trả `ArrayPool` chỉ thêm hai dòng |
| bỏ | xáo tại chỗ trên `itemOfSlot` | dãy slot và dãy item phải cùng có mặt lúc ghép, mảng ghi ra đánh chỉ số theo slot |
| bỏ | bỏ qua lệch độ dài | lỗi của code caller, ném ngay lần gọi đầu |
| bỏ | `SlotShuffle.Shuffle` public | class xếp slot mang thêm hàm xáo chung là hai lý do thay đổi; `FisherYates` giữ "gọi nguồn đúng n − 1 lần" một chỗ cho cả `ShuffleBag` |
| bỏ | `FisherYates.Shuffle` chặn `nextIndex` null | dãy dài từ 2 nổ `NullReferenceException` ngay lần gọi đầu; `Assign` chặn ở cửa của nó vì báo đúng tên tham số |
| bỏ | `FisherYates.Shuffle` chỉ cho `int` | thân không phụ thuộc kiểu phần tử; caller sau xáo kiểu khác không cần overload |
| bỏ | lặp bước gỡ tới khi hết đứng yên | phép đổi không tạo đứng yên mới, một lượt đủ |
| bỏ | bước gỡ chỉ đổi slot cùng dấu | slot có dấu duy nhất rơi trúng item cũ sẽ đứng yên dù có item cùng ưa thích nhận thay được (mốc `[1, 2, 0]`) |
| bỏ | Sattolo — sinh hoán vị không điểm bất động ngay từ đầu | chỉ đúng khi không có nhóm; ghép với ràng buộc nhóm thì mất bảo đảm tối đa |
| bỏ | `swap` không nhận lại `owner` | lambda phải bắt biến, cấp phát closure mỗi lần gọi |
| bỏ | không kiểm hoán vị trước khi tráo | mảng trùng phần tử làm vòng `while` không khép, game treo thay vì nổ; kiểm là một lượt `O(n)` trên cùng bộ đệm |
| bỏ | `Invert` kiểm hoán vị | chỉ số ngoài dải tự ném, đầu vào thường là kết quả `Assign` |
| bỏ | trả danh sách cặp tráo cho caller tự chạy | thêm một bộ đệm caller phải thuê, caller nào cũng viết lại vòng chạy |
| bỏ | xáo bằng `UnityEngine.Random`, seed qua `InitState` | đè trạng thái ngẫu nhiên toàn cục, lệch chuỗi của hệ khác; caller cần kết quả cố định truyền `new System.Random(seed).Next` |
| bỏ | namespace chứa chữ `Random` | code trong namespace anh em gõ `Random.` sẽ trỏ vào nó |
| sau | overload `Assign` nhận `ReadOnlySpan<int>` nhóm | khi có caller cần hơn hai nhóm; bản hai nhóm giữ |
