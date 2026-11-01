# Plan — `HorizontalRow` lên Horcrux

Ngày 2026-11-02. Nguồn: category-jam, `Assets/_CategoryJam/Scripts/Utilities/HorizontalGrid.cs`, hai chủ thật: hàng box
(`BoxesContainer`, ô kéo `_grid`) và thanh khay (`CacheTileSlots`, `[RequireComponent]`). Xếp các con đang bật thành một hàng
ngang cách đều quanh gốc: xếp ngay, trượt tới chỗ, hoặc bay vào từ phải lúc vào level. Không gọi tên type dự án.

**Phân công:** Task 1 (Horcrux, code runtime) developer gõ · Task 2 (tài liệu) và Task 3 (test PlayMode) agent viết sau khi code có.
Dự án chuyển sang bản Horcrux (đổi `using`, đổi `.meta` theo luật giữ GUID của `reuse-gate`) là việc riêng khi developer yêu cầu.

Horcrux là submodule: Task 1–3 commit **trong `Assets/Horcrux`**.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | transform cha có các con cùng cỡ xếp thành hàng ngang, số con bật tắt đổi lúc chạy (box thêm bớt, ô khay mở dần) |
| Mục tiêu | ba động tác: `RepositionNow` (đặt ngay), `RepositionSmooth` (trượt, trả `Sequence` để chủ chờ), `SetSpaceAndPlayAppearAsync` (từng con bay vào từ phải, so le); mọi số thành núm Inspector với mặc định bằng số đang chạy |
| Chỗ đặt | `Runtime/Utilities/Layout/HorizontalRow.cs`, namespace `Horcrux.Runtime.Utilities.Layout` (cùng thư mục `ViewportAnchor` của plan ViewportAnchor nếu đã gõ; chưa có thì tạo) |
| Tên | `HorizontalGrid` đổi thành **`HorizontalRow`**: một hàng, không phải lưới. Hai API gộp tham số `space`: `SetSpaceAndRepositionSmooth(space)` giữ, `SetSpaceAndPlayStartLevelAnimation(space)` thành `SetSpaceAndPlayAppearAsync(space)` — "start level" là người dùng đầu tiên. Developer đổi tuỳ ý trước khi gõ |
| Thư viện | PrimeTween cho tween, UniTask cho chờ frame và `WhenAll` — stack mặc định, `com.horcrux.runtime` đã tham chiếu cả hai |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Hàng dọc, lưới hai chiều | chưa có người dùng; thêm là component khác (`VerticalRow`), không phải cờ trục trên cái này |
| Con không cùng cỡ (cách theo bề rộng từng con) | hai người dùng đều con cùng cỡ; cần thì thêm chế độ đọc `Renderer.bounds` sau, `space` giữ nghĩa khoảng cách tâm |
| Odin `[Button]` | Horcrux runtime đã dùng Odin ở `PersistenceDataEntry.cs`; giữ hai nút để developer thử trong Inspector |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| `Start` chờ hai frame rồi `RepositionNow` | con sinh ở `Awake` của cha (box clone theo sức chứa) có mặt sau frame đầu; hai chủ hiện tại dựa vào mốc này. Đổi thành chủ gọi tường minh là đổi hành vi quan sát được của hai chủ — việc chuyển dự án (ngoài plan) quyết, không đổi ở Horcrux |
| `RepositionSmooth` tween từ `child.localPosition.x` tới đích dù hai số bằng nhau | con đã đúng chỗ vẫn chờ trọn tween, chủ `await` sequence nhận một mốc thời gian thống nhất; PrimeTween không cảnh báo vì start tường minh |
| `SetSpaceAndPlayAppearAsync` tắt con rồi bật lại trong sequence | con "chưa có" cho tới lượt của nó; tắt bằng `SetActive` chứ không dời ra ngoài màn để không cần biết bề rộng màn |
| Một buffer `_activeChildren` dùng lại | mọi API duyệt xong danh sách trước khi `await`, nên không hai lượt chồng nhau |
| `StopMoves` dừng cả sequence trượt lẫn mọi sequence bay vào | hai động tác ghi cùng thuộc tính `localPosition.x`, gộp một slot dừng là đúng luật "cùng thuộc tính" |

**Mở rộng sau**

- Chủ muốn tự quyết mốc xếp lần đầu: thêm `[SerializeField] bool repositionOnStart = true`; mặc định giữ hành vi cũ.

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| `HorizontalGrid.cs` của dự án | toàn bộ | 5 hằng `const` thành `[SerializeField]`; tên hai API |
| Unity `HorizontalLayoutGroup` | — | chỉ cho `RectTransform` trên Canvas; đây là object world |
| Horcrux `Utilities/Layout/ViewportAnchor` (plan) | cùng thư mục | neo theo màn, không xếp con |
| Horcrux `TransformExtensions.PerformActionOnChildren` | — | duyệt mọi con kể cả tắt; ở đây cần lọc `activeSelf` và giữ buffer |

---

## Task 1 — Horcrux (developer gõ)

### `Runtime/Utilities/Layout/HorizontalRow.cs`

```csharp
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Layout
{
    /// <summary>Xếp các con đang bật thành một hàng ngang cách đều quanh gốc: xếp ngay, trượt tới chỗ, hoặc bay vào từ phải.</summary>
    public class HorizontalRow : MonoBehaviour
    {
        #region Properties

        [Splitter("Configs")]
        [Tooltip("Khoảng cách tâm hai con kề nhau, đơn vị world.")]
        [SerializeField] private float space = 1.5f;
        [Tooltip("Local y của mọi con: cho con nằm trên hay dưới sprite vẽ ở chính transform này.")]
        [SerializeField] private float childLocalY;
        [Tooltip("Giây trượt tới chỗ mới khi RepositionSmooth.")]
        [SerializeField, Min(0f)] private float repositionDuration = 0.2f;
        [Tooltip("Giây mỗi con bay vào từ phải khi SetSpaceAndPlayAppearAsync.")]
        [SerializeField, Min(0f)] private float appearDuration = 0.3f;
        [Tooltip("Giây chờ trước khi con đầu bay vào.")]
        [SerializeField, Min(0f)] private float appearStartDelay = 0.5f;
        [Tooltip("Con sau bay vào trễ hơn con trước bấy nhiêu giây.")]
        [SerializeField, Min(0f)] private float appearDelayPerChild = 0.05f;
        [Tooltip("Con bay vào từ bên phải đích bấy nhiêu đơn vị world.")]
        [SerializeField] private float appearOffsetX = 15f;

        private Sequence _moveSequence;
        private readonly List<Sequence> _appearSequences = new();
        // Mọi API duyệt xong danh sách trước khi await, nên một buffer là đủ.
        private readonly List<Transform> _activeChildren = new();
        private readonly List<UniTask> _appearTasks = new();

        /// <summary>Khoảng cách tâm hai con kề nhau.</summary>
        public float Space => space;

        #endregion Properties

        #region Unity Callbacks

        private void Start() => RepositionAfterChildrenSpawnedAsync().Forget();

        #endregion Unity Callbacks

        #region API

        public Sequence SetSpaceAndRepositionSmooth(float newSpace)
        {
            space = newSpace;
            return RepositionSmooth();
        }

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

        /// <summary>Hai frame sau khi bật: con sinh ở Awake của cha đã có mặt.</summary>
        private async UniTaskVoid RepositionAfterChildrenSpawnedAsync()
        {
            await UniTask.DelayFrame(2, cancellationToken: destroyCancellationToken);
            RepositionNow();
        }

        /// <summary>Dừng tween của chính hàng này trên các con, không chạm tween khác của con.</summary>
        private void StopMoves()
        {
            _moveSequence.Stop();

            foreach (var appear in _appearSequences)
                appear.Stop();
            _appearSequences.Clear();
        }

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

**Khác với bản dự án, và vì sao**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| 5 hằng `const` | `REPOSITION_DURATION`… | `[SerializeField]` mặc định cùng số | số tinh chỉnh cảm giác phơi Inspector; prefab chưa ghi key thì nhận mặc định C#, hành vi không đổi |
| `tasks = new List<UniTask>(count)` mỗi lần vào level | cấp phát | field `_appearTasks` dùng lại | nhịp mỗi level, miễn phí |
| `SetSpaceAndPlayStartLevelAnimation` | `UniTask.DelayFrame(2)` không token | kèm `destroyCancellationToken` | object huỷ giữa hai frame chờ thì không ghi lên transform đã huỷ |
| tên | `HorizontalGrid`, `…PlayStartLevelAnimation` | `HorizontalRow`, `…PlayAppearAsync` | tên theo cơ chế |

### Nghiệm thu Task 1

`com.horcrux.runtime.csproj` build sạch. Prefab của dự án vẫn trỏ script cũ cho tới khi dự án chuyển (ngoài plan).

---

## Task 2 — Tài liệu `Runtime/Utilities/Layout/HorizontalRow.md` (agent)

"Trước khi chạy": gắn `HorizontalRow` lên transform cha; con là object con trực tiếp; `Space` và `Child Local Y` theo art; con cần
ẩn thì `SetActive(false)` (hàng chỉ đếm con đang bật). Bảng ba động tác và ai `await` gì. "Design decisions" là bảng trên.

## Task 3 — Test PlayMode (agent)

`Tests/PlayMode/HorizontalRowTests.cs` (tween cần Play).

| Case | Input | Kỳ vọng |
|---|---|---|
| một con | `space 2`, `RepositionNow` | x = 0 |
| ba con | `space 2` | x = −2, 0, 2; y = `childLocalY` |
| con tắt | 3 con, con giữa tắt | hai con còn lại ở −1, 1 |
| không con | `RepositionNow` | không ném |
| trượt | 2 con ở x = 5, `RepositionSmooth` rồi chờ `Sequence` | x = −1, 1; sequence xong sau `repositionDuration` |
| trượt khi đã đúng chỗ | con đã ở đích | sequence vẫn sống trọn `repositionDuration` |
| trượt hai lần liên tiếp | gọi `RepositionSmooth` lần hai ngay | sequence đầu `isAlive` false, con tới đích lần hai |
| bay vào | 2 con, `SetSpaceAndPlayAppearAsync(2)` | ngay sau gọi (sau 2 frame) cả hai tắt; con 0 bật tại `appearStartDelay`, con 1 trễ thêm `appearDelayPerChild`; cuối x = −1, 1 |
| bay vào rồi trượt | gọi `RepositionSmooth` giữa lúc bay | mọi sequence bay dừng, con về đích trượt |
| huỷ giữa chừng | gọi `SetSpaceAndPlayAppearAsync(2)` rồi Destroy object sau 1 frame, `await` trong test | `Assert.ThrowsAsync<OperationCanceledException>` (`DelayFrame` với `destroyCancellationToken` ném); không ghi lên transform đã huỷ, không lỗi khác trong log |
