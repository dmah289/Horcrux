# Plan — Độ khó theo tiến độ: `ProgressCurve`, `LevelTypeTable`, `RandomPick`

Bản tạm: category-jam `Assets/_CategoryJam/Scripts/Config/LevelDifficulty.cs` › `DifficultyCurve.PointAt`, `Utilities/Extensions/LegacyHelpers.cs` › `GetRandomFromListWithProrityPredicates`, `GetRandomFromListReseeded`, `Utilities/Extensions/ListExtensions.cs` › `GetRandomFromList`; khuôn mahjong-jam (`D:\game\iKame\mahjong-jam`) `Assets/_Assets/Scripts/Config/ABTests/ABTestManager.LevelDifficulty.cs` › `GetDifficultType`, `Config/Base/LevelDifficultConfigSO.cs` › `GetCurrentConfigBase`.
Ba cơ chế độc lập. Không phụ thuộc Plan nào; `Plans/SystemPlan.md` §15 Level Library trỏ sang `LevelTypeTable` (`IDifficultyTagProvider` bọc `TypeOf`).
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.EditModeTests.csproj --tests Horcrux.EditModeTests`.

## Cơ chế A — `ProgressCurve`: giá trị theo % tiến độ

### Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| tiến độ nằm giữa hai mốc | mốc i có hiệu lực khi tiến độ **vượt quá** mốc i | giá trị bậc thang của mốc đang hiệu lực, không nội suy |
| tiến độ đúng tại mốc | vẫn dùng mốc trước | (0, 0), (50, 0.5): 50 → 0, 51 → 0.5 |
| tiến độ trước mốc đầu, âm | dùng mốc đầu | giá trị mốc đầu |
| tiến độ qua mốc cuối, trên 100 | dùng mốc cuối | giá trị mốc cuối |
| bảng không tăng dần | không kiểm | kết quả sai nghĩa — cửa nhận dữ liệu của dự án kiểm |
| bảng rỗng | không guard | `ArgumentOutOfRangeException` tại `steps[^1]` |
| struct dữ liệu của dự án | đọc qua `IProgressStep`, lời gọi generic có ràng buộc | không boxing; tên field và key JSON của dự án giữ nguyên |

### Flow

```
designer: bảng mốc (progressPercent, value) tăng dần — asset hay remote config
   ▼
dự án: List<TStep>, TStep : IProgressStep (tên field theo wire format)                 dự án
   ▼  mỗi lần cần giá trị: ValueAt(steps, p)
ProgressCurve.ValueAt (static, không state)                                           ProgressCurve.cs
   ▼  float Value của mốc đang hiệu lực
công thức của dự án                                                                     dự án
```

### GĐ1 — xong thì bảng "giá trị theo % tiến độ" tra bằng một lời gọi

```
  dự án: List<TStep : IProgressStep+> ──ValueAt+──► ProgressCurve+ ──float──► công thức của dự án
```

#### `Runtime/Utilities/Progression/ProgressCurve.cs` — file mới

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Progression
{
    /// <summary>Một mốc của curve bậc thang: từ khi tiến độ vượt quá ProgressPercent, Value có hiệu lực.</summary>
    // Interface hai getter, không class serialize sẵn: tên field là wire format của từng game; ràng buộc generic tránh boxing.
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
                // >= rồi i − 1: đúng tại mốc vẫn dùng mốc trước, như mọi curve đã author.
                if (steps[i].ProgressPercent >= progressPercent)
                    return steps[Mathf.Clamp(i - 1, 0, steps.Count - 1)].Value;
            }

            return steps[^1].Value;
        }
    }
}
```

`Tests/EditMode/ProgressCurveTests.cs` (agent), khai `struct TestStep : IProgressStep` (hai field, hai getter), list dựng tại chỗ:

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

---

## Cơ chế B — `LevelTypeTable`: level → loại

### Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| level có trong bảng | quét tới dòng khớp | loại của level, không log |
| level thiếu (cả level 0, âm, bảng rỗng) | `LogError` chứa số level | `fallback` caller truyền — loại dễ nhất của game |
| level trùng | dòng đầu thắng | loại của dòng đầu |
| bảng không theo thứ tự | quét hết | đúng loại |

### Flow

```
designer: bảng (levelNumber, type) — CSV, remote config
   ▼
dự án: List<TEntry>, TEntry : ILevelTypeEntry<TType>, enum loại của dự án                 dự án (SystemPlan §15)
   ▼  vào level n: TypeOf(entries, n, fallback)
LevelTypeTable.TypeOf (static, không state)                                             LevelTypeTable.cs
   ├─ có: TType
   └─ thiếu: Debug.LogError, trả fallback
   ▼
dự án tra curve, preset theo loại                                                         dự án
```

### GĐ2 — xong thì lõi tra level → loại của `SystemPlan` §15 có sẵn

```
  dự án: List<TEntry : ILevelTypeEntry<TType>+> ──TypeOf+──► LevelTypeTable+ ──TType──► dự án tra curve theo loại
                                                                  └─ thiếu: LogError+, fallback
```

#### `Runtime/Utilities/Progression/LevelTypeTable.cs` — file mới

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
        // fallback là tham số, không default(TType): enum của game không chắc giá trị 0 là dễ nhất.
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

`Tests/EditMode/LevelTypeTableTests.cs` (agent), khai `enum TestType { A, B }`, `class TestEntry : ILevelTypeEntry<TestType>`; ca thiếu dùng `LogAssert.Expect(LogType.Error, …)` khớp chuỗi chứa `level 99`:

| Case | Input | Kỳ vọng |
|---|---|---|
| có trong bảng | {1 → B}; `TypeOf(1, A)` | B, không log |
| thiếu | {1 → B}; `TypeOf(99, A)` | log lỗi chứa `level 99`, trả A |
| bảng rỗng | `[]`; `TypeOf(1, A)` | log lỗi, trả A |
| level trùng | {3 → A, 3 → B}; `TypeOf(3, A)` | A (dòng đầu thắng) |
| level 0, âm | {1 → B}; `TypeOf(0, A)`, `TypeOf(-1, A)` | log lỗi, trả A |
| bảng không theo thứ tự | {5 → B, 2 → A}; `TypeOf(2, B)` | A |

---

## Cơ chế C — `RandomPick`: chọn ngẫu nhiên theo chuỗi predicate ưu tiên

### Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| list rỗng | trả `default`, không gọi `nextIndex` | `null` với class — nhánh "không có gì để chọn" của caller |
| chọn bất kỳ | `list[nextIndex(count)]` | một phần tử theo nguồn caller đưa |
| chọn theo ưu tiên `p0…pn` | pass k lọc phần tử thoả mọi predicate từ k tới cuối; pass đầu có kết quả thì chọn trong đó | `[1,2,3,6,9]`, `>100`, `%2==0`, `%3==0` → 6 |
| không pass nào có | chọn trong cả list | một phần tử của list |
| không predicate, `predicates` null | như `PickRandom` | một phần tử của list |
| cùng nguồn seed | tất định | cùng phần tử |
| gọi mỗi lần dữ liệu đổi (spawn) | buffer dùng lại theo `T`, `Clear` sau khi chọn | không cấp phát; buffer không giữ phần tử của level đã dispose |
| dự án truyền `UnityEngine.Random.Range` (hoặc bản reseed) | | kết quả như bản dự án trước khi chuyển |

### Flow

```
caller dự án: List<T> ứng viên, Func<int,int> nextIndex                                    dự án
   │  nguồn: static readonly delegate quanh UnityEngine.Random.Range · test: new System.Random(seed)
   ├─ PickRandom(list, nextIndex) ──────────► RandomPick                                    RandomPick.cs
   │                                            rỗng → default; còn lại → list[nextIndex(count)]
   └─ PickByPriority(list, nextIndex, p0…pn) ─► RandomPick
                                                pass k: MatchesAllFrom(pk…pn) → MatchBuffer<T>.Items
                                                có → PickRandom(matches), Clear · không pass nào → PickRandom(list)
```

### Thuật toán

Pass `k` (0 ≤ k < n) lọc `{x : p_k(x) ∧ … ∧ p_{n−1}(x)}` — predicate đầu mong muốn nhất, bỏ dần từ đầu; pass đầu khác rỗng thì chọn đều trong đó, không pass nào thì chọn trong cả list.

| Pass | Đòi (`[1,2,3,6,9]`, `p0 >100`, `p1 %2==0`, `p2 %3==0`) | Ứng viên |
|---|---|---|
| 0 | `p0 ∧ p1 ∧ p2` | ∅ |
| 1 | `p1 ∧ p2` | {6} → chọn 6 |

Oracle: bản LINQ `TakeLast(n − k) + Where + All` (nghĩa gốc của chuỗi ưu tiên); case vét 2 000 ca ở GĐ4 so tập ứng viên với nó.

### GĐ3 — xong thì chọn ngẫu nhiên bằng nguồn của caller có lõi để `GetRandomFromList` trỏ về

```
  caller ──PickRandom+(list, nextIndex)──► RandomPick+ ──nextIndex(count)──► nguồn của caller ──► list[i]
```

#### `Runtime/Utilities/Picking/RandomPick.cs` — file mới

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

`Tests/EditMode/RandomPickTests.cs` (agent); `nextIndex` từ `new System.Random(seed)`: `count => rng.Next(count)`:

| Case | Input | Kỳ vọng |
|---|---|---|
| rỗng | `[]` | `default` (0 với `int`, `null` với class); `nextIndex` không được gọi |
| một phần tử | `[7]` | 7; `nextIndex` được gọi đúng một lần với 1 |
| tất định | `[1,2,3]`, `System.Random(5)` hai lần | cùng phần tử |
| nguồn trả biên | `[1,2,3]`, `nextIndex` trả `count - 1` | 3 |
| nguồn trả 0 | `[1,2,3]`, `nextIndex` trả 0 | 1 |

### GĐ4 — xong thì bước chọn theo chuỗi predicate ưu tiên có lõi không cấp phát để dự án trỏ về

```
  caller ──PickByPriority+(list, nextIndex, p0…pn)──► RandomPick ──pass k: MatchesAllFrom+ → MatchBuffer<T>+──► PickRandom(matches) ──► phần tử
                                                                 └─ không pass nào ──► PickRandom(list)
```

#### `RandomPick.cs` › `API` › thêm sau `PickRandom`, trước `#endregion API`

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
                    // Mọi predicate từ firstPredicate tới cuối, không riêng predicate đó: predicate đầu mong muốn nhất, bỏ dần từ đầu.
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

#### `RandomPick.cs` › thêm region `Class Methods` sau `#endregion API`

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

`Tests/EditMode/RandomPickTests.cs` (agent), thêm; oracle là bản LINQ gốc viết lại trong test (`TakeLast + Where + All`, trả tập ứng viên của pass đầu có phần tử):

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

---

## Chuyển dự án (agent)

Đường dẫn dự án tính từ `Assets/_CategoryJam/Scripts/`.

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/Progression/Progression.md`, `.html` | tạo, `Code: Runtime/Utilities/Progression`, một tài liệu cho `ProgressCurve` và `LevelTypeTable`: luồng bảng từ asset hay remote config → `TypeOf` → `ValueAt` · bảng mốc bậc thang (bảng case GĐ1) · implement hai interface bằng explicit implementation để key JSON không đổi · ca bảng phải tăng dần, `steps` rỗng ném; `.html`: kịch bản (bảng mốc, level thiếu), cách dùng, flow, lưu ý |
| `docs/Runtime/RandomPick/RandomPick.md`, `.html` | tạo, `Code: Runtime/Utilities/Picking`: luật pass bằng ví dụ năm phần tử ba predicate · quy ước nguồn ngẫu nhiên của caller · hai delegate mẫu (Unity `static readonly`, `System.Random` cho test); `.html`: kịch bản chuỗi ưu tiên, cách dùng, flow, lưu ý |
| `Config/LevelDifficulty.cs` › `DifficultyStep` | `: IProgressStep`, explicit implementation (code dưới); thêm `using Horcrux.Runtime.Utilities.Progression;` |
| `Config/LevelDifficulty.cs` › `DifficultyCurve.PointAt` | thân vòng lặp → `public float PointAt(float progressPercent) => ProgressCurve.ValueAt(steps, progressPercent);`, giữ summary |
| `Utilities/Extensions/ListExtensions.cs` › `GetRandomFromList` | giữ tên, trỏ `PickRandom(UnityRange)` (code dưới); thêm `using Horcrux.Runtime.Utilities.Picking;` |
| `Utilities/Extensions/LegacyHelpers.cs` › `LegacyListExtensions` | xoá `GetRandomFromListReseeded`, thân `GetRandomFromListWithProrityPredicates`, `MatchesAllFrom`, `MatchBuffer<T>`; thay bằng `UnityRangeReseeded` và lời gọi `PickByPriority` (code dưới) — reseed rồi `Range` ở mỗi lần rút như bản cũ, cả nhánh không predicate; `DivideEqually`, `GetRandomElementsInGroups` giữ; thêm `using Horcrux.Runtime.Utilities.Picking;` |
| năm call site, hai test của dự án | không đổi dòng nào |
| `LevelTypeTable` | không chạm: dự án không có bảng level → loại (level gắn thẳng SO độ khó) |
| SO `Content/Difficulty/*.asset`, key remote config | không đổi: `DifficultyStep` giữ tên field serialize |
| Plan này | xoá cùng `.meta` |
| kiểm | grep `GetRandomFromListReseeded`, `MatchBuffer`, `MatchesAllFrom` trong `Assets/_CategoryJam` ra 0 · compile dự án sạch · `Tests/EditMode/DifficultyCurveTests.cs`, `PriorityPredicatePickTests.cs`, `DifficultyProfileAssetTests.cs` xanh không sửa dòng nào — phép kiểm "không đổi hành vi" |
| chơi thử | vào một level, chơi tới khi vài box xong: box kế có tileId hợp lý với tile trong cache (không lặp box cùng id liên tiếp khi còn id khác), auto-play (cheat) chọn tile và chạy hết level, panel debug của `GameDifficultyManager` hiện `Point Per Free Slot` tăng theo mốc curve khi `Play: %` vượt mốc; hỏng: log `finalDiag is null`, auto-play đứng, point không đổi qua mốc |

`Config/LevelDifficulty.cs` › `DifficultyStep`, cũ:

```csharp
    [Serializable]
    public struct DifficultyStep
    {
        [Range(0, 100)] public int progressPercent;
        [Min(0)] public float pointPerFreeSlot;
    }
```

mới:

```csharp
    [Serializable]
    public struct DifficultyStep : IProgressStep
    {
        [Range(0, 100)] public int progressPercent;
        [Min(0)] public float pointPerFreeSlot;

        // Explicit implementation: tên field serialize và key JSON không đổi.
        int IProgressStep.ProgressPercent => progressPercent;
        float IProgressStep.Value => pointPerFreeSlot;
    }
```

`Utilities/Extensions/ListExtensions.cs` › `ListExtensions` › `GetRandomFromList`, cũ:

```csharp
        public static T GetRandomFromList<T>(this List<T> list)
        {
            if (list.Count == 0) return default(T);

            T random = list[UnityEngine.Random.Range(0, list.Count)];
            return random;
        }
```

mới:

```csharp
        /// <summary>Nguồn ngẫu nhiên của gameplay: UnityEngine.Random, giữ nước đi đã author.</summary>
        // static readonly: không cấp phát delegate mỗi lần gọi.
        public static readonly Func<int, int> UnityRange = static count => UnityEngine.Random.Range(0, count);

        public static T GetRandomFromList<T>(this List<T> list) => list.PickRandom(UnityRange);
```

`Utilities/Extensions/LegacyHelpers.cs` › `LegacyListExtensions`, thay phần xoá bằng:

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

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | chấm điểm độ khó, asset độ khó của dự án lên Horcrux | mang từ vựng game; ba lõi thuần là phần mang đi được |
| bỏ | `>` thay `>=` trong `ValueAt` | đổi giá trị tại đúng mốc của mọi curve đã author |
| bỏ | nội suy giữa hai mốc | luật của người dùng là bậc thang; nội suy là đổi hành vi |
| bỏ | guard `steps` rỗng trong `ValueAt` | cửa nhận dữ liệu của dự án đã chặn rỗng và mốc không tăng; guard lần hai là nhánh không ai tới |
| bỏ | giải mã mốc trong `ValueAt` | việc của struct dự án, trong getter `IProgressStep` |
| bỏ | tra curve theo loại với fallback trong Horcrux | dữ liệu dự án, vài dòng |
| bỏ | `TypeOf` tìm bằng `List.Find(lambda)` | lambda bắt `levelNumber`, cấp phát mỗi lần vào level |
| bỏ | thiếu level thì trả loại nhỏ nhất im lặng | bảng từ server không có nút Validate; thiếu là lỗi setup không nổ nên phải log |
| bỏ | `TryTypeOf(out)` để caller tự log | mọi caller thật cùng làm một việc (log, rơi về dễ nhất), hai dòng lặp ở mỗi caller |
| bỏ | `TEntry` ràng `struct` | bảng của dự án có thể là class |
| bỏ | `TType` không ràng `struct, Enum` | `$"{fallback}"` phải in tên enum; caller không truyền được kiểu không phải enum |
| bỏ | `nextIndex` mặc định `UnityEngine.Random` | developer chốt: Horcrux không chọn nguồn; dự án truyền `Random.Range` giữ nước đi đã author, test truyền `System.Random` để tất định |
| bỏ | reseed trong Horcrux | reseed là quyết định của dự án, nằm trong delegate nó truyền |
| bỏ | `PickRandom` ném khi list rỗng | caller của dự án dựa vào `null` để biết "không có gì để chọn"; `nextIndex(0)` là dải rỗng |
| bỏ | `predicates == null` ném | caller gọi `params` rỗng và `null`, đều có test |
| bỏ | pass k chỉ đòi predicate k | đổi tập ứng viên của mọi pass sau pass 0, đổi level đã author |
| bỏ | LINQ `TakeLast + Where + ToList` mỗi pass | rác ở nhịp spawn; hai vòng lặp cho cùng tập ứng viên |
| bỏ | khoá luồng cho `MatchBuffer<T>` | mọi caller ở main thread |
| bỏ | `Shuffle`, `KShuffle` lên đây | xáo tại chỗ là ca khác; bản chung là `FisherYates.Shuffle` (`Horcrux_SlotShuffle_Plan.md` GĐ1), chuyển caller sang nó không thuộc Plan này |
| bỏ | `GetRandomElements` lên đây | một caller rút bằng `OrderBy(Guid.NewGuid())`; đổi cách rút là đổi level đã author |
| bỏ | namespace chứa chữ `Random` | che `UnityEngine.Random` ở code dùng namespace đó |
| sau | dự án khuôn ở dòng mở chuyển sang bản Horcrux | việc riêng: bảng qua `ILevelTypeEntry`, mốc mã hoá giải trong getter `IProgressStep`, bản LINQ → `PickByPriority` với `nextIndex` reseed |
| sau | `ValueAt` nhận `float progress`, mốc `float` | khi mốc theo đơn vị khác % (số lượt, giây) |
| sau | `StepAt<TStep>` trả cả mốc, `ValueAt` gọi nó | khi cần nhiều giá trị của cùng mốc (min, max điểm) |
