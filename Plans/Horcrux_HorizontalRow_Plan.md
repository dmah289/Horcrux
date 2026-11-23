# Plan — `HorizontalRow`: xếp các con đang bật thành hàng ngang cách đều

Ngày 2026-11-02. Nguồn: category-jam `Assets/_CategoryJam/Scripts/Utilities/HorizontalGrid.cs` (`SlotLocalX`, `LocalPositionOf`,
`RepositionNow`, `RepositionSmooth`, `SetSpaceAndPlayStartLevelAnimation`), hai chủ trong `GameplayRoot.prefab`: hàng hộp
(`BoxesContainer`, ô kéo `_grid` → `box_row`) và thanh khay (`CacheTileSlots`, `[RequireComponent]` trên `cache_bar`), đã chơi thử.
Xếp các con đang bật thành hàng ngang cách đều quanh gốc: xếp ngay, trượt tới chỗ, bay vào từ phải lúc vào màn.

**Phân công:** GĐ1–3 developer gõ, agent viết test theo bảng case rồi compile · GĐ4 (tài liệu) · GĐ5 (dự án chuyển sang bản Horcrux,
xoá bản tạm, xoá Plan) agent làm.
**Chạy được, thấy được:** GĐ1–3 compile sạch + bảng case xanh trong `Tests/PlayMode/HorizontalRowTests.cs` (tween và chờ frame cần
Play; `Horcrux.PlayModeTests.asmdef` đã tham chiếu `PrimeTween.Runtime` và `UniTask`); thấy trong game ở GĐ5.
**Lệnh kiểm** (chạy từ gốc dự án): `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.PlayModeTests.csproj`;
test PlayMode không qua tool này — developer chạy trong Window › General › Test Runner › PlayMode.
**Commit:** GĐ1–4 trong `Assets/Horcrux`; GĐ5 ở dự án kèm con trỏ submodule.
**Phụ thuộc:** không.
**Đã kiểm trước khi ghi:** `HorizontalRow.cs` ở ba trạng thái (sau GĐ1, GĐ2, GĐ3) qua `csc -langversion:9` trên
`UnityEngine.CoreModule` của Editor 6000.0.73f1, `com.horcrux.runtime.dll` (`Splitter`), `UniTask.dll`, `PrimeTween.Runtime.dll`,
`Sirenix.OdinInspector.Attributes.dll` — sạch, kể cả chuyển ngầm `Sequence` → `UniTask` ở GĐ3. Thuật toán là bản tạm đang chạy trong
game; không có toán mới. `§0`: không — công thức một dòng ở GĐ1.

## Ngữ cảnh đã chốt

- **Người dùng:** cha có các con cùng cỡ xếp hàng ngang, số con bật đổi lúc chạy: slot ẩn khi trống, mở thêm ô khi xem quảng cáo, ô
  khay mở dần theo màn và booster.
- **Mục tiêu:** ba động tác, mỗi động tác một GĐ: `RepositionNow` · `RepositionSmooth` (trả `Sequence` để chủ chờ) ·
  `SetSpaceAndPlayAppearAsync`; mọi số thời gian và offset thành núm Inspector, mặc định bằng số đang chạy.
- **Ranh giới:** hàng chỉ đặt `localPosition.x`, `y` của con đang bật; chủ quyết con nào bật, gọi lúc nào, chờ hay không.
- **Chỗ đặt, tên:** `Runtime/Utilities/Layout/HorizontalRow.cs`, namespace `Horcrux.Runtime.Utilities.Layout`. `HorizontalGrid` →
  `HorizontalRow` (một hàng, không phải lưới), `SetSpaceAndPlayStartLevelAnimation` → `SetSpaceAndPlayAppearAsync` (tên theo cơ
  chế, hậu tố `Async` vì trả `UniTask`). Tên đổi tuỳ ý trước khi gõ.
- **Thư viện:** PrimeTween, UniTask; Odin `[Button]` cho hai nút thử; `[Splitter]` của Horcrux
  (`Runtime/Utilities/Attribute/SplitterAttribute.cs`, namespace cha nên không cần `using`).
- **Cố ý không làm:** hàng dọc, lưới hai chiều — chưa có người dùng; cần thì component khác, không phải cờ trục · con khác cỡ — chưa có
  người dùng.
- **Quyết định trái trực giác:**
  - Lọc `activeSelf`, không `activeInHierarchy`: cha tắt thì mọi con đều "tắt theo cây", hàng vẫn phải xếp đúng để lúc cha bật không nhảy.
  - Giấu con bằng `SetActive(false)` lúc bay vào, không dời ra ngoài màn: hàng không cần biết bề rộng màn.
  - `RepositionSmooth` tween từ chỗ đang đứng kể cả con đã đúng chỗ: chủ `await` một mốc chung bằng `repositionDuration`.
- **Mở rộng sau:** con khác cỡ — chế độ đọc `Renderer.bounds`, `space` giữ nghĩa khoảng cách tâm · `[SerializeField] bool
  repositionOnStart = true` khi chủ muốn tự quyết mốc xếp lần đầu · list task bay vào thành field dùng lại khi đo thấy cấp phát mỗi lần
  vào màn đáng kể (giờ một list mỗi lần vào màn).
- `Giả định (cần xác nhận): nguồn không ghi lý do của hai frame chờ trước khi bay vào và trước lần tự xếp đầu; Plan giữ nguyên số
  frame, lý do ở GĐ1, GĐ3 suy từ thứ tự gọi của chủ.`
- `Giả định (cần xác nhận): giữ hai nút [Button] (RepositionNow, RepositionSmooth) trong runtime để developer thử trong Inspector.`

## Đã khảo sát

| Nguồn | Lấy | Không lấy, vì sao |
|---|---|---|
| category-jam `Scripts/Utilities/HorizontalGrid.cs` (bản tạm, commit `5c3549ed`, `a179102a` thêm `SlotLocalX`, `LocalPositionOf`) | cả cơ chế | hằng `const` thời gian, offset → núm Inspector; chỗ khác ở bảng "Khác với bản dự án" từng GĐ |
| category-jam `BoxesContainer.cs` | bốn cách gọi; `space` đổi theo số hộp | — |
| category-jam `CacheTileSlots.cs` | `GetComponent`, `RepositionNow` sau `SetLocked`, đọc `Space` tính thanh nền, `LocalPositionOf` làm đích khay bay | — |
| category-jam `GameDirector.StartLevelAsync` | thứ tự `OpenSlots` → `SpawnInitialBoxesAsync` | — |
| category-jam `GameplayRoot.prefab` | GUID script `3b10628ba4222cf4092c8a20be197ec7`; `cache_bar` có 7 ô con sẵn, `box_row` không | — |
| Unity `HorizontalLayoutGroup` | — | chỉ cho `RectTransform` |
| Horcrux `TransformExtensions.PerformActionOnChildren` | — | duyệt cả con tắt, không giữ buffer theo chỉ số |

## Luồng — một hàng con qua một màn

```
chủ gắn HorizontalRow lên cha; con là object con trực tiếp
        ▼
con bật / tắt ──► chủ gọi RepositionNow: xếp ngay; hai frame sau khi bật, hàng tự xếp một lần    GĐ1
        ▼
số con đổi giữa màn ──► RepositionSmooth / SetSpaceAndRepositionSmooth, chủ await hoặc bỏ qua    GĐ2
        ▼
vào màn ──► SetSpaceAndPlayAppearAsync: xếp ngay, giấu con, từng con bay vào từ phải so le        GĐ3
        ▼
tài liệu (GĐ4) · dự án dùng bản Horcrux, xoá bản tạm (GĐ5)
```

## Bản đồ hệ cuối

```
chủ (component trên cha; quyết con nào bật, gọi lúc nào, await hay không)
   │ RepositionNow / RepositionSmooth / SetSpaceAndRepositionSmooth / SetSpaceAndPlayAppearAsync / Space / LocalPositionOf
   ▼
HorizontalRow ── GetActiveChildren: lọc activeSelf ──► con 0..n-1: localPosition.x = SlotLocalX(i, n, space)
   ├─ LocalPositionOf(i, n) ──► chủ đọc chỗ nghỉ tương lai (đích chuyến bay) trước khi hàng trượt xong
   ├─ _moveSequence ──► Tween.LocalPositionX (song song)
   └─ _appearSequences ──► delay → bật → Tween.LocalPositionX

Inspector: space, childLocalY · repositionDuration · appearDuration, appearStartDelay, appearDelayPerChild, appearOffsetX
           nút RepositionSmooth, RepositionNow
```

---

## Giai đoạn 1 — Chủ gắn component, con bật/tắt, xếp ngay

**Mục tiêu:** chủ bật/tắt con rồi gọi `RepositionNow`: con đang bật đứng cách đều quanh gốc ở `childLocalY`; chủ đọc `Space` tính
thanh nền. Hai frame sau khi bật, hàng tự xếp một lần. Chủ hỏi được trước chỗ nghỉ của con thứ `i` khi có `n` con
(`LocalPositionOf`) — đích của vật bay tới ô vừa mở, lúc hàng chưa trượt xong. Xong thì một thanh mở `n` ô đúng chỗ.

**Vì sao bây giờ:** lời gọi đầu tiên của mọi chủ lúc vào màn (mở ô ngay sau khi đặt khoá); chưa có gì phải chờ nên chưa tween.

**Sơ đồ sau GĐ1** (`+` là phần mới)

```
  chủ ──RepositionNow+ / Space+──► HorizontalRow+ ──localPosition──► con đang bật
      ──LocalPositionOf+(i, n)──► chỗ nghỉ tương lai (đích chuyến bay)     cả hai qua SlotLocalX+
  Start+ ──2 frame──► RepositionNow
```

**Toán → code:** `n` con cách đều quanh gốc: `x_i = -(n - 1)·space / 2 + i·space`, `i = 0..n-1`; `n = 1` thì `x_0 = 0`. Công thức
nằm trong **một** hàm thuần `SlotLocalX`; mọi động tác xếp (GĐ1–GĐ3) và `LocalPositionOf` gọi nó.

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

        /// <summary>x cục bộ của con thứ index trong count con cách đều space, căn giữa quanh gốc.</summary>
        public static float SlotLocalX(int index, int count, float space) => -(count - 1) * space / 2 + index * space;

        /// <summary>Chỗ nghỉ cục bộ của con thứ index khi có count con bật; đọc được trước khi hàng trượt xong.</summary>
        public Vector3 LocalPositionOf(int index, int count) => new(SlotLocalX(index, count, space), childLocalY, 0f);

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

**Consumes / Produces:** consumes `MonoBehaviour`, `UniTask.DelayFrame`, `destroyCancellationToken` · produces
`HorizontalRow.RepositionNow`, `Space`, `SlotLocalX`, `LocalPositionOf`; núm `space`, `childLocalY`.

**Quyết định:**
- `Start` chờ hai frame rồi tự xếp: hai chủ hiện tại đều gọi tường minh sau đó, nhưng mốc tự xếp là hành vi quan sát được của component
  (ô sẵn trong prefab đứng đúng chỗ từ lúc scene lên, trước khi màn đầu nạp).
- Buffer `_activeChildren` dùng lại: mọi API duyệt hết danh sách trước điểm `await` đầu tiên, nên không hai lượt chồng nhau. Phương
  án bỏ: `GetComponentsInChildren` — cấp phát mỗi lần, lấy cả cháu.
- Odin `[Button]` trong runtime: Horcrux runtime đã dùng Odin ngoài file Editor (`Abstractions/Foundations/Persistence/PersistenceDataEntry.cs`,
  `Implementations/Foundations/RemoteConfigSystem/RemoteConfig.cs`).
- `LocalPositionOf` public: vật bay tới ô vừa mở cần đích **trước** khi hàng trượt xong; `localPosition` của con lúc cất cánh còn là chỗ
  cũ. Xếp và dự đoán là hai bản buộc khớp nên cùng gọi `SlotLocalX`. `SlotLocalX` static để test và chủ không có instance (tính trước
  bề ngang) gọi được.

**Editor setup:** không — gắn component lên cha của chủ là việc của GĐ5; trong Horcrux chỉ có test dựng object bằng code.

**Kiểm:** lệnh kiểm ở đầu Plan. Test dựng `new GameObject("row").AddComponent<HorizontalRow>()`, con là `new GameObject().transform`
với `SetParent(row, false)`; `space` và các núm khác đặt bằng reflection vì không có setter (`Horcrux.PlayModeTests` không tham chiếu
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
| `SlotLocalX` | (0, 1, 2) · (0, 3, 2), (1, 3, 2), (2, 3, 2) · (3, 4, 1.5) · (3, 7, 1.5) · (0, 6, 1.5), (5, 6, 1.5) · (6, 7, 1.5) | 0 · −2, 0, 2 · 2.25 · 0 · −3.75, 3.75 · 4.5 |
| `LocalPositionOf` | `space 2`, `childLocalY 0.5`, (2, 3) | (2, 0.5, 0) |
| dự đoán khớp xếp | n = 1…7 con, `RepositionNow` | mọi i: `localPosition` **bằng tuyệt đối** `LocalPositionOf(i, n)` |

**Mốc commit:** `feat(layout): HorizontalRow xếp ngay` (Horcrux).

---

## Giai đoạn 2 — Số con đổi giữa màn: trượt tới chỗ

**Mục tiêu:** `RepositionSmooth` trả một `Sequence` gom tween song song của mọi con đang bật tới chỗ mới; `SetSpaceAndRepositionSmooth`
đổi `space` rồi trượt. Chủ `await` sequence khi cần chờ (mở thêm ô) hoặc bỏ qua (dồn con khi một con rời hàng). Xong GĐ này hàng co
giãn theo số con.

**Vì sao bây giờ:** đổi số con giữa màn là nhịp thứ hai của cả hai chủ; đặt ngay thì con nhảy, nên tween sinh cùng lúc với lý do có nó.

**Sơ đồ sau GĐ2**

```
  chủ ──RepositionSmooth+ / SetSpaceAndRepositionSmooth+──► HorizontalRow ──StopMoves+──► _moveSequence cũ
                       ◄──Sequence+ (await hoặc bỏ)──          └─ Insert(0, LocalPositionX)+ ──► mỗi con đang bật
```

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

            _moveSequence = Sequence.Create();
            for (var i = 0; i < activeChildCount; i++)
            {
                var child = activeChildren[i];
                _moveSequence.Insert(0f, Tween.LocalPositionX(child, child.localPosition.x, SlotLocalX(i, activeChildCount, space), repositionDuration, Ease.OutQuad));
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

**Consumes / Produces:** consumes GĐ1, `PrimeTween.Sequence`, `Tween.LocalPositionX` · produces `RepositionSmooth`,
`SetSpaceAndRepositionSmooth`; núm `repositionDuration`.

**Quyết định:**
- Tween từ `child.localPosition.x` tường minh, không `Tween.LocalPositionX(child, endValue, …)`: con đã đúng chỗ thì PrimeTween cảnh
  báo "end bằng current" và kết thúc ngay, sequence xong sớm hơn các con khác; start tường minh cho chủ một mốc `await` thống nhất.
- `Insert(0f, …)` cho mọi con vào một `Sequence`: chủ `await` một thứ, `Stop` một thứ. Phương án bỏ: `List<Tween>` và
  `UniTask.WhenAll` — chủ không còn `Sequence` để `Stop`, nút Inspector không trả được gì.
- `StopMoves` trước khi dựng: chủ gọi hai lần sát nhau; không dừng thì hai tween ghi cùng `localPosition.x`. `_moveSequence` mặc định
  là `default(Sequence)`, `Stop` trên nó không làm gì.

**Editor setup:** không.

**Kiểm:** lệnh kiểm ở đầu Plan. Test chờ tween bằng `yield return new WaitForSeconds(repositionDuration + 0.05f)` trong `[UnityTest]`;
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

**Mốc commit:** `feat(layout): HorizontalRow trượt tới chỗ` (Horcrux).

---

## Giai đoạn 3 — Vào màn: con bay vào từ phải, so le

**Mục tiêu:** `SetSpaceAndPlayAppearAsync(space)` xếp ngay, chờ hai frame, giấu mọi con đang bật, rồi từng con bật lại và tween từ
`đích + appearOffsetX` về đích, con sau trễ hơn con trước `appearDelayPerChild`; chủ `await` tới khi con cuối tới nơi. Xong GĐ này
hàng có màn vào.

**Vì sao bây giờ:** động tác cuối của luồng, dùng cả `RepositionNow` lẫn `StopMoves`; mọc trên hai GĐ trước, không đụng chữ ký nào.

**Sơ đồ sau GĐ3**

```
  chủ ──SetSpaceAndPlayAppearAsync+──► HorizontalRow ──RepositionNow──► con
         ◄──UniTask+ (await)──              │ 2 frame → StopMoves (dừng trượt + bay+) → SetActive(false)+ mỗi con
                                             └─ _appearSequences+: delay(i) → SetActive(true) → LocalPositionX(đích+offset → đích)
```

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

            var appearTasks = new List<UniTask>(activeChildCount);
            for (var i = 0; i < activeChildCount; i++)
            {
                var child = activeChildren[i];
                var targetX = SlotLocalX(i, activeChildCount, space);

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
| `await UniTask.DelayFrame(2)` | không token | kèm `destroyCancellationToken` | object huỷ giữa hai frame chờ thì không ghi lên transform đã huỷ; chủ `.Forget()` thì `OperationCanceledException` bị UniTask nuốt theo mặc định, chủ `await` thì nhận nó như mọi lệnh huỷ khác |
| `SetSpaceAndPlayStartLevelAnimation` | tên theo người dùng đầu | `SetSpaceAndPlayAppearAsync` | tên theo cơ chế, hậu tố `Async` vì trả `UniTask` |

**Consumes / Produces:** consumes GĐ2, `UniTask.WhenAll`, `Sequence.ChainDelay/ChainCallback/Chain`, chuyển ngầm `Sequence` → `UniTask`
của PrimeTween · produces `SetSpaceAndPlayAppearAsync`; núm `appearDuration`, `appearStartDelay`, `appearDelayPerChild`, `appearOffsetX`.

**Quyết định:**
- Giấu con bằng `SetActive(false)` rồi bật trong sequence, không dời ra ngoài màn: con "chưa có" cho tới lượt của nó. Phương án bỏ: đặt
  sẵn ở `targetX + appearOffsetX` — con hiện ngoài mép phải trong `appearStartDelay` nếu offset nhỏ hơn nửa màn.
- `StopMoves` dừng cả sequence trượt lẫn mọi sequence bay vào: hai động tác ghi cùng thuộc tính; một chỗ dừng là đủ, và
  `RepositionSmooth` gọi giữa lúc bay sẽ dừng mọi con đang bay. Hệ quả ghi ở tài liệu: con chưa tới lượt bật vẫn tắt, nên chủ chờ bay
  xong rồi mới trượt (chủ hiện tại `await` tuần tự).
- `RepositionNow` trước khi chờ hai frame: con về đúng chỗ ngay frame gọi, con nào chủ bật sau lời gọi vẫn kịp vào danh sách giấu (giả
  định ở "Ngữ cảnh đã chốt").
- List task cục bộ, như bản dự án: một lần cấp phát mỗi lần vào màn; field dùng chung giữa các lượt async thì phải suy luận lượt chồng
  nhau, không đáng.

**Editor setup:** không.

**Kiểm:** lệnh kiểm ở đầu Plan. API async chờ trong `[UnityTest]` qua `UniTask.ToCoroutine`; mốc thời gian đọc bằng `Time.time` tại
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
| huỷ trước mốc | `Destroy` cha sau 1 frame, `await` trong `UniTask.ToCoroutine` bọc `try`/`catch (OperationCanceledException)` | nhánh `catch` chạy (cờ đặt trong `catch` là true; `Assert.Fail` sau `await` không chạy); không lỗi khác trong log |
| huỷ giữa lúc bay | `Destroy` cha ở nửa `appearDuration`, không `await` | không lỗi trong log |

`Assert.ThrowsAsync` của NUnit chờ `Task` chặn luồng chính, không dùng được với UniTask trong `[UnityTest]`.

**Mốc commit:** `feat(layout): HorizontalRow bay vào từ phải` (Horcrux).

---

## Giai đoạn 4 — Tài liệu `docs/Runtime/HorizontalRow/HorizontalRow.md` và `HorizontalRow.html` (agent)

`.md` mở đầu `Code: Runtime/Utilities/Layout/HorizontalRow.cs`. Mục: "Trước khi chạy" — gắn `HorizontalRow` lên transform cha; con là
object con trực tiếp; `Space` và `Child Local Y` theo art; con cần ẩn thì `SetActive(false)` (hàng chỉ đếm con đang bật); hàng tự xếp
một lần hai frame sau khi bật; vật trang trí đặt cạnh hàng thì không làm con của cha (hàng xếp mọi con đang bật) · bảng ba động tác: ai
gọi lúc nào, chủ `await` gì, động tác nào dừng động tác nào · "Design decisions" gom các **Quyết định** của GĐ1–3, kể cả "chờ bay xong
rồi mới trượt" và `space` âm không chặn · "Dùng trong dự án": chủ giữ component qua ô kéo hay `[RequireComponent]` + `GetComponent`.
`.html` chọn từ `.md`: kịch bản (vào màn, mở thêm con, con rời hàng), cách dùng ("Trước khi chạy" nguyên vẹn, ba động tác), flow, lưu ý.

**Mốc commit:** `docs(layout): HorizontalRow.md` (Horcrux).

---

## Giai đoạn 5 — Dự án dùng bản Horcrux, xoá bản tạm, xoá Plan (agent)

**Mục tiêu:** hai chủ trỏ `HorizontalRow`; `HorizontalGrid.cs` xoá; hai component trong `GameplayRoot.prefab` giữ nguyên giá trị
`space`, `childLocalY` và không "Missing script".

### Chỗ đổi trong category-jam

| File | Cũ | Mới |
|---|---|---|
| `Scripts/Entities/Box/BoxesContainer.cs` | `using CategoryJam.Gameplay.Utilities;` (không còn type nào khác của namespace này dùng trong file) · `HorizontalGrid _grid` · `_grid.SetSpaceAndPlayStartLevelAnimation(` | `using Horcrux.Runtime.Utilities.Layout;` · `HorizontalRow _grid` (tên field giữ: đã serialize trong prefab) · `_grid.SetSpaceAndPlayAppearAsync(` |
| `Scripts/Entities/Cache/CacheTileSlots.cs` | `[RequireComponent(typeof(HorizontalGrid))]` · `HorizontalGrid _horizontalGrid` và 5 chỗ dùng (`GetComponent<HorizontalGrid>()`, `.RepositionNow()`, `.RepositionSmooth()`, `.Space`, `.LocalPositionOf(` — đích khay Extra Slot trong `FlyTrayAsync`) · Tooltip ô hạt Extra Slot "… `HorizontalGrid` xếp mọi con đang bật." | thêm `using Horcrux.Runtime.Utilities.Layout;` (giữ `using CategoryJam.Gameplay.Utilities;` vì `SetPositionX`) · `HorizontalRow _row` và 5 chỗ dùng · Tooltip: `HorizontalRow` |
| `Tests/EditMode/HorizontalGridLayoutTests.cs` + `.meta` | 5 case `SlotLocalX`, `LocalPositionOf` trên `HorizontalGrid` | xoá: các case đã ở bảng GĐ1 của `HorizontalRowTests` |
| `Scripts/Feel.md` (đích khay, Máy kiểm), `Scripts/Layout.md` (cây `box_row`, `cache_bar`), `Scripts/Rendering.md` (`extra_slot_arrive`) | `HorizontalGrid`, `HorizontalGridLayoutTests` | `HorizontalRow` của Horcrux; Máy kiểm trỏ `HorizontalRowTests` (PlayMode, Horcrux) |
| `Scripts/Utilities/HorizontalGrid.cs` + `.meta` | | xoá |
| `Assets/Horcrux/Plans/Horcrux_HorizontalRow_Plan.md` + `.meta` | | xoá |

**Editor setup** — `GameplayRoot.prefab` trỏ script bằng GUID `3b10628ba4222cf4092c8a20be197ec7` ở hai component; bản Horcrux đã có
`.meta` riêng từ GĐ1 nên không chuyển file bằng Project window được, mà **bản Horcrux nhận lại GUID của bản tạm**:

1. Đóng Prefab Mode nếu đang mở `GameplayRoot.prefab` (Prefab Mode tự lưu đè file).
2. Đóng Unity. Xoá `HorizontalGrid.cs` + `.meta` của dự án. Mở `Assets/Horcrux/Runtime/Utilities/Layout/HorizontalRow.cs.meta`, thay dòng
   `guid:` bằng `3b10628ba4222cf4092c8a20be197ec7` (GUID Unity sinh cho bản Horcrux chưa được asset nào trỏ, bỏ được).
3. Mở Unity, chờ import. Mở `GameplayRoot.prefab`, chọn `cache_bar` rồi `box_row`: Inspector hiện `Horizontal Row` với `Space`,
   `Child Local Y` giữ giá trị cũ của prefab, năm núm còn lại ở mặc định; `Boxes Container › _grid` vẫn trỏ component của `box_row`.

**Kiểm:** `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj CategoryJam.Gameplay.csproj,CategoryJam.EditModeTests.csproj --tests CategoryJam.EditModeTests`
xanh · `grep -rn "HorizontalGrid\|SetSpaceAndPlayStartLevelAnimation" Assets/_CategoryJam` rỗng · grep GUID trên ra đúng hai chỗ trong
prefab và một `.meta` của Horcrux · `HorizontalRowTests` PlayMode xanh trong Test Runner.

**Chơi thử (developer):** vào màn 1 — thanh khay hiện đủ số ô mở cách đều, nền thanh vừa khít; hai hộp bay vào từ phải so le sau nửa
giây rồi đứng cách đều quanh tâm hàng. Dùng booster thêm ô khay — ô mới trượt vào, nền thanh nới ra, khay Extra Slot bay đúng vào ô vừa
mở. Bấm slot khoá để mở hộp thứ ba — cả hàng trượt với khoảng cách hẹp hơn. Chơi tới hết tile — hộp trống ẩn, hộp còn lại dồn về giữa.
Mọi thứ như trước khi chuyển. Dấu hiệu hỏng: hộp đứng ngoài mép phải màn (không bay về); hộp mất sau khi vào màn (bị tắt không bật
lại); ô khay chồng nhau hay lệch tâm; Inspector báo "Missing (Mono Script)".

**Mốc commit:** `refactor(layout): dự án dùng HorizontalRow của Horcrux` (dự án, kèm con trỏ submodule).

---

## File → GĐ

| File | GĐ1 | GĐ2 | GĐ3 | GĐ4 | GĐ5 |
|---|---|---|---|---|---|
| `Runtime/Utilities/Layout/HorizontalRow.cs` | tạo: `space`, `childLocalY`, `Space`, `SlotLocalX`, `LocalPositionOf`, `RepositionNow`, `Start` | `repositionDuration`, `_moveSequence`, `RepositionSmooth`, `SetSpaceAndRepositionSmooth`, `StopMoves` | 4 núm `appear*`, `_appearSequences`, `SetSpaceAndPlayAppearAsync`, `StopMoves` dừng cả bay | | `.meta` nhận GUID bản tạm |
| `Tests/PlayMode/HorizontalRowTests.cs` (agent) | tạo | thêm case | thêm case | | |
| `docs/Runtime/HorizontalRow/HorizontalRow.md`, `.html` (agent) | | | | tạo | |
| category-jam `BoxesContainer.cs`, `CacheTileSlots.cs`, `Feel.md`, `Layout.md`, `Rendering.md`; xoá `HorizontalGrid.cs`, `HorizontalGridLayoutTests.cs`; Plan này (xoá) | | | | | đổi |
