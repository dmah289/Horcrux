# Plan — Bộ xem trước trình diễn trên prefab (`EditorPreviewSession`, `PreviewSnapshot`, `PreviewTicker`) lên Horcrux

Ngày 2026-10-31. Nguồn: bản tạm trong dự án category-jam, đợt feeling:

- `Assets/_CategoryJam/Scripts/Entities/EditorPreviewSession.Editor.cs` — một màn xem trước mỗi lúc; lưu prefab, đóng Prefab Mode,
  recompile, đổi Play mode thì dừng và trả nguyên.
- `Assets/_CategoryJam/Scripts/Entities/PreviewSnapshot.Editor.cs` — chụp và trả nguyên transform, SpriteRenderer cả cây.
- `Assets/_CategoryJam/Scripts/Entities/Box/BoxAnimation.Editor.cs` `TickParticles`/`AdvanceParticles` — đẩy hệ hạt ở Edit mode theo
  đồng hồ Editor, vẽ lại Scene view.
- `Assets/_CategoryJam/Scripts/Entities/Tile/Tile.Editor.cs` `RepaintSceneView` — vẽ lại Scene view mỗi tick khi đang xem.

Hai người dùng thật: nút `Preview Complete` của `BoxAnimation`, nút `Preview Finale` của `Tile`. Không gọi type dự án, chỉ `UnityEditor`.

**Phân công** (developer chốt 2026-10-31):
- Task 1 (Horcrux, code Editor): **developer gõ** — ngoại lệ của luật "code Editor agent viết thẳng", theo ý developer cho plan này.
- Task 2 (tài liệu), Task 3 (dự án đổi sang bản Horcrux, xoá bản tạm): agent làm.
- Hai script `playrun.sh`, `feeltrace.sh` (cùng mục §4.3 của audit) agent sửa thẳng, không trong plan này.

Horcrux là submodule: Task 1–2 commit **trong `Assets/Horcrux`**, Task 3 commit ở repo dự án kèm con trỏ submodule.

---

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | partial `*.Editor.cs` của class trình diễn trong dự án (`BoxAnimation`, `Tile`); dự án sau: mọi prefab có trình diễn muốn một nút xem trước ngay trong Prefab Mode |
| Mục tiêu | bấm nút trên prefab là diễn tại chỗ rồi trả nguyên từng giá trị; không bao giờ lưu trạng thái giữa chừng vào prefab |
| Chỗ đặt | **`Runtime/Utilities/EditorPreview/`**, mỗi file bọc `#if UNITY_EDITOR`, namespace `Horcrux.Runtime.Utilities.EditorPreview`. Không ở `Editor/`: hai partial Editor của dự án nằm trong assembly runtime `CategoryJam.Gameplay` (partial không tách assembly được) và assembly đó không tham chiếu `com.horcrux.editor`; Horcrux đã có tiền lệ năm file `*.Editor.cs` trong Runtime |
| Tên | `EditorPreviewSession`, `PreviewSnapshot` giữ; `PreviewTicker` gộp "đẩy hạt" và "vẽ lại Scene view" (hai người dùng chỉ khác có hạt hay không). Developer đổi tuỳ ý trước khi gõ |
| Quyền truy cập | `public` cả ba (bản tạm `internal`): dự án gọi từ assembly khác |

**Cố ý KHÔNG làm**

| Không làm | Vì |
|---|---|
| Chụp thêm `ParticleSystem`, `TMP_Text`, `SortingGroup` | hai người dùng chỉ đổi transform và SpriteRenderer; hạt trả về bằng `Stop(StopEmittingAndClear)` trong `ResetState` của chính class trình diễn |
| Snapshot tự `Capture` khi `Begin` | snapshot là của object, session là của Editor: hai đời khác nhau (object có nhiều snapshot cho nhiều nút) |
| Nút `[Button]` dựng sẵn trong Horcrux (base class hay attribute) | nút gọi method private của class trình diễn (`PlayComplete`, `StepFinale`); phải nằm trong partial của class đó |
| Chạy được ở Play mode cho `PreviewTicker` | Play mode hệ hạt tự chạy; `BoxAnimation` chỉ bật ticker khi `!Application.isPlaying` |

**Quyết định trái trực giác**

| Chỗ | Vì |
|---|---|
| `End()` gỡ đăng ký **trước** khi gọi `stop` | `stop` có thể gọi lại `End` (OnComplete của tween diễn hết): gỡ trước thì lần gọi lồng thấy `s_stop` null và thoát |
| `PreviewTicker.Tick` chỉ `Simulate` hệ hạt `isPlaying || isPaused` | hệ đã `Stop` trong `ResetState` thì không đẩy nữa; `Simulate(dt, true, false, false)` giữ trạng thái tạm dừng để frame sau đẩy tiếp |
| `PreviewSnapshot.Restore` giữ `null` check `_transform == null` | object con có thể bị huỷ trong lúc diễn (Editor xoá tay); đây là Editor tool, người nhìn là người sửa được |
| File `.cs` trong `Runtime/` bọc `#if UNITY_EDITOR`, không đặt tên `*.Editor.cs` | `*.Editor.cs` là khuôn partial của một class runtime; đây là class đứng riêng, tên file là tên class |

---

## Đã khảo sát

| Nguồn | Lấy gì | Không lấy, vì sao |
|---|---|---|
| Bản tạm trong dự án (hai file + hai đoạn Editor) | toàn bộ, gộp phần tick | `internal` → `public` |
| Horcrux `Editor/Probe/PlayProbe.cs`, `Editor/FeelTrace/` | — | chạy trong Play; đây là Edit mode trên prefab |
| Odin `[Button]` | nút trên component (đang dùng) | nút ở lại dự án (bảng trên) |
| Unity `PrefabStage.prefabSaving`, `prefabStageClosing`, `EditorSceneManager.sceneSaving`, `AssemblyReloadEvents.beforeAssemblyReload`, `EditorApplication.playModeStateChanged` | bốn lối thoát của Editor | — |

---

## Task 1 — Horcrux (developer gõ)

Thư mục `Runtime/Utilities/EditorPreview/`, ba file; mỗi file bọc toàn bộ trong `#if UNITY_EDITOR … #endif`.

### 1.1 `EditorPreviewSession.cs`

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

### 1.2 `PreviewSnapshot.cs`

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

### 1.3 `PreviewTicker.cs`

```csharp
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.EditorPreview
{
    /// <summary>Edit mode không vẽ lại Scene view theo tween và chỉ chạy hạt của object đang chọn: mỗi tick Editor đẩy các hệ hạt đã giao
    /// theo đồng hồ Editor rồi vẽ lại Scene view. Start không hạt thì chỉ vẽ lại. Dừng bằng Stop; EditorPreviewSession gọi Stop ở mọi lối ra.</summary>
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
                // Simulate giữ hệ hạt ở trạng thái tạm dừng: đã Play hay đang đẩy thì đẩy tiếp, Stop ở reset thì thôi.
                if (particles != null && (particles.isPlaying || particles.isPaused))
                    particles.Simulate(deltaTime, true, false, false);
            }

            SceneView.RepaintAll();
        }
    }
}
#endif
```

**Editor setup:** tạo thư mục và ba file trong Project window của Unity (để có `.meta`). Không kéo thả gì.

---

## Task 2 — Tài liệu module (agent)

`Runtime/Utilities/EditorPreview/EditorPreview.md`: luồng `nút [Button] → Capture → Begin(stop) → diễn → End → stop: ResetState + Restore`,
bốn lối thoát Editor, và mục **Trước khi chạy** cho class trình diễn muốn có nút:

| # | Thao tác | Thiếu thì hỏng ở đâu |
|---|---|---|
| 1 | Partial `*.Editor.cs` của class, bên trong `#if UNITY_EDITOR`, `using Horcrux.Runtime.Utilities.EditorPreview;` | không compile ở Editor |
| 2 | Field `private readonly PreviewSnapshot _previewSnapshot = new();` và `static` tham chiếu object đang xem | hai lần bấm chồng nhau trả nguyên sai object |
| 3 | Nút: `EditorPreviewSession.End()` → nếu Edit mode thì đọc mốc nghỉ (`CaptureRestState`) vì `Awake` không chạy → `_previewSnapshot.Capture(transform)` → `EditorPreviewSession.Begin(StopPreview)` → `PreviewTicker.Start(hạt…)` khi Edit mode → gọi hàm diễn thật với callback kết thúc là `EditorPreviewSession.End` | bỏ `CaptureRestState` thì tween nhân lên mốc nghỉ cũ của lần Play trước |
| 4 | `StopPreview` (static): `PreviewTicker.Stop()` → hàm về nghỉ của class (`ResetState`) → `_previewSnapshot.Restore()` | thiếu `ResetState` thì tween còn sống ghi đè lên giá trị vừa trả |

## Task 3 — Dự án dùng bản Horcrux, xoá bản tạm (agent)

| File dự án | Đổi |
|---|---|
| `Entities/EditorPreviewSession.Editor.cs`, `Entities/PreviewSnapshot.Editor.cs` | **xoá** (cả `.meta`) |
| `Entities/Box/BoxAnimation.Editor.cs` | `using Horcrux.Runtime.Utilities.EditorPreview;` · xoá `s_lastParticleTime`, `TickParticles`, `AdvanceParticles` · `PreviewComplete`: khối `if (!Application.isPlaying) { s_lastParticleTime = …; EditorApplication.update += TickParticles; }` → `if (!Application.isPlaying) PreviewTicker.Start(receiveBurst, completeBurst);` · `StopPreview`: `EditorApplication.update -= TickParticles;` → `PreviewTicker.Stop();` |
| `Entities/Tile/Tile.Editor.cs` | `using Horcrux.Runtime.Utilities.EditorPreview;` · xoá `RepaintSceneView` · `PreviewFinale`: `EditorApplication.update += RepaintSceneView;` → `PreviewTicker.Start();` · `StopFinalePreview`: `EditorApplication.update -= RepaintSceneView;` → `PreviewTicker.Stop();` |

Không có GUID nào phải giữ: ba class thuần, không gắn lên prefab.

**Nghiệm thu:**
- grep `EditorPreviewSession`, `PreviewSnapshot`, `TickParticles`, `RepaintSceneView` trong `Assets/_CategoryJam` chỉ còn hai partial Editor gọi bản Horcrux; compile sạch cả Editor lẫn build Android (file Horcrux bọc `#if`).
- Mở `CategoryBox.prefab` trong Prefab Mode: bấm `Preview Complete` → nắp bay vào, hạt chạy, box vọt đi, rồi mọi giá trị trở về như trước (so `git diff` prefab rỗng sau khi lưu). Bấm lần hai khi đang diễn: màn cũ trả nguyên rồi màn mới chạy.
- Mở `Tile.prefab`: `Preview Finale` diễn tada rồi trả nguyên; đổi một số trong lúc diễn → auto save dừng màn xem, prefab không có giá trị giữa chừng.
- Vào Play khi đang xem → màn dừng, không exception.
