# Plan — Bộ xem trước trình diễn trên prefab (`PreviewSnapshot`, `EditorPreviewSession`, `PreviewTicker`) lên Horcrux

Ngày 2026-11-06. Nguồn: category-jam, bản tạm của đợt feeling trong `Assets/_CategoryJam/Scripts/Entities/`:
`PreviewSnapshot.Editor.cs`, `EditorPreviewSession.Editor.cs` (lấy toàn bộ, `internal` → `public`), `Box/BoxAnimation.Editor.cs`
(nút `Preview Complete`; `TickParticles`/`AdvanceParticles`), `Tile/Tile.Editor.cs` (nút `Preview Finale`; `RepaintSceneView`). Hai
người dùng thật: `BoxAnimation` (Edit lẫn Play mode), `Tile` (chỉ Edit mode). Ba class chỉ gọi `UnityEditor`.

**Phân công** (developer chốt 2026-10-31): GĐ1–3 code Editor của Horcrux **developer gõ** — ngoại lệ của "code Editor agent viết
thẳng", theo ý developer; phần nối dự án mỗi giai đoạn, tài liệu (GĐ4), nghiệm thu tổng (GĐ5) agent làm. **Chạy được, thấy được** =
bấm nút trong Prefab Mode thấy diễn rồi trả nguyên trên `CategoryBox.prefab` hoặc `Tile.prefab`, kèm phép kiểm máy của agent:
prefab không đổi dòng YAML nào sau màn xem. Không có test: caller duy nhất là hai partial Editor của dự án, nên mỗi giai đoạn nối
dự án ngay, xoá bản tạm cùng tên cùng lúc. Mỗi giai đoạn một commit trong `Assets/Horcrux` rồi một commit ở dự án kèm con trỏ
submodule; GĐ4 chỉ commit trong `Assets/Horcrux`. Không phụ thuộc Plan nào, không có `§0`.

## Ngữ cảnh đã chốt

- **Mục tiêu:** bấm nút trên prefab là diễn tại chỗ rồi trả nguyên từng giá trị; không bao giờ lưu trạng thái giữa chừng vào
  prefab. Người dùng: partial `*.Editor.cs` của class trình diễn (`BoxAnimation`, `Tile`), sau này mọi prefab có trình diễn.
- **Chỗ đặt:** **`Runtime/Utilities/EditorPreview/`**, mỗi file bọc `#if UNITY_EDITOR`, namespace
  `Horcrux.Runtime.Utilities.EditorPreview` — không ở `Editor/` (lý do ở GĐ1). Tài liệu `docs/Editor/EditorPreview.md`, chỉ `.md`.
- **Tên:** `PreviewSnapshot`, `EditorPreviewSession` giữ; `PreviewTicker` gộp "đẩy hạt" và "vẽ lại Scene view" (hai người dùng chỉ
  khác có hạt hay không). Cả ba `public` (bản tạm `internal`): dự án gọi từ assembly khác. Developer đổi tên tuỳ ý trước khi gõ.
- **Không làm:** chụp `ParticleSystem`, `TMP_Text`, `SortingGroup` (GĐ1) · snapshot tự `Capture` khi `Begin` (GĐ2) · nút
  `[Button]` dựng sẵn trong Horcrux (nút gọi method private của class trình diễn, phải ở partial của nó) · `PreviewTicker` chạy ở
  Play mode, ticker tự quét hệ hạt trong cây (GĐ3).
- **Đã khảo sát:** `BoxAnimation.Editor.cs` (nhịp tick: `timeSinceStartup`, `Simulate` khi `isPlaying || isPaused`,
  `RepaintAll`; hệ hạt sinh giữa chừng; danh sách hạt là từ vựng box, không lấy) · `Tile.Editor.cs` (người dùng không hạt) ·
  `BoxAnimation.cs`, `TileView.cs` › `CaptureRestState` (mốc nghỉ đọc từ prefab vì Edit mode không chạy `Awake`) · Horcrux `Runtime/Utilities/` (tiền lệ `*.Editor.cs` trong `Runtime/`:
  `PersistenceDataEntry`, `BasePersistenceDataCollection`, `AudioCatalog`, `AudioService`, `PoolService`) · `Editor/Probe/`,
  `Editor/FeelTrace/` (không hợp: chạy trong Play) · Odin `[Button]` (nút ở lại dự án) · `PrefabStage.prefabSaving`,
  `prefabStageClosing`, `EditorSceneManager.sceneSaving`, `AssemblyReloadEvents.beforeAssemblyReload`,
  `EditorApplication.playModeStateChanged` (năm lối thoát).

## Luồng mô tả — một lần bấm nút xem trước

```
developer mở prefab trong Prefab Mode, bấm nút trên component trình diễn
        ▼
nút: kết thúc màn đang chạy → đọc mốc nghỉ từ prefab → chụp cả cây                  GĐ1 (chụp, trả nguyên)
        ▼
mở phiên với thân dừng; lưu / đóng Prefab Mode / lưu scene / recompile /
đổi Play mode thì phiên tự kết thúc                                                  GĐ2 (phiên, lối thoát)
        ▼
Edit mode: ticker đẩy hệ hạt đã giao theo đồng hồ Editor, vẽ lại Scene view;
hệ hạt sinh giữa chừng giao thêm                                                     GĐ3 (đồng hồ Editor)
        ▼
diễn hết (OnComplete gọi End) hay bị cắt → thân dừng: ticker dừng →
class về nghỉ (dừng tween, Stop hạt) → snapshot trả nguyên                           GĐ1 + GĐ2 + GĐ3
        ▼
tài liệu tool (GĐ4) · nghiệm thu tổng trên dự án (GĐ5)
```

## Bản đồ hệ cuối

```
  partial *.Editor.cs của class trình diễn (dự án)
  ┌──────────────────────────────────────────────────────────────────────────────────┐
  │ nút:  EditorPreviewSession.End()                                                 │
  │       CaptureRestState()              ← Edit mode không chạy Awake               │
  │       _snapshot.Capture(transform) ──────────────► PreviewSnapshot (của object)  │
  │       EditorPreviewSession.Begin(Stop) ──────────► EditorPreviewSession (static) │
  │       PreviewTicker.Start(hạt…) ─────────────────► PreviewTicker (static)        │
  │       hàm diễn thật(onComplete: EditorPreviewSession.End)                        │
  │       … sinh hệ hạt giữa chừng: PreviewTicker.Add(hạt)                           │
  │                                                                                  │
  │ Stop (static): PreviewTicker.Stop() → về nghỉ của class → _snapshot.Restore()    │
  └──────────────────────────────────────────────────────────────────────────────────┘
                     ▲                                 │
                     │ stop()                          │ Begin: End() màn cũ, đăng ký 5 sự kiện Editor
                     └──── EditorPreviewSession.End ◄──┘ End: gỡ 5 sự kiện, gọi stop một lần
                              ▲
     prefabSaving · prefabStageClosing · sceneSaving · beforeAssemblyReload · playModeStateChanged
```

---

## Giai đoạn 1 — Chụp cả cây, diễn, trả nguyên từng giá trị

**Mục tiêu:** `PreviewSnapshot.Capture(root)` ghi transform (vị trí, góc, scale, bật tắt) và SpriteRenderer (màu, bật tắt) của mọi
node trong cây kể cả node đang tắt; `Restore()` trả từng giá trị rồi quên. Xong giai đoạn này hai nút `Preview Complete`,
`Preview Finale` chạy trên bản Horcrux của snapshot.

**Vì sao bây giờ:** "trả nguyên" là lý do tồn tại của cả bộ; không có nó một lần bấm là một lần prefab lưu trạng thái giữa chừng.

### `Runtime/Utilities/EditorPreview/PreviewSnapshot.cs` — file mới

```csharp
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.EditorPreview
{
    /// <summary>Chụp transform (vị trí, góc, scale, bật tắt) và SpriteRenderer (màu, bật tắt) cả cây một object, để màn xem trước trên prefab trả nguyên sau khi diễn.</summary>
    public sealed class PreviewSnapshot
    {
        private readonly List<NodeState> _nodes = new();
        private readonly List<RendererState> _renderers = new();

        public void Capture(Transform root)
        {
            _nodes.Clear();
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                _nodes.Add(new NodeState(node));

            _renderers.Clear();
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                _renderers.Add(new RendererState(renderer));
        }

        public void Restore()
        {
            foreach (var node in _nodes)
                node.Restore();
            foreach (var renderer in _renderers)
                renderer.Restore();

            _nodes.Clear();
            _renderers.Clear();
        }

        private readonly struct NodeState
        {
            private readonly Transform _transform;
            private readonly Vector3 _localPosition;
            private readonly Quaternion _localRotation;
            private readonly Vector3 _localScale;
            private readonly bool _active;

            public NodeState(Transform transform)
            {
                _transform = transform;
                _localPosition = transform.localPosition;
                _localRotation = transform.localRotation;
                _localScale = transform.localScale;
                _active = transform.gameObject.activeSelf;
            }

            public void Restore()
            {
                if (_transform == null) return;

                _transform.SetLocalPositionAndRotation(_localPosition, _localRotation);
                _transform.localScale = _localScale;
                _transform.gameObject.SetActive(_active);
            }
        }

        private readonly struct RendererState
        {
            private readonly SpriteRenderer _renderer;
            private readonly Color _color;
            private readonly bool _enabled;

            public RendererState(SpriteRenderer renderer)
            {
                _renderer = renderer;
                _color = renderer.color;
                _enabled = renderer.enabled;
            }

            public void Restore()
            {
                if (_renderer == null) return;

                _renderer.color = _color;
                _renderer.enabled = _enabled;
            }
        }
    }
}
#endif
```

**Khác với bản dự án:** `internal sealed` → `public sealed`; namespace. Thân không đổi.

**Quyết định** — `Restore` giữ `null` check: object con có thể bị huỷ trong lúc diễn (Editor xoá tay, đóng Prefab Mode huỷ cả stage);
đây là Editor tool, người nhìn là người sửa được. Phương án bỏ: ném để lộ sớm — ném trong handler sự kiện Editor làm các handler sau
không chạy. — `GetComponentsInChildren(true)`: node đang tắt lúc chụp (nắp box) được bật trong lúc diễn, phải trả về tắt. — File trong
`Runtime/` bọc `#if UNITY_EDITOR`, không đặt tên `*.Editor.cs`: `*.Editor.cs` là khuôn partial của một class runtime, đây là class đứng
riêng, tên file là tên class. — Ở `Runtime/`, không ở `Editor/`: hai partial Editor của dự án nằm trong assembly runtime
`CategoryJam.Gameplay` (partial không tách assembly được), assembly đó chỉ tham chiếu `com.horcrux.runtime`, không
`com.horcrux.editor`. — Chỉ chụp transform và SpriteRenderer: hai người dùng chỉ đổi hai thứ này; hạt về nghỉ bằng
`Stop(StopEmittingAndClear)` trong hàm về nghỉ của class trình diễn. Phương án bỏ: chụp thêm `ParticleSystem`, `TMP_Text`,
`SortingGroup` — chưa ai cần.

| Consumes | Produces |
|---|---|
| `UnityEngine.Transform`, `SpriteRenderer` | `PreviewSnapshot.Capture(Transform)`, `Restore()` |

**Sơ đồ tương tác sau GĐ1** (`+` là phần mới)

```
  nút ──Capture(transform)──► PreviewSnapshot+ (của object)
  thân dừng ──về nghỉ của class──► ──Restore()──► PreviewSnapshot+
  (phiên và tick còn chạy trên bản tạm trong dự án)
```

### Dự án nối vào (agent)

| File | Đổi |
|---|---|
| `Entities/PreviewSnapshot.Editor.cs` + `.meta` | **xoá** |
| `Entities/Box/BoxAnimation.Editor.cs` | thêm `using Horcrux.Runtime.Utilities.EditorPreview;` dưới `using Sirenix.OdinInspector;` |
| `Entities/Tile/Tile.Editor.cs` | thêm `using Horcrux.Runtime.Utilities.EditorPreview;` trong khối `#if UNITY_EDITOR` đầu file, dưới `using PrimeTween;` |

Không dòng gọi nào đổi: tên class và chữ ký `Capture`/`Restore` giữ. Bản tạm phải xoá **cùng lúc**: class cùng tên trong namespace
`CategoryJam.Gameplay.Entities` thắng `using` ở đầu file, bản tạm còn thì compile vẫn sạch mà nút chạy bản tạm.

**Editor setup:** tạo thư mục `Runtime/Utilities/EditorPreview/` và file `PreviewSnapshot.cs` trong Project window của Unity (để có
`.meta`). Không kéo thả gì.

**Kiểm:** `bash Assets/Horcrux/Editor/CompileCheck/Tools~/verify.sh --csproj CategoryJam.Gameplay.csproj,com.horcrux.runtime.csproj --tests CategoryJam.EditModeTests`
xanh · `grep -rn "class PreviewSnapshot" Assets/_CategoryJam` ra 0 dòng · prefab không đổi: sau mỗi dòng kịch bản, lưu prefab (Ctrl+S
trong Prefab Mode) rồi `git diff --stat -- Assets/_CategoryJam/Addressables/Box/CategoryBox.prefab Assets/_CategoryJam/Addressables/Tile/Tile.prefab`
rỗng (agent chạy).

| Vào đâu | Làm gì | Nhìn gì | Dấu hiệu hỏng |
|---|---|---|---|
| `CategoryBox.prefab`, Prefab Mode, chọn gốc có `BoxAnimation` | bấm `Preview Complete`, chờ diễn hết | nắp bay vào, box nén, vọt đi; rồi nắp tắt lại, box về đúng chỗ và cỡ, khung sáng như trước | box lệch vị trí, nắp còn bật, `git diff` prefab có `m_LocalPosition`, `m_Color`, `m_IsActive` |
| `Tile.prefab`, Prefab Mode, chọn gốc có `Tile` | bấm `Preview Finale`, chờ diễn hết | tada tại chỗ, bóng co theo độ cao; rồi scale gốc, thân, bóng về như trước | bóng còn nhạt hay tắt, scale gốc khác 1 |
| đang diễn `Preview Complete` | xoá tay một object con của box_anim trong Hierarchy, chờ diễn hết | phần còn lại trả nguyên, Console không exception | `MissingReferenceException` từ `Restore` |
| bấm xem khi chưa từng xem | bấm `Preview Finale` lần đầu sau khi mở Editor | diễn bình thường | — |

**Mốc commit:** Horcrux `feat(editor-preview): PreviewSnapshot chụp và trả nguyên cả cây` · dự án `refactor(preview): PreviewSnapshot
dùng bản Horcrux` kèm con trỏ submodule.

---

## Giai đoạn 2 — Một màn mỗi lúc, năm lối thoát của Editor

**Mục tiêu:** `EditorPreviewSession.Begin(stop)` kết thúc màn đang chạy (của bất kỳ object nào) rồi đăng ký năm sự kiện Editor;
`End()` là cửa duy nhất kết thúc — diễn hết, bấm lại, hay Editor lưu / đóng Prefab Mode / lưu scene / recompile / đổi Play mode. Xong
giai đoạn này sửa một số trong lúc xem là an toàn: auto save dừng màn xem, trả nguyên, rồi mới lưu.

**Vì sao bây giờ:** trả nguyên chỉ chắc khi mọi đường ra đều đi qua thân dừng; GĐ1 mới có đường ra "diễn hết".

### `Runtime/Utilities/EditorPreview/EditorPreviewSession.cs` — file mới

```csharp
#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Horcrux.Runtime.Utilities.EditorPreview
{
    /// <summary>Mỗi lúc một màn xem trước trên prefab. Màn mới, lưu, đóng Prefab Mode, recompile hay đổi Play mode thì màn đang chạy dừng
    /// và trả nguyên trước, nên prefab không lưu trạng thái giữa chừng.</summary>
    public static class EditorPreviewSession
    {
        private static Action s_stop;

        /// <summary>Màn đang chạy (của bất kỳ object nào) dừng trước; stop là thân dừng của màn mới.</summary>
        public static void Begin(Action stop)
        {
            End();
            s_stop = stop;
            PrefabStage.prefabSaving += OnPrefabSaving;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            AssemblyReloadEvents.beforeAssemblyReload += End;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>Cửa duy nhất kết thúc màn xem: diễn hết, bấm xem lại, hay một lối thoát của Editor. Không có màn nào thì không làm gì.</summary>
        public static void End()
        {
            var stop = s_stop;
            s_stop = null;
            PrefabStage.prefabSaving -= OnPrefabSaving;
            PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            AssemblyReloadEvents.beforeAssemblyReload -= End;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            stop?.Invoke();
        }

        private static void OnPrefabSaving(GameObject _) => End();
        private static void OnPrefabStageClosing(PrefabStage _) => End();
        private static void OnSceneSaving(Scene scene, string path) => End();
        private static void OnPlayModeStateChanged(PlayModeStateChange _) => End();
    }
}
#endif
```

**Khác với bản dự án:** `internal static` → `public static`; namespace; summary bỏ "(box, tile)". Thân không đổi.

**Quyết định** — `End()` gỡ đăng ký và xoá `s_stop` **trước** khi gọi `stop`: `stop` có thể gọi lại `End` (thân dừng dừng sequence,
`OnComplete` của sequence là `End`); gỡ trước thì lần gọi lồng thấy `s_stop` null và thoát, thân dừng chạy đúng một lần. Phương án bỏ:
cờ `s_ending` — thêm một trạng thái phải giữ khớp. — Một `Action` static thay vì danh sách màn: hai nút trên hai prefab không bao giờ
cần diễn cùng lúc; màn mới cắt màn cũ là hành vi muốn có (bấm lại khi đang diễn). — `-=` trước `+=` không cần: `Begin` luôn qua `End`
nên không đăng ký hai lần. — `Begin` không tự `Capture` snapshot: snapshot là của object (một object có thể nhiều snapshot cho nhiều
nút), session là của Editor — hai đời khác nhau.

| Consumes | Produces |
|---|---|
| GĐ1 · `PrefabStage`, `EditorSceneManager`, `AssemblyReloadEvents`, `EditorApplication` | `EditorPreviewSession.Begin(Action)`, `End()` |

**Sơ đồ tương tác sau GĐ2**

```
  nút ──End()+──► ──Capture──► PreviewSnapshot ──► ──Begin(Stop)+──► EditorPreviewSession+ ──► hàm diễn(onComplete: End+)
                                                                           ▲
                        prefabSaving · prefabStageClosing · sceneSaving · beforeAssemblyReload · playModeStateChanged+
                                                                           │
  thân dừng Stop ◄──stop()+── End ◄────────────────────────────────────────┘
```

### Dự án nối vào (agent)

| File | Đổi |
|---|---|
| `Entities/EditorPreviewSession.Editor.cs` + `.meta` | **xoá** |

`using` đã có từ GĐ1; mọi lời gọi `EditorPreviewSession.End()`, `Begin(StopPreview)`, `Begin(StopFinalePreview)`,
`.OnComplete(EditorPreviewSession.End)`, `PlayComplete(null, null, EditorPreviewSession.End)` giữ nguyên.

**Editor setup:** tạo `EditorPreviewSession.cs` trong Project window.

**Kiểm:** `verify.sh` (lệnh GĐ1) xanh · `grep -rn "class EditorPreviewSession" Assets/_CategoryJam` ra 0 dòng · `git diff --stat` hai
prefab rỗng sau mỗi dòng.

| Vào đâu | Làm gì | Nhìn gì | Dấu hiệu hỏng |
|---|---|---|---|
| `CategoryBox.prefab`, đang diễn | bấm `Preview Complete` lần hai giữa chừng | box về nguyên tức thì rồi màn mới chạy từ đầu | hai màn chồng (nắp bay hai lần), hoặc màn mới không chạy |
| `Tile.prefab`, đang diễn | sửa một số trong `finale` ở Inspector | màn dừng, tile về nguyên, số mới lưu vào prefab; `git diff` chỉ có đúng số đó | prefab có `m_LocalScale` giữa chừng |
| `CategoryBox.prefab`, đang diễn | đóng Prefab Mode bằng mũi tên `<` trên Hierarchy | không hộp thoại "unsaved", mở lại prefab thấy như trước | prefab lưu nắp đang bật |
| `CategoryBox.prefab`, đang diễn | bấm Play | vào Play không exception; thoát Play, mở lại prefab như trước | exception từ `Restore` hay tween trên object đã huỷ |
| `Tile.prefab`, đang diễn | sửa một file `.cs` bất kỳ rồi quay lại Editor (recompile) | màn dừng trước khi reload, tile về nguyên | tween "sống lại" sau reload, scale lệch |
| không màn nào chạy | gọi `EditorPreviewSession.End()` (bấm nút khi đã xem xong) | không gì xảy ra | exception |
| `CategoryBox.prefab` | bấm `Preview Complete`, đợi `Preview Complete` diễn hết | thân dừng chạy **một** lần (đặt `Debug.Log` tạm trong `StopPreview` nếu muốn thấy) | log hai dòng |

**Mốc commit:** Horcrux `feat(editor-preview): EditorPreviewSession, một màn mỗi lúc, năm lối thoát Editor` · dự án
`refactor(preview): EditorPreviewSession dùng bản Horcrux` kèm con trỏ submodule.

---

## Giai đoạn 3 — Đồng hồ Editor: đẩy hạt, vẽ lại Scene view

**Mục tiêu:** `PreviewTicker.Start(hạt…)` đăng ký `EditorApplication.update`; mỗi tick đẩy các hệ hạt đã giao theo đồng hồ Editor
rồi `SceneView.RepaintAll()`; `Add(hạt)` nhận hệ hạt sinh giữa màn xem; `Stop()` gỡ và quên. Xong giai đoạn này hai partial của dự án
không còn code tick riêng.

**Vì sao bây giờ:** Edit mode không vẽ lại Scene view theo tween, chỉ chạy hạt của object đang chọn; hai người dùng đang viết nó hai lần.

### `Runtime/Utilities/EditorPreview/PreviewTicker.cs` — file mới

```csharp
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.EditorPreview
{
    /// <summary>Edit mode không vẽ lại Scene view theo tween và chỉ chạy hạt của object đang chọn: mỗi tick Editor đẩy các hệ hạt đã giao
    /// theo đồng hồ Editor rồi vẽ lại Scene view. Start không hạt thì chỉ vẽ lại; hệ hạt sinh giữa màn xem giao bằng Add.
    /// Dừng bằng Stop; thân dừng của màn xem gọi Stop ở mọi lối ra.</summary>
    public static class PreviewTicker
    {
        private static readonly List<ParticleSystem> s_systems = new();
        private static double s_lastTime;

        public static void Start(params ParticleSystem[] systems)
        {
            s_systems.Clear();
            s_systems.AddRange(systems);
            s_lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        /// <summary>Hệ hạt sinh sau Start (hiệu ứng tạm đặt ngoài cây object): đẩy từ tick kế tiếp.</summary>
        public static void Add(ParticleSystem system) => s_systems.Add(system);

        public static void Stop()
        {
            EditorApplication.update -= Tick;
            s_systems.Clear();
        }

        private static void Tick()
        {
            var now = EditorApplication.timeSinceStartup;
            var deltaTime = (float)(now - s_lastTime);
            s_lastTime = now;

            foreach (var particles in s_systems)
            {
                // Simulate giữ hệ hạt ở trạng thái tạm dừng: đã Play hay đang đẩy thì đẩy tiếp, Stop ở hàm về nghỉ thì thôi.
                if (particles != null && (particles.isPlaying || particles.isPaused))
                    particles.Simulate(deltaTime, true, false, false);
            }

            SceneView.RepaintAll();
        }
    }
}
#endif
```

**Khác với bản dự án**

| Chỗ | Bản dự án | Bản Horcrux | Vì |
|---|---|---|---|
| Danh sách hệ hạt | `TickParticles` gọi tên bốn field của box và `s_previewCompleteFx` | `Start(params)` + `Add` | ticker không biết từ vựng box; hiệu ứng box xong sinh giữa màn xem nên không có lúc `Start` |
| `null` check hệ hạt | không (field serialize luôn có) | `particles != null` | hệ hạt `Add` là object tạm, có thể bị huỷ tay |
| Bảo vệ `s_previewing == null` | `TickParticles` thoát sớm | không | ticker không giữ object; `Stop` gỡ update ở mọi lối ra qua thân dừng |
| `Tile` | `RepaintSceneView` riêng | `Start()` không hạt | cùng một nhịp |

**Quyết định** — `Add` thay vì `Start` lại với đủ danh sách: caller không lặp lại danh sách, đồng hồ không reset. Phương án bỏ: ticker
quét `GetComponentsInChildren<ParticleSystem>` của root mỗi tick — hiệu ứng box xong nằm ngoài cây, và quét mỗi tick cấp phát. —
`Simulate(dt, withChildren: true, restart: false, fixedTimeStep: false)`: hệ hạt ở trạng thái tạm dừng sau mỗi lần, frame sau đẩy tiếp;
hệ đã `Stop(StopEmittingAndClear)` trong hàm về nghỉ không `isPlaying` cũng không `isPaused` nên không bị đẩy lại. — Tick đầu `deltaTime`
là thời gian từ `Start` tới tick kế, cỡ một frame Editor. — Ticker không phục vụ Play mode: ở Play hệ hạt tự chạy, Scene view tự vẽ
lại; `BoxAnimation` chỉ bật ticker khi `!Application.isPlaying`, `Tile` từ chối Play mode.

| Consumes | Produces |
|---|---|
| GĐ2 · `EditorApplication.update`, `timeSinceStartup`, `SceneView` | `PreviewTicker.Start(params ParticleSystem[])`, `Add(ParticleSystem)`, `Stop()` |

**Sơ đồ tương tác sau GĐ3**

```
  nút ──End──► ──Capture──► ──Begin(Stop)──► ──Start(hạt…)+──► PreviewTicker+ ──mỗi tick: Simulate, RepaintAll+──► Scene view
                                                                      ▲
  hàm diễn ── sinh hiệu ứng tạm ──Add(hạt)+───────────────────────────┘
  thân dừng Stop: PreviewTicker.Stop()+ → về nghỉ của class → Restore
```

### Dự án nối vào (agent)

`Entities/Box/BoxAnimation.Editor.cs` › fields — xoá dòng:

```csharp
        private static double s_lastParticleTime;
```

`BoxAnimation.Editor.cs` › `PreviewComplete` — đoạn cũ → đoạn mới. Cũ:

```csharp
            if (!Application.isPlaying)
            {
                s_lastParticleTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += TickParticles;
            }
```

Mới:

```csharp
            if (!Application.isPlaying)
                PreviewTicker.Start(receiveBurst, finalReceiveBurst, nameRevealStars, leaveTrail);
```

`BoxAnimation.Editor.cs` › `StopPreview` — dòng đầu `EditorApplication.update -= TickParticles;` → `PreviewTicker.Stop();`.

`BoxAnimation.Editor.cs` › `SpawnPreviewCompleteFx` — thêm sau `s_previewCompleteFx.Play(true);`:

```csharp
            s_previewCompleteFx.Play(true);
            PreviewTicker.Add(s_previewCompleteFx);
```

`BoxAnimation.Editor.cs` — xoá hai method `TickParticles`, `AdvanceParticles`.

`Entities/Tile/Tile.Editor.cs` › `PreviewFinale` — `EditorApplication.update += RepaintSceneView;` → `PreviewTicker.Start();` ·
`StopFinalePreview` — `EditorApplication.update -= RepaintSceneView;` → `PreviewTicker.Stop();` · xoá `RepaintSceneView`.

Thứ tự trong `StopPreview` giữ: `PreviewTicker.Stop()` trước, huỷ `s_previewCompleteFx` sau, rồi `ResetState`, `Restore`.

**Editor setup:** tạo `PreviewTicker.cs` trong Project window.

**Kiểm:** `verify.sh` xanh · `grep -rn "TickParticles\|AdvanceParticles\|RepaintSceneView\|s_lastParticleTime" Assets/_CategoryJam` ra 0 dòng
· `git diff --stat` hai prefab rỗng sau mỗi dòng.

| Vào đâu | Làm gì | Nhìn gì | Dấu hiệu hỏng |
|---|---|---|---|
| `CategoryBox.prefab`, Prefab Mode, **không** chọn object hạt | bấm `Preview Complete` | hạt cầu vồng lúc tile cuối, sao tab tên, hiệu ứng box xong nổ tại chỗ box lúc nắp chạm, vệt sáng kéo theo khi vọt; Scene view mượt theo tween | hạt đứng im hay chỉ chạy khi rê chuột vào Scene view; hiệu ứng box xong không nổ (thiếu `Add`) |
| `CategoryBox.prefab`, sau khi diễn hết | nhìn Hierarchy của stage | không còn object `PF_CompleteBox` tạm; hạt của box trống | object tạm còn, hạt còn treo |
| `Tile.prefab` | bấm `Preview Finale` | tada mượt không cần rê chuột | Scene view chỉ đổi khi rê chuột |
| `CategoryBox.prefab`, đang diễn | bấm `Preview Complete` lần hai | hạt lần một dừng và xoá; lần hai chạy từ đầu | hạt hai lần chồng |
| `CategoryBox.prefab`, đang diễn | xoá tay object `PF_CompleteBox` tạm trong Hierarchy | diễn tiếp, Console không exception | `MissingReferenceException` từ `Tick` |
| `Game.unity`, Play mode, một box đủ tile | box đóng nắp trong game | như trước khi có Plan: hạt và hiệu ứng chạy theo Play, ticker không bật | hạt chạy nhanh gấp đôi (ticker bật trong Play) |

**Mốc commit:** Horcrux `feat(editor-preview): PreviewTicker đẩy hạt và vẽ lại Scene view theo đồng hồ Editor` · dự án
`refactor(preview): bỏ code tick riêng, dùng PreviewTicker` kèm con trỏ submodule.

---

## Giai đoạn 4 — Tài liệu `docs/Editor/EditorPreview.md` (agent)

Mở đầu `Code: Runtime/Utilities/EditorPreview/` (tool Editor đặt trong `Runtime/` — lý do ở "Ngữ cảnh đã chốt", chép sang). Mục:
Ngữ cảnh đã chốt (mục trên, bớt dòng dự án) · luồng một lần bấm nút (ASCII ở trên) · bảng API ba class · **Trước khi chạy** cho class
trình diễn muốn có nút:

| # | Thao tác | Thiếu thì hỏng ở đâu |
|---|---|---|
| 1 | Partial `*.Editor.cs` của class, bên trong `#if UNITY_EDITOR`, `using Horcrux.Runtime.Utilities.EditorPreview;` | không compile ở Editor |
| 2 | Field `private readonly PreviewSnapshot _previewSnapshot = new();` và `static` tham chiếu object đang xem | hai lần bấm chồng nhau trả nguyên sai object |
| 3 | Nút: `EditorPreviewSession.End()` → nếu Edit mode thì đọc mốc nghỉ từ prefab (hàm kiểu `CaptureRestState`) vì `Awake` không chạy → `_previewSnapshot.Capture(transform)` → `EditorPreviewSession.Begin(Stop)` → `PreviewTicker.Start(hạt…)` khi Edit mode → gọi hàm diễn thật với callback kết thúc là `EditorPreviewSession.End` | bỏ đọc mốc nghỉ thì tween nhân lên mốc nghỉ cũ của lần Play trước |
| 4 | Hệ hạt sinh giữa màn xem (hiệu ứng từ pool không có ở Edit mode): tạo bản tạm `HideFlags.DontSave`, đặt vào stage bằng `StageUtility.PlaceGameObjectInCurrentStage`, `PreviewTicker.Add`; thân dừng huỷ | hiệu ứng không nổ, hoặc bản tạm lưu vào prefab |
| 5 | Thân dừng (static): `PreviewTicker.Stop()` → hàm về nghỉ của class (dừng tween, `Stop(StopEmittingAndClear)` hạt) → `_previewSnapshot.Restore()` | thiếu về nghỉ thì tween còn sống ghi đè lên giá trị vừa trả |

Thêm "Design decisions" gom các **Quyết định** của GĐ1–3 và "Bẫy": `End` lồng từ `OnComplete` · hệ hạt chỉ đẩy khi `isPlaying || isPaused`
· class cùng tên trong namespace dự án thắng `using`. Không kịch bản chơi thử, không tên dự án.

**Mốc commit:** Horcrux `docs(editor-preview): EditorPreview.md`.

---

## Giai đoạn 5 — Dự án chỉ còn bản Horcrux: nghiệm thu tổng (agent)

**Mục tiêu:** chứng minh dự án không còn bản tạm, player build không thấy ba file, và cả luồng chạy trên hai prefab.

**Kiểm:**

- `grep -rn "EditorPreviewSession\|PreviewSnapshot\|PreviewTicker" Assets/_CategoryJam` chỉ ra hai partial `BoxAnimation.Editor.cs`,
  `Tile.Editor.cs`; `grep -rln "class PreviewSnapshot\|class EditorPreviewSession" Assets/` chỉ ra ba file Horcrux.
- `verify.sh` xanh (lệnh GĐ1) · một build Android cỡ nhỏ (developer, Build Settings) compile sạch: ba file bọc `#if UNITY_EDITOR`,
  player không thấy.
- `graphify update .` ở gốc dự án.
- **Cần chơi thử** (developer), chuỗi ngắn nhất, prefab phải không đổi sau mỗi bước (`git diff --stat` hai prefab rỗng, agent chạy):

| Vào đâu | Làm gì | Nhìn gì | Dấu hiệu hỏng |
|---|---|---|---|
| `CategoryBox.prefab` | `Preview Complete` → chờ hết → bấm lại giữa chừng → sửa một số giữa chừng | ba lần đều về nguyên; số mới lưu, không giá trị giữa chừng | `git diff` prefab có dòng ngoài số vừa sửa |
| `Tile.prefab` | `Preview Finale` → chờ hết → bấm Play giữa chừng | về nguyên; vào Play không exception | exception, scale tile lệch |
| `Game.unity`, Play | chơi một box đủ tile | hoàn thành box như trước Plan | hạt nhanh gấp đôi, hiệu ứng thiếu |

**Mốc commit:** không có file đổi; con trỏ submodule đã theo từng giai đoạn.

---

## File → giai đoạn

| File | GĐ1 | GĐ2 | GĐ3 | GĐ4 | GĐ5 |
|---|---|---|---|---|---|
| `Runtime/Utilities/EditorPreview/PreviewSnapshot.cs` | tạo | | | | |
| `Runtime/Utilities/EditorPreview/EditorPreviewSession.cs` | | tạo | | | |
| `Runtime/Utilities/EditorPreview/PreviewTicker.cs` | | | tạo | | |
| `docs/Editor/EditorPreview.md` (agent) | | | | tạo | |
| dự án: `Entities/PreviewSnapshot.Editor.cs` | xoá | | | | |
| dự án: `Entities/EditorPreviewSession.Editor.cs` | | xoá | | | |
| dự án: `Entities/Box/BoxAnimation.Editor.cs` | `using` | | `PreviewTicker`, xoá `TickParticles`, `AdvanceParticles`, `s_lastParticleTime` | | kiểm |
| dự án: `Entities/Tile/Tile.Editor.cs` | `using` | | `PreviewTicker`, xoá `RepaintSceneView` | | kiểm |
