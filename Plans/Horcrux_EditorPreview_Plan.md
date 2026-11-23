# Plan — Bộ xem trước trình diễn trên prefab (`PreviewSnapshot`, `EditorPreviewSession`, `PreviewTicker`) lên Horcrux

Bản tạm: category-jam `Assets/_CategoryJam/Scripts/Entities/` — `PreviewSnapshot.Editor.cs`, `EditorPreviewSession.Editor.cs`, `Box/BoxAnimation.Editor.cs` (`TickParticles`, `AdvanceParticles`), `Tile/Tile.Editor.cs` (`RepaintSceneView`). Không phụ thuộc Plan nào.
Kiểm: `bash Assets/Horcrux/Editor/CompileCheck/Tools~/verify.sh --csproj CategoryJam.Gameplay.csproj,com.horcrux.runtime.csproj --tests CategoryJam.EditModeTests` · sau mỗi dòng case, lưu prefab (Ctrl+S trong Prefab Mode) rồi `git diff --stat -- Assets/_CategoryJam/Addressables/Box/CategoryBox.prefab Assets/_CategoryJam/Addressables/Tile/Tile.prefab` rỗng (agent chạy).

## Kịch bản

| Tình huống | Hệ làm gì | Developer thấy |
|---|---|---|
| bấm nút xem trước trong Prefab Mode, Edit mode | cắt màn cũ, chụp cả cây, mở phiên, ticker đẩy hạt và vẽ lại Scene view, diễn | diễn tại chỗ, hạt chạy, Scene view mượt không cần rê chuột |
| diễn hết | `OnComplete` gọi `End` → thân dừng: ticker dừng → về nghỉ → `Restore` | prefab về đúng từng giá trị; `git diff` rỗng |
| bấm lại giữa chừng | `Begin` gọi `End` màn cũ trước | về nguyên tức thì, màn mới chạy từ đầu; hạt lần một dừng và xoá |
| sửa số ở Inspector, lưu prefab, đóng Prefab Mode, lưu scene, recompile, đổi Play mode giữa màn | sự kiện Editor gọi `End` trước khi lưu, đóng, reload | trả nguyên rồi mới lưu; chỉ số vừa sửa vào prefab; không exception |
| `End` khi không màn nào chạy | không làm gì | — |
| thân dừng dừng sequence, `OnComplete` gọi lại `End` | lần lồng thấy phiên đã trống, thoát | thân dừng chạy đúng một lần |
| object con hay hệ hạt tạm bị xoá tay giữa màn | `Restore` và `Tick` bỏ qua object đã huỷ | phần còn lại trả nguyên, Console sạch |
| node đang tắt lúc chụp được bật khi diễn | snapshot gồm node tắt | trả về tắt |
| hệ hạt sinh giữa màn (hiệu ứng tạm ngoài cây) | `PreviewTicker.Add` đẩy từ tick kế | hiệu ứng nổ; thiếu `Add` thì đứng im |
| class không có hạt | `PreviewTicker.Start()` chỉ vẽ lại | tween mượt |
| Play mode | caller không bật ticker; hạt tự chạy, Scene view tự vẽ | như không có bộ xem trước; ticker bật trong Play thì hạt nhanh gấp đôi |
| nút bỏ bước đọc mốc nghỉ từ prefab ở Edit mode (`Awake` không chạy) | tween nhân lên mốc nghỉ cũ | vật lệch cỡ, lệch chỗ |
| thân dừng thiếu bước về nghỉ của class | tween còn sống ghi đè giá trị vừa trả | prefab lưu trạng thái giữa chừng |

## Flow

```
  partial *.Editor.cs của class trình diễn (dự án)
  ┌───────────────────────────────────────────────────────────────────────────────────┐
  │ nút:  EditorPreviewSession.End()                                                  │
  │       CaptureRestState()              ← Edit mode không chạy Awake                │
  │       _snapshot.Capture(transform) ──────────────► PreviewSnapshot (của object)   │  PreviewSnapshot.cs
  │       EditorPreviewSession.Begin(Stop) ──────────► EditorPreviewSession (static)  │  EditorPreviewSession.cs
  │       PreviewTicker.Start(hạt…)  [Edit mode] ────► PreviewTicker (static)         │  PreviewTicker.cs
  │       hàm diễn thật(onComplete: EditorPreviewSession.End)                         │
  │       … sinh hệ hạt giữa chừng: PreviewTicker.Add(hạt)                            │
  │                                                                                   │
  │ Stop (static): PreviewTicker.Stop() → về nghỉ của class → _snapshot.Restore()     │
  └───────────────────────────────────────────────────────────────────────────────────┘
                     ▲                                 │
                     │ stop()                          │ Begin: End() màn cũ, đăng ký 5 sự kiện Editor
                     └──── EditorPreviewSession.End ◄──┘ End: gỡ 5 sự kiện, gọi stop một lần
                              ▲
     prefabSaving · prefabStageClosing · sceneSaving · beforeAssemblyReload · playModeStateChanged
  PreviewTicker mỗi EditorApplication.update: Simulate hạt đã giao (isPlaying || isPaused) · SceneView.RepaintAll

  ba file ở Runtime/Utilities/EditorPreview/, bọc #if UNITY_EDITOR, namespace Horcrux.Runtime.Utilities.EditorPreview
```

---

## GĐ1 — xong thì nút xem trước nào cũng trả prefab nguyên từng giá trị sau khi diễn

```
  nút ──Capture(transform)──► PreviewSnapshot+ (của object)
  thân dừng ──về nghỉ của class──► ──Restore()──► PreviewSnapshot+
  (phiên và tick còn chạy trên bản tạm trong dự án)
```

**Editor setup:** tạo thư mục `Runtime/Utilities/EditorPreview/` và file `PreviewSnapshot.cs` trong Project window (để có `.meta`).

`Runtime/Utilities/EditorPreview/PreviewSnapshot.cs` — file mới:

```csharp
// Ở Runtime/ chứ không Editor/: partial Editor của class trình diễn nằm trong assembly runtime, không tham chiếu assembly Editor.
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
            // Gồm node đang tắt: node được bật trong lúc diễn phải trả về tắt.
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
                // Không ném: con bị huỷ lúc diễn là thường; ném trong handler sự kiện Editor chặn các handler sau.
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

### Dự án nối vào (agent)

| File | Đổi |
|---|---|
| `Entities/PreviewSnapshot.Editor.cs` + `.meta` | **xoá** cùng lúc — class cùng tên trong namespace `CategoryJam.Gameplay.Entities` thắng `using`: còn thì compile sạch mà nút chạy bản tạm |
| `Entities/Box/BoxAnimation.Editor.cs` | thêm `using Horcrux.Runtime.Utilities.EditorPreview;` dưới `using Sirenix.OdinInspector;` |
| `Entities/Tile/Tile.Editor.cs` | thêm `using Horcrux.Runtime.Utilities.EditorPreview;` trong khối `#if UNITY_EDITOR` đầu file, dưới `using PrimeTween;` |

| Input | Làm gì | Kỳ vọng | Hỏng |
|---|---|---|---|
| `CategoryBox.prefab`, Prefab Mode, chọn gốc có `BoxAnimation` | bấm `Preview Complete`, chờ diễn hết | nắp bay vào, box nén, vọt đi; rồi nắp tắt lại, box về đúng chỗ và cỡ, khung sáng như trước | box lệch vị trí, nắp còn bật, `git diff` prefab có `m_LocalPosition`, `m_Color`, `m_IsActive` |
| `Tile.prefab`, Prefab Mode, chọn gốc có `Tile` | bấm `Preview Finale`, chờ diễn hết | tada tại chỗ, bóng co theo độ cao; rồi scale gốc, thân, bóng về như trước | bóng còn nhạt hay tắt, scale gốc khác 1 |
| đang diễn `Preview Complete` | xoá tay một object con của box_anim trong Hierarchy, chờ diễn hết | phần còn lại trả nguyên, Console không exception | `MissingReferenceException` từ `Restore` |
| bấm xem khi chưa từng xem | bấm `Preview Finale` lần đầu sau khi mở Editor | diễn bình thường | — |

---

## GĐ2 — xong thì sửa số, lưu, đóng Prefab Mode, recompile, vào Play giữa màn xem đều an toàn

```
  nút ──End()+──► ──Capture──► PreviewSnapshot ──► ──Begin(Stop)+──► EditorPreviewSession+ ──► hàm diễn(onComplete: End+)
                                                                           ▲
                        prefabSaving · prefabStageClosing · sceneSaving · beforeAssemblyReload · playModeStateChanged+
                                                                           │
  thân dừng Stop ◄──stop()+── End ◄────────────────────────────────────────┘
```

**Editor setup:** tạo `EditorPreviewSession.cs` trong Project window.

`Runtime/Utilities/EditorPreview/EditorPreviewSession.cs` — file mới:

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
        // Một màn, không danh sách: màn mới cắt màn cũ là hành vi muốn có.
        private static Action s_stop;

        /// <summary>Màn đang chạy (của bất kỳ object nào) dừng trước; stop là thân dừng của màn mới.</summary>
        public static void Begin(Action stop)
        {
            // Luôn qua End nên các += dưới không bao giờ đăng ký hai lần.
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
            // Xoá và gỡ trước khi gọi: stop dừng sequence, OnComplete gọi lại End, lần lồng thấy null và thoát.
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

### Dự án nối vào (agent)

| File | Đổi |
|---|---|
| `Entities/EditorPreviewSession.Editor.cs` + `.meta` | **xoá**; `using` đã có từ GĐ1; mọi lời gọi `EditorPreviewSession.End()`, `Begin(StopPreview)`, `Begin(StopFinalePreview)`, `.OnComplete(EditorPreviewSession.End)`, `PlayComplete(null, null, EditorPreviewSession.End)` giữ nguyên |

| Input | Làm gì | Kỳ vọng | Hỏng |
|---|---|---|---|
| `CategoryBox.prefab`, đang diễn | bấm `Preview Complete` lần hai giữa chừng | box về nguyên tức thì rồi màn mới chạy từ đầu | hai màn chồng (nắp bay hai lần), hoặc màn mới không chạy |
| `Tile.prefab`, đang diễn | sửa một số trong `finale` ở Inspector | màn dừng, tile về nguyên, số mới lưu vào prefab; `git diff` chỉ có đúng số đó | prefab có `m_LocalScale` giữa chừng |
| `CategoryBox.prefab`, đang diễn | đóng Prefab Mode bằng mũi tên `<` trên Hierarchy | không hộp thoại "unsaved", mở lại prefab thấy như trước | prefab lưu nắp đang bật |
| `CategoryBox.prefab`, đang diễn | bấm Play | vào Play không exception; thoát Play, mở lại prefab như trước | exception từ `Restore` hay tween trên object đã huỷ |
| `Tile.prefab`, đang diễn | sửa một file `.cs` bất kỳ rồi quay lại Editor (recompile) | màn dừng trước khi reload, tile về nguyên | tween "sống lại" sau reload, scale lệch |
| không màn nào chạy | gọi `EditorPreviewSession.End()` (bấm nút khi đã xem xong) | không gì xảy ra | exception |
| `CategoryBox.prefab` | bấm `Preview Complete`, đợi `Preview Complete` diễn hết | thân dừng chạy **một** lần (đặt `Debug.Log` tạm trong `StopPreview` nếu muốn thấy) | log hai dòng |

---

## GĐ3 — xong thì class trình diễn nào cũng có hạt chạy và Scene view mượt ở Edit mode mà không viết code tick

```
  nút ──End──► ──Capture──► ──Begin(Stop)──► ──Start(hạt…)+──► PreviewTicker+ ──mỗi tick: Simulate, RepaintAll+──► Scene view
                                                                      ▲
  hàm diễn ── sinh hiệu ứng tạm ──Add(hạt)+───────────────────────────┘
  thân dừng Stop: PreviewTicker.Stop()+ → về nghỉ của class → Restore
```

**Editor setup:** tạo `PreviewTicker.cs` trong Project window.

`Runtime/Utilities/EditorPreview/PreviewTicker.cs` — file mới:

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
                // null: hệ hạt giao bằng Add là object tạm, có thể bị xoá tay giữa màn xem.
                if (particles != null && (particles.isPlaying || particles.isPaused))
                    particles.Simulate(deltaTime, true, false, false);
            }

            SceneView.RepaintAll();
        }
    }
}
#endif
```

### Dự án nối vào (agent)

`Entities/Box/BoxAnimation.Editor.cs` › fields — xoá dòng:

```csharp
        private static double s_lastParticleTime;
```

`BoxAnimation.Editor.cs` › `PreviewComplete` — cũ:

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

`BoxAnimation.Editor.cs` › `SpawnPreviewCompleteFx` — thêm sau `s_previewCompleteFx.Play(true);`:

```csharp
            s_previewCompleteFx.Play(true);
            PreviewTicker.Add(s_previewCompleteFx);
```

| Chỗ | Đổi |
|---|---|
| `BoxAnimation.Editor.cs` › `StopPreview` | dòng đầu `EditorApplication.update -= TickParticles;` → `PreviewTicker.Stop();`; thứ tự giữ: `PreviewTicker.Stop()` trước, huỷ `s_previewCompleteFx` sau, rồi `ResetState`, `Restore` |
| `BoxAnimation.Editor.cs` | xoá hai method `TickParticles`, `AdvanceParticles` |
| `Entities/Tile/Tile.Editor.cs` › `PreviewFinale` | `EditorApplication.update += RepaintSceneView;` → `PreviewTicker.Start();` |
| `Tile.Editor.cs` › `StopFinalePreview` | `EditorApplication.update -= RepaintSceneView;` → `PreviewTicker.Stop();`; xoá `RepaintSceneView` |

| Input | Làm gì | Kỳ vọng | Hỏng |
|---|---|---|---|
| `CategoryBox.prefab`, Prefab Mode, **không** chọn object hạt | bấm `Preview Complete` | hạt cầu vồng lúc tile cuối, sao tab tên, hiệu ứng box xong nổ tại chỗ box lúc nắp chạm, vệt sáng kéo theo khi vọt; Scene view mượt theo tween | hạt đứng im hay chỉ chạy khi rê chuột vào Scene view; hiệu ứng box xong không nổ (thiếu `Add`) |
| `CategoryBox.prefab`, sau khi diễn hết | nhìn Hierarchy của stage | không còn object `PF_CompleteBox` tạm; hạt của box trống | object tạm còn, hạt còn treo |
| `Tile.prefab` | bấm `Preview Finale` | tada mượt không cần rê chuột | Scene view chỉ đổi khi rê chuột |
| `CategoryBox.prefab`, đang diễn | bấm `Preview Complete` lần hai | hạt lần một dừng và xoá; lần hai chạy từ đầu | hạt hai lần chồng |
| `CategoryBox.prefab`, đang diễn | xoá tay object `PF_CompleteBox` tạm trong Hierarchy | diễn tiếp, Console không exception | `MissingReferenceException` từ `Tick` |
| `Game.unity`, Play mode, một box đủ tile | box đóng nắp trong game | như trước khi có Plan: hạt và hiệu ứng chạy theo Play, ticker không bật | hạt chạy nhanh gấp đôi (ticker bật trong Play) |

---

## Chuyển dự án (agent)

| Chỗ | Đổi |
|---|---|
| `docs/Editor/EditorPreview.md` | tạo, chỉ `.md`, `Code: Runtime/Utilities/EditorPreview/` · vì sao ở `Runtime/` · Flow · bảng API ba class · "Trước khi chạy" năm bước, mỗi bước kèm thiếu thì hỏng ở đâu: partial `*.Editor.cs` trong `#if UNITY_EDITOR` + `using` · field `PreviewSnapshot` và `static` object đang xem · nút theo Flow · hệ hạt sinh giữa màn: bản tạm `HideFlags.DontSave`, `StageUtility.PlaceGameObjectInCurrentStage`, `PreviewTicker.Add`, thân dừng huỷ · thân dừng `Stop` → về nghỉ (`Stop(StopEmittingAndClear)` hạt) → `Restore` · Design decisions từ dòng `bỏ` dưới · Bẫy: `End` lồng từ `OnComplete`, hạt chỉ đẩy khi `isPlaying \|\| isPaused`, class cùng tên trong namespace dự án thắng `using` · không kịch bản chơi thử, không tên dự án |
| grep | `class PreviewSnapshot\|class EditorPreviewSession` trong `Assets/` chỉ ra file Horcrux · `TickParticles\|AdvanceParticles\|RepaintSceneView\|s_lastParticleTime` trong `Assets/_CategoryJam` ra 0 · `EditorPreviewSession\|PreviewSnapshot\|PreviewTicker` trong `Assets/_CategoryJam` chỉ ra `BoxAnimation.Editor.cs`, `Tile.Editor.cs` |
| build | một build Android cỡ nhỏ (developer, Build Settings) compile sạch: ba file bọc `#if UNITY_EDITOR`, player không thấy |
| graph | `graphify update .` ở gốc dự án |
| Plan này | xoá cùng `.meta` |
| chơi thử | `CategoryBox.prefab`: `Preview Complete` → chờ hết → bấm lại giữa chừng → sửa một số giữa chừng · `Tile.prefab`: `Preview Finale` → chờ hết → bấm Play giữa chừng · `Game.unity` Play, một box đủ tile hoàn thành như trước; `git diff --stat` hai prefab rỗng sau mỗi bước; hỏng: prefab có dòng ngoài số vừa sửa, exception, hạt nhanh gấp đôi |

## Không làm · Mở rộng sau

| | Phương án | Vì sao |
|---|---|---|
| bỏ | agent viết thẳng GĐ1–3 như code Editor khác | developer chốt tự gõ ba class (2026-10-31) |
| bỏ | đặt ở `Editor/`, assembly `com.horcrux.editor` | partial Editor của dự án nằm trong assembly runtime (partial không tách assembly), chỉ tham chiếu `com.horcrux.runtime` |
| bỏ | đặt tên file `*.Editor.cs` | khuôn đó dành cho partial của class runtime; đây là class đứng riêng, tên file là tên class |
| bỏ | giữ `internal` như bản tạm | dự án gọi từ assembly khác |
| bỏ | `Restore` ném khi object đã huỷ | ném trong handler sự kiện Editor làm các handler sau không chạy; người nhìn là người sửa được |
| bỏ | chụp thêm `ParticleSystem`, `TMP_Text`, `SortingGroup` | hai người dùng chỉ đổi transform và SpriteRenderer; hạt về nghỉ bằng `Stop(StopEmittingAndClear)` ở hàm về nghỉ của class |
| bỏ | cờ `s_ending` chống `End` lồng | thêm một trạng thái phải giữ khớp; gỡ trước khi gọi `stop` là đủ |
| bỏ | danh sách màn | hai nút trên hai prefab không bao giờ cần diễn cùng lúc |
| bỏ | `Begin` tự `Capture` snapshot | snapshot là của object (một object nhiều snapshot cho nhiều nút), phiên là của Editor — hai đời khác nhau |
| bỏ | `Start` lại với đủ danh sách thay `Add` | caller lặp lại danh sách, đồng hồ reset |
| bỏ | ticker quét `GetComponentsInChildren<ParticleSystem>` mỗi tick | hiệu ứng tạm nằm ngoài cây, quét mỗi tick cấp phát |
| bỏ | ticker gọi tên field hạt của class trình diễn (bản tạm `TickParticles`) | ticker không biết từ vựng caller; hiệu ứng sinh giữa màn không có lúc `Start` |
| bỏ | ticker thoát sớm khi không có object đang xem (bản tạm) | ticker không giữ object; `Stop` gỡ update ở mọi lối ra qua thân dừng |
| bỏ | `RepaintSceneView` riêng cho class không hạt | `Start()` không hạt, cùng một nhịp |
| bỏ | `PreviewTicker` chạy ở Play mode | ở Play hạt tự chạy, Scene view tự vẽ lại |
| bỏ | nút `[Button]` dựng sẵn trong Horcrux | nút gọi method private của class trình diễn, phải ở partial của nó |
