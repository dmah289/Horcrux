# Plan — Độ khó theo tiến độ: `ProgressCurve`, `LevelTypeTable`, `RandomPick` lên Horcrux

Ngày 2026-11-06. Nguồn: category-jam, `Assets/_CategoryJam/Scripts/Config/LevelDifficulty.cs` (`DifficultyCurve.PointAt`),
`Assets/_CategoryJam/Scripts/Utilities/Extensions/LegacyHelpers.cs` (`LegacyListExtensions.GetRandomFromListWithProrityPredicates`,
`GetRandomFromListReseeded`), `Assets/_CategoryJam/Scripts/Utilities/Extensions/ListExtensions.cs` (`GetRandomFromList`); mahjong-jam
(`D:\game\iKame\mahjong-jam`), `Assets/_Assets/Scripts/Config/ABTests/ABTestManager.LevelDifficulty.cs`
(`LevelDifficultAssignmentConfigCollection<T>.GetDifficultType`) và `Assets/_Assets/Scripts/Config/Base/LevelDifficultConfigSO.cs`
(`GetCurrentConfigBase`). Ba cơ chế thuần, độc lập: giá trị theo mốc % tiến độ · level → loại · chọn ngẫu nhiên trong list theo chuỗi
predicate ưu tiên. Phần chấm điểm theo tile chặn (`GameDifficultyManager`) và SO độ khó (`LevelDifficultyProfile`) ở lại dự án.

**Phân công:** GĐ1–GĐ4 (Horcrux, code runtime) developer gõ, sau mỗi giai đoạn agent viết test của giai đoạn đó theo bảng case rồi
compile · GĐ5 (tài liệu) và GĐ6 (category-jam chuyển sang bản Horcrux, xoá bản tạm) agent làm.

**Chạy được, thấy được ở GĐ1–GĐ4** = `com.horcrux.runtime` compile sạch và bảng case của giai đoạn xanh trong `Tests/EditMode/`;
Horcrux không có host nên "thấy trong game" là GĐ6. Horcrux là submodule: GĐ1–GĐ5 commit **trong `Assets/Horcrux`**, GĐ6 commit ở repo
dự án kèm con trỏ submodule.

Không phụ thuộc Plan nào; `Plans/SystemPlan.md` §15 Level Library trỏ sang `LevelTypeTable` của Plan này. Không có lõi toán, không có `§0`.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | game có bảng độ khó do designer chỉnh, có thể tới từ remote config: mỗi loại một curve "giá trị theo % tiến độ", level → loại; và mọi chỗ chọn ngẫu nhiên một phần tử trong list theo luật ưu tiên. **category-jam** dùng `ProgressCurve` (một curve trong mỗi SO độ khó) và `RandomPick` (5 call site); level → loại là tham chiếu SO gắn thẳng trong level, không tra bảng. **mahjong-jam** dùng cả ba: bảng level → loại nhập từ CSV vào remote config, curve bậc thang mốc mã hoá, chọn theo chuỗi predicate; mahjong-jam chuyển sang bản Horcrux là việc riêng ngoài Plan này |
| Mục tiêu | ba lõi thuần, test không dựng gì; **không đổi kết quả ngẫu nhiên** của dự án khi chuyển (NT6) |
| Chỗ đặt | `Runtime/Utilities/Progression/ProgressCurve.cs`, `LevelTypeTable.cs` (namespace `Horcrux.Runtime.Utilities.Progression`) · `Runtime/Utilities/Picking/RandomPick.cs` (namespace `Horcrux.Runtime.Utilities.Picking`; không đặt `Random` vào tên namespace để `UnityEngine.Random` trong Horcrux không bị che) |
| Từ vựng | **framework không đặt tên field dữ liệu**: step, entry là struct hay class của dự án, khớp key JSON remote config (`pointPerFreeSlot`, `levelNumber`, `type` là wire format với server). Horcrux khai **interface hai getter** và hàm generic ràng buộc theo interface; dự án implement là xong, struct không boxing nhờ lời gọi generic có ràng buộc |
| Nguồn ngẫu nhiên | **nhận `Func<int, int> nextIndex` từ caller** (`count → [0, count)`), developer chốt 2026-11-02: dự án truyền `UnityEngine.Random.Range` để giữ nước đi đã author; test truyền `System.Random` để tất định |
| Tên | `ProgressCurve.ValueAt`, `LevelTypeTable.TypeOf`, `RandomPick.PickRandom`, `RandomPick.PickByPriority`. Developer đổi tuỳ ý trước khi gõ |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Class `ProgressCurve` serialize sẵn `List<ProgressStep>` | tên field là wire format với remote config của từng game; framework đặt tên là ép dự án đổi key JSON. Dự án giữ class data, Horcrux giữ thuật toán |
| Tra curve theo loại với fallback | cặp "loại → curve" là dữ liệu của dự án: category-jam gắn SO độ khó thẳng vào level, mahjong-jam `configs.Find(c => c.Type == type)` vài dòng; ở lại |
| Nội suy giữa hai mốc | luật của người dùng là **bậc thang**: mốc i có hiệu lực khi tiến độ vượt quá mốc i; nội suy là đổi hành vi |
| `Shuffle`, `KShuffle`, `GetRandomElements` | `Shuffle` không caller; `KShuffle` ba caller trong `LevelLoader` xáo tại chỗ theo seed, là ca khác (hoán vị, không chọn); `GetRandomElements` một caller dùng `OrderBy(Guid.NewGuid())` — đổi cách rút là đổi 50 level đã author |
| Nguồn ngẫu nhiên mặc định (`UnityEngine.Random`) khi caller không truyền | Horcrux không chọn nguồn ngẫu nhiên; tham số bắt buộc để test luôn tất định và dự án luôn thấy mình đang dùng nguồn nào |

**Quyết định trái trực giác** — lý do viết ở giai đoạn sinh ra nó: mốc i có hiệu lực khi tiến độ **vượt quá** mốc i, đúng tại mốc vẫn
là mốc trước (GĐ1) · `ValueAt` không guard `steps` rỗng (GĐ1) · `TypeOf` thiếu level: `LogError` rồi trả `fallback` caller truyền (GĐ2)
· `PickRandom` list rỗng trả `default`, không gọi `nextIndex` (GĐ3) · pass k của `PickByPriority` lọc phần tử thoả **mọi** predicate từ
k tới cuối (GĐ4) · `MatchBuffer<T>` static dùng lại, `Clear` sau khi chọn (GĐ4).

**Mở rộng sau**

- Mốc theo đơn vị khác % (số lượt, giây): `ValueAt` nhận `float progress` và mốc là `float`; hiện mọi người dùng là % nguyên.
- `StepAt<TStep>` trả cả mốc thay vì một `float`: mahjong-jam `GetCurrentConfigBase` cần min/max box point của cùng một mốc; thêm
  hàm, `ValueAt` gọi nó. Ngoài Plan vì category-jam chỉ cần một giá trị.
- mahjong-jam chuyển sang bản Horcrux: bảng level → loại qua `ILevelTypeEntry`, mốc mã hoá giải mã trong getter của `IProgressStep`,
  `GetRandomFromListWithProrityPredicates` bản LINQ → `PickByPriority` với `nextIndex` reseed mỗi lần như bản của nó.

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| category-jam `Config/LevelDifficulty.cs` (bản 2026-11-05) | thuật toán `DifficultyCurve.PointAt` | `LevelDifficultyType`, `DifficultyStep`, `DifficultyCurve`, `LevelFallingConfig`, `LevelDifficultySelection.Resolve`: từ vựng dự án và wire format |
| category-jam `Config/LevelDifficultyProfile.cs`, `Level/LevelInfo.difficulty`, `Director/GameDirector.StartLevelAsync` | cách level → độ khó: SO gắn trong level, thiếu thì `GameplayConfig.defaultDifficulty` qua `LevelDifficultySelection.Resolve` | không có bảng level → loại để tra |
| category-jam `Utilities/Extensions/LegacyHelpers.cs`, `ListExtensions.cs` | `GetRandomFromListWithProrityPredicates` (hai vòng lặp, `MatchBuffer<T>`), `GetRandomFromList` | `GetRandomFromListReseeded` (`InitState` mỗi lần là quyết định của dự án, đi vào `nextIndex` của nó; chỉ `GetRandomFromListWithProrityPredicates` gọi) · `KShuffle`, `Shuffle`, `GetRandomElements`, `GetRandomElementsInGroups`, `DivideEqually`, `GetRandomFromArray` |
| category-jam call site | `GameDifficultyManager.CalculateTargetPoint` và chuỗi debug gọi `_curve.PointAt(LevelProgress)` · `GameDifficultyManager.GetNextBoxTileId` gọi `GetRandomFromListWithProrityPredicates` với 9 predicate · `AutoPlayer` (3 chỗ) và `EntitiesManager.GetHighestTile` gọi `GetRandomFromList` | — |
| category-jam `Tests/EditMode/DifficultyCurveTests.cs`, `PriorityPredicatePickTests.cs`, `DifficultyProfileAssetTests.cs` | bảng case của curve, bản LINQ gốc (`TakeLast + Where + All`) làm oracle; test SO chặn `steps` rỗng và mốc không tăng | — |
| mahjong-jam `ABTestManager.LevelDifficulty.cs` | `GetDifficultType`: `configs.Find(x => x.levelNumber == levelNumber)`, thiếu thì enum nhỏ nhất, không log; `Validate` (Editor) bắt level thiếu và trùng | Odin, import CSV, ABTest |
| mahjong-jam `LevelDifficultConfigSO.cs` | `GetCurrentConfigBase`: cùng vòng lặp `>=` và `Clamp(i - 1)`, mốc giải mã bằng `IntegerEncryptor.Decode`; `items.Length == 0` trả config rỗng | mã hoá, trả cả mốc ("Mở rộng sau") |
| mahjong-jam `Dessentials/Extensions/DListExtensions.cs` | bản LINQ `TakeLast + Where + All` là nghĩa gốc của chuỗi ưu tiên; `GetRandomFromList` reseed mỗi lần | cấp phát mỗi pass |
| Horcrux `Runtime/Utilities/` | — | chưa có curve bậc thang, tra bảng level, pick trong list. `ExtensionMethods/GeometryHelper.RandomPointInAnnulus` và `AudioService` (pitch) dùng `UnityEngine.Random` trực tiếp: số thực liên tục, không chọn phần tử, ca khác |
| `Plans/SystemPlan.md` §15 Level Library | `IDifficultyTagProvider` là service bọc `LevelTypeTable.TypeOf` khi §15 được dựng | — |
| Unity `AnimationCurve` | — | curve liên tục với tangent; người dùng cần bậc thang và key JSON phẳng |

---

## Cơ chế A — `ProgressCurve`: giá trị theo % tiến độ

### Luồng mô tả

```
designer chỉnh bảng mốc (asset hay remote config), mỗi mốc (progressPercent, value) tăng dần
            │
            ▼
dự án đọc bảng vào List<TStep> của mình, TStep : IProgressStep            GĐ6 (category-jam)
            │
            ▼
mỗi lần cần giá trị (sau mỗi box xong): tiến độ % ──► ValueAt(steps, p)   GĐ1
            │                                           mốc i có hiệu lực khi p vượt quá mốc i
            ▼
giá trị bậc thang ──► công thức của dự án (target point của box kế)        ở lại dự án
```

### Bản đồ hệ cuối

```
  dự án: List<TStep> (field tên theo wire format) ──ValueAt(steps, p)──► ProgressCurve (static, không state)
         TStep : IProgressStep { ProgressPercent, Value }                   trả float Value của mốc đang hiệu lực
```

---

## Giai đoạn 1 — `ProgressCurve.ValueAt`

**Mục tiêu:** từ một list mốc tăng dần và một tiến độ %, trả giá trị của mốc đang hiệu lực. Xong giai đoạn này mọi dự án có bảng
"giá trị theo % tiến độ" tra được bằng một lời gọi.

**Vì sao bây giờ:** cơ chế A chỉ có một chặng; là lõi nhỏ nhất và không phụ thuộc hai cơ chế kia.

**Ai gọi:** test của giai đoạn; trong dự án là `DifficultyCurve.PointAt` (GĐ6).

### `Runtime/Utilities/Progression/ProgressCurve.cs` — file mới

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Progression
{
    /// <summary>Một mốc của curve bậc thang: từ khi tiến độ vượt quá ProgressPercent, Value có hiệu lực.</summary>
    public interface IProgressStep
    {
        int ProgressPercent { get; }
        float Value { get; }
    }

    /// <summary>Curve bậc thang theo % tiến độ; dự án giữ class dữ liệu (tên field là wire format), đây chỉ giữ cách tra.</summary>
    public static class ProgressCurve
    {
        /// <summary>
        /// Mốc i có hiệu lực khi tiến độ vượt quá mốc i; trước mốc đầu dùng mốc đầu, qua mốc cuối dùng mốc cuối.
        /// steps rỗng là lỗi dữ liệu, cửa nhận dữ liệu của dự án chặn; ở đây ném ArgumentOutOfRangeException.
        /// </summary>
        public static float ValueAt<TStep>(List<TStep> steps, float progressPercent) where TStep : IProgressStep
        {
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i].ProgressPercent >= progressPercent)
                    return steps[Mathf.Clamp(i - 1, 0, steps.Count - 1)].Value;
            }

            return steps[^1].Value;
        }
    }
}
```

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| `DifficultyCurve.PointAt` | method của class dữ liệu, đọc `steps[i].progressPercent`, `pointPerFreeSlot` | static generic, đọc qua `IProgressStep` | class dữ liệu và tên field ở lại dự án; vòng lặp, `>=`, `Clamp(i - 1)`, `steps[^1]` giữ nguyên văn |
| mahjong-jam `GetCurrentConfigBase` | `items.Length == 0` trả config rỗng; giải mã mốc trong vòng lặp | không guard; getter của `IProgressStep` giải mã | guard là nhánh không ai tới khi cửa dữ liệu đã chặn; giải mã là việc của struct dự án |

**Quyết định** — mốc i có hiệu lực khi tiến độ **vượt quá** mốc i, đúng tại mốc vẫn là mốc trước: hành vi V2 của mahjong-jam, test
`PointAt_TwoSteps_StepTakesEffectAfterItsProgress` của category-jam khoá (50 → mốc 0, 51 → mốc 1). Phương án bỏ: `>` thay `>=` — đổi
giá trị tại đúng mốc của mọi curve đã author. — Không guard `steps` rỗng: category-jam `DifficultyProfileAssetTests` bắt `steps` rỗng
và mốc không tăng trên từng SO độ khó, `LevelDifficultySelection.Resolve` bắt level thiếu SO; guard lần hai là nhánh không ai tới.

| Consumes | Produces |
|---|---|
| `UnityEngine.Mathf.Clamp` | `IProgressStep`, `ProgressCurve.ValueAt` |

**Sơ đồ tương tác sau GĐ1** (`+` là phần mới)

```
  dự án: List<TStep : IProgressStep+> ──ValueAt+──► ProgressCurve+ ──float──► công thức của dự án
```

**Editor setup:** không.

**Kiểm:** compile sạch. `Tests/EditMode/ProgressCurveTests.cs` khai `struct TestStep : IProgressStep` (hai field, hai getter), list
dựng tại chỗ.

| Case | Input | Kỳ vọng |
|---|---|---|
| hai mốc, đúng tại mốc | (0, 0), (50, 0.5); tiến độ 0, 49, 50, 51, 100 | 0, 0, 0, 0.5, 0.5 |
| một mốc | (0, 0.5); tiến độ 0, 50, 100 | 0.5 |
| tiến độ âm | (0, 0.25), (50, 1); −10 | 0.25 |
| tiến độ ngoài dải | (0, 0.25), (50, 1); 150 | 1 (mốc cuối) |
| mốc đầu khác 0 | (30, 1), (60, 2); tiến độ 0, 30, 31 | 1, 1, 2 (trước mốc đầu dùng mốc đầu) |
| curve 6 mốc của người dùng đầu | (0,0) (20,.5) (35,1) (50,1.5) (70,2) (80,2.5); tiến độ 20, 21, 35, 36, 50, 69, 80, 81, 100 | 0, .5, .5, 1, 1, 1.5, 2, 2.5, 2.5 |
| mốc không tăng dần | (50, 1), (0, 0); tiến độ 10 | 1 — mốc đầu `>=` 10 là mốc 0, clamp(−1) về mốc 0. Ghi ở tài liệu: bảng phải tăng dần, cửa nhận dữ liệu của dự án kiểm |
| rỗng | `[]`; tiến độ 10 | ném `ArgumentOutOfRangeException` tại `steps[^1]` |

**Mốc commit:** `feat(progression): ProgressCurve.ValueAt`.

---

## Cơ chế B — `LevelTypeTable`: level → loại

### Luồng mô tả

```
designer chỉnh bảng (levelNumber, type) — CSV, remote config
            │
            ▼
dự án đọc bảng vào List<TEntry>, TEntry : ILevelTypeEntry<TType>; enum loại là của dự án   (mahjong-jam, §15 — ngoài Plan)
            │
            ▼
vào level n: TypeOf(entries, n, fallback) ──► loại của level                                  GĐ2
            │                                 thiếu: LogError, trả fallback
            ▼
loại ──► dự án tra curve, preset theo loại                                                     ở lại dự án
```

### Bản đồ hệ cuối

```
  dự án: List<TEntry> ──TypeOf(entries, n, fallback)──► LevelTypeTable (static, không state) ──TType──► dự án
         TEntry : ILevelTypeEntry<TType> { LevelNumber, Type }
```

---

## Giai đoạn 2 — `LevelTypeTable.TypeOf`

**Mục tiêu:** từ bảng level → loại và một số level, trả loại; level không có trong bảng là lỗi dữ liệu, log rồi trả `fallback`. Xong
giai đoạn này lõi tra của `SystemPlan` §15 có sẵn.

**Vì sao bây giờ:** cơ chế B chỉ có một chặng, độc lập với A và C. Đặt sau A để hai file cùng thư mục `Progression/` ra đời liền nhau.

**Ai gọi:** test của giai đoạn. category-jam không có bảng level → loại nên GĐ6 không chạm cơ chế này; caller thật là mahjong-jam
(`LevelManager` gọi `GetDifficultType` bốn chỗ) khi chuyển, và `IDifficultyTagProvider` của §15.

### `Runtime/Utilities/Progression/LevelTypeTable.cs` — file mới

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Progression
{
    /// <summary>Một dòng của bảng level → loại; dự án giữ class dữ liệu và enum loại.</summary>
    public interface ILevelTypeEntry<TType> where TType : struct, Enum
    {
        int LevelNumber { get; }
        TType Type { get; }
    }

    /// <summary>Tra loại của một level. Dữ liệu có thể tới từ server nên log không kèm context object.</summary>
    public static class LevelTypeTable
    {
        /// <summary>Level không có trong bảng là lỗi dữ liệu: LogError rồi trả fallback (loại dễ nhất của game). Level trùng: dòng đầu thắng.</summary>
        public static TType TypeOf<TEntry, TType>(List<TEntry> entries, int levelNumber, TType fallback)
            where TEntry : ILevelTypeEntry<TType>
            where TType : struct, Enum
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].LevelNumber == levelNumber)
                    return entries[i].Type;
            }

            Debug.LogError($"[LevelTypeTable]: level {levelNumber} chưa có trong bảng, dùng {fallback}");
            return fallback;
        }
    }
}
```

`TEntry` không ràng `struct`: bảng của mahjong-jam là class. `TType` ràng `struct, Enum` để `$"{fallback}"` in tên enum và caller không
truyền được kiểu không phải enum.

**Khác với bản dự án**

| Chỗ | mahjong-jam `GetDifficultType` | Bản Horcrux | Vì |
|---|---|---|---|
| tìm | `configs.Find(x => x.levelNumber == levelNumber)` — lambda bắt `levelNumber`, cấp phát mỗi lần | vòng lặp chỉ số | gọi mỗi lần vào level, không rác; dòng đầu thắng như `Find` |
| thiếu | trả `Enum.GetValues(...).Min()` im lặng; `Validate` trong Editor bắt | `LogError` rồi trả `fallback` caller truyền | bảng từ server không có nút Validate; thiếu là lỗi setup không nổ nên phải log; "dễ nhất" là của từng game nên caller truyền |

**Quyết định** — `fallback` là tham số, không phải `default(TType)`: enum của game không chắc có giá trị 0 là "dễ nhất". Phương án
bỏ: `TryTypeOf(out)` để caller tự log — mọi caller thật đều làm cùng một việc (log, rơi về dễ nhất), hai dòng lặp ở mỗi caller.

| Consumes | Produces |
|---|---|
| `UnityEngine.Debug.LogError` | `ILevelTypeEntry<TType>`, `LevelTypeTable.TypeOf` |

**Sơ đồ tương tác sau GĐ2**

```
  dự án: List<TEntry : ILevelTypeEntry<TType>+> ──TypeOf+──► LevelTypeTable+ ──TType──► dự án tra curve theo loại
                                                                  └─ thiếu: LogError+, fallback
```

**Editor setup:** không.

**Kiểm:** compile sạch. `Tests/EditMode/LevelTypeTableTests.cs` khai `enum TestType { A, B }`, `class TestEntry : ILevelTypeEntry<TestType>`;
ca thiếu dùng `LogAssert.Expect(LogType.Error, …)` khớp chuỗi chứa `level 99`.

| Case | Input | Kỳ vọng |
|---|---|---|
| có trong bảng | {1 → B}; `TypeOf(1, A)` | B, không log |
| thiếu | {1 → B}; `TypeOf(99, A)` | log lỗi chứa `level 99`, trả A |
| bảng rỗng | `[]`; `TypeOf(1, A)` | log lỗi, trả A |
| level trùng | {3 → A, 3 → B}; `TypeOf(3, A)` | A (dòng đầu thắng) |
| level 0, âm | {1 → B}; `TypeOf(0, A)`, `TypeOf(-1, A)` | log lỗi, trả A |
| bảng không theo thứ tự | {5 → B, 2 → A}; `TypeOf(2, B)` | A |

**Mốc commit:** `feat(progression): LevelTypeTable.TypeOf`.

---

## Cơ chế C — `RandomPick`: chọn ngẫu nhiên theo chuỗi predicate ưu tiên

### Luồng mô tả

```
dự án có List<T> ứng viên và một nguồn ngẫu nhiên của riêng nó (Func<int,int> nextIndex)
            │
            ├─► chọn bất kỳ: PickRandom(list, nextIndex)                                        GĐ3
            │        rỗng → default; còn lại → list[nextIndex(count)]
            │
            └─► chọn theo ưu tiên: PickByPriority(list, nextIndex, p0, p1, …, pn)                GĐ4
                     pass 0 lọc thoả p0…pn → có thì PickRandom trong đó
                     pass 1 lọc thoả p1…pn → …
                     không pass nào có → PickRandom cả list
            │
            ▼
dự án nối: GetRandomFromList, GetRandomFromListWithProrityPredicates giữ tên, truyền nguồn Unity   GĐ6
```

### Bản đồ hệ cuối

```
  caller dự án ──PickRandom(list, nextIndex)──────────► RandomPick (static)
               ──PickByPriority(list, nextIndex, preds)─►    │ MatchBuffer<T>.Items (static, dùng lại, Clear sau khi chọn)
                                                             └─ PickRandom(matches, nextIndex)
  nguồn ngẫu nhiên: dự án static readonly Func<int,int> (UnityEngine.Random.Range) · test new System.Random(seed)
```

Ai sở hữu gì: `RandomPick` không giữ state ngoài buffer tạm; nguồn ngẫu nhiên và seed là của caller.

---

## Giai đoạn 3 — `RandomPick.PickRandom`

**Mục tiêu:** chọn một phần tử bất kỳ trong list bằng nguồn ngẫu nhiên caller đưa; list rỗng trả `default`. Xong giai đoạn này mọi
`GetRandomFromList` của dự án có lõi để trỏ về.

**Vì sao bây giờ:** là bước chọn cuối của mọi pass trong GĐ4; viết trước để GĐ4 chỉ còn phần lọc.

**Ai gọi:** test của giai đoạn; `PickByPriority` (GĐ4); trong dự án là `ListExtensions.GetRandomFromList` (GĐ6).

### `Runtime/Utilities/Picking/RandomPick.cs` — file mới

```csharp
using System;
using System.Collections.Generic;

namespace Horcrux.Runtime.Utilities.Picking
{
    /// <summary>
    /// Chọn ngẫu nhiên trong list. Nguồn ngẫu nhiên là của caller: nextIndex(count) trả chỉ số trong [0, count), để dự án giữ
    /// nước đi đã author và test chạy tất định.
    /// </summary>
    public static class RandomPick
    {
        #region API

        /// <summary>List rỗng trả default, không gọi nextIndex.</summary>
        public static T PickRandom<T>(this List<T> list, Func<int, int> nextIndex)
        {
            if (list.Count == 0) return default;
            return list[nextIndex(list.Count)];
        }

        #endregion API
    }
}
```

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| `ListExtensions.GetRandomFromList` | `UnityEngine.Random.Range(0, list.Count)` gọi thẳng | `nextIndex(list.Count)` | nguồn ngẫu nhiên là của caller; kết quả giữ nguyên khi caller truyền `Random.Range` |
| `LegacyListExtensions.GetRandomFromListReseeded` | `InitState(Guid.NewGuid().GetHashCode())` rồi `Range` | không có | reseed là quyết định của dự án, nằm trong delegate nó truyền |

**Quyết định** — list rỗng trả `default` và không gọi `nextIndex`: caller của dự án (`AutoPlayer`, `EntitiesManager`,
`GameDifficultyManager`) dựa vào `null` để biết "không có gì để chọn"; `nextIndex(0)` là dải rỗng, gọi là lỗi của nguồn. Phương án bỏ:
ném — ba caller đều có nhánh `null` sẵn.

| Consumes | Produces |
|---|---|
| `System.Func<int, int>` | `RandomPick.PickRandom` |

**Sơ đồ tương tác sau GĐ3**

```
  caller ──PickRandom+(list, nextIndex)──► RandomPick+ ──nextIndex(count)──► nguồn của caller ──► list[i]
```

**Editor setup:** không.

**Kiểm:** compile sạch. `Tests/EditMode/RandomPickTests.cs`; `nextIndex` từ `new System.Random(seed)`: `count => rng.Next(count)`.

| Case | Input | Kỳ vọng |
|---|---|---|
| rỗng | `[]` | `default` (0 với `int`, `null` với class); `nextIndex` không được gọi |
| một phần tử | `[7]` | 7; `nextIndex` được gọi đúng một lần với 1 |
| tất định | `[1,2,3]`, `System.Random(5)` hai lần | cùng phần tử |
| nguồn trả biên | `[1,2,3]`, `nextIndex` trả `count - 1` | 3 |
| nguồn trả 0 | `[1,2,3]`, `nextIndex` trả 0 | 1 |

**Mốc commit:** `feat(picking): RandomPick.PickRandom`.

---

## Giai đoạn 4 — `RandomPick.PickByPriority`

**Mục tiêu:** chọn theo chuỗi predicate ưu tiên: pass k lọc phần tử thoả mọi predicate từ k tới cuối, pass đầu có kết quả thì chọn
ngẫu nhiên trong đó, không pass nào có thì chọn trong cả list; không cấp phát mỗi lần gọi. Xong giai đoạn này bước chọn tileId
của box kế (9 predicate, chạy sau mỗi box xong) có lõi để trỏ về.

**Vì sao bây giờ:** là caller đầu tiên của `PickRandom` ngoài test; phần lọc chỉ có nghĩa khi bước chọn cuối đã có.

**Ai gọi:** test của giai đoạn; trong dự án là `LegacyListExtensions.GetRandomFromListWithProrityPredicates` (GĐ6).

### `RandomPick.cs` › `API` › thêm sau `PickRandom`, trước `#endregion API`

```csharp
        /// <summary>
        /// Pass thứ k lọc phần tử thoả mọi predicate từ k tới cuối (pass 0 đòi tất cả); pass đầu có kết quả thì chọn ngẫu nhiên
        /// trong đó, không pass nào có thì chọn trong cả list. Không predicate thì như PickRandom.
        /// </summary>
        public static T PickByPriority<T>(this List<T> list, Func<int, int> nextIndex, params Predicate<T>[] predicates)
        {
            if (list.Count == 0) return default;
            if (predicates == null || predicates.Length == 0) return list.PickRandom(nextIndex);

            var matches = MatchBuffer<T>.Items;
            for (var firstPredicate = 0; firstPredicate < predicates.Length; firstPredicate++)
            {
                matches.Clear();
                for (var i = 0; i < list.Count; i++)
                {
                    if (MatchesAllFrom(list[i], predicates, firstPredicate))
                        matches.Add(list[i]);
                }

                if (matches.Count > 0)
                {
                    var picked = matches.PickRandom(nextIndex);
                    matches.Clear();
                    return picked;
                }
            }

            return list.PickRandom(nextIndex);
        }
```

### `RandomPick.cs` › thêm region `Class Methods` sau `#endregion API`

```csharp
        #region Class Methods

        private static bool MatchesAllFrom<T>(T item, Predicate<T>[] predicates, int firstPredicate)
        {
            for (var i = firstPredicate; i < predicates.Length; i++)
            {
                if (!predicates[i](item))
                    return false;
            }

            return true;
        }

        // Buffer dùng lại cho mỗi T: caller gọi ở nhịp "mỗi lần dữ liệu đổi" (spawn), không cấp phát list tạm; Clear để không giữ phần tử.
        private static class MatchBuffer<T>
        {
            public static readonly List<T> Items = new();
        }

        #endregion Class Methods
```

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| `GetRandomFromListWithProrityPredicates` | bước chọn gọi `GetRandomFromListReseeded` (reseed rồi `Range`) | `PickRandom(nextIndex)` | reseed đi vào delegate của dự án (GĐ6); hai vòng lặp, `MatchesAllFrom`, `MatchBuffer<T>` giữ nguyên văn |
| mahjong-jam bản LINQ | `predicates.TakeLast(n - i)`, `Where(All)`, `ToList()` mỗi pass | hai vòng lặp, buffer dùng lại | cùng tập ứng viên ở mỗi pass (test vét cạn so với bản LINQ), không rác ở nhịp spawn |

**Quyết định** — pass k đòi **mọi** predicate từ k tới cuối, không phải "predicate k": nghĩa gốc của chuỗi ưu tiên là predicate đầu
mong muốn nhất, bỏ dần từ đầu; test vét cạn so với bản LINQ gốc khoá. Phương án bỏ: pass k chỉ đòi predicate k — đổi tập ứng viên của
mọi pass sau pass 0, đổi 50 level đã author. — `MatchBuffer<T>` static generic: một buffer mỗi `T`, không khoá luồng vì mọi caller
ở main thread; `Clear` sau khi chọn để buffer không giữ tham chiếu tới phần tử của level đã dispose. — `predicates == null` xử như
không predicate: caller của dự án gọi `params` rỗng và `null` đều có test.

| Consumes | Produces |
|---|---|
| GĐ3 `PickRandom` · `System.Predicate<T>` | `RandomPick.PickByPriority` |

**Sơ đồ tương tác sau GĐ4**

```
  caller ──PickByPriority+(list, nextIndex, p0…pn)──► RandomPick ──pass k: MatchesAllFrom+ → MatchBuffer<T>+──► PickRandom(matches) ──► phần tử
                                                                 └─ không pass nào ──► PickRandom(list)
```

**Editor setup:** không.

**Kiểm:** compile sạch; thêm vào `RandomPickTests.cs`. Oracle vét cạn là bản LINQ gốc viết lại trong test (`TakeLast + Where + All`,
trả tập ứng viên của pass đầu có phần tử).

| Case | Input | Kỳ vọng |
|---|---|---|
| pass đầu không ai thoả | `[1,2,3,6,9]`, preds `>100`, `%2==0`, `%3==0` | 6 (pass 1 chỉ 6 thoả hai predicate cuối) |
| pass đầu có một | `[1,2,3,6,9]`, preds `==6`, `%3==0` | 6, `nextIndex` gọi với 1 |
| không pass nào | `[4,5]`, pred `>100` | nằm trong list |
| không predicate | `[7,8]`, `null` và `[]` | nằm trong list |
| một predicate thoả hết | `[7,8]`, pred `>0` | nằm trong list; `nextIndex` gọi với 2 |
| một phần tử không thoả gì | `[7]`, pred `>100` | 7 |
| rỗng | `[]`, pred `true` | `default`, `nextIndex` không gọi |
| tất định | `[1,2,3,6,9]`, 3 pred, `System.Random(5)` hai lần | cùng phần tử |
| vét 2000 ca ngẫu nhiên | list 1–11 phần tử giá trị 0–19, 1–6 predicate ngẫu nhiên (`> t`, `% m == 0`, `<= t`), seed 20261025 | kết quả nằm trong tập ứng viên của bản LINQ, bằng phần tử duy nhất khi tập có một; có ít nhất một ca một ứng viên |
| buffer không giữ phần tử | sau `PickByPriority` trên list class | `MatchBuffer<T>.Items.Count == 0` qua reflection (`MatchBuffer` là private lồng); không với tới thì bỏ ca này |

**Mốc commit:** `feat(picking): RandomPick.PickByPriority`.

---

## Giai đoạn 5 — Tài liệu (agent)

`docs/Runtime/Progression/Progression.md` (một tài liệu cho `ProgressCurve` và `LevelTypeTable`), mở đầu `Code: Runtime/Utilities/Progression`:
luồng *bảng từ asset hay remote config → `TypeOf` → `ValueAt`* vẽ ASCII · bảng mốc của curve bậc thang (bảng case GĐ1) · cách dự án
implement hai interface để key JSON không đổi (struct với explicit interface implementation) · "Design decisions" gom **Quyết định** của
GĐ1–GĐ2 · ca "bảng phải tăng dần, cửa nhận dữ liệu của dự án kiểm" và ca `steps` rỗng ném.

`docs/Runtime/RandomPick/RandomPick.md`, mở đầu `Code: Runtime/Utilities/Picking`: luật pass ưu tiên bằng một ví dụ năm phần tử ba
predicate (ca "pass đầu không ai thoả") · quy ước nguồn ngẫu nhiên của caller · hai delegate mẫu (Unity `static readonly`, `System.Random`
cho test) · "Design decisions" gom **Quyết định** của GĐ3–GĐ4.

Không có "Trước khi chạy": không wire tay. Mỗi `.md` có `.html` cùng tên chọn từ nó: kịch bản (bảng mốc, level thiếu, chuỗi ưu
tiên), cách dùng (implement interface, truyền nguồn ngẫu nhiên), flow, lưu ý.

**Mốc commit:** `docs(progression,picking): Progression.md, RandomPick.md`.

---

## Giai đoạn 6 — category-jam dùng bản Horcrux, xoá bản tạm (agent)

**Mục tiêu:** `DifficultyCurve.PointAt` trỏ về `ValueAt`; `GetRandomFromList`, `GetRandomFromListWithProrityPredicates` giữ tên, trỏ về
`RandomPick` với nguồn Unity; `GetRandomFromListReseeded`, `MatchesAllFrom`, `MatchBuffer` xoá khỏi dự án. Năm call site và hai test
của dự án không đổi dòng nào; `LevelTypeTable` không có caller ở category-jam nên giai đoạn này không chạm.

### `Config/LevelDifficulty.cs` — đoạn cũ → đoạn mới

Cũ:

```csharp
    [Serializable]
    public struct DifficultyStep
    {
        [Range(0, 100)] public int progressPercent;
        [Min(0)] public float pointPerFreeSlot;
    }
```

Mới — explicit interface implementation để tên field serialize và key JSON không đổi:

```csharp
    [Serializable]
    public struct DifficultyStep : IProgressStep
    {
        [Range(0, 100)] public int progressPercent;
        [Min(0)] public float pointPerFreeSlot;

        int IProgressStep.ProgressPercent => progressPercent;
        float IProgressStep.Value => pointPerFreeSlot;
    }
```

`DifficultyCurve.PointAt` — thân cũ (vòng lặp) → `public float PointAt(float progressPercent) => ProgressCurve.ValueAt(steps, progressPercent);`
giữ summary. Thêm `using Horcrux.Runtime.Utilities.Progression;`.

### `Utilities/Extensions/ListExtensions.cs` › `ListExtensions` › `GetRandomFromList` — đoạn cũ → đoạn mới

Cũ:

```csharp
        public static T GetRandomFromList<T>(this List<T> list)
        {
            if (list.Count == 0) return default(T);

            T random = list[UnityEngine.Random.Range(0, list.Count)];
            return random;
        }
```

Mới — delegate `static readonly` để không cấp phát mỗi lần gọi:

```csharp
        /// <summary>Nguồn ngẫu nhiên của gameplay: UnityEngine.Random, giữ nước đi đã author.</summary>
        public static readonly Func<int, int> UnityRange = static count => UnityEngine.Random.Range(0, count);

        public static T GetRandomFromList<T>(this List<T> list) => list.PickRandom(UnityRange);
```

Thêm `using Horcrux.Runtime.Utilities.Picking;`.

### `Utilities/Extensions/LegacyHelpers.cs` › `LegacyListExtensions` — đoạn cũ → đoạn mới

Xoá `GetRandomFromListReseeded`, thân `GetRandomFromListWithProrityPredicates`, `MatchesAllFrom`, `MatchBuffer<T>`. Thay bằng:

```csharp
        /// <summary>Reseed mỗi lần rút rồi Random.Range: nước đi của bước chọn tileId đã author theo nguồn này.</summary>
        public static readonly Func<int, int> UnityRangeReseeded = static count =>
        {
            UnityEngine.Random.InitState(Guid.NewGuid().GetHashCode());
            return UnityEngine.Random.Range(0, count);
        };

        /// <summary>Chọn theo chuỗi predicate ưu tiên, nguồn ngẫu nhiên reseed mỗi lần rút.</summary>
        public static T GetRandomFromListWithProrityPredicates<T>(this List<T> list, params Predicate<T>[] predicates)
            => list.PickByPriority(UnityRangeReseeded, predicates);
```

`DivideEqually`, `GetRandomElementsInGroups` giữ nguyên. Thêm `using Horcrux.Runtime.Utilities.Picking;`.

**Khác với bản dự án:** không đổi hành vi. `GetRandomFromListReseeded` reseed rồi `Range` ở mỗi lần rút, kể cả nhánh "không predicate" —
`UnityRangeReseeded` làm đúng chuỗi đó ở mỗi `nextIndex`.

**Editor setup:** không — `DifficultyStep` giữ tên field serialize, YAML của các SO `Content/Difficulty/*.asset` không đổi; key remote
config không đổi.

**Kiểm:** grep `GetRandomFromListReseeded`, `MatchBuffer`, `MatchesAllFrom` trong `Assets/_CategoryJam` trả 0 · compile dự án sạch ·
test dự án `Tests/EditMode/DifficultyCurveTests.cs`, `PriorityPredicatePickTests.cs`, `DifficultyProfileAssetTests.cs` xanh không sửa dòng
nào — đây là phép kiểm "không đổi hành vi" · **cần chơi thử** (developer): vào một level, chơi tới khi vài box xong; nhìn box kế có
tileId hợp lý với tile trong cache (không lặp box cùng id liên tiếp khi còn id khác), auto-play (cheat) chọn tile và chạy hết level;
panel debug của `GameDifficultyManager` hiện `Point Per Free Slot` tăng theo mốc curve khi `Play: %` vượt mốc. Dấu hiệu hỏng: log
`finalDiag is null`, auto-play đứng, point không đổi qua mốc.

**Mốc commit:** repo dự án `refactor(difficulty): DifficultyCurve và pick ngẫu nhiên dùng Progression, Picking của Horcrux` kèm con trỏ submodule.

---

## File → giai đoạn

| File | GĐ1 | GĐ2 | GĐ3 | GĐ4 | GĐ5 | GĐ6 |
|---|---|---|---|---|---|---|
| `Runtime/Utilities/Progression/ProgressCurve.cs` | tạo | | | | | |
| `Runtime/Utilities/Progression/LevelTypeTable.cs` | | tạo | | | | |
| `Runtime/Utilities/Picking/RandomPick.cs` | | | tạo | `PickByPriority`, `MatchesAllFrom`, `MatchBuffer` | | |
| `Tests/EditMode/ProgressCurveTests.cs` (agent) | tạo | | | | | |
| `Tests/EditMode/LevelTypeTableTests.cs` (agent) | | tạo | | | | |
| `Tests/EditMode/RandomPickTests.cs` (agent) | | | tạo | thêm case | | |
| `docs/Runtime/Progression/Progression.md`, `docs/Runtime/RandomPick/RandomPick.md` và hai `.html` (agent) | | | | | tạo | |
| dự án: `Config/LevelDifficulty.cs`, `Utilities/Extensions/ListExtensions.cs`, `Utilities/Extensions/LegacyHelpers.cs` | | | | | | đổi |
