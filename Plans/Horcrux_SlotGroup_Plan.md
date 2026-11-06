# Plan — `Slot<TOccupant>` và `SlotGroup<TSlot, TOccupant>` lên Horcrux

Ngày 2026-11-06. Nguồn: category-jam, `Assets/_CategoryJam/Scripts/Entities/TileSlot/TileSlot.cs` và
`Entities/TileSlotGroup.cs`, đã chạy với ba chủ thật: `Box` (ô đáp theo sức chứa), `CacheTileSlots` (khay ô mở dần), `ShoppingCart`
(xe đẩy). Cơ chế đặt chỗ → đáp → rời với hai hook, và nhóm đếm lại theo sự kiện; phần gọi tên tile (`TileScaleMode`,
`TargetTileLocalScale`, `LandedState`, `PlayPoofVFX`, `View.SetDrawState`) ở lại dự án trong class con.

**Phân công:** giai đoạn 1–4 (Horcrux, code runtime) developer gõ, sau mỗi giai đoạn agent viết test của giai đoạn đó theo bảng
case rồi compile · giai đoạn 5 (tài liệu) và giai đoạn 6 (dự án chuyển sang bản Horcrux) agent làm.

**Chạy được, thấy được ở giai đoạn 1–4** = `com.horcrux.runtime` compile sạch và bảng case của giai đoạn xanh trong
`Tests/EditMode/SlotGroupTests.cs`; Horcrux không có host nên "thấy trong game" là giai đoạn 6. Horcrux là submodule: giai đoạn
1–5 commit **trong `Assets/Horcrux`**, giai đoạn 6 commit ở repo dự án kèm con trỏ submodule.

Không phụ thuộc Plan nào. Không có lõi toán, không có `§0`.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | mọi game xếp vật vào ô: vật đặt chỗ lúc cất cánh, chiếm ô lúc đáp, rời ô thì trả; chủ ô cần đếm ô đã chiếm và tìm ô trống |
| Mục tiêu | hai class generic giữ **bất biến** của ô và nhóm: một ô một vật · đặt chỗ không tính là chiếm · trạng thái ô suy từ hai tham chiếu, không giữ riêng · số ô chiếm của nhóm luôn bằng số ô `State == Occupied` sau mỗi sự kiện |
| Chỗ đặt | `Runtime/Utilities/Slots/Slot.cs`, `Runtime/Utilities/Slots/SlotGroup.cs`, namespace `Horcrux.Runtime.Utilities.Slots`; không gọi tên type dự án |
| Tên | `Slot<TOccupant>`, `SlotState`, `SlotGroup<TSlot, TOccupant>`; dự án: `TileSlot : Slot<Tile>`. Developer đổi tuỳ ý trước khi gõ |
| Ràng buộc generic | `TOccupant : class` — chữ ký là thứ sửa sau đắt; `class` để `null` là "trống" không cần `Nullable`, so bằng tham chiếu |
| Sự kiện | `Changed` mang **ô**, người nghe đọc `State`; không mang loại thay đổi — ba chủ hiện có đều hỏi `State == Occupied` |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| `TargetTileLocalScale`, `ScaleMode`, `LandedState`, `PlayPoofVFX` trong base | từ vựng của tile 2.5D; dự án khai ở `TileSlot : Slot<Tile>`. Framework giữ cơ chế, dự án giữ từ vựng |
| `tile.View.SetDrawState(Landed)` trong `Land` | gọi tên view dự án; `TileSlot` của dự án seal `OnLanded` để gọi nó rồi mở hook `OnTileLanded` cho hai ô con (giai đoạn 6) |
| `SlotGroup` là `MonoBehaviour` | nhóm không có đời sống riêng: chủ `new` nó, `Attach` khi số ô đổi, `ResetAll` khi reset; không cần mốc tắt |
| Interface `ISlot` | một implementation; base class đủ cho hook `protected virtual` |
| `FindFreeSlot(predicate)` | chủ lọc theo luật riêng (`Box` so id trước khi hỏi nhóm; khay bỏ ô khoá) ở bên ngoài, nhóm chỉ biết "trống" |
| Đếm "đang bay tới" (`ReservedCount`) | khay tự đếm `ReservedBy != null` trên `Slots` khi cần; thêm vào nhóm khi có chủ thứ hai hỏi |

**Quyết định trái trực giác** — lý do viết ở giai đoạn sinh ra nó: `State` suy từ hai tham chiếu, đáp thắng đặt chỗ (GĐ2) ·
`Reserve` không phát `Changed` (GĐ1) · `IsFree` kiểm `activeSelf` (GĐ1) · `Vacate` phát `Changed` kể cả khi ô chỉ đặt chỗ (GĐ3) ·
`Clear` im lặng (GĐ4) · `Attach` gọi lại được (GĐ4).

**Mở rộng sau**

- Chủ cần biết "vừa đặt chỗ": `Reserve` phát `Changed`, người nghe đọc `State == Reserved`; nhóm đếm lại vẫn đúng.

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| `TileSlot.cs`, `TileSlotGroup.cs` của dự án (bản 2026-11-05) | toàn bộ cơ chế: `State` suy từ `OccupiedBy`/`ReservedBy`, hai hook, nhóm đếm lại | field và property mang domain tile (bảng "Cố ý KHÔNG làm") |
| `Tile.cs` (`FlyToAsync`, `FlyToCartAsync`, `ShiftLeftInCache`, `LeaveSlot`) | thứ tự gọi `Reserve` → `Land`, `Vacate` lúc rời | — |
| `Box.cs`, `CacheTileSlots.cs`, `ShoppingCart.cs` | ba cách gọi `Attach`, `FindFreeSlot`, `ResetAll`, `FreeCount`, `OccupiedCount`, `AreAllOccupied`, `Slots`; ba handler `Changed(TileSlot)` đọc `State` | — |
| Horcrux `Utilities/Common/DeferredSet.cs` | — | tập không trùng có tombstone, không có "ô" và "vật" |
| Horcrux `Pooling` | — | cấp phát object, không giữ quan hệ ô–vật |

---

## Luồng mô tả — một vật đi qua ô

```
chủ (Box, khay, xe) dựng bộ ô ──► hỏi ô trống ──► vật đặt chỗ ô lúc cất cánh      GĐ1
                                                       │
                                                       ▼
                                   vật đáp: hook trước → chiếm → xoá đặt chỗ → hook sau
                                   nhóm đếm lại, báo chủ; chủ hỏi "đầy chưa"          GĐ2
                                                       │
                                                       ▼
                                   vật rời (bay đi, về pool): ô trả, nhóm đếm lại     GĐ3
                                                       │
                                                       ▼
                                   chủ đổi số ô giữa level · reset cả level           GĐ4
                                                       │
                                                       ▼
                                   tài liệu module (GĐ5) · dự án dùng bản Horcrux (GĐ6)
```

## Bản đồ hệ cuối

```
                 Reserve / Land / Vacate                  Attach / FindFreeSlot / ResetAll
  vật (TOccupant) ─────────────────────► Slot<TOccupant> ◄──────────────────────────── chủ
                                           │  ▲                                          ▲
                             Changed(slot) │  │ Changed += / -=                          │ Changed(slot)
                                           ▼  │                                          │ Slots, FreeCount,
                                  SlotGroup<TSlot, TOccupant> ───────────────────────────┘ OccupiedCount, AreAllOccupied
                                  (đếm lại OccupiedCount mỗi lần ô báo)

  class con của dự án : Slot<Vật>  — override OnBeforeLand, OnLanded, Clear; khai từ vựng riêng
```

Ai sở hữu gì: ô giữ `ReservedBy`, `OccupiedBy` và **suy** `State`; nhóm giữ `Slots`, `OccupiedCount` (cache, dựng lại từ `State` mỗi
sự kiện); chủ không giữ bản sao nào.

---

## Giai đoạn 1 — Bộ ô, ô trống, đặt chỗ lúc cất cánh

**Mục tiêu:** chủ `Attach` một mảng ô, hỏi `FindFreeSlot`/`FreeCount`; vật `Reserve` ô thì ô hết trống. Xong giai đoạn này hệ
trả lời được "còn chỗ không" và giữ chỗ cho chuyến bay.

**Vì sao bây giờ:** đây là câu hỏi đầu tiên của mọi chủ trước khi cho vật bay; chưa cần sự kiện vì chưa có gì đổi mà chủ phải
phản ứng.

### `Runtime/Utilities/Slots/Slot.cs` — file mới

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Slots
{
    /// <summary>Trạng thái một ô: trống, có vật đang bay tới giữ chỗ, hay đang chứa vật đã đáp.</summary>
    public enum SlotState
    {
        Empty,
        Reserved,
    }

    /// <summary>
    /// Một ô giữ tối đa một vật: đặt chỗ lúc vật cất cánh, chiếm lúc đáp, trả lúc rời. Loại ô cụ thể chen việc riêng qua
    /// hook, không đụng bất biến.
    /// </summary>
    public class Slot<TOccupant> : MonoBehaviour where TOccupant : class
    {
        #region Properties

        public TOccupant ReservedBy { get; private set; }

        /// <summary>Suy từ tham chiếu, không giữ riêng: không có cờ nào lệch với tham chiếu.</summary>
        public SlotState State => ReservedBy != null ? SlotState.Reserved : SlotState.Empty;

        /// <summary>Ô đang bật và trống. Ô tắt (khay chưa mở, box thu sức chứa) không nhận vật.</summary>
        public bool IsFree => gameObject.activeSelf && State == SlotState.Empty;

        #endregion Properties

        #region API

        /// <summary>Không phát sự kiện: đặt chỗ là trạng thái tạm của chuyến bay, chủ chỉ phản ứng lúc đáp và rời.</summary>
        public void Reserve(TOccupant occupant) => ReservedBy = occupant;

        #endregion API
    }
}
```

### `Runtime/Utilities/Slots/SlotGroup.cs` — file mới

```csharp
using System;

namespace Horcrux.Runtime.Utilities.Slots
{
    /// <summary>Bộ ô của một chủ: tìm ô trống, đếm; chủ new nó và Attach mảng ô của mình.</summary>
    public class SlotGroup<TSlot, TOccupant>
        where TSlot : Slot<TOccupant>
        where TOccupant : class
    {
        #region Properties

        public TSlot[] Slots { get; private set; } = Array.Empty<TSlot>();

        public int FreeCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < Slots.Length; i++)
                    if (Slots[i].IsFree)
                        count++;
                return count;
            }
        }

        #endregion Properties

        #region API

        public void Attach(TSlot[] slots) => Slots = slots;

        public TSlot FindFreeSlot()
        {
            for (var i = 0; i < Slots.Length; i++)
                if (Slots[i].IsFree)
                    return Slots[i];
            return null;
        }

        #endregion API
    }
}
```

**Khác với bản dự án:** `IsFree` bỏ `gameObject != null` — `this` là component đang được gọi; `gameObject` null chỉ sau `Destroy`,
khi đó mọi truy cập khác cũng ném.

**Quyết định** — `State` là property suy ra, không phải field: một sự thật một chủ; cờ riêng là bản sao thứ hai phải giữ khớp.
Phương án bỏ: `enum` field ghi ở `Reserve`/`Land` — thêm một cửa ghi cho mỗi chuyển trạng thái.

| Consumes | Produces |
|---|---|
| `UnityEngine.MonoBehaviour` (ô là component gắn trong prefab của chủ) | `Slot<T>.Reserve`, `ReservedBy`, `State`, `IsFree` · `SlotGroup.Attach`, `Slots`, `FindFreeSlot`, `FreeCount` |

**Sơ đồ tương tác sau GĐ1** (`+` là phần mới)

```
  vật ──Reserve──► Slot+ ◄──IsFree── SlotGroup+ ◄──Attach / FindFreeSlot / FreeCount── chủ
```

**Editor setup:** không — ô là component của dự án, gắn sẵn trong prefab chủ.

**Kiểm:** compile sạch. Test khai `TestSlot : Slot<TestOccupant>` (Unity không gắn `MonoBehaviour` generic), dựng bằng
`new GameObject().AddComponent<TestSlot>()`; `TestOccupant` là class rỗng.

| Case | Input | Kỳ vọng |
|---|---|---|
| ô mới | — | `IsFree` true, `State` Empty, `ReservedBy` null |
| đặt chỗ | `Reserve(a)` | `IsFree` false, `State` Reserved |
| ô tắt | `gameObject.SetActive(false)` | `IsFree` false dù không ai giữ |
| tìm ô trống | 3 ô, ô 0 đặt chỗ | `FindFreeSlot` trả ô 1; `FreeCount` 2 |
| hết chỗ | cả ba đặt chỗ | `FindFreeSlot` null, `FreeCount` 0 |
| mảng rỗng | `Attach(Array.Empty)` | `FindFreeSlot` null, `FreeCount` 0 |

**Mốc commit:** `feat(slots): Slot đặt chỗ, SlotGroup tìm ô trống`.

---

## Giai đoạn 2 — Vật đáp: chiếm ô, nhóm đếm lại, chủ biết đầy

**Mục tiêu:** `Land` chiếm ô theo thứ tự cố định (hook trước → chiếm → xoá đặt chỗ → hook sau), ô phát `Changed`; nhóm nghe mọi
ô, đếm lại `OccupiedCount`, phát `Changed` lên chủ; chủ hỏi `AreAllOccupied`. Xong giai đoạn này một box đầy biết mình đầy.

**Vì sao bây giờ:** đáp là sự kiện đầu tiên chủ phải phản ứng (tiếng, nhún, đóng nắp); sự kiện và bộ đếm sinh ra cùng lúc với
lý do tồn tại của chúng.

### `Slot.cs` › enum `SlotState` › thêm sau `Reserved`

```csharp
        Reserved,
        Occupied,
```

### `Slot.cs` › `Properties` › thêm sau `ReservedBy`, thay thân `State`

```csharp
        public TOccupant ReservedBy { get; private set; }
        public TOccupant OccupiedBy { get; private set; }

        /// <summary>Phát khi vật đáp và khi vật rời; đặt chỗ không phát. Người nghe đọc <see cref="State"/>.</summary>
        public event Action<Slot<TOccupant>> Changed;

        /// <summary>Suy từ hai tham chiếu, không giữ riêng: vật đã đáp thắng vật đang giữ chỗ.</summary>
        public SlotState State => OccupiedBy != null ? SlotState.Occupied
            : ReservedBy != null ? SlotState.Reserved
            : SlotState.Empty;
```

(thêm `using System;`)

### `Slot.cs` › `API` › thêm sau `Reserve`, trước `#endregion API`

```csharp
        /// <summary>Vật vừa đáp. Bất biến chỉ có một bản ở đây: hook trước, chiếm, xoá đặt chỗ, hook sau.</summary>
        public void Land(TOccupant occupant)
        {
            OnBeforeLand(occupant);
            Occupy(occupant);
            ReservedBy = null;
            OnLanded(occupant);
        }

        /// <summary>Trước khi chiếm ô: đặt vật vào cây của ô, tắt phần vẽ không cần.</summary>
        protected virtual void OnBeforeLand(TOccupant occupant) { }

        /// <summary>Sau khi đã chiếm ô và xoá đặt chỗ: hiệu ứng, trạng thái riêng của loại ô.</summary>
        protected virtual void OnLanded(TOccupant occupant) { }
```

### `Slot.cs` › thêm region `Class Methods` sau `#endregion API`

```csharp
        #region Class Methods

        // Phát Changed khi ReservedBy còn giữ: State đã là Occupied vì vật đã đáp thắng.
        private void Occupy(TOccupant occupant)
        {
            OccupiedBy = occupant;
            Changed?.Invoke(this);
        }

        #endregion Class Methods
```

### `SlotGroup.cs` › `Properties` › thêm sau `Slots`

```csharp
        public TSlot[] Slots { get; private set; } = Array.Empty<TSlot>();
        public int OccupiedCount { get; private set; }

        public event Action<Slot<TOccupant>> Changed;
```

và sau `FreeCount`:

```csharp
        public bool AreAllOccupied => OccupiedCount == Slots.Length;
```

### `SlotGroup.cs` › `API` › `Attach` — đoạn cũ → đoạn mới

Cũ:

```csharp
        public void Attach(TSlot[] slots) => Slots = slots;
```

Mới — nhóm phải nghe từng ô trước khi ô đầu tiên đáp:

```csharp
        public void Attach(TSlot[] slots)
        {
            foreach (var slot in slots)
                slot.Changed += HandleSlotChanged;

            Slots = slots;
            RecountOccupied();
        }
```

### `SlotGroup.cs` › thêm region `Class Methods` sau `API`

```csharp
        #region Class Methods

        private void HandleSlotChanged(Slot<TOccupant> slot)
        {
            RecountOccupied();
            Changed?.Invoke(slot);
        }

        private void RecountOccupied()
        {
            OccupiedCount = 0;
            foreach (var slot in Slots)
                if (slot.State == SlotState.Occupied)
                    OccupiedCount++;
        }

        #endregion Class Methods
```

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| `Land` | `tile.View.SetDrawState(Landed)` giữa `ReservedBy = null` và `OnLanded` | không có dòng đó | gọi tên view dự án; `TileSlot` seal `OnLanded` để gọi rồi hook `OnTileLanded` (GĐ6). Thứ tự không đổi |
| `Changed` | `Action<TileSlot>` | `Action<Slot<TOccupant>>` | ô không biết `TSlot`; ba handler của chủ chỉ đọc `State` nên không cần cast |

**Quyết định** — nhóm **đếm lại từ đầu** ở mỗi sự kiện, không cộng trừ theo loại thay đổi: `OccupiedCount` là cache dựng lại từ
`State`, không thể lệch; giá là O(n) mỗi lần đáp với n cỡ chục. Phương án bỏ: sự kiện mang `Linked/Unlinked` và nhóm `±1` —
`Vacate` thừa (GĐ3) sẽ làm lệch số. — `Changed` ở nhóm đồng bộ, một người nghe là chủ; chủ cần mốc khác (đặt chỗ) thì thêm sự
kiện, không thêm cờ.

| Consumes | Produces |
|---|---|
| GĐ1 | `Slot.Land`, `OccupiedBy`, `Changed`, hook `OnBeforeLand`/`OnLanded` · `SlotGroup.OccupiedCount`, `AreAllOccupied`, `Changed` |

**Sơ đồ tương tác sau GĐ2**

```
  vật ──Reserve / Land+──► Slot ──Changed+──► SlotGroup ──Changed+──► chủ
         (hook trước+ → chiếm+ → xoá đặt chỗ → hook sau+)   (đếm lại+)      hỏi AreAllOccupied+
```

**Editor setup:** không.

**Kiểm:** compile sạch; `TestSlot` ghi lại thứ tự hook vào một `List<string>`.

| Case | Input | Kỳ vọng |
|---|---|---|
| đáp | `Reserve(a)` rồi `Land(a)` | thứ tự `OnBeforeLand` → `Changed` → `OnLanded`; lúc `Changed` phát `State` Occupied; lúc `OnLanded` chạy `ReservedBy` null |
| đáp không đặt chỗ trước | `Land(a)` | `State` Occupied, `Changed` một lần |
| đáp thắng đặt chỗ | `Reserve(a)`, `Land(b)` | `State` Occupied ngay trong handler `Changed`; sau `Land` `ReservedBy` null |
| nhóm đếm | 3 ô, `Land` 2 | `OccupiedCount` 2, `FreeCount` 1, `AreAllOccupied` false; `Changed` nhóm phát 2 lần, mang đúng ô |
| đủ | `Land` cả 3 | `AreAllOccupied` true |
| mảng rỗng | `Attach(Array.Empty)` | `AreAllOccupied` true (0 == 0) — ghi ở tài liệu là ca chủ phải tự chặn |

**Mốc commit:** `feat(slots): Land với hai hook, nhóm đếm lại và báo chủ`.

---

## Giai đoạn 3 — Vật rời ô

**Mục tiêu:** `Vacate` trả ô và phát `Changed`; nhóm đếm lại. Xong giai đoạn này một box, khay, xe sống qua cả vòng vật vào và ra
(nam châm hút tile khỏi box, tile cache dồn trái, tile về pool).

**Vì sao bây giờ:** rời là đường ra duy nhất còn thiếu của một ô đang chiếm; không có nó ô chỉ đầy dần.

### `Slot.cs` › `API` › thêm sau `Reserve`, trước `Land`

```csharp
        public void Reserve(TOccupant occupant) => ReservedBy = occupant;

        /// <summary>Vật rời ô (bay đi, về pool). Phát Changed kể cả khi ô chỉ mới đặt chỗ: nhóm đếm lại từ đầu nên lời gọi thừa không lệch số.</summary>
        public void Vacate()
        {
            OccupiedBy = null;
            Changed?.Invoke(this);
        }
```

**Quyết định** — `Vacate` không xoá `ReservedBy`: vật rời ô là vật đã đáp; vật khác đang bay tới ô này vẫn giữ chỗ. Vật về pool
giữa chuyến bay chỉ xảy ra lúc dispose level, nơi chủ `ResetAll` ngay sau (GĐ4); vật không `Vacate` ô mình chưa chiếm. — Phát `Changed` cả khi ô chỉ đặt chỗ: rẻ hơn mọi kỷ
luật "chỉ `Vacate` khi đã `Land`" ở phía vật; người nghe đọc `State`, thấy `Empty` hay `Reserved` đều là "không chiếm".

| Consumes | Produces |
|---|---|
| GĐ2 | `Slot.Vacate` |

**Sơ đồ tương tác sau GĐ3**

```
  vật ──Reserve / Land / Vacate+──► Slot ──Changed──► SlotGroup (đếm lại) ──Changed──► chủ
```

**Editor setup:** không.

**Kiểm:** compile sạch.

| Case | Input | Kỳ vọng |
|---|---|---|
| rời | `Land(a)` rồi `Vacate()` | `Changed` phát, `State` Empty, `IsFree` true |
| Vacate thừa | `Reserve(a)` rồi `Vacate()` | `Changed` phát, `State` vẫn Reserved, `OccupiedCount` của nhóm 0 |
| rời rồi đáp lại | `Land(a)`, `Vacate()`, `Land(b)` | `OccupiedBy` b, nhóm đếm 1 |
| ra vào nhiều lần | 3 ô, `Land` 3, `Vacate` 2, `Land` 1 | `OccupiedCount` 2, `AreAllOccupied` false |

**Mốc commit:** `feat(slots): Vacate`.

---

## Giai đoạn 4 — Chủ đổi số ô giữa level, reset cả level

**Mục tiêu:** `Attach` gọi lại được với mảng ô mới (box đổi sức chứa khi lấy từ pool, xe đẩy sinh thêm ô): ô đã đăng ký không
đăng ký lần hai, ô rời bộ gỡ đăng ký, đếm lại một lần. `ResetAll` đưa cả nhóm về trống im lặng; `Clear` là `virtual` để loại ô
xoá vết riêng (alpha, scale, tween).

**Vì sao bây giờ:** ba chủ thật đều dùng một pool cho nhiều cỡ, nên số ô đổi giữa các lần lấy ra; và level dispose phải xoá sạch
không bắn n sự kiện. Đây là chặng cuối của vòng đời chủ.

### `SlotGroup.cs` › `API` › `Attach` — đoạn cũ → đoạn mới

Cũ:

```csharp
        public void Attach(TSlot[] slots)
        {
            foreach (var slot in slots)
                slot.Changed += HandleSlotChanged;

            Slots = slots;
            RecountOccupied();
        }
```

Mới — so từng ô với bộ cũ; `Array.IndexOf` vì n cỡ chục và chỉ chạy lúc đổi cỡ:

```csharp
        /// <summary>Gọi lại được sau khi chủ thêm hay bớt ô: ô đã đăng ký không đăng ký lần hai, ô rời bộ gỡ đăng ký.</summary>
        public void Attach(TSlot[] slots)
        {
            foreach (var slot in Slots)
            {
                if (Array.IndexOf(slots, slot) >= 0) continue;
                slot.Changed -= HandleSlotChanged;
            }

            foreach (var slot in slots)
            {
                if (Array.IndexOf(Slots, slot) >= 0) continue;
                slot.Changed += HandleSlotChanged;
            }

            Slots = slots;
            RecountOccupied();
        }
```

### `SlotGroup.cs` › `API` › thêm sau `FindFreeSlot`

```csharp
        /// <summary>Clear từng ô (im lặng) rồi OccupiedCount về 0; chỉ khi dispose hoặc reset cả level.</summary>
        public void ResetAll()
        {
            foreach (var slot in Slots)
                slot.Clear();
            OccupiedCount = 0;
        }
```

### `Slot.cs` › `API` › thêm sau `Land`, trước hai hook

```csharp
        /// <summary>Xoá im lặng, không phát Changed; chỉ hợp lệ trong ResetAll của nhóm, nơi đếm về 0 sau đó. Loại ô override để xoá vết riêng, gọi base.</summary>
        public virtual void Clear()
        {
            OccupiedBy = null;
            ReservedBy = null;
        }
```

**Quyết định** — `Clear` im lặng: phát `Changed` ở đây là n lần đếm lại và n lần chủ phản ứng (tiếng, nhún) cho một lần reset; cửa
ghi `OccupiedCount = 0` nằm cùng `ResetAll`. Phương án bỏ: `Vacate` từng ô — bắn hiệu ứng rời cho ô đang dispose. — `Attach` gỡ
đăng ký ô rời bộ: ô box bị tắt khi thu sức chứa vẫn sống trong pool; không gỡ thì lần sau bật lại nó báo vào nhóm hai lần.

| Consumes | Produces |
|---|---|
| GĐ3 | `SlotGroup.ResetAll`, `Attach` tái nhập · `Slot.Clear` virtual |

**Sơ đồ tương tác sau GĐ4**

```
  chủ ──Attach (lại)+ / ResetAll+──► SlotGroup ──Clear+──► Slot (im lặng)
                                         │ -= ô rời, += ô mới, đếm lại+
  class con ── override Clear+ ── xoá vết riêng rồi base.Clear()
```

**Editor setup:** không.

**Kiểm:** compile sạch.

| Case | Input | Kỳ vọng |
|---|---|---|
| `Attach` lại với 2 ô cũ + 1 ô mới | `Land` ô bị bỏ sau đó | `OccupiedCount` không đổi (đã gỡ); `Land` ô mới thì đổi |
| `Attach` lần hai cùng mảng | `Land` một ô | `Changed` nhóm phát **một** lần |
| `Attach` lại khi đã có ô chiếm | 3 ô `Land` 2, `Attach` 2 ô trong đó có 1 ô chiếm | `OccupiedCount` 1 ngay sau `Attach`, không sự kiện |
| `ResetAll` | sau `Land` 2 | `OccupiedCount` 0, không `Changed` nào, mọi ô `IsFree` |
| `Clear` override | `TestSlot.Clear` ghi cờ | cờ bật cho từng ô, `base` đã chạy (`ReservedBy` null) |

**Mốc commit:** `feat(slots): Attach tái nhập, ResetAll, Clear virtual`.

---

## Giai đoạn 5 — Tài liệu `docs/Runtime/SlotGroup/SlotGroup.md` và `SlotGroup.html` (agent)

`.md` mở đầu `Code: Runtime/Utilities/Slots`. Mục: vòng đời một vật qua ô (luồng mô tả ở trên, ASCII) · bảng API hai class ·
"Design decisions" gom các **Quyết định** của GĐ1–4 · "Dùng trong dự án": class con khai từ vựng (`Slot<Vật>` với hook seal và hook
mới) và chủ `new SlotGroup<ÔCon, Vật>()`, `Attach` ở `Awake` và khi số ô đổi · ca `AreAllOccupied` với mảng rỗng. Không có "Trước
khi chạy": ô là component của dự án gắn trong prefab của nó. `.html` chọn từ `.md`: kịch bản (vật vào, ra, reset), cách dùng (class
con, chủ), flow, lưu ý (`Vacate` thừa, `Clear` im lặng, mảng rỗng).

**Mốc commit:** `docs(slots): SlotGroup.md`.

---

## Giai đoạn 6 — Dự án dùng bản Horcrux, xoá bản tạm (agent)

**Mục tiêu:** `TileSlot : Slot<Tile>` chỉ còn từ vựng tile; `TileSlotGroup.cs`, `TileSlotState` xoá; ba chủ đổi kiểu. Vật (`Tile.cs`)
không đổi dòng nào: `Reserve`, `Land`, `Vacate`, `OccupiedBy`, `LandedState` giữ tên.

### `Entities/TileSlot/TileSlot.cs` — đoạn cũ → đoạn mới

Xoá `enum TileSlotState`, `Changed`, `ReservedBy`, `OccupiedBy`, `State`, `IsFree`, `Reserve`, `Vacate`, `Land`, `Clear` thân cũ,
`OnBeforeLand`, `OnLanded`, `Occupy`. Giữ `TileScaleMode`, `_targetTileLocalScale`, `TargetTileLocalScale`, `LandedState`, `ScaleMode`,
`SetTargetTileLocalScale`, `PlayPoofVFX`. Khai:

```csharp
    public class TileSlot : Slot<Tile>
    {
        ...

        /// <summary>Mọi ô tile về order đáp rồi mới chen việc riêng; seal để hai ô con không bỏ qua bước này.</summary>
        protected sealed override void OnLanded(Tile tile)
        {
            tile.View.SetDrawState(TileDrawState.Landed);
            OnTileLanded(tile);
        }

        /// <summary>Sau khi đã về order đáp: hiệu ứng riêng của loại ô.</summary>
        protected virtual void OnTileLanded(Tile tile) { }
```

`BoxTileSlot`, `CacheTileSlot`: `protected override void OnLanded(Tile tile)` → `OnTileLanded`. `OnBeforeLand` của `BoxTileSlot` giữ
nguyên tên. Hai `Clear` override giữ nguyên, đã gọi `base.Clear()`.

### Ba chủ

| File | Cũ | Mới |
|---|---|---|
| `Box.cs` | `TileSlotGroup<BoxTileSlot> _slots` · `HandleSlotChanged(TileSlot slot)` · `TileSlotState.Occupied` | `SlotGroup<BoxTileSlot, Tile>` · `HandleSlotChanged(Slot<Tile> slot)` · `SlotState.Occupied` |
| `CategoryBox.cs` | `HandleSlotChanged(TileSlot slot)` · `TileSlotState.Occupied` | `Slot<Tile>` · `SlotState.Occupied` |
| `CacheTileSlots.cs` | `TileSlotGroup<CacheTileSlot> _group` · `HandleSlotChanged(TileSlot slot)` · `<see cref="TileSlotGroup{T}.FreeCount"/>` | `SlotGroup<CacheTileSlot, Tile>` · `Slot<Tile>` · `<see cref="SlotGroup{TSlot,TOccupant}.FreeCount"/>` |
| `ShoppingCart.cs` | `TileSlotGroup<TileSlot> _group` · `HandleSlotChanged(TileSlot slot)` · `TileSlotState.Occupied` | `SlotGroup<TileSlot, Tile>` · `Slot<Tile>` · `SlotState.Occupied` |

Thêm `using Horcrux.Runtime.Utilities.Slots;` ở năm file trên và `TileSlot.cs`. Xoá `Entities/TileSlotGroup.cs` + `.meta`.

**Editor setup:** không — `TileSlot.cs` ở lại dự án nên GUID script trong prefab không đổi; `_targetTileLocalScale` vẫn serialize ở
`TileSlot`.

**Kiểm:** grep `TileSlotGroup`, `TileSlotState` trả 0 · compile dự án sạch · test dự án `Tests/` xanh · **cần chơi thử** (developer):
một level — tile vào cache, dồn trái, vào box, box đầy đóng nắp, nam châm rút tile khỏi box, booster xe đẩy nhận và nhả tile;
dấu hiệu hỏng: box không đóng nắp khi đủ, khay báo "còn 1 ô" sai, tile đáp đè sai thứ tự vẽ.

**Mốc commit:** repo dự án `refactor(slots): TileSlot dùng Slot<Tile> của Horcrux` kèm con trỏ submodule.

---

## File → giai đoạn

| File | GĐ1 | GĐ2 | GĐ3 | GĐ4 | GĐ5 | GĐ6 |
|---|---|---|---|---|---|---|
| `Runtime/Utilities/Slots/Slot.cs` | tạo | `OccupiedBy`, `Changed`, `Land`, hook | `Vacate` | `Clear` | | |
| `Runtime/Utilities/Slots/SlotGroup.cs` | tạo | `OccupiedCount`, `Changed`, đếm lại | | `Attach` tái nhập, `ResetAll` | | |
| `Tests/EditMode/SlotGroupTests.cs` (agent) | tạo | thêm case | thêm case | thêm case | | |
| `docs/Runtime/SlotGroup/SlotGroup.md`, `.html` (agent) | | | | | tạo | |
| dự án: `TileSlot.cs`, `BoxTileSlot.cs`, `CacheTileSlot.cs`, `Box.cs`, `CategoryBox.cs`, `CacheTileSlots.cs`, `ShoppingCart.cs`, xoá `TileSlotGroup.cs` | | | | | | đổi |
