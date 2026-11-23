# Plan — `ConveyorLoop`: hàng vật trượt quay vòng giữ nguyên khe

Bản tạm: category-jam `Scripts/Entities/Shelf/ShelfMover.cs` › `LoopLength`, `WrapIntoLoop`, `RegisterBounds`; `Tests/EditMode/ShelfMoverTests.cs`. Không phụ thuộc Plan nào; chung `Runtime/AssemblyInfo.cs` với `Horcrux_SpriteFill_Plan.md` GĐ1 — Plan gõ trước tạo file, Plan sau thấy dòng đã có thì bỏ qua.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.EditModeTests.csproj --tests Horcrux.EditModeTests`.

## Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| hàng ≥ 2 vật, chủ gọi `Advance(quãng)` mỗi frame | dời mọi vật cùng quãng theo trục | cả hàng trượt cùng nhịp |
| vật đi qua `max + margin` (quãng dương) hay `min − margin` (quãng âm) | lùi đúng một vòng `Length`, giữ phần đã vượt | vật khuất ở biên này hiện lại ở biên kia, khe giữ nguyên |
| hàng trộn vật dài, vật ngắn | chỗ nối vật cuối → vật đầu lấy khe mép trung bình | chỗ nối không đè, hàng quay như vòng khép kín |
| frame giật, quãng lớn bất thường | vẫn lùi một vòng, giữ phần vượt | khe không lệch dồn sau nhiều vòng |
| hàng dưới 2 vật | `Length` = 0, `Advance` không dời | vật đứng yên |
| `Remove` vật giữa chừng | bỏ vật; biên, vòng giữ nguyên | ô trống chạy theo hàng, vật còn lại giữ khe |
| `Remove` vật không thuộc hàng | bỏ qua | không log |
| `Add` sau lần `Advance` đầu | `LogError`, bỏ qua | Console đỏ, hàng không đổi |
| trục Y (hay Z) | đọc, ghi `localPosition[trục]` | thành phần khác không đổi |

## Flow

```
chủ (dự án: gom vật theo hàng, quyết quãng mỗi frame, quyết khi nào dừng)
   │ new(AxisType, margin) · Add · Remove · Advance
   ▼
ConveyorLoop                                                    Runtime/Utilities/Layout/ConveyorLoop.cs
   ├─ _members, _halfLengths (song song theo chỉ số) · _min, _max · Length, Count (internal, test đọc)
   ├─ Add ──► RebuildBounds ──► LoopLength(vị trí, nửa bề dài)   static public, tool soạn level gọi thẳng
   ├─ Remove ──► bỏ khỏi _members, _halfLengths; biên, Length giữ
   └─ Advance ──► WrapIntoLoop từng vật ──► localPosition[trục]
test ──Count · Length──► ConveyorLoop                            Runtime/AssemblyInfo.cs (InternalsVisibleTo)
```

## Thuật toán

Độ dài một vòng — vật lùi đúng `L` thì khe mọi cặp kề nhau giữ nguyên; chỗ nối (vật cuối → vật đầu) cũng là một khe của hàng.

| Ký hiệu | Nghĩa |
|---|---|
| $x_i$, $h_i$ | toạ độ tâm, nửa bề dài vật $i$ theo trục; $n$ vật |
| $f, l$ | vật đầu (toạ độ nhỏ nhất), vật cuối (lớn nhất) |
| $S = x_l - x_f$ | khoảng tâm từ đầu tới cuối |
| $G = S - (2\sum_i h_i - h_f - h_l)$ | tổng khe mép của $n - 1$ cặp kề (vật giữa góp hai nửa bề dài, hai vật đầu một) |

$$L = S + h_f + h_l + \frac{G}{n - 1}$$

Trong code: `edgeGapSum` là $G$, `LoopLength` trả $L$. Lùi vòng: quãng dương và toạ độ > `_max + _margin` thì `− Length`; quãng âm và toạ độ < `_min − _margin` thì `+ Length`.

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| mọi vật cùng $h$, tâm cách đều $d$ | $L = n \cdot d$ | GĐ1 case 1 |
| cùng $h$, cách không đều | $L = n \cdot S / (n - 1)$ | GĐ1 case 1 |
| N N S N N S N N xếp sát, mép cách đều 0.6 | $g = 0.6$, $L = 48$ | GĐ1 case 2 |
| $n < 2$ | $L = 0$ | GĐ1 case 3 |

Bản mẫu (C#): case 2, 4, 5, tổng 8051 bước, mỗi hàng 3 vòng — lệch khoảng cách vòng ≤ 4·10⁻⁴, khe mép nhỏ nhất 0.5999 (đặt 0.6) · đối chứng vòng = n × khoảng tâm trung bình: khe nối −0.54 (đè).

---

## GĐ1 — xong thì một hàng trượt quay vòng chạy được trên bản Horcrux

```
  chủ ──new+(AxisType, margin) · Add+ · Advance+──► ConveyorLoop+ ──localPosition[trục]──► vật
                                                      └─ RebuildBounds+ ──► LoopLength+ (static)
  test ──Count+ · Length+ (internal)──► ConveyorLoop
```

`Runtime/AssemblyInfo.cs` — file mới (bỏ qua nếu đã có dòng này):

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Horcrux.EditModeTests")]
```

`Runtime/Utilities/Layout/ConveyorLoop.cs` — file mới:

```csharp
using System.Collections.Generic;
using Horcrux.Runtime.Utilities.Common;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Layout
{
    /// <summary>
    /// Một hàng vật trượt cùng nhịp trên một trục, qua biên thì lùi đúng một vòng nên khe giữa các vật giữ nguyên mãi.
    /// Vật là con cùng một cha: đọc, ghi localPosition.
    /// </summary>
    public sealed class ConveyorLoop
    {
        #region Properties

        private readonly int _axis;
        private readonly float _margin;
        private readonly List<Transform> _members = new();
        private readonly List<float> _halfLengths = new();
        private readonly List<float> _positionBuffer = new();

        private float _min;
        private float _max;

        /// <summary>Độ dài một vòng; 0 khi hàng chưa đủ hai vật.</summary>
        internal float Length { get; private set; }

        internal int Count => _members.Count;

        #endregion Properties

        /// <param name="axis">Trục trượt; chỉ số thành phần của localPosition.</param>
        /// <param name="margin">Vật đi quá vật xa nhất lúc dựng thêm đoạn này mới lùi vòng: đủ để khuất ngoài màn hình.</param>
        public ConveyorLoop(AxisType axis, float margin)
        {
            _axis = (int)axis;
            _margin = margin;
        }

        #region API

        /// <summary>Thêm vật ở chỗ đang đứng; biên và vòng tính lại từ mọi vật. Thêm hết trước lần Advance đầu.</summary>
        /// <param name="halfLength">Nửa bề dài vật theo trục trượt.</param>
        public void Add(Transform member, float halfLength)
        {
            _members.Add(member);
            _halfLengths.Add(halfLength);
            RebuildBounds();
        }

        /// <summary>Dời mọi vật một quãng có dấu theo trục; vật qua biên phía đang đi lùi đúng một vòng.</summary>
        public void Advance(float distance)
        {
            // Một vật không có khoảng cách tới vật kế nên không có vòng.
            if (_members.Count < 2) return;

            for (int i = 0; i < _members.Count; i++)
            {
                var position = _members[i].localPosition;
                position[_axis] = WrapIntoLoop(position[_axis] + distance, distance);
                _members[i].localPosition = position;
            }
        }

        /// <summary>
        /// Một vòng của hàng: từ vật đầu tới vật cuối, cộng chỗ nối vật cuối sang vật đầu. Chỗ nối cách mép bằng khe mép trung
        /// bình của hàng, vì vật nhỏ xen vật lớn thì khoảng cách tâm trung bình hẹp hơn hai vật lớn cần, hai vật hai đầu đè nhau.
        /// Hàng một loại vật thì đúng bằng n × khoảng cách tâm trung bình.
        /// </summary>
        /// <param name="positions">Toạ độ theo trục trượt của mọi vật trong hàng, thứ tự nào cũng được.</param>
        /// <param name="halfLengths">Nửa bề dài từng vật theo trục trượt, cùng thứ tự.</param>
        public static float LoopLength(IReadOnlyList<float> positions, IReadOnlyList<float> halfLengths)
        {
            int count = positions.Count;
            if (count < 2) return 0f;

            int first = 0, last = 0;
            float halfLengthSum = 0f;
            for (int i = 0; i < count; i++)
            {
                if (positions[i] < positions[first]) first = i;
                if (positions[i] > positions[last]) last = i;
                halfLengthSum += halfLengths[i];
            }

            float span = positions[last] - positions[first];
            float ends = halfLengths[first] + halfLengths[last];
            // Tổng khe mép của n − 1 cặp vật cạnh nhau: mỗi vật giữa góp hai mép, hai vật đầu góp một.
            float edgeGapSum = span - (2f * halfLengthSum - ends);
            return span + ends + edgeGapSum / (count - 1);
        }

        #endregion API

        #region Class Methods

        /// <summary>
        /// Qua biên thì lùi đúng một vòng, giữ phần đã vượt. Đặt về một điểm cố định làm vật hụt phần vượt mỗi lần quay vòng,
        /// hụt nhiều khi frame giật; lệch cộng dồn tới khi đè vật cạnh.
        /// </summary>
        private float WrapIntoLoop(float coord, float distance)
        {
            // if, không while: quãng một frame nhỏ hơn vòng nhiều bậc (vòng ≥ hai bề ngang màn hình).
            if (distance > 0f && coord > _max + _margin) return coord - Length;
            if (distance < 0f && coord < _min - _margin) return coord + Length;
            return coord;
        }

        private void RebuildBounds()
        {
            _positionBuffer.Clear();
            _min = float.MaxValue;
            _max = float.MinValue;
            foreach (var member in _members)
            {
                float coord = member.localPosition[_axis];
                if (coord < _min) _min = coord;
                if (coord > _max) _max = coord;
                _positionBuffer.Add(coord);
            }

            Length = LoopLength(_positionBuffer, _halfLengths);
        }

        #endregion Class Methods
    }
}
```

`Tests/EditMode/ConveyorLoopTests.cs` (agent) — vật là `GameObject` tạm con một root, `DestroyImmediate` ở `TearDown`; `STEP = 0.05`, sai số khe `0.01`; khoảng cách vòng của vật i = `(x_i − x_0) mod Length`:

| # | Case | Input | Kỳ vọng |
|---|---|---|---|
| 1 | vòng hàng một loại, cách không đều | x {−21, −14, 0, 7, 14}, h 3.2 | `LoopLength` = 5 × 35 / 4 = 43.75 |
| 2 | chỗ nối hàng xếp sát giữ khe mép | N N S N N S N N tại {−24, −17, −12, −7, 0, 5, 10, 17}, N h 3.2, S h 1.2, quãng âm 3 vòng | mỗi bước: khe mép mọi cặp kề theo vòng = 0.6 — đỏ nếu chỗ nối lấy khoảng tâm trung bình (khe nối −0.54) |
| 3 | dưới hai vật | 0 vật, 1 vật | `LoopLength` = 0; `Advance(1)` không dời vật |
| 4 | hàng trộn cỡ giữ khe sau nhiều vòng | N S N S N tại {−24, −12, 0, 12, 24}, quãng dương 3 vòng | khoảng cách vòng từ vật 0 tới mọi vật không đổi |
| 5 | frame lệch nhịp | 5 vật h 3.2 cách 7, `System.Random(7)`: bước `STEP × [0.7, 1.3]`, 1/40 bước là 1 | như case 4 — đỏ nếu lùi vòng đặt vật về điểm cố định |
| 6 | trục Y | 3 vật h 1 trên y {−7, 0, 7}, `AxisType.Y`, quãng âm 3 vòng | x không đổi; khoảng cách vòng theo y không đổi |

---

## GĐ2 — xong thì chủ bỏ được vật khỏi hàng giữa chừng, `Add` muộn báo đỏ

```
  chủ ──Add (guard+ _hasAdvanced) · Advance (đánh dấu+) · Remove+──► ConveyorLoop ──► vật
                                                       Remove+: bỏ khỏi _members, _halfLengths; biên, Length giữ
```

`ConveyorLoop.cs` › region `Properties` › sau `private float _max;`:

```csharp
        private float _max;
        private bool _hasAdvanced;
```

`ConveyorLoop.cs` › `Add` — thêm guard đầu hàm:

```csharp
        public void Add(Transform member, float halfLength)
        {
            // Biên tính từ chỗ đứng hiện tại: sau khi hàng đã trượt, chỗ đó không còn là chỗ dựng, vật sẽ đè nhau.
            if (_hasAdvanced)
            {
                Debug.LogError($"[ConveyorLoop]: Add {member.name} sau khi hàng đã chạy; thêm mọi vật trước lần Advance đầu.");
                return;
            }

            _members.Add(member);
```

`ConveyorLoop.cs` › `Advance` — đánh dấu sau guard số vật:

```csharp
            if (_members.Count < 2) return;
            _hasAdvanced = true;
```

`ConveyorLoop.cs` › region `API` › sau `Advance`:

```csharp
        /// <summary>
        /// Bỏ vật khỏi hàng. Biên và vòng giữ nguyên: vật còn lại giữ khe, chỗ vật vừa rời thành ô trống chạy theo hàng.
        /// Vật không thuộc hàng thì bỏ qua.
        /// </summary>
        public void Remove(Transform member)
        {
            int index = _members.IndexOf(member);
            if (index < 0) return;

            _members.RemoveAt(index);
            _halfLengths.RemoveAt(index); // Không RebuildBounds: vị trí lúc này đã trượt, dựng vòng trên đó thì vật đè nhau.
        }
```

`Tests/EditMode/ConveyorLoopTests.cs` (agent) — thêm:

| # | Case | Input | Kỳ vọng |
|---|---|---|---|
| 7 | rời giữa hàng | 5 vật như case 4, `Remove` vật 2 trước khi chạy, 3 vòng | `Length` không đổi; khoảng cách vòng giữa 4 vật còn lại không đổi |
| 8 | rời còn một vật | 2 vật, `Remove` một, `Advance(1)` | vật còn lại đứng yên |
| 9 | thêm sau khi chạy | 2 vật, `Advance(0.1)`, `Add` vật thứ ba | `LogAssert.Expect(LogType.Error, …)`; `Count` = 2 |
| 10 | bỏ vật lạ | 2 vật, `Remove` vật không thuộc hàng | `Count` = 2, không log |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/ConveyorLoop/ConveyorLoop.md`, `.html` | tạo: `Code: Runtime/Utilities/Layout/ConveyorLoop.cs` · luồng · công thức vòng rút gọn · API · hai bẫy: đặt về điểm cố định khi quay vòng, vòng lấy khoảng tâm trung bình; tên trung tính |
| `Scripts/Director/ShelfConveyorRows.cs` | tạo: class thuần do `EntitiesManager` sở hữu, khuôn `FallingShelfColumns` |
| `ShelfConveyorRows.MARGIN` | `const float MARGIN = 2f`, thay `ShelfMover.OFFSET` (tool level đọc chung) |
| `ShelfConveyorRows.Add(Transform shelf, ShelfMotion motion, float speed, Vector2 halfExtents)` | trục (`AxisType`), dấu từ `motion`; khoá hàng = `(trục, RoundToInt(toạ độ ngang trục))`; hàng chưa có thì `new ConveyorLoop(trục, MARGIN)`, nhận `speed` và dấu của kệ đầu; nhớ kệ → hàng |
| `ShelfConveyorRows.Remove(Transform shelf)` | tra kệ → hàng, `loop.Remove` |
| `ShelfConveyorRows.Advance(float distanceScale)` | mỗi hàng `loop.Advance(dấu × speed × distanceScale)` |
| `ShelfConveyorRows.Clear()` | bỏ mọi hàng |
| `EntitiesManager` › `_movers` | → `private readonly ShelfConveyorRows _conveyorRows = new();` + `public ShelfConveyorRows ConveyorRows => _conveyorRows;` |
| `EntitiesManager.Update` | thoát khi `!Director.LevelLoaded \|\| Director.IsBoosterBeingUsed`; `_conveyorRows.Advance(distanceScale)` (giữ `SHELF_SPEED_SCALE`) |
| `EntitiesManager.CreateShelf` | bỏ `_movers.Add(shelf.Mover)` |
| `EntitiesManager.HandleLevelDisposed` | `_movers.Clear()` → `_conveyorRows.Clear()` |
| `LevelLoader.InstantiateShelves` | sau hai vòng `PrepareForLevel`: mỗi kệ trượt `entitiesManager.ConveyorRows.Add(shelf.transform, shelf.Motion, shelf.Speed, shelf.Capacity == 1 ? LevelBoundsFitter.SINGLE_SHELF_CELL_EXTENTS : LevelBoundsFitter.SHELF_CELL_EXTENTS)` |
| `LevelLoader.FinalizeLevelSetup` › vòng trả kệ trống | `entitiesManager.ConveyorRows.Remove(shelf.Item1.transform)` ngay trước `Return` |
| `Addressables/Shelf/NormalMovingShelf.prefab`, `SingleMovingShelf.prefab` | gỡ component `ShelfMover` **lúc `ShelfMover.cs` còn compile** (xoá script trước thì prefab giữ script missing): sửa YAML từ bản đã commit, dòng `- component:` và khối `--- !u!114`, diff chỉ hai khối. Sau đó prefab kệ thường và kệ trượt giống hệt; gộp hay giữ do developer quyết |
| `ShelfMover.cs` + `.meta` | xoá, sau dòng prefab |
| `Shelf` | bỏ ô `mover`, `Mover`, `mover.Begin` trong `PrepareForLevel`, `mover.Stop` trong `OnReturnToPool` |
| `_GameDesign/Script/LevelEditor/Shelf/MovingRowCheck.cs`, `_GameDesign/Tests/EditMode/MovingRowCheckTests.cs`, `MovingRowLayoutTests.cs` | `ShelfMover.LoopLength` → `ConveyorLoop.LoopLength`; `ShelfMover.OFFSET` → `ShelfConveyorRows.MARGIN`; comment nhắc `ShelfMover` theo tên mới |
| `_GameDesign/Tests/EditMode/CategoryJam.GameDesign.Tests.asmdef` › `references` | thêm `"com.horcrux.runtime"`: hai file test trên gọi thẳng `ConveyorLoop.LoopLength`, reference assembly không bắc cầu |
| `_GameDesign/Script/LevelEditor/Shelf/MovingRowAnalysis.cs`, `MovingRowLayout.cs`, `_GameDesign/Script/LevelEditor/Window/CellTab.cs`, `_GameDesign/Tests/EditMode/MovingZoneTests.cs` | chỉ comment nhắc `ShelfMover` → `ShelfConveyorRows` / `ConveyorLoop` |
| `Tests/EditMode/ShelfMoverTests.cs` | → `ShelfConveyorRowsTests.cs`: giữ `LoneSingleInRow_MovesWithRow`; thêm hai hàng khác y chạy độc lập · `RightToLeft` đi chiều âm. Case vòng, khe đã ở Horcrux |
| `Entities.md` | cây `Shelf` bỏ `ShelfMover`; đoạn băng chuyền trỏ `ShelfConveyorRows` + Horcrux `ConveyorLoop`; hai bẫy quay vòng trỏ sang tài liệu Horcrux; dòng "NormalShelf và NormalMovingShelf chỉ khác đúng mảnh này" sửa theo prefab mới |
| kiểm | `verify.sh --csproj CategoryJam.Gameplay.csproj,CategoryJam.Editor.csproj,CategoryJam.GameDesign.csproj,CategoryJam.EditModeTests.csproj,CategoryJam.GameDesign.Tests.csproj --tests CategoryJam.EditModeTests,CategoryJam.GameDesign.Tests,Horcrux.EditModeTests` xanh · `grep -r ShelfMover Assets/_CategoryJam Assets/_GameDesign` rỗng |
| Plan này | xoá cùng `.meta` |
| chơi thử | level có hàng kệ trượt trộn kệ 3 ô và 1 ô, cả hai chiều, chạy ≥ 2 vòng theo bảng Kịch bản; kệ trống trả pool để lại ô trống chạy theo hàng; dùng booster thì hàng dừng; hỏng: kệ đứng yên, hai kệ đè nhau, kệ nhảy giữa màn hình |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | vòng = n × khoảng tâm trung bình | vật ngắn xen vật dài kéo tâm trung bình hẹp lại, hai vật dài hai đầu đè nhau ở chỗ nối |
| bỏ | lùi vòng đặt vật về một điểm cố định | vật hụt phần vượt mỗi vòng, frame giật hụt nhiều, lệch dồn tới khi đè vật kề |
| bỏ | `Remove` tính lại biên và vòng | vị trí lúc đó đã trượt; dựng vòng trên đó làm vật đè nhau |
| bỏ | `Remove` vật lạ thì `LogError` | chủ trả mọi vật trống về pool, kể cả vật không trượt; bỏ qua là hợp đồng |
| bỏ | registry hai `static Dictionary`, khoá toạ độ làm tròn, như bản tạm | state static sống qua lần Play khi tắt domain reload; luật gom hàng thuộc dự án |
| bỏ | tốc độ riêng từng vật, mỗi vật tự `Step` | khe chỉ giữ được khi cả hàng chung nhịp; caller nhân tốc độ vào quãng. Dữ liệu 134 level: mọi ô `speed: 0`, không hàng trộn chiều |
| bỏ | chiều lùi vòng theo enum chuyển động của dự án | theo dấu quãng: framework không biết từ vựng chuyển động của dự án |
| bỏ | `_spacingOffset` cộng vào vòng | cả hai prefab đều 0, không call site khác mặc định |
| bỏ | `OnDrawGizmos` vẽ biên | vẽ từ registry static; hết registry thì chủ tự vẽ nếu cần |
| bỏ | gom vật theo hàng trong `ConveyorLoop` | luật gom thuộc dự án (toạ độ làm tròn, dữ liệu level) |
| bỏ | `Clear` để dùng lại | chủ dựng bản mới mỗi level, vài object |
| bỏ | `LoopLength` không static | tool soạn level tính vòng không dựng hàng |
| bỏ | chặn trục `Z` | `localPosition[2]` chạy đúng |
| sau | `Count`, `Length` thành `public` | khi chủ thứ hai cần đọc; giờ chỉ test đọc |
| sau | tốc độ riêng từng vật | khi có hàng cố ý trượt lệch nhịp — khác hợp đồng "khe giữ nguyên" nên là class khác |
