# Plan — `Slot<TOccupant>` và `SlotGroup<TSlot, TOccupant>` lên Horcrux

Ngày 2026-11-02. Nguồn: category-jam, `Assets/_CategoryJam/Scripts/Entities/TileSlot/TileSlot.cs` và
`Entities/TileSlotGroup.cs`, đã chạy với ba chủ thật: `Box` (ô đáp theo sức chứa), `CacheTileSlots` (khay ô mở dần), `ShoppingCart`
(xe đẩy). Cơ chế đặt chỗ → liên kết → đáp với hai hook và nhóm đếm lại theo sự kiện không gọi tên tile; phần gọi tên (`TileState`,
`TileScaleMode`, `View.SetDrawState`) ở lại dự án trong class con.

**Phân công:** Task 1 (Horcrux, code runtime) developer gõ · Task 2 (tài liệu `SlotGroup.md`) và Task 3 (test) agent viết sau khi
code có · Task 4 (dự án chuyển sang bản Horcrux) agent làm khi developer yêu cầu, ngoài phạm vi plan này.

Horcrux là submodule: Task 1–3 commit **trong `Assets/Horcrux`**.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | mọi game xếp vật vào ô: vật đặt chỗ lúc cất cánh, liên kết lúc đáp, rời ô thì bỏ liên kết; chủ ô cần đếm ô đã liên kết và tìm ô trống |
| Mục tiêu | hai class generic giữ **bất biến** của ô và nhóm: một ô một vật, đặt chỗ không tính là liên kết, số liên kết của nhóm luôn bằng số ô `IsLinked` sau mỗi sự kiện |
| Chỗ đặt | `Runtime/Utilities/Slots/Slot.cs`, `Runtime/Utilities/Slots/SlotGroup.cs`, namespace `Horcrux.Runtime.Utilities.Slots`; không gọi tên type dự án |
| Tên | `Slot<TOccupant>`, `SlotGroup<TSlot, TOccupant>`, `SlotChange`; dự án: `TileSlot : Slot<Tile>`. Developer đổi tuỳ ý trước khi gõ |
| Ràng buộc generic | `TOccupant : class` — chữ ký là thứ sửa sau đắt; `class` để `null` là "trống" không cần `Nullable` và so bằng tham chiếu |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| `TargetLocalScale`, `ScaleMode`, `LandedState`, `PlayPoofVFX` trong base | là từ vựng của tile 2.5D; dự án khai ở `TileSlot : Slot<Tile>`. Framework giữ cơ chế, dự án giữ từ vựng |
| `tile.View.SetDrawState(Landed)` trong `Land` | gọi tên view của dự án. `TileSlot` của dự án khai `protected sealed override OnLanded` gọi `SetDrawState` rồi mở hook mới `OnTileLanded` cho hai ô con — không dựa vào kỷ luật gọi `base`, vì hai override hiện có không gọi `base`. Thứ tự trong `Land` không đổi: hook trước → liên kết → xoá đặt chỗ → `SetDrawState` → hook con |
| `Link` public | bản dự án không có caller nào ngoài `Land`; liên kết nằm trong thân `Land` |
| `SlotGroup` là `MonoBehaviour` | nhóm không có đời sống riêng: chủ (`Box`, khay) `new` nó, `Attach` lại khi số ô đổi, `ResetAll` khi reset; không cần mốc tắt |
| Interface `ISlot` | một implementation; base class đủ cho hook `protected virtual` |
| `FindFreeSlot` theo điều kiện (predicate) | chủ lọc theo luật riêng (`Box` so id trước khi hỏi nhóm) ở bên ngoài, nhóm chỉ biết "trống" |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| `Unlink` phát `Changed` kể cả khi ô chưa liên kết (chỉ đặt chỗ) | nhóm **đếm lại** từ đầu ở mỗi sự kiện, nên `Unlink` thừa không làm lệch `LinkedCount`; rẻ hơn mọi kỷ luật "chỉ Unlink khi đã Link" |
| `Clear` im lặng, không phát `Changed` | chỉ hợp lệ khi chủ `ResetAll` cả nhóm rồi đặt `LinkedCount = 0`; phát event ở đây là n lần đếm lại cho một lần reset |
| `IsFree` kiểm `gameObject.activeSelf` | ô tắt (khay chưa mở, box thu sức chứa) không nhận vật; ô là component nên trạng thái bật tắt là của nó |
| `Attach` gọi lại được, so từng ô với bộ cũ | chủ đổi số ô giữa level (sức chứa box, khay mở thêm ô): ô đã đăng ký không đăng ký lần hai, ô rời bộ gỡ đăng ký, rồi đếm lại một lần |
| `Reserve` không phát event | đặt chỗ là trạng thái tạm của chuyến bay; chủ chỉ quan tâm liên kết |

**Mở rộng sau**

- `Changed` mang cả `Reserved` khi có chủ cần đếm ô "đang bay tới": thêm phần tử enum, `Reserve` phát event; nhóm đếm lại vẫn đúng.

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| `TileSlot.cs`, `TileSlotGroup.cs` của dự án | toàn bộ cơ chế, hai hook, cách đếm lại | field và property mang domain tile (bảng trên) |
| Horcrux `Utilities/Common/DeferredSet.cs` | — | là tập không trùng có tombstone, không có khái niệm "ô" và "vật" |
| Horcrux `Pooling` | — | pool cấp phát object, không giữ quan hệ ô–vật |
| `Box.cs`, `CacheTileSlots.cs`, `ShoppingCart.cs` | ba cách gọi `Attach`, `FindFreeSlot`, `ResetAll`, `LinkedCount`, `FreeCount`, `AreAllLinked`, `Slots`, và ba handler `Changed(TileSlot, SlotChange)` | — |

---

## Task 1 — Horcrux (developer gõ)

Thư mục `Runtime/Utilities/Slots/`, hai file.

### 1.1 `Slot.cs`

```csharp
using System;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Slots
{
    public enum SlotChange
    {
        Linked = 1,
        Unlinked = 2,
    }

    /// <summary>
    /// Một ô giữ tối đa một vật: đặt chỗ lúc vật cất cánh, liên kết lúc đáp, bỏ liên kết lúc rời. Loại ô cụ thể chen việc
    /// riêng qua hai hook của <see cref="Land"/>.
    /// </summary>
    public class Slot<TOccupant> : MonoBehaviour where TOccupant : class
    {
        #region Properties

        public event Action<Slot<TOccupant>, SlotChange> Changed;

        public TOccupant ReservedBy { get; private set; }
        public TOccupant LinkedOccupant { get; private set; }

        /// <summary>Ô đang bật, chưa ai đặt chỗ, chưa ai liên kết.</summary>
        public bool IsFree => gameObject.activeSelf && ReservedBy == null && LinkedOccupant == null;
        public bool IsLinked => LinkedOccupant != null;

        #endregion Properties

        #region API

        public void Reserve(TOccupant occupant) => ReservedBy = occupant;

        /// <summary>Phát Changed kể cả khi ô chỉ mới đặt chỗ: nhóm đếm lại từ đầu nên lời gọi thừa không làm lệch số.</summary>
        public void Unlink()
        {
            LinkedOccupant = null;
            Changed?.Invoke(this, SlotChange.Unlinked);
        }

        /// <summary>Vật vừa đáp. Bất biến chỉ có một bản ở đây: hook trước, liên kết, xoá đặt chỗ, hook sau.</summary>
        public void Land(TOccupant occupant)
        {
            OnBeforeLink(occupant);
            LinkedOccupant = occupant;
            Changed?.Invoke(this, SlotChange.Linked);
            ReservedBy = null;
            OnLanded(occupant);
        }

        /// <summary>Xoá im lặng, không phát Changed; chỉ hợp lệ trong ResetAll của nhóm, nơi đếm lại về 0 sau đó.</summary>
        public virtual void Clear()
        {
            LinkedOccupant = null;
            ReservedBy = null;
        }

        /// <summary>Trước khi liên kết: đặt vật vào cây của ô, tắt phần vẽ không cần.</summary>
        protected virtual void OnBeforeLink(TOccupant occupant) { }

        /// <summary>Sau khi đã liên kết và xoá đặt chỗ: hiệu ứng và trạng thái riêng của loại ô.</summary>
        protected virtual void OnLanded(TOccupant occupant) { }

        #endregion API
    }
}
```

### 1.2 `SlotGroup.cs`

```csharp
using System;

namespace Horcrux.Runtime.Utilities.Slots
{
    /// <summary>
    /// Bộ ô của một chủ: đếm lại số ô liên kết mỗi khi một ô báo đổi, nên Unlink thừa trên ô chỉ được đặt chỗ không làm lệch số.
    /// </summary>
    public class SlotGroup<TSlot, TOccupant>
        where TSlot : Slot<TOccupant>
        where TOccupant : class
    {
        #region Properties

        public TSlot[] Slots { get; private set; } = Array.Empty<TSlot>();
        public int LinkedCount { get; private set; }

        public event Action<Slot<TOccupant>, SlotChange> Changed;

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

        public bool AreAllLinked => LinkedCount == Slots.Length;

        #endregion Properties

        #region API

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
            RecountLinked();
        }

        public TSlot FindFreeSlot()
        {
            for (var i = 0; i < Slots.Length; i++)
                if (Slots[i].IsFree)
                    return Slots[i];
            return null;
        }

        /// <summary>Clear từng ô (im lặng) rồi LinkedCount về 0; chỉ khi dispose hoặc reset cả level.</summary>
        public void ResetAll()
        {
            foreach (var slot in Slots)
                slot.Clear();
            LinkedCount = 0;
        }

        #endregion API

        #region Class Methods

        private void HandleSlotChanged(Slot<TOccupant> slot, SlotChange change)
        {
            RecountLinked();
            Changed?.Invoke(slot, change);
        }

        private void RecountLinked()
        {
            LinkedCount = 0;
            foreach (var slot in Slots)
                if (slot.IsLinked)
                    LinkedCount++;
        }

        #endregion Class Methods
    }
}
```

**Khác với bản dự án, và vì sao**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| `IsFree` | `gameObject != null && gameObject.activeSelf && …` | bỏ `gameObject != null` | `this` là component đang được gọi; `gameObject` của nó null chỉ khi đã Destroy, và khi đó mọi truy cập khác cũng ném |
| `Land` | gọi `tile.View.SetDrawState(Landed)` giữa hai hook | không có dòng đó | gọi tên view dự án; `TileSlot` seal `OnLanded` để gọi `SetDrawState` rồi hook `OnTileLanded`, hai ô con override hook mới. Thứ tự giữ nguyên |
| `Link` | public, chỉ `Land` gọi | thân nằm trong `Land` | không caller ngoài |
| `Changed` | `Action<TileSlot, SlotChange>` | `Action<Slot<TOccupant>, SlotChange>` | ba handler của chủ (`Box`, khay, xe đẩy) đổi tham số đầu thành `Slot<Tile>` khi chuyển; cần `TileSlot` thì cast một lần |
| `SlotChange` | `Linked = 0, Unlinked = 1` | `= 1, = 2` | enum chia sẻ qua event; `0` là `default` để lộ giá trị chưa gán |
| field `_targetTileLocalScale` + `SetTargetTileLocalScale`, `LandedState`, `ScaleMode`, `PlayPoofVFX` | có | ở `TileSlot` của dự án | từ vựng tile |

### Nghiệm thu Task 1

`com.horcrux.runtime.csproj` build sạch; chưa có caller trong Horcrux là đúng: caller là class con của dự án (Task 4).

---

## Task 2 — Tài liệu `Runtime/Utilities/Slots/SlotGroup.md` (agent)

Mục: vòng đời một vật qua ô (Reserve → Land → Unlink) vẽ ASCII · bảng API hai class · "Design decisions" là bảng "Quyết định trái
trực giác" ở trên · "Dùng trong dự án": class con khai từ vựng (`TileSlot : Slot<Tile>` với hook) và chủ `new SlotGroup<TileSlot, Tile>()`,
`Attach` ở Awake và khi số ô đổi. Không có "Trước khi chạy": ô là component dự án gắn trong prefab của nó.

## Task 3 — Test EditMode (agent)

`Tests/EditMode/SlotGroupTests.cs`: Unity không gắn được MonoBehaviour generic, nên test khai `TestSlot : Slot<TestOccupant>`
(ghi lại thứ tự hook) và dựng bằng `new GameObject().AddComponent<TestSlot>()`; `TestOccupant` là class rỗng.

| Case | Input | Kỳ vọng |
|---|---|---|
| ô mới | — | `IsFree` true, `IsLinked` false, `ReservedBy` null |
| đặt chỗ | `Reserve(a)` | `IsFree` false, `IsLinked` false, không event |
| đáp | `Reserve(a)` rồi `Land(a)` | thứ tự `OnBeforeLink` → `Changed(Linked)` → `ReservedBy == null` khi `OnLanded` chạy; `IsLinked` true |
| rời | `Land(a)` rồi `Unlink()` | `Changed(Unlinked)`, `IsFree` true |
| Unlink thừa | `Reserve(a)` rồi `Unlink()` | `Changed(Unlinked)` phát, `LinkedCount` của nhóm vẫn 0 |
| ô tắt | `gameObject.SetActive(false)` | `IsFree` false dù không ai giữ |
| nhóm đếm | 3 ô, `Land` 2 | `LinkedCount` 2, `FreeCount` 1, `AreAllLinked` false |
| đủ | `Land` cả 3 | `AreAllLinked` true |
| `Attach` lại với 2 trong 3 ô cũ + 1 ô mới | `Land` ô bị bỏ sau đó | nhóm không đổi `LinkedCount` (đã gỡ đăng ký); `Land` ô mới thì đổi |
| `Attach` lần hai cùng mảng | `Land` một ô | `Changed` của nhóm phát **một** lần (không đăng ký đôi) |
| `ResetAll` | sau `Land` 2 | `LinkedCount` 0, không event nào, mọi ô `IsFree` |
| `FindFreeSlot` | ô 0 đặt chỗ, ô 1 liên kết, ô 2 trống | trả ô 2; cả ba bận thì null |
| mảng rỗng | `Attach(Array.Empty)` | `AreAllLinked` true (0 == 0) — ghi ở doc là ca chủ phải tự chặn |
