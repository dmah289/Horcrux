# Plan — `ConveyorLoop`: hàng vật trượt quay vòng giữ nguyên khe

Ngày 2026-11-23. Nguồn: category-jam `Assets/_CategoryJam/Scripts/Entities/Shelf/ShelfMover.cs` (`LoopLength`, `WrapIntoLoop`,
`RegisterBounds`), `Tests/EditMode/ShelfMoverTests.cs`, đã chơi thử. Một hàng vật dài ngắn khác nhau trượt cùng nhịp trên một trục;
vật qua biên lùi đúng một vòng nên khe giữa các vật giữ nguyên mãi.

**Phân công:** GĐ1–2 developer gõ, agent viết test theo bảng case rồi compile · GĐ3 (tài liệu) · GĐ4 (category-jam chuyển sang bản
Horcrux, xoá `ShelfMover`, xoá Plan) agent làm.
**Chạy được, thấy được:** GĐ1–2 compile sạch + bảng case xanh trong `Tests/EditMode/ConveyorLoopTests.cs`; thấy trong game ở GĐ4.
**Lệnh kiểm** (chạy từ gốc dự án): `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.EditModeTests.csproj --tests Horcrux.EditModeTests`.
**Commit:** GĐ1–3 trong `Assets/Horcrux`; GĐ4 ở dự án kèm con trỏ submodule.
**Phụ thuộc:** không. **Chung file:** `Runtime/AssemblyInfo.cs` — `Horcrux_SpriteFill_Plan.md` GĐ1 cũng cần dòng
`InternalsVisibleTo("Horcrux.EditModeTests")`; Plan gõ trước tạo file, Plan sau thấy dòng đã có thì bỏ qua.
**Đã kiểm trước khi ghi:** `ConveyorLoop.cs` sau GĐ1 và sau GĐ2, cùng `AssemblyInfo.cs`, qua `csc -langversion:9` trên
`UnityEngine.CoreModule` của Editor 6000.0.73f1 và `com.horcrux.runtime.dll` (cho `AxisType`); assembly tên `Horcrux.EditModeTests`
đọc được `Count`, `Length`, assembly tên khác báo CS1061 (đối chứng `InternalsVisibleTo`). Bản mẫu C# cùng thuật toán chạy ba hàng của
bảng GĐ1 (case 2, 4, 5), tổng 8051 bước, mỗi hàng 3 vòng: lệch vị trí vòng so với vật 0 tối đa 4·10⁻⁴, khe mép nhỏ nhất của hàng xếp
sát 0.5999 (đặt 0.6); đối chứng lấy vòng = n × khoảng tâm trung bình cho hàng đó ra khe nối −0.54 (đè nhau). `§0`: có — độ dài vòng là
công thức suy ra, không hiển nhiên.

## Ngữ cảnh đã chốt

- **Người dùng:** hàng vật trượt vòng trên một trục, vật là con cùng một cha: kệ trượt trong game sort, hàng trộn vật dài và vật ngắn ·
  băng chuyền món trong game merge · tấm nền parallax rộng khác nhau · đoàn xe chạy vòng.
- **Mục tiêu:** một class thuần giữ thành viên, biên, độ dài vòng của **một** hàng; chủ (dự án) giữ một bản mỗi hàng và gọi
  `Advance` mỗi frame. Hành vi trùng bản dự án ở mọi frame.
- **Ranh giới:** gom vật thành hàng, quyết quãng mỗi frame, quyết lúc dừng là việc của chủ. `ConveyorLoop` chỉ dời và lùi vòng.
- **Chỗ đặt, tên:** `Runtime/Utilities/Layout/ConveyorLoop.cs`, namespace `Horcrux.Runtime.Utilities.Layout`, assembly
  `com.horcrux.runtime` (không thêm reference). Trục dùng `Horcrux.Runtime.Utilities.Common.AxisType` có sẵn. Tên đổi tuỳ ý trước khi gõ.
- **Cố ý không làm:** tốc độ riêng từng vật — khe chỉ giữ được khi cả hàng chung nhịp; caller nhân tốc độ vào quãng · gom vật theo hàng
  — luật gom thuộc dự án (theo toạ độ làm tròn, theo dữ liệu level) · `Clear` để dùng lại — chủ dựng bản mới mỗi level, vài object.
- **Quyết định trái trực giác:**
  - `Remove` **không** tính lại biên và vòng: vật còn lại giữ khe, chỗ vật rời thành ô trống chạy theo hàng. Tính lại từ chỗ đứng
    giữa chừng là dựng vòng trên vị trí đã trượt, vật đè nhau.
  - Biên và vòng tính từ chỗ đứng **lúc `Add`**; thêm sau lần `Advance` đầu là lỗi setup, `LogError` rồi bỏ qua.
  - Một lần lùi vòng mỗi `Advance` (`if`, không `while`): quãng một frame nhỏ hơn vòng nhiều bậc (vòng ≥ hai bề ngang màn hình).
  - Hàng dưới hai vật đứng yên: một vật không có khoảng cách tới vật kế nên không có vòng.
- **Mở rộng sau:** tốc độ riêng từng vật khi có hàng cố ý trượt lệch nhịp — khác hợp đồng "khe giữ nguyên" nên là class khác ·
  `Count`, `Length` thành `public` khi chủ thứ hai cần đọc (giờ `internal`, chỉ test đọc).

## Đã khảo sát

| Nguồn | Lấy | Không lấy, vì sao |
|---|---|---|
| category-jam `ShelfMover.cs` (bản tạm) | `LoopLength` nguyên văn · luật lùi đúng một vòng giữ phần vượt · biên từ chỗ đứng lúc đăng ký · `Unregister` không tính lại | hai `static Dictionary` gom hàng: state static sống qua lần Play khi tắt domain reload, gom là luật dự án · vận tốc riêng từng kệ · `_spacingOffset`: cả hai prefab đều 0 · `OnDrawGizmos` vẽ biên từ registry static: hết registry thì chủ tự vẽ nếu cần |
| category-jam `ShelfMoverTests.cs` | năm case làm bảng case GĐ1 | — |
| category-jam `EntitiesManager`, `LevelLoader`, `Shelf` | thứ tự: đặt `localPosition` → đăng ký; kệ trống trả pool sau khi đăng ký cả hàng | — |
| category-jam `FallingShelfColumns.cs` | khuôn chủ: class thuần do `EntitiesManager` sở hữu, dựng ở `LevelLoader`, `Clear` lúc dispose | — |
| category-jam `_GameDesign` `MovingRowCheck.cs` | caller thứ hai của `LoopLength` (tool level) — lý do giữ hàm static public | — |
| category-jam dữ liệu level (`Content/Levels/Source/Base_*.asset`, 134 file) | 2842 ô đều `speed: 0` (game đọc thành 1); `motion` chỉ 0/2/3; 26 level có kệ trượt, mỗi level một hàng, không hàng nào trộn chiều 2 và 3 | — |
| Horcrux `Runtime/Utilities/Common/AxisType.cs` | enum trục `X, Y, Z`, ép `int` ra đúng chỉ số `Vector3` | enum trục riêng: trùng thứ đã có |
| Horcrux `Runtime/Utilities/`, `Plans/` | — | chưa có cơ chế vòng |

## Luồng — một hàng qua một level

```
chủ đặt localPosition các vật ──► new ConveyorLoop(axis, margin) ──► Add(vật, nửa bề dài) từng vật      GĐ1
        ▼                                     └─ mỗi Add: biên min/max + Length tính lại từ mọi vật
mỗi frame chủ gọi Advance(quãng có dấu) ──► mọi vật dời; vật qua max + margin (min − margin) lùi một vòng   GĐ1
        ▼
vật rời hàng giữa chừng ──► Remove: biên, vòng giữ nguyên · Add sau Advance ──► LogError, bỏ qua         GĐ2
        ▼
tài liệu (GĐ3) · category-jam chuyển sang bản Horcrux, xoá ShelfMover (GĐ4)
```

## Bản đồ hệ cuối

```
chủ (dự án: gom vật theo hàng, quyết quãng mỗi frame, quyết khi nào dừng)
   │ new(AxisType, margin) · Add · Remove · Advance
   ▼
ConveyorLoop ── _members, _halfLengths (song song theo chỉ số) · _min, _max · Length, Count (internal, test đọc)
   ├─ RebuildBounds (mỗi Add) ──► LoopLength(vị trí, nửa bề dài)       static public, tool level gọi thẳng
   └─ Advance ──► WrapIntoLoop từng vật ──► localPosition[trục]
```

## §0 — Độ dài một vòng

**Hiện tượng.** Hàng là một băng chuyền khép kín: vật ra khỏi đầu này hiện lại ở đầu kia. Muốn khe giữa mọi cặp vật kề nhau giữ
nguyên qua mọi vòng thì vật lùi đúng **một vòng** `L`, và chỗ nối (vật cuối → vật đầu khi quay về) cũng phải là một khe "bình
thường" của hàng.

| Ký hiệu | Nghĩa |
|---|---|
| $x_i$ | toạ độ tâm vật $i$ theo trục, $n$ vật |
| $h_i$ | nửa bề dài vật $i$ theo trục |
| $f, l$ | vật đầu (toạ độ nhỏ nhất), vật cuối (lớn nhất) |
| $S = x_l - x_f$ | khoảng tâm từ đầu tới cuối |

① Vòng = đoạn từ tâm vật đầu tới tâm vật cuối, cộng đoạn nối từ tâm vật cuối vòng về tâm vật đầu:
$L = S + h_l + g + h_f$, với $g$ là khe mép ở chỗ nối.

② Tổng khe mép của $n - 1$ cặp kề nhau = khoảng tâm trừ phần vật chiếm. Vật giữa góp hai nửa bề dài, hai vật đầu mỗi vật một:
$$G = S - \left(2\sum_i h_i - h_f - h_l\right)$$

③ Chỗ nối lấy khe mép **trung bình**: $g = G / (n - 1)$. Lấy khoảng cách tâm trung bình thì sai khi vật khác cỡ — vật ngắn xen vật
dài kéo tâm trung bình hẹp lại, hai vật dài ở hai đầu đè nhau ở chỗ nối (đối chứng ở dòng "Đã kiểm": khe nối −0.54).

$$L = S + h_f + h_l + \frac{G}{n - 1}$$

| Mốc | Kỳ vọng | ✓ |
|---|---|---|
| mọi vật cùng $h$, tâm cách đều $d$ | $L = n \cdot d$ (hàng một loại vật quay như lưới đều) | GĐ1 case 1 |
| cùng $h$, cách không đều | $L = n \cdot S / (n - 1)$: $n$ lần khoảng tâm trung bình | GĐ1 case 1 |
| N N S N N S N N xếp sát, mép cách đều 0.6 | $g = 0.6$, $L = 48$ | GĐ1 case 2, bản mẫu |
| $n < 2$ | không có vòng, $L = 0$ | GĐ1 case 3 |

---

## Giai đoạn 1 — Một hàng trượt quay vòng

**Mục tiêu:** chủ dựng loop, `Add` từng vật đã đứng đúng chỗ, gọi `Advance` mỗi frame; mọi vật trượt cùng nhịp, khe giữa mọi cặp kề
nhau giữ nguyên sau nhiều vòng, kể cả frame lệch nhịp. Xong là một hàng trượt chạy được trên bản Horcrux.

**Vì sao bây giờ:** đây là lõi; mọi thứ khác (rời hàng, guard) chỉ có nghĩa khi hàng đã chạy.

**Sơ đồ sau GĐ1** (`+` là phần mới)

```
  chủ ──new+(AxisType, margin) · Add+ · Advance+──► ConveyorLoop+ ──localPosition[trục]──► vật
                                                      └─ RebuildBounds+ ──► LoopLength+ (static)
  test ──Count+ · Length+ (internal)──► ConveyorLoop
```

**Toán → code:** §0 nằm trọn trong `LoopLength` — `edgeGapSum` là $G$, giá trị trả về là $L$. Luật lùi vòng ở `WrapIntoLoop`:
vật đi chiều dương qua `_max + _margin` thì trừ `Length`, chiều âm qua `_min − _margin` thì cộng. **Trừ đúng một vòng, giữ phần
đã vượt** — đặt vật về một điểm cố định làm vật hụt phần vượt mỗi lần quay vòng, frame giật hụt nhiều, lệch cộng dồn tới khi đè
vật kề.

### `Runtime/AssemblyInfo.cs` — file mới (bỏ qua nếu đã có dòng này)

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Horcrux.EditModeTests")]
```

### `Runtime/Utilities/Layout/ConveyorLoop.cs` — file mới

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

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản này, vì sao |
|---|---|---|
| ai giữ hàng | hai `static Dictionary` trong `ShelfMover`, khoá là toạ độ làm tròn | một instance mỗi hàng, chủ giữ: không state static, luật gom ở dự án |
| bước trượt | mỗi vật tự `Step`, vận tốc riêng | `Advance` dời cả hàng một quãng: khe chỉ giữ được khi chung nhịp, nên nhịp chung là cấu trúc, không là kỷ luật |
| chiều lùi vòng | theo enum chuyển động của kệ | theo dấu quãng: framework không biết từ vựng chuyển động của dự án |
| trục | enum chuyển động của dự án | `AxisType` của Horcrux |
| `_spacingOffset` cộng vào vòng | ô Inspector, cả hai prefab đều 0 | bỏ: không có call site khác mặc định |

**Consumes / Produces:** consumes `AxisType`, `Transform.localPosition` · produces `ConveyorLoop(AxisType, float)`,
`Add(Transform, float)`, `Advance(float)`, `static LoopLength(IReadOnlyList<float>, IReadOnlyList<float>)`; `internal` `Count`,
`Length`.

**Quyết định:** `Count`, `Length` là `internal` — chỉ test đọc; public cho chủ không ai gọi · `LoopLength` static public — tool level
của dự án tính vòng không dựng hàng · `AxisType` có cả `Z`: `localPosition[2]` chạy đúng, không chặn.

**Editor setup:** không.

**Kiểm:** lệnh kiểm ở đầu Plan. Agent viết `Tests/EditMode/ConveyorLoopTests.cs` (vật là `GameObject` tạm con một root,
`DestroyImmediate` ở `TearDown`; `STEP = 0.05`, sai số khe `0.01`; "khoảng cách vòng" của vật i = `(x_i − x_0) mod Length`):

| # | Case | Input | Kỳ vọng |
|---|---|---|---|
| 1 | vòng hàng một loại, cách không đều | x {−21, −14, 0, 7, 14}, h 3.2 | `LoopLength` = 5 × 35 / 4 = 43.75 |
| 2 | chỗ nối hàng xếp sát giữ khe mép | N N S N N S N N tại {−24, −17, −12, −7, 0, 5, 10, 17}, N h 3.2, S h 1.2, quãng âm 3 vòng | mỗi bước: khe mép mọi cặp kề theo vòng = 0.6 |
| 3 | dưới hai vật | 0 vật, 1 vật | `LoopLength` = 0; `Advance(1)` không dời vật |
| 4 | hàng trộn cỡ giữ khe sau nhiều vòng | N S N S N tại {−24, −12, 0, 12, 24}, quãng dương 3 vòng | khoảng cách vòng từ vật 0 tới mọi vật không đổi |
| 5 | frame lệch nhịp | 5 vật h 3.2 cách 7, `System.Random(7)`: bước `STEP × [0.7, 1.3]`, 1/40 bước là 1 | như case 4 |
| 6 | trục Y | 3 vật h 1 trên y {−7, 0, 7}, `AxisType.Y`, quãng âm 3 vòng | x không đổi; khoảng cách vòng theo y không đổi |

Case 2 đỏ khi chỗ nối lấy khoảng tâm trung bình (khe nối −0.54); case 5 đỏ khi lùi vòng đặt vật về điểm cố định.

**Mốc commit:** `feat(layout): ConveyorLoop — hàng trượt quay vòng giữ khe` (Horcrux).

---

## Giai đoạn 2 — Vật rời hàng giữa chừng, thêm sai thời điểm

**Mục tiêu:** chủ bỏ một vật khỏi hàng (vật trống trả về pool lúc dựng level): vật còn lại chạy tiếp, giữ khe, chỗ trống chạy theo
hàng. Thêm vật sau khi hàng đã chạy thì Console báo đỏ, hàng không đổi.

**Vì sao bây giờ:** category-jam trả kệ trống về pool **sau** khi đăng ký cả hàng (`LevelLoader.FinalizeLevelSetup`); thiếu
`Remove` thì GĐ4 không chuyển được.

**Sơ đồ sau GĐ2**

```
  chủ ──Add (guard+ _hasAdvanced) · Advance (đánh dấu+) · Remove+──► ConveyorLoop ──► vật
                                                       Remove+: bỏ khỏi _members, _halfLengths; biên, Length giữ
```

### `ConveyorLoop.cs` › region `Properties` › sau `private float _max;`

```csharp
        private float _max;
        private bool _hasAdvanced;
```

### `ConveyorLoop.cs` › `Add` — thêm guard đầu hàm

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

### `ConveyorLoop.cs` › `Advance` — đánh dấu sau guard số vật

```csharp
            if (_members.Count < 2) return;
            _hasAdvanced = true;
```

### `ConveyorLoop.cs` › region `API` › sau `Advance`

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
            _halfLengths.RemoveAt(index);
        }
```

**Consumes / Produces:** consumes GĐ1 · produces `Remove(Transform)`.

**Quyết định, phương án bỏ:** tính lại biên khi `Remove` — bỏ, vì vị trí lúc đó đã trượt (lý do ở "Ngữ cảnh đã chốt") · `Remove`
vật lạ thì `LogError` — bỏ, vì chủ trả mọi vật trống về pool, kể cả vật không trượt; bỏ qua là hợp đồng.

**Editor setup:** không.

**Kiểm:** bảng case thêm vào `ConveyorLoopTests.cs`:

| # | Case | Input | Kỳ vọng |
|---|---|---|---|
| 7 | rời giữa hàng | 5 vật như case 4, `Remove` vật 2 trước khi chạy, 3 vòng | `Length` không đổi; khoảng cách vòng giữa 4 vật còn lại không đổi |
| 8 | rời còn một vật | 2 vật, `Remove` một, `Advance(1)` | vật còn lại đứng yên |
| 9 | thêm sau khi chạy | 2 vật, `Advance(0.1)`, `Add` vật thứ ba | `LogAssert.Expect(LogType.Error, …)`; `Count` = 2 |
| 10 | bỏ vật lạ | 2 vật, `Remove` vật không thuộc hàng | `Count` = 2, không log |

**Mốc commit:** `feat(layout): ConveyorLoop.Remove, chặn Add sau khi chạy` (Horcrux).

---

## Giai đoạn 3 — Tài liệu (agent)

`docs/Runtime/ConveyorLoop/ConveyorLoop.md` (`Code: Runtime/Utilities/Layout/ConveyorLoop.cs`; luồng, §0 rút gọn, API, quyết định
trái trực giác, hai bẫy: đặt về điểm cố định khi quay vòng · vòng lấy khoảng tâm trung bình) và `ConveyorLoop.html` chọn từ `.md`.
Tên trung tính, không tên dự án.

**Mốc commit:** `docs(layout): ConveyorLoop` (Horcrux).

---

## Giai đoạn 4 — category-jam dùng bản Horcrux, xoá `ShelfMover`, xoá Plan (agent)

**Mục tiêu:** kệ trượt chạy y như trước trên `ConveyorLoop`; `grep ShelfMover` không còn gì; registry static biến mất.

**Chủ mới** `Assets/_CategoryJam/Scripts/Director/ShelfConveyorRows.cs` — class thuần do `EntitiesManager` sở hữu, khuôn
`FallingShelfColumns`:

| Member | Làm gì |
|---|---|
| `const float MARGIN = 2f` | biên khuất, thay `ShelfMover.OFFSET` (tool level đọc chung) |
| `Add(Transform shelf, ShelfMotion motion, float speed, Vector2 halfExtents)` | trục (`AxisType`) và dấu từ `motion`, khoá hàng = `(trục, RoundToInt(toạ độ ngang trục))`; hàng chưa có thì `new ConveyorLoop(trục, MARGIN)` và nhận `speed`, dấu của kệ đầu; nhớ kệ → hàng |
| `Remove(Transform shelf)` | tra kệ → hàng, `loop.Remove` |
| `Advance(float distanceScale)` | mỗi hàng `loop.Advance(dấu × speed × distanceScale)` |
| `Clear()` | bỏ mọi hàng |

**Chỗ đổi**

| File › chỗ | Đổi |
|---|---|
| `EntitiesManager` › `_movers` | → `private readonly ShelfConveyorRows _conveyorRows = new();` + `public ShelfConveyorRows ConveyorRows => _conveyorRows;` |
| `EntitiesManager.Update` | thoát khi `!Director.LevelLoaded \|\| Director.IsBoosterBeingUsed`; `_conveyorRows.Advance(distanceScale)` (giữ `SHELF_SPEED_SCALE`) |
| `EntitiesManager.CreateShelf` | bỏ `_movers.Add(shelf.Mover)` |
| `EntitiesManager.HandleLevelDisposed` | `_movers.Clear()` → `_conveyorRows.Clear()` |
| `LevelLoader.InstantiateShelves` | sau hai vòng `PrepareForLevel`: mỗi kệ trượt `entitiesManager.ConveyorRows.Add(shelf.transform, shelf.Motion, shelf.Speed, shelf.Capacity == 1 ? LevelBoundsFitter.SINGLE_SHELF_CELL_EXTENTS : LevelBoundsFitter.SHELF_CELL_EXTENTS)` |
| `LevelLoader.FinalizeLevelSetup` › vòng trả kệ trống | `entitiesManager.ConveyorRows.Remove(shelf.Item1.transform)` ngay trước `Return` |
| `Shelf` | bỏ ô `mover`, `Mover`, `mover.Begin` trong `PrepareForLevel`, `mover.Stop` trong `OnReturnToPool` |
| `_GameDesign/Script/LevelEditor/Shelf/MovingRowCheck.cs`, `_GameDesign/Tests/EditMode/MovingRowCheckTests.cs`, `MovingRowLayoutTests.cs` | `ShelfMover.LoopLength` → `ConveyorLoop.LoopLength`; `ShelfMover.OFFSET` → `ShelfConveyorRows.MARGIN`; comment nhắc `ShelfMover` viết lại theo tên mới |
| `_GameDesign/Tests/EditMode/CategoryJam.GameDesign.Tests.asmdef` › `references` | thêm `"com.horcrux.runtime"`: hai file test trên gọi thẳng `ConveyorLoop.LoopLength`, reference assembly không bắc cầu |
| `_GameDesign/Script/LevelEditor/Shelf/MovingRowAnalysis.cs`, `MovingRowLayout.cs`, `_GameDesign/Script/LevelEditor/Window/CellTab.cs`, `_GameDesign/Tests/EditMode/MovingZoneTests.cs` | chỉ comment nhắc `ShelfMover` → `ShelfConveyorRows` / `ConveyorLoop` |
| `Tests/EditMode/ShelfMoverTests.cs` | → `ShelfConveyorRowsTests.cs`: giữ `LoneSingleInRow_MovesWithRow` (kệ 1 ô lẻ chạy theo hàng), thêm hai case gom hàng mới — hai hàng khác y chạy độc lập · `RightToLeft` đi chiều âm. Case vòng, khe đã ở Horcrux |
| `Entities.md` | cây `Shelf` bỏ `ShelfMover`; đoạn băng chuyền trỏ `ShelfConveyorRows` + Horcrux `ConveyorLoop`; hai bẫy quay vòng chuyển sang tài liệu Horcrux, ở đây trỏ sang; dòng "NormalShelf và NormalMovingShelf chỉ khác đúng mảnh này" sửa theo prefab sau bước 1 |
| `Assets/Horcrux/Plans/Horcrux_ConveyorLoop_Plan.md` + `.meta` | xoá |

**Editor setup** (thứ tự bắt buộc — xoá script trước thì prefab giữ một script missing):

1. Lúc `ShelfMover.cs` còn compile: gỡ component `ShelfMover` khỏi `Addressables/Shelf/NormalMovingShelf.prefab` và
   `SingleMovingShelf.prefab` (sửa YAML từ bản đã commit: dòng `- component:` và khối `--- !u!114`, diff chỉ còn đúng hai khối).
2. Xoá `ShelfMover.cs` và `.meta`, rồi bỏ ô `mover` khỏi `Shelf`.

Sau bước 1, prefab kệ thường và kệ trượt (cùng cỡ) giống hệt nhau; gộp hay giữ do developer quyết, Plan không đổi prefab nào khác.

*Tương đương hành vi:* thuật toán dời nguyên văn. Khác: tốc độ và chiều chung cả hàng lấy từ kệ đầu — dữ liệu hiện tại mọi ô
`speed: 0` (đọc thành 1) và không hàng nào trộn chiều (soát 134 level ở "Đã khảo sát"), nên không ô nào đổi.

**Kiểm:** `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj CategoryJam.Gameplay.csproj,CategoryJam.Editor.csproj,CategoryJam.GameDesign.csproj,CategoryJam.EditModeTests.csproj,CategoryJam.GameDesign.Tests.csproj --tests CategoryJam.EditModeTests,CategoryJam.GameDesign.Tests,Horcrux.EditModeTests`
xanh · `grep -r ShelfMover Assets/_CategoryJam Assets/_GameDesign` rỗng.

**Chơi thử (developer):** vào level có hàng kệ trượt trộn kệ 3 ô và 1 ô, cả hai chiều · nhìn hàng chạy hết ít nhất hai vòng ·
khoảng cách giữa các kệ không đổi, chỗ nối hàng không đè, kệ trống bị trả về pool lúc vào level để lại ô trống chạy theo hàng
như trước · dùng booster thì hàng dừng như trước · dấu hiệu hỏng: kệ đứng yên, hai kệ đè nhau, kệ nhảy giữa màn hình.

**Mốc commit:** `refactor(shelf): kệ trượt chạy trên Horcrux ConveyorLoop, xoá ShelfMover` (dự án, kèm con trỏ submodule).

---

## File → GĐ

| File | GĐ |
|---|---|
| `Runtime/AssemblyInfo.cs` (nếu chưa có) | 1 |
| `Runtime/Utilities/Layout/ConveyorLoop.cs` | 1, 2 |
| `Tests/EditMode/ConveyorLoopTests.cs` | 1, 2 |
| `docs/Runtime/ConveyorLoop/ConveyorLoop.md`, `.html` | 3 |
| category-jam `ShelfConveyorRows.cs`, `EntitiesManager.cs`, `LevelLoader.cs`, `Shelf.cs`, `ShelfMover.cs` (xoá), hai prefab kệ trượt, `MovingRowCheck.cs`, `MovingRowCheckTests.cs`, `MovingRowLayoutTests.cs`, `CategoryJam.GameDesign.Tests.asmdef`, `MovingRowAnalysis.cs`, `MovingRowLayout.cs`, `CellTab.cs`, `MovingZoneTests.cs`, `ShelfMoverTests.cs` → `ShelfConveyorRowsTests.cs`, `Entities.md`; Plan này (xoá) | 4 |
