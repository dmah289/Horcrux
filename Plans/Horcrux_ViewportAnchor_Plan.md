# Plan — `ViewportAnchor` và `ViewportCamera` lên Horcrux

Ngày 2026-10-22. Nguồn: bản tạm trong dự án ở `Assets/_CategoryJam/Gameplay/Utilities/` (`ViewportAnchor.cs`,
`ViewportAnchor.Editor.cs`, `GameplayViewport.cs`, `GameplayViewport.Editor.cs`), dựng ở `docs/plans/Gameplay_Layout_Plan.md`
§1.1–§1.2 và đã chơi thử với `designWidthUnits = 10.5`.

**Phân công:**
- Task 1 (Horcrux, code): developer gõ.
- Task 2 (tài liệu module), Task 3 (dự án đổi sang bản Horcrux), Task 4 (test): agent làm.

Horcrux là submodule: Task 1–2 commit **trong `Assets/Horcrux`**, Task 3 commit ở repo dự án kèm con trỏ submodule.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | dự án này: `GameplayRoot.prefab`, `camera_main` + 9 anchor dưới `world` · dự án sau có camera ortho dọc và đặt vật thể world theo mép màn |
| Mục tiêu | camera ortho giữ cố định bề rộng world · vật thể world neo theo điểm trên màn, tính **một lần** ở boot, không `Update`, không constraint |
| Chỗ đặt | Horcrux `Runtime/Utilities/Layout/`, namespace `Horcrux.Runtime.Utilities.Layout`; không gọi tên type nào của dự án (bản tạm đã đạt) |
| Tên | `ViewportAnchor` giữ · `GameplayViewport` đổi thành **`ViewportCamera`**: "Gameplay" là người dùng đầu tiên, không phải cơ chế. Field `gameplayCamera` thành `targetCamera`. Developer đổi tuỳ ý trước khi gõ |
| Giữ GUID | **chuyển file trong Project window của Unity** rồi mới sửa: `GameplayRoot.prefab` trỏ script bằng GUID, chuyển bằng Project window thì 10 component trên prefab giữ nguyên, không phải nối lại |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Tính lại khi màn đổi cỡ (xoay, chia đôi màn) | game khoá dọc, màn không đổi cỡ trong phiên; dự án đầu tiên cần thì thêm một hàm gọi lại `ApplyLayout`, không đổi chữ ký |
| Camera perspective | chưa có người dùng; `ViewportToWorldPoint` với z = 0 chỉ đúng cho ortho |
| Neo theo bề cao (giữ cố định chiều cao world) | chưa có người dùng; thêm sau là một `enum` mới trên `ViewportCamera`, `designWidthUnits` giữ nguyên nghĩa |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| `ViewportAnchor` không có `Awake` | thứ tự `Awake` giữa các object không định trước; anchor phải chạy **sau** khi camera đã đổi size, nên một chủ gọi (`ViewportCamera`) gọi theo thứ tự mảng |
| Aspect đọc từ `targetCamera.aspect`, không từ `Screen` | đổi so với bản tạm. Camera phủ cả màn thì hai số bằng nhau; camera có `rect` nhỏ hơn màn thì chỉ `camera.aspect` đúng. Test EditMode đặt được `camera.aspect`, không đặt được `Screen` |

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| Bản tạm trong dự án (4 file trên) | toàn bộ | dòng "Bản tạm của dự án" trong XML doc · `Screen.width / Screen.height` (lý do ở bảng trên) |
| Horcrux `Utilities/` | chưa có helper bố cục world nào | — |
| Unity `PositionConstraint`, `AspectRatioFitter`, Cinemachine | — | constraint chạy mỗi frame cho thứ không đổi · `AspectRatioFitter` chỉ cho `RectTransform` · Cinemachine thừa cho một camera đứng yên |
| Odin `[Button]` | nút Apply Layout trong Inspector | Horcrux runtime đã dùng Odin (`PersistenceDataEntry.cs`) |

---

## Task 1 — Horcrux (developer gõ)

### 1.1 Chuyển file

Trong Project window của Unity, kéo 4 file (kèm `.meta` tự đi theo) từ `Assets/_CategoryJam/Gameplay/Utilities/` sang
`Assets/Horcrux/Runtime/Utilities/Layout/` (tạo thư mục). Đổi tên trong Project window:
`GameplayViewport.cs` → `ViewportCamera.cs`, `GameplayViewport.Editor.cs` → `ViewportCamera.Editor.cs`.

*Thiếu bước này thì:* tạo file mới bằng tay ra GUID mới, `GameplayRoot.prefab` báo 10 script missing.

Sau khi chuyển, dự án chưa compile được (namespace cũ còn ở 4 file) cho tới hết 1.2.

### 1.2 Chuỗi chỗ đổi

Dòng không nhắc là giữ nguyên.

| # | File | Cũ | Mới |
|---|---|---|---|
| 1 | cả 4 file | `namespace CategoryJam.Gameplay.Utilities` | `namespace Horcrux.Runtime.Utilities.Layout` |
| 2 | `ViewportAnchor.cs` | `/// <see cref="GameplayViewport"/> calls` | `/// <see cref="ViewportCamera"/> calls` |
| 3 | `ViewportAnchor.cs` | `/// Bản tạm của dự án, chờ chuyển vào Horcrux Utilities.` | xoá dòng |
| 4 | `ViewportAnchor.Editor.cs` | `GetComponentInChildren<GameplayViewport>(true)` | `GetComponentInChildren<ViewportCamera>(true)` |
| 5 | `ViewportAnchor.Editor.cs` | `"[ViewportAnchor]: No GameplayViewport under the same root."` | `"[ViewportAnchor]: No ViewportCamera under the same root."` |
| 6 | `ViewportCamera.cs`, `ViewportCamera.Editor.cs` | `sealed partial class GameplayViewport` | `sealed partial class ViewportCamera` |
| 7 | `ViewportCamera.cs` | `/// Sizes the gameplay camera so` | `/// Sizes an orthographic camera so` |
| 8 | `ViewportCamera.cs` | `/// (1 unit = 100 px of art at 1080 px wide), then places` | `/// (10.8 = 1080 px of art at 100 pixels per unit), then places` |
| 9 | `ViewportCamera.cs` | `[SerializeField] private Camera gameplayCamera;` | `[SerializeField] private Camera targetCamera;` |
| 10 | `ViewportCamera.cs` | `var aspect = Screen.width / (float)Screen.height;` | `var aspect = targetCamera.aspect;` |
| 11 | `ViewportCamera.cs` | `gameplayCamera.orthographicSize` · `anchor.Apply(gameplayCamera)` | `targetCamera.orthographicSize` · `anchor.Apply(targetCamera)` |

Đổi tên field ở bước 9 thì key YAML trong `GameplayRoot.prefab` là `gameplayCamera`: Task 3 đổi key đó cùng lần (không gắn
`FormerlySerializedAs`). Commit trong `Assets/Horcrux` sau khi Task 3 vá xong và compile sạch, để hai commit cùng một mốc chạy được.

---

## Task 2 — Tài liệu module (agent)

`Runtime/Utilities/Layout/ViewportAnchor.md`, viết sau khi đọc code developer đã gõ:

- **Trước khi chạy:** camera ortho có `ViewportCamera`, ô `Target Camera` kéo chính camera đó · mỗi vật thể cần neo gắn
  `ViewportAnchor`, kéo vào mảng `Anchors` theo thứ tự muốn đặt · Game view chọn cỡ dọc trước khi Play (`ViewportCamera`
  đọc aspect một lần ở `Awake`) · nút **Apply Layout** trên cả hai component để xem trong Scene view.
- **Design decisions:** hai dòng "Quyết định trái trực giác" ở trên, kèm bẫy *đã sai một lần* của dự án: đổi đơn vị camera
  thì quét mọi hằng số khoảng cách trong code trước lần Play đầu.

## Task 3 — Dự án dùng bản Horcrux (agent)

- `using Horcrux.Runtime.Utilities.Layout;` ở file nào gọi tên hai type (hiện không file `.cs` nào ngoài 4 file đã chuyển).
- `GameplayRoot.prefab`: key `gameplayCamera:` thành `targetCamera:`.
- `Structure.md` bỏ dòng `GameplayViewport + ViewportAnchor` ở `Utilities/` và dòng nợ tương ứng; `Gameplay_Layout_Plan.md`
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

Safe area lệch không dựng được trong EditMode (`Screen.safeArea` chỉ đọc): kiểm khi chơi thử trên máy có tai thỏ.
Field private đặt qua `SerializedObject` trong test, không mở API mới cho test.
