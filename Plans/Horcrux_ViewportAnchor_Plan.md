# Plan — `ViewportAnchor`, `ViewportCover` và `ViewportCamera` lên Horcrux

Ngày 2026-10-22. Nguồn: bản tạm trong dự án ở `Assets/_CategoryJam/Gameplay/Utilities/` (`ViewportAnchor.cs`,
`ViewportAnchor.Editor.cs`, `ViewportCover.cs`, `GameplayViewport.cs`, `GameplayViewport.Editor.cs`), dựng ở
`docs/plans/Gameplay_Layout_Plan.md` §1.1–§1.2 và đã chơi thử với `designWidthUnits = 10.5`. `ViewportCover` thêm ngày
2026-10-23 (commit dự án `516dfbe0`) cho ảnh nền gameplay, đã kiểm trong Play ở 9:21, 9:19.5 và 3:4.

**Phân công:**
- Task 1 (Horcrux, code): developer gõ.
- Task 2 (tài liệu module), Task 3 (dự án đổi sang bản Horcrux), Task 4 (test): agent làm.

Horcrux là submodule: Task 1–2 commit **trong `Assets/Horcrux`**, Task 3 commit ở repo dự án kèm con trỏ submodule.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | dự án này: `GameplayRoot.prefab`, `camera_main` + 9 anchor dưới `world` + 1 cover (`world/background`, ảnh nền) · dự án sau có camera ortho dọc và đặt vật thể world theo mép màn |
| Mục tiêu | camera ortho giữ cố định bề rộng world · vật thể world neo theo điểm trên màn · sprite nền phủ kín màn ở mọi tỉ lệ; tất cả tính **một lần** ở boot, không `Update`, không constraint |
| Chỗ đặt | Horcrux `Runtime/Utilities/Layout/`, namespace `Horcrux.Runtime.Utilities.Layout`; không gọi tên type nào của dự án (bản tạm đã đạt) |
| Tên | `ViewportAnchor`, `ViewportCover` giữ · `GameplayViewport` đổi thành **`ViewportCamera`**: "Gameplay" là người dùng đầu tiên, không phải cơ chế. Field `gameplayCamera` thành `targetCamera`. Developer đổi tuỳ ý trước khi gõ |
| Giữ GUID | **chuyển file trong Project window của Unity** rồi mới sửa: `GameplayRoot.prefab` trỏ script bằng GUID, chuyển bằng Project window thì 11 component trên prefab giữ nguyên, không phải nối lại |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Tính lại khi màn đổi cỡ (xoay, chia đôi màn) | game khoá dọc, màn không đổi cỡ trong phiên; dự án đầu tiên cần thì thêm một hàm gọi lại `ApplyLayout`, không đổi chữ ký |
| Camera perspective | chưa có người dùng; `ViewportToWorldPoint` với z = 0 chỉ đúng cho ortho |
| Neo theo bề cao (giữ cố định chiều cao world) | chưa có người dùng; thêm sau là một `enum` mới trên `ViewportCamera`, `designWidthUnits` giữ nguyên nghĩa |
| `ViewportCover` co giãn không đều (kéo méo cho vừa khít) | méo art; ảnh nền vẽ dư hai đầu thì cắt trục thừa là đủ |
| `ViewportCover` cho `Image` của UI | UI đã có `AspectRatioFitter` chế độ `EnvelopeParent` |
| `ViewportCover` bù scale của cha | mọi cha hiện tại scale 1; Tooltip của field `target` ghi điều kiện này. Cần thì chia cho `parent.lossyScale`, không đổi chữ ký |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| `ViewportAnchor` không có `Awake` | thứ tự `Awake` giữa các object không định trước; anchor phải chạy **sau** khi camera đã đổi size, nên một chủ gọi (`ViewportCamera`) gọi theo thứ tự mảng |
| Aspect đọc từ `targetCamera.aspect`, không từ `Screen` | đổi so với bản tạm. Camera phủ cả màn thì hai số bằng nhau; camera có `rect` nhỏ hơn màn thì chỉ `camera.aspect` đúng. Test EditMode đặt được `camera.aspect`, không đặt được `Screen`. *Đã sai một lần:* vào Play qua MCP khi Editor không focus, `Screen` ở `Awake` trả cỡ ngang (aspect 1.37) trong khi camera vẽ 1080×2340, ortho ra 3.94 thay vì 11.7. `ViewportCover` đã đọc `camera.aspect` từ đầu |
| `ViewportCover` là component riêng, không phải cờ trên `ViewportAnchor` | anchor chỉ đặt vị trí, cover chỉ đặt scale; ảnh nền cần cả hai nên gắn cả hai trên một object. `ViewportCamera` gọi hai mảng riêng, cover sau anchor (cover không đọc vị trí nên thứ tự giữa hai mảng không đổi kết quả) |
| `ViewportCover` phóng **đều** theo trục thiếu hơn (`max(viewW / spriteW, viewH / spriteH)`) | phủ kín mọi tỉ lệ mà không méo; trục còn lại tràn ra ngoài màn và bị cắt. Ảnh nền vẽ 1080×2340 nên 9:19.5 gần khớp, máy thấp hơn cắt trên dưới, máy cao hơn cắt hai bên |

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| Bản tạm trong dự án (4 file trên) | toàn bộ | dòng "Bản tạm của dự án" trong XML doc · `Screen.width / Screen.height` (lý do ở bảng trên) |
| Horcrux `Utilities/` | chưa có helper bố cục world nào | — |
| Unity `PositionConstraint`, `AspectRatioFitter`, Cinemachine | — | constraint chạy mỗi frame cho thứ không đổi · `AspectRatioFitter` chỉ cho `RectTransform` · Cinemachine thừa cho một camera đứng yên |
| Ảnh nền trên Canvas Screen Space–Camera + `AspectRatioFitter` `EnvelopeParent` | — | chạy được (canvas sort theo sorting layer `Background` nằm dưới sprite), nhưng thêm một canvas chỉ cho một ảnh; sprite world đi chung đơn vị và sorting layer với gameplay. Canvas Overlay thì luôn đè gameplay |
| Odin `[Button]` | nút Apply Layout trong Inspector | Horcrux runtime đã dùng Odin (`PersistenceDataEntry.cs`) |

---

## Task 1 — Horcrux (developer gõ)

### 1.1 Chuyển file

Trong Project window của Unity, kéo 5 file (kèm `.meta` tự đi theo) từ `Assets/_CategoryJam/Gameplay/Utilities/` sang
`Assets/Horcrux/Runtime/Utilities/Layout/` (tạo thư mục). Đổi tên trong Project window:
`GameplayViewport.cs` → `ViewportCamera.cs`, `GameplayViewport.Editor.cs` → `ViewportCamera.Editor.cs`.

*Thiếu bước này thì:* tạo file mới bằng tay ra GUID mới, `GameplayRoot.prefab` báo 11 script missing.

Sau khi chuyển, dự án chưa compile được (namespace cũ còn ở 5 file) cho tới hết 1.2.

### 1.2 Chuỗi chỗ đổi

Dòng không nhắc là giữ nguyên.

| # | File | Cũ | Mới |
|---|---|---|---|
| 1 | cả 5 file | `namespace CategoryJam.Gameplay.Utilities` | `namespace Horcrux.Runtime.Utilities.Layout` |
| 2 | `ViewportAnchor.cs`, `ViewportCover.cs` | `/// <see cref="GameplayViewport"/> calls` | `/// <see cref="ViewportCamera"/> calls` |
| 3 | `ViewportAnchor.cs` | `/// Bản tạm của dự án, chờ chuyển vào Horcrux Utilities.` | xoá dòng |
| 3b | `ViewportCover.cs` | `/// Bản tạm của dự án, chờ chuyển vào Horcrux Utilities cùng <see cref="ViewportAnchor"/>.` | xoá dòng |
| 4 | `ViewportAnchor.Editor.cs` | `GetComponentInChildren<GameplayViewport>(true)` | `GetComponentInChildren<ViewportCamera>(true)` |
| 5 | `ViewportAnchor.Editor.cs` | `"[ViewportAnchor]: No GameplayViewport under the same root."` | `"[ViewportAnchor]: No ViewportCamera under the same root."` |
| 6 | `ViewportCamera.cs`, `ViewportCamera.Editor.cs` | `sealed partial class GameplayViewport` | `sealed partial class ViewportCamera` |
| 7 | `ViewportCamera.cs` | `/// Sizes the gameplay camera so` | `/// Sizes an orthographic camera so` |
| 8 | `ViewportCamera.cs` | `/// (1 unit = 100 px of art at 1080 px wide), then places` | `/// (10.8 = 1080 px of art at 100 pixels per unit), then places` |
| 9 | `ViewportCamera.cs` | `[SerializeField] private Camera gameplayCamera;` | `[SerializeField] private Camera targetCamera;` |
| 10 | `ViewportCamera.cs` | `var aspect = Screen.width / (float)Screen.height;` | `var aspect = targetCamera.aspect;` |
| 11 | `ViewportCamera.cs` | `gameplayCamera.orthographicSize` · `anchor.Apply(gameplayCamera)` · `cover.Apply(gameplayCamera)` | `targetCamera.orthographicSize` · `anchor.Apply(targetCamera)` · `cover.Apply(targetCamera)` |

Đổi tên field ở bước 9 thì key YAML trong `GameplayRoot.prefab` là `gameplayCamera`: Task 3 đổi key đó cùng lần (không gắn
`FormerlySerializedAs`). Commit trong `Assets/Horcrux` sau khi Task 3 vá xong và compile sạch, để hai commit cùng một mốc chạy được.

---

## Task 2 — Tài liệu module (agent)

`Runtime/Utilities/Layout/ViewportAnchor.md` (một tài liệu cho cả ba component), viết sau khi đọc code developer đã gõ:

- **Trước khi chạy:** camera ortho có `ViewportCamera`, ô `Target Camera` kéo chính camera đó · mỗi vật thể cần neo gắn
  `ViewportAnchor`, kéo vào mảng `Anchors` theo thứ tự muốn đặt · sprite cần phủ kín màn (ảnh nền) gắn `ViewportCover`, ô
  `Target` kéo `SpriteRenderer` của nó, cha scale 1, kéo vào mảng `Covers`; muốn nằm giữa màn thì gắn thêm `ViewportAnchor (0.5, 0.5)` ·
  ảnh nền đặt ngoài thư mục atlas world (một ảnh cỡ cả màn làm phình atlas), import Single + FullRect · Game view chọn cỡ dọc trước khi Play (`ViewportCamera`
  đọc aspect một lần ở `Awake`) · nút **Apply Layout** trên cả hai component để xem trong Scene view.
- **Design decisions:** các dòng "Quyết định trái trực giác" ở trên, kèm bẫy *đã sai một lần* của dự án: đổi đơn vị camera
  thì quét mọi hằng số khoảng cách trong code trước lần Play đầu.

## Task 3 — Dự án dùng bản Horcrux (agent)

- `using Horcrux.Runtime.Utilities.Layout;` ở file nào gọi tên ba type (hiện không file `.cs` nào ngoài 5 file đã chuyển).
- `GameplayRoot.prefab`: key `gameplayCamera:` thành `targetCamera:`.
- `Structure.md` bỏ dòng `GameplayViewport + ViewportAnchor + ViewportCover` ở `Utilities/` và dòng nợ tương ứng; `Gameplay_Layout_Plan.md`
  đổi tên `GameplayViewport` thành `ViewportCamera` (đổi tên là đổi cả hệ).
- Vá đường dẫn trong `CategoryJam.Gameplay.csproj` và csproj Horcrux nếu Unity chưa sinh lại, rồi compile 4 csproj.
- Nghiệm thu: grep `GameplayViewport`, `gameplayCamera` trên `.cs`, `.prefab`, `.unity`, `.md` toàn repo trừ lịch sử commit: 0.

## Task 4 — Test EditMode (agent)

`Assets/Horcrux/Tests/EditMode/ViewportLayoutTests.cs`. Camera ortho dựng trong test, đặt `aspect` tường minh.

| Case | Input | Kỳ vọng |
|---|---|---|
| bề rộng cố định 9:19.5 | `designWidthUnits 10.8`, `aspect 1080/2340` | `orthographicSize` 11.7 |
| bề rộng cố định 9:16 | `aspect 1080/1920` | `orthographicSize` 9.6 |
| tâm | anchor `(0.5, 0.5)`, camera ở (0, 0) | position (0, 0) |
| góc dưới trái | anchor `(0, 0)`, aspect 9:16 | position (−5.4, −9.6) |
| góc trên phải | anchor `(1, 1)`, aspect 9:16 | position (5.4, 9.6) |
| offset | anchor `(0.5, 0.5)`, `worldOffset (1, −2)` | position (1, −2) |
| giữ z | transform z = 7 trước `Apply` | z vẫn 7 |
| thứ tự mảng | hai anchor, anchor thứ hai là con của anchor thứ nhất | con nằm đúng điểm của nó sau khi cha đã dời |
| safe area cả màn | `respectSafeArea true`, Editor có safe area = cả màn | bằng ca `false` |
| cover máy cao hơn ảnh | sprite 10.8×23.4, `designWidthUnits 10.8`, aspect 9:21 (view 10.8×25.2) | scale 1.0769, phủ kín chiều cao, tràn hai bên |
| cover máy thấp hơn ảnh | sprite 10.8×23.4, aspect 9:16 (view 10.8×19.2) | scale 1, tràn trên dưới |
| cover ảnh ngang | sprite 20×10, aspect 9:16 | scale 1.92 (trục cao quyết định) |
| cover giữ z | `localScale.z` = 1 sau `Apply` | z = 1 |

Sprite trong test dựng bằng `Sprite.Create` từ `Texture2D` (vd 108×234 px, 10 pixels per unit cho 10.8×23.4).

Safe area lệch không dựng được trong EditMode (`Screen.safeArea` chỉ đọc): kiểm khi chơi thử trên máy có tai thỏ.
Field private đặt qua `SerializedObject` trong test, không mở API mới cho test.
