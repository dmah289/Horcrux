# Plan — `SortingAudit`: quét sorting layer và order của prefab, scene

Ngày 2026-10-23. Nguồn: dự án Category Jam, mô hình ba bậc ở `docs/plans/Sorting_Plan.md` của dự án (layer chỉ ở gốc entity,
con trong SortingGroup ở `Default` 0…99). Tool này là phép kiểm máy cho mô hình đó; không gọi tên type dự án.

**Phân công:** Task 1 (code) developer gõ · Task 2 (tài liệu `SortingAudit.md`) agent viết sau khi code có.

## Ngữ cảnh đã chốt

| Mục | Chốt |
|---|---|
| Người dùng | developer sau khi sửa prefab hay thêm entity; chạy khi được hỏi, không chạy nền |
| Mục tiêu | một cửa sổ, một nút Scan, mỗi dòng = một object vi phạm một luật, bấm là chọn object; đếm tổng |
| Chỗ đặt | `Assets/Horcrux/Editor/SortingAudit/`, namespace `Horcrux.Editor.SortingAudit`, asmdef `com.horcrux.editor` đã có |
| Luật | R1 layer chết · R2 con trong group khai layer ≠ Default · R3 order trong group ngoài 0…99 · R4 báo cáo gốc (không phải vi phạm) |
| Không làm | tự sửa asset · quét Canvas (mở rộng sau) · chạy theo mỗi lần asset đổi |

**Đã sai một lần (dự án nguồn):** so sorting layer ID bằng số đọc từ YAML — YAML ghi có dấu, TagManager ghi không dấu — kết luận
nhầm layer còn sống là đã chết. Tool so **qua API** `SortingLayer.IsValid(id)` trên object đã dựng, không đọc file.

## Task 1 — `SortingAuditCore.cs` và `SortingAuditWindow.cs`

### `Assets/Horcrux/Editor/SortingAudit/SortingAuditCore.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Horcrux.Editor.SortingAudit
{
    public enum SortingRule
    {
        DeadLayer = 1,
        ChildDeclaresLayer = 2,
        OrderOutOfRange = 3,
        Root = 4,
    }

    public readonly struct SortingFinding
    {
        public readonly SortingRule Rule;
        public readonly Object Asset;
        public readonly GameObject Target;
        public readonly string Message;

        public SortingFinding(SortingRule rule, Object asset, GameObject target, string message)
        {
            Rule = rule;
            Asset = asset;
            Target = target;
            Message = message;
        }
    }

    /// <summary>Quét một cây object đã dựng: renderer, SortingGroup, SpriteMask. Không chạm asset.</summary>
    public static class SortingAuditCore
    {
        public const int MaxChildOrder = 99;

        public static void Scan(GameObject root, Object asset, List<SortingFinding> into)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                Check(renderer.gameObject, renderer.sortingLayerID, renderer.sortingOrder, renderer.GetType().Name, asset, into);

            foreach (var group in root.GetComponentsInChildren<SortingGroup>(true))
                Check(group.gameObject, group.sortingLayerID, group.sortingOrder, nameof(SortingGroup), asset, into, selfIsGroup: true);

            foreach (var mask in root.GetComponentsInChildren<SpriteMask>(true))
            {
                if (!mask.isCustomRangeActive) continue;
                if (!SortingLayer.IsValid(mask.frontSortingLayerID) || !SortingLayer.IsValid(mask.backSortingLayerID))
                    into.Add(new SortingFinding(SortingRule.DeadLayer, asset, mask.gameObject, "SpriteMask: dải trỏ sorting layer không có trong TagManager"));
            }
        }

        private static void Check(GameObject go, int layerId, int order, string kind, Object asset, List<SortingFinding> into, bool selfIsGroup = false)
        {
            if (!SortingLayer.IsValid(layerId))
            {
                into.Add(new SortingFinding(SortingRule.DeadLayer, asset, go, $"{kind}: sorting layer {layerId} không có trong TagManager, Unity vẽ ở Default"));
                return;
            }

            var parentGroup = selfIsGroup
                ? (go.transform.parent != null ? go.transform.parent.GetComponentInParent<SortingGroup>(true) : null)
                : go.GetComponentInParent<SortingGroup>(true);

            if (parentGroup == null)
            {
                into.Add(new SortingFinding(SortingRule.Root, asset, go, $"{kind}: {SortingLayer.IDToName(layerId)} {order}"));
                return;
            }

            if (layerId != 0)
                into.Add(new SortingFinding(SortingRule.ChildDeclaresLayer, asset, go, $"{kind}: con của group '{parentGroup.name}' khai layer {SortingLayer.IDToName(layerId)}, group nuốt layer này"));

            if (order < 0 || order > MaxChildOrder)
                into.Add(new SortingFinding(SortingRule.OrderOutOfRange, asset, go, $"{kind}: order {order} ngoài 0…{MaxChildOrder} trong group '{parentGroup.name}'"));
        }
    }
}
```

`GetComponentInParent<SortingGroup>(true)` trên chính object có group trả về chính nó, nên với group phải hỏi từ `parent` để tìm
group **tổ tiên**. Renderer nằm cùng object với group là con của group đó (Unity sort nó trong group), nên hỏi từ chính nó là đúng.

### `Assets/Horcrux/Editor/SortingAudit/SortingAuditWindow.cs`

```csharp
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Editor.SortingAudit
{
    public sealed class SortingAuditWindow : EditorWindow
    {
        private static readonly GUIContent ScanLabel = new("Scan", "Quét mọi prefab dưới thư mục và scene đang mở");
        private static readonly GUIContent FolderLabel = new("Folder", "Thư mục gốc chứa prefab, ví dụ Assets/_CategoryJam");
        private static readonly GUIContent ShowRootsLabel = new("Show roots (R4)", "Liệt kê gốc entity: layer và order thật của dự án");

        private string _folder = "Assets";
        private bool _showRoots;
        private Vector2 _scroll;
        private readonly List<SortingFinding> _findings = new();
        private int _violations;

        [MenuItem("Horcrux/Sorting Audit")]
        private static void ShowWindow() => GetWindow<SortingAuditWindow>("Sorting Audit").Show();

        private void OnGUI()
        {
            _folder = EditorGUILayout.TextField(FolderLabel, _folder);
            _showRoots = EditorGUILayout.Toggle(ShowRootsLabel, _showRoots);

            if (GUILayout.Button(ScanLabel))
                Scan();

            EditorGUILayout.LabelField($"{_violations} vi phạm, {_findings.Count - _violations} gốc");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var f in _findings)
            {
                if (f.Rule == SortingRule.Root && !_showRoots) continue;
                if (GUILayout.Button($"[{f.Rule}] {f.Asset.name} › {f.Target.name}: {f.Message}", EditorStyles.linkLabel))
                {
                    Selection.activeObject = f.Target;
                    EditorGUIUtility.PingObject(f.Asset);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void Scan()
        {
            _findings.Clear();

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { _folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    SortingAuditCore.Scan(prefab, prefab, _findings);
            }

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
            foreach (var root in scene.GetRootGameObjects())
                SortingAuditCore.Scan(root, sceneAsset != null ? sceneAsset : root, _findings);

            _violations = 0;
            foreach (var f in _findings)
                if (f.Rule != SortingRule.Root) _violations++;
        }
    }
}
```

Quét prefab asset đã nạp (không `LoadPrefabContents`): nested instance đã được Unity dựng đầy đủ trong asset, nên R2 thấy
được cả override nằm trong prefab cha. `Selection.activeObject` trên object trong prefab asset mở được Inspector; bấm đúp để vào
Prefab Mode là việc của developer.

### Nghiệm thu Task 1

| Case | Kỳ vọng |
|---|---|
| Prefab có renderer trỏ layer đã xoá khỏi TagManager | một dòng R1 |
| Renderer `Board` nằm dưới object có `SortingGroup` | một dòng R2 |
| Renderer order 3000 trong group | một dòng R3 |
| Renderer `Box` 0 không có group tổ tiên | một dòng R4 (chỉ hiện khi bật Show roots) |
| Renderer cùng object với group, layer `Default` | không dòng |
| `SpriteMask` dải trỏ layer chết | một dòng R1 |
| Quét `Assets/_CategoryJam` sau commit 2 của dự án | 0 vi phạm |

## Task 2 — `SortingAudit.md` (agent)

Mục: "Dùng khi nào" · bảng 4 luật kèm vì sao · "Trước khi chạy" (mở scene cần quét; thư mục) · giới hạn (không quét Canvas; prefab
Lana Studio có layer riêng thì loại bằng thư mục) · "đã sai một lần" ID có dấu/không dấu.
