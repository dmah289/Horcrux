# Plan — `UIProxyFlight`: bản sao UI bay tới vật world rồi nhường chỗ

Ngày 2026-11-23. Nguồn: category-jam `Assets/_CategoryJam/Scripts/Entities/Cache/BoosterFlight.cs` (`FlyAsync`), hai instance trong
`Game.unity` (`extra_slot_flight`, `pack_flight`), caller `CacheTileSlots.FlyTrayAsync`, `FlyPackAsync`; đã chơi thử cả hai booster.
Sprite world luôn bị canvas Overlay đè, nên vật không bay thẳng từ nút được: một bản sao UI bay từ nút tới chỗ vật, tới nơi đúng cỡ vật
trên màn hình, rồi caller bật vật thật cùng frame.

**Phân công:** GĐ1–GĐ2 developer gõ, agent viết test theo bảng case rồi compile · GĐ3 (tài liệu) · GĐ4 (dự án chuyển sang bản Horcrux,
xoá Plan) agent làm.
**Chạy được, thấy được:** GĐ1, GĐ2 compile sạch + bảng case xanh trong `Tests/PlayMode/UIProxyFlightTests.cs` (Test Runner, PlayMode);
thấy trong game ở GĐ4.
**Lệnh kiểm** (chạy từ gốc dự án): GĐ1–GĐ3 `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj
com.horcrux.runtime.csproj,Horcrux.PlayModeTests.csproj --tests Horcrux.EditModeTests` · GĐ4 thêm `CategoryJam.Gameplay.csproj` và
`--tests CategoryJam.EditModeTests,Horcrux.EditModeTests`. PlayMode không qua tool: chạy trong Window › General › Test Runner.
**Commit:** GĐ1, GĐ2, GĐ3 trong `Assets/Horcrux`; GĐ4 ở dự án kèm con trỏ submodule.
**Phụ thuộc:** GĐ2 phụ thuộc `Horcrux_HopCurve_DipFlash_Plan.md` › GĐ1 (`HopCurve.ApexT`, `HopCurve.ArcY`), viết như Plan đó đã gõ.
GĐ1 không phụ thuộc gì, gõ được sớm.
**Chung file:** `BoosterFlight.cs`, `Scripts/Feel.md` §10 — `Horcrux_HopCurve_DipFlash_Plan.md` › GĐ10. GĐ4 ở đây làm trước thì mục
`BoosterFlight.cs` của GĐ10 bên kia không còn gì để đổi (file chỉ còn lớp nối, không gọi `TileFlightCurve`); hàng §10 của `Feel.md` viết
một lần, theo tên hàm đang có lúc sửa (`HopCurve.ArcY` hay `TileFlightCurve.ArcY`).
**Đã kiểm trước khi ghi:** csc của Rider, `-langversion:9`, trên `UnityEngine.CoreModule`/`UIModule` của Editor 6000.0.73f1 và
`Library/ScriptAssemblies` (`com.horcrux.runtime`, `PrimeTween.Runtime`, `UniTask`, `UnityEngine.UI`, `Coffee.UIParticle`): `UIProxyFlight.cs`
bản GĐ1 và bản GĐ2 (kèm `HopCurve.cs` chép từ Plan HopCurve GĐ1), `BoosterFlight.cs` lớp con và hai lời gọi mới của GĐ4 — 0 lỗi. Bản
mẫu: mọi biểu thức là của bản dự án đã chơi thử (hai booster); Plan chỉ dời chúng, không đổi số. Hành vi huỷ đọc từ mã PrimeTween
`AsyncAwaitSupport` (`GetResult` ném `OperationCanceledException` khi token đã huỷ, kể cả huỷ trước lúc `await`). `§0`: không — hai công
thức một dòng nằm ở GĐ sinh ra chúng.

## Ngữ cảnh đã chốt

- **Người dùng:** nút vật phẩm nằm ở canvas Overlay, vật nó sinh ra là sprite world trên bàn chơi; bản sao bay từ nút tới chỗ vật sẽ đứng,
  tới nơi đúng bề ngang vật trên màn hình, caller bật vật thật. Ví dụ: vật phẩm hỗ trợ bay từ nút xuống bàn chơi · món mua trong shop bay
  vào túi đồ world · icon phần thưởng bay xuống một ô world khi nhận quà giữa màn.
- **Mục tiêu:** cơ chế bay (cỡ theo bề ngang đích trên màn hình, hai hình đường bay, nghiêng theo hướng ngang, dọn ở mọi đường ra) lên
  Horcrux; dự án chỉ còn lớp nối với đuôi hạt. **Cảm giác không đổi:** mọi biểu thức chép nguyên văn; số trên scene giữ nguyên.
- **Ranh giới:** canvas gốc là Screen Space Overlay; đích là một điểm world đứng yên suốt chuyến; caller bật vật thật sau `await`.
- **Chỗ đặt, tên:** `Runtime/Utilities/UI/UIProxyFlight.cs`, namespace `Horcrux.Runtime.Utilities.UI`, cạnh `FlyRewardItem`. Tên nói cơ
  chế (bản sao UI), không nói kịch bản. Tên chốt khi gõ.
- **Đuôi hạt ở lại dự án qua hai hook `protected virtual`:** đuôi hiện là Coffee `UIParticle`, ngoài stack Horcrux; framework không tham
  chiếu được assembly đó. Lớp nối của dự án ghi đè hai hook; script giữ GUID nên scene không phải kéo lại.
- **API nhận đích world:** hai caller hiện tự quy chỗ đáp và bề ngang world ra màn hình bằng cùng sáu dòng; quy đổi vào `FlyAsync`,
  caller truyền camera, điểm, bề ngang world.
- **Cố ý KHÔNG làm, kèm lý do:**
  - overload nhận đích màn hình — caller UI→UI duy nhất hiện có (icon popup nảy tại chỗ, đứng, rồi bay thẳng về nút HUD) khác hình bay:
    không vồng, không nghiêng, ease kiểu In để icon đập vào nút. Gộp khi có caller thứ hai cùng lý do thay đổi.
  - gộp với `FlyRewardItem` — khác lý do thay đổi: icon thưởng bay UI→UI, mờ dần, nổ hạt ở mốc; bản sao này nhường chỗ cho vật world.
  - canvas Screen Space Camera hay World Space — `visual.position = điểm màn hình` chỉ đúng ở Overlay; chưa có người dùng.
  - đuôi tuỳ chọn trong framework (field `ParticleSystem`) — `UIParticle` không có `ParticleSystem` ở gốc; hook rẻ hơn.
- **Quyết định trái trực giác:**
  - **Đích chốt lúc gọi, không đọc lại mỗi frame:** đích là chỗ nghỉ cuối của vật (tính bằng chính công thức bố cục, hoặc vật đứng yên),
    camera không đổi suốt chuyến. Đọc lại mỗi frame cần đích `Transform` và một lần quy đổi màn hình mỗi frame — chưa caller nào cần.
  - **Hình đường bay là `enum` tường minh từ `1`:** một ô số vừa là núm vừa là công tắc (`fallHeight > 0`) thì đặt 0 là đổi cả hình bay
    mà không gì báo. Giá trị `0` (ô chưa chọn) ghi `LogError` rồi bay hình `Arc`, người chơi không kẹt ở nút.
  - **Huỷ ném `OperationCanceledException`:** `await` một `Sequence` gắn `SetCancellationToken(ct)` mà token huỷ thì ném; `finally` vẫn
    tắt bản sao và gọi hook kết thúc.
  - **Cỡ đặt bằng `sizeDelta`, không scale transform:** con giữ cỡ riêng (đuôi hạt) không co giãn theo bản sao.
- **Mở rộng sau:** overload đích màn hình khi có caller UI→UI thứ hai cùng hình bay (thêm hình `Straight` và pha nảy–đứng trước khi bay;
  cỡ hai đầu qua `LocalScaleForScreenWidth` của `Horcrux_ScreenSize_Plan.md`) · bám đích: overload nhận `Transform`, quy đổi màn hình
  mỗi frame (cùng cách overload `Transform` của `Horcrux_CharmFlyArcFollow_Plan.md`), khi có đích trượt trong lúc bay · ba dòng quy đổi
  bề ngang world → pixel thành `WorldWidthOnScreen(this Camera, …)` trong `ScreenSizeExtensions` khi có người dùng thứ hai · canvas Camera
  khi có HUD ở chế độ đó.

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| bản tạm category-jam `Scripts/Entities/Cache/BoosterFlight.cs` | toàn bộ cơ chế, nguyên văn biểu thức | `fallHeight > 0` làm công tắc hình bay; comment "huỷ thì await vẫn xong bình thường" sai |
| category-jam `CacheTileSlots.FlyTrayAsync`, `FlyPackAsync` | sáu dòng quy đổi world → màn hình | — |
| `Runtime/Utilities/UI/FlyRewardItem.cs` | khuôn component UI bay, `[Splitter]`, chỗ đặt | khác lý do thay đổi (trên) |
| `TransformExtensions.CharmJump` | dùng nguyên cho hình `Arc` (cung vồng theo +Y) | — |
| `HopCurve.ApexT`, `ArcY` (Plan HopCurve GĐ1) | hình `DropIn`: lên đỉnh cao hơn đích rồi rơi `fallDuration` giây cuối | — |
| `CharmFlyArc` (và overload `Transform` của Plan CharmFlyArcFollow) | — | bezier vồng theo pháp tuyến dây cung, đổi phía theo hướng bay; cảm giác đã chơi thử là cung `CharmJump` vồng lên |
| `Horcrux_ScreenSize_Plan.md` | cùng khái niệm "bề ngang trên màn hình"; chỗ cho `WorldWidthOnScreen` | hàm của Plan đó đo rect trên canvas; ở đây đo đoạn world, một người dùng |

## Luồng

```
caller: await flight.FlyAsync(nútScreen, worldCamera, chỗĐápWorld, bềNgangWorld, ct); rồi bật vật thật
UIProxyFlight.FlyAsync:
    toScreen    = camera.WorldToScreenPoint(chỗĐáp) ; toWidth = hiệu x màn hình của hai mép           (GĐ1)
    endWidth    = toWidth / scaleFactor ; startWidth = startScale · endWidth
    bật visual tại nútScreen, OnFlightStarted()                        ── lớp con: đuôi Play
    Sequence: cỡ (sizeEase) ‖ nghiêng sin(πt) ‖ đường bay theo path:
        Arc    → visual.CharmJump(toScreen, arcHeight·scaleFactor, duration, moveEase)              (GĐ1)
        DropIn → x lerp tuyến tính, y = HopCurve.ArcY(t, from.y, to.y, fallHeight·scaleFactor, apexT) (GĐ2)
    finally: OnFlightEnded(), xoay về 0, tắt visual                     ── lớp con: đuôi Stop + Clear
```

## Bản đồ hệ cuối

| Thành phần | Ai gọi | Việc |
|---|---|---|
| `UIProxyFlight.FlyAsync` | caller dự án (category-jam: `CacheTileSlots.FlyTrayAsync`, `FlyPackAsync`) | toàn chuyến bay; trả khi bản sao đã tắt |
| `OnFlightStarted`, `OnFlightEnded` (`protected virtual`) | `FlyAsync` | chỗ lớp con bật, dọn thứ đi kèm |
| `UIProxyFlightPath` | Inspector | `Arc` · `DropIn` |
| lớp con dự án (category-jam: `BoosterFlight : UIProxyFlight`) | scene | ô `trail` (`UIParticle`), ghi đè hai hook |

## GĐ1 — bay hình `Arc` tới đích world

**Mục tiêu:** bản sao bay vồng từ một điểm màn hình tới chỗ một vật world, tới nơi đúng bề ngang vật trên màn hình, nghiêng theo hướng
ngang, dọn ở mọi đường ra. Xong GĐ thì một hình bay đủ cho người dùng thật (bình Pack của category-jam).

**Vì sao bây giờ:** gốc của luồng; GĐ2 chỉ thêm một nhánh vào `BuildPath`.

**Sơ đồ sau GĐ1** (`+` là phần mới)

```
  test / caller ──FlyAsync+──► UIProxyFlight+ ──BuildPath+──► TransformExtensions.CharmJump
                                   ├─ Tween.Custom (SetWidth+, SetTilt+)
                                   └─ OnFlightStarted+ / OnFlightEnded+ ──► lớp con
```

**Ai gọi:** test PlayMode của GĐ; trong dự án `CacheTileSlots.FlyPackAsync` (GĐ4).

### `Runtime/Utilities/UI/UIProxyFlight.cs` — file mới

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Utilities.ExtensionMethods;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Utilities.UI
{
    public enum UIProxyFlightPath
    {
        /// <summary>Cung CharmJump cao arcHeight trên đường thẳng, nhịp theo moveEase.</summary>
        Arc = 1,
    }

    /// <summary>
    /// Bản sao UI bay từ một điểm màn hình tới chỗ một vật world, tới nơi đúng bề ngang vật trên màn hình; caller bật vật thật
    /// khi await xong. Canvas gốc phải là Screen Space Overlay: vị trí RectTransform là pixel màn hình.
    /// </summary>
    public class UIProxyFlight : MonoBehaviour
    {
        #region Properties

        [Splitter("References")]
        [Tooltip("Canvas lồng của bản sao (Override Sorting, order trên nút); scaleFactor đọc ở canvas gốc.")]
        [SerializeField] private Canvas canvas;
        [Tooltip("Root của bản sao, anchor và pivot giữa; con stretch theo nó. Tắt lúc nghỉ.")]
        [SerializeField] private RectTransform visual;
        [Tooltip("Image có sprite đo tỉ lệ cao/ngang của bản sao.")]
        [SerializeField] private Image aspectImage;

        [Splitter("Configs")]
        [SerializeField] private UIProxyFlightPath path = UIProxyFlightPath.Arc;
        [Tooltip("Số giây bay từ điểm xuất phát tới đích.")]
        [SerializeField, Min(0.05f)] private float duration = 0.5f;
        [Tooltip("Path Arc: độ vồng trên đường thẳng, đơn vị canvas tham chiếu.")]
        [SerializeField, Min(0f)] private float arcHeight = 160f;
        [Tooltip("Bề ngang bản sao lúc rời điểm xuất phát so với đích.")]
        [SerializeField, Range(0.1f, 1f)] private float startScale = 0.6f;
        [Tooltip("Path Arc: ease của đường bay. Kiểu In: tăng tốc tới cuối, vật đập vào đích; InOutSine: đáp nhẹ.")]
        [SerializeField] private Ease moveEase = Ease.InOutSine;
        [SerializeField] private Ease sizeEase = Ease.OutQuad;
        [Tooltip("Góc nghiêng lớn nhất về hướng bay ngang, ở giữa chuyến; về 0 lúc đáp. 0 là không nghiêng.")]
        [SerializeField, Range(0f, 12f)] private float maxTiltDegrees;

        private float _aspect;
        private float _tiltSign;

        #endregion

        #region Unity Callbacks

        private void Awake() => visual.gameObject.SetActive(false);

        #endregion

        #region API

        /// <summary>
        /// Bay tới chỗ toWorld, tới nơi bề ngang bằng toWorldWidth trên màn hình. Đích chốt lúc gọi. Xong hay huỷ đều tắt bản sao;
        /// huỷ thì ném OperationCanceledException sau khi đã dọn.
        /// </summary>
        public async UniTask FlyAsync(Vector2 fromScreen, Camera worldCamera, Vector3 toWorld, float toWorldWidth, CancellationToken ct)
        {
            Vector2 toScreen = worldCamera.WorldToScreenPoint(toWorld);
            var halfWidth = 0.5f * toWorldWidth * Vector3.right;
            var toScreenWidth = worldCamera.WorldToScreenPoint(toWorld + halfWidth).x
                                - worldCamera.WorldToScreenPoint(toWorld - halfWidth).x;

            var scaleFactor = canvas.rootCanvas.scaleFactor;
            var spriteRect = aspectImage.sprite.rect;
            _aspect = spriteRect.height / spriteRect.width;
            // Bay sang phải thì đỉnh nghiêng sang phải: góc âm (theo chiều kim đồng hồ).
            _tiltSign = toScreen.x >= fromScreen.x ? -1f : 1f;
            var endWidth = toScreenWidth / scaleFactor;
            var startWidth = startScale * endWidth;

            visual.position = fromScreen;
            visual.localRotation = Quaternion.identity;
            SetWidth(startWidth);
            visual.gameObject.SetActive(true);
            OnFlightStarted();

            try
            {
                var sequence = Sequence.Create()
                    .Group(Tween.Custom(this, startWidth, endWidth, duration,
                        static (flight, width) => flight.SetWidth(width), sizeEase))
                    .Group(Tween.Custom(this, 0f, 1f, duration, static (flight, t) => flight.SetTilt(t), Ease.Linear))
                    .Group(BuildPath(fromScreen, toScreen, scaleFactor));
                await sequence.SetCancellationToken(ct);
            }
            finally
            {
                OnFlightEnded();
                visual.localRotation = Quaternion.identity;
                visual.gameObject.SetActive(false);
            }
        }

        #endregion

        #region Class Methods

        /// <summary>Bản sao vừa bật ở điểm xuất phát; lớp con bật thứ đi kèm (đuôi hạt).</summary>
        protected virtual void OnFlightStarted() { }

        /// <summary>Chuyến bay xong hoặc bị huỷ, trước khi tắt bản sao; lớp con dọn thứ đi kèm.</summary>
        protected virtual void OnFlightEnded() { }

        private Sequence BuildPath(Vector2 fromScreen, Vector2 toScreen, float scaleFactor)
        {
            if (path != UIProxyFlightPath.Arc)
                Debug.LogError($"[UIProxyFlight]: path {path} chưa chọn, bay Arc", this);
            return visual.CharmJump(toScreen, arcHeight * scaleFactor, duration, moveEase);
        }

        // Cỡ đặt bằng sizeDelta, không scale transform: con giữ cỡ riêng (đuôi hạt) không co giãn theo bản sao.
        private void SetWidth(float width) => visual.sizeDelta = new Vector2(width, width * _aspect);

        // Nửa chu kỳ sin: 0 lúc rời điểm xuất phát và lúc đáp, lớn nhất giữa đường.
        private void SetTilt(float t) => visual.localRotation = Quaternion.Euler(0f, 0f, _tiltSign * maxTiltDegrees * Mathf.Sin(Mathf.PI * t));

        #endregion
    }
}
```

**Toán → code.**
- Bề ngang trên màn hình là hiệu `x` màn hình của hai mép đích, `toWorld ± ½·toWorldWidth·x̂`. Camera ortho nên hiệu này không phụ thuộc
  độ sâu.
- Đổi sang đơn vị canvas bằng cách chia `scaleFactor` của canvas gốc, vì `sizeDelta` tính theo canvas tham chiếu. Phép chia này và
  `arcHeight · scaleFactor` đúng khi mọi cha của `visual` dưới canvas gốc có scale 1 (Editor setup bước 2).
- Nghiêng theo `θ(t) = s·θmax·sin(πt)`: bằng 0 ở `t` = 0 và 1, đạt `θmax` ở `t = 0.5`. `s = −1` khi bay sang phải (Unity quay dương ngược
  chiều kim đồng hồ).

| Consumes | Produces |
|---|---|
| `TransformExtensions.CharmJump`, `Tween.Custom`, `Sequence`, `Camera.WorldToScreenPoint`, `Canvas.rootCanvas.scaleFactor` | `UIProxyFlight.FlyAsync`, `OnFlightStarted`, `OnFlightEnded`, `UIProxyFlightPath.Arc` |

**Quyết định, phương án bỏ, vì sao:**
- **`BuildPath` tách hàm ngay từ GĐ này:** GĐ2 chỉ thêm một nhánh vào đây.
- **`LogError` cho ô chưa chọn, không ném:** cấu hình sai lúc setup mà không nổ thì ghi log rồi chạy tiếp; bản sao vẫn bay.
- **Không `try/catch` quanh `await`:** huỷ là hợp đồng của caller. Phương án bỏ: nuốt OCE rồi trả bình thường — caller phải tự kiểm
  token, quên là bật vật thật trên level đã dọn.
- **Quy đổi bằng `/ scaleFactor`, không qua ma trận cha của `visual`:** chép nguyên bản đã chơi thử; điều kiện scale 1 ghi ở Editor setup.

**Editor setup** (dựng từ 0):
1. Dưới canvas HUD **Screen Space – Overlay**, tạo `ui_proxy_flight` có `Canvas` (Override Sorting, order trên nút xuất phát) + `UIProxyFlight`.
   Thiếu Override Sorting thì bản sao chui dưới nút.
2. Con `visual`: `RectTransform` anchor và pivot giữa, chứa `Image` của vật; con của `visual` stretch theo nó. **Mọi cha của `visual` dưới
   canvas gốc giữ scale 1.** Cha có scale khác 1 thì bản sao tới nơi sai cỡ (lệch đúng bằng scale đó) và độ vồng sai theo cùng tỉ lệ,
   không log nào báo.
3. Kéo `visual` vào ô **Visual**, Image vào **Aspect Image**, Canvas lồng vào **Canvas**. Thiếu Aspect Image (sprite null) thì nổ
   `NullReferenceException` ở lần bay đầu.
4. **Path** = `Arc`.

**Kiểm:** lệnh kiểm GĐ1–GĐ3 ở đầu Plan. Test PlayMode `Tests/PlayMode/UIProxyFlightTests.cs` (agent viết sau khi đọc code thật). Dựng:
canvas Overlay với `CanvasScaler` Constant Pixel Size (`scaleFactor` 1), camera ortho size 5, lớp con test đếm hook và chụp `sizeDelta`
trong `OnFlightEnded`. Cỡ màn trong PlayMode là cỡ Game view, nên kỳ vọng tính từ `Screen.height`.

| Input | Kỳ vọng |
|---|---|
| bay `duration` 0.1 tới vật world rộng 1 unit | `OnFlightEnded` thấy `sizeDelta.x` = `Screen.height / (2·5)` (±0.5) · `visual` tắt · xoay identity |
| cùng chuyến, đếm hook | `OnFlightStarted` 1 · `OnFlightEnded` 1 |
| huỷ token giữa chuyến | ném `OperationCanceledException` · `OnFlightEnded` 1 · `visual` tắt · xoay identity |
| token đã huỷ trước khi gọi | ném `OperationCanceledException` · `OnFlightEnded` 1 · `visual` tắt |
| bay sang phải, `maxTiltDegrees` 10, `duration` 0.4, chụp góc sau 0.2 s | `localEulerAngles.z` ∈ [348, 352] (−10° ± 2°, lệch một frame); bay sang trái ∈ [8, 12] — đỏ nếu đảo dấu |
| `path` = `(UIProxyFlightPath)0` | một `LogError` (`LogAssert.Expect`), vẫn bay xong, đáp đúng đích ±0.5 px |

**Mốc commit:** `feat(ui): UIProxyFlight bay hình Arc tới đích world`.

## GĐ2 — hình `DropIn`

**Mục tiêu:** bản sao bay như vật rơi vào ô: lên đỉnh cao hơn đích rồi rơi vào trong `fallDuration` giây cuối. Xong GĐ thì đủ hai hình
bay cho cả hai người dùng của category-jam.

**Vì sao bây giờ:** hình `Arc` đã chạy, GĐ này chỉ thêm một nhánh; cần `HopCurve` GĐ1 đã gõ.

**Sơ đồ sau GĐ2** (`+` là phần mới)

```
  FlyAsync ──BuildPath──┬─ Arc     ──► CharmJump
                        └─ DropIn+ ──► Tween.Custom ──SetDropIn+──► HopCurve.ApexT, HopCurve.ArcY
```

**Ai gọi:** test của GĐ; trong dự án `CacheTileSlots.FlyTrayAsync` (GĐ4).

### `UIProxyFlight.cs` › đầu file › sau `using Horcrux.Runtime.Utilities.ExtensionMethods;`

```csharp
using Horcrux.Runtime.Utilities.PhysXHelper;
```

### › `UIProxyFlightPath` › sau `Arc = 1,`

Quanh chỗ thêm:

```csharp
        /// <summary>Cung CharmJump cao arcHeight trên đường thẳng, nhịp theo moveEase.</summary>
        Arc = 1,
    }
```

Đoạn mới, trước `}` đóng enum:

```csharp
        /// <summary>Lên đỉnh cao hơn đích fallHeight rồi rơi vào trong fallDuration giây cuối, gốc đi tuyến tính.</summary>
        DropIn = 2,
```

### › region `Properties` › sau `moveEase`

Quanh chỗ thêm:

```csharp
        [SerializeField] private Ease moveEase = Ease.InOutSine;
        [SerializeField] private Ease sizeEase = Ease.OutQuad;
```

Đoạn mới giữa hai dòng trên:

```csharp
        [Tooltip("Path DropIn: đỉnh cao hơn đích bao nhiêu, đơn vị canvas tham chiếu.")]
        [SerializeField, Min(0f)] private float fallHeight;
        [Tooltip("Path DropIn: số giây rơi từ đỉnh vào đích; đỉnh không sớm hơn nửa chuyến.")]
        [SerializeField, Min(0.02f)] private float fallDuration = 0.15f;
```

### › region `Properties` › sau `private float _tiltSign;`

```csharp
        private Vector2 _from;
        private Vector2 _to;
        private float _fallHeightPx;
        private float _apexT;
```

### › region `Class Methods` › `BuildPath` — thân mới

Neo, đầu thân hiện có:

```csharp
        private Sequence BuildPath(Vector2 fromScreen, Vector2 toScreen, float scaleFactor)
        {
            if (path != UIProxyFlightPath.Arc)
```

Đoạn mới chèn ngay sau `{`, trước `if (path != UIProxyFlightPath.Arc)`:

```csharp
            if (path == UIProxyFlightPath.DropIn)
            {
                _from = fromScreen;
                _to = toScreen;
                _fallHeightPx = fallHeight * scaleFactor;
                _apexT = HopCurve.ApexT(duration, fallDuration);
                // t tuyến tính: đường đất đều, pha rơi của ArcY tự cho cú tăng tốc lúc đáp.
                return Sequence.Create().Group(Tween.Custom(this, 0f, 1f, duration, static (flight, t) => flight.SetDropIn(t), Ease.Linear));
            }

```

### › region `Class Methods` › sau `SetWidth`

```csharp

        private void SetDropIn(float t)
        {
            var x = Mathf.LerpUnclamped(_from.x, _to.x, t);
            var y = HopCurve.ArcY(t, _from.y, _to.y, _fallHeightPx, _apexT);
            visual.position = new Vector3(x, y, visual.position.z);
        }
```

**Toán → code:** dùng nguyên `ArcY` và `ApexT` của Plan HopCurve §0. `fallHeight` nhân `scaleFactor` vì `y` tính bằng pixel màn hình;
`ArcY` nhận cùng đơn vị với `y0`, `y1` caller truyền.

| Consumes | Produces |
|---|---|
| `HopCurve.ApexT`, `HopCurve.ArcY` (Plan HopCurve GĐ1) | `UIProxyFlightPath.DropIn`, field `fallHeight`, `fallDuration` |

**Quyết định:**
- **`Sequence.Create().Group(...)` bọc tween DropIn:** hai nhánh cùng trả `Sequence`, nên `FlyAsync` ghép bằng một `Group`.
- **Ba số của chuyến DropIn giữ trong field, không closure:** lambda `static` không bắt được biến ngoài; field cho 0 B mỗi chuyến.
- **`fallHeight` mặc định 0:** con số đã chỉnh là của từng scene; instance mới chọn `DropIn` thì tự đặt độ cao. Phương án bỏ: mặc định
  150 của bản tạm — số cảm giác của một dự án.

**Editor setup:** instance hình rơi đặt **Path** = `DropIn`, chỉnh **Fall Height** (0 là không lên đỉnh, bản sao trượt thẳng vào đích),
**Fall Duration**.

**Kiểm:** lệnh kiểm GĐ1–GĐ3; thêm vào `UIProxyFlightTests`:

| Input | Kỳ vọng |
|---|---|
| `DropIn`, `fallHeight` 150, `duration` 0.5, `fallDuration` 0.15, cùng độ cao hai đầu | `y` lớn nhất ≈ đích + 150 (±3 px, lấy mẫu theo frame) tại `t ≈ 0.7` (`ApexT` = max(0.5, 1 − 0.15/0.5)) · kết thúc đúng đích ±0.5 px |
| `DropIn`, `fallHeight` 0, cùng độ cao hai đầu | `y` không vượt đích ở mọi frame (đỏ nếu nhánh vẫn đi `CharmJump` với `arcHeight` 160) |
| `DropIn` huỷ giữa chuyến | như case huỷ GĐ1 |

**Mốc commit:** `feat(ui): UIProxyFlight hình DropIn`.

## GĐ3 — tài liệu (agent)

**Mục tiêu:** người dùng hệ dựng được instance và tránh được bẫy mà không đọc code.

`docs/Runtime/UIProxyFlight/UIProxyFlight.md` mở đầu `Code: Runtime/Utilities/UI/UIProxyFlight.cs`, và `.html` cùng thư mục.
- **`.md`:** luồng (như trên) · bảng API (`FlyAsync`, hai hook, `UIProxyFlightPath`, field) · "Trước khi chạy" (Editor setup GĐ1–2) ·
  "Design decisions" theo Ngữ cảnh đã chốt (đích chốt lúc gọi, enum từ 1, huỷ ném OCE, cỡ bằng `sizeDelta`).
- **Bẫy:** canvas gốc không phải Overlay · cha của `visual` có scale khác 1 · thiếu Override Sorting · `path` = 0 · huỷ ném OCE · lớp con
  quên dọn đuôi trong `OnFlightEnded` (vệt từ chuyến trước) · đích trượt trong lúc bay (bản sao đáp chỗ cũ).
- **`.html`:** trang cho developer và designer: luồng, bảng field, Trước khi chạy, bẫy; đồng bộ theo `.md`.

**Editor setup:** không. **Kiểm:** hai file có đủ mục trên; tên API khớp code đã gõ.

**Mốc commit:** `docs(ui): UIProxyFlight.md, .html`.

## GĐ4 — category-jam dùng bản Horcrux, xoá Plan (agent)

**Mục tiêu:** lớp `BoosterFlight` chỉ còn lớp nối với đuôi hạt; hai caller gọi một dòng.

**Vì sao bây giờ:** đủ hai hình bay và tài liệu.

### `Assets/_CategoryJam/Scripts/Entities/Cache/BoosterFlight.cs` — viết lại, giữ file và `.meta` (GUID scene đang trỏ)

```csharp
using Coffee.UIExtensions;
using Horcrux.Runtime.Utilities.UI;
using UnityEngine;

namespace CategoryJam.Gameplay.Entities
{
    /// <summary>Bản sao UI của vật booster (khay Extra Slot, bình Pack), kéo đuôi ribbon trắng: UIParticle ngoài stack Horcrux.</summary>
    public class BoosterFlight : UIProxyFlight
    {
        [Tooltip("Đuôi ribbon trắng và bling, con của visual.")]
        [SerializeField] private UIParticle trail;

        protected override void OnFlightStarted() => trail.Play();

        protected override void OnFlightEnded()
        {
            trail.Stop();
            trail.Clear();
        }
    }
}
```

### `Assets/_CategoryJam/Scripts/Entities/Cache/CacheTileSlots.cs`

| Chỗ | Cũ | Mới |
|---|---|---|
| `FlyTrayAsync` | `var worldCamera = Director.WorldCamera;` · dòng `landing` · `halfWidth`, `screenWidth` (ba dòng) · `return _extraSlotFlight.FlyAsync(launchScreenPoint, worldCamera.WorldToScreenPoint(landing), screenWidth, ct);` | giữ `var landing = transform.TransformPoint(_horizontalGrid.LocalPositionOf(openCount - 1, openCount));` rồi `return _extraSlotFlight.FlyAsync(launchScreenPoint, Director.WorldCamera, landing, slot.RestWorldWidth, ct);` |
| `FlyPackAsync` | như trên với `_packBooster` | `return _packFlight.FlyAsync(launchScreenPoint, Director.WorldCamera, _packBooster.transform.position, _packBooster.RestWorldWidth, ct);` |

`ct.ThrowIfCancellationRequested()` sau hai lời gọi (comment "Lưới an toàn: huỷ giữa lúc bay thì không …") giữ nguyên: `FlyAsync` đã ném
khi huỷ, dòng đó vô hại và vẫn che nhánh `WhenAll` của `OpenOneMoreSlotAsync`.

### `Assets/_CategoryJam/Scenes/Game.unity` (agent sửa text, rồi đọc lại trên object đã dựng bằng eval)

| Instance | Có sẵn | Đổi |
|---|---|---|
| `extra_slot_flight` | `fallHeight: 150`, `fallDuration: 0.15` | thêm `path: 2` |
| `pack_flight` | không có key `fallHeight`, `fallDuration` | thêm `path: 1` |

Các key còn lại (`canvas`, `visual`, `aspectImage`, `trail`, `duration`, `arcHeight`, `startScale`, `moveEase`, `sizeEase`, `maxTiltDegrees`)
trùng tên field, không đổi. Lần lưu đầu Unity ghi cho `pack_flight` `fallHeight: 0`, `fallDuration: 0.15` (mặc định) — vô hại vì `path`
là `Arc`, và trùng giá trị bản tạm đang dùng.

### `Assets/_CategoryJam/Scripts/Feel.md`

| Chỗ | Đổi |
|---|---|
| §10, hàng núm `extra_slot_flight` | `fallHeight > 0: cùng hình dạng …` → `path = DropIn: cùng hình dạng …`; `fallHeight 0 thì về cung CharmJump …` → `path = Arc: cung CharmJump theo arcHeight, moveEase — pack_flight bay kiểu này`; thêm `path` vào cột Núm; tên hàm đường cong theo bản đang có (xem Chung file) |
| §11, hàng núm `pack_flight` | thêm `path` (`Arc`) vào cột Núm |
| Trước khi chạy, hàng `extra_slot_flight` | thêm "`path` = `DropIn`" vào cột thiết lập, "khay bay vồng thay vì rơi" vào cột hỏng |

Xong thì **xoá Plan này** cùng `.meta`.

**Editor setup:** không (YAML sửa bằng text ở trên).

**Kiểm:**
- `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,CategoryJam.Gameplay.csproj --tests
  CategoryJam.EditModeTests,Horcrux.EditModeTests` xanh; grep `TileFlightCurve` trong `BoosterFlight.cs` = 0.
- Eval trên scene đã dựng: `extra_slot_flight.path == DropIn` · `pack_flight.path == Arc` · `trail` không null ở cả hai.
- **Chơi thử** (developer): vào một level có thanh cache. Bấm Extra Slot: khay lên đỉnh trên ô mới rồi rơi vào, ô world hiện đúng chỗ và
  cỡ khay. Bấm Pack khi thanh có tile: bình bay vồng, nghiêng nhẹ giữa đường, đáp đúng chỗ và cỡ bình thật. Khác trước: không gì (cảm giác
  giữ nguyên). Dấu hiệu hỏng: bản sao giật cỡ một frame lúc nhường vật thật · khay bay vồng thay vì rơi (`path` sai) · đuôi kéo vệt từ
  chuyến trước · log `UIProxyFlight` đỏ (`path` chưa chọn).

**Mốc commit:** `refactor(booster): BoosterFlight dựng trên UIProxyFlight của Horcrux` ở dự án kèm con trỏ submodule; xoá Plan trong
`Assets/Horcrux` cùng con trỏ đó.

## File → GĐ

| File | GĐ |
|---|---|
| `Runtime/Utilities/UI/UIProxyFlight.cs` | 1, 2 |
| `Tests/PlayMode/UIProxyFlightTests.cs` (agent) | 1, 2 |
| `docs/Runtime/UIProxyFlight/UIProxyFlight.md`, `.html` (agent) | 3 |
| category-jam `BoosterFlight.cs`, `CacheTileSlots.cs`, `Game.unity`, `Feel.md`; xoá Plan này | 4 |
