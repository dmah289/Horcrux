# Plan — `Slot<TOccupant>` và `SlotGroup<TSlot, TOccupant>`: ô đặt chỗ → đáp → rời, nhóm đếm và báo chủ

Bản tạm: category-jam `Assets/_CategoryJam/Scripts/Entities/TileSlot/TileSlot.cs`, `Entities/TileSlotGroup.cs` (chủ `Box`, `CacheTileSlots`, `PackBooster`); từ vựng tile ở lại `TileSlot` của dự án. Không phụ thuộc Plan nào.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.EditModeTests.csproj --tests Horcrux.EditModeTests`.

## Kịch bản

| Tình huống | Hệ làm gì | Chủ thấy |
|---|---|---|
| chủ `Attach` mảng ô, hỏi còn chỗ không | `FindFreeSlot` trả ô bật và trống đầu tiên | ô hoặc `null`; `FreeCount` |
| vật cất cánh, `Reserve` ô | ghi `ReservedBy`, không phát sự kiện | ô hết trống, `State` Reserved; chủ không được báo |
| ô tắt (khay chưa mở, box thu sức chứa) | `IsFree` false dù không ai giữ | ô không nhận vật |
| vật đáp, `Land` | hook trước → chiếm, phát `Changed` → xoá đặt chỗ → hook sau | nhóm đếm lại rồi phát `Changed` mang ô; trong handler `State` đã Occupied |
| vật đáp vào ô đang giữ chỗ cho vật khác | vật đáp thắng | `State` Occupied; sau `Land` `ReservedBy` null |
| ô cuối cùng có vật | nhóm đếm bằng số ô | `AreAllOccupied` true |
| vật rời, `Vacate` | `OccupiedBy` null, phát `Changed` | nhóm đếm lại |
| `Vacate` ô chỉ mới đặt chỗ | phát `Changed`, giữ `ReservedBy` | `State` vẫn Reserved, `OccupiedCount` không lệch |
| chủ đổi số ô giữa level (lấy từ pool, sinh thêm ô) | `Attach` lại: gỡ ô rời bộ, đăng ký ô mới, đếm lại một lần | không sự kiện; ô cũ không báo hai lần |
| reset hay dispose level | `ResetAll`: `Clear` từng ô im lặng, đếm về 0 | không `Changed` nào; mọi ô trống |
| mảng rỗng | `0 == 0` | `AreAllOccupied` true — chủ tự chặn |

## Flow

```
  vật (TOccupant) ──Reserve (cất cánh) / Land (đáp) / Vacate (rời)──► Slot<TOccupant>                     Slot.cs
                                Land: OnBeforeLand → chiếm, Changed → xoá đặt chỗ → OnLanded
                                                                         │ Changed(slot)   ▲ Changed += / -= · Clear (im lặng)
                                                                         ▼                 │
  chủ (dự án) ──new · Attach (lại khi số ô đổi) · FindFreeSlot · ResetAll──► SlotGroup<TSlot, TOccupant>  SlotGroup.cs
       ▲                                                                     đếm lại OccupiedCount mỗi lần ô báo
       └──────── Changed(slot) · Slots, FreeCount, OccupiedCount, AreAllOccupied ────────┘

  class con của dự án : Slot<Vật> — override OnBeforeLand, OnLanded, Clear; khai từ vựng riêng
```

## Thuật toán

| Bất biến | Giữ bằng |
|---|---|
| một ô một vật | `OccupiedBy` là một tham chiếu |
| đặt chỗ không tính là chiếm | nhóm chỉ đếm `State == Occupied` |
| `State` không lệch với tham chiếu; vật đáp thắng vật giữ chỗ | `State` suy từ `OccupiedBy`, `ReservedBy` mỗi lần đọc |
| `OccupiedCount` khớp số ô Occupied sau mỗi sự kiện | đếm lại từ đầu ở mỗi `Changed` và mỗi `Attach`; `ResetAll` đặt 0 sau khi `Clear` im lặng |

---

## GĐ1 — xong thì chủ hỏi được "còn chỗ không" và vật giữ được chỗ cho chuyến bay

```
  vật ──Reserve──► Slot+ ◄──IsFree── SlotGroup+ ◄──Attach / FindFreeSlot / FreeCount── chủ
```

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
    // TOccupant : class — null là "trống", so bằng tham chiếu, không cần Nullable; chữ ký generic sửa sau đắt.
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

        // Không nhận predicate: luật lọc là của chủ (so id, bỏ ô khoá), nhóm chỉ biết "trống".
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

`Tests/EditMode/SlotGroupTests.cs` (agent); `TestSlot : Slot<TestOccupant>` (Unity không gắn `MonoBehaviour` generic) dựng bằng `new GameObject().AddComponent<TestSlot>()`, `TestOccupant` là class rỗng:

| Case | Input | Kỳ vọng |
|---|---|---|
| ô mới | — | `IsFree` true, `State` Empty, `ReservedBy` null |
| đặt chỗ | `Reserve(a)` | `IsFree` false, `State` Reserved |
| ô tắt | `gameObject.SetActive(false)` | `IsFree` false dù không ai giữ |
| tìm ô trống | 3 ô, ô 0 đặt chỗ | `FindFreeSlot` trả ô 1; `FreeCount` 2 |
| hết chỗ | cả ba đặt chỗ | `FindFreeSlot` null, `FreeCount` 0 |
| mảng rỗng | `Attach(Array.Empty)` | `FindFreeSlot` null, `FreeCount` 0 |

---

## GĐ2 — xong thì vật đáp chiếm ô, chủ được báo và biết khi nào đầy

```
  vật ──Reserve / Land+──► Slot ──Changed+──► SlotGroup ──Changed+──► chủ
         (hook trước+ → chiếm+ → xoá đặt chỗ → hook sau+)   (đếm lại+)      hỏi AreAllOccupied+
```

### `Slot.cs` › enum `SlotState` › thêm sau `Reserved`

```csharp
        Reserved,
        Occupied,
```

### `Slot.cs` › `Properties` › thêm sau `ReservedBy`, thay thân `State`

Thêm `using System;`.

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

Mới:

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

        // Đếm lại từ đầu, không ±1 theo loại sự kiện: Vacate thừa không làm lệch số; n cỡ chục.
        private void RecountOccupied()
        {
            OccupiedCount = 0;
            foreach (var slot in Slots)
                if (slot.State == SlotState.Occupied)
                    OccupiedCount++;
        }

        #endregion Class Methods
```

`Tests/EditMode/SlotGroupTests.cs` (agent), thêm; `TestSlot` ghi thứ tự hook vào một `List<string>`:

| Case | Input | Kỳ vọng |
|---|---|---|
| đáp | `Reserve(a)` rồi `Land(a)` | thứ tự `OnBeforeLand` → `Changed` → `OnLanded`; lúc `Changed` phát `State` Occupied; lúc `OnLanded` chạy `ReservedBy` null |
| đáp không đặt chỗ trước | `Land(a)` | `State` Occupied, `Changed` một lần |
| đáp thắng đặt chỗ | `Reserve(a)`, `Land(b)` | `State` Occupied ngay trong handler `Changed`; sau `Land` `ReservedBy` null |
| nhóm đếm | 3 ô, `Land` 2 | `OccupiedCount` 2, `FreeCount` 1, `AreAllOccupied` false; `Changed` nhóm phát 2 lần, mang đúng ô |
| đủ | `Land` cả 3 | `AreAllOccupied` true |
| mảng rỗng | `Attach(Array.Empty)` | `AreAllOccupied` true (0 == 0) — ghi ở tài liệu là ca chủ phải tự chặn |

---

## GĐ3 — xong thì ô sống qua cả vòng vật vào và ra (nam châm hút vật khỏi box, khay dồn trái, vật về pool)

```
  vật ──Reserve / Land / Vacate+──► Slot ──Changed──► SlotGroup (đếm lại) ──Changed──► chủ
```

### `Slot.cs` › `API` › thêm sau `Reserve`, trước `Land`

```csharp
        public void Reserve(TOccupant occupant) => ReservedBy = occupant;

        /// <summary>Vật rời ô (bay đi, về pool). Phát Changed kể cả khi ô chỉ mới đặt chỗ: nhóm đếm lại từ đầu nên lời gọi thừa không lệch số.</summary>
        public void Vacate()
        {
            // Không xoá ReservedBy: vật rời là vật đã đáp; vật khác đang bay tới ô này vẫn giữ chỗ.
            OccupiedBy = null;
            Changed?.Invoke(this);
        }
```

`Tests/EditMode/SlotGroupTests.cs` (agent), thêm:

| Case | Input | Kỳ vọng |
|---|---|---|
| rời | `Land(a)` rồi `Vacate()` | `Changed` phát, `State` Empty, `IsFree` true |
| Vacate thừa | `Reserve(a)` rồi `Vacate()` | `Changed` phát, `State` vẫn Reserved, `OccupiedCount` của nhóm 0 |
| rời rồi đáp lại | `Land(a)`, `Vacate()`, `Land(b)` | `OccupiedBy` b, nhóm đếm 1 |
| ra vào nhiều lần | 3 ô, `Land` 3, `Vacate` 2, `Land` 1 | `OccupiedCount` 2, `AreAllOccupied` false |

---

## GĐ4 — xong thì chủ đổi được số ô giữa level và reset cả level không bắn sự kiện

```
  chủ ──Attach (lại)+ / ResetAll+──► SlotGroup ──Clear+──► Slot (im lặng)
                                         │ -= ô rời, += ô mới, đếm lại+
  class con ── override Clear+ ── xoá vết riêng rồi base.Clear()
```

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

Mới:

```csharp
        /// <summary>Gọi lại được sau khi chủ thêm hay bớt ô: ô đã đăng ký không đăng ký lần hai, ô rời bộ gỡ đăng ký.</summary>
        public void Attach(TSlot[] slots)
        {
            // Array.IndexOf, không HashSet: n cỡ chục và chỉ chạy lúc đổi cỡ.
            foreach (var slot in Slots)
            {
                if (Array.IndexOf(slots, slot) >= 0) continue;
                // Ô tắt khi thu sức chứa vẫn sống trong pool; không gỡ thì bật lại nó báo hai lần.
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

`Tests/EditMode/SlotGroupTests.cs` (agent), thêm:

| Case | Input | Kỳ vọng |
|---|---|---|
| `Attach` lại với 2 ô cũ + 1 ô mới | `Land` ô bị bỏ sau đó | `OccupiedCount` không đổi (đã gỡ); `Land` ô mới thì đổi |
| `Attach` lần hai cùng mảng | `Land` một ô | `Changed` nhóm phát **một** lần |
| `Attach` lại khi đã có ô chiếm | 3 ô `Land` 2, `Attach` 2 ô trong đó có 1 ô chiếm | `OccupiedCount` 1 ngay sau `Attach`, không sự kiện |
| `ResetAll` | sau `Land` 2 | `OccupiedCount` 0, không `Changed` nào, mọi ô `IsFree` |
| `Clear` override | `TestSlot.Clear` ghi cờ | cờ bật cho từng ô, `base` đã chạy (`ReservedBy` null) |

---

## Chuyển dự án (agent)

Đường dẫn dự án tính từ `Assets/_CategoryJam/Scripts/`.

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/SlotGroup/SlotGroup.md`, `.html` | tạo, `Code: Runtime/Utilities/Slots`: vòng đời một vật qua ô (Flow) · bảng API hai class · cách dùng: class con khai từ vựng (`Slot<Vật>`, hook seal và hook mới), chủ `new SlotGroup<ÔCon, Vật>()`, `Attach` ở `Awake` và khi số ô đổi · ca `AreAllOccupied` mảng rỗng; không có "Trước khi chạy" (ô gắn trong prefab của dự án); `.html`: kịch bản vào, ra, reset · cách dùng · flow · lưu ý (`Vacate` thừa, `Clear` im lặng, mảng rỗng) |
| `Entities/TileSlot/TileSlot.cs` | `TileSlot : Slot<Tile>`; xoá `enum TileSlotState`, `Changed`, `ReservedBy`, `OccupiedBy`, `State`, `IsFree`, `Reserve`, `Vacate`, `Land`, thân cũ `Clear`, `OnBeforeLand`, `OnLanded`, `Occupy`; giữ `TileScaleMode`, `_targetTileLocalScale`, `TargetTileLocalScale`, `LandedState`, `ScaleMode`, `SetTargetTileLocalScale`, `PlayPoofVFX`; thêm `OnLanded` seal và hook `OnTileLanded` (code dưới bảng) |
| `Entities/TileSlot/BoxTileSlot.cs`, `CacheTileSlot.cs` | `protected override void OnLanded(Tile tile)` → `OnTileLanded`; `OnBeforeLand` của `BoxTileSlot` và hai `Clear` override (đã gọi `base.Clear()`) giữ |
| `Entities/Box/Box.cs` | `SlotGroup<BoxTileSlot, Tile>` · `HandleSlotChanged(Slot<Tile> slot)` · `SlotState.Occupied` |
| `Entities/Box/CategoryBox.cs` | `HandleSlotChanged(Slot<Tile> slot)` · `SlotState.Occupied` |
| `Entities/Cache/CacheTileSlots.cs` | `SlotGroup<CacheTileSlot, Tile>` · `HandleSlotChanged(Slot<Tile> slot)` · `<see cref="SlotGroup{TSlot,TOccupant}.FreeCount"/>` |
| `Entities/Cache/PackBooster.cs` | `SlotGroup<TileSlot, Tile>` · `HandleSlotChanged(Slot<Tile> slot)` · `SlotState.Occupied` |
| bốn file chủ trên, `TileSlot.cs` | thêm `using Horcrux.Runtime.Utilities.Slots;` |
| `Entities/TileSlotGroup.cs` | xoá cùng `.meta` |
| `Tile.cs`, prefab | không đổi: `Reserve`, `Land`, `Vacate`, `OccupiedBy`, `LandedState` giữ tên; `TileSlot.cs` ở lại dự án nên GUID script và `_targetTileLocalScale` serialize giữ |
| Plan này | xoá cùng `.meta` |
| kiểm | grep `TileSlotGroup`, `TileSlotState` ra 0 · compile dự án sạch · test dự án `Tests/` xanh |
| chơi thử | một level: tile vào cache, dồn trái, vào box, box đầy đóng nắp, nam châm rút tile khỏi box, booster xe đẩy nhận và nhả tile; hỏng: box không đóng nắp khi đủ, khay báo "còn 1 ô" sai, tile đáp đè sai thứ tự vẽ |

`TileSlot.cs`:

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

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | từ vựng của vật (view, thứ tự vẽ) trong `Land` của base | base không gọi tên type dự án; class con seal `OnLanded` để gọi, thứ tự không đổi |
| bỏ | `ISlot` | một implementation, base class đủ |
| bỏ | `SlotGroup` là `MonoBehaviour` | nhóm không có đời sống riêng, chỉ `Attach` khi số ô đổi và `ResetAll` khi reset; thêm component phải gắn mà không mốc `OnDisable` nào cần |
| bỏ | `State` là field enum ghi ở `Reserve`, `Land` | bản sao thứ hai phải giữ khớp, thêm một cửa ghi cho mỗi chuyển trạng thái |
| bỏ | `IsFree` kiểm `gameObject != null` | `gameObject` null chỉ sau `Destroy`, khi đó mọi truy cập khác cũng ném |
| bỏ | `Changed` mang loại thay đổi | mọi chủ chỉ hỏi `State == Occupied`; cần "vừa đặt chỗ" thì `Reserve` phát `Changed`, đếm lại vẫn đúng |
| bỏ | `Changed` kiểu `Action<TSlot>` | ô không biết `TSlot`; handler của chủ chỉ đọc `State`, không cần cast |
| bỏ | cờ ở nhóm cho mốc khác (đặt chỗ) | chủ cần mốc khác thì thêm sự kiện, không thêm cờ |
| bỏ | `Vacate` chỉ phát khi ô đã chiếm | rẻ hơn mọi kỷ luật "chỉ `Vacate` khi đã `Land`" ở phía vật; người nghe đọc `State`, Empty hay Reserved đều là "không chiếm" |
| bỏ | `ResetAll` gọi `Vacate` từng ô | n lần đếm lại, n lần chủ phản ứng (tiếng, nhún), bắn hiệu ứng rời cho ô đang dispose |
| sau | `ReservedCount` | khi có chủ thứ hai hỏi |
