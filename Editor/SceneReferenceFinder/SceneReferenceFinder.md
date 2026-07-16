# Scene Reference Finder

Trả lời câu hỏi **reverse lookup** ở mức instance: *"Nếu tôi xóa GameObject / Component này, có gây null reference ở đâu trong scene/prefab không?"*

Khác với **Usage Finder** (làm ở mức asset/GUID), tool này làm trên **instance đang sống** trong scene hoặc prefab stage — reference nội bộ là `instanceID`, không nằm trong dependency graph.

## Mở cửa sổ

- Menu: **Horcrux > Scene Reference Finder**
- Context menu: right-click GameObject trong Hierarchy → **Find References In Scene (Horcrux)**

---

## Bố cục

```
┌────────────────────────────────────────────┐
│ [ Active Scene ] [ Prefab Stage ]           │ ← scope (active = xanh)
│ Target: [ ObjectField        ] [Use Selected]│
│ [ 🔍 Find References                        ] │
│ [ 🔍 filter                            ] [✕] │
│ ──────────────────────────────────────────  │
│ ▼ 🎮 EnemySpawner (2 refs)                   │
│    ▼ 📦 SpawnManager                          │
│         SpawnManager > Target ── Transform    │ ← click: ping + expand Inspector
│ ──────────────────────────────────────────  │
│ 2 references in 1 GameObject                  │ ← status bar
└────────────────────────────────────────────┘
```

---

## Scope

| Scope | Quét |
|-------|------|
| **Active Scene** | Scene đang active + toàn bộ con cháu |
| **Prefab Stage** | Prefab đang mở trong Prefab Mode (nếu có) |

- `Use Selected` tự **đồng bộ scope** với nơi target đang sống: target trong prefab stage → chọn Prefab Stage; ngược lại → Active Scene.

---

## Cơ chế

| Bước | Chi tiết |
|------|----------|
| Target set | GameObject **hoặc** Component. GO → match cả reference tới GO lẫn tới **mọi Component** của nó (xóa GO là xóa hết) |
| Match | Duyệt mọi MonoBehaviour trong scope qua `SerializedPropertyWalker`; so `objectReferenceInstanceIDValue` với set instanceID target |
| Vì sao match theo instanceID | Không cần load object; đúng cả khi field trỏ tới Component (không chỉ GO) |
| Kết quả | Cây 3 tầng `GO → Component → field`. Click field → select + ping + expand đúng component trong Inspector (`NavigationHelper`) |
| "Nothing references this" | An toàn để xóa — không tạo null ref trong scope này |

> **Chỉ quét user scripts (MonoBehaviour).** Built-in Unity component không phải nơi user vô tình tạo dangling reference.

---

## Caching Strategy (zero alloc trong OnGUI)

| Giai đoạn | Khi nào | Cache gì |
|-----------|---------|----------|
| Scan time | Bấm Find | `goLabel`, `compLabel`, `displayLabel`, `foldoutKey`, `totalRefCount` |
| Filter rebuild | Layout phase khi `_filterDirty` | `_filteredResults` |
| Draw time | Mỗi OnGUI | Chỉ update `.text` trên reusable GUIContent |

- Non-alloc buffers static: `TargetIds` (HashSet), `CompBuffer` (List) reuse giữa các lần scan.

---

## Dùng chung từ `Common/`

| Thành phần | Dùng để |
|-----------|---------|
| `SerializedPropertyWalker` | Traversal + `RefMatchVisitor` so instanceID |
| `NavigationHelper` | Select/ping/expand Inspector, mở prefab stage |
| `Static*` (Color, GUILayout, Styles) | Toolbar, nút, row styling |

---

## Cấu trúc file

| File | Vai trò |
|------|---------|
| `SceneReferenceFinderWindow.cs` | EditorWindow — scope, target, scan, filter, status, context MenuItem |
| `SceneReferenceDrawer.cs` | Vẽ cây 3 tầng (GO → Component → field) reusable GUIContent |
| `SceneReferenceScanner.cs` | Logic quét — static, `RefMatchVisitor` qua walker |
| `SceneRefResult.cs` | Data model: `SceneRefFieldResult`, `SceneRefComponentResult`, `SceneRefObjectResult` |
| `SceneReferenceFinder.md` | Tài liệu thiết kế (file này) |

---

## Lưu ý

- **Chỉ trong scope hiện tại** — reference từ scene khác / prefab khác không được quét. Muốn kiểm asset-level (prefab nào trên disk trỏ tới) → dùng **Usage Finder**.
- **Không tự động quét** — chỉ khi bấm Find. Thay đổi scene/selection không tự cập nhật.
- **Component target:** kéo thẳng một Component vào ObjectField, hoặc `Use Selected` (lấy GameObject đang chọn).
- **Không bắt `[SerializeReference]` / AssetReference** — tool này chỉ tìm `ObjectReference` trực tiếp (nguồn gây null ref khi xóa instance).
