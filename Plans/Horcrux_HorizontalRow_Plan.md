# Plan — `HorizontalRow`: xếp các con đang bật thành hàng ngang cách đều

Bản tạm: category-jam `Scripts/Utilities/HorizontalGrid.cs` › `SlotLocalX`, `LocalPositionOf`, `RepositionNow`, `RepositionSmooth`, `SetSpaceAndPlayStartLevelAnimation`; hai chủ trong `GameplayRoot.prefab`: `BoxesContainer` (`box_row`), `CacheTileSlots` (`cache_bar`).
**Phụ thuộc:** không — gõ lúc nào cũng được.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.PlayModeTests.csproj`; PlayMode chạy ở Window › General › Test Runner.

## Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| chủ bật, tắt con rồi gọi `RepositionNow` | con đang bật đặt ở `SlotLocalX(i, n, space)`, y = `childLocalY` | con đứng cách đều quanh gốc; con tắt giữ `localPosition`, không chiếm ô |
| cha vừa bật, chủ chưa gọi gì | 2 frame sau tự `RepositionNow` | ô sẵn trong prefab đứng đúng chỗ từ lúc scene lên |
| cha tắt, chủ vẫn xếp | lọc `activeSelf`, không theo cây | lúc cha bật, con đã đúng chỗ, không nhảy |
| vật bay tới ô vừa mở, hàng chưa trượt xong | `LocalPositionOf(i, n)` tính chỗ nghỉ tương lai | đích bằng tuyệt đối chỗ con sẽ đứng |
| số con đổi giữa màn | `RepositionSmooth` / `SetSpaceAndRepositionSmooth`: mọi con trượt song song `repositionDuration` từ chỗ đang đứng | `await` một mốc chung, kể cả con đã đúng chỗ; hoặc bỏ qua |
| gọi trượt hai lần sát nhau | dừng sequence trượt cũ | con tới đích lần hai |
| vào màn | `SetSpaceAndPlayAppearAsync`: xếp ngay, chờ 2 frame, giấu mọi con đang bật, con i bật tại `appearStartDelay + i·appearDelayPerChild` rồi bay từ `đích + appearOffsetX` về đích | con bay vào từ phải so le; `await` xong khi con cuối tới |
| trượt giữa lúc bay vào | `StopMoves` dừng cả mọi sequence bay | con chưa tới lượt vẫn tắt — chủ chờ bay xong rồi mới trượt |
| object huỷ trước mốc 2 frame hay giữa lúc bay | token huỷ dừng, không ghi lên transform đã huỷ | chủ `.Forget()`: im lặng; chủ `await`: nhận `OperationCanceledException` |
| không con · `space` 0 · `space` âm | xếp theo công thức | không ném; `space` âm đảo thứ tự, không chặn — chủ chịu trách nhiệm dấu |

## Flow

```
chủ (component trên cha; quyết con nào bật, gọi lúc nào, await hay không)
   │ RepositionNow / RepositionSmooth / SetSpaceAndRepositionSmooth / SetSpaceAndPlayAppearAsync / Space / LocalPositionOf
   ▼
HorizontalRow                                                     Runtime/Utilities/Layout/HorizontalRow.cs
   ├─ GetActiveChildren: lọc activeSelf ──► con 0..n-1: localPosition = LocalPositionOf(i, n) ──► SlotLocalX(i, n, space)
   ├─ LocalPositionOf(i, n) ──► chủ đọc chỗ nghỉ tương lai (đích chuyến bay) trước khi hàng trượt xong
   ├─ _moveSequence ──► Tween.LocalPositionX (song song)
   ├─ _appearSequences ──► delay → SetActive(true) → Tween.LocalPositionX
   └─ Start ──2 frame──► RepositionNow
Inspector: space, childLocalY · repositionDuration · appearDuration, appearStartDelay, appearDelayPerChild, appearOffsetX
           nút RepositionSmooth, RepositionNow
```

## Thuật toán

`n` con cách đều quanh gốc: `x_i = −(n − 1)·space / 2 + i·space`, `i = 0..n−1`; `n = 1` thì `x_0 = 0`. Bất biến: xếp và dự đoán (`LocalPositionOf`) cùng gọi `SlotLocalX` nên khớp tuyệt đối.

| Mốc bay vào (giây, tính từ sau 2 frame) | Giá trị |
|---|---|
| con i bật | `appearStartDelay + i·appearDelayPerChild` |
| `await` xong | `appearStartDelay + (n − 1)·appearDelayPerChild + appearDuration` |

---

## GĐ1 — xong thì chủ mở `n` ô đúng chỗ và biết trước chỗ nghỉ của ô

```
  chủ ──RepositionNow+ / Space+──► HorizontalRow+ ──localPosition──► con đang bật
      ──LocalPositionOf+(i, n)──► chỗ nghỉ tương lai (đích chuyến bay)     cả hai qua SlotLocalX+
  Start+ ──2 frame──► RepositionNow
```

`Runtime/Utilities/Layout/HorizontalRow.cs` — file mới:

```csharp
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Layout
{
    /// <summary>Xếp các con đang bật thành một hàng ngang cách đều quanh gốc.</summary>
    public class HorizontalRow : MonoBehaviour
    {
        #region Properties

        [Splitter("Configs")]
        [Tooltip("Khoảng cách tâm hai con kề nhau, đơn vị world.")]
        [SerializeField] private float space = 1.5f;
        [Tooltip("Local y của mọi con: cho con nằm trên hay dưới sprite vẽ ở chính transform này.")]
        [SerializeField] private float childLocalY;

        // Mọi API duyệt xong danh sách trước khi await, nên một buffer là đủ.
        private readonly List<Transform> _activeChildren = new();

        /// <summary>Khoảng cách tâm hai con kề nhau.</summary>
        public float Space => space;

        /// <summary>x cục bộ của con thứ index trong count con cách đều space, căn giữa quanh gốc.</summary>
        public static float SlotLocalX(int index, int count, float space) => -(count - 1) * space / 2 + index * space;

        /// <summary>Chỗ nghỉ cục bộ của con thứ index khi có count con bật; đọc được trước khi hàng trượt xong.</summary>
        public Vector3 LocalPositionOf(int index, int count) => new(SlotLocalX(index, count, space), childLocalY, 0f);

        #endregion Properties

        #region Unity Callbacks

        // Tự xếp dù chủ gọi sau: ô sẵn trong prefab đứng đúng chỗ ngay từ lúc scene lên.
        private void Start() => RepositionAfterChildrenSpawnedAsync().Forget();

        #endregion Unity Callbacks

        #region API

        [Button]
        public void RepositionNow()
        {
            var activeChildren = GetActiveChildren();
            var activeChildCount = activeChildren.Count;
            for (var i = 0; i < activeChildCount; i++)
                activeChildren[i].localPosition = LocalPositionOf(i, activeChildCount);
        }

        #endregion API

        #region Class Methods

        /// <summary>Hai frame sau khi bật: con cha sinh ở Awake/Start đã có mặt.</summary>
        private async UniTaskVoid RepositionAfterChildrenSpawnedAsync()
        {
            await UniTask.DelayFrame(2, cancellationToken: destroyCancellationToken);
            RepositionNow();
        }

        /// <summary>Chỉ con đang bật, theo thứ tự sibling; con tắt không chiếm chỗ.</summary>
        private List<Transform> GetActiveChildren()
        {
            _activeChildren.Clear();
            var root = transform;
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                // activeSelf, không activeInHierarchy: cha tắt thì mọi con tắt theo cây, hàng vẫn phải xếp đúng.
                if (child.gameObject.activeSelf)
                    _activeChildren.Add(child);
            }

            return _activeChildren;
        }

        #endregion Class Methods
    }
}
```

`Tests/PlayMode/HorizontalRowTests.cs` (agent) — dựng `new GameObject("row").AddComponent<HorizontalRow>()`, con `new GameObject().transform` với `SetParent(row, false)`; núm đặt bằng reflection (`Horcrux.PlayModeTests` không tham chiếu `UnityEditor`, không có `SerializedObject`):

| Case | Input | Kỳ vọng |
|---|---|---|
| một con | `space 2`, `RepositionNow` | x = 0, y = `childLocalY`, z = 0 |
| ba con | `space 2` | x = −2, 0, 2 |
| con tắt | 3 con, con giữa `SetActive(false)` | hai con còn lại ở −1, 1; con tắt giữ nguyên `localPosition` |
| không con | `RepositionNow` | không ném |
| `space` 0 | 3 con | cả ba x = 0, không ném |
| `space` âm | 3 con, `space −2` | x = 2, 0, −2: thứ tự đảo, không chặn |
| gọi lặp | `RepositionNow` hai lần | kết quả như một lần; buffer không phình (`_activeChildren.Count == n`, đọc qua reflection) |
| frame đầu | `AddComponent` lên cha có 2 con, không gọi gì, chờ 3 frame | hai con ở ±0.75 (mặc định `space 1.5`) |
| con sinh sau frame đầu | thêm con thứ ba ở frame 1 | sau mốc hai frame cả ba đã xếp |
| huỷ trước mốc | `Destroy` cha ngay sau `AddComponent`, chờ 3 frame | không lỗi trong log (`LogAssert.NoUnexpectedReceived`) |
| `SlotLocalX` | (0, 1, 2) · (0, 3, 2), (1, 3, 2), (2, 3, 2) · (3, 4, 1.5) · (3, 7, 1.5) · (0, 6, 1.5), (5, 6, 1.5) · (6, 7, 1.5) | 0 · −2, 0, 2 · 2.25 · 0 · −3.75, 3.75 · 4.5 |
| `LocalPositionOf` | `space 2`, `childLocalY 0.5`, (2, 3) | (2, 0.5, 0) |
| dự đoán khớp xếp | n = 1…7 con, `RepositionNow` | mọi i: `localPosition` **bằng tuyệt đối** `LocalPositionOf(i, n)` |

---

## GĐ2 — xong thì hàng co giãn theo số con, chủ `await` được lúc trượt xong

```
  chủ ──RepositionSmooth+ / SetSpaceAndRepositionSmooth+──► HorizontalRow ──StopMoves+──► _moveSequence cũ
                       ◄──Sequence+ (await hoặc bỏ)──          └─ Insert(0, LocalPositionX)+ ──► mỗi con đang bật
```

`HorizontalRow.cs` › `using` › thêm sau `using Cysharp.Threading.Tasks;`:

```csharp
using PrimeTween;
```

`HorizontalRow.cs` › `Properties` › thêm sau `childLocalY`:

```csharp
        [SerializeField] private float childLocalY;
        [Tooltip("Giây trượt tới chỗ mới khi RepositionSmooth.")]
        [SerializeField, Min(0f)] private float repositionDuration = 0.2f;

        private Sequence _moveSequence;
```

`HorizontalRow.cs` › `API` › thêm trước `RepositionNow`:

```csharp
        public Sequence SetSpaceAndRepositionSmooth(float newSpace)
        {
            space = newSpace;
            return RepositionSmooth();
        }

        /// <summary>Start tường minh từ chỗ đang đứng: con đã đúng chỗ vẫn chờ trọn tween, chủ await được một mốc chung.</summary>
        [Button]
        public Sequence RepositionSmooth()
        {
            // Gọi sát nhau thì hai tween ghi cùng localPosition.x; Stop trên Sequence default không làm gì.
            StopMoves();

            var activeChildren = GetActiveChildren();
            var activeChildCount = activeChildren.Count;

            _moveSequence = Sequence.Create();
            for (var i = 0; i < activeChildCount; i++)
            {
                var child = activeChildren[i];
                // Start tường minh: end bằng current thì PrimeTween cảnh báo và kết thúc sớm hơn các con khác.
                _moveSequence.Insert(0f, Tween.LocalPositionX(child, child.localPosition.x, SlotLocalX(i, activeChildCount, space), repositionDuration, Ease.OutQuad));
            }

            return _moveSequence;
        }
```

`HorizontalRow.cs` › `Class Methods` › thêm sau `RepositionAfterChildrenSpawnedAsync`:

```csharp
        /// <summary>Dừng tween của chính hàng này trên các con, không chạm tween khác của con.</summary>
        private void StopMoves()
        {
            _moveSequence.Stop();
        }
```

`Tests/PlayMode/HorizontalRowTests.cs` (agent) — thêm; chờ tween bằng `yield return new WaitForSeconds(repositionDuration + 0.05f)` trong `[UnityTest]`; `TearDown` gọi `Tween.StopAll()` rồi `yield return null` như `Tests/PlayMode/CharmJumpTests.cs`:

| Case | Input | Kỳ vọng |
|---|---|---|
| trượt | 2 con ở x = 5, `space 2`, `RepositionSmooth`, chờ | x = −1, 1; `isAlive` false sau `repositionDuration`, true ở nửa chừng |
| đã đúng chỗ | con đã ở đích | sequence vẫn `isAlive` ở nửa `repositionDuration`; không warning của PrimeTween trong log |
| hai lần liên tiếp | gọi `RepositionSmooth` lần hai ngay | sequence đầu `isAlive` false ngay; con tới đích lần hai |
| đổi space | 2 con, `SetSpaceAndRepositionSmooth(4)` | `Space` = 4; đích −2, 2 |
| con tắt | 3 con, con giữa tắt | hai con bật trượt tới −1, 1; con tắt đứng yên |
| không con | `RepositionSmooth` | trả sequence, không ném; `await` xong |
| một con | | trượt tới 0 |
| huỷ giữa chừng | `Destroy` cha ở nửa `repositionDuration` | không lỗi trong log |

---

## GĐ3 — xong thì hàng có màn vào: con bay vào từ phải, so le

```
  chủ ──SetSpaceAndPlayAppearAsync+──► HorizontalRow ──RepositionNow──► con
         ◄──UniTask+ (await)──              │ 2 frame → StopMoves (dừng trượt + bay+) → SetActive(false)+ mỗi con
                                             └─ _appearSequences+: delay(i) → SetActive(true) → LocalPositionX(đích+offset → đích)
```

`HorizontalRow.cs` › `Properties` › thêm sau `repositionDuration`:

```csharp
        [SerializeField, Min(0f)] private float repositionDuration = 0.2f;
        [Tooltip("Giây mỗi con bay vào từ phải khi SetSpaceAndPlayAppearAsync.")]
        [SerializeField, Min(0f)] private float appearDuration = 0.3f;
        [Tooltip("Giây chờ trước khi con đầu bay vào.")]
        [SerializeField, Min(0f)] private float appearStartDelay = 0.5f;
        [Tooltip("Con sau bay vào trễ hơn con trước bấy nhiêu giây.")]
        [SerializeField, Min(0f)] private float appearDelayPerChild = 0.05f;
        [Tooltip("Con bay vào từ bên phải đích bấy nhiêu đơn vị world.")]
        [SerializeField] private float appearOffsetX = 15f;
```

và sau `_moveSequence`:

```csharp
        private Sequence _moveSequence;
        private readonly List<Sequence> _appearSequences = new();
```

`HorizontalRow.cs` › `API` › thêm sau `SetSpaceAndRepositionSmooth`, trước `RepositionSmooth`:

```csharp
        /// <summary>Xếp ngay, rồi từng con bay vào từ bên phải, con sau trễ hơn con trước appearDelayPerChild.</summary>
        public async UniTask SetSpaceAndPlayAppearAsync(float newSpace)
        {
            space = newSpace;
            // Trước khi chờ: con về chỗ ngay frame gọi; con chủ bật sau lời gọi vẫn kịp vào danh sách giấu.
            RepositionNow();

            // Token: object huỷ giữa hai frame chờ thì không ghi lên transform đã huỷ.
            await UniTask.DelayFrame(2, cancellationToken: destroyCancellationToken);

            StopMoves();

            var activeChildren = GetActiveChildren();
            var activeChildCount = activeChildren.Count;

            var appearTasks = new List<UniTask>(activeChildCount);
            for (var i = 0; i < activeChildCount; i++)
            {
                var child = activeChildren[i];
                var targetX = SlotLocalX(i, activeChildCount, space);

                // Giấu bằng tắt, không đặt sẵn ở đích + offset: offset nhỏ hơn nửa màn thì con lộ ở mép phải.
                child.gameObject.SetActive(false);

                var appear = Sequence.Create()
                    .ChainDelay(appearStartDelay + i * appearDelayPerChild)
                    .ChainCallback(child, static c => c.gameObject.SetActive(true))
                    .Chain(Tween.LocalPositionX(child, targetX + appearOffsetX, targetX, appearDuration, Ease.OutCubic));

                _appearSequences.Add(appear);
                appearTasks.Add(appear);
            }

            await UniTask.WhenAll(appearTasks);
        }
```

`HorizontalRow.cs` › `Class Methods` › `StopMoves` — cũ:

```csharp
        private void StopMoves()
        {
            _moveSequence.Stop();
        }
```

Mới:

```csharp
        private void StopMoves()
        {
            _moveSequence.Stop();

            // Bay vào và trượt ghi cùng localPosition.x: một chỗ dừng cho cả hai.
            foreach (var appear in _appearSequences)
                appear.Stop();
            _appearSequences.Clear();
        }
```

`Tests/PlayMode/HorizontalRowTests.cs` (agent) — thêm; async chờ trong `[UnityTest]` qua `UniTask.ToCoroutine` (`Assert.ThrowsAsync` chặn luồng chính, không dùng); mốc bật đọc `Time.time` tại `ChainCallback`, test ghi `activeSelf` mỗi frame vào list:

| Case | Input | Kỳ vọng |
|---|---|---|
| bay vào | 2 con, `SetSpaceAndPlayAppearAsync(2)` | sau 2 frame cả hai `activeSelf` false; con 0 bật tại `appearStartDelay` ± 1 frame, con 1 trễ thêm `appearDelayPerChild`; lúc bật x ≈ đích + `appearOffsetX`; cuối x = −1, 1; `await` xong sau `appearStartDelay + appearDelayPerChild + appearDuration` ± 1 frame |
| một con | | bật tại `appearStartDelay`, cuối x = 0 |
| không con | | `await` xong ngay sau 2 frame, không ném |
| con tắt sẵn | 3 con, con giữa tắt trước khi gọi | con giữa vẫn tắt sau khi xong; hai con kia bay về −1, 1 |
| chạm số con lớn | 7 con | con cuối bật tại `appearStartDelay + 6·appearDelayPerChild`; cuối cách đều |
| bay rồi trượt | `RepositionSmooth` giữa lúc bay | mọi sequence bay `isAlive` false; con đã bật về đích trượt; con chưa tới lượt vẫn tắt |
| hai lần liên tiếp | gọi lần hai sau 3 frame | lượt đầu dừng, lượt hai chạy trọn, con cuối đúng chỗ, không con nào kẹt tắt |
| huỷ trước mốc | `Destroy` cha sau 1 frame, `await` trong `UniTask.ToCoroutine` bọc `try`/`catch (OperationCanceledException)` | nhánh `catch` chạy (cờ đặt trong `catch` là true; `Assert.Fail` sau `await` không chạy); không lỗi khác trong log |
| huỷ giữa lúc bay | `Destroy` cha ở nửa `appearDuration`, không `await` | không lỗi trong log |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/HorizontalRow/HorizontalRow.md` | tạo: `Code: Runtime/Utilities/Layout/HorizontalRow.cs` · "Trước khi chạy": gắn lên transform cha, con là object con trực tiếp, `Space` và `Child Local Y` theo art, con cần ẩn thì `SetActive(false)`, tự xếp một lần hai frame sau khi bật, vật trang trí cạnh hàng không làm con của cha · bảng ba động tác: ai gọi lúc nào, `await` gì, động tác nào dừng động tác nào · Design decisions, kể cả chờ bay xong rồi mới trượt, `space` âm không chặn · Dùng trong dự án: ô kéo hay `[RequireComponent]` + `GetComponent` |
| `docs/Runtime/HorizontalRow/HorizontalRow.html` | chọn từ `.md`: kịch bản (vào màn, mở thêm con, con rời hàng), cách dùng ("Trước khi chạy" nguyên vẹn, ba động tác), flow, lưu ý |
| `Scripts/Entities/Box/BoxesContainer.cs` | `using CategoryJam.Gameplay.Utilities;` → `using Horcrux.Runtime.Utilities.Layout;` (không type nào khác của namespace cũ trong file) · `HorizontalGrid _grid` → `HorizontalRow _grid` (tên field giữ: đã serialize trong prefab) · `_grid.SetSpaceAndPlayStartLevelAnimation(` → `_grid.SetSpaceAndPlayAppearAsync(` |
| `Scripts/Entities/Cache/CacheTileSlots.cs` | thêm `using Horcrux.Runtime.Utilities.Layout;` (giữ `using CategoryJam.Gameplay.Utilities;` vì `SetPositionX`) · `[RequireComponent(typeof(HorizontalGrid))]`, `HorizontalGrid _horizontalGrid` và 5 chỗ dùng (`GetComponent<HorizontalGrid>()`, `.RepositionNow()`, `.RepositionSmooth()`, `.Space`, `.LocalPositionOf(` trong `FlyTrayAsync`) → `HorizontalRow _row` · Tooltip ô hạt Extra Slot: `HorizontalGrid` → `HorizontalRow` |
| `Tests/EditMode/HorizontalGridLayoutTests.cs` + `.meta` | xoá: 5 case đã ở bảng GĐ1 của `HorizontalRowTests` |
| `Scripts/Feel.md` (đích khay, Máy kiểm), `Scripts/Layout.md` (cây `box_row`, `cache_bar`), `Scripts/Rendering.md` (`extra_slot_arrive`) | `HorizontalGrid`, `HorizontalGridLayoutTests` → `HorizontalRow` của Horcrux; Máy kiểm trỏ `HorizontalRowTests` (PlayMode, Horcrux) |
| GUID ① | `GameplayRoot.prefab` trỏ script bằng GUID `3b10628ba4222cf4092c8a20be197ec7` ở hai component; bản Horcrux nhận lại GUID này. Đóng Prefab Mode nếu đang mở `GameplayRoot.prefab` (Prefab Mode tự lưu đè file) |
| GUID ② | đóng Unity; xoá `Scripts/Utilities/HorizontalGrid.cs` + `.meta`; trong `Assets/Horcrux/Runtime/Utilities/Layout/HorizontalRow.cs.meta` thay dòng `guid:` bằng GUID trên (GUID Unity sinh cho bản Horcrux chưa asset nào trỏ) |
| GUID ③ | mở Unity, chờ import; `GameplayRoot.prefab` › `cache_bar`, `box_row`: Inspector hiện `Horizontal Row`, `Space`, `Child Local Y` giữ giá trị cũ, năm núm còn lại mặc định; `Boxes Container › _grid` vẫn trỏ component của `box_row` |
| kiểm | `verify.sh --csproj CategoryJam.Gameplay.csproj,CategoryJam.EditModeTests.csproj --tests CategoryJam.EditModeTests` xanh · `grep -rn "HorizontalGrid\|SetSpaceAndPlayStartLevelAnimation" Assets/_CategoryJam` rỗng · GUID trên ra đúng hai chỗ trong prefab và một `.meta` Horcrux · `HorizontalRowTests` PlayMode xanh |
| Plan này | xoá cùng `.meta` |
| chơi thử | theo bảng Kịch bản: màn 1 thanh khay đủ ô mở, nền vừa khít, hai hộp bay vào từ phải so le · booster thêm ô: ô mới trượt vào, khay Extra Slot bay đúng ô vừa mở · mở hộp thứ ba: hàng trượt hẹp lại · hộp trống ẩn, hộp còn lại dồn giữa; hỏng: hộp đứng ngoài mép phải, hộp mất sau khi vào màn, ô khay chồng hay lệch tâm, "Missing (Mono Script)" |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | lọc `activeInHierarchy` | cha tắt thì mọi con "tắt theo cây"; hàng vẫn phải xếp đúng để lúc cha bật không nhảy |
| bỏ | `GetComponentsInChildren` | cấp phát mỗi lần, lấy cả cháu |
| bỏ | Unity `HorizontalLayoutGroup` | chỉ cho `RectTransform` |
| bỏ | `TransformExtensions.PerformActionOnChildren` | duyệt cả con tắt, không giữ buffer theo chỉ số |
| bỏ | `SlotLocalX` thành member instance | test và chủ chưa có instance (tính trước bề ngang) phải gọi được |
| bỏ | `Tween.LocalPositionX(child, endValue, …)` không start | con đã đúng chỗ thì PrimeTween cảnh báo, kết thúc ngay; mất mốc `await` chung |
| bỏ | `List<Tween>` + `UniTask.WhenAll` cho trượt | chủ không còn `Sequence` để `Stop`, nút Inspector không trả được gì |
| bỏ | đặt sẵn con ở `targetX + appearOffsetX` thay vì tắt | con lộ ngoài mép phải trong `appearStartDelay` nếu offset nhỏ hơn nửa màn |
| bỏ | hằng `const` thời gian, offset như bản tạm | số tinh chỉnh cảm giác phơi Inspector; prefab chưa ghi key nhận mặc định C#, hành vi không đổi |
| bỏ | `DelayFrame(2)` không token như bản tạm | object huỷ giữa hai frame thì ghi lên transform đã huỷ |
| bỏ | chặn `space` âm | chủ chịu trách nhiệm dấu |
| bỏ | nút thử `[Button]` dời sang file Editor | Horcrux runtime đã dùng Odin ngoài Editor (`PersistenceDataEntry.cs`, `RemoteConfig.cs`) |
| bỏ | tên `HorizontalGrid`, `SetSpaceAndPlayStartLevelAnimation`, `RepositionAfterLayoutAsync` | một hàng không phải lưới · tên theo cơ chế, hậu tố `Async` vì trả `UniTask` · tên theo mốc chờ: con đã được sinh |
| bỏ | hàng dọc, lưới hai chiều qua cờ trục | chưa người dùng; cần thì component khác |
| sau | con khác cỡ: chế độ đọc `Renderer.bounds`, `space` giữ nghĩa khoảng cách tâm | khi có người dùng |
| sau | `[SerializeField] bool repositionOnStart = true` | khi chủ muốn tự quyết mốc xếp lần đầu |
| sau | list task bay vào thành field dùng lại | khi đo thấy cấp phát mỗi lần vào màn đáng kể; field dùng chung giữa các lượt async phải suy luận lượt chồng nhau |
