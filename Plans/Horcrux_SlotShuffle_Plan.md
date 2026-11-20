# Plan — `SlotShuffle` lên Horcrux

Ngày 2026-11-20. Nguồn: brainstorm tut mở khoá booster Shuffle ở category-jam. Level mở khoá Shuffle phải xáo bàn GD đã xếp thành
bàn "ngược ý đồ" (tile thuộc box đang mở rời hàng trước), ép người chơi dùng Shuffle, Shuffle trả về đúng bàn GD. Phần xếp lại không
mang domain dự án nào nên lên Horcrux: một hàm thuần xếp lại n item vào chính n slot chúng đang đứng, theo nhóm slot mỗi item ưa,
không item nào đứng yên khi tránh được, ngẫu nhiên theo nguồn caller đưa.

**Phân công:** GĐ1–2 developer gõ, agent viết test theo bảng case rồi compile · GĐ3 (tài liệu) agent làm. **Chạy được, thấy
được** ở GĐ1–2 = compile sạch + bảng case xanh trong `Tests/EditMode/SlotShuffleTests.cs`. Dự án chưa có bản tạm: caller đầu
tiên là task xáo bàn level mở khoá Shuffle của category-jam — kịch bản riêng, viết theo API của Plan này, "thấy trong game" ở
task đó. GĐ1–3 commit trong `Assets/Horcrux` kèm con trỏ submodule ở dự án. Không phụ thuộc Plan nào; `Runtime/Utilities/Permutation/`
chưa có, GĐ1 tạo. Có `§0`.

## Ngữ cảnh đã chốt

- **Người dùng:** code gameplay cần **xáo lại** một tập vật đang đứng ở các chỗ cố định (tile trên kệ, lá bài trên bàn, quân ở
  điểm spawn) sao cho mỗi vật đổi chỗ, và một loại vật dồn về một loại chỗ. Caller giữ dữ liệu của item, hàm chỉ trả chỉ số.
- **Chỗ đặt:** `Runtime/Utilities/Permutation/SlotShuffle.cs`, namespace `Horcrux.Runtime.Utilities.Permutation`. Tên
  `SlotShuffle`, `Assign`, "dấu" (`isSlotMarked`, `prefersMarked`) đổi tuỳ ý trước khi gõ. Không đặt `Random` vào namespace:
  code trong namespace anh em gõ `Random.` sẽ trỏ vào nó.
- **Mục tiêu:** một hàm thuần trên `Span<int>`, `ReadOnlySpan<bool>` và `Func<int, int>`, test không dựng gì. Ba bảo đảm theo
  thứ tự ưu tiên: ① kết quả là hoán vị · ② số item vào đúng nhóm slot ưa thích là **tối đa** · ③ item chỉ đứng yên ở slot cũ khi
  **mọi** cách xếp đạt ② đều giữ nó ở đó. Cùng input, cùng nguồn ngẫu nhiên thì cùng kết quả.
- **Nguồn ngẫu nhiên do caller đưa** (`code-design` › Bất biến): `nextIndex(n)` trả số trong `[0, n)`. Dự án cần bàn cố định
  theo level truyền `new System.Random(levelNumber).Next`; test truyền `System.Random(seed).Next`. Hàm không chạm
  `UnityEngine.Random`, không làm lệch chuỗi ngẫu nhiên của hệ khác.
- **Không làm:** nhiều hơn hai nhóm (dự án cần đúng hai; thêm overload nhận `ReadOnlySpan<int>` nhóm khi có caller, bản hai nhóm
  giữ) · ràng buộc từng cặp item–slot `Func<int, int, bool>` (bài toán ghép cặp tổng quát, đắt hơn hẳn nhu cầu) · phân phối đều
  tuyệt đối trên mọi cách xếp hợp lệ (gameplay cần "trông ngẫu nhiên", không cần đều; bước gỡ đứng yên lệch nhẹ về phía slot đứng
  sau điểm bắt đầu quét) · public hàm xáo một dãy (chưa caller; xem "Mở rộng sau").
- **Quyết định trái trực giác:** ưu thích thắng đổi chỗ. Khi chỉ có một cách đạt ② mà item phải đứng yên, item đứng yên. Đảo ưu
  tiên thì caller "dồn loại A về vùng B" nhận về bàn có loại A ở sai vùng chỉ để mọi item nhúc nhích.
- **Đã khảo sát:**

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| Horcrux `Runtime/Utilities/` | chỗ đặt, style hàm thuần (`CountdownFormatter`) | chưa có hàm xáo hay hoán vị nào |
| category-jam `Utilities/Extensions/ListExtensions.cs` `KShuffle` | Fisher–Yates | bản có seed gọi `UnityEngine.Random.InitState`: đè trạng thái ngẫu nhiên toàn cục |
| category-jam `Director/ShuffleBooster.cs` `RotateTileIdsRandomly` | ý "mỗi tile nhận id của tile khác" | hoán vị một vòng bằng đổi cặp kề nhau, không có khái niệm nhóm; đổi sang hàm này là đổi phân phối của booster đang chạy |
| `System.Buffers.ArrayPool<int>` | hai mảng tạm | — |

- **Mở rộng sau:** public `Shuffle<T>(Span<T>, Func<int, int>)` cạnh `Assign` khi dự án bỏ `KShuffle(seed)` (6 call site ở
  category-jam, mỗi lần gọi reseed `UnityEngine.Random` toàn cục) · overload nhiều nhóm như ở "Không làm".

## Luồng mô tả — xáo một bàn

```
caller có n item, item i đứng ở slot i; mỗi slot có dấu hay không; mỗi item ưa slot có dấu hay không
                              ▼
      xếp slot: có dấu trước, không dấu sau, xáo từng nhóm
      xếp item: ưa có dấu trước, ưa không dấu sau, xáo từng nhóm
      ghép theo chỉ số → số item đúng nhóm tối đa; phần tràn chọn ngẫu nhiên                 GĐ1
                              ▼
      slot còn giữ item cũ đổi với một slot cùng dấu, hoặc đang chứa item cùng ưa thích     GĐ2
                              ▼
      itemOfSlot[j] = item slot j nhận → caller ghi dữ liệu item vào slot
```

## Bản đồ hệ cuối

```
  caller (một lần mỗi lần xáo)
     │ itemOfSlot (n ô ghi ra), isSlotMarked, prefersMarked, nextIndex
     ▼
  SlotShuffle.Assign
     ├─ FillMarkedFirstShuffled ×2 ──► Shuffle (Fisher–Yates, nextIndex)
     ├─ ghép theo chỉ số
     └─ MoveItemsOffOwnSlot ─────────► nextIndex (một lần mỗi slot còn giữ item cũ)
     ▼
  itemOfSlot ──► caller: slot j nhận dữ liệu của item itemOfSlot[j]
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

**Kiểm chứng:** bản Python cùng thuật toán, vét cạn `n ≤ 6`, mọi tổ hợp dấu × ưa thích, 20 seed mỗi tổ hợp (109 220 lượt): ① ② ③
đúng ở mọi lượt, với ③ so bằng cách liệt kê mọi hoán vị đạt trần. Đối chứng: bỏ bước gỡ thì ca `[1, 2, 0]` bên dưới sai ở 144/200 seed.

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| dấu `[T, F, F]`, ưa `[T, T, F]` | luôn `[1, 2, 0]`: slot 0 phải nhận item ưa có dấu mà không phải item 0; item 2 không về slot 2 | cứu bằng item cùng ưa thích |
| dấu `[T, F, F]`, ưa `[T, F, F]` | luôn `[0, 2, 1]`: item 0 buộc đứng yên, hai item còn lại đổi nhau | đứng yên bắt buộc |
| `n` 1 | `[0]` | đứng yên bắt buộc |
| dấu 5/8, ưa 3/8 | đúng nhóm `3 + 3 = 6` | `a < b` |
| dấu 2/8, ưa 6/8 | đúng nhóm `2 + 2 = 4` | `a > b` |

---

## Giai đoạn 1 — Ghép đạt số đúng nhóm tối đa

**Mục tiêu:** caller đưa dấu slot, ưa thích của item và nguồn ngẫu nhiên, nhận một hoán vị đạt số đúng nhóm tối đa. Xong giai đoạn
này caller "dồn loại A về vùng B" đã dùng được; item có thể còn đứng yên.

**Vì sao bây giờ:** ② là bảo đảm caller mua hàm này để có; bước gỡ đứng yên ở GĐ2 dựa trên một hoán vị đã đạt ②.

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

            Shuffle(order.Slice(0, markedCount), nextIndex);
            Shuffle(order.Slice(markedCount), nextIndex);
        }

        /// <summary>Fisher–Yates từ cuối về đầu.</summary>
        private static void Shuffle(Span<int> values, Func<int, int> nextIndex)
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

**Quyết định** — trả `itemOfSlot` (slot nhận item nào), không `slotOfItem`: caller ghi dữ liệu vào từng slot, đọc thẳng
`data[itemOfSlot[j]]` không cần đảo mảng. — Mảng tạm thuê `ArrayPool`, không `new int[n]`: hàm thuần không biết caller gọi ở nhịp nào;
thuê và trả chỉ thêm hai dòng. — Lệch độ dài ném `ArgumentException`: lỗi của code caller, nổ ngay lần gọi đầu (`code-design` ›
Guard). — `count == 0` thoát trước khi thuê mảng: không cần lần mượn–trả nào cho bàn rỗng. — Không xáo tại chỗ trên `itemOfSlot`: dãy slot và dãy item phải cùng có mặt lúc ghép, mảng ghi ra đánh chỉ số theo slot.

| Consumes | Produces |
|---|---|
| `System.Buffers.ArrayPool<int>`, `nextIndex` của caller | `SlotShuffle.Assign` |

**Sơ đồ tương tác sau GĐ1** (`+` là phần mới)

```
  caller ──itemOfSlot, isSlotMarked, prefersMarked, nextIndex──► SlotShuffle.Assign+ ──► FillMarkedFirstShuffled+ ×2 ──► Shuffle+
                                                                        │ ghép theo chỉ số
                                                                        ▼
                                                                    itemOfSlot ──► caller
```

**Editor setup:** không — hàm thuần, caller gọi bằng code.

**Kiểm:** compile sạch. Test đếm "đúng nhóm" bằng `prefersMarked[itemOfSlot[j]] == isSlotMarked[j]`; nguồn ngẫu nhiên
`new System.Random(seed).Next`.

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
| không cấp phát ở lần gọi thứ hai | `n` 8, gọi một lần làm nóng pool | `Is.Not.AllocatingGCMemory()` (nguồn `nextIndex` dựng trước) |

**Mốc commit:** `feat(permutation): SlotShuffle.Assign`.

---

## Giai đoạn 2 — Không item nào đứng yên khi tránh được

**Mục tiêu:** item chỉ còn ở slot cũ khi mọi cách xếp đạt tối đa đều buộc vậy. Xong giai đoạn này bàn xáo trông khác hẳn bàn gốc kể
cả ở vùng không dính tới dấu.

**Vì sao bây giờ:** cần hoán vị đã đạt tối đa của GĐ1 làm nền: bước gỡ chỉ đổi những cặp giữ nguyên số đúng nhóm (`§0` ②).

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

**Quyết định** — một lượt quét, không lặp tới khi hết đứng yên: phép đổi không tạo đứng yên mới (`§0` ②). — Nhận cả nhánh "cùng ưa
thích", không chỉ "cùng dấu": thiếu nhánh này thì slot có dấu duy nhất rơi trúng item cũ của nó sẽ đứng yên dù có item khác cùng ưa
thích nhận thay được (mốc `[1, 2, 0]`). — Phương án bỏ: thuật toán sinh hoán vị không điểm bất động ngay từ đầu (Sattolo) — chỉ đúng
khi không có nhóm; ghép với ràng buộc nhóm thì mất bảo đảm tối đa.

| Consumes | Produces |
|---|---|
| GĐ1 (`itemOfSlot` đạt tối đa) | `MoveItemsOffOwnSlot` (private) |

**Sơ đồ tương tác sau GĐ2**

```
  caller ──► SlotShuffle.Assign ──► FillMarkedFirstShuffled ×2 ──► ghép ──► MoveItemsOffOwnSlot+ ──► itemOfSlot ──► caller
```

**Editor setup:** không.

**Kiểm:** compile sạch; bảng GĐ1 vẫn xanh (số đúng nhóm không đổi qua bước gỡ).

| Case | Input | Kỳ vọng |
|---|---|---|
| ai cũng rời chỗ | dấu = ưa = `[T×3, F×5]`, 500 seed | không `j` nào `itemOfSlot[j] == j`. Không dùng mảng ngẫu nhiên: mảng có đúng một `T` (hay một `F`) rơi vào ca đứng yên bắt buộc |
| cứu bằng item cùng ưa thích | dấu `[T, F, F]`, ưa `[T, T, F]`, 200 seed | luôn `[1, 2, 0]` |
| đứng yên bắt buộc | dấu `[T, F, F]`, ưa `[T, F, F]`, 200 seed | luôn `[0, 2, 1]` |
| một phần tử | `n` 1 | `[0]` |
| mọi slot có dấu, mọi item ưa có dấu | `n` 5, 200 seed | không đứng yên; đúng nhóm 5 |
| vét cạn | `n` 0–6, mọi tổ hợp dấu × ưa, 20 seed mỗi tổ hợp | hoán vị · đúng nhóm = trần `§0` · tập slot đứng yên = tập slot mà **mọi** hoán vị đạt trần đều giữ item cũ (liệt kê mọi hoán vị) |

**Mốc commit:** `feat(permutation): SlotShuffle gỡ item đứng yên`.

---

## Giai đoạn 3 — Tài liệu `docs/Runtime/SlotShuffle/SlotShuffle.md` và `SlotShuffle.html` (agent)

`.md` mở đầu `Code: Runtime/Utilities/Permutation`. Mục: ba bảo đảm và thứ tự ưu tiên · `§0` ở trên (trần, ghép theo chỉ số, bước
gỡ, ca đứng yên bắt buộc, bảng mốc) · bảng API `Assign` · "Design decisions" gom "Không làm", "Quyết định trái trực giác" và các
**Quyết định** của GĐ1–2 · "Dùng": caller giữ dữ liệu item theo chỉ số slot gốc, ghi `data[itemOfSlot[j]]` vào slot `j`; muốn trả
về bàn gốc thì giữ bản dữ liệu gốc và ghi lại · nguồn ngẫu nhiên: `System.Random(seed).Next` cho kết quả cố định, không dùng
`UnityEngine.Random.InitState`. Ví dụ tên trung tính (`Piece`, `Spot`). Không có "Trước khi chạy": hàm thuần, không wire. `.html`
chọn từ `.md`: kịch bản (dồn vật đặc biệt vào vùng khuất; xáo lại điểm spawn mỗi đợt, quân mạnh ra xa; chia lại bài, lá chủ chốt
úp xuống), cách dùng, flow ghép theo chỉ số, lưu ý (ưa thích thắng đổi chỗ; độ dài ba mảng; seed cố định).

**Mốc commit:** `docs(permutation): SlotShuffle.md`.

---

## Dùng ở category-jam (task riêng, viết theo API này)

Kịch bản xáo bàn level mở khoá Shuffle chốt ở task đó; phần chạm hàm này dự kiến:

| Ô | Giá trị |
|---|---|
| slot | tile đang thấy mà Shuffle đổi được (hàng trước + hàng sau, tầng ≤ 6, có ID), theo một thứ tự cố định |
| item `i` | cặp (ID, variant) gốc của slot `i` |
| `isSlotMarked[j]` | slot `j` ở hàng sau, hoặc là tile "?" ở hàng trước — chỗ người chơi không nhặt được ID vào box |
| `prefersMarked[i]` | ID của item `i` thuộc một box đang mở |
| `nextIndex` | `new System.Random(levelNumber).Next` — bàn lộn xộn cố định theo level |

Trả về bàn GD: ghi lại (ID, variant) gốc đã giữ, không cần hoán vị ngược.

## File → giai đoạn

| File | GĐ1 | GĐ2 | GĐ3 |
|---|---|---|---|
| `Runtime/Utilities/Permutation/SlotShuffle.cs` | tạo, `Assign`, `FillMarkedFirstShuffled`, `Shuffle` | `MoveItemsOffOwnSlot`, gọi trong `Assign`, sửa summary | |
| `Tests/EditMode/SlotShuffleTests.cs` (agent) | tạo | thêm case | |
| `docs/Runtime/SlotShuffle/SlotShuffle.md`, `.html` (agent) | | | tạo |
