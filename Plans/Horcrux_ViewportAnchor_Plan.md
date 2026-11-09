# Plan — `ViewportCamera`, `ViewportAnchor` và `ViewportCover` lên Horcrux

Ngày 2026-11-06. Nguồn: category-jam, bản tạm ở `Assets/_CategoryJam/Scripts/Utilities/` (`GameplayViewport.cs`,
`GameplayViewport.Editor.cs`, `ViewportAnchor.cs`, `ViewportAnchor.Editor.cs`, `ViewportCover.cs`, `SafeAreaHeadroom.cs`), dựng theo
`Assets/_CategoryJam/Scripts/Layout.md` §1–§2, gắn trong `GameplayRoot.prefab` (1 camera, 9 anchor, 1 cover), đã chơi thử ở 1080×2340,
1080×1920, đo trên Device Simulator với máy tai thỏ. Camera ortho giữ cố định bề rộng world, vật thể world neo theo điểm trên màn (trong
safe area mà dải trống thiết kế đã nuốt phần inset nằm gọn trong nó), sprite nền phủ kín màn; tính **một lần** ở boot.

**Phân công:** GĐ1–3 developer gõ, agent viết test theo bảng case rồi compile · GĐ4 (hai partial `*.Editor.cs`), GĐ5 (tài liệu), GĐ6
(dự án chuyển sang bản Horcrux, giữ GUID) agent làm. **Chạy được, thấy được** ở GĐ1–3 = compile sạch + bảng case xanh trong
`Tests/EditMode/ViewportLayoutTests.cs`; thấy ở Scene view ở GĐ4, trong game ở GĐ6. GĐ1–5 commit trong `Assets/Horcrux`, GĐ6 commit ở
dự án kèm con trỏ submodule. Không phụ thuộc Plan nào; `Horcrux_BandFit_Plan.md`, `Horcrux_HorizontalRow_Plan.md` đặt file cùng
`Runtime/Utilities/Layout/`. Không có `§0`: ba công thức một dòng nằm ở giai đoạn sinh ra chúng.

## Ngữ cảnh đã chốt

- **Người dùng:** `GameplayRoot.prefab` › `camera_main` (camera + 9 anchor dưới `world` + 1 cover ở `world/background`) · dự án sau có
  camera ortho dọc, đặt vật thể world theo mép màn và một ảnh nền phủ kín.
- **Chỗ đặt, tên:** `Runtime/Utilities/Layout/`, namespace `Horcrux.Runtime.Utilities.Layout` (`Splitter` từ namespace cha, không cần
  `using`). `ViewportAnchor`, `ViewportCover` giữ; `GameplayViewport` → **`ViewportCamera`**, `gameplayCamera` → `targetCamera` (lý do ở
  GĐ1). Thêm asset **`SafeAreaHeadroom`** (GĐ2). Tên đổi tuỳ ý trước khi gõ.
- **Dải trống thiết kế là một asset, mọi consumer cùng mép trên của một màn kéo cùng asset đó:** panel safe area của HUD và
  `ViewportCamera` (cho anchor bật safe area). Hai bên phải dời cùng số pixel, nếu không khe HUD–world đổi theo máy. Phương án bỏ: field số
  trên mỗi consumer (hai bản buộc khớp mà không gì canh) · hằng static dự án đặt lúc boot (bước setup ẩn, quên thì im lặng về cách cũ).
  Lý do và công thức ở GĐ2.
- **Bất biến:** tính một lần ở boot, không `Update`, không constraint. Ba file `.meta` Horcrux nhận lại GUID bản tạm ở GĐ6; hai
  `*.Editor.cs` GUID tuỳ ý.
- **Không làm:** tính lại khi màn đổi cỡ, neo theo bề cao, guard null (lý do ở GĐ1) · camera perspective (GĐ2) · cover co giãn không đều,
  bù scale của cha (GĐ3) · cover cho `Image` UI (đã có `UICoverFitter`, `Horcrux_UIFitters_Plan.md`) · nút `Apply Layout` trên cover (GĐ4) ·
  panel safe area cho UI (thuộc `SystemPlan.md` §6; dự án dùng panel của nó, chỉ đọc asset) · dải trống ở cạnh dưới và hai bên (chưa có
  thiết kế nào chừa).
- **Đã khảo sát:** 6 file bản tạm (lấy cả cơ chế; bỏ `Screen.width / Screen.height`, lý do ở GĐ1; hằng dải trống thành asset, lý do ở
  GĐ2) · dự án `Scripts/Hud/HeadroomSafeArea.cs` (panel HUD đọc cùng quy tắc) · `Layout.md` §1–§2 (kịch bản neo cụm
  trên, cụm dưới; "đã sai một lần" về đổi đơn vị camera, đổi cỡ Game view lúc Play; số `10.5`, `worldOffset` là nháp, sống trong prefab) · `GameplayRoot.prefab` (camera `m_NormalizedViewPortRect` 0,0,1,1;
  `world` scale 1; key YAML `gameplayCamera`, `anchors` (9), `covers` (1), `designWidthUnits`) · Horcrux `Runtime/Utilities/` (chưa có;
  `Layout/` chưa tồn tại) · Horcrux `Editor/Probe` (`ScreenBox`, `playrun.sh` với `SIZES` và `DEVICES`: kiểm GĐ6) · Unity `PositionConstraint`,
  `AspectRatioFitter`, Cinemachine (không hợp: chạy mỗi frame · chỉ cho `RectTransform` · thừa cho camera đứng yên) · Canvas + `EnvelopeParent`
  cho ảnh nền (lý do bỏ ở GĐ3) · `UICoverFitter` (cùng cách nói `max(view / ảnh)`; khác: `RectTransform` theo khung đổi trong Editor).

## Luồng mô tả — một lần boot của màn gameplay

```
Unity Awake trên camera ──► ViewportCamera đọc targetCamera.aspect, đặt orthographicSize theo designWidthUnits   GĐ1
                                        ▼
                     safe area một lần: SafeAreaHeadroom nuốt phần inset trên nằm gọn trong dải trống thiết kế   GĐ2
                     từng ViewportAnchor theo thứ tự mảng: điểm màn (trong safe area đó nếu bật) → world, giữ z    GĐ2
                                        ▼
                     từng ViewportCover: scale đều để sprite phủ kín view, trục thừa tràn ra ngoài màn           GĐ3
                                        ▼
                     developer bấm Apply Layout trong Inspector: cả chuỗi chạy lại, thấy ở Scene view            GĐ4
                                        ▼
                     tài liệu (GĐ5) · dự án dùng bản Horcrux, giữ GUID (GĐ6)
```

## Bản đồ hệ cuối

```
  Unity Awake ─────────────► ViewportCamera.ApplyLayout
  nút Apply Layout (camera) ─┘        │
  nút Apply Layout (anchor) ──────────┤  tìm ViewportCamera cùng root
                                      ├─ targetCamera.orthographicSize = designWidthUnits / 2 / targetCamera.aspect
                                      ├─ safe = safeAreaHeadroom.Absorb(Screen.safeArea) / cỡ màn   SafeAreaHeadroom (asset)
                                      ├─ anchors[i].Apply(targetCamera, safe)  ViewportAnchor: viewportPoint (trong safe) → world + worldOffset, giữ z
                                      └─ covers[i].Apply(targetCamera)   ViewportCover: target.localScale = max(viewW / spriteW, viewH / spriteH)

  panel safe area của HUD (dự án) ──► cùng asset SafeAreaHeadroom.Absorb ──► khung HUD dời cùng số pixel với cụm world
```

`ViewportCamera` là cửa gọi duy nhất; không ai cache kết quả — gọi lại là tính lại từ cấu hình.

---

## Giai đoạn 1 — Camera định bề rộng

**Mục tiêu:** `ViewportCamera` trên camera ortho đặt `orthographicSize` để đúng `designWidthUnits` unit world trải hết bề rộng màn, ở
`Awake`. Xong giai đoạn này mọi sprite world giữ đúng cỡ art ở mọi tỉ lệ màn dọc; màn cao chỉ dư phần dọc.

**Vì sao bây giờ:** bề rộng world là đại lượng mọi thứ sau neo vào; anchor và cover đều đọc camera đã đặt size.

### `Runtime/Utilities/Layout/ViewportCamera.cs` — file mới

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Layout
{
    /// <summary>
    /// Sizes an orthographic camera so <see cref="designWidthUnits"/> world units always span the screen width
    /// (10.8 = 1080 px of art at 100 pixels per unit). Runs once at boot: the screen keeps one size for the whole session.
    /// </summary>
    public sealed partial class ViewportCamera : MonoBehaviour
    {
        #region Properties

        [Splitter("References")]
        [SerializeField] private Camera targetCamera;

        [Splitter("Configs")]
        [Tooltip("World units visible across the screen width.")]
        [SerializeField, Min(0.01f)] private float designWidthUnits = 10.8f;

        #endregion

        #region Unity Callbacks

        private void Awake() => ApplyLayout();

        #endregion

        #region API

        /// <summary>Half the design width divided by width/height: the camera's half height in world units.</summary>
        public void ApplyLayout()
        {
            targetCamera.orthographicSize = designWidthUnits * 0.5f / targetCamera.aspect;
        }

        #endregion
    }
}
```

`partial` từ giai đoạn này: giai đoạn 4 ghép nút Inspector vào file `.Editor.cs`, không chạm dòng khai báo class.

**Toán → code.** Ortho size là nửa chiều cao view (unit). Bề rộng view = `2 · size · aspect`; đặt bằng `designWidthUnits` thì
`size = designWidthUnits / (2 · aspect)`. Mốc: `10.8` ở 1080×2340 → `11.7`; ở 1080×1920 → `9.6`.

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| nguồn aspect | `Screen.width / (float)Screen.height` | `targetCamera.aspect` | camera phủ cả màn thì hai số bằng nhau; camera có `rect` nhỏ hơn màn thì chỉ `camera.aspect` đúng; test EditMode đặt được `camera.aspect`, không đặt được `Screen`. *Đã sai một lần:* vào Play qua công cụ khi Editor không focus, `Screen` ở `Awake` trả cỡ ngang (aspect 1.37) trong khi camera vẽ 1080×2340, ortho ra 3.94 thay vì 11.7. `ViewportCover` đọc `camera.aspect` từ đầu |
| tên | `GameplayViewport`, `gameplayCamera` | `ViewportCamera`, `targetCamera` | tên theo cơ chế, không theo người dùng đầu |
| XML doc | `(1 unit = 100 px of art at 1080 px wide)` | `(10.8 = 1080 px of art at 100 pixels per unit)` | nói về giá trị mặc định của field, không về một dự án |

**Quyết định** — tính ở `Awake`, không `Start`: mọi thứ khác trong prefab đọc vị trí anchor sớm nhất ở `Start` hay frame tạo level; camera
phải xong trước. Phương án bỏ: `[DefaultExecutionOrder]` — thứ tự ngầm, không thấy trong Inspector. — Tính một lần, không theo dõi
màn đổi cỡ (xoay, chia đôi màn): game khoá dọc, màn không đổi cỡ trong phiên. Mở rộng sau: host gọi lại `ApplyLayout()` khi nhận sự
kiện đổi cỡ, không đổi chữ ký. — Chỉ giữ cố định bề rộng: chưa có người dùng neo theo bề cao; cần thì một `enum` chọn trục trên
`ViewportCamera`, công thức ortho đổi theo trục, `designWidthUnits` giữ nghĩa. — Không guard `targetCamera` (và `target`, `sprite` ở
GĐ2–3) null: thiếu là lỗi setup, lộ ở frame đầu bằng `NullReferenceException` trỏ đúng component; Editor setup ghi "thiếu thì hỏng ở đâu".

| Consumes | Produces |
|---|---|
| `UnityEngine.Camera` (ortho, `aspect`) · `Splitter` (`Runtime/Utilities/Attribute/SplitterAttribute.cs`) | `ViewportCamera.ApplyLayout()` · field `targetCamera`, `designWidthUnits` |

**Sơ đồ tương tác sau GĐ1** (`+` là phần mới)

```
  Unity Awake ──► ViewportCamera+ ──orthographicSize+──► targetCamera
```

**Editor setup** (dự án nào dùng; dự án này đã có trong `GameplayRoot.prefab` › `camera_main`)

| # | Thao tác | Thiếu thì |
|---|---|---|
| 1 | Camera › **Projection** = `Orthographic` | công thức ghi `orthographicSize` mà camera không đọc: bề rộng không cố định |
| 2 | Gắn `ViewportCamera` lên object của camera; ô **Target Camera** kéo chính camera đó | `NullReferenceException` ở `Awake`, trỏ `ViewportCamera` |
| 3 | **Design Width Units**: số unit world trải hết bề rộng (mặc định `10.8` cho art 1080 px ở 100 px/unit) | dùng mặc định; art PPU khác 100 thì sprite sai cỡ, không lỗi |
| 4 | Game view chọn cỡ **dọc** (vd 1080×2340) trước khi Play | aspect đọc một lần ở `Awake`: Game view ngang cho ortho sai, mọi thứ sai cỡ; đổi cỡ khi đang Play thì Play lại |

**Kiểm:** compile sạch. Camera test dựng bằng `new GameObject().AddComponent<Camera>()`, `orthographic = true`, đặt `aspect` tường minh;
field private đặt qua `SerializedObject` (`FindProperty`, `ApplyModifiedPropertiesWithoutUndo`), không mở API cho test; `Awake` không
chạy ở EditMode nên test gọi `ApplyLayout()`.

| Case | Input | Kỳ vọng |
|---|---|---|
| 9:19.5 | `designWidthUnits 10.8`, `aspect 1080/2340` | `orthographicSize` 11.7 (±1e-4) |
| 9:16 | `aspect 1080/1920` | 9.6 |
| vuông | `aspect 1` | 5.4 |
| đổi bề rộng | `designWidthUnits 10.5`, `aspect 1080/2340` | 11.375 |
| gọi lại | `ApplyLayout` hai lần cùng aspect | cùng số; không cộng dồn |
| camera đổi aspect giữa hai lần gọi | 9:16 rồi 9:19.5 | 9.6 rồi 11.7: không cache |

**Mốc commit:** `feat(layout): ViewportCamera giữ bề rộng thiết kế`.

---

## Giai đoạn 2 — Anchor bám mép màn

**Mục tiêu:** `ViewportAnchor` đặt transform của nó tại một điểm màn (0–1, tuỳ chọn trong safe area) cộng `worldOffset`, giữ z;
`ViewportCamera` tính safe area **một lần** qua asset `SafeAreaHeadroom` rồi gọi từng anchor **theo thứ tự mảng** ngay sau khi đặt size.
Xong giai đoạn này hàng box, thanh khay, mask nằm đúng mép ở mọi tỉ lệ màn, và trên máy tai thỏ cụm trên chỉ dời phần inset vượt dải
trống thiết kế, cùng số pixel với HUD.

**Vì sao bây giờ:** đặt vật thể theo mép là lý do tồn tại của bề rộng cố định; anchor cần camera đã có size nên mới đến lượt. Dải trống
đến cùng lúc vì `respectSafeArea` sinh ra ở đây: anchor đọc safe area thô thì máy tai thỏ đếm inset hai lần ngay từ bản đầu.

### `Runtime/Utilities/Layout/SafeAreaHeadroom.cs` — file mới

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Layout
{
    /// <summary>
    /// The top band a screen design leaves empty for the status bar and camera cutout. A device top inset that fits inside the
    /// band moves nothing; a larger one moves content by the excess only. Every consumer on the top edge of one screen (the HUD
    /// safe-area panel, <see cref="ViewportCamera"/> for its anchors) references the same asset, so they move by the same pixels
    /// and the gap between them holds on every device.
    /// </summary>
    [CreateAssetMenu(menuName = "Horcrux/Layout/Safe Area Headroom", fileName = "SafeAreaHeadroom")]
    public sealed class SafeAreaHeadroom : ScriptableObject
    {
        #region Properties

        [Splitter("Configs")]
        [Tooltip("Empty band above the top HUD in the design, as a fraction of the design height. 0 = the full top inset moves content.")]
        [SerializeField, Range(0f, 0.25f)] private float top;

        #endregion

        #region API

        /// <summary><paramref name="safeArea"/> in screen pixels with its top edge raised by up to the headroom; other edges kept.</summary>
        public Rect Absorb(Rect safeArea, float screenHeight)
        {
            var inset = screenHeight - safeArea.yMax;
            var shift = Mathf.Max(0f, inset - top * screenHeight);
            return Rect.MinMaxRect(safeArea.xMin, safeArea.yMin, safeArea.xMax, screenHeight - shift);
        }

        #endregion
    }
}
```

### `Runtime/Utilities/Layout/ViewportAnchor.cs` — file mới

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Layout
{
    /// <summary>
    /// Places this transform at a point of the screen, in world space, once: <see cref="ViewportCamera"/> calls
    /// <see cref="Apply"/> at boot. Screen size never changes during a session, so nothing runs per frame.
    /// </summary>
    public sealed partial class ViewportAnchor : MonoBehaviour
    {
        #region Properties

        [Splitter("Configs")]
        [Tooltip("Screen point, (0,0) bottom-left to (1,1) top-right.")]
        [SerializeField] private Vector2 viewportPoint = new(0.5f, 0.5f);
        [Tooltip("Added to the world position after the viewport point is resolved.")]
        [SerializeField] private Vector2 worldOffset;
        [Tooltip("Resolve the viewport point inside the safe area ViewportCamera passes in instead of the whole screen.")]
        [SerializeField] private bool respectSafeArea;

        #endregion

        #region API

        /// <summary>
        /// Moves this transform to the configured screen point as seen by <paramref name="camera"/>; keeps z.
        /// <paramref name="safeViewport"/> is the safe area in viewport units (0–1), used only when the anchor respects it.
        /// </summary>
        public void Apply(Camera camera, Rect safeViewport)
        {
            var point = respectSafeArea ? ToSafeAreaViewport(viewportPoint, safeViewport) : viewportPoint;
            var world = camera.ViewportToWorldPoint(new Vector3(point.x, point.y, 0f));
            transform.position = new Vector3(world.x + worldOffset.x, world.y + worldOffset.y, transform.position.z);
        }

        #endregion

        #region Class Methods

        private static Vector2 ToSafeAreaViewport(Vector2 point, Rect safe) =>
            new(safe.x + point.x * safe.width, safe.y + point.y * safe.height);

        #endregion
    }
}
```

### `ViewportCamera.cs` › `Properties` › thêm sau `targetCamera`, trước `[Splitter("Configs")]`

```csharp
        [SerializeField] private Camera targetCamera;
        [Tooltip("Applied in this order, after the camera size is set.")]
        [SerializeField] private ViewportAnchor[] anchors;
        [Tooltip("Design top band for anchors that respect the safe area. Same asset as the HUD safe-area panel of this screen.")]
        [SerializeField] private SafeAreaHeadroom safeAreaHeadroom;
```

### `ViewportCamera.cs` › `API` › `ApplyLayout` — thêm sau dòng đặt `orthographicSize`

```csharp
            targetCamera.orthographicSize = designWidthUnits * 0.5f / targetCamera.aspect;

            var safe = SafeViewport();
            foreach (var anchor in anchors)
                anchor.Apply(targetCamera, safe);
```

### `ViewportCamera.cs` › region mới `Class Methods` sau `API`

```csharp
        #region Class Methods

        // safe area in viewport units (0–1), top inset absorbed by the design headroom
        private Rect SafeViewport()
        {
            var safe = safeAreaHeadroom.Absorb(Screen.safeArea, Screen.height);
            return new Rect(safe.x / Screen.width, safe.y / Screen.height, safe.width / Screen.width, safe.height / Screen.height);
        }

        #endregion
```

Sửa XML doc của class: `... span the screen width (10.8 = 1080 px of art at 100 pixels per unit), then places every
<see cref="ViewportAnchor"/> in order.`

**Toán → code.** `ViewportToWorldPoint(x, y, 0)` với camera ortho trả điểm world trên mặt phẳng z của camera; chỉ lấy x, y, giữ z của
transform. Mốc 9:16 với `10.8`: `(0, 0)` → `(−5.4, −9.6)`, `(1, 1)` → `(5.4, 9.6)`. Safe area ở đơn vị viewport `s`: điểm `(px, py)` trong
nó là `(s.x + px · s.width, s.y + py · s.height)` của cả màn.

Dải trống: màn cao `H` pixel, inset trên `i = H − safeArea.yMax`, dải trống thiết kế `h = top · H`. Mép trên safe area chỉ hạ

$$\text{shift} = \max(0,\; i - h)$$

Inset nằm gọn trong dải thì `shift = 0`: nội dung đứng như ở Game view. Vượt thì chỉ dời phần vượt. `top` là phần của **chiều cao màn**
vì canvas HUD khớp theo chiều cao: dải trống trên HUD co giãn đúng theo `H`. Mốc với `top = 94.3 / 2340` (`h` ≈ 96.7 px ở `H = 2400`):
máy 1080×2400 inset 110 → hạ 13.3 px; máy 1170×2532 inset 141 (`h` ≈ 102.0) → hạ 39.0 px.

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| nguồn dải trống | hằng `SafeAreaHeadroom.TopHeadroom` trong class static | field `top` của asset, kéo vào camera và panel HUD | mỗi thiết kế màn có dải riêng (Menu có thể khác Game); Horcrux không giữ số của dự án |
| ai đọc `Screen` | mỗi anchor bật safe area tự đọc `Screen.safeArea` | `ViewportCamera` đọc một lần, truyền `Rect` viewport | một lần mỗi layout thay vì mỗi anchor; anchor thành hàm thuần, test EditMode dựng được safe area lệch |
| `Apply` | `Apply(Camera)` | `Apply(Camera, Rect)` | theo dòng trên |

Còn lại chỉ namespace, `<see cref="ViewportCamera"/>` và bỏ dòng "Bản tạm của dự án" trong XML doc.

**Quyết định** — `ViewportAnchor` không có `Awake`: thứ tự `Awake` giữa các object không định trước, anchor phải chạy **sau** khi camera
đổi size và anchor con phải chạy sau anchor cha (`transform.position` của con tính trên cha đã dời); một chủ gọi theo mảng là thứ tự
tường minh trong Inspector. Hệ quả: anchor trên object đang tắt vẫn được đặt. Phương án bỏ: `[DefaultExecutionOrder]` + `Awake` — không
xếp được thứ tự giữa hai anchor. — `SafeViewport` đọc `Screen`: safe area là khái niệm của màn, không của camera; đúng khi camera phủ
cả màn (điều kiện của GĐ1). — Giữ `z`: thứ tự vẽ 2.5D của dự án sống ở z; anchor chỉ lo x, y. — Chỉ camera ortho: `ViewportToWorldPoint`
với z = 0 chỉ đúng cho ortho; perspective chưa có người dùng.

**Quyết định** — dải trống: demo thường đã chừa dải trên cùng cho thanh trạng thái và lỗ camera; safe area thô cộng inset của máy lên trên
dải đó, đếm hai lần. *Đã sai một lần:* HUD và cụm world cùng neo trong safe area thô, khớp demo ở Game view (safe area bằng cả màn), trên
máy punch-hole cả cụm trên tụt hơn trăm pixel. Asset, không field số trên từng consumer: HUD và world là **hai bản buộc khớp lúc chạy**,
lệch nhau thì khe giữa chúng đổi theo máy; một asset là một nguồn. Thiếu asset trên camera là lỗi setup, `NullReferenceException` ở
`Awake` như `targetCamera`; dự án không có dải trống thì asset `top = 0`, hành vi bằng safe area thô. Phương án bỏ: hằng static dự án
đặt lúc boot — bước setup ẩn, quên thì im lặng về cách đếm hai lần. Chỉ cạnh trên: chưa thiết kế nào chừa dải ở cạnh dưới, hai bên.

| Consumes | Produces |
|---|---|
| GĐ1 · `Camera.ViewportToWorldPoint` · `Screen.safeArea` | `SafeAreaHeadroom.Absorb(Rect, float)` · `ViewportAnchor.Apply(Camera, Rect)` · `ViewportCamera.anchors`, `safeAreaHeadroom` |

**Sơ đồ tương tác sau GĐ2**

```
  Unity Awake ──► ViewportCamera ──orthographicSize──► targetCamera
                        │
                        ├──SafeViewport()+ ──Absorb(Screen.safeArea)+──► SafeAreaHeadroom+ (asset)
                        └──anchors[i].Apply(targetCamera, safe)+──► ViewportAnchor+ ──transform.position──► vật thể world
```

**Editor setup**

| # | Thao tác | Thiếu thì |
|---|---|---|
| 1 | Gắn `ViewportAnchor` lên object cần neo; **Viewport Point** `(0,0)` dưới trái → `(1,1)` trên phải; **World Offset** theo unit; **Respect Safe Area** bật cho cụm kề HUD trên | object đứng ở vị trí prefab, không theo màn |
| 2 | `ViewportCamera` › ô **Anchors**: kéo từng anchor vào, **theo thứ tự muốn đặt** — anchor con sau anchor cha | anchor không trong mảng: không ai gọi, đứng yên; con trước cha: con đặt xong bị cha kéo đi |
| 3 | Hai vật kề nhau ở hai hệ toạ độ (world và canvas HUD) neo **cùng một mép, cùng luật co giãn** (dự án: cụm trên `y = 1` trong safe area với `worldOffset.y` âm) | ở màn thấp hai vật đè nhau |
| 4 | Project › **Create › Horcrux › Layout › Safe Area Headroom**, một asset cho mỗi thiết kế màn; **Top** = khoảng từ mép trên canvas tới mép trên phần tử HUD cao nhất, chia chiều cao canvas, đo ở cỡ demo | `top = 0`: máy tai thỏ dời cả cụm trên đúng bằng inset, tụt dưới dải trống demo đã chừa |
| 5 | `ViewportCamera` › ô **Safe Area Headroom** kéo asset đó; panel safe area của HUD cùng màn đọc **cùng asset** | ô trống: `NullReferenceException` ở `Awake`, trỏ `ViewportCamera` · HUD đọc asset khác hay safe area thô: khe HUD–world đổi theo máy |
| 6 | Đổi vị trí phần tử HUD cao nhất thì đo lại **Top** | dải trống lệch thiết kế: máy tai thỏ dời thiếu hay thừa |

**Kiểm:** compile sạch; camera test đặt ở `(0, 0, −10)`, aspect 9:16, `designWidthUnits 10.8`, asset `SafeAreaHeadroom` từ
`ScriptableObject.CreateInstance` gán vào `safeAreaHeadroom`, rồi `ApplyLayout`. Ca safe area lệch gọi thẳng `Apply(camera, rect)`.

| Case | Input | Kỳ vọng |
|---|---|---|
| tâm | anchor `(0.5, 0.5)` | position `(0, 0)` |
| góc dưới trái | `(0, 0)` | `(−5.4, −9.6)` |
| góc trên phải | `(1, 1)` | `(5.4, 9.6)` |
| ngoài dải | `(1.5, −0.5)` | `(10.8, −19.2)`: không kẹp, điểm ngoài màn hợp lệ |
| offset | `(0.5, 0.5)`, `worldOffset (1, −2)` | `(1, −2)` |
| giữ z | transform z = 7 trước `Apply` | z vẫn 7 |
| camera không ở gốc | camera `(3, 1, −10)`, anchor `(0.5, 0.5)` | `(3, 1)` |
| thứ tự mảng | anchor B là con của anchor A, mảng `[A, B]`, A `(0, 1)`, B `(1, 0)` | B ở `(5.4, −9.6)` thế giới sau khi A đã dời |
| thứ tự đảo | mảng `[B, A]` | B lệch đúng bằng độ dời của A: bằng chứng thứ tự mảng là hợp đồng |
| object tắt | anchor trên object `SetActive(false)` | vẫn được đặt |
| mảng rỗng | `anchors` rỗng | `ApplyLayout` không ném |
| safe area cả màn | `respectSafeArea true`, Editor có safe area = cả màn | bằng ca `false` |
| safe area hạ mép trên | `Apply(camera, (0, 0, 1, 0.95))`, `respectSafeArea true`, anchor `(0.5, 1)` | `(0, 8.64)` |
| safe area lệch trái | `Apply(camera, (0.1, 0, 0.8, 1))`, `respectSafeArea true`, anchor `(0, 0.5)` | `(−4.32, 0)` |
| không bật safe area | `Apply(camera, (0, 0.05, 1, 0.9))`, `respectSafeArea false`, anchor `(0.5, 1)` | `(0, 9.6)`: rect bị bỏ qua |
| không inset | `Absorb((0, 0, 1080, 2400), 2400)`, `top 94.3/2340` | `yMax` 2400 |
| inset trong dải | inset 86.7 (dải ≈ 96.7) | `yMax` 2400: không dời |
| inset vượt dải | inset 110 | `2400 − yMax` = 110 − 96.7 ≈ 13.3 (±1e-3) |
| dải 0 | `top 0`, inset 110 | `2400 − yMax` = 110: bằng safe area thô |
| cạnh khác giữ | `(12, 48, 1050, 2152)`, inset 200 | `xMin` 12, `yMin` 48, `xMax` 1062 |

`Screen.safeArea` lệch không dựng được ở EditMode (chỉ đọc): `SafeViewport` kiểm ở GĐ6 trên Device Simulator với máy tai thỏ.

**Mốc commit:** `feat(layout): ViewportAnchor neo world theo điểm màn`.

---

## Giai đoạn 3 — Cover phủ nền

**Mục tiêu:** `ViewportCover` phóng đều một `SpriteRenderer` để phủ kín view của camera, trục thừa tràn ra ngoài màn; `ViewportCamera`
gọi từng cover sau mảng anchor. Xong giai đoạn này ảnh nền không hở mép ở mọi tỉ lệ.

**Vì sao bây giờ:** cover đọc `orthographicSize` và `aspect` đã đặt ở GĐ1; ảnh nền nằm giữa màn nhờ một anchor `(0.5, 0.5)` của GĐ2 — cover
chỉ còn lo cỡ.

### `Runtime/Utilities/Layout/ViewportCover.cs` — file mới

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Layout
{
    /// <summary>
    /// Scales a sprite uniformly so it covers the whole screen, cropping the overflowing axis, once:
    /// <see cref="ViewportCamera"/> calls <see cref="Apply"/> at boot. Pair with a centered <see cref="ViewportAnchor"/>.
    /// </summary>
    public sealed class ViewportCover : MonoBehaviour
    {
        #region Properties

        [Splitter("References")]
        [Tooltip("Sprite that must fill the screen. Its transform's parent must have scale 1.")]
        [SerializeField] private SpriteRenderer target;

        #endregion

        #region API

        /// <summary>Sets the uniform scale at which <see cref="target"/> covers the view of <paramref name="camera"/>.</summary>
        public void Apply(Camera camera)
        {
            var viewHeight = camera.orthographicSize * 2f;
            var viewWidth = viewHeight * camera.aspect;
            var size = target.sprite.bounds.size;
            var scale = Mathf.Max(viewWidth / size.x, viewHeight / size.y);
            target.transform.localScale = new Vector3(scale, scale, 1f);
        }

        #endregion
    }
}
```

### `ViewportCamera.cs` › `Properties` › thêm sau `anchors`

```csharp
        [SerializeField] private ViewportAnchor[] anchors;
        [Tooltip("Sprites scaled to cover the screen, after the camera size is set.")]
        [SerializeField] private ViewportCover[] covers;
```

### `ViewportCamera.cs` › `API` › `ApplyLayout` — thêm sau vòng `anchors`

```csharp
            foreach (var anchor in anchors)
                anchor.Apply(targetCamera, safe);

            foreach (var cover in covers)
                cover.Apply(targetCamera);
```

Sửa XML doc của class: `... then places every <see cref="ViewportAnchor"/> in order and scales every <see cref="ViewportCover"/> to
fill the screen.`

**Toán → code.** View cao `2 · orthographicSize`, rộng `cao · aspect`. `sprite.bounds.size` là cỡ sprite ở scale 1 (unit, theo PPU).
Scale phủ kín = `max(viewW / spriteW, viewH / spriteH)`: trục thiếu hơn vừa khít, trục kia tràn. Ảnh 1080×2340 ở 100 PPU là 10.8×23.4:
máy 9:19.5 khớp (scale 1), máy thấp hơn cắt trên dưới, máy cao hơn cắt hai bên.

**Khác với bản dự án:** chỉ namespace, `<see cref="ViewportCamera"/>` và bỏ dòng "Bản tạm của dự án"; thân hàm giữ nguyên.

**Quyết định** — component riêng, không phải cờ trên `ViewportAnchor`: anchor chỉ đặt vị trí, cover chỉ đặt scale, ảnh nền cần cả hai thì
gắn cả hai; `ViewportCamera` gọi hai mảng riêng, cover sau anchor (cover không đọc vị trí nên thứ tự giữa hai mảng không đổi kết quả).
Phương án bỏ: `bool coverScreen` trên anchor — anchor mang thêm lý do thay đổi và một ô `SpriteRenderer` vô nghĩa cho 9 anchor khác. —
Phóng **đều**: không méo art; ảnh nền vẽ dư hai đầu thì cắt trục thừa là đủ, kéo méo cho vừa khít là phương án bỏ. — Đọc `sprite.bounds`,
không đọc `localScale` hiện tại: gọi lại không cộng dồn, nút Inspector của GĐ4 bấm nhiều lần vẫn đúng. — Sprite world, không ảnh nền trên
Canvas: Screen Space–Camera + `AspectRatioFitter` `EnvelopeParent` chạy được nhưng thêm một canvas cho một ảnh, còn sprite world đi chung
đơn vị và sorting layer với gameplay; Canvas Overlay luôn đè gameplay. — Không bù scale của cha: mọi cha hiện tại scale 1 (`world` trong
prefab), Tooltip ô `Target` ghi điều kiện này. Mở rộng sau: cha scale khác 1 thì `Apply` chia cho `target.transform.parent.lossyScale`,
không đổi chữ ký.

| Consumes | Produces |
|---|---|
| GĐ1 · `SpriteRenderer.sprite.bounds` | `ViewportCover.Apply(Camera)` · `ViewportCamera.covers` |

**Sơ đồ tương tác sau GĐ3**

```
  Unity Awake ──► ViewportCamera ──orthographicSize──► targetCamera
                        ├──anchors[i].Apply──► ViewportAnchor ──position──► vật thể world (kể cả object nền)
                        └──covers[i].Apply+──► ViewportCover+ ──localScale──► target (SpriteRenderer nền)
```

**Editor setup**

| # | Thao tác | Thiếu thì |
|---|---|---|
| 1 | Object nền: `SpriteRenderer` + `ViewportAnchor` **Viewport Point** `(0.5, 0.5)` + `ViewportCover`; ô **Target** kéo `SpriteRenderer` của chính nó; cha scale 1 | thiếu anchor: nền phủ kín nhưng lệch tâm, hở một mép · thiếu `Target`: `NullReferenceException` ở `Awake` · cha scale ≠ 1: scale nhân đôi, nền quá to hoặc hở |
| 2 | `ViewportCamera` › ô **Covers**: kéo `ViewportCover` của nền vào; object nền cũng nằm trong **Anchors** | nền giữ scale prefab, hở mép ở tỉ lệ khác ảnh |
| 3 | Ảnh nền import **Sprite (2D and UI)**, Sprite Mode `Single`, Mesh Type `Full Rect`, PPU bằng art; đặt ngoài thư mục atlas world | vào atlas: một ảnh cỡ cả màn làm phình atlas · Mesh Type `Tight`: `bounds` vẫn là rect, không ảnh hưởng, nhưng tốn vertex vô ích |

**Kiểm:** compile sạch; sprite dựng bằng `Sprite.Create` từ `Texture2D` (108×234 px, 10 PPU → 10.8×23.4 unit); camera 9:21, 9:16 với
`designWidthUnits 10.8`.

| Case | Input | Kỳ vọng |
|---|---|---|
| máy cao hơn ảnh | sprite 10.8×23.4, aspect 9:21 (view 10.8×25.2) | scale 1.0769 (±1e-3): kín chiều cao, tràn hai bên |
| máy thấp hơn ảnh | aspect 9:16 (view 10.8×19.2) | scale 1: kín bề rộng, tràn trên dưới |
| khớp | aspect 9:19.5 | scale 1 |
| ảnh ngang | sprite 20×10, aspect 9:16 | 1.92: trục cao quyết định |
| giữ z | `localScale.z` sau `Apply` | 1 |
| gọi lại | `Apply` hai lần | cùng scale, không cộng dồn |
| mảng rỗng | `covers` rỗng | `ApplyLayout` không ném |

**Mốc commit:** `feat(layout): ViewportCover phủ kín màn`.

---

## Giai đoạn 4 — Nút Apply Layout trong Inspector (agent)

**Mục tiêu:** bấm **Apply Layout** trên `ViewportCamera` hoặc trên bất kỳ `ViewportAnchor` nào là cả chuỗi GĐ1–3 chạy lại ở Edit mode;
designer chỉnh `Viewport Point`, `World Offset` và thấy ngay ở Scene view, không vào Play.

**Vì sao bây giờ:** ba thành phần đã đủ để "thấy"; nút chỉ gọi API có sẵn. Code Editor: agent viết thẳng theo `reuse-gate`, developer
review và veto.

### `Runtime/Utilities/Layout/ViewportCamera.Editor.cs` — file mới

```csharp
#if UNITY_EDITOR
using Sirenix.OdinInspector;

namespace Horcrux.Runtime.Utilities.Layout
{
    public sealed partial class ViewportCamera
    {
        [Button("Apply Layout")]
        private void ApplyLayoutInEditor() => ApplyLayout();
    }
}
#endif
```

### `Runtime/Utilities/Layout/ViewportAnchor.Editor.cs` — file mới

```csharp
#if UNITY_EDITOR
using Sirenix.OdinInspector;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Layout
{
    public sealed partial class ViewportAnchor
    {
        [Button("Apply Layout")]
        private void ApplyLayoutInEditor()
        {
            var viewport = transform.root.GetComponentInChildren<ViewportCamera>(true);
            if (viewport == null)
            {
                Debug.LogError("[ViewportAnchor]: No ViewportCamera under the same root.", this);
                return;
            }

            viewport.ApplyLayout();
        }
    }
}
#endif
```

**Quyết định** — nút trên anchor gọi `ViewportCamera.ApplyLayout`, không gọi `Apply` của chính nó: `Apply` lẻ cho kết quả đúng chỉ khi
camera đã đúng size và anchor cha đã dời; chạy lại cả chuỗi rẻ (mười phép tính) và luôn đúng. Phương án bỏ: `Apply(Camera.main)` —
`Camera.main` trong Prefab Mode là camera của scene đang mở, không phải camera trong prefab. — Tìm camera bằng `transform.root`: trong
Prefab Mode root là root prefab; trong scene là root của object. Phương án bỏ: ô kéo `ViewportCamera` trên mỗi anchor — 9 ô kéo cho
một nút Editor. — Không có nút trên `ViewportCover`: cover chỉ có khi có camera trong cùng root, nút trên camera và anchor đã chạy lại cả
chuỗi. — Odin `[Button]` trong partial `*.Editor.cs` bọc `#if UNITY_EDITOR`: Odin đã là phụ thuộc của `com.horcrux.runtime`
(`PersistenceDataEntry.cs`, `PoolService.Editor.cs`).

| Consumes | Produces |
|---|---|
| GĐ1–3 · Odin `[Button]` | hai nút **Apply Layout** |

**Sơ đồ tương tác sau GĐ4**

```
  nút Apply Layout (ViewportCamera)+ ──────────────────► ViewportCamera.ApplyLayout ──► GĐ1 → GĐ2 → GĐ3
  nút Apply Layout (ViewportAnchor)+ ──root.GetComponentInChildren<ViewportCamera>──┘
```

**Editor setup**

| # | Thao tác | Thiếu thì |
|---|---|---|
| 1 | Game view chọn cỡ dọc **trước** khi bấm nút | `targetCamera.aspect` ở Edit mode theo Game view: Game view ngang cho Scene view sai y hệt Play |
| 2 | Anchor và camera cùng một root (prefab hay scene) | nút trên anchor báo `[ViewportAnchor]: No ViewportCamera under the same root.` trong Console, không đổi gì |

**Kiểm:** compile sạch (`com.horcrux.runtime` ở cả Editor lẫn build: file bọc `#if UNITY_EDITOR`). Developer bấm nút trên `camera_main`
của `GameplayRoot.prefab` trong Prefab Mode với Game view 1080×2340: hàng box, thanh khay nằm như lúc Play; đổi Game view 1080×1920 bấm
lại: cụm trên hạ xuống theo mép. Dấu hiệu hỏng: bấm nhiều lần vị trí hay scale nền thay đổi dần (phải đứng yên).

**Mốc commit:** `feat(layout): nút Apply Layout trong Inspector`.

---

## Giai đoạn 5 — Tài liệu `docs/Runtime/ViewportAnchor/ViewportAnchor.md` và `ViewportAnchor.html` (agent)

Một tài liệu cho ba component và asset, mở đầu `Code: Runtime/Utilities/Layout/`, viết sau khi đọc code developer đã gõ. Mục:

- **Trước khi chạy:** bốn bảng Editor setup của GĐ1–4 gộp một bảng, nhãn thật trên Inspector, mỗi dòng "thiếu thì hỏng ở đâu".
- **Luồng:** luồng mô tả ở trên; bảng API (`ApplyLayout()`, `ViewportAnchor.Apply(Camera, Rect)`, `ViewportCover.Apply(Camera)`,
  `SafeAreaHeadroom.Absorb(Rect, float)`, field serialize).
- **Core algorithm:** công thức `shift = max(0, i − h)` của GĐ2 kèm hai mốc máy.
- **Design decisions:** các **Quyết định** của GĐ1–4 kèm ba bẫy *đã sai một lần*: `Screen` ở `Awake` khi Editor không focus; đổi
  đơn vị camera thì grep mọi hằng khoảng cách trong code (kể cả hằng scale) trước lần Play đầu; safe area thô đếm inset hai lần trên dải
  trống demo đã chừa, Game view không bao giờ lộ.
- **Kịch bản dùng** (tên trung tính): cụm kề HUD trên neo `y = 1` trong safe area với offset âm, panel HUD và camera kéo cùng một asset
  `SafeAreaHeadroom`; đo **Top** ở cỡ demo; cụm kề thanh dưới co theo chiều cao neo theo tỉ lệ `y`; ảnh nền = anchor tâm + cover, import
  Single, Full Rect, ngoài atlas.
- **Testing:** kiểm trên máy tai thỏ bằng Device Simulator (`docs/Editor/Probe.md`, `DEVICES`) trước khi build.

`.html` chọn từ `.md`: kịch bản dùng, "Trước khi chạy" nguyên vẹn, flow boot, lưu ý (ba bẫy đã sai một lần, Game view dọc trước
khi Play).

**Mốc commit:** `docs(layout): ViewportAnchor.md`.

---

## Giai đoạn 6 — Dự án dùng bản Horcrux, giữ GUID, xoá bản tạm (agent)

**Mục tiêu:** `GameplayRoot.prefab` chạy trên ba component Horcrux với 11 component giữ nguyên tham chiếu; camera và panel HUD đọc cùng
một asset `SafeAreaHeadroom`; 6 file bản tạm, test bản tạm và `.meta` xoá; tài liệu dự án đổi tên theo.

**Thứ tự — trong một lần Unity refresh** (Editor đóng, hoặc làm hết rồi mới focus Editor; Prefab Mode của `GameplayRoot` đóng trước):

| # | Việc | Thiếu thì |
|---|---|---|
| 1 | Xoá 6 file ở `Assets/_CategoryJam/Scripts/Utilities/` (`GameplayViewport.cs`, `GameplayViewport.Editor.cs`, `ViewportAnchor.cs`, `ViewportAnchor.Editor.cs`, `ViewportCover.cs`, `SafeAreaHeadroom.cs`), `Tests/EditMode/SafeAreaHeadroomTests.cs` (case đã sang `ViewportLayoutTests` ở GĐ2) và các `.meta` | hai asset cùng GUID sau bước 2: Unity cấp GUID mới cho một bản, prefab báo 11 script missing |
| 2 | Ghi GUID bản tạm vào `.meta` Horcrux: `ViewportCamera.cs.meta` ← `3fc862fca3d20714aa53ea069560ef9f` · `ViewportAnchor.cs.meta` ← `c29c8c47f2b864541b173c28f727e131` · `ViewportCover.cs.meta` ← `233404ff652a67d4ca28f25e2e133308` | prefab trỏ GUID không còn: 11 script missing |
| 3 | `GameplayRoot.prefab`: key `gameplayCamera: {fileID: 5567243261129153408}` → `targetCamera: {fileID: 5567243261129153408}` | key cũ bị bỏ khi deserialize, `targetCamera` null: `NullReferenceException` ở `Awake` |
| 4 | `Scripts/Hud/HeadroomSafeArea.cs`: thêm `using Horcrux.Runtime.Utilities.Layout;`, field `[SerializeField] private SafeAreaHeadroom headroom;`, thân `GetSafeArea` thành `headroom.Absorb(base.GetSafeArea(), Screen.height)`; XML doc trỏ asset thay cho class static | class static đã xoá ở bước 1: lỗi compile |
| 5 | Không file `.cs` nào khác của dự án gọi tên bốn type (grep 2026-11-09: `SafeAreaHeadroom` chỉ ở `HeadroomSafeArea.cs`, `ViewportAnchor.cs` bản tạm và test bản tạm) | — |

**Sau refresh, Editor mở** (giá trị dải trống là của thiết kế, chuyển nguyên số, không đo lại):

| # | Việc | Thiếu thì |
|---|---|---|
| 6 | Tạo asset `SafeAreaHeadroom` cho màn Game, **Top** = `94.3 / 2340` (≈ 0.0403, hằng của bản tạm: mép trên `txtLevel` trong `CanvasUI`). Giả định (cần xác nhận): đặt ở `Assets/_CategoryJam/Content/Layout/SafeAreaHeadroom_Game.asset` | — |
| 7 | `GameplayRoot.prefab` › `camera_main` › `ViewportCamera` › ô **Safe Area Headroom** kéo asset bước 6 | `NullReferenceException` ở `Awake` của `ViewportCamera` |
| 8 | `Game.unity` › `CanvasUI/ViewInGame/*SafeArea*` › `HeadroomSafeArea` › ô **Headroom** kéo **cùng** asset | `NullReferenceException` ở `Start` của panel; asset khác: khe pill–hàng box đổi theo máy |

**Tài liệu dự án:** `Scripts/Layout.md` §1 tiêu đề và thân `GameplayViewport` → `ViewportCamera`, "aspect đọc từ `Screen`" → "từ
`camera.aspect`", nút trên anchor là **Apply Layout** (không có nút tên khác); §2, §3, "Đã sai một lần", "Trước khi chạy" hàng 2 đổi tên type
và key (`gameplayCamera` → `targetCamera`); đoạn "Inset tai thỏ chỉ đẩy phần vượt dải trống" và "Trước khi chạy" hàng 7: hằng
`SafeAreaHeadroom.TopHeadroom` → asset `SafeAreaHeadroom_Game`, hai ô kéo cùng asset, đổi `txtLevel` thì sửa **Top** của asset; hàng "Mở rộng
sau" bỏ mục "lên Horcrux". `Structure.md`: dòng `Utilities/` bỏ ba type và dòng "bản tạm chờ lên Horcrux"; dòng `Prefabs/` đổi
`GameplayViewport` → `ViewportCamera`; dòng `Content/` thêm `Layout/`; bảng nợ bỏ hàng ba type.

**Kiểm (agent):**

- `bash "Assets/Horcrux/Editor/CompileCheck/Tools~/verify.sh" --csproj CategoryJam.Gameplay.csproj,CategoryJam.Editor.csproj,CategoryJam.EditModeTests.csproj,com.horcrux.runtime.csproj,com.horcrux.editor.csproj,Horcrux.EditModeTests.csproj --tests CategoryJam.EditModeTests,Horcrux.EditModeTests`
  — lỗi build ngay sau khi xoá file là lỗi tạm tới khi Editor sinh lại csproj; chạy lại trước khi kết luận.
- grep `GameplayViewport`, `gameplayCamera`, `CategoryJam.Gameplay.Utilities.Viewport`, `TopHeadroom` trên `.cs`, `.prefab`, `.unity`,
  `.md` toàn repo: 0.
- Nhất quán chéo trước–sau: một probe (`Editor/Probe`, `playrun.sh` với `SIZES="1080x2340 1080x1920"`,
  `DEVICES="Poco M7 Pro 5G;iOS Notch Device"`, `PLAY_FROM_BUILD_SCENE=1`) vào level, in `ScreenBox.Of` của 9 object anchor, khung
  `*SafeArea*` và `localScale` của `background`; chạy **trước** bước 1 và **sau** bước 8, hai file ra trùng từng số ở cả hai cỡ và hai
  máy (mốc bản tạm: máy Poco dời 13 px, máy iOS Notch 39 px so với safe area cả màn).

**Cần chơi thử (developer):** *vào* level 1 ở Game view 1080×2340 rồi 1080×1920, và trên máy có tai thỏ · *nhìn* hàng box, thanh
khay, mask, bàn chơi nằm như lúc chưa chuyển; nền phủ kín, không hở mép; khe pill–hàng box trên máy bằng ở Game view · *dấu hiệu hỏng:*
11 script missing trên `GameplayRoot` · `NullReferenceException` ở `Awake` của `ViewportCamera` · cụm trên chui dưới tai thỏ hoặc tụt
sâu dưới dải trống · nền hở một dải ở cạnh.

**Mốc commit:** repo dự án `refactor(layout): dùng ViewportCamera, ViewportAnchor, ViewportCover của Horcrux` kèm con trỏ submodule
(`Assets/Horcrux` ở commit của GĐ5).

---

## File → giai đoạn

| File | GĐ1 | GĐ2 | GĐ3 | GĐ4 | GĐ5 | GĐ6 |
|---|---|---|---|---|---|---|
| `Runtime/Utilities/Layout/ViewportCamera.cs` | tạo | `anchors`, `safeAreaHeadroom`, `SafeViewport`, vòng gọi | `covers`, vòng gọi | | | `.meta` nhận GUID `3fc862fc…` |
| `Runtime/Utilities/Layout/ViewportAnchor.cs` | | tạo | | | | `.meta` nhận GUID `c29c8c47…` |
| `Runtime/Utilities/Layout/SafeAreaHeadroom.cs` | | tạo | | | | |
| `Runtime/Utilities/Layout/ViewportCover.cs` | | | tạo | | | `.meta` nhận GUID `233404ff…` |
| `Runtime/Utilities/Layout/ViewportCamera.Editor.cs` (agent) | | | | tạo | | |
| `Runtime/Utilities/Layout/ViewportAnchor.Editor.cs` (agent) | | | | tạo | | |
| `Tests/EditMode/ViewportLayoutTests.cs` (agent) | tạo | thêm case | thêm case | | | |
| `docs/Runtime/ViewportAnchor/ViewportAnchor.md`, `.html` (agent) | | | | | tạo | |
| dự án: xoá 6 file bản tạm và `SafeAreaHeadroomTests.cs`, `HeadroomSafeArea.cs`, asset `SafeAreaHeadroom_Game`, `GameplayRoot.prefab`, `Game.unity`, `Scripts/Layout.md`, `Structure.md` | | | | | | đổi |
