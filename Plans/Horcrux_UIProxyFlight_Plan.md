# Plan — `UIProxyFlight` lên Horcrux: bản sao UI bay tới vật world rồi nhường chỗ

Nguồn: category-jam `Assets/_CategoryJam/Scripts/Entities/Cache/BoosterFlight.cs`, hai instance trong `Game.unity`
(`extra_slot_flight`, `pack_flight`), caller `CacheTileSlots.FlyTrayAsync`, `FlyPackAsync`; đã chơi thử cả hai booster.

**Phân công:** GĐ1–GĐ2 developer gõ (code runtime Horcrux), agent viết test theo bảng case rồi compile · GĐ3 (dự án chuyển sang bản
Horcrux) và GĐ4 (tài liệu, xoá Plan) agent làm. GĐ1, GĐ2, GĐ4 commit trong `Assets/Horcrux`; GĐ3 commit ở dự án; mỗi lần kèm con trỏ
submodule. **Phụ thuộc** `Horcrux_HopCurve_DipFlash_Plan.md` GĐ1 (`HopCurve.ApexT`, `HopCurve.ArcY`): Plan này viết như Plan đó đã gõ.
GĐ10 của Plan đó có hai dòng `BoosterFlight.cs`: làm GĐ3 ở đây trước thì hai dòng đó không còn gì để đổi (code đã lên Horcrux, gọi
`HopCurve` sẵn). Không có `§0`: hai công thức một dòng nằm ở giai đoạn sinh ra chúng.

## Ngữ cảnh đã chốt

- **Người dùng:** nút booster nằm ở Canvas Overlay, vật booster (khay ô cache, bình hút tile) là sprite world — sprite world luôn bị
  canvas Overlay đè, nên vật không bay thẳng từ nút được. Một bản sao UI bay từ nút tới chỗ vật sẽ đứng, đúng cỡ vật trên màn hình, rồi
  caller bật vật thật cùng frame. Dự án sau: món mua từ shop bay vào túi đồ trên bàn chơi · icon phần thưởng bay xuống ô world.
- **Mục tiêu:** cơ chế bay (cỡ theo bề ngang đích trên màn hình, hai hình đường bay, nghiêng theo hướng ngang, dọn ở mọi đường ra) lên
  Horcrux; dự án chỉ còn lớp nối với đuôi hạt. **Cảm giác không đổi:** mọi biểu thức chép nguyên văn; số trên scene giữ nguyên.
- **Chỗ đặt, tên:** `Runtime/Utilities/UI/UIProxyFlight.cs`, namespace `Horcrux.Runtime.Utilities.UI`, cạnh `FlyRewardItem`. Tên nói cơ
  chế (bản sao UI), không nói booster. Đổi tên type tuỳ ý trước khi gõ.
- **Đuôi hạt ở lại dự án qua hai hook `protected virtual`:** đuôi hiện là Coffee `UIParticle`, ngoài stack Horcrux; framework không
  tham chiếu được assembly đó. Dự án giữ `BoosterFlight` thành lớp con ghi đè hai hook — script giữ GUID, scene không phải kéo lại.
- **Hình đường bay là `enum` tường minh từ `1`:** hiện chế độ ẩn trong `fallHeight > 0`; một ô số vừa là núm vừa là công tắc thì
  đặt 0 là đổi cả hình bay mà không gì báo. Giá trị `0` (ô chưa chọn) ghi `LogError` rồi bay hình `Arc`.
- **API nhận đích world:** hai caller đều tự quy chỗ đáp và bề ngang world ra màn hình bằng cùng sáu dòng; quy đổi vào
  `UIProxyFlight`, caller truyền camera, điểm, bề ngang world.
- **Cố ý KHÔNG làm, kèm lý do:**
  - overload nhận đích màn hình — caller UI→UI duy nhất (`Bridge/BoosterUnlockTutorial.FlyIconAsync`: icon popup nảy tại chỗ, đứng,
    rồi bay thẳng về nút HUD, cỡ theo bề ngang nút trên màn hình) khác hình bay: không vồng, không nghiêng, ease kiểu In để icon đập
    vào nút. Gộp khi có caller thứ hai cùng lý do thay đổi; lúc đó thêm hình bay `Straight` và pha nảy–đứng trước khi bay.
  - gộp với `FlyRewardItem` — khác lý do thay đổi: icon thưởng bay UI→UI, mờ dần, nổ hạt ở mốc; bản sao này nhường chỗ cho vật world.
  - hỗ trợ canvas Screen Space Camera hay World Space — `visual.position = điểm màn hình` chỉ đúng ở Overlay; chưa có người dùng khác.
  - đuôi tuỳ chọn trong framework (`ParticleSystem` field) — `UIParticle` của dự án không có `ParticleSystem` ở gốc; hook rẻ hơn.
- **Huỷ ném `OperationCanceledException`:** `await` một `Sequence` gắn `SetCancellationToken(ct)` mà token bị huỷ thì ném;
  `finally` vẫn tắt bản sao và gọi hook kết thúc. Comment hiện tại "huỷ thì await vẫn xong bình thường" là sai, không chép.
- **Mở rộng sau:** overload đích màn hình khi có caller UI→UI thứ hai cùng hình bay (mục trên) · canvas Camera khi dự án có HUD ở chế độ đó.

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| `Runtime/Utilities/UI/FlyRewardItem.cs` | khuôn component UI bay, `[Splitter]`, chỗ đặt | khác lý do thay đổi (trên) |
| `TransformExtensions.CharmJump` | dùng nguyên cho hình `Arc` | — |
| `HopCurve.ApexT`, `ArcY` (Plan HopCurve GĐ1) | hình `DropIn`: lên đỉnh cao hơn đích rồi rơi `fallDuration` giây cuối | — |
| `CharmFlyArc` | — | tốc độ theo độ dài cung, không đổi cỡ, không nghiêng |
| `CacheTileSlots.FlyTrayAsync`, `FlyPackAsync` | sáu dòng quy đổi world → màn hình | — |

## Luồng mô tả

```
caller (dự án): await flight.FlyAsync(nútScreen, worldCamera, chỗĐápWorld, bềNgangWorld, ct); rồi bật vật thật
UIProxyFlight.FlyAsync:
    toScreen    = camera.WorldToScreenPoint(chỗĐáp) ; toWidth = |điểm màn hình hai mép| (GĐ1)
    endWidth    = toWidth / scaleFactor ; startWidth = startScale · endWidth
    bật visual tại nútScreen, OnFlightStarted()                        ── dự án: đuôi Play
    Sequence: cỡ (sizeEase) ‖ nghiêng sin(πt) ‖ đường bay theo path:
        Arc    → visual.CharmJump(toScreen, arcHeight·scaleFactor, duration, moveEase)
        DropIn → x lerp tuyến tính, y = HopCurve.ArcY(t, from.y, to.y, fallHeight·scaleFactor, apexT)   (GĐ2)
    finally: OnFlightEnded(), xoay về 0, tắt visual                     ── dự án: đuôi Stop + Clear
```

## Bản đồ hệ cuối

| Thành phần | Ai gọi | Việc |
|---|---|---|
| `UIProxyFlight.FlyAsync` | `CacheTileSlots.FlyTrayAsync`, `FlyPackAsync` | toàn chuyến bay; trả khi bản sao đã tắt |
| `UIProxyFlight.OnFlightStarted`, `OnFlightEnded` (`protected virtual`) | `FlyAsync` | chỗ lớp con bật, dọn thứ đi kèm |
| `UIProxyFlightPath` | Inspector | `Arc` · `DropIn` |
| `BoosterFlight : UIProxyFlight` (dự án) | scene | ô `trail` (`UIParticle`), ghi đè hai hook |

## Giai đoạn 1 — bay hình `Arc` tới đích world

**Mục tiêu:** bản sao bay vồng từ một điểm màn hình tới chỗ một vật world, tới nơi đúng bề ngang vật trên màn hình, nghiêng theo hướng
ngang, dọn ở mọi đường ra. **Vì sao trước:** bình hút tile chỉ dùng hình này — xong giai đoạn là đủ một người dùng thật.

**Ai gọi:** test PlayMode của giai đoạn; trong dự án `CacheTileSlots.FlyPackAsync` (GĐ3).

File mới `Runtime/Utilities/UI/UIProxyFlight.cs`:

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
        /// Bay tới chỗ toWorld, tới nơi bề ngang bằng toWorldWidth trên màn hình. Xong hay huỷ đều tắt bản sao; huỷ thì ném
        /// OperationCanceledException sau khi đã dọn.
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
- Đổi sang đơn vị canvas bằng cách chia `scaleFactor` của canvas gốc, vì `sizeDelta` tính theo canvas tham chiếu.
- Nghiêng theo `θ(t) = s·θmax·sin(πt)`: bằng 0 ở `t` = 0 và 1, đạt `θmax` ở `t = 0.5`. `s = −1` khi bay sang phải (Unity quay dương ngược
  chiều kim đồng hồ).

**Quyết định, phương án bỏ, vì sao:**
- **`BuildPath` tách hàm ngay từ giai đoạn này:** GĐ2 chỉ thêm một nhánh vào đây.
- **`LogError` cho ô chưa chọn, không ném:** cấu hình sai lúc setup mà không nổ thì ghi log rồi chạy tiếp (`code-design` › Guard). Bản sao
  vẫn bay, nên người chơi không kẹt ở nút.
- **Không `try/catch` quanh `await`:** huỷ là hợp đồng của caller.

**Editor setup** (dựng từ 0, cho dự án sau):
1. Dưới canvas HUD **Screen Space – Overlay**, tạo `ui_proxy_flight` có `Canvas` (Override Sorting, order trên nút xuất phát) + `UIProxyFlight`.
   Thiếu Override Sorting thì bản sao chui dưới nút.
2. Con `visual`: `RectTransform` anchor và pivot giữa, chứa `Image` của vật; con của `visual` stretch theo nó. Kéo vào ô **Visual**, Image
   vào **Aspect Image**, Canvas vào **Canvas**. Thiếu Aspect Image (sprite null) thì nổ ở lần bay đầu.
3. **Path** = `Arc`.

**Kiểm:** compile sạch. Test PlayMode `Tests/PlayMode/UIProxyFlightTests.cs` (agent viết sau khi đọc code thật). Dựng: canvas Overlay,
camera ortho, lớp con test đếm hook và chụp `sizeDelta` trong `OnFlightEnded`.

| Input | Kỳ vọng |
|---|---|
| bay `duration` 0.1 tới vật world rộng 1 unit, camera ortho size 5, màn 1080×1920, `scaleFactor` 1 | `OnFlightEnded` thấy `sizeDelta.x` = 1080 / (2·5·0.5625) = 192 (±0.5) · `visual` tắt · xoay identity |
| cùng chuyến, đếm hook | `OnFlightStarted` 1 · `OnFlightEnded` 1 |
| huỷ token giữa chuyến | ném `OperationCanceledException` · `OnFlightEnded` 1 · `visual` tắt · xoay identity |
| bay sang phải, `maxTiltDegrees` 10, đọc ở `t ≈ 0.5` | `localEulerAngles.z` ≈ 350 (−10°) |
| `path` = `(UIProxyFlightPath)0` | một `LogError` (`LogAssert.Expect`), vẫn bay xong |

**Mốc commit:** `feat(ui): UIProxyFlight bay hình Arc tới đích world`.

## Giai đoạn 2 — hình `DropIn`

**Mục tiêu:** bản sao bay như vật rơi vào ô: lên đỉnh cao hơn đích rồi rơi vào trong `fallDuration` giây cuối. **Vì sao bây giờ:**
khay ô cache dùng hình này; hình `Arc` đã chạy nên giai đoạn chỉ thêm một nhánh.

**Ai gọi:** test của giai đoạn; trong dự án `CacheTileSlots.FlyTrayAsync` (GĐ3).

`UIProxyFlight.cs` › `UIProxyFlightPath`, sau `Arc = 1,`:

```csharp
        /// <summary>Lên đỉnh cao hơn đích fallHeight rồi rơi vào trong fallDuration giây cuối, gốc đi tuyến tính.</summary>
        DropIn = 2,
```

› `Properties`, sau `moveEase`:

```csharp
        [Tooltip("Path DropIn: đỉnh cao hơn đích bao nhiêu, đơn vị canvas tham chiếu.")]
        [SerializeField, Min(0f)] private float fallHeight = 150f;
        [Tooltip("Path DropIn: số giây rơi từ đỉnh vào đích; đỉnh không sớm hơn nửa chuyến.")]
        [SerializeField, Min(0.02f)] private float fallDuration = 0.15f;
```

› sau `private float _tiltSign;`:

```csharp
        private Vector2 _from;
        private Vector2 _to;
        private float _fallHeightPx;
        private float _apexT;
```

`using Horcrux.Runtime.Utilities.PhysXHelper;` thêm vào đầu file. `BuildPath` cũ → mới:

```csharp
        private Sequence BuildPath(Vector2 fromScreen, Vector2 toScreen, float scaleFactor)
        {
            if (path == UIProxyFlightPath.DropIn)
            {
                _from = fromScreen;
                _to = toScreen;
                _fallHeightPx = fallHeight * scaleFactor;
                _apexT = HopCurve.ApexT(duration, fallDuration);
                // t tuyến tính: đường đất đều, pha rơi của ArcY tự cho cú tăng tốc lúc đáp.
                return Sequence.Create().Group(Tween.Custom(this, 0f, 1f, duration, static (flight, t) => flight.SetDropIn(t), Ease.Linear));
            }

            if (path != UIProxyFlightPath.Arc)
                Debug.LogError($"[UIProxyFlight]: path {path} chưa chọn, bay Arc", this);
            return visual.CharmJump(toScreen, arcHeight * scaleFactor, duration, moveEase);
        }
```

› sau `SetWidth`:

```csharp
        private void SetDropIn(float t)
        {
            var x = Mathf.LerpUnclamped(_from.x, _to.x, t);
            var y = HopCurve.ArcY(t, _from.y, _to.y, _fallHeightPx, _apexT);
            visual.position = new Vector3(x, y, visual.position.z);
        }
```

**Toán → code:** dùng nguyên `ArcY` và `ApexT` của Plan HopCurve §0. `fallHeight` nhân `scaleFactor` vì `y` tính bằng pixel màn hình.

**Quyết định:**
- **`Sequence.Create().Group(...)` bọc tween DropIn:** hai nhánh cùng trả `Sequence`, nên `FlyAsync` ghép bằng một `Group`.
- **Ba số của chuyến DropIn giữ trong field, không closure:** lambda `static` không bắt được biến ngoài (`code-design` › delegate `static`).

**Editor setup:** instance hình rơi đặt **Path** = `DropIn`, chỉnh **Fall Height**, **Fall Duration**.

**Kiểm:** compile sạch; thêm vào `UIProxyFlightTests`:

| Input | Kỳ vọng |
|---|---|
| `DropIn`, `fallHeight` 150, `duration` 0.5, `fallDuration` 0.15, cùng độ cao hai đầu | `y` lớn nhất ≈ đích + 150 tại `t ≈ 0.7` (`ApexT` = max(0.5, 1 − 0.15/0.5)) · kết thúc đúng đích ±0.5 px |
| `DropIn` huỷ giữa chuyến | như case huỷ GĐ1 |

**Mốc commit:** `feat(ui): UIProxyFlight hình DropIn`.

## Giai đoạn 3 — dự án dùng bản Horcrux (agent)

**Mục tiêu:** `BoosterFlight` còn lớp nối với đuôi hạt; hai caller gọi một dòng. **Vì sao bây giờ:** đủ hai hình đường bay.

`Entities/Cache/BoosterFlight.cs` viết lại, giữ file và `.meta` (GUID scene đang trỏ):

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

`Entities/Cache/CacheTileSlots.cs`:

| Chỗ | Cũ | Mới |
|---|---|---|
| `FlyTrayAsync` | sáu dòng `worldCamera`, `halfWidth`, `screenWidth` rồi `_extraSlotFlight.FlyAsync(launchScreenPoint, worldCamera.WorldToScreenPoint(landing), screenWidth, ct)` | `var landing = transform.TransformPoint(_horizontalGrid.LocalPositionOf(openCount - 1, openCount));` rồi `return _extraSlotFlight.FlyAsync(launchScreenPoint, Director.WorldCamera, landing, slot.RestWorldWidth, ct);` |
| `FlyPackAsync` | như trên với `_packBooster` | `return _packFlight.FlyAsync(launchScreenPoint, Director.WorldCamera, _packBooster.transform.position, _packBooster.RestWorldWidth, ct);` |
| comment quanh hai lời gọi nói "huỷ thì trả về bình thường" | — | sửa thành "huỷ thì ném `OperationCanceledException`" |

**YAML `Game.unity`** (agent sửa text, rồi đọc lại trên object đã dựng bằng eval):

| Instance | Đổi |
|---|---|
| `extra_slot_flight` (`fallHeight: 150`) | thêm `path: 2` |
| `pack_flight` (không có key `fallHeight`) | thêm `path: 1` |

Các key còn lại (`canvas`, `visual`, `aspectImage`, `trail`, `duration`, `arcHeight`, `startScale`, `moveEase`, `fallHeight`,
`fallDuration`, `sizeEase`, `maxTiltDegrees`) trùng tên field, không đổi. Nếu `pack_flight` chưa có key `fallHeight` thì sau lần lưu đầu,
Unity ghi `fallHeight: 150` (mặc định mới) cho nó. Key đó vô hại vì `path` là `Arc`.

**Kiểm:**
- Compile sạch; grep `TileFlightCurve` trong `BoosterFlight.cs` = 0.
- Eval trên scene đã dựng: `extra_slot_flight.path == DropIn` · `pack_flight.path == Arc` · `trail` không null ở cả hai.
- **Cần chơi thử** (developer): bấm Extra Slot thì khay lên đỉnh trên ô rồi rơi vào. Bấm Pack thì bình bay vồng, nghiêng nhẹ, đáp đúng
  chỗ và cỡ bình thật. Dấu hiệu hỏng: bản sao giật cỡ một frame lúc nhường vật thật · đuôi kéo vệt từ chuyến trước · log `UIProxyFlight`
  đỏ (`path` chưa chọn).

**Mốc commit:** `refactor(booster): BoosterFlight dựng trên UIProxyFlight của Horcrux` ở dự án, kèm con trỏ submodule.

## Giai đoạn 4 — tài liệu (agent)

`docs/Runtime/UIProxyFlight/UIProxyFlight.md` mở đầu `Code: Runtime/Utilities/UI/UIProxyFlight.cs` và `.html` cùng thư mục.
- **`.md`:** luồng (như trên) · bảng API (`FlyAsync`, hai hook, `UIProxyFlightPath`, field) · "Trước khi chạy" (Editor setup GĐ1–2) ·
  "Design decisions" theo Ngữ cảnh đã chốt.
- **Bẫy:** canvas gốc không phải Overlay · thiếu Override Sorting · `path` = 0 · huỷ ném OCE · lớp con quên dọn đuôi trong `OnFlightEnded`
  (vệt từ chuyến trước).
- **`.html`:** theo `docs` › `.html`.

Dự án: `Scripts/Feel.md` §10, §11 đổi tên field `fallHeight > 0` thành `path`. Xong thì **xoá Plan này** cùng `.meta`.

**Mốc commit:** `docs(ui): UIProxyFlight.md, .html`, xoá Plan.

## Use case

Booster bay từ nút xuống bàn chơi (khay thêm ô, bình hút) · món mua trong shop bay vào túi đồ world · icon phần thưởng bay xuống ô của
bàn chơi khi nhận quà giữa màn.

## File → giai đoạn

| File | GĐ |
|---|---|
| `Runtime/Utilities/UI/UIProxyFlight.cs` | 1, 2 |
| `Tests/PlayMode/UIProxyFlightTests.cs` (agent) | 1, 2 |
| dự án: `BoosterFlight.cs`, `CacheTileSlots.cs`, `Game.unity` | 3 |
| `docs/Runtime/UIProxyFlight/UIProxyFlight.md`, `.html`; dự án `Feel.md` | 4 |
