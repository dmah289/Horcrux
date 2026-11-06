# Plan — `HorizontalRow` lên Horcrux

Ngày 2026-11-06. Nguồn: category-jam `Assets/_CategoryJam/Scripts/Utilities/HorizontalGrid.cs`, đã chạy với hai chủ trong
`GameplayRoot.prefab`: hàng hộp (`BoxesContainer`, ô kéo `_grid` → `box_row`) và thanh khay (`CacheTileSlots`, `[RequireComponent]`
trên `cache_bar`). Xếp các con đang bật thành hàng ngang cách đều quanh gốc: xếp ngay, trượt tới chỗ, bay vào từ phải lúc vào level.

**Phân công:** GĐ1–3 developer gõ, agent viết test theo bảng case rồi compile · GĐ4 (tài liệu), GĐ5 (dự án chuyển sang bản Horcrux,
xoá bản tạm) agent làm. **Chạy được, thấy được** ở GĐ1–3 = compile sạch + bảng case xanh trong `Tests/PlayMode/HorizontalRowTests.cs`
(tween và chờ frame cần Play; `Horcrux.PlayModeTests.asmdef` đã tham chiếu `PrimeTween.Runtime` và `UniTask`); thấy trong game ở GĐ5.
GĐ1–4 commit trong `Assets/Horcrux`, GĐ5 commit ở dự án kèm con trỏ submodule. Không phụ thuộc Plan nào; chung thư mục
`Runtime/Utilities/Layout/` với `Horcrux_ViewportAnchor_Plan.md` — plan nào gõ trước thì tạo thư mục. Không có `§0`; công thức một
dòng ở GĐ1.

## Ngữ cảnh đã chốt

- **Người dùng:** cha có các con cùng cỡ xếp hàng ngang, số con bật đổi lúc chạy (slot hộp ẩn khi trống, mở thêm khi xem ads; ô
  khay mở dần theo level và booster).
- **Chỗ đặt, tên:** `Runtime/Utilities/Layout/HorizontalRow.cs`, namespace `Horcrux.Runtime.Utilities.Layout`. `HorizontalGrid` →
  `HorizontalRow`, `SetSpaceAndPlayStartLevelAnimation` → `SetSpaceAndPlayAppearAsync` (lý do ở GĐ1, GĐ3). Tên đổi tuỳ ý trước khi gõ.
- **Mục tiêu:** ba động tác, mỗi động tác một giai đoạn: `RepositionNow` · `RepositionSmooth` (trả `Sequence` để chủ chờ) ·
  `SetSpaceAndPlayAppearAsync`; mọi số thời gian và offset thành núm Inspector, mặc định bằng số đang chạy.
- **Thư viện:** PrimeTween, UniTask; Odin `[Button]` cho hai nút thử (lý do ở GĐ1); `[Splitter]` của Horcrux
  (`Runtime/Utilities/Attribute/SplitterAttribute.cs`, namespace cha nên không cần `using`).
- **Không làm:** hàng dọc, lưới hai chiều (chưa có người dùng; cần thì component khác, không phải cờ trục) · con khác cỡ (cần thì thêm
  chế độ đọc `Renderer.bounds`, `space` giữ nghĩa khoảng cách tâm).
- **Đã khảo sát:** `HorizontalGrid.cs` (commit `5c3549ed`, lấy cả cơ chế; chỗ khác ở bảng "Khác với bản dự án" từng GĐ) ·
  `BoxesContainer.cs` (bốn cách gọi, `space` đổi theo số hộp) · `CacheTileSlots.cs` (`GetComponent`, `RepositionNow` sau `SetLocked`,
  đọc `Space` tính thanh nền) · `GameDirector.StartLevelAsync` (thứ tự `OpenSlots` → `SpawnInitialBoxesAsync`) · `GameplayRoot.prefab`
  (GUID script `3b10628ba4222cf4092c8a20be197ec7`; `cache_bar` có 7 ô con sẵn, `box_row` không) · Unity `HorizontalLayoutGroup`
  (không hợp: chỉ cho `RectTransform`) · Horcrux `TransformExtensions.PerformActionOnChildren` (không hợp: duyệt cả con tắt, không
  giữ buffer theo chỉ số).

## Luồng mô tả — một hàng con qua một level

```
chủ gắn HorizontalRow lên cha; con là object con trực tiếp
        ▼
con bật / tắt ──► chủ gọi RepositionNow: xếp ngay; hai frame sau khi bật, hàng tự xếp một lần    GĐ1
        ▼
số con đổi giữa level ──► RepositionSmooth / SetSpaceAndRepositionSmooth, chủ await hoặc bỏ qua   GĐ2
        ▼
vào level ──► SetSpaceAndPlayAppearAsync: xếp ngay, giấu con, từng con bay vào từ phải so le      GĐ3
        ▼
tài liệu (GĐ4) · dự án dùng bản Horcrux, xoá bản tạm (GĐ5)
```

## Bản đồ hệ cuối

```
chủ (component trên cha; quyết con nào bật, gọi lúc nào, await hay không)
   │ RepositionNow / RepositionSmooth / SetSpaceAndRepositionSmooth / SetSpaceAndPlayAppearAsync / Space
   ▼
HorizontalRow ── GetActiveChildren: lọc activeSelf ──► con 0..n-1: localPosition.x = leftX + i·space
   ├─ _moveSequence ──► Tween.LocalPositionX (song song)
   └─ _appearSequences ──► delay → bật → Tween.LocalPositionX

Inspector: space, childLocalY · repositionDuration · appearDuration, appearStartDelay, appearDelayPerChild, appearOffsetX
           nút RepositionSmooth, RepositionNow
```

---

## Giai đoạn 1 — Chủ gắn component, con bật/tắt, xếp ngay

**Mục tiêu:** chủ bật/tắt con rồi gọi `RepositionNow`: con đang bật đứng cách đều quanh gốc ở `childLocalY`; chủ đọc `Space` tính
thanh nền. Hai frame sau khi bật, hàng tự xếp một lần. Xong thì thanh khay mở `n` ô đúng chỗ.

**Vì sao bây giờ:** lời gọi đầu tiên của mọi chủ lúc vào level (`OpenSlots` gọi ngay sau `SetLocked`); chưa có gì phải chờ nên chưa tween.

**Toán → code:** `n` con cách đều quanh gốc: `x_i = -(n - 1)·space / 2 + i·space`, `i = 0..n-1`; `n = 1` thì `x_0 = 0`.

### `Runtime/Utilities/Layout/HorizontalRow.cs` — file mới

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

        #endregion Properties

        #region Unity Callbacks

        private void Start() => RepositionAfterChildrenSpawnedAsync().Forget();

        #endregion Unity Callbacks

        #region API

        [Button]
        public void RepositionNow()
        {
            var activeChildren = GetActiveChildren();
            var activeChildCount = activeChildren.Count;
            var leftX = -(activeChildCount - 1) * space / 2;
            for (var i = 0; i < activeChildCount; i++)
                activeChildren[i].localPosition = new Vector3(leftX + i * space, childLocalY, 0f);
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
                if (child.gameObject.activeSelf)
                    _activeChildren.Add(child);
            }

            return _activeChildren;
        }

        #endregion Class Methods
    }
}
```

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| Tooltip `space`, `[Splitter]` | không có | có | hai núm đầu tiên của nhóm Configs; `Splitter` có drawer sẵn ở `Editor/Utilities/SplitterDrawer.cs` |
| `RepositionAfterLayoutAsync` | tên theo "layout" | `RepositionAfterChildrenSpawnedAsync` | tên theo mốc chờ: con đã được sinh |

**Quyết định** — `Start` chờ hai frame rồi tự xếp: hai chủ hiện tại đều gọi tường minh sau đó (`OpenSlots`, `SetSpaceAndRepositionSmooth`),
nhưng mốc tự xếp là hành vi quan sát được của component (ô khay sẵn trong prefab đứng đúng chỗ từ lúc scene lên, trước khi level
đầu nạp). Mở rộng sau: chủ muốn tự quyết mốc xếp lần đầu thì thêm `[SerializeField] bool repositionOnStart = true`, mặc định giữ
hành vi hiện có. — Lọc `activeSelf`, không `activeInHierarchy`:
cha tắt thì mọi con đều "tắt theo cây", hàng vẫn phải xếp đúng để lúc cha bật không nhảy. — Buffer `_activeChildren` dùng lại:
mọi API duyệt hết danh sách trước điểm `await` đầu tiên, nên không hai lượt chồng nhau. Phương án bỏ: `GetComponentsInChildren`
— cấp phát mỗi lần, lấy cả cháu. — Tên `HorizontalRow`, không giữ `HorizontalGrid`: một hàng, không phải lưới. — Odin `[Button]` trong
runtime: Horcrux runtime đã dùng Odin ngoài file Editor (`Abstractions/Foundations/Persistence/PersistenceDataEntry.cs`,
`Implementations/Foundations/RemoteConfigSystem/RemoteConfig.cs`); hai nút để developer thử trong Inspector.

| Consumes | Produces |
|---|---|
| `UnityEngine.MonoBehaviour`, `UniTask.DelayFrame`, `destroyCancellationToken` | `HorizontalRow.RepositionNow`, `Space`; núm `space`, `childLocalY` |

**Sơ đồ tương tác sau GĐ1** (`+` là phần mới)

```
  chủ ──RepositionNow+ / Space+──► HorizontalRow+ ──localPosition──► con đang bật
  Start+ ──2 frame──► RepositionNow
```

**Editor setup:** không — component gắn lên cha của chủ là việc của giai đoạn 5; trong Horcrux chỉ có test dựng object bằng code.

**Kiểm:** compile sạch. Test dựng `new GameObject("row").AddComponent<HorizontalRow>()`, con là `new GameObject().transform` với
`SetParent(row, false)`; `space` và các núm khác đặt bằng reflection vì không có setter (`Horcrux.PlayModeTests` không tham chiếu
`UnityEditor`, nên không dùng `SerializedObject`).

| Case | Input | Kỳ vọng |
|---|---|---|
| một con | `space 2`, `RepositionNow` | x = 0, y = `childLocalY`, z = 0 |
| ba con | `space 2` | x = −2, 0, 2 |
| con tắt | 3 con, con giữa `SetActive(false)` | hai con còn lại ở −1, 1; con tắt giữ nguyên `localPosition` |
| không con | `RepositionNow` | không ném |
| `space` 0 | 3 con | cả ba x = 0, không ném |
| `space` âm | 3 con, `space −2` | x = 2, 0, −2: thứ tự đảo, không chặn — chủ chịu trách nhiệm dấu |
| gọi lặp | `RepositionNow` hai lần | kết quả như một lần; buffer không phình (`_activeChildren.Count == n`, đọc qua reflection) |
| frame đầu | `AddComponent` lên cha có 2 con, không gọi gì, chờ 3 frame | hai con ở ±0.75 (mặc định `space 1.5`) |
| con sinh sau frame đầu | thêm con thứ ba ở frame 1 | sau mốc hai frame cả ba đã xếp |
| huỷ trước mốc | `Destroy` cha ngay sau `AddComponent`, chờ 3 frame | không lỗi trong log (`LogAssert.NoUnexpectedReceived`) |

**Mốc commit:** `feat(layout): HorizontalRow xếp ngay`.

---

## Giai đoạn 2 — Số con đổi giữa level: trượt tới chỗ

**Mục tiêu:** `RepositionSmooth` trả một `Sequence` gom tween song song của mọi con đang bật tới chỗ mới; `SetSpaceAndRepositionSmooth`
đổi `space` rồi trượt. Chủ `await` sequence khi cần chờ (mở thêm ô khay, mở slot ads) hoặc bỏ qua (dồn hộp khi hết tile). Xong giai
đoạn này hàng hộp co giãn theo số hộp.

**Vì sao bây giờ:** đổi số con giữa level là nhịp thứ hai của cả hai chủ; đặt ngay thì hộp nhảy, nên tween sinh cùng lúc với lý do có nó.

### `HorizontalRow.cs` › `using` › thêm sau `using Cysharp.Threading.Tasks;`

```csharp
using PrimeTween;
```

### `HorizontalRow.cs` › `Properties` › thêm sau `childLocalY`

```csharp
        [SerializeField] private float childLocalY;
        [Tooltip("Giây trượt tới chỗ mới khi RepositionSmooth.")]
        [SerializeField, Min(0f)] private float repositionDuration = 0.2f;

        private Sequence _moveSequence;
```

### `HorizontalRow.cs` › `API` › thêm trước `RepositionNow`

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
            StopMoves();

            var activeChildren = GetActiveChildren();
            var activeChildCount = activeChildren.Count;
            var leftX = -(activeChildCount - 1) * space / 2;

            _moveSequence = Sequence.Create();
            for (var i = 0; i < activeChildCount; i++)
            {
                var child = activeChildren[i];
                _moveSequence.Insert(0f, Tween.LocalPositionX(child, child.localPosition.x, leftX + i * space, repositionDuration, Ease.OutQuad));
            }

            return _moveSequence;
        }
```

### `HorizontalRow.cs` › `Class Methods` › thêm sau `RepositionAfterChildrenSpawnedAsync`

```csharp
        /// <summary>Dừng tween của chính hàng này trên các con, không chạm tween khác của con.</summary>
        private void StopMoves()
        {
            _moveSequence.Stop();
        }
```

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| `REPOSITION_DURATION` | `const 0.2f` | `[SerializeField] repositionDuration = 0.2f` | số tinh chỉnh cảm giác phơi Inspector; prefab chưa ghi key thì nhận mặc định C#, hành vi không đổi |

**Quyết định** — tween từ `child.localPosition.x` tường minh, không `Tween.LocalPositionX(child, endValue, …)`: con đã đúng chỗ thì
PrimeTween cảnh báo "end bằng current" và kết thúc ngay, sequence xong sớm hơn các con khác; start tường minh cho chủ một mốc
`await` thống nhất bằng `repositionDuration`. — `Insert(0f, …)` cho mọi con vào một `Sequence`: chủ `await` một thứ, `Stop` một thứ.
Phương án bỏ: `List<Tween>` và `UniTask.WhenAll` — chủ không còn `Sequence` để `Stop`, nút Inspector không trả được gì. — `StopMoves`
trước khi dựng: chủ gọi hai lần sát nhau (`SpawnInitialBoxesAsync` rồi `RepositionSlotsAsync`); không dừng thì hai tween ghi cùng
`localPosition.x`. `_moveSequence` mặc định là `default(Sequence)`, `Stop` trên nó không làm gì.

| Consumes | Produces |
|---|---|
| GĐ1 · `PrimeTween.Sequence`, `Tween.LocalPositionX` | `RepositionSmooth`, `SetSpaceAndRepositionSmooth`; núm `repositionDuration` |

**Sơ đồ tương tác sau GĐ2**

```
  chủ ──RepositionSmooth+ / SetSpaceAndRepositionSmooth+──► HorizontalRow ──StopMoves+──► _moveSequence cũ
                       ◄──Sequence+ (await hoặc bỏ)──          └─ Insert(0, LocalPositionX)+ ──► mỗi con đang bật
```

**Editor setup:** không.

**Kiểm:** compile sạch. Test chờ tween bằng `yield return new WaitForSeconds(repositionDuration + 0.05f)` trong `[UnityTest]`;
`TearDown` gọi `Tween.StopAll()` rồi `yield return null` như `Tests/PlayMode/CharmJumpTests.cs`.

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

**Mốc commit:** `feat(layout): HorizontalRow trượt tới chỗ`.

---

## Giai đoạn 3 — Vào level: con bay vào từ phải, so le

**Mục tiêu:** `SetSpaceAndPlayAppearAsync(space)` xếp ngay, chờ hai frame, giấu mọi con đang bật, rồi từng con bật lại và tween từ
`đích + appearOffsetX` về đích, con sau trễ hơn con trước `appearDelayPerChild`; chủ `await` tới khi con cuối tới nơi. Xong giai đoạn
này hàng hộp có màn vào level.

**Vì sao bây giờ:** động tác cuối của luồng, dùng cả `RepositionNow` lẫn `StopMoves`; mọc trên hai GĐ trước, không đụng chữ ký nào.

### `HorizontalRow.cs` › `Properties` › thêm sau `repositionDuration`

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
        private readonly List<UniTask> _appearTasks = new();
```

### `HorizontalRow.cs` › `API` › thêm sau `SetSpaceAndRepositionSmooth`, trước `RepositionSmooth`

```csharp
        /// <summary>Xếp ngay, rồi từng con bay vào từ bên phải, con sau trễ hơn con trước appearDelayPerChild.</summary>
        public async UniTask SetSpaceAndPlayAppearAsync(float newSpace)
        {
            space = newSpace;
            RepositionNow();

            await UniTask.DelayFrame(2, cancellationToken: destroyCancellationToken);

            StopMoves();

            var activeChildren = GetActiveChildren();
            var activeChildCount = activeChildren.Count;
            var leftX = -(activeChildCount - 1) * space / 2;

            _appearTasks.Clear();
            for (var i = 0; i < activeChildCount; i++)
            {
                var child = activeChildren[i];
                var targetX = leftX + i * space;

                child.gameObject.SetActive(false);

                var appear = Sequence.Create()
                    .ChainDelay(appearStartDelay + i * appearDelayPerChild)
                    .ChainCallback(child, static c => c.gameObject.SetActive(true))
                    .Chain(Tween.LocalPositionX(child, targetX + appearOffsetX, targetX, appearDuration, Ease.OutCubic));

                _appearSequences.Add(appear);
                _appearTasks.Add(appear);
            }

            await UniTask.WhenAll(_appearTasks);
            _appearTasks.Clear();
        }
```

### `HorizontalRow.cs` › `Class Methods` › `StopMoves` — đoạn cũ → đoạn mới

Cũ:

```csharp
        private void StopMoves()
        {
            _moveSequence.Stop();
        }
```

Mới — bay vào và trượt ghi cùng `localPosition.x`, nên một chỗ dừng cho cả hai:

```csharp
        private void StopMoves()
        {
            _moveSequence.Stop();

            foreach (var appear in _appearSequences)
                appear.Stop();
            _appearSequences.Clear();
        }
```

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| 4 hằng `APPEAR_*` | `const` | `[SerializeField]` cùng mặc định | như `repositionDuration` |
| `var tasks = new List<UniTask>(activeChildCount)` | cấp phát mỗi lần vào level | field `_appearTasks` dùng lại, `Clear` sau `WhenAll` | nhịp mỗi level, miễn phí |
| `await UniTask.DelayFrame(2)` | không token | kèm `destroyCancellationToken` | object huỷ giữa hai frame chờ thì không ghi lên transform đã huỷ; chủ `.Forget()` thì `OperationCanceledException` bị UniTask nuốt theo mặc định, chủ `await` thì nhận nó như mọi lệnh huỷ khác |
| `SetSpaceAndPlayStartLevelAnimation` | tên theo người dùng đầu | `SetSpaceAndPlayAppearAsync` | tên theo cơ chế, hậu tố `Async` vì trả `UniTask` |

**Quyết định** — giấu con bằng `SetActive(false)` rồi bật trong sequence, không dời ra ngoài màn: hàng không cần biết bề rộng màn;
con "chưa có" cho tới lượt của nó. Phương án bỏ: đặt sẵn ở `targetX + appearOffsetX` — con hiện ngoài mép phải trong `appearStartDelay`
nếu offset nhỏ hơn nửa màn. — `StopMoves` dừng cả sequence trượt lẫn mọi sequence bay vào: hai động tác ghi cùng thuộc tính; một
slot dừng là đủ, và `RepositionSmooth` gọi giữa lúc bay sẽ dừng mọi con đang bay. Hệ quả ghi ở tài liệu: con chưa tới lượt bật vẫn tắt,
nên chủ chờ bay xong rồi mới trượt (host hiện tại `await` tuần tự). — `RepositionNow` trước khi chờ hai frame: con về đúng chỗ ngay
frame gọi, con nào chủ bật sau lời gọi vẫn kịp vào danh sách giấu. `Giả định (cần xác nhận): nguồn không ghi lý do của hai frame
chờ; plan giữ nguyên số frame, lý do ở trên suy từ thứ tự gọi của host.`

| Consumes | Produces |
|---|---|
| GĐ2 · `UniTask.WhenAll`, `Sequence.ChainDelay/ChainCallback/Chain`, chuyển đổi ngầm `Sequence` → `UniTask` của PrimeTween | `SetSpaceAndPlayAppearAsync`; núm `appearDuration`, `appearStartDelay`, `appearDelayPerChild`, `appearOffsetX` |

**Sơ đồ tương tác sau GĐ3**

```
  chủ ──SetSpaceAndPlayAppearAsync+──► HorizontalRow ──RepositionNow──► con
         ◄──UniTask+ (await)──              │ 2 frame → StopMoves (dừng trượt + bay+) → SetActive(false)+ mỗi con
                                             └─ _appearSequences+: delay(i) → SetActive(true) → LocalPositionX(đích+offset → đích)
```

**Editor setup:** không.

**Kiểm:** compile sạch. API async chờ trong `[UnityTest]` qua `UniTask.ToCoroutine`; mốc thời gian đọc bằng `Time.time` tại
`ChainCallback` (test ghi `activeSelf` mỗi frame vào list).

| Case | Input | Kỳ vọng |
|---|---|---|
| bay vào | 2 con, `SetSpaceAndPlayAppearAsync(2)` | sau 2 frame cả hai `activeSelf` false; con 0 bật tại `appearStartDelay` ± 1 frame, con 1 trễ thêm `appearDelayPerChild`; lúc bật x ≈ đích + `appearOffsetX`; cuối x = −1, 1; `await` xong sau `appearStartDelay + appearDelayPerChild + appearDuration` ± 1 frame |
| một con | | bật tại `appearStartDelay`, cuối x = 0 |
| không con | | `await` xong ngay sau 2 frame, không ném |
| con tắt sẵn | 3 con, con giữa tắt trước khi gọi | con giữa vẫn tắt sau khi xong; hai con kia bay về −1, 1 |
| chạm số con lớn | 7 con | con cuối bật tại `appearStartDelay + 6·appearDelayPerChild`; cuối cách đều |
| bay rồi trượt | `RepositionSmooth` giữa lúc bay | mọi sequence bay `isAlive` false; con đã bật về đích trượt; con chưa tới lượt vẫn tắt |
| hai lần liên tiếp | gọi lần hai sau 3 frame | lượt đầu dừng, lượt hai chạy trọn, con cuối đúng chỗ, không con nào kẹt tắt |
| huỷ trước mốc | `Destroy` cha sau 1 frame, `await` trong test | `Assert.ThrowsAsync<OperationCanceledException>`; không lỗi khác trong log |
| huỷ giữa lúc bay | `Destroy` cha ở nửa `appearDuration`, không `await` | không lỗi trong log |

**Mốc commit:** `feat(layout): HorizontalRow bay vào từ phải`.

---

## Giai đoạn 4 — Tài liệu `docs/Runtime/HorizontalRow/HorizontalRow.md` và `HorizontalRow.html` (agent)

`.md` mở đầu `Code: Runtime/Utilities/Layout`. Mục: "Trước khi chạy" — gắn `HorizontalRow` lên transform cha; con là object con trực tiếp; `Space` và `Child Local Y` theo art; con cần
ẩn thì `SetActive(false)` (hàng chỉ đếm con đang bật); hàng tự xếp một lần hai frame sau khi bật · bảng ba động tác: ai gọi lúc nào, chủ
`await` gì, động tác nào dừng động tác nào · "Design decisions" gom các **Quyết định** của GĐ1–3, kể cả "chờ bay xong rồi mới trượt"
và `space` âm không chặn · "Dùng trong dự án": chủ giữ component qua ô kéo hay `[RequireComponent]` + `GetComponent`. `.html` chọn
từ `.md`: kịch bản (vào level, mở thêm con, con rời hàng), cách dùng ("Trước khi chạy" nguyên vẹn, ba động tác), flow, lưu ý.

**Mốc commit:** `docs(layout): HorizontalRow.md`.

---

## Giai đoạn 5 — Dự án dùng bản Horcrux, xoá bản tạm (agent)

**Mục tiêu:** hai chủ trỏ `HorizontalRow`; `HorizontalGrid.cs` xoá; hai component trong `GameplayRoot.prefab` giữ nguyên giá trị
`space`, `childLocalY` và không "Missing script". Đây là lúc "thấy trong game".

### Code dự án

| File | Cũ | Mới |
|---|---|---|
| `Entities/Box/BoxesContainer.cs` | `using CategoryJam.Gameplay.Utilities;` (không còn type nào khác của namespace này dùng trong file) · `HorizontalGrid _grid` · `_grid.SetSpaceAndPlayStartLevelAnimation(` | `using Horcrux.Runtime.Utilities.Layout;` · `HorizontalRow _grid` (tên field giữ: đã serialize trong prefab) · `_grid.SetSpaceAndPlayAppearAsync(` |
| `Entities/Cache/CacheTileSlots.cs` | `[RequireComponent(typeof(HorizontalGrid))]` · `HorizontalGrid _horizontalGrid` và 4 chỗ dùng (`GetComponent<HorizontalGrid>()`, `.RepositionNow()`, `.RepositionSmooth()`, `.Space`) | thêm `using Horcrux.Runtime.Utilities.Layout;` (giữ `using CategoryJam.Gameplay.Utilities;` vì `SetPositionX`) · `HorizontalRow _row` và 4 chỗ dùng |

Xoá `Assets/_CategoryJam/Scripts/Utilities/HorizontalGrid.cs` và `.meta`.

**Editor setup** — `GameplayRoot.prefab` trỏ script bằng GUID `3b10628ba4222cf4092c8a20be197ec7` ở hai component; bản Horcrux đã có
`.meta` riêng từ giai đoạn 1 nên không chuyển file bằng Project window được, mà **bản Horcrux nhận lại GUID của bản tạm**:

1. Đóng Prefab Mode nếu đang mở `GameplayRoot.prefab` (Prefab Mode tự lưu đè file).
2. Đóng Unity. Xoá `HorizontalGrid.cs` + `.meta` của dự án. Mở `Assets/Horcrux/Runtime/Utilities/Layout/HorizontalRow.cs.meta`, thay dòng
   `guid:` bằng `3b10628ba4222cf4092c8a20be197ec7` (GUID Unity sinh cho bản Horcrux chưa được asset nào trỏ, bỏ được).
3. Mở Unity, chờ import. Mở `GameplayRoot.prefab`, chọn `cache_bar` rồi `box_row`: Inspector hiện `Horizontal Row` với `Space`,
   `Child Local Y` giữ giá trị cũ của prefab, năm núm còn lại ở mặc định; `Boxes Container › _grid` vẫn trỏ component của `box_row`.

**Kiểm:** grep `HorizontalGrid`, `SetSpaceAndPlayStartLevelAnimation` trong `Assets/_CategoryJam` trả 0 · grep GUID trên ra đúng hai chỗ
trong prefab và một `.meta` của Horcrux · compile dự án sạch · test Horcrux xanh · **cần chơi thử** (developer): vào level 1 — thanh
khay hiện đủ số ô mở cách đều, nền thanh vừa khít; hai hộp bay vào từ phải so le sau nửa giây rồi đứng cách đều quanh tâm hàng. Dùng
booster thêm ô khay — ô mới trượt vào, nền thanh nới ra. Bấm slot khoá để mở hộp thứ ba — cả hàng trượt với khoảng cách hẹp hơn
(hai hộp rộng ×1.2). Chơi tới hết tile — hộp trống ẩn, hộp còn lại dồn về giữa. Dấu hiệu hỏng: hộp đứng ngoài mép phải màn (không
bay về); hộp mất sau khi vào level (bị tắt không bật lại); ô khay chồng nhau hay lệch tâm; Inspector báo "Missing (Mono Script)".

**Mốc commit:** repo dự án `refactor(layout): dự án dùng HorizontalRow của Horcrux` kèm con trỏ submodule.

---

## File → giai đoạn

| File | GĐ1 | GĐ2 | GĐ3 | GĐ4 | GĐ5 |
|---|---|---|---|---|---|
| `Runtime/Utilities/Layout/HorizontalRow.cs` | tạo: `space`, `childLocalY`, `Space`, `RepositionNow`, `Start` | `repositionDuration`, `_moveSequence`, `RepositionSmooth`, `SetSpaceAndRepositionSmooth`, `StopMoves` | 4 núm `appear*`, `_appearSequences`, `_appearTasks`, `SetSpaceAndPlayAppearAsync`, `StopMoves` dừng cả bay | | `.meta` nhận GUID bản tạm |
| `Tests/PlayMode/HorizontalRowTests.cs` (agent) | tạo | thêm case | thêm case | | |
| `docs/Runtime/HorizontalRow/HorizontalRow.md`, `.html` (agent) | | | | tạo | |
| dự án: `BoxesContainer.cs`, `CacheTileSlots.cs`, xoá `HorizontalGrid.cs` | | | | | đổi |
