# Plan — Độ khó theo tiến độ: `ProgressCurve`, `LevelTypeTable`, `RandomPick` lên Horcrux

Ngày 2026-11-02. Nguồn: category-jam, `Assets/_CategoryJam/Scripts/Config/LevelDifficulty.cs` (`DifficultyCurve.PointAt`,
`LevelDifficultyLookup.TypeOf`) và `Utilities/Extensions/LegacyHelpers.cs` (`GetRandomFromListWithProrityPredicates`,
`GetRandomFromListReseeded`), `ListExtensions.GetRandomFromList`. Là lõi V2 của mahjong-jam nên đã có người dùng thứ hai. Ba cơ chế
thuần: giá trị theo mốc % tiến độ · level → loại · chọn ngẫu nhiên trong danh sách theo chuỗi predicate ưu tiên. Phần chấm điểm theo
tile chặn (`GameDifficultyManager`) và cặp "loại → curve" bám enum dự án ở lại.

**Phân công:** Task 1–3 (Horcrux, code runtime) developer gõ · Task 4 (tài liệu) và Task 5 (test EditMode) agent viết sau khi code có.
Dự án chuyển sang bản Horcrux là việc riêng khi developer yêu cầu.

Horcrux là submodule: mọi task commit **trong `Assets/Horcrux`**.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | game có bảng độ khó do designer chỉnh (và có thể tới từ remote config): mỗi level một loại, mỗi loại một curve "giá trị theo % tiến độ"; và mọi chỗ chọn ngẫu nhiên một phần tử trong list theo luật ưu tiên |
| Mục tiêu | ba lõi thuần, test không dựng gì; **không đổi kết quả ngẫu nhiên** của dự án khi chuyển (NT6) |
| Chỗ đặt | `Runtime/Utilities/Progression/ProgressCurve.cs`, `LevelTypeTable.cs` (namespace `Horcrux.Runtime.Utilities.Progression`) · `Runtime/Utilities/Picking/RandomPick.cs` (namespace `Horcrux.Runtime.Utilities.Picking`; không đặt `Random` vào tên namespace để `UnityEngine.Random` trong Horcrux không bị che) |
| Từ vựng | **framework không đặt tên field dữ liệu**: step, assignment là struct/class của dự án, khớp key JSON remote config (`pointPerFreeSlot`, `levelNumber`, `type` là wire format với server). Horcrux khai **interface hai getter** và hàm generic ràng buộc theo interface; struct hay class của dự án implement là xong, struct không boxing nhờ lời gọi generic có ràng buộc |
| Nguồn ngẫu nhiên | **nhận `Func<int, int> nextIndex` từ caller** (`count → [0, count)`), developer chốt 2026-11-02: dự án truyền `UnityEngine.Random.Range` để giữ nước đi đã author; test truyền `System.Random` để tất định |
| Tên | `ProgressCurve.ValueAt`, `LevelTypeTable.TypeOf`, `RandomPick.PickRandom`, `RandomPick.PickByPriority`. Developer đổi tuỳ ý trước khi gõ |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Class `ProgressCurve` serialize sẵn `List<ProgressStep>` | tên field là wire format với remote config của từng game; framework đặt tên là ép dự án đổi key JSON. Dự án giữ class data, Horcrux giữ thuật toán |
| `CurveOf(type)` với fallback | cặp "loại → curve" là `(TType type, List<TStep> steps)` của dự án; 12 dòng, ở lại |
| Nội suy giữa hai mốc | luật của người dùng là **bậc thang**: mốc i có hiệu lực khi tiến độ vượt quá mốc i; nội suy là đổi hành vi |
| `Shuffle`, `GetRandomElements` | `Shuffle` không caller; `GetRandomElements` một caller dùng `OrderBy(Guid.NewGuid())` — đổi cách rút là đổi 50 level đã author |
| Nguồn ngẫu nhiên mặc định (`UnityEngine.Random`) khi caller không truyền | Horcrux không chọn nguồn ngẫu nhiên; tham số bắt buộc để test luôn tất định và dự án luôn thấy mình đang dùng nguồn nào |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| `ValueAt`: mốc i có hiệu lực khi tiến độ **vượt quá** `progressPercent` của mốc i, đúng tại mốc vẫn là mốc trước | hành vi V2 của mahjong-jam, test `PointAt_TwoSteps` của dự án khoá: 50 → mốc 0, 51 → mốc 1 |
| `ValueAt` không guard `steps` rỗng | lỗi dữ liệu chặn ở cửa `CurveOf` của dự án (LogError, rơi về curve mặc định); guard lần hai là nhánh không ai tới |
| `TypeOf` thiếu level: `LogError` rồi trả `fallback` caller truyền | dữ liệu có thể từ server nên không kèm context object; thiếu là lỗi setup không nổ, phải log; fallback là loại "dễ nhất" của từng game nên caller truyền |
| `PickByPriority`: pass k lọc phần tử thoả **mọi** predicate từ k tới cuối; pass đầu có kết quả thì chọn trong đó; không pass nào có thì chọn trong cả list | nghĩa gốc của "chuỗi ưu tiên": predicate đầu là mong muốn nhất, bỏ dần từ đầu; test `Pick_IsAlwaysInsideReferenceCandidates…` khoá bằng bản LINQ gốc |
| `MatchBuffer<T>` static dùng lại, `Clear` sau khi chọn | chạy mỗi lần spawn, không cấp phát list tạm; `Clear` để buffer không giữ phần tử |
| `PickRandom` list rỗng trả `default` | caller của dự án dựa vào `null` để biết "không có gì để chọn" |

**Mở rộng sau**

- Mốc theo đơn vị khác % (số lượt, giây): `ValueAt` nhận `float progress` và mốc là `float`; hiện mọi người dùng là % nguyên.

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| `LevelDifficulty.cs` | thuật toán `PointAt`, `TypeOf` | `LevelDifficultyType`, `DifficultyStep`, `DifficultyCurve`, `LevelAssignment`, `CurveOf`: từ vựng dự án và wire format |
| `LegacyHelpers.cs`, `ListExtensions.cs` | `GetRandomFromListWithProrityPredicates`, `GetRandomFromList` | `GetRandomFromListReseeded` (InitState mỗi lần là quyết định của dự án, đi vào `nextIndex` của nó), `KShuffle`, `Shuffle`, `GetRandomElements` |
| Horcrux `Utilities/` | — | chưa có curve bậc thang, tra bảng level, hay pick ngẫu nhiên; `GeometryHelper.RandomPointInAnnulus` dùng `UnityEngine.Random` trực tiếp, là ca khác (hình học) |
| `SystemPlan.md` §15 Level Library | `IDifficultyTagProvider` | `LevelTypeTable` là lõi tra; provider của §15 là service bọc nó khi §15 được dựng |
| Unity `AnimationCurve` | — | curve liên tục với tangent; người dùng cần bậc thang và key JSON phẳng |

---

## Task 1 — `ProgressCurve` (developer gõ)

### `Runtime/Utilities/Progression/ProgressCurve.cs`

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
        /// <summary>Mốc i có hiệu lực khi tiến độ vượt quá mốc i; trước mốc đầu dùng mốc đầu, qua mốc cuối dùng mốc cuối.</summary>
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

Dự án nối: `[Serializable] public struct DifficultyStep : IProgressStep { [Range(0,100)] public int progressPercent; [Min(0)] public float pointPerFreeSlot;
int IProgressStep.ProgressPercent => progressPercent; float IProgressStep.Value => pointPerFreeSlot; }` và `DifficultyCurve.PointAt(p) =>
ProgressCurve.ValueAt(steps, p)`. Key YAML và JSON không đổi.

---

## Task 2 — `LevelTypeTable` (developer gõ)

### `Runtime/Utilities/Progression/LevelTypeTable.cs`

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
        /// <summary>Level không có trong bảng là lỗi dữ liệu: LogError rồi trả fallback (loại dễ nhất của game).</summary>
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

Dự án nối: `[Serializable] public class LevelAssignment : ILevelTypeEntry<LevelDifficultyType> { public int levelNumber; public LevelDifficultyType type;
int ILevelTypeEntry<LevelDifficultyType>.LevelNumber => levelNumber; LevelDifficultyType ILevelTypeEntry<LevelDifficultyType>.Type => type; }`
và `TypeOf(assignments, n) => LevelTypeTable.TypeOf(assignments, n, LevelDifficultyType.NoBrain)`. `CurveOf` giữ ở dự án. `TEntry` là
class nên không ràng `struct`; `TType` ràng `struct, Enum` để `$"{fallback}"` in tên enum.

---

## Task 3 — `RandomPick` (developer gõ)

### `Runtime/Utilities/Picking/RandomPick.cs`

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
        /// <summary>List rỗng trả default.</summary>
        public static T PickRandom<T>(this List<T> list, Func<int, int> nextIndex)
        {
            if (list.Count == 0) return default;
            return list[nextIndex(list.Count)];
        }

        /// <summary>
        /// Pass thứ k lọc phần tử thoả mọi predicate từ k tới cuối (pass 0 đòi tất cả); pass đầu có kết quả thì chọn ngẫu nhiên
        /// trong đó, không pass nào có thì chọn trong cả list.
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

        private static bool MatchesAllFrom<T>(T item, Predicate<T>[] predicates, int firstPredicate)
        {
            for (var i = firstPredicate; i < predicates.Length; i++)
            {
                if (!predicates[i](item))
                    return false;
            }

            return true;
        }

        // Buffer dùng lại cho mỗi T, không cấp phát mỗi lần chọn; Clear để không giữ phần tử.
        private static class MatchBuffer<T>
        {
            public static readonly List<T> Items = new();
        }
    }
}
```

Dự án nối, giữ nguyên nước đi: `GetRandomFromList` → `list.PickRandom(UnityRange)` với `static readonly Func<int, int> UnityRange =
static count => UnityEngine.Random.Range(0, count);` · `GetRandomFromListWithProrityPredicates(preds)` → `list.PickByPriority(UnityRangeReseeded, preds)`
với `UnityRangeReseeded = static count => { UnityEngine.Random.InitState(Guid.NewGuid().GetHashCode()); return UnityEngine.Random.Range(0, count); }`.
Hai delegate khai `static readonly` ở dự án, không cấp phát mỗi lần gọi.

### Nghiệm thu Task 1–3

`com.horcrux.runtime.csproj` build sạch. Chưa có caller trong Horcrux: caller là lớp nối của dự án.

---

## Task 4 — Tài liệu (agent)

`Runtime/Utilities/Progression/Progression.md` (một tài liệu cho `ProgressCurve` và `LevelTypeTable`): luồng *bảng từ asset hay remote
config → `TypeOf` → `ValueAt`* vẽ ASCII · bảng mốc của curve bậc thang (bảng test case dưới) · cách dự án implement hai interface
để key JSON không đổi · "Design decisions" là bảng trên. `Runtime/Utilities/Picking/RandomPick.md`: luật pass ưu tiên bằng một ví dụ
bốn phần tử ba predicate · quy ước nguồn ngẫu nhiên của caller · hai delegate mẫu (Unity, `System.Random` cho test).

## Task 5 — Test EditMode (agent)

`Tests/EditMode/ProgressCurveTests.cs`, `LevelTypeTableTests.cs`, `RandomPickTests.cs`; struct `TestStep : IProgressStep`, class
`TestEntry : ILevelTypeEntry<TestType>` khai trong test; `nextIndex` từ `new System.Random(seed)`.

| Case | Input | Kỳ vọng |
|---|---|---|
| hai mốc, đúng tại mốc | mốc (0, 0), (50, 0.5); tiến độ 0, 49, 50, 51, 100 | 0, 0, 0, 0.5, 0.5 |
| một mốc | (0, 0.5); tiến độ 0, 50, 100 | 0.5 |
| tiến độ âm | (0, 0.25), (50, 1); −10 | 0.25 |
| curve 6 mốc của người dùng đầu | (0,0) (20,.5) (35,1) (50,1.5) (70,2) (80,2.5); tiến độ 20, 21, 35, 36, 50, 69, 80, 81, 100 | 0, .5, .5, 1, 1, 1.5, 2, 2.5, 2.5 |
| mốc không tăng dần | (50, 1), (0, 0); tiến độ 10 | mốc đầu `>=` 10 là mốc 0 → trả mốc clamp(−1) = mốc 0 = 1 — ghi ở doc: bảng phải tăng dần, cửa nhận dữ liệu của dự án kiểm |
| level có trong bảng | {1 → B}; `TypeOf(1, A)` | B |
| level thiếu | `TypeOf(99, A)` | `LogAssert.Expect(Error, "level 99")`, trả A |
| bảng rỗng | `TypeOf(1, A)` | log lỗi, trả A |
| `PickRandom` rỗng | `[]` | `default` |
| `PickRandom` một phần tử | `[7]` | 7, `nextIndex` được gọi với 1 |
| `PickRandom` tất định | `[1,2,3]`, `System.Random(5)` | cùng seed hai lần ra cùng phần tử |
| pass đầu không ai thoả | `[1,2,3,6,9]`, preds `>100`, `%2==0`, `%3==0` | 6 (pass 1 chỉ 6 thoả hai predicate cuối) |
| không pass nào | `[4,5]`, pred `>100` | nằm trong list |
| không predicate | `[7,8]`, `null` và `[]` | nằm trong list |
| rỗng | `[]`, pred `true` | `default` |
| vét 2000 ca ngẫu nhiên | list 1–11 phần tử, 1–6 predicate ngẫu nhiên, seed 20261025 | kết quả nằm trong tập ứng viên của bản LINQ gốc (`TakeLast` + `Where` + `All`), và bằng phần tử duy nhất khi tập có một; có ít nhất một ca một ứng viên |
| buffer không giữ phần tử | sau `PickByPriority` | `MatchBuffer<T>.Items.Count == 0` qua reflection (`MatchBuffer` là private lồng); test không với tới thì bỏ ca này |
