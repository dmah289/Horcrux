# Plan — `ViewportCamera`, `ViewportAnchor`, `ViewportCover`: camera ortho giữ trọn khung thiết kế, vật world neo mép màn, nền phủ kín

Bản tạm: category-jam `Scripts/Utilities/` (`GameplayViewport.cs`, `GameplayViewport.Editor.cs`, `ViewportAnchor.cs`, `ViewportAnchor.Editor.cs`, `ViewportCover.cs`, `SafeAreaHeadroom.cs`, `BannerSpace.cs`) gắn ở `GameplayRoot.prefab` › `camera_main` (1 camera, 9 anchor, 1 cover).
**Phụ thuộc:** không — gõ lúc nào cũng được. Plan chờ: Chuyển dự án của [Ads](Horcrux_Ads_Plan.md) dùng `BottomInset` của GĐ2.
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.EditModeTests.csproj --tests Horcrux.EditModeTests`; chuyển dự án `--csproj CategoryJam.Gameplay.csproj,CategoryJam.Editor.csproj,CategoryJam.EditModeTests.csproj,com.horcrux.runtime.csproj,com.horcrux.editor.csproj,Horcrux.EditModeTests.csproj --tests CategoryJam.EditModeTests,Horcrux.EditModeTests`.

## Kịch bản

| Tình huống | Hệ làm gì | Người chơi thấy |
|---|---|---|
| màn đúng tỉ lệ thiết kế | ortho giữ bề rộng = giữ chiều cao | sprite đúng cỡ art |
| màn cao hơn thiết kế (1080×2400) | ortho giữ bề rộng | world giữ cỡ, dư phần dọc; cụm trên dời theo mép trên |
| màn rộng hơn thiết kế (16:9, tablet 3:2) | ortho giữ chiều cao | world co đều (16:9: 82 % cỡ art), dư hai bên; khe HUD–world giữ tỉ lệ demo |
| vật neo mép + offset pixel demo | điểm màn → world, giữ z | vật bám mép ở mọi tỉ lệ, khe với HUD như demo |
| máy tai thỏ, inset trên nằm gọn trong dải trống thiết kế | `Absorb`: không dời | cụm trên đứng như ở Game view |
| inset trên vượt dải trống | dời đúng phần vượt | cụm world và HUD dời cùng số pixel |
| dự án chừa dải đáy cho banner lúc vào màn | `BottomInset.Raise` nâng mép dưới theo provider | cụm dưới và HUD nâng cùng số pixel |
| không ai đăng ký provider | dải đáy 0 | như không có banner |
| dải đáy cao hơn vùng safe | vùng rỗng, không lật | anchor đáy không nhảy lên đỉnh |
| banner ẩn, hiện giữa màn | không tính lại | layout đứng yên |
| anchor con dưới anchor cha | gọi theo thứ tự mảng | con đặt trên cha đã dời |
| anchor trên object tắt | vẫn được đặt | đúng chỗ khi bật |
| điểm viewport ngoài 0–1 | không kẹp | vật ngoài màn hợp lệ |
| ảnh nền | anchor tâm + cover scale đều phủ kín | nền kín, trục thừa tràn ra ngoài màn |
| màn đổi cỡ giữa phiên, đổi Game view lúc Play | không tính lại | Play lại mới đúng |
| ô `Target Camera`, `Safe Area Headroom`, `Target` trống | `NullReferenceException` ở `Awake`, trỏ đúng component | lỗi setup lộ ở frame đầu |
| bấm **Apply Layout** trên camera hay anchor (Edit mode) | cả chuỗi chạy lại | Scene view như lúc Play; bấm nhiều lần không trôi |
| anchor không cùng root với camera, bấm nút | `LogError`, không đổi gì | Console báo |

## Flow

```
  Unity Awake ─────────────► ViewportCamera.ApplyLayout                                  Runtime/Utilities/Layout/ViewportCamera.cs
  nút Apply Layout (camera) ─┘        │                                                  ViewportCamera.Editor.cs (agent)
  nút Apply Layout (anchor) ──────────┤  tìm ViewportCamera cùng root                    ViewportAnchor.Editor.cs (agent)
                                      ├─ targetCamera.orthographicSize = designWidthUnits / 2 · max(1 / aspect, designH / designW)
                                      ├─ safe = BottomInset.Raise(safeAreaHeadroom.Absorb(Screen.safeArea)) / cỡ màn
                                      │        SafeAreaHeadroom.cs (asset) · BottomInset.cs ◄── IBottomInsetProvider (dự án, tuỳ chọn)
                                      ├─ anchors[i].Apply(targetCamera, safe, px→viewport)   ViewportAnchor.cs: mép (trong safe) + designPixelOffset → world, giữ z
                                      └─ covers[i].Apply(targetCamera)                       ViewportCover.cs: target.localScale = max(viewW / spriteW, viewH / spriteH)

  panel safe area của HUD (dự án) ──► cùng asset SafeAreaHeadroom.Absorb, cùng BottomInset.Raise ──► khung HUD dời cùng số pixel với cụm world
```

## Thuật toán

| Bước | Công thức | Mốc |
|---|---|---|
| ortho | `size = designWidthUnits / 2 · max(1 / aspect, designResolution.y / designResolution.x)` — giữ bề rộng hay chiều cao, lấy số lớn để view chứa trọn thiết kế; hai số bằng nhau ở aspect thiết kế nên đổi nhánh không giật | `10.8`, 1080×2340: 1080×2340 → 11.7 · 1080×2400 → 12.0 · 1080×1920 và 2136×3200 → 11.7 |
| pixel thiết kế → viewport | canvas HUD khớp chiều cao: 1 px thiết kế = `H / designResolution.y` px màn trên cả hai trục → `(1 / (designResolution.y · aspect), 1 / designResolution.y)` | |
| điểm neo | `area.position + area.size ⊙ point + offset ⊙ px→viewport`; `area` là safe (viewport) hay `(0, 0, 1, 1)` | |
| viewport → world | `ViewportToWorldPoint(x, y, 0)`, camera ortho: điểm trên mặt phẳng z của camera; lấy x, y, giữ z transform | ortho 9.6, 9:16: `(0, 0)` → `(−5.4, −9.6)`, `(1, 1)` → `(5.4, 9.6)` |
| unit cũ → offset | 1 unit = `designResolution.x / designWidthUnits` px thiết kế — không phụ thuộc aspect máy, PPU art, camera Game view lúc đo; `u` unit → `u · designResolution.x / designWidthUnits` px | dự án 10.5 unit: −4.9 unit = −504 px |
| dải trống trên | inset `i = H − safeArea.yMax`, dải `h = top · H`, `shift = max(0, i − h)`; `top` theo chiều cao màn vì HUD khớp chiều cao | `top = 94.3 / 2340`: 1080×2400 inset 110 → 13.3 px · 1170×2532 inset 141 → 39.0 px |
| dải đáy | `yMin' = min(yMin + inset, yMax)` | |
| cover | view `2 · orthographicSize` × `· aspect`; `scale = max(viewW / spriteW, viewH / spriteH)` trên `sprite.bounds` ở scale 1 | ảnh 10.8×23.4: 9:19.5 → 1 · máy thấp hơn cắt trên dưới · máy cao hơn cắt hai bên |

Bản tạm cùng công thức đo trên Device Simulator: Poco M7 Pro 5G dời 13 px, iOS Notch Device 39 px so với safe area cả màn.

---

## GĐ1 — xong thì ở cỡ thiết kế sprite đúng cỡ art; màn cao chỉ dư phần dọc, màn rộng co đều cả world

```
  Unity Awake ──► ViewportCamera+ ──orthographicSize+──► targetCamera
```

`Runtime/Utilities/Layout/ViewportCamera.cs` — file mới:

```csharp
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Layout
{
    /// <summary>
    /// Sizes an orthographic camera so the design (<see cref="designResolution"/>, <see cref="designWidthUnits"/> wide; 10.8 = 1080 px
    /// of art at 100 pixels per unit) fits the screen: width on screens as tall as the design or taller, height on wider ones, as a
    /// canvas matching height does. Runs once at boot: the screen keeps one size for the whole session.
    /// </summary>
    // partial: nút Apply Layout nằm ở ViewportCamera.Editor.cs, không chạm dòng khai báo này.
    public sealed partial class ViewportCamera : MonoBehaviour
    {
        #region Properties

        [Splitter("References")]
        [SerializeField] private Camera targetCamera;

        [Splitter("Configs")]
        [Tooltip("World units across the design width.")]
        [SerializeField, Min(0.01f)] private float designWidthUnits = 10.8f;
        // Lặp reference resolution của CanvasScaler: canvas ở scene khác prefab camera, không kéo được.
        [Tooltip("Demo size in pixels. Same as the HUD CanvasScaler reference resolution; anchor offsets are in these pixels.")]
        [SerializeField] private Vector2 designResolution = new(1080f, 2340f);

        #endregion

        #region Unity Callbacks

        // Awake, không Start: mọi thứ khác đọc vị trí anchor sớm nhất ở Start hay frame tạo level.
        private void Awake() => ApplyLayout();

        #endregion

        #region API

        public void ApplyLayout()
        {
            // camera.aspect, không Screen: Screen ở Awake khi Editor không focus trả cỡ ngang.
            targetCamera.orthographicSize = OrthographicSize(designWidthUnits, designResolution, targetCamera.aspect);
        }

        /// <summary>
        /// Half view height in world units: the design width spans the screen unless the screen is wider than the design,
        /// then the design height spans it. The larger of the two keeps the whole design visible.
        /// </summary>
        // Max, không chỉ giữ bề rộng: HUD khớp chiều cao; chỉ bề rộng thì tablet giãn khe HUD–world.
        public static float OrthographicSize(float designWidthUnits, Vector2 designResolution, float screenAspect)
            => designWidthUnits * 0.5f * Mathf.Max(1f / screenAspect, designResolution.y / designResolution.x);

        #endregion
    }
}
```

Editor setup (dự án mới; category-jam đã có trong `GameplayRoot.prefab` › `camera_main`):

| # | Thao tác | Thiếu thì |
|---|---|---|
| 1 | Camera › **Projection** = `Orthographic` | công thức ghi `orthographicSize` mà camera không đọc: bề rộng không cố định |
| 2 | Gắn `ViewportCamera` lên object của camera; ô **Target Camera** kéo chính camera đó | `NullReferenceException` ở `Awake`, trỏ `ViewportCamera` |
| 3 | **Design Width Units**: số unit world trải hết bề rộng thiết kế (mặc định `10.8` cho art 1080 px ở 100 px/unit) | dùng mặc định; art PPU khác 100 thì sprite sai cỡ, không lỗi |
| 4 | **Design Resolution**: cỡ demo, bằng reference resolution của `CanvasScaler` HUD | khác canvas: màn rộng hơn thiết kế đổi nhánh sai chỗ, khe HUD–world lệch theo máy |
| 5 | Game view chọn cỡ **dọc** (vd 1080×2340) trước khi Play | aspect đọc một lần ở `Awake`: Game view ngang cho ortho sai, mọi thứ sai cỡ; đổi cỡ khi đang Play thì Play lại |

`Tests/EditMode/ViewportLayoutTests.cs` (agent) — camera `new GameObject().AddComponent<Camera>()`, `orthographic = true`, `aspect` tường minh; field private qua `SerializedObject` (`FindProperty`, `ApplyModifiedPropertiesWithoutUndo`); `Awake` không chạy ở EditMode nên gọi `ApplyLayout()`:

| Case | Input | Kỳ vọng |
|---|---|---|
| aspect thiết kế | `designWidthUnits 10.8`, `designResolution 1080×2340`, `aspect 1080/2340` | `orthographicSize` 11.7 (±1e-4) |
| cao hơn thiết kế | `aspect 1080/2400` | 12.0: giữ bề rộng |
| rộng hơn: 9:16 | `aspect 1080/1920` | 11.7: giữ chiều cao |
| rộng hơn: tablet 3:2 | `aspect 2136/3200` | 11.7 |
| đổi bề rộng | `designWidthUnits 10.5`, `aspect 1080/2340` | 11.375 |
| gọi lại | `ApplyLayout` hai lần cùng aspect | cùng số; không cộng dồn |
| camera đổi aspect giữa hai lần gọi | 1080/2400 rồi 1080/2340 | 12.0 rồi 11.7: không cache |

---

## GĐ2 — xong thì hàng box, thanh khay, mask nằm đúng mép ở mọi tỉ lệ; máy tai thỏ dời cụm trên đúng phần vượt dải trống, cùng số pixel với HUD

```
  Unity Awake ──► ViewportCamera ──orthographicSize──► targetCamera
                        │
                        ├──SafeViewport()+ ──Absorb(Screen.safeArea)+──► SafeAreaHeadroom+ (asset)
                        │                   └─BottomInset.Raise+ ──BottomInsetPixels──► IBottomInsetProvider (dự án, thiếu = 0)
                        └──anchors[i].Apply(targetCamera, safe, designPixelToViewport)+──► ViewportAnchor+ ──transform.position──► vật thể world
```

`Runtime/Utilities/Layout/SafeAreaHeadroom.cs` — file mới:

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
            // top nhân chiều cao màn: canvas HUD khớp chiều cao nên dải trống co giãn theo H.
            var shift = Mathf.Max(0f, inset - top * screenHeight);
            return Rect.MinMaxRect(safeArea.xMin, safeArea.yMin, safeArea.xMax, screenHeight - shift);
        }

        #endregion
    }
}
```

`Runtime/Utilities/Layout/ViewportAnchor.cs` — file mới:

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
        [Tooltip("Edge point the offset is measured from, (0,0) bottom-left to (1,1) top-right.")]
        [SerializeField] private Vector2 viewportPoint = new(0.5f, 0.5f);
        [Tooltip("Offset from the viewport point in pixels of the design resolution, scaled with the screen height like the HUD canvas.")]
        [SerializeField] private Vector2 designPixelOffset;
        [Tooltip("Resolve the viewport point inside the safe area ViewportCamera passes in instead of the whole screen.")]
        [SerializeField] private bool respectSafeArea;

        #endregion

        #region API

        /// <summary>
        /// Moves this transform to the configured screen point as seen by <paramref name="camera"/>; keeps z.
        /// <paramref name="safeViewport"/> is the safe area in viewport units (0–1), used only when the anchor respects it;
        /// <paramref name="designPixelToViewport"/> converts one design pixel to viewport units on each axis.
        /// </summary>
        // Không Awake: ViewportCamera gọi theo mảng sau khi đặt size, anchor con sau anchor cha.
        public void Apply(Camera camera, Rect safeViewport, Vector2 designPixelToViewport)
        {
            var area = respectSafeArea ? safeViewport : new Rect(0f, 0f, 1f, 1f);
            var point = ViewportPoint(area, viewportPoint, designPixelOffset, designPixelToViewport);
            // z = 0 chỉ đúng cho camera ortho; perspective chưa có người dùng.
            var world = camera.ViewportToWorldPoint(new Vector3(point.x, point.y, 0f));
            // Giữ z: thứ tự vẽ 2.5D sống ở z, anchor chỉ lo x, y.
            transform.position = new Vector3(world.x, world.y, transform.position.z);
        }

        /// <summary><paramref name="point"/> inside <paramref name="area"/>, then the design-pixel offset; all in viewport units.</summary>
        // Offset bằng pixel thiết kế, không unit world: cùng đơn vị HUD nên khe HUD–world giữ trên mọi máy.
        public static Vector2 ViewportPoint(Rect area, Vector2 point, Vector2 designPixelOffset, Vector2 designPixelToViewport)
            => area.position + Vector2.Scale(area.size, point) + Vector2.Scale(designPixelOffset, designPixelToViewport);

        #endregion
    }
}
```

`ViewportCamera.cs` › `Properties` › thêm sau `targetCamera`, trước `[Splitter("Configs")]`:

```csharp
        [SerializeField] private Camera targetCamera;
        [Tooltip("Applied in this order, after the camera size is set.")]
        [SerializeField] private ViewportAnchor[] anchors;
        [Tooltip("Design top band for anchors that respect the safe area. Same asset as the HUD safe-area panel of this screen.")]
        [SerializeField] private SafeAreaHeadroom safeAreaHeadroom;
```

`ViewportCamera.cs` › `API` › `ApplyLayout` — thêm sau dòng đặt `orthographicSize`:

```csharp
            // camera.aspect, không Screen: Screen ở Awake khi Editor không focus trả cỡ ngang.
            targetCamera.orthographicSize = OrthographicSize(designWidthUnits, designResolution, targetCamera.aspect);

            var safe = SafeViewport();
            var designPixelToViewport = new Vector2(1f / (designResolution.y * targetCamera.aspect), 1f / designResolution.y);
            foreach (var anchor in anchors)
                anchor.Apply(targetCamera, safe, designPixelToViewport);
```

`Runtime/Utilities/Layout/BottomInset.cs` — file mới:

```csharp
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Layout
{
    /// <summary>
    /// Screen pixels reserved at the bottom outside the canvas, such as a banner. The project registers one with
    /// <c>[Service(typeof(IBottomInsetProvider))]</c> and fixes the value before a screen is built.
    /// </summary>
    public interface IBottomInsetProvider
    {
        float BottomInsetPixels { get; }
    }

    /// <summary>Raises the bottom edge of a safe area; the HUD safe-area panel and <see cref="ViewportCamera"/> both call it.</summary>
    public static class BottomInset
    {
        /// <summary><paramref name="safeArea"/> in screen pixels, bottom raised by the registered provider; none: unchanged.</summary>
        // TryGet, không Get: màn không banner, dự án không quảng cáo thì không ai đăng ký.
        public static Rect Raise(Rect safeArea) =>
            Raise(safeArea, Service.TryGet<IBottomInsetProvider>(out var provider) ? provider.BottomInsetPixels : 0f);

        // Cửa của test; hàm một tham số chỉ đọc provider rồi gọi hàm này.
        public static Rect Raise(Rect safeArea, float insetPixels) =>
            // Min với yMax: dải cao hơn vùng thì vùng rỗng; rect lật làm anchor đáy nhảy lên đỉnh.
            Rect.MinMaxRect(safeArea.xMin, Mathf.Min(safeArea.yMin + insetPixels, safeArea.yMax), safeArea.xMax, safeArea.yMax);
    }
}
```

`ViewportCamera.cs` › region mới `Class Methods` sau `API`:

```csharp
        #region Class Methods

        // safe area in viewport units (0–1): top inset absorbed by the design headroom, bottom raised by the reserved band
        // Đọc Screen, không camera: safe area là của màn; đúng khi camera phủ cả màn.
        private Rect SafeViewport()
        {
            var safe = BottomInset.Raise(safeAreaHeadroom.Absorb(Screen.safeArea, Screen.height));
            return new Rect(safe.x / Screen.width, safe.y / Screen.height, safe.width / Screen.width, safe.height / Screen.height);
        }

        #endregion
```

`ViewportCamera.cs` › XML doc của class: thêm câu cuối `Then places every <see cref="ViewportAnchor"/> in order.`

Editor setup:

| # | Thao tác | Thiếu thì |
|---|---|---|
| 1 | Gắn `ViewportAnchor` lên object cần neo; **Viewport Point** là mép đo từ đó, `(0,0)` dưới trái → `(1,1)` trên phải; **Design Pixel Offset** là khoảng đo trên demo theo pixel thiết kế; khoảng có sẵn theo unit world thì nhân `designResolution.x / designWidthUnits`, không theo PPU của art hay cỡ camera Game view lúc chụp; **Respect Safe Area** bật cho vật kề HUD ở mép đó | object đứng ở vị trí prefab, không theo màn · điểm giữa màn thay cho mép: lệch HUD trên máy có inset · quy unit theo PPU hay camera lúc chụp: vật lệch khỏi demo một khoảng tỉ lệ với offset |
| 2 | `ViewportCamera` › ô **Anchors**: kéo từng anchor vào, **theo thứ tự muốn đặt** — anchor con sau anchor cha | anchor không trong mảng: không ai gọi, đứng yên; con trước cha: con đặt xong bị cha kéo đi |
| 3 | Hai vật kề nhau ở hai hệ toạ độ (world và canvas HUD) neo **cùng một mép, cùng luật co giãn** (vd cụm trên `y = 1`, cụm dưới `y = 0`, trong safe area, offset pixel demo) | ở màn thấp hai vật đè nhau |
| 4 | Project › **Create › Horcrux › Layout › Safe Area Headroom**, một asset cho mỗi thiết kế màn; **Top** = khoảng từ mép trên canvas tới mép trên phần tử HUD cao nhất, chia chiều cao canvas, đo ở cỡ demo | `top = 0`: máy tai thỏ dời cả cụm trên đúng bằng inset, tụt dưới dải trống demo đã chừa |
| 5 | `ViewportCamera` › ô **Safe Area Headroom** kéo asset đó; panel safe area của HUD cùng màn đọc **cùng asset** | ô trống: `NullReferenceException` ở `Awake`, trỏ `ViewportCamera` · HUD đọc asset khác hay safe area thô: khe HUD–world đổi theo máy |
| 6 | Đổi vị trí phần tử HUD cao nhất thì đo lại **Top** | dải trống lệch thiết kế: máy tai thỏ dời thiếu hay thừa |

`Tests/EditMode/ViewportLayoutTests.cs` (agent) — camera `(0, 0, −10)`, aspect 9:16, `designWidthUnits 10.8`, `designResolution 1080×1920` (ortho 9.6), `SafeAreaHeadroom` từ `ScriptableObject.CreateInstance` gán vào `safeAreaHeadroom`, rồi `ApplyLayout`; ca safe area lệch gọi thẳng `Apply(camera, rect, Vector2.zero)`, ca offset gọi `ViewportPoint`:

| Case | Input | Kỳ vọng |
|---|---|---|
| tâm | anchor `(0.5, 0.5)` | position `(0, 0)` |
| góc dưới trái | `(0, 0)` | `(−5.4, −9.6)` |
| góc trên phải | `(1, 1)` | `(5.4, 9.6)` |
| ngoài dải | `(1.5, −0.5)` | `(10.8, −19.2)`: không kẹp, điểm ngoài màn hợp lệ |
| offset | `(0.5, 0.5)`, `designPixelOffset (100, −200)` | `(1, −2)`: 1 unit = 100 px thiết kế |
| offset đo từ mép safe | `ViewportPoint((0, 0.1, 1, 0.85), (0, 1), (100, −100), (0.002, 1/2000))` | `(0.2, 0.9)` |
| offset trùng unit cũ ở cỡ thiết kế | 10.5 unit, 1080×2340, `(0.5, 1)`, `designPixelOffset (0, −504)` | dưới mép trên 4.9 unit |
| giữ z | transform z = 7 trước `Apply` | z vẫn 7 |
| camera không ở gốc | camera `(3, 1, −10)`, anchor `(0.5, 0.5)` | `(3, 1)` |
| thứ tự mảng | anchor B là con của anchor A, mảng `[A, B]`, A `(0, 1)`, B `(1, 0)` | B ở `(5.4, −9.6)` thế giới sau khi A đã dời |
| thứ tự đảo | mảng `[B, A]` | B lệch đúng bằng độ dời của A: bằng chứng thứ tự mảng là hợp đồng |
| object tắt | anchor trên object `SetActive(false)` | vẫn được đặt |
| mảng rỗng | `anchors` rỗng | `ApplyLayout` không ném |
| safe area cả màn | `respectSafeArea true`, Editor có safe area = cả màn | bằng ca `false` |
| safe area hạ mép trên | `Apply(camera, (0, 0, 1, 0.95), Vector2.zero)`, `respectSafeArea true`, anchor `(0.5, 1)` | `(0, 8.64)` |
| safe area lệch trái | `Apply(camera, (0.1, 0, 0.8, 1), Vector2.zero)`, `respectSafeArea true`, anchor `(0, 0.5)` | `(−4.32, 0)` |
| không bật safe area | `Apply(camera, (0, 0.05, 1, 0.9), Vector2.zero)`, `respectSafeArea false`, anchor `(0.5, 1)` | `(0, 9.6)`: rect bị bỏ qua |
| không inset | `Absorb((0, 0, 1080, 2400), 2400)`, `top 94.3/2340` | `yMax` 2400 |
| inset trong dải | inset 86.7 (dải ≈ 96.7) | `yMax` 2400: không dời |
| inset vượt dải | inset 110 | `2400 − yMax` = 110 − 96.7 ≈ 13.3 (±1e-3) |
| dải 0 | `top 0`, inset 110 | `2400 − yMax` = 110: bằng safe area thô |
| cạnh khác giữ | `(12, 48, 1050, 2152)`, inset 200 | `xMin` 12, `yMin` 48, `xMax` 1062 |
| dải đáy | `Raise((0, 34, 1080, 2306), 168)` | `yMin` 202; `xMin`, `xMax`, `yMax` giữ |
| dải đáy 0 | `Raise(rect, 0)` | bằng `rect` |
| dải cao hơn vùng | `Raise((0, 0, 1080, 100), 168)` | `yMin` = `yMax` = 100: vùng rỗng, không lật |
| không provider | `Raise(rect)` khi chưa đăng ký `IBottomInsetProvider` | bằng `rect` |

---

## GĐ3 — xong thì ảnh nền không hở mép ở mọi tỉ lệ

```
  Unity Awake ──► ViewportCamera ──orthographicSize──► targetCamera
                        ├──anchors[i].Apply──► ViewportAnchor ──position──► vật thể world (kể cả object nền)
                        └──covers[i].Apply+──► ViewportCover+ ──localScale──► target (SpriteRenderer nền)
```

`Runtime/Utilities/Layout/ViewportCover.cs` — file mới:

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
            // bounds ở scale 1, không đọc localScale hiện tại: gọi lại, bấm nút nhiều lần không cộng dồn.
            var size = target.sprite.bounds.size;
            var scale = Mathf.Max(viewWidth / size.x, viewHeight / size.y);
            target.transform.localScale = new Vector3(scale, scale, 1f);
        }

        #endregion
    }
}
```

`ViewportCamera.cs` › `Properties` › thêm sau `anchors`:

```csharp
        [SerializeField] private ViewportAnchor[] anchors;
        [Tooltip("Sprites scaled to cover the screen, after the camera size is set.")]
        [SerializeField] private ViewportCover[] covers;
```

`ViewportCamera.cs` › `API` › `ApplyLayout` — thêm sau vòng `anchors`:

```csharp
            foreach (var anchor in anchors)
                anchor.Apply(targetCamera, safe, designPixelToViewport);

            foreach (var cover in covers)
                cover.Apply(targetCamera);
```

`ViewportCamera.cs` › XML doc của class: `... then places every <see cref="ViewportAnchor"/> in order and scales every <see cref="ViewportCover"/> to fill the screen.`

Editor setup:

| # | Thao tác | Thiếu thì |
|---|---|---|
| 1 | Object nền: `SpriteRenderer` + `ViewportAnchor` **Viewport Point** `(0.5, 0.5)` + `ViewportCover`; ô **Target** kéo `SpriteRenderer` của chính nó; cha scale 1 | thiếu anchor: nền phủ kín nhưng lệch tâm, hở một mép · thiếu `Target`: `NullReferenceException` ở `Awake` · cha scale ≠ 1: scale nhân đôi, nền quá to hoặc hở |
| 2 | `ViewportCamera` › ô **Covers**: kéo `ViewportCover` của nền vào; object nền cũng nằm trong **Anchors** | nền giữ scale prefab, hở mép ở tỉ lệ khác ảnh |
| 3 | Ảnh nền import **Sprite (2D and UI)**, Sprite Mode `Single`, Mesh Type `Full Rect`, PPU bằng art; đặt ngoài thư mục atlas world | vào atlas: một ảnh cỡ cả màn làm phình atlas · Mesh Type `Tight`: `bounds` vẫn là rect, không ảnh hưởng, nhưng tốn vertex vô ích |

`Tests/EditMode/ViewportLayoutTests.cs` (agent) — sprite `Sprite.Create` từ `Texture2D` 108×234 px, 10 PPU (10.8×23.4 unit); `designWidthUnits 10.8`, `designResolution 1080×1920` (9:16 là thiết kế, view 10.8×19.2):

| Case | Input | Kỳ vọng |
|---|---|---|
| máy cao hơn ảnh | sprite 10.8×23.4, aspect 9:21 (view 10.8×25.2) | scale 1.0769 (±1e-3): kín chiều cao, tràn hai bên |
| máy thấp hơn ảnh | aspect 9:16 (view 10.8×19.2) | scale 1: kín bề rộng, tràn trên dưới |
| khớp | aspect 9:19.5 | scale 1 |
| ảnh ngang | sprite 20×10, aspect 9:16 | 1.92: trục cao quyết định |
| giữ z | `localScale.z` sau `Apply` | 1 |
| gọi lại | `Apply` hai lần | cùng scale, không cộng dồn |
| mảng rỗng | `covers` rỗng | `ApplyLayout` không ném |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `Runtime/Utilities/Layout/ViewportCamera.Editor.cs`, `ViewportAnchor.Editor.cs` | tạo sau GĐ3 (code dưới), GUID tuỳ ý; bọc `#if UNITY_EDITOR` nên compile cả Editor lẫn build |
| `docs/Runtime/ViewportAnchor/ViewportAnchor.md`, `.html` | tạo một tài liệu cho ba component, asset và `BottomInset`, `Code: Runtime/Utilities/Layout/`: "Trước khi chạy" = Editor setup GĐ1–3 và nút gộp một bảng · Flow · bảng API (`ApplyLayout()`, `OrthographicSize`, `Apply(Camera, Rect, Vector2)`, `ViewportPoint`, `ViewportCover.Apply(Camera)`, `Absorb(Rect, float)`, `BottomInset.Raise`, field serialize) · Thuật toán · Không làm · bẫy: quy unit ra offset theo `designResolution.x / designWidthUnits`; `Screen` ở `Awake` khi Editor không focus; đổi đơn vị camera thì grep mọi hằng khoảng cách, kể cả hằng scale, trước lần Play đầu; safe area thô đếm inset hai lần, Game view không lộ; camera chỉ giữ bề rộng làm tablet giãn khe và co bàn chơi · kịch bản dùng tên trung tính: cụm kề HUD trên `y = 1` trong safe area offset âm, panel HUD và camera cùng asset, đo Top ở cỡ demo; cụm kề thanh dưới `y = 0` offset dương; nền = anchor tâm + cover · kiểm máy tai thỏ bằng Device Simulator (`docs/Editor/Probe.md`, `DEVICES`) |
| probe trước | trước khi xoá bản tạm: `Editor/Probe` `playrun.sh` với `SIZES="1080x2340 1080x1920 2136x3200"`, `DEVICES="Poco M7 Pro 5G;iOS Notch Device"`, `PLAY_FROM_BUILD_SCENE=1`, vào level, in `ScreenBox.Of` của 9 anchor, khung `*SafeArea*`, `localScale` của `background`; Menu theo `docs/tools/layout_probe/measure_menu.cs` |
| bản tạm | Editor đóng, Prefab Mode `GameplayRoot` đóng; xoá 6 file `Scripts/Utilities/` (trừ `BannerSpace.cs` ở hàng dưới), `Tests/EditMode/SafeAreaHeadroomTests.cs`, `GameplayViewportTests.cs` (+ `.meta`) cùng một lần Refresh với hai hàng kế — hai asset cùng GUID thì Unity cấp GUID mới, 11 script missing |
| `.meta` Horcrux | `ViewportCamera.cs.meta` ← `3fc862fca3d20714aa53ea069560ef9f` · `ViewportAnchor.cs.meta` ← `c29c8c47f2b864541b173c28f727e131` · `ViewportCover.cs.meta` ← `233404ff652a67d4ca28f25e2e133308` · `SafeAreaHeadroom.cs.meta` ← `fb1d5289493ff0b48971e95027977c88` |
| `GameplayRoot.prefab` | key `gameplayCamera: {fileID: 5567243261129153408}` → `targetCamera: {fileID: 5567243261129153408}`; key `anchors`, `covers`, `designWidthUnits`, `designResolution` giữ — developer gõ lệch tên field thì sửa key theo code |
| asset `SafeAreaHeadroom_Game`, `SafeAreaHeadroom_Menu` và ô đã kéo (`camera_main`, `Game.unity` › `ViewInGame/*SafeArea*`, `Menu.unity` › `Home/*SafeArea*`, `CanvasMenu/*SafeArea*`) | giữ nguyên; chỉ đổi type dưới asset nhờ GUID |
| `Scripts/Hud/HeadroomSafeArea.cs` | `using CategoryJam.Gameplay.Utilities;` → `using Horcrux.Runtime.Utilities.Layout;`; `BannerSpace.RaiseBottom` → `BottomInset.Raise` |
| `Scripts/Utilities/BannerSpace.cs`, `Tests/EditMode/BannerSpaceTests.cs` (+ `.meta`) | xoá — case đã ở `ViewportLayoutTests` |
| `_Game/Scripts/Ads/GameplayBanner.cs` | cài `IBottomInsetProvider` thay `IBannerSpace`, `ReservedPixels` → `BottomInsetPixels`, `[Service(typeof(GameplayBanner), typeof(IBottomInsetProvider))]` — thiếu kiểu provider thì dải đáy 0, banner đè booster bar không log |
| `Scripts/Layout.md` | §1 `GameplayViewport` → `ViewportCamera`, "aspect đọc từ `Screen`" → "từ `camera.aspect`", nút trên anchor là **Apply Layout**; §2, §3, "Đã sai một lần", "Trước khi chạy" hàng 2 đổi tên type và key; menu **Create › Horcrux › Layout › Safe Area Headroom**; "Mở rộng sau" bỏ mục "lên Horcrux" |
| `Structure.md` | dòng `Utilities/` bỏ bốn type và "bản tạm chờ lên Horcrux"; dòng `Prefabs/` `GameplayViewport` → `ViewportCamera`; bảng nợ bỏ hàng bản tạm |
| kiểm | lệnh chuyển dự án ở dòng mở (lỗi build ngay sau khi xoá file là tạm tới khi Editor sinh lại csproj — chạy lại) · grep `GameplayViewport`, `gameplayCamera`, `CategoryJam.Gameplay.Utilities.Viewport`, `TopHeadroom`, `BannerSpace`, `IBannerSpace` trên `.cs`, `.prefab`, `.unity`, `.md` = 0 · probe sau trùng probe trước từng số ở ba cỡ, hai máy (cũng là phép kiểm `SafeViewport`, không dựng được ở EditMode) |
| Plan này | xoá cùng `.meta` |
| chơi thử | Prefab Mode `GameplayRoot`, Game view 1080×2340, bấm Apply Layout trên `camera_main`: hàng box, thanh khay như lúc Play → 1080×1920 bấm lại: cụm co đều 82 %, khe với HUD giữ tỉ lệ demo → 1080×2400: world giữ cỡ, cụm dời theo mép trên · Play level 1 ở 1080×2340, 1080×1920, 2136×3200 và máy tai thỏ: hàng box, thanh khay, mask, bàn chơi như trước khi chuyển, nền kín, khe pill–hàng box như Game view; hỏng: bấm nhiều lần vị trí hay scale nền trôi · 11 script missing trên `GameplayRoot` · `NullReferenceException` ở `Awake` của `ViewportCamera` · cụm trên chui dưới tai thỏ hay tụt sâu dưới dải trống · nền hở một dải |

`Runtime/Utilities/Layout/ViewportCamera.Editor.cs` (agent):

```csharp
#if UNITY_EDITOR
using Sirenix.OdinInspector;

namespace Horcrux.Runtime.Utilities.Layout
{
    public sealed partial class ViewportCamera
    {
        // Odin đã là phụ thuộc của com.horcrux.runtime; bọc UNITY_EDITOR nên build không mang nút.
        [Button("Apply Layout")]
        private void ApplyLayoutInEditor() => ApplyLayout();
    }
}
#endif
```

`Runtime/Utilities/Layout/ViewportAnchor.Editor.cs` (agent):

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
            // root: trong Prefab Mode là root prefab; Camera.main lúc đó là camera của scene đang mở.
            var viewport = transform.root.GetComponentInChildren<ViewportCamera>(true);
            if (viewport == null)
            {
                Debug.LogError("[ViewportAnchor]: No ViewportCamera under the same root.", this);
                return;
            }

            // Cả chuỗi, không Apply lẻ: anchor đúng chỉ khi camera đã đặt size và cha đã dời.
            viewport.ApplyLayout();
        }
    }
}
#endif
```

| # | Thao tác với nút | Thiếu thì |
|---|---|---|
| 1 | Game view chọn cỡ dọc **trước** khi bấm nút | `targetCamera.aspect` ở Edit mode theo Game view: Game view ngang cho Scene view sai y hệt Play |
| 2 | Anchor và camera cùng một root (prefab hay scene) | nút trên anchor báo `[ViewportAnchor]: No ViewportCamera under the same root.` trong Console, không đổi gì |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | aspect từ `Screen.width / Screen.height` (bản dự án) | camera có `rect` nhỏ hơn màn thì sai; Editor không focus thì `Awake` đọc cỡ ngang (ortho 3.94 thay 11.7); test không đặt được `Screen` |
| bỏ | tên `GameplayViewport`, `gameplayCamera` (bản dự án) | tên theo cơ chế, không theo người dùng đầu |
| bỏ | `[DefaultExecutionOrder]` + `Awake` trên camera, anchor | thứ tự ngầm, không thấy trong Inspector; không xếp được thứ tự giữa hai anchor |
| bỏ | `enum` chọn trục camera | trục đúng phụ thuộc màn, không phụ thuộc dự án |
| bỏ | ngưỡng aspect riêng | thêm một hằng; vùng giữa ngưỡng và aspect thiết kế vẫn lệch |
| bỏ | guard `targetCamera`, `safeAreaHeadroom`, `target`, `sprite` null | `NullReferenceException` ở frame đầu trỏ đúng component; Editor setup ghi "thiếu thì" |
| bỏ | field số dải trống trên mỗi consumer | HUD và world là hai bản buộc khớp mà không gì canh |
| bỏ | hằng static dải trống dự án đặt lúc boot | bước setup ẩn, quên thì im lặng đếm inset hai lần |
| bỏ | đặt cả vị trí anchor bằng phần của safe area | điểm `y = 0.785` chỉ tụt 78.5 % inset trên và nhích theo inset dưới, HUD tụt đúng 100 % |
| bỏ | anchor nghe sự kiện banner rồi `ApplyLayout()` lại | bàn chơi nhảy giữa lượt; dải đáy chốt lúc vào màn (cùng quyết định với `Horcrux_Ads_Plan.md`) |
| bỏ | ô số pixel dải đáy trên `ViewportCamera` | dải quyết lúc chạy, không biết lúc authoring |
| bỏ | `AdService` của Horcrux cài `IBottomInsetProvider` | dải theo mốc level và màn có banner: luật của dự án |
| bỏ | tên theo banner (`BannerSpace`) | nguồn inset đáy khác (thanh điều hướng tự vẽ, quảng cáo native) dùng cùng cửa |
| bỏ | `bool coverScreen` trên anchor | anchor thêm lý do thay đổi và một ô `SpriteRenderer` vô nghĩa cho 9 anchor khác |
| bỏ | cover kéo méo cho khít | méo art; nền vẽ dư hai đầu thì cắt trục thừa là đủ |
| bỏ | nền trên Canvas Screen Space–Camera + `AspectRatioFitter` `EnvelopeParent` | thêm một canvas cho một ảnh; sprite world chung đơn vị, sorting layer với gameplay; Overlay luôn đè gameplay |
| bỏ | `PositionConstraint`, Cinemachine | chạy mỗi frame, thừa cho camera đứng yên |
| bỏ | cover cho `Image` UI | đã có `UICoverFitter` (`Horcrux_UIFitters_Plan.md`) |
| bỏ | panel safe area cho UI | thuộc `SystemPlan.md` §6; dự án dùng panel của nó, chỉ đọc asset |
| bỏ | ô kéo `ViewportCamera` trên mỗi anchor cho nút | 9 ô kéo cho một nút Editor |
| bỏ | nút Apply Layout trên `ViewportCover` | cover chỉ có khi có camera cùng root; nút camera, anchor đã chạy cả chuỗi |
| sau | tính lại khi màn đổi cỡ: host gọi lại `ApplyLayout()` khi nhận sự kiện | game khoá dọc; không đổi chữ ký |
| sau | camera perspective | chưa có người dùng |
| sau | cover bù scale của cha: `Apply` chia cho `target.transform.parent.lossyScale` | khi có cha scale ≠ 1; không đổi chữ ký |
| sau | dải trống ở cạnh dưới và hai bên | chưa thiết kế nào chừa |
