# Plan — `UIProxyFlight`: bản sao UI bay tới vật world rồi nhường chỗ

Bản tạm: category-jam `Scripts/Entities/Cache/BoosterFlight.cs` › `FlyAsync`; instance `extra_slot_flight`, `pack_flight` trong `Game.unity`; caller `CacheTileSlots.FlyTrayAsync`, `FlyPackAsync`. GĐ1 gõ được sớm; GĐ2 sau `Horcrux_HopCurve_DipFlash_Plan.md` GĐ1 (`HopCurve.ApexT`, `ArcY`). Chung `BoosterFlight.cs`, `Scripts/Feel.md` §10 với Plan HopCurve › Chuyển dự án: Plan này chuyển trước thì mục `BoosterFlight.cs` bên kia bỏ; hàng §10 viết một lần, theo tên hàm đang có (`HopCurve.ArcY` hay `TileFlightCurve.ArcY`).
Kiểm: `bash "<horcrux>/Editor/CompileCheck/Tools~/verify.sh" --csproj com.horcrux.runtime.csproj,Horcrux.PlayModeTests.csproj --tests Horcrux.EditModeTests`; PlayMode chạy ở Window › General › Test Runner.

## Kịch bản

| Tình huống | Hệ làm gì | Caller thấy |
|---|---|---|
| caller `await FlyAsync(nútScreen, worldCamera, chỗĐápWorld, bềNgangWorld, ct)` | quy chỗ đáp và bề ngang ra màn hình một lần; bật bản sao ở nút, cỡ `startScale ×` cỡ đích | bản sao bay tới chỗ vật, tới nơi đúng bề ngang vật trên màn hình; sau `await` caller bật vật thật cùng frame |
| `path` = `Arc` | cung `CharmJump` cao `arcHeight·scaleFactor`, nhịp `moveEase` | bay vồng lên |
| `path` = `DropIn` | x tuyến tính, y theo `HopCurve.ArcY`: lên đỉnh cao hơn đích `fallHeight`, rơi trong `fallDuration` giây cuối | như vật rơi vào ô |
| `DropIn`, `fallHeight` 0 | không lên đỉnh | trượt thẳng vào đích |
| `maxTiltDegrees` > 0 | nghiêng theo hướng bay ngang, lớn nhất giữa chuyến | nghiêng nhẹ giữa đường, thẳng lúc đáp |
| `path` = 0 (ô chưa chọn) | `LogError`, bay `Arc` | Console đỏ, người chơi không kẹt ở nút |
| huỷ token giữa chuyến hay trước khi gọi | `finally`: `OnFlightEnded`, xoay về 0, tắt bản sao; rồi ném `OperationCanceledException` | caller nhận OCE, không bật vật thật |
| lớp con ghi đè hook | `OnFlightStarted` lúc bản sao bật, `OnFlightEnded` trước khi tắt | đuôi hạt bật mỗi chuyến, dọn sạch, không vệt từ chuyến trước |
| đích trượt trong lúc bay | đích chốt lúc gọi, không đọc lại | bản sao đáp chỗ cũ — ngoài hợp đồng |
| canvas gốc không Overlay · cha của `visual` scale ≠ 1 | không kiểm | sai chỗ, sai cỡ, không log — ngoài hợp đồng |

## Flow

```
caller (dự án): await flight.FlyAsync(nútScreen, worldCamera, chỗĐápWorld, bềNgangWorld, ct); rồi bật vật thật
   ▼
UIProxyFlight.FlyAsync                                            Runtime/Utilities/UI/UIProxyFlight.cs
   ├─ toScreen = camera.WorldToScreenPoint(chỗĐáp) · toWidth = hiệu x màn hình hai mép
   ├─ endWidth = toWidth / scaleFactor · startWidth = startScale · endWidth
   ├─ bật visual tại nútScreen ──► OnFlightStarted()  ──► lớp con: đuôi Play
   ├─ Sequence: SetWidth (sizeEase) ‖ SetTilt sin(πt) ‖ BuildPath theo path:
   │     Arc    ──► visual.CharmJump(toScreen, arcHeight·scaleFactor, duration, moveEase)    TransformExtensions
   │     DropIn ──► SetDropIn: x lerp, y = HopCurve.ArcY(t, from.y, to.y, fallHeight·scaleFactor, apexT)
   │                                                               Runtime/Utilities/PhysXHelper/HopCurve.cs
   └─ finally: OnFlightEnded() ──► lớp con: đuôi Stop + Clear · xoay về 0 · tắt visual
lớp con dự án : UIProxyFlight (scene) ── ô trail (UIParticle), ghi đè hai hook
Inspector: path ← UIProxyFlightPath { Arc = 1, DropIn = 2 }
```

## Thuật toán

| Đại lượng | Công thức | Điều kiện |
|---|---|---|
| bề ngang đích trên màn hình | `screen(toWorld + ½·w·x̂).x − screen(toWorld − ½·w·x̂).x` | camera ortho: không phụ thuộc độ sâu |
| cỡ canvas | pixel `/ scaleFactor` của canvas gốc; `arcHeight`, `fallHeight` `· scaleFactor` | `sizeDelta` theo canvas tham chiếu, `y` bay theo pixel; mọi cha của `visual` dưới canvas gốc scale 1 |
| nghiêng | `θ(t) = s·θmax·sin(πt)`, `s = −1` khi bay sang phải | 0 ở `t` = 0 và 1, `θmax` ở `t` = 0.5; Unity quay dương ngược chiều kim đồng hồ |
| `DropIn` | `ApexT`, `ArcY` nguyên văn của `Horcrux_HopCurve_DipFlash_Plan.md` › Thuật toán; `t` tuyến tính | `ArcY` nhận cùng đơn vị với `y0`, `y1` |

Bản mẫu: mọi biểu thức chép nguyên bản tạm đã chơi thử cả hai booster · huỷ đọc từ PrimeTween `AsyncAwaitSupport`: `GetResult` ném OCE khi token đã huỷ, kể cả huỷ trước lúc `await`.

---

## GĐ1 — xong thì bản sao bay hình `Arc` tới đích world (đủ cho bình Pack)

```
  test / caller ──FlyAsync+──► UIProxyFlight+ ──BuildPath+──► TransformExtensions.CharmJump
                                   ├─ Tween.Custom (SetWidth+, SetTilt+)
                                   └─ OnFlightStarted+ / OnFlightEnded+ ──► lớp con
```

`Runtime/Utilities/UI/UIProxyFlight.cs` — file mới:

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Utilities.ExtensionMethods;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Utilities.UI
{
    // Từ 1: 0 là ô chưa chọn. Ô số kiêm công tắc (fallHeight > 0) thì đặt 0 đổi hình bay không gì báo.
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
            // Quy đổi một lần: đích là chỗ nghỉ cuối của vật, camera không đổi suốt chuyến.
            Vector2 toScreen = worldCamera.WorldToScreenPoint(toWorld);
            var halfWidth = 0.5f * toWorldWidth * Vector3.right;
            var toScreenWidth = worldCamera.WorldToScreenPoint(toWorld + halfWidth).x
                                - worldCamera.WorldToScreenPoint(toWorld - halfWidth).x;

            // Chia scaleFactor, không qua ma trận cha của visual: đúng khi mọi cha có scale 1 (Editor setup bước 2).
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

            // Không catch: nuốt OCE thì caller quên kiểm token là bật vật thật trên level đã dọn.
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
                // Log, không ném: setup sai vẫn bay, người chơi không kẹt ở nút.
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

**Editor setup** (dựng từ 0):

1. Dưới canvas HUD **Screen Space – Overlay**, tạo `ui_proxy_flight` có `Canvas` (Override Sorting, order trên nút xuất phát) + `UIProxyFlight`. Thiếu Override Sorting thì bản sao chui dưới nút.
2. Con `visual`: `RectTransform` anchor và pivot giữa, chứa `Image` của vật; con của `visual` stretch theo nó. **Mọi cha của `visual` dưới canvas gốc giữ scale 1** — khác 1 thì bản sao tới nơi sai cỡ (lệch đúng bằng scale đó), độ vồng sai cùng tỉ lệ, không log nào báo.
3. Kéo `visual` vào ô **Visual**, Image vào **Aspect Image**, Canvas lồng vào **Canvas**. Thiếu Aspect Image (sprite null) thì `NullReferenceException` ở lần bay đầu.
4. **Path** = `Arc`.

`Tests/PlayMode/UIProxyFlightTests.cs` (agent, viết sau khi đọc code thật) — canvas Overlay, `CanvasScaler` Constant Pixel Size (`scaleFactor` 1), camera ortho size 5, lớp con test đếm hook và chụp `sizeDelta` trong `OnFlightEnded`; cỡ màn PlayMode là cỡ Game view, kỳ vọng tính từ `Screen.height`:

| Input | Kỳ vọng |
|---|---|
| bay `duration` 0.1 tới vật world rộng 1 unit | `OnFlightEnded` thấy `sizeDelta.x` = `Screen.height / (2·5)` (±0.5) · `visual` tắt · xoay identity |
| cùng chuyến, đếm hook | `OnFlightStarted` 1 · `OnFlightEnded` 1 |
| huỷ token giữa chuyến | ném `OperationCanceledException` · `OnFlightEnded` 1 · `visual` tắt · xoay identity |
| token đã huỷ trước khi gọi | ném `OperationCanceledException` · `OnFlightEnded` 1 · `visual` tắt |
| bay sang phải, `maxTiltDegrees` 10, `duration` 0.4, chụp góc sau 0.2 s | `localEulerAngles.z` ∈ [348, 352] (−10° ± 2°, lệch một frame); bay sang trái ∈ [8, 12] — đỏ nếu đảo dấu |
| `path` = `(UIProxyFlightPath)0` | một `LogError` (`LogAssert.Expect`), vẫn bay xong, đáp đúng đích ±0.5 px |

---

## GĐ2 — xong thì có hình `DropIn`: đủ hai hình bay cho cả hai người dùng

```
  FlyAsync ──BuildPath──┬─ Arc     ──► CharmJump
                        └─ DropIn+ ──► Tween.Custom ──SetDropIn+──► HopCurve.ApexT, HopCurve.ArcY
```

`UIProxyFlight.cs` › đầu file › sau `using Horcrux.Runtime.Utilities.ExtensionMethods;`:

```csharp
using Horcrux.Runtime.Utilities.PhysXHelper;
```

`UIProxyFlight.cs` › `UIProxyFlightPath` › sau `Arc = 1,` — quanh chỗ thêm:

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

`UIProxyFlight.cs` › region `Properties` › sau `moveEase` — quanh chỗ thêm:

```csharp
        [SerializeField] private Ease moveEase = Ease.InOutSine;
        [SerializeField] private Ease sizeEase = Ease.OutQuad;
```

Đoạn mới giữa hai dòng trên:

```csharp
        // Mặc định 0: độ cao là số cảm giác của từng scene; instance chọn DropIn tự đặt.
        [Tooltip("Path DropIn: đỉnh cao hơn đích bao nhiêu, đơn vị canvas tham chiếu.")]
        [SerializeField, Min(0f)] private float fallHeight;
        [Tooltip("Path DropIn: số giây rơi từ đỉnh vào đích; đỉnh không sớm hơn nửa chuyến.")]
        [SerializeField, Min(0.02f)] private float fallDuration = 0.15f;
```

`UIProxyFlight.cs` › region `Properties` › sau `private float _tiltSign;`:

```csharp
        // Field, không closure: lambda static không bắt biến ngoài; 0 B mỗi chuyến.
        private Vector2 _from;
        private Vector2 _to;
        private float _fallHeightPx;
        private float _apexT;
```

`UIProxyFlight.cs` › region `Class Methods` › `BuildPath` — neo, đầu thân hiện có:

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
                // Bọc Sequence: hai nhánh cùng kiểu trả, FlyAsync ghép bằng một Group.
                return Sequence.Create().Group(Tween.Custom(this, 0f, 1f, duration, static (flight, t) => flight.SetDropIn(t), Ease.Linear));
            }

```

`UIProxyFlight.cs` › region `Class Methods` › sau `SetWidth`:

```csharp

        private void SetDropIn(float t)
        {
            var x = Mathf.LerpUnclamped(_from.x, _to.x, t);
            var y = HopCurve.ArcY(t, _from.y, _to.y, _fallHeightPx, _apexT);
            visual.position = new Vector3(x, y, visual.position.z);
        }
```

**Editor setup:**

1. Instance hình rơi: **Path** = `DropIn`, chỉnh **Fall Height** (0 là không lên đỉnh, bản sao trượt thẳng vào đích), **Fall Duration**.

`Tests/PlayMode/UIProxyFlightTests.cs` (agent) — thêm:

| Input | Kỳ vọng |
|---|---|
| `DropIn`, `fallHeight` 150, `duration` 0.5, `fallDuration` 0.15, cùng độ cao hai đầu | `y` lớn nhất ≈ đích + 150 (±3 px, lấy mẫu theo frame) tại `t ≈ 0.7` (`ApexT` = max(0.5, 1 − 0.15/0.5)) · kết thúc đúng đích ±0.5 px |
| `DropIn`, `fallHeight` 0, cùng độ cao hai đầu | `y` không vượt đích ở mọi frame (đỏ nếu nhánh vẫn đi `CharmJump` với `arcHeight` 160) |
| `DropIn` huỷ giữa chuyến | như case huỷ GĐ1 |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `docs/Runtime/UIProxyFlight/UIProxyFlight.md`, `.html` | tạo: `Code: Runtime/Utilities/UI/UIProxyFlight.cs` · luồng · bảng API (`FlyAsync`, hai hook, `UIProxyFlightPath`, field) · Trước khi chạy (Editor setup GĐ1–2) · Design decisions (đích chốt lúc gọi, enum từ 1, huỷ ném OCE, cỡ bằng `sizeDelta`) · bẫy: canvas gốc không Overlay, cha của `visual` scale ≠ 1, thiếu Override Sorting, `path` = 0, huỷ ném OCE, lớp con quên dọn đuôi trong `OnFlightEnded`, đích trượt trong lúc bay |
| `Scripts/Entities/Cache/BoosterFlight.cs` | viết lại, giữ file và `.meta` (GUID scene trỏ): `BoosterFlight : UIProxyFlight` (namespace `CategoryJam.Gameplay.Entities`, `using Coffee.UIExtensions;`, `using Horcrux.Runtime.Utilities.UI;`) · ô `[SerializeField] UIParticle trail` (Tooltip: đuôi ribbon trắng và bling, con của visual) · `OnFlightStarted` → `trail.Play()` · `OnFlightEnded` → `trail.Stop(); trail.Clear();` |
| `CacheTileSlots.FlyTrayAsync` | bỏ `worldCamera`, `halfWidth`, `screenWidth`; giữ dòng `landing` (`_horizontalGrid` hay `_row` nếu `Horcrux_HorizontalRow_Plan.md` đã chuyển) → `return _extraSlotFlight.FlyAsync(launchScreenPoint, Director.WorldCamera, landing, slot.RestWorldWidth, ct);` |
| `CacheTileSlots.FlyPackAsync` | → `return _packFlight.FlyAsync(launchScreenPoint, Director.WorldCamera, _packBooster.transform.position, _packBooster.RestWorldWidth, ct);` |
| `CacheTileSlots` › `ct.ThrowIfCancellationRequested()` sau hai lời gọi | giữ: vô hại, vẫn che nhánh `WhenAll` của `OpenOneMoreSlotAsync` |
| `Scenes/Game.unity` › `extra_slot_flight` | thêm `path: 2` (`fallHeight: 150`, `fallDuration: 0.15` có sẵn) |
| `Scenes/Game.unity` › `pack_flight` | thêm `path: 1`; lần lưu đầu Unity ghi `fallHeight: 0`, `fallDuration: 0.15` — vô hại vì `Arc` |
| `Game.unity`, key còn lại | `canvas`, `visual`, `aspectImage`, `trail`, `duration`, `arcHeight`, `startScale`, `moveEase`, `sizeEase`, `maxTiltDegrees` trùng tên field, không đổi |
| `Scripts/Feel.md` §10, hàng `extra_slot_flight` | `fallHeight > 0: cùng hình dạng …` → `path = DropIn: cùng hình dạng …`; `fallHeight 0 thì về cung CharmJump …` → `path = Arc: cung CharmJump theo arcHeight, moveEase — pack_flight bay kiểu này`; thêm `path` vào cột Núm; tên hàm đường cong theo bản đang có |
| `Feel.md` §11, hàng `pack_flight` | thêm `path` (`Arc`) vào cột Núm |
| `Feel.md` › Trước khi chạy, hàng `extra_slot_flight` | thêm "`path` = `DropIn`" vào cột thiết lập, "khay bay vồng thay vì rơi" vào cột hỏng |
| kiểm | `verify.sh --csproj com.horcrux.runtime.csproj,CategoryJam.Gameplay.csproj --tests CategoryJam.EditModeTests,Horcrux.EditModeTests` xanh · grep `TileFlightCurve` trong `BoosterFlight.cs` = 0 · eval scene: `extra_slot_flight.path == DropIn`, `pack_flight.path == Arc`, `trail` không null ở cả hai |
| Plan này | xoá cùng `.meta` |
| chơi thử | theo bảng Kịch bản: Extra Slot — khay lên đỉnh trên ô mới rồi rơi vào, ô hiện đúng chỗ và cỡ · Pack — bình bay vồng, nghiêng nhẹ, đáp đúng chỗ và cỡ; hỏng: bản sao giật cỡ một frame lúc nhường vật thật, khay bay vồng (`path` sai), đuôi kéo vệt chuyến trước, log `UIProxyFlight` đỏ |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | gộp với `FlyRewardItem` | khác lý do thay đổi: icon thưởng bay UI→UI, mờ dần, nổ hạt; bản sao này nhường chỗ cho vật world |
| bỏ | đuôi tuỳ chọn trong framework (field `ParticleSystem`) | `UIParticle` ngoài stack Horcrux, không có `ParticleSystem` ở gốc; hook rẻ hơn |
| bỏ | `fallHeight > 0` làm công tắc hình bay như bản tạm | đặt 0 là đổi hình bay mà không gì báo |
| bỏ | `path` chưa chọn thì ném | setup sai vẫn bay, người chơi không kẹt ở nút |
| bỏ | nuốt OCE rồi trả bình thường | caller phải tự kiểm token; quên là bật vật thật trên level đã dọn |
| bỏ | cỡ bằng scale transform | con giữ cỡ riêng (đuôi hạt) co giãn theo bản sao |
| bỏ | quy đổi qua ma trận cha của `visual` | chép nguyên bản đã chơi thử; điều kiện scale 1 ở Editor setup |
| bỏ | `CharmFlyArc` cho hình `Arc` | bezier vồng theo pháp tuyến dây cung, đổi phía theo hướng bay; cảm giác đã chơi thử là cung `CharmJump` vồng lên |
| bỏ | `fallHeight` mặc định 150 như bản tạm | số cảm giác của một dự án |
| sau | overload nhận đích màn hình | khi có caller UI→UI thứ hai cùng hình bay; caller hiện có khác hình (không vồng, không nghiêng, ease In đập vào nút) — thêm hình `Straight`, pha nảy–đứng, cỡ hai đầu qua `LocalScaleForScreenWidth` của `Horcrux_ScreenSize_Plan.md` |
| sau | bám đích: overload nhận `Transform`, quy đổi màn hình mỗi frame (như overload `Transform` của `Horcrux_CharmFlyArcFollow_Plan.md`) | khi có đích trượt trong lúc bay; giờ đích chốt lúc gọi |
| sau | ba dòng quy đổi bề ngang world → pixel thành `WorldWidthOnScreen(this Camera, …)` trong `ScreenSizeExtensions` | khi có người dùng thứ hai |
| sau | canvas Screen Space Camera, World Space | khi có HUD ở chế độ đó; `visual.position = điểm màn hình` chỉ đúng ở Overlay |
